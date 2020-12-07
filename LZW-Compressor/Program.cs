using System;

// Кононов К.Г., 11-809, 3 курс, 2020
namespace LZW_Compressor
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            Compressor compressor;

            if (args.Length != 4)
            {
                Console.WriteLine("Wrong number of arguments");
                return;
            }
            if (args[0].ToLower() == "-i" && args[2].ToLower() == "-o")
            {
                compressor = new Compressor();
                compressor.Start(args[1], args[3]);
            }
            else
                Console.WriteLine("Invalid argument command given. Exiting.");
        }
    }
}