using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace LZW_Decompressor
{
    public class Decompressor
    {
        private int MaxBits; // Максимальное количество бит для чтения
        private int DictionarySize; // Размер словаря
        
        private ulong BitBuffer; // Буфер для хранения байтов во время чтения из файла
        private int BitCounter; // Счетчик для количества битов в буфере
        
        public void Start(string inputFilePath, string outputFilePath)
        {
            var watch = new Stopwatch();
            watch.Start();
            watch.Stop();
            watch.Reset();
            
            Console.WriteLine("Распаковка...");
            watch.Start();
            Decompress(inputFilePath, outputFilePath);
            watch.Stop();
            Console.WriteLine($"{inputFilePath} распакован за {watch.ElapsedMilliseconds} миллисекунд");
            watch.Reset();
        }
        
        public bool Decompress(string inputFilePath, string outputFilePath)
        {
            FileStream reader;
            try
            {
                reader = new FileStream(inputFilePath, FileMode.Open);
            }
            catch
            {
                throw new FileNotFoundException($"{inputFilePath} не найден");
            }
            
            FileStream writer = new FileStream(outputFilePath, FileMode.Create);

            try
            {
                BitBuffer = 0;
                BitCounter = 0;

                // Чтение первых 8 байтов из сжатого файла для того, чтобы узнать значения MaxBits и DictionarySize
                byte[] data = new byte[8];
                reader.Read(data, 0, 8);
                MaxBits = BitConverter.ToInt32(data, 0);
                DictionarySize = BitConverter.ToInt32(data, 4);
                
                int maxValue = (1 << MaxBits) - 1;
                int maxCode = maxValue - 1;
            
                int[] prefixDictionary = new int[DictionarySize];
                int[] charDictionary = new int[DictionarySize];

                int nextCode = 256;
                byte[] decodeStack = new byte[DictionarySize];
                
                int oldCode = ReadCode(reader);
                byte symbol = (byte)oldCode;
                
                // Запись первого кода
                writer.WriteByte((byte)oldCode);
                
                int newCode = ReadCode(reader);

                // Проход по всему файлу
                while (newCode != maxValue)
                {
                    int currentCode;
                    int counter;
                    
                    if (newCode >= nextCode)
                    { 
                        // Защита от случая "префикс + символ + префикс + символ ..."
                        decodeStack[0] = symbol;
                        counter = 1;
                        currentCode = oldCode;
                    }
                    else
                    {
                        counter = 0;
                        currentCode = newCode;
                    }

                    // Декодировать строку, перебирая префиксы в обратном порядке
                    while (currentCode > 255) 
                    {
                        decodeStack[counter] = (byte)charDictionary[currentCode];
                        counter++;
                        currentCode = prefixDictionary[currentCode];
                    }

                    decodeStack[counter] = (byte)currentCode;
                    symbol = decodeStack[counter];

                    // Вывод стека декодирования в поток файла
                    while (counter >= 0)
                    {
                        writer.WriteByte(decodeStack[counter]);
                        counter--;
                    }

                    // Вставка в словарь
                    if (nextCode <= maxCode)
                    {
                        prefixDictionary[nextCode] = oldCode;
                        charDictionary[nextCode] = symbol;
                        ++nextCode;
                    }

                    oldCode = newCode;
                    newCode = ReadCode(reader);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
                reader.Close();
                writer.Close();
                File.Delete(outputFilePath);
                return false;
            }

            return true;
        }
        
        // Метод для чтения кода из потока файла
        private int ReadCode(FileStream reader)
        {
            uint value;

            // Заполнение буфера битов
            while (BitCounter <= 24) 
            {
                BitBuffer |= (ulong)reader.ReadByte() << (24 - BitCounter); // Вставка байтов в буфер
                BitCounter += 8; // Увеличение счетчика битов
            }

            value = (uint)BitBuffer >> (32 - MaxBits); // Получение последнего байта из буфера
            BitBuffer <<= MaxBits; // Удаление последнего байта из буфера
            BitCounter -= MaxBits; // Уменьшение счетчика битов

            // Вывод полученного байта из потока файла
            int temp = (int)value;
            return temp;
        }
    }
}