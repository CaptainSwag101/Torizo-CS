using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Torizo.Lunar
{
    public static class LunarCompression
    {
        internal const string lunarDllPath = "D:\\jpmac\\Documents\\GitHub\\Torizo\\Lunar Compress.dll";

        #region Lunar Compress Functions
        [DllImport(lunarDllPath, CharSet = CharSet.Ansi)]
        private static extern bool LunarOpenFile(string filename, uint filemode);

        [DllImport(lunarDllPath)]
        private static extern bool LunarCloseFile();

        [DllImport(lunarDllPath)]
        private static extern unsafe uint LunarDecompress(byte* destination, uint startAddress, uint maxDataSize, uint format, uint format2, uint* lastRomPosition);
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
            fixed (byte* decompPtr = decompressedData)
            {
                uint decompressedSize = LunarDecompress(decompPtr, startAddress, (uint)maxDataSize, 4, 0, (uint*)0);
                if (decompressedSize > 0)
                {
                    return decompressedData;
                }
            }

            return null;
        }

        // Information about Super Metroid's decompression routine obtained from https://www.romhacking.net/documents/243/
        // I intend to entirely replace Lunar Compress with my own code at some point,
        // since FuSoYa refuses to release his source code. FuSoYa my dude, you've done some incredible work, but check your fucking ego.
        public static byte[] DecompressNew(string filename, uint startAddress, ushort maxDataSize = ushort.MaxValue)
        {
            List<byte> output = new List<byte>();

            using BinaryReader reader = new BinaryReader(new FileStream(filename, FileMode.Open, FileAccess.Read));
            reader.BaseStream.Seek(startAddress, SeekOrigin.Begin);

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
                    case 5: // EORed Copy
                        {
                            // Similar to Library Copy, but the copied data is Exclusive ORed with 0b11111111
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

            return output.ToArray();
        }
    }
}
