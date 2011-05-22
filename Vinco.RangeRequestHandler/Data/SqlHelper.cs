using System;
using System.Data.SqlClient;
using System.Data;
using System.Configuration;
using System.IO;

using System.Collections.Generic;
using System.Dynamic;
using Vinco.Utils;


namespace Vinco.Data
{
    public static class SqlHelper
    {
        /// <summary>
        /// 4KB
        /// </summary>
        private const int READ_BUFFER_LENGTH = 4096;

        /// <summary>
        /// 512 MB
        /// </summary>
        private const int WRITE_BUFFER_LENGTH = 524288000;

        public static dynamic RecordToExpando(this IDataReader reader)
        {
            dynamic expando = new ExpandoObject();
            var data = expando as IDictionary<string, object>;
            for (int i = 0; i < reader.FieldCount; i++)
            {
                data.Add(reader.GetName(i), reader[i]);
            }
            return expando;
        }

        public static IDataReader FindFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }
            SqlCommand command = null;
            SqlConnection connection = null;

            connection = new SqlConnection(ConnectionString);
            command = new SqlCommand("files_GetByName", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(CreateInputParam("@Name", SqlDbType.NVarChar, fileName));
            connection.Open();

            IDataReader reader = command.ExecuteReader();
            reader.Read();

            return reader;
        }

        public static long Read(int fileId, Stream stream, long offset, long length, Func<bool> continueAction)
        {
            if (fileId <= 0)
            {
                throw new ArgumentNullException("fileId");
            }
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            if (offset < 0)
            {
                throw new IndexOutOfRangeException("offset");
            }
            if (length < 0)
            {
                throw new IndexOutOfRangeException("length");
            }
            long totalBytesSent = 0L;
            SqlCommand command = null;
            SqlConnection connection = null;
            try
            {
                connection = new SqlConnection(ConnectionString);
                command = new SqlCommand("streams_GetByFileId", connection);
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(CreateInputParam("@FileId", SqlDbType.Int, fileId));
                connection.Open();
                using (var reader = command.ExecuteReader(CommandBehavior.SequentialAccess))
                {
                    int columnIndex = 1;
                    long seekReadLength = 0L;
                    bool continueRead = true;
                    byte[] buffer = new Byte[READ_BUFFER_LENGTH];

                    // Read rows (partitions)
                    while (reader.Read())
                    {
                        if (!continueRead)
                        {
                            break;
                        }

                        // Get partition length
                        long seekReadPosition = reader.GetInt64(0);
                        seekReadLength += seekReadPosition;

                        // Find start partition
                        if (offset < seekReadLength)
                        {
                            long dataRead = 0L;
                            long dataIndex = 0L;

                            // Find index from which to begin read.
                            if (offset > 0)
                            {
                                dataIndex = Seek(seekReadLength, seekReadPosition, offset);

                                // Reset offset next row it should start form the begining.
                                offset = 0L;
                            }

                            // Find length to copy bytes into buffer
                            int bytesToRead = buffer.Length;
                            if (length > 0)
                            {
                                bytesToRead = FindLengthToCopy(length, buffer.LongLength);
                            }

                            // Read bytes from database
                            while ((dataRead = reader.GetBytes(columnIndex, dataIndex, buffer, 0, bytesToRead)) > 0)
                            {
                                // Terminate action check.
                                if (continueAction != null)
                                {
                                    continueRead = continueAction();
                                }
                                if (!continueRead)
                                {
                                    break;
                                }

                                // Write bytes into buffer
                                dataIndex += dataRead;
                                totalBytesSent += dataRead;
                                stream.Write(buffer, 0, (int)dataRead);

                                // Find remaining length to copy bytes into buffer
                                if (length > 0)
                                {
                                    length = (length - dataRead);
                                    bytesToRead = FindLengthToCopy(length, buffer.LongLength);
                                    if (length == 0)
                                    {
                                        continueRead = false;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                if (command != null)
                {
                    command.Dispose();
                    command = null;
                }
                if (connection != null)
                {
                    connection.Dispose();
                    connection = null;
                }
            }
            return totalBytesSent;
        }

        public static void Write(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentNullException("fileName");
            }
            using (FileStream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                SqlConnection connection = null;
                SqlCommand fileCommand = null;
                SqlCommand streamCommand = null;
                try
                {
                    string name = Path.GetFileName(fileName);
                    string contentType = MimeMapping.GetMimeMapping(name);
                    string extension = Path.GetExtension(name).TrimStart(new char[] { '.' });

                    connection = new SqlConnection(ConnectionString);
                    fileCommand = new SqlCommand("dbo.Insert_File", connection);
                    fileCommand.CommandType = CommandType.StoredProcedure;

                    fileCommand.Parameters.Add(CreateInputParam("@Name", SqlDbType.NVarChar, name));
                    fileCommand.Parameters.Add(CreateInputParam("@ContentType", SqlDbType.NVarChar, contentType));
                    fileCommand.Parameters.Add(CreateInputParam("@Extension", SqlDbType.NVarChar, extension));
                    fileCommand.Parameters.Add(CreateInputParam("@Length", SqlDbType.BigInt, stream.Length));

                    connection.Open();

                    int fileId = (int)fileCommand.ExecuteScalar();

                    using (BinaryReader reader = new BinaryReader(stream))
                    {
                        streamCommand = new SqlCommand("dbo.Insert_Stream", connection);
                        streamCommand.CommandType = CommandType.StoredProcedure;

                        streamCommand.Parameters.Add(CreateInputParam("@FileId", SqlDbType.Int, fileId));

                        var partitionParameter = CreateInputParam("@Partition", SqlDbType.TinyInt, 0);
                        streamCommand.Parameters.Add(partitionParameter);

                        var lengthParameter = CreateInputParam("@Length", SqlDbType.BigInt, 0);
                        streamCommand.Parameters.Add(lengthParameter);

                        var blobParameter = CreateInputParam("@BlobData", SqlDbType.VarBinary, null);
                        streamCommand.Parameters.Add(blobParameter);

                        int offset = 0;
                        int partition = 0;
                        byte[] buffer = reader.ReadBytes(WRITE_BUFFER_LENGTH);

                        while (buffer.Length > 0)
                        {
                            blobParameter.Value = buffer;
                            lengthParameter.Value = buffer.LongLength;

                            int result = streamCommand.ExecuteNonQuery();

                            offset += buffer.Length;

                            partition++;

                            partitionParameter.Value = partition;

                            buffer = reader.ReadBytes((int)GetNextChunkSize((long)offset, stream.Length));
                        }
                    }
                }
                finally
                {
                    if (fileCommand != null)
                    {
                        fileCommand.Dispose();
                        fileCommand = null;
                    }
                    if (streamCommand != null)
                    {
                        streamCommand.Dispose();
                        streamCommand = null;
                    }
                    if (connection != null)
                    {
                        connection.Dispose();
                        connection = null;
                    }
                }
            }
        }

        private static int FindLengthToCopy(long length, long bufferLength)
        {
            if (length > bufferLength)
            {
                return (int)bufferLength;
            }
            return (int)length;
        }

        private static long Seek(long readLength, long readPosition, long offset)
        {
            return (offset - (readLength - readPosition));
        }

        private static long GetNextChunkSize(long offset, long lenght)
        {
            long num2 = WRITE_BUFFER_LENGTH;
            if (WRITE_BUFFER_LENGTH > (lenght - offset))
            {
                num2 = lenght - offset;
            }
            return num2;
        }
        
        #region Private methods

        private static SqlParameter CreateInputParam(string paramName, SqlDbType dbType, object objValue)
        {
            SqlParameter parameter = new SqlParameter(paramName, dbType);
            if (objValue == null)
            {
                parameter.IsNullable = true;
                parameter.Value = DBNull.Value;
                return parameter;
            }
            parameter.Value = objValue;
            return parameter;
        }

        #endregion

        public static string ConnectionString
        {
            get
            {
                string connectionString = ConfigurationManager.ConnectionStrings["DbConnectionString"].ConnectionString;
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new InvalidOperationException("connectionString");
                }
                return connectionString;
            }
        }
    }
}