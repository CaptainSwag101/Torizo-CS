using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Torizo
{
    [StructLayout(LayoutKind.Sequential, Size = 3, Pack = 1)]
    public struct BankedAddress
    {
        public ushort Offset;
        public byte Bank;

        public uint ToPointer()
        {
            uint result = (uint)(((this.Bank % 0x80) * 0x8000) + (this.Offset % 0x8000));
            return result;
        }

        public static BankedAddress FromPointer(uint pointer)
        {
            BankedAddress result;

            result.Bank = (byte)(pointer / 0x8000);
            if (result.Bank < 0x80)
                result.Bank += 0x80;

            result.Offset = (ushort)(pointer - (result.Bank * 0x8000));
            if (result.Offset < 0x8000)
                result.Offset += 0x8000;

            return result;
        }
    }

    
}
