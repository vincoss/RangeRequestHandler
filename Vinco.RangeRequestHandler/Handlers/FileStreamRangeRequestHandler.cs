using System;
using System.IO;
using System.Web;


namespace Vinco.Handlers
{
    public class FileStreamRangeRequestHandler : RangeRequestHandler
    {
        protected const int BYTES_TO_READ = 4092;

        protected override void FindFileInfo(System.Web.HttpContextBase httpContext)
        {
            // TODO:

            HttpRequestBase request = httpContext.Request;

            string physicalPath = "";// TODO: Path here //request.PhysicalPath;

            FileInfo info = GetFileInfo(physicalPath);
            if (info != null)
            {
                RequestFileInfo = new FileInfoWrapper(info);
            }
        }

        protected override long Write(Stream stream, long offset, long length, Func<bool> continueAction)
        {
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
            if (length > RequestFileInfo.Length)
            {
                throw new IndexOutOfRangeException("length");
            }
            if (offset > (RequestFileInfo.Length - 1))
            {
                throw new IndexOutOfRangeException("offset");
            }
            long totalBytesSent = 0L;
            using (Stream sourceStream = RequestFileInfo.OpenRead())
            {
                bool continueRead = true;
                byte[] bytes = new byte[BYTES_TO_READ];

                // Find start position
                sourceStream.Seek(offset, SeekOrigin.Begin);

                // Get read length if not specified
                if (length == 0)
                {
                    length = sourceStream.Length;
                }
                long totalBytesToRead = length;

                while (totalBytesToRead > 0)
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

                    // Find length to copy bytes into buffer
                    int bytesToRead = (totalBytesToRead > BYTES_TO_READ) ? BYTES_TO_READ : (int)totalBytesToRead;

                    // Read
                    long bytesRead = sourceStream.Read(bytes, 0, bytesToRead);

                    // Write bytes into buffer
                    stream.Write(bytes, 0, (int)bytesRead); 
                    stream.Flush();

                    // Adjust
                    totalBytesToRead = (totalBytesToRead - bytesRead);
                    totalBytesSent = (totalBytesSent + bytesRead);
                }
            }
            return totalBytesSent;
        }

        private static FileInfo GetFileInfo(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException("path");
            }
            if (!File.Exists(path))
            {
                return null;
            }
            return new FileInfo(path);
        }
    }
}