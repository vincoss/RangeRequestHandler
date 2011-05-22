using System;
using System.IO;


namespace Vinco.RangeConsoleClient.Code
{
    public static class FileComparer
    {
        public static bool FileEquals(string originalFilePath, string copyFilePath)
        {
            if (string.IsNullOrWhiteSpace(originalFilePath))
            {
                throw new ArgumentNullException("originalFilePath");
            }
            if (string.IsNullOrWhiteSpace(copyFilePath))
            {
                throw new ArgumentNullException("copyFilePath");
            }
            using (var left = new FileStream(originalFilePath, FileMode.Open, FileAccess.Read))
            using (var right = new FileStream(copyFilePath, FileMode.Open, FileAccess.Read))
            {
                return CompareStreams(left, right);
            }
        }

        #region Compare files helper methods

        private static bool CompareStreams(Stream leftStream, Stream rightStream)
        {
            const int BUFFER_SIZE = 4096;
            byte[] left = new byte[BUFFER_SIZE];
            byte[] right = new byte[BUFFER_SIZE];
            while (true)
            {
                int leftLength = leftStream.Read(left, 0, BUFFER_SIZE);
                int rightLength = rightStream.Read(right, 0, BUFFER_SIZE);

                if (leftLength != rightLength)
                {
                    return false;
                }
                if (leftLength == 0)
                {
                    return true;
                }
                bool result = CompareBytes(left, left.Length, right, right.Length);
                if (!result)
                {
                    return false;
                }
            }
        }

        private static bool CompareBytes(byte[] bufferOne, int lengthOne, byte[] bufferTwo, int lengthTwo)
        {
            if (lengthOne != lengthTwo)
            {
                return false;
            }
            for (int i = 0; i < lengthOne; i++)
            {
                if (bufferOne[i] != bufferTwo[i])
                {
                    return false;
                }
            }
            return true;
        }

        #endregion
    }
}
