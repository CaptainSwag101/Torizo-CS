using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Torizo.Graphics
{
    [StructLayout(LayoutKind.Sequential, Size = 9, Pack = 1)]
    public struct TilesetInfo
    {
        public BankedAddress TableAddress;
        public BankedAddress TileAddress;
        public BankedAddress PaletteAddress;

        public static TilesetInfo ReadTilesetInfo(int tilesetIndex)
        {
            TilesetInfo info;

            uint tilesetAddress = GetTilesetInfoAddress(tilesetIndex);
            using BinaryReader reader = new BinaryReader(new MemoryStream(MainWindow.LoadedROM));
            reader.BaseStream.Seek(tilesetAddress, SeekOrigin.Begin);

            byte[] infoData = reader.ReadBytes(9);
            
            // read tileset info
            GCHandle tilesetHandle = GCHandle.Alloc(infoData, GCHandleType.Pinned);
            TilesetInfo currentTileset = (TilesetInfo)Marshal.PtrToStructure(tilesetHandle.AddrOfPinnedObject(), typeof(TilesetInfo));
            tilesetHandle.Free();

            info = currentTileset;
            return info;
        }

        public static uint GetTilesetInfoAddress(int tilesetIndex)
        {
            uint tilesetStart = 0x07E6A2;
            uint tilesetAddress = tilesetStart + ((uint)tilesetIndex * 9);
            return tilesetAddress + MainWindow.RomHeaderSize;
        }
    }

    public struct Tileset
    {
        private const uint CommonTileTableOffset = 0x01CA09D;
        private const uint CommonTileGraphicsOffset = 0x01C8000;

        public (byte[] CommonTileTable, byte[] UniqueTileTable) TileTables;
        public (byte[] CommonTileGraphics, byte[] UniqueTileGraphics) TileGraphics;
        public ushort[] Palette;

        public static Tileset ReadTilesetFromInfo(TilesetInfo info)
        {
            Tileset tileset;

            tileset.TileTables = (ReadCommonTileTable(), ReadUniqueTileTable(info.TableAddress.ToPointer()));
            tileset.TileGraphics = (ReadCommonTileGraphics(), ReadUniqueTileGraphics(info.TileAddress.ToPointer()));
            tileset.Palette = ReadPalette(info.PaletteAddress.ToPointer());

            return tileset;
        }

        public static byte[] ReadCommonTileTable()
        {
            uint offset = CommonTileTableOffset + MainWindow.RomHeaderSize;
            byte[] compressedTable = new byte[ushort.MaxValue];
            Array.Copy(MainWindow.LoadedROM, offset, compressedTable, 0, ushort.MaxValue);
            byte[] decompressedTable = Compression.DecompressData(compressedTable);

            return decompressedTable;
        }

        public static byte[] ReadUniqueTileTable(uint tableAddress)
        {
            uint offset = tableAddress + MainWindow.RomHeaderSize;
            byte[] compressedTable = new byte[ushort.MaxValue];
            Array.Copy(MainWindow.LoadedROM, offset, compressedTable, 0, ushort.MaxValue);
            byte[] decompressedTable = Compression.DecompressData(compressedTable);

            return decompressedTable;
        }

        public static byte[] ReadCommonTileGraphics()
        {
            uint offset = CommonTileGraphicsOffset + MainWindow.RomHeaderSize;
            byte[] compressedGraphics = new byte[ushort.MaxValue];
            Array.Copy(MainWindow.LoadedROM, offset, compressedGraphics, 0, ushort.MaxValue);
            byte[] decompressedGraphics = Compression.DecompressData(compressedGraphics);

            return decompressedGraphics;
        }

        public static byte[] ReadUniqueTileGraphics(uint tileAddress)
        {
            uint offset = tileAddress + MainWindow.RomHeaderSize;

            Compression.OpenFileLunar(MainWindow.LoadedROMPath, FileAccess.Read);
            byte[] decompressedGraphics = Compression.DecompressDataLunar(offset);
            Compression.CloseFileLunar();

            byte[] compressedGraphics = new byte[ushort.MaxValue];
            Array.Copy(MainWindow.LoadedROM, offset, compressedGraphics, 0, ushort.MaxValue);
            byte[] decompressedGraphics2 = Compression.DecompressData(compressedGraphics);

            for (int i = 0; i < decompressedGraphics.Length; ++i)
            {
                if (decompressedGraphics[i] != decompressedGraphics2[i])
                {
                    int x = 0;
                }
            }

            return decompressedGraphics;
        }

        public static ushort[] ReadPalette(uint paletteAddress)
        {
            uint offset = paletteAddress + MainWindow.RomHeaderSize;
            byte[] compressedPalette = new byte[ushort.MaxValue];
            Array.Copy(MainWindow.LoadedROM, offset, compressedPalette, 0, ushort.MaxValue);
            byte[] decompressedPalette = Compression.DecompressData(compressedPalette);

            // Convert palette data from bytes to ushorts
            ushort[] paletteData = new ushort[decompressedPalette.Length / 2];
            for (int i = 0; i < paletteData.Length; i += 2)
            {
                paletteData[i / 2] = BitConverter.ToUInt16(new byte[] { decompressedPalette[i], decompressedPalette[i + 1] });
            }

            return paletteData;
        }

        public static byte[] IndexedGraphicsToPixelData(byte[] indexedGraphics, byte bitsPerPixel = 4)
        {
            byte[] pixelData = new byte[indexedGraphics.Length * bitsPerPixel];

            int tileCount = indexedGraphics.Length / (16 * (bitsPerPixel / 2));
            for (int tileNum = 0; tileNum < tileCount; ++tileNum)
            {
                byte[] tileData = new byte[8 * 8];
                int tileDataOffset = (tileNum * (16 * (bitsPerPixel / 2)));  // each 8x8 tile has 16 bytes of data per set of 2 bitplanes

                // SNES only supports multiples of 2 bits per pixel
                for (byte bpSet = 0; bpSet < (bitsPerPixel / 2); ++bpSet)
                {
                    for (byte y = 0; y < 8; ++y)
                    {
                        int rowOffset = (bpSet * 16) + (y * 2);

                        // Process bitplane 0
                        for (byte x = 0; x < 8; ++x)
                        {
                            byte bit = (byte)((indexedGraphics[tileDataOffset + rowOffset]) >> x);
                            bit &= 1;   // mask only the bit we need
                            tileData[(y * 8) + x] |= (byte)(bit << (bpSet * 2));
                        }

                        // Process bitplane 1
                        for (byte x = 0; x < 8; ++x)
                        {
                            byte bit = (byte)((indexedGraphics[tileDataOffset + rowOffset + 1]) >> x);
                            bit &= 1;   // mask only the bit we need
                            tileData[(y * 8) + x] |= (byte)(bit << ((bpSet * 2) + 1));
                        }
                    }
                }

                // Temporary, for debugging and comparing output to SMILE
                // (Actually this might be correct)
                for (int i = 0; i < tileData.Length / 8; ++i)
                {
                    byte[] temp = new byte[8];
                    Array.Copy(tileData, i * 8, temp, 0, 8);
                    Array.Reverse(temp);
                    Array.Copy(temp, 0, tileData, i * 8, 8);
                }
                

                Array.Copy(tileData, 0, pixelData, tileData.Length * tileNum, tileData.Length);
            }

            return pixelData;
        }

        public static ushort[] GetTileTableEntry(int blockId, byte[] tileTable)
        {
            ushort[] tableEntry = new ushort[4];

            for (int i = 0; i < 4; ++i)
            {
                int offset = ((blockId & 0x3FF) * 8) + (i * 2);
                if (offset >= tileTable.Length || offset + 1 >= tileTable.Length)
                    return tableEntry;

                tableEntry[i] = BitConverter.ToUInt16(new byte[] { tileTable[offset], tileTable[offset + 1] });
            }

            return tableEntry;
        }
    }
}
