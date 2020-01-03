using System;
using System.Collections.Generic;
using System.Text;

namespace Torizo.Graphics
{
    public static class PaletteConverter
    {
        public static uint SnesToPcColor(ushort snesColor)
        {
            return (uint)((snesColor & 0x1F) << 0x13 | (snesColor & 0x3E0) << 6 | snesColor >> 7 & 0xF8);
        }

        public static ushort PcToSnesColor(uint pcColor)
        {
            uint r, g, b;

            r = pcColor & 0xF80000;
            g = pcColor & 0xF800;
            b = pcColor & 0xF8;

            if (((pcColor & 0x40000) != 0) && (r < 0xF80000))
            {
                r += 0x80000;
            }

            if (((pcColor & 0x400) != 0) && (g < 0xF800))
            {
                g += 0x800;
            }

            if (((pcColor & 4) != 0) && (b < 0xF8))
            {
                b += 8;
            }

            return (ushort)(r >> 0x13 | g >> 6 | b << 7);
        }

        public static uint[] SnesPaletteToPcPalette(ushort[] snesPalette, bool mask)
        {
            int numColors = snesPalette.Length;
            uint[] pcPalette = new uint[numColors];

            for (int i = 0; i < numColors; ++i)
            {
                if (mask)
                {
                    pcPalette[i] = 0xFFFFFFFF;
                }
                else
                {
                    pcPalette[i] = SnesToPcColor(snesPalette[i]);
                }
            }

            return pcPalette;
        }
    }
}
