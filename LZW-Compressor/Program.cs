using System;

namespace LZW_Compressor
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            Compressor decompressor;
            
            if (args.Length != 4)
            {
                Console.WriteLine("Wrong number of arguments");
                return;
            }
            if (args[0].ToLower() == "-i" && args[2].ToLower() == "-o")
            {
                decompressor = new Compressor();
                decompressor.Start(args[1], args[3]);
            }
            else
                Console.WriteLine("Invalid argument command given. Exiting.");
        }
    }
}