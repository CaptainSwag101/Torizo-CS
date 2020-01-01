using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Torizo.Lunar
{
    public static class LunarCompression
    {
        internal const string lunarDllPath = "D:\\jpmac\\Documents\\GitHub\\Torizo\\Lunar Compress.dll";

        private enum CompressionMethod
        {
            DirectCopy,
            ByteFill,
            WordFill,
            SigmaFill,
            LibraryCopy,
            XorCopy,
            MinusCopy
        }

        #region Lunar Compress Functions
        [DllImport(lunarDllPath, CharSet = CharSet.Ansi)]
        private static extern bool LunarOpenFile(string filename, uint filemode);

        [DllImport(lunarDllPath)]
        private static extern bool LunarCloseFile();

        [DllImport(lunarDllPath)]
        private static extern unsafe uint LunarDecompress(byte* destination, uint startAddress, uint maxDataSize, uint format, uint format2, uint* lastRomPosition);

        [DllImport(lunarDllPath)]
        private static extern unsafe uint LunarRecompress(byte* source, byte* destination, uint dataSize, uint maxDataSize, uint format, uint format2);
        #endregion

        public static bool OpenFile(string filename, FileAccess mode)
        {
            uint filemode;
            switch (mode)
            {
                case FileAccess.Read:
                    filemode = 0;
                    break;

                case FileAccess.ReadWrite:
                    filemode = 1;
                    break;

                default:
                    filemode = 0;
                    break;
            }

            return LunarOpenFile(filename, filemode);
        }

        public unsafe static byte[] Decompress(uint startAddress, ushort maxDataSize = ushort.MaxValue)
        {
            byte[] decompressedData = new byte[maxDataSize];
            int decompressedSize = 0;
            fixed (byte* decompPtr = decompressedData)
            {
                decompressedSize = (int)LunarDecompress(decompPtr, startAddress, (uint)maxDataSize, 4, 0, (uint*)0);
            }

            byte[] result = new byte[decompressedSize];
            Array.Copy(decompressedData, result, decompressedSize);
            return result;
        }

        public unsafe static byte[] Recompress(byte[] originalData, ushort maxDataSize = ushort.MaxValue)
        {
            byte[] compressedData = new byte[maxDataSize];
            int compressedSize = 0; 
            fixed (byte* compPtr = compressedData)
            {
                fixed (byte* origPtr = originalData)
                {
                    compressedSize = (int)LunarRecompress(origPtr, compPtr, (uint)originalData.Length, (uint)maxDataSize, 4, 0);
                }
            }

            byte[] result = new byte[compressedSize];
            Array.Copy(compressedData, result, compressedSize);
            return result;
        }

        // Information about Super Metroid's decompression routine obtained from https://www.romhacking.net/documents/243/
        // I intend to entirely replace Lunar Compress with my own code at some point,
        // since FuSoYa refuses to release his source code. FuSoYa my dude, you've done some incredible work,
        // but closed-source is the antithesis of what the game hacking/modding community should be about.
        public static byte[] DecompressNew(byte[] source, int maxDataSize = int.MaxValue)
        {
            List<byte> output = new List<byte>();

            using BinaryReader reader = new BinaryReader(new MemoryStream(source));

            uint bytesWritten = 0;
            while (bytesWritten < maxDataSize)
            {
                byte raw = reader.ReadByte();

                if (raw == 0xFF)    // termination code
                    break;

                byte cmd = (byte)((raw & 0b11100000) >> 5);
                ushort val = (ushort)(raw & 0b00011111);

                if (cmd == 7)    // Extended CMD
                {
                    cmd = (byte)(val >> 2);
                    ushort val2 = (ushort)(((val & 0b00000011) << 8) | reader.ReadByte());
                    val = val2;
                }

                switch (cmd)
                {
                    case 0: // Direct Copy
                        {
                            // Copy (val + 1) bytes to the output
                            for (ushort b = 0; b < (val + 1); ++b)
                            {
                                output.Add(reader.ReadByte());
                                ++bytesWritten;

                                if (bytesWritten >= maxDataSize)
                                    break;
                            }
                            break;
                        }
                    case 1: // Byte Fill
                        {
                            // Writes the next byte (val + 1) bytes deep into the output
                            byte fill = reader.ReadByte();
                            for (ushort b = 0; b < (val + 1); ++b)
                            {
                                output.Add(fill);
                                ++bytesWritten;

                                if (bytesWritten >= maxDataSize)
                                    break;
                            }
                            break;
                        }
                    case 2: // Word Fill
                        {
                            // Writes the next word (val + 1) bytes deep into the output
                            ushort fill = reader.ReadUInt16();
                            for (ushort b = 0; b < (val + 1); ++b)
                            {
                                byte[] split = BitConverter.GetBytes(fill);
                                output.Add(split[0]);
                                ++bytesWritten;
                                ++b;

                                if (bytesWritten >= maxDataSize | b >= (val + 1))
                                    break;

                                output.Add(split[1]);
                                ++bytesWritten;

                                if (bytesWritten >= maxDataSize)
                                    break;
                            }
                            break;
                        }
                    case 3: // Sigma Fill
                        {
                            // Writes the next byte to the output, then increments that byte by 1
                            // and writes it again, etc. Writes (val + 1) times.
                            byte fill = reader.ReadByte();
                            for (ushort b = 0; b < (val + 1); ++b)
                            {
                                output.Add(fill++);
                                ++bytesWritten;

                                if (bytesWritten >= maxDataSize)
                                    break;
                            }
                            break;
                        }
                    case 4: // Library Copy
                        {
                            // Copies (val + 1) bytes from the output address in the next two bytes
                            ushort libraryAddr = reader.ReadUInt16();
                            for (ushort b = 0; b < (val + 1); ++b)
                            {
                                output.Add(output[libraryAddr + b]);
                                ++bytesWritten;

                                if (bytesWritten >= maxDataSize)
                                    break;
                            }
                            break;
                        }
                    case 5: // XORed Copy
                        {
                            // Similar to Library Copy, but the copied data is eXclusive ORed with 0b11111111
                            ushort libraryAddr = reader.ReadUInt16();
                            for (ushort b = 0; b < (val + 1); ++b)
                            {
                                output.Add((byte)(output[libraryAddr + b] ^ 0b11111111));
                                ++bytesWritten;

                                if (bytesWritten >= maxDataSize)
                                    break;
                            }
                            break;
                        }
                    case 6: // Minus Copy
                        {
                            // Subtracts the next byte from the current output length
                            // and copies (val + 1) bytes from the output. Can copy through current byte.
                            byte minus = reader.ReadByte();
                            int outputOffset = output.Count - minus;
                            for (ushort b = 0; b < (val + 1); ++b)
                            {
                                output.Add(output[outputOffset + b]);
                                ++bytesWritten;

                                if (bytesWritten >= maxDataSize)
                                    break;
                            }
                            break;
                        }
                }
            }

            uint compressedDataSize = (uint)reader.BaseStream.Position;
            return output.ToArray();
        }

        // Information about Super Metroid's decompression routine obtained from https://www.romhacking.net/documents/243/
        // I intend to entirely replace Lunar Compress with my own code at some point,
        // since FuSoYa refuses to release his source code. FuSoYa my dude, you've done some incredible work,
        // but closed-source is the antithesis of what the game hacking/modding community should be about.
        public static byte[] RecompressNew(byte[] originalData)
        {
            List<byte> result = new List<byte>();

            // This is probably gonna be super messy for the time being,
            // I'm not super great at writing compression heuristics.

            // We've got a lot of different commands we can use to compress data in Super Metroid,
            // from deduplicating immediately repeating bytes/words to finding
            // increasing sequences of bytes, or copying previous sequences
            // and performing various transformations on them.
            // And we need to find a good priority for each of them in the case where multiple
            // commands would work equally or almost equally well.

            // This will be a two-step process.
            // Part 1: Try compressing the sequence with each method, and store the resulting sequence that can be compressed
            // Part 2: Compare how long each compressable sequence is, and choose the method that provided the best compression.
            // If no method is able to compress the data, add a byte to the Direct Copy sequence and start a new initial sequence.
            // Once we find a valid compression for the current byte, add the Direct Copy command + array to the compressed output,
            // and then append the newly found compression method next.
            int bytesProcessed = 0;
            List<byte> directCopySequence = new List<byte>();
            while (bytesProcessed <= originalData.Length)
            {
                // Check if we've reached the end of the orignal data or filled up the Direct Copy sequence,
                // and append it to the compressed output if needed.
                if (directCopySequence.Count == 1023 || bytesProcessed >= originalData.Length)
                {
                    if (directCopySequence.Count > 0)
                    {
                        result.AddRange(generateCompressionBytes(CompressionMethod.DirectCopy, directCopySequence.ToArray(), 0));
                        directCopySequence.Clear();
                    }

                    // Only break if we've reached the end of the array, not if we've just filled up the direct copy sequence
                    if (bytesProcessed >= originalData.Length)
                        break;
                }


                ////// Part 1 //////
                // Process the data via each method. For speed purposes we will perform the calculations in parallel with Tasks.
                var compressionTasks = new Task<(CompressionMethod Method, byte[] CompressableSequence, int Offset)>[]
                {
                    new Task<(CompressionMethod Method, byte[] CompressableSequence, int Offset)>(() => compressWithByteOrSigmaFill(originalData, bytesProcessed)),
                    new Task<(CompressionMethod Method, byte[] CompressableSequence, int Offset)>(() => compressWithWordFill(originalData, bytesProcessed)),
                    new Task<(CompressionMethod Method, byte[] CompressableSequence, int Offset)>(() => compressWithLibraryCopy(originalData, bytesProcessed)),
                    new Task<(CompressionMethod Method, byte[] CompressableSequence, int Offset)>(() => compressWithXorCopy(originalData, bytesProcessed)),
                    new Task<(CompressionMethod Method, byte[] CompressableSequence, int Offset)>(() => compressWithMinusCopy(originalData, bytesProcessed)),
                };

                foreach (var task in compressionTasks)
                {
                    task.Start();
                }

                // Wait for all compression tasks to complete before continuing
                Task.WaitAll(compressionTasks);


                ////// Part 2 //////
                // Process each of the results of the tasks to see which is the longest,
                // and generate the compression data for the chosen method.
                // Then, increment bytesProcessed by the length of the chosen method's sequence.
                int longestCompressionLength = 0;
                int bestMethodIndex = -1;
                foreach (var task in compressionTasks)
                {
                    if (task.Result.CompressableSequence.Length > longestCompressionLength)
                    {
                        bestMethodIndex = Array.IndexOf(compressionTasks, task);
                        longestCompressionLength = task.Result.CompressableSequence.Length;
                    }
                }

                // If none of the compression methods are successful, add one byte to directCopySequence and increment bytesProcessed,
                // and do nothing else. We will wait to add the direct copy sequence to the compressed output until we either
                // max out the direct copy sequence length, reach the end of the original data, or find a new compressable sequence.
                if (bestMethodIndex == -1 || longestCompressionLength == 0)
                {
                    directCopySequence.Add(originalData[bytesProcessed++]);
                }
                else
                {
                    // First, check if there's any data in directCopySequence. If so, add a Direct Copy
                    // to the compressed output first, then clear that sequence and resume processing this new method.
                    if (directCopySequence.Count > 0)
                    {
                        result.AddRange(generateCompressionBytes(CompressionMethod.DirectCopy, directCopySequence.ToArray(), 0));
                        directCopySequence.Clear();
                    }

                    var selectedCompression = compressionTasks[bestMethodIndex].Result;
                    result.AddRange(generateCompressionBytes(selectedCompression.Method, selectedCompression.CompressableSequence, selectedCompression.Offset));
                    bytesProcessed += longestCompressionLength;
                }
            }
            // Add termination command
            result.Add(0xFF);

            return result.ToArray();
        }

        private static (CompressionMethod Method, byte[] CompressableSequence, int Offset) compressWithByteOrSigmaFill(byte[] originalData, int bytesProcessed)
        {
            List<byte> compressableSequence = new List<byte>();

            // If we don't have enough room to compress, return immediately
            if (bytesProcessed + 1 >= originalData.Length)
                return (CompressionMethod.ByteFill, compressableSequence.ToArray(), 0);

            compressableSequence.Add(originalData[bytesProcessed]);

            // Check for repeated or increasing following bytes
            bool sigma = false;
            int repeatLength = 0;
            while (true)
            {
                // 1023 or the end of the original data is our hard limit for any command
                if (repeatLength >= 1023 || bytesProcessed + repeatLength + 1 >= originalData.Length)
                    break;

                // If the next byte matches the only sequence byte, it repeats
                byte next = originalData[bytesProcessed + repeatLength + 1];
                if (!sigma && compressableSequence[0] == next)
                {
                    compressableSequence.Add(next);
                    ++repeatLength;
                }
                else if (compressableSequence[repeatLength] + 1 == next)
                {
                    sigma = true;
                    compressableSequence.Add(next);
                    ++repeatLength;
                }
                else
                {
                    break;
                }
            }

            // Do not return the initial bytes if we cannot actually compress them
            if (repeatLength == 0)
                compressableSequence.Clear();

            return (sigma ? CompressionMethod.SigmaFill : CompressionMethod.ByteFill, compressableSequence.ToArray(), 0);
        }

        private static (CompressionMethod Method, byte[] CompressableSequence, int Offset) compressWithWordFill(byte[] originalData, int bytesProcessed)
        {
            List<byte> compressableSequence = new List<byte>();

            // If we don't have enough room to compress, return immediately
            if (bytesProcessed + 2 >= originalData.Length)
                return (CompressionMethod.WordFill, compressableSequence.ToArray(), 0);

            compressableSequence.AddRange(new byte[] { originalData[bytesProcessed], originalData[bytesProcessed + 1] });

            // Check for repeated following words (two-byte sequences)
            int repeatLength = 0;
            while (true)
            {
                // 1023 or the end of the original data is our hard limit for any command
                if (repeatLength >= 1022 || bytesProcessed + repeatLength + 2 >= originalData.Length)
                    break;

                // If the next byte matches the first sequence byte, it repeats
                byte next = originalData[bytesProcessed + repeatLength + 2];
                if (compressableSequence[0] == next)
                {
                    compressableSequence.Add(next);
                    ++repeatLength;
                }
                else
                {
                    break;
                }

                // 1023 or the end of the original data is our hard limit for any command
                // Yes, we do need to check this twice
                if (repeatLength >= 1022 || bytesProcessed + repeatLength + 2 >= originalData.Length)
                    break;

                // If the next byte matches the only sequence byte, it repeats
                byte next2 = originalData[bytesProcessed + repeatLength + 2];
                if (compressableSequence[1] == next2)
                {
                    compressableSequence.Add(next2);
                    ++repeatLength;
                }
                else
                {
                    break;
                }
            }

            // Do not return the initial bytes if we cannot actually compress them
            if (repeatLength <= 1)
                compressableSequence.Clear();

            return (CompressionMethod.WordFill, compressableSequence.ToArray(), 0);
        }

        private static (CompressionMethod Method, byte[] CompressableSequence, int Offset) compressWithLibraryCopy(byte[] originalData, int bytesProcessed)
        {
            List<byte> compressableSequence = new List<byte>();

            // If we don't have enough room to compress, return immediately
            if (bytesProcessed + 1 >= originalData.Length)
                return (CompressionMethod.LibraryCopy, compressableSequence.ToArray(), 0);

            compressableSequence.Add(originalData[bytesProcessed]);

            // Check for this sequence previously in the uncompressed data
            // Find the final occurrence of the sequence in the original data, then each time
            // we cannot find a longer matching sequence at the current position, move on to the
            // previous occurrence of the current sequence and see if we can find a longer sequence starting from there.
            int foundIndex = -1;
            while (true)
            {
                int lastFoundIndex = foundIndex;
                foundIndex = originalData.LastIndexOf(compressableSequence, bytesProcessed - 1);

                // If we can't find any more 
                if (foundIndex == -1 || compressableSequence.Count >= 1023 || bytesProcessed + compressableSequence.Count >= originalData.Length)
                {
                    if (foundIndex == -1)
                    {
                        compressableSequence.RemoveAt(compressableSequence.Count - 1);
                        foundIndex = lastFoundIndex;
                    }

                    break;
                }

                compressableSequence.Add(originalData[bytesProcessed + compressableSequence.Count]);
            }

            // Do not return the initial bytes if we cannot actually compress them
            if(foundIndex == -1 || compressableSequence.Count < 3)
                compressableSequence.Clear();

            return (CompressionMethod.LibraryCopy, compressableSequence.ToArray(), foundIndex);
        }

        private static (CompressionMethod Method, byte[] CompressableSequence, int Offset) compressWithXorCopy(byte[] originalData, int bytesProcessed)
        {
            List<byte> compressableSequence = new List<byte>();

            // If we don't have enough room to compress, return immediately
            if (bytesProcessed + 1 >= originalData.Length)
                return (CompressionMethod.XorCopy, compressableSequence.ToArray(), 0);

            compressableSequence.Add(originalData[bytesProcessed]);

            // Check for this sequence previously in the uncompressed data
            // Find the final occurrence of the sequence in the original data, then each time
            // we cannot find a longer matching sequence at the current position, move on to the
            // previous occurrence of the current sequence and see if we can find a longer sequence starting from there.
            int foundIndex = -1;
            while (true)
            {
                int lastFoundIndex = foundIndex;
                List<byte> xorSequence = new List<byte>(compressableSequence);
                for (int i = 0; i < xorSequence.Count; ++i)
                {
                    xorSequence[i] = (byte)(xorSequence[i] ^ 0b11111111);
                }

                foundIndex = originalData.LastIndexOf(xorSequence, bytesProcessed - 1);

                // If we can't find any more 
                if (foundIndex == -1 || compressableSequence.Count >= 1023 || bytesProcessed + compressableSequence.Count >= originalData.Length)
                {
                    if (foundIndex == -1)
                    {
                        compressableSequence.RemoveAt(compressableSequence.Count - 1);
                        foundIndex = lastFoundIndex;
                    }

                    break;
                }

                compressableSequence.Add(originalData[bytesProcessed + compressableSequence.Count]);
            }

            // Do not return the initial bytes if we cannot actually compress them
            if (foundIndex == -1 || compressableSequence.Count < 3)
                compressableSequence.Clear();

            return (CompressionMethod.XorCopy, compressableSequence.ToArray(), foundIndex);
        }

        private static (CompressionMethod Method, byte[] CompressableSequence, int Offset) compressWithMinusCopy(byte[] originalData, int bytesProcessed)
        {
            List<byte> compressableSequence = new List<byte>();

            // If we don't have enough room to compress, return immediately
            if (bytesProcessed + 1 >= originalData.Length)
                return (CompressionMethod.MinusCopy, compressableSequence.ToArray(), 0);

            compressableSequence.Add(originalData[bytesProcessed]);

            // Check for this sequence previously in the uncompressed data
            // Find the final occurrence of the sequence in the original data, then each time
            // we cannot find a longer matching sequence at the current position, move on to the
            // previous occurrence of the current sequence and see if we can find a longer sequence starting from there.
            int foundIndex = -1;
            while (true)
            {
                int lastFoundIndex = foundIndex;
                foundIndex = originalData.LastIndexOf(compressableSequence, bytesProcessed - 1);

                // If we can't find any more within a single byte range
                if (foundIndex == -1 || bytesProcessed - foundIndex > byte.MaxValue || compressableSequence.Count >= 1023 || bytesProcessed + compressableSequence.Count >= originalData.Length)
                {
                    if (foundIndex == -1 || bytesProcessed - foundIndex > byte.MaxValue)
                    {
                        compressableSequence.RemoveAt(compressableSequence.Count - 1);
                        foundIndex = lastFoundIndex;
                    }

                    break;
                }

                compressableSequence.Add(originalData[bytesProcessed + compressableSequence.Count]);
            }

            // Do not return the initial bytes if we cannot actually compress them
            if (foundIndex == -1 || compressableSequence.Count < 2)
                compressableSequence.Clear();

            return (CompressionMethod.MinusCopy, compressableSequence.ToArray(), bytesProcessed - foundIndex);
        }

        private static byte[] generateCompressionBytes(CompressionMethod command, byte[] sequence, int offset)
        {
            List<byte> compressed = new List<byte>();

            byte cmd = 0;
            ushort val = (ushort)(sequence.Length - 1);
            List<byte> data = new List<byte>();

            switch (command)
            {
                case (CompressionMethod.DirectCopy):
                    cmd = 0b000;
                    data.AddRange(sequence);
                    break;

                case (CompressionMethod.ByteFill):
                    cmd = 0b001;
                    data.Add(sequence[0]);
                    break;

                case (CompressionMethod.WordFill):
                    cmd = 0b010;
                    data.Add(sequence[0]);
                    data.Add(sequence[1]);
                    break;

                case (CompressionMethod.SigmaFill):
                    cmd = 0b011;
                    data.Add(sequence[0]);
                    break;

                case (CompressionMethod.LibraryCopy):
                    cmd = 0b100;
                    data.AddRange(BitConverter.GetBytes((ushort)offset));
                    break;

                case (CompressionMethod.XorCopy):
                    cmd = 0b101;
                    data.AddRange(BitConverter.GetBytes((ushort)offset));
                    break;

                case (CompressionMethod.MinusCopy):
                    cmd = 0b110;
                    data.Add((byte)offset);
                    break;
            }

            if (val < 32)    // Normal command
            {
                byte raw = (byte)(cmd << 5);
                raw |= (byte)val;
                compressed.Add(raw);
            }
            else    // Extended command
            {
                ushort raw = 0b111 << 13;
                raw |= (ushort)(cmd << 10);
                raw |= (ushort)(val & 1023);
                byte[] rawBytes = BitConverter.GetBytes(raw);
                Array.Reverse(rawBytes);
                compressed.AddRange(rawBytes);
            }
            compressed.AddRange(data);

            return compressed.ToArray();
        }


    }

    internal static class ListExtensions
    {
        public static int LastIndexOf(this byte[] list, List<byte> seq, int index, byte compressionLevel = 0)
        {
            if (list.Length - index < seq.Count || index < 0)
            {
                return -1;
            }

            // Start at the end of the index and work backwards
            if (compressionLevel == 0)  // Max compression, very slow for large files
            {
                int foundIndex = Array.LastIndexOf(list, seq[0], index);
                while (foundIndex > -1)
                {
                    // Check if the whole sequence matches
                    bool found = true;
                    for (int i = 0; i < seq.Count; ++i)
                    {
                        if (list[foundIndex + i] != seq[i])
                        {
                            found = false;
                            break;
                        }
                    }

                    if (found)
                    {
                        // If the whole sequence matches, we've found the index we need
                        return foundIndex;
                    }
                    else
                    {
                        // If not, start the search over again from the previous found index if possible
                        if (foundIndex - 1 < 0)
                            return -1;

                        foundIndex = Array.LastIndexOf(list, seq[0], foundIndex - 1);
                    }
                }
            }
            else if (compressionLevel == 1) // Less compression, way faster for large files
            {
                int foundIndex = Array.LastIndexOf(list, seq[0], index);
                if (foundIndex == -1)
                    return -1;

                bool found = true;
                for (int i = 0; i < seq.Count; ++i)
                {
                    if (list[foundIndex + i] != seq[i])
                    {
                        found = false;
                        break;
                    }
                }

                if (found)
                {
                    return foundIndex;
                }
            }

            return -1;
        }
    }
}
