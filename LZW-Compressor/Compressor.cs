﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace LZW_Compressor
{
    public class Compressor
    {
        private int MaxBits; // Максимальное количество бит для чтения
        private int HashBit; // Используется для алгоритма хэширования с целью поиска индекса
        private int DictionarySize; // Размер словаря
        
        private int[] CodeDictionary; // Словарь кодов
        private int[] PrefixDictionary; // Словарь префиксов
        private int[] CharDictionary; // Словарь символов
        
        private ulong BitBuffer; // Буфер для хранения байтов во время чтения из файла
        private int BitCounter; // Счетчик для количества битов в буфере
        
        /* Метод для определения наиболее эффективного кол-ва бит для чтения
        Величина MaxBits зависит от размера входного текстового файла
        Данный метод гарантирует наиболее ёмкое сжатие, которое осуществляется
        в методе Compress(), но за ёмкость сжатия приходится платить временем сжатия */
        private int[] FindEffectiveMaxBitsForFileSize(long size)
        {
            var dict = new Dictionary<int, int>
            {
                {9, (int)Math.Pow(2, 12)},
                {10, (int)Math.Pow(2, 13)},
                {11, (int)Math.Pow(2, 14)},
                {12, (int)Math.Pow(2, 15)},
                {13, (int)Math.Pow(2, 16)},
                {14, (int)Math.Pow(2, 17)},
                {15, (int)Math.Pow(2, 18)},
                {16, (int)Math.Pow(2, 19)},
                {17, (int)Math.Pow(2, 20)},
                {18, (int)Math.Pow(2, 21)},
                {19, (int)Math.Pow(2, 22)},
                {20, (int)Math.Pow(2, 23)},
                {21, (int)Math.Pow(2, 24)},
            };
            
            int maxBits = dict.FirstOrDefault(x => size < x.Value).Key;

            if (maxBits == 0)
            {
                maxBits = dict.Last().Key;
            }
            
            return new[] { maxBits, 256 * (int)(Math.Pow(2, maxBits) - 1)};
        }
        
        public void Start(string inputFilePath, string outputFilePath)
        {
            var watch = new Stopwatch();
            watch.Start();
            watch.Stop();
            watch.Reset();
            
            Console.WriteLine("Сжатие...");
            watch.Start();
            long sourceLength = new FileInfo(inputFilePath).Length;
            var config = FindEffectiveMaxBitsForFileSize(sourceLength);
            int maxBits = config[0];
            int tableSize = config[1];
            Compress(inputFilePath, outputFilePath, maxBits, tableSize);
            watch.Stop();
            long encodedLength = new FileInfo($"{outputFilePath}.lzw").Length;
            string ratio = String.Format("{0:P1}", (double) encodedLength / sourceLength);
            Console.WriteLine($"{inputFilePath} был сжат за {watch.ElapsedMilliseconds} миллисекунд\nРазмер сжатого файла: {encodedLength}\nКоэффициент сжатия: {ratio}");
            watch.Reset();
        }
        
        public bool Compress(string inputFilePath, string outputFilePath, int maxBits, int dictionarySize)
        {
            FileStream reader = new FileStream(inputFilePath, FileMode.Open);
            FileStream writer = new FileStream(outputFilePath + ".lzw", FileMode.Create);

            MaxBits = maxBits;
            HashBit = MaxBits - 8;
            DictionarySize = dictionarySize;
            
            var maxValue = (int)Math.Pow(2, MaxBits) - 1;
            var maxCode = maxValue - 1;
            
            CodeDictionary = new int[DictionarySize];
            PrefixDictionary = new int[DictionarySize];
            CharDictionary = new int[DictionarySize];

            try
            {
                BitBuffer = 0;
                BitCounter = 0;

                int nextCode = 256;
                int symbol;
                int prefix;

                /* Запись первых 8 байтов в файл, которые обозначают следующее:
                1. max_bits - 4 байта, максимальное кол-во бит для чтения
                2. dictionary_size - 4 байта, размер словаря
                Это необходимо для корректной работы метода деархивации, 
                который заранее ничего не знает о MaxBits и DictionarySize */
                byte[] max_bits = BitConverter.GetBytes(MaxBits);
                writer.Write(max_bits, 0, max_bits.Length);
                byte[] dictionary_size = BitConverter.GetBytes(DictionarySize);
                writer.Write(dictionary_size, 0, dictionary_size.Length);
                
                // Заполнение словаря кодов значениями -1, что означает "пусто"
                for (int i = 0; i < DictionarySize; i++)
                    CodeDictionary[i] = -1;

                // Чтение первого символа
                prefix = reader.ReadByte();

                // Чтение символов до конца файла
                while((symbol = reader.ReadByte()) != -1)
                {
                    var index = FindMatch(prefix, symbol);

                    /* Если в словаре есть такая последовательность символов,
                    то префикс приравнивается этой последовательности,
                    иначе в словарь добавляется новый код */
                    if (CodeDictionary[index] != -1)
                    {
                        prefix = CodeDictionary[index];
                    }
                    else
                    {
                        if (nextCode <= maxCode)
                        {
                            CodeDictionary[index] = nextCode++;
                            PrefixDictionary[index] = prefix;
                            CharDictionary[index] = (byte)symbol;
                        }
                        
                        WriteCode(writer, prefix); // Вывод префикса в поток
                        prefix = symbol; // Префикс приравнивается последнему прочитанному символу
                    }
                }

                // Завершающие операции после прекращения получения новых символов, т.е. достигнут конец файла
                WriteCode(writer, prefix);
                WriteCode(writer, maxValue);
                WriteCode(writer, 0);
                
                reader.Close();
                writer.Close();
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
        
        
        // Метод для записи кода в поток файла
        private void WriteCode(FileStream writer, int code)
        {
            BitBuffer |= (ulong)code << (32 - MaxBits - BitCounter); // Освобождение места и вставка кода в буфер
            BitCounter += MaxBits; // Увеличение счетчика битов

            // Запись всех байтов кода
            while (BitCounter >= 8) 
            {
                writer.WriteByte((byte)((BitBuffer >> 24) & 255)); // Запись байта из буфера битов
                BitBuffer <<= 8; // Убрать записанный байт из буфера
                BitCounter -= 8; // Уменьшение счетчика битов
            }
        }
        
        // Метод для нахождения индекса префикса + символа
        private int FindMatch(int prefix, int symbol)
        {
            int index = (int)(symbol * Math.Pow(2, HashBit) + prefix);
            int offset = (index == 0) ? 1 : DictionarySize - index;

            while (true)
            {
                if (CodeDictionary[index] == -1 || PrefixDictionary[index] == prefix && CharDictionary[index] == symbol)
                {
                    return index;
                }
                
                index -= offset;
                if (index < 0)
                {
                    index += DictionarySize;
                }
            }
        }
    }
}