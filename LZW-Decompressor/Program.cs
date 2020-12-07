using System;
using System.Text.RegularExpressions;

namespace LZW_Decompressor
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            Decompressor decompressor;
            
            if (args.Length != 4)
            {
                Console.WriteLine("Wrong number of arguments");
                return;
            }
            if (args[0].ToLower() == "-i" && args[2].ToLower() == "-o")
            {
                decompressor = new Decompressor();
                decompressor.Start(args[1], args[3]);
            }
            else
                Console.WriteLine("Invalid argument command given. Exiting.");
        }
    }
}