using System;
using System.Runtime.InteropServices;


namespace Vinco
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ByteRange
    {
        public long Offset;
        public long Length;
    }
}
