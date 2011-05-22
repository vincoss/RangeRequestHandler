using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using Vinco.RangeConsoleClient.Code;


namespace Vinco.RangeConsoleClient
{
    class Program
    {
        static void Main(string[] args)
        {
            CompareFiles();

            Console.WriteLine("Done...");
            Console.Read();
        }

        private static void CompareFiles()
        {
            string originalPath = @"";
            string copyPath = @"";

            Console.WriteLine("Begin compare...");

            bool result = FileComparer.FileEquals(originalPath, copyPath);

            Console.WriteLine("Files are the same? : {0}", result);
        }
    }
}