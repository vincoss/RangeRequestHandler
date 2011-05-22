using System;
using System.IO;


namespace Vinco
{
    public interface IFileInfo
    {
        string Name { get; }

        long Length { get; }

        string FullName { get; }

        DateTime LastModified { get; }

        Stream OpenRead();
    }

    public class DatabaseFileInfo : IFileInfo
    {
        public DatabaseFileInfo(string name, long length, DateTime lastModified)
        {
            Name = name;
            Length = length;
            LastModified = new DateTime(lastModified.Year, lastModified.Month, lastModified.Day, lastModified.Hour, lastModified.Minute, lastModified.Second, 0);
        }

        public string Name { get; private set; }

        public long Length { get; private set; }

        public string FullName
        {
            get { throw new NotSupportedException(); }
        }

        public DateTime LastModified { get; private set; }

        public Stream OpenRead()
        {
            throw new NotSupportedException();
        }
    }

    public class FileInfoWrapper : IFileInfo
    {
        private readonly FileInfo _fileInfo;

        public FileInfoWrapper(FileInfo info)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }
            _fileInfo = info;

            Name = info.Name;
            Length = info.Length;
            FullName = info.FullName;
            LastModified = new DateTime(info.LastWriteTime.Year, info.LastWriteTime.Month, info.LastWriteTime.Day, info.LastWriteTime.Hour, info.LastWriteTime.Minute, info.LastWriteTime.Second, 0);
        }

        public string Name { get; private set; }

        public long Length { get; private set; }

        public string FullName { get; private set; }

        public DateTime LastModified { get; private set; }

        public Stream OpenRead()
        {
            return _fileInfo.OpenRead();
        }
    }
}
