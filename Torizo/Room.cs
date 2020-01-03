using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Torizo
{
    [StructLayout(LayoutKind.Sequential, Size = 2, Pack = 1)]
    public struct Tile
    {
        public byte BlockID;
        public byte PatternByte;
    }

    [StructLayout(LayoutKind.Sequential, Size = 12, Pack = 1)]
    public struct DoorData
    {
        public ushort RoomID;      // pointer to room definition (mdb) [$8F]
        public byte DoorBitFlag;   // 7th = elevator, 6th = switches regions
        public byte Direction;     // direction, and whether or not door closes after passing through (others?)
        public byte Xi;            // X of door "illusion" on exit (16x16)
        public byte Yi;            // Y of door "illusion" on exit (16x16)
        public byte X;             // X of door on exit (16x16)
        public byte Y;             // Y of door on exit (16x16)
        public ushort ExitDistance;// distance Samus is placed from the door (not 16x16)
        public ushort ScrollData;  // pointer to code for updating scroll data (i.e. if you start in a room with no scroll) [$8F]
    }

    /*
    public struct CloneDoor
    {
        string Name;
        DoorData DoorProperties;
    }
    */

    [StructLayout(LayoutKind.Sequential, Size = 16, Pack = 1)]
    public struct Enemy // 16 bytes total
    {
        public ushort Species;     // pointer [A0] to enemy data
        public ushort X;
        public ushort Y;
        public ushort Orientation;
        public ushort Special;     // (Prop-X)
        public ushort Unknown1;    // graphic?
        public ushort Speed;
        public ushort Unknown2;    // speed2?
    }

    [StructLayout(LayoutKind.Sequential, Size = 64, Pack = 1)]
    public struct EnemyStats    // 64 bytes total
    {
        public ushort UNKNOWNAnimate;  // 1/2, # of bytes to rip from rom for tiles
        public ushort Palette;         // 3/4
        public ushort HP;              // 5/6
        public ushort Damage;          // 7/8
        public ushort Width;           // 9/10
        public ushort Height;          // 11/12
        public byte PaletteBank;       // 13
        public byte HurtFlash;         // 14, how long enemy flashes when shot
        public ushort ImpactSound;     // 15/16
        public ushort Unknown1;        // 17/18, 0000 for all except bosses/mini's
        public ushort EnemyAI;         // 19/20, (initialization)
        public ushort EnemyParts;      // 21/22, (enemy parts 0 = 1)
        public ushort Unknown2;        // 23/24, (?)
        public ushort UnknownGraphicPointer;   // 25/26, add on 2nd graphic to an enemy?!! (motion)
        public ushort GrappleReaction; // 27-28
        public ushort EnemyShot2;      // 29/30,  (metroid grab & moctroid suck)
        public ushort Unknown3;        // 31/32, (frozen AI?)
        public ushort Unknown4;        // 33/34, always 0000? (x-ray pause AI)
        public byte DeathAnimation;    // 36
        public ushort Unknown5;        // 37/38, always 0000?
        public ushort Unknown6;        // 39/40
        public ushort PowerbombInvulnerability;    // 41/42  ''<Kejardon> Bytes 41-42: Powerbomb invulnerability (00 00 = vulnerable, (00-4C) 80 = untouchable)
        public ushort Unknown7;        // 43/44
        public ushort Unknown8;        // 45/46
        public ushort Unknown9;        // 47/48
        public ushort EnemyTouch;      // 49/50 'when an enemy touches you
        public ushort EnemyShot;       // 51/52  'when your shot touches an enemy
        public ushort Unknown10;       // 53/54
        public BankedAddress EnemyTiles; // 55/56/57
        public byte LayerControl;      // 58
        public ushort ItemDrop;        // 59/60, (bank 1A)
                                // Bytes: 1 = Energy, 2 = Big Energy, 3 = Missiles, 4 = nothing, 5 = super missiles, 6 = power bombs 14 0A 55 82 05 05
        public ushort Vulnerabilities; // 61/62, (bank B4)
        public ushort EnemyName;       // 63/64, (bank 1A)
    }

    [StructLayout(LayoutKind.Sequential, Size = 6, Pack = 1)]
    public struct PLM
    {
        public ushort Type;
        public byte X;
        public byte Y;
        public byte I; // index?
        public byte Unknown;
    }

    public struct AreaSave
    {

    }

    [StructLayout(LayoutKind.Sequential, Size = 16, Pack = 1)]
    public struct FX1
    {
        public ushort Select;          // 0000 or doorID, use this entry... FFFF none... anything else, add 10h to FX1 pointer, and loop back to find next entry
        public ushort SurfaceStart;    // starting point of liquid surface
        public ushort SurfaceNew;      // new surface of liquid
        public ushort SurfaceSpeed;    // speed of liquid surface (lower is faster, bit 15 selects direction (0 = flow down, 1 = flow up)
        public byte SurfaceDelay;      // lower is faster (0 = a LONG time) TODO: Is this in frames?
        public byte Layer3Type;
        public byte A;
        public byte B;
        public byte C;
        public byte PaletteFXBits;         // palette FX bitflags (region-based)
        public byte AnimateTileBits;       // tile animation bitflags (region-based)
        public byte PaletteBlendBits;      // index value for palette blend data table [$89]
    }

    [StructLayout(LayoutKind.Sequential, Size = 11, Pack = 1)]
    public struct RoomHeader
    {
        public byte RoomID;        // index value for room, purpose unknown
        public byte Region;        // area of Zebes (on map)
        public byte X;             // X on map
        public byte Y;             // Y on map
        public byte Width;         // in screens and/or map tiles
        public byte Height;        // in screens and/or map tiles
        public byte UpScroller;
        public byte DownScroller;
        public byte Unknown;
        public ushort DoorOut;     // [$8F] pointer
    }

    [StructLayout(LayoutKind.Sequential, Size = 26, Pack = 1)]
    public struct RoomState
    {
        public BankedAddress LevelData;
        public byte TilesetIndex;
        public byte MusicTrack;
        public byte MusicControl;
        public ushort FX1;             // pointer to room_fx1 [$83]
        public ushort EnemyPopulation; // pointer to enemy_pop [$A1] (info about enemies)
        public ushort EnemySet;        // pointer to room_set [$B4]
        public ushort Layer2Scroll;    // layer 2 scroll data
        public ushort Scroll;          // pointer to mdb_scroll [$8F]
        public ushort Unknown;         // used in escape version of Bomb Torizo Room, WTF? xray casing code? [$8F]
        public ushort FX2;             // pointer to room_fx2 (code? I forgot) [$8F]
        public ushort PLM;             // pointer to PLM data
        public ushort BGData;          // pointer to bg_data [$8F]
        public ushort Layer12Handling; // pointer to layer 1 and layer 2 handling code [$8F]
    }

    public struct LevelData
    {
        public ushort Header;
        public Tile[,] TileLayer1;
        public byte[,] BtsData;
        public Tile[,] TileLayer2;
    }

    public struct Room
    {
        public RoomHeader Header;
        public List<((ushort StateCode, byte[] StateParams) StateHeader, RoomState StateData)> StateInfo;
        public List<DoorData> DoorList;
        public PLM PLM;
        public LevelData LevelData;

        public static Room ReadRoom(uint roomOffset)
        {
            Room room = new Room();

            using BinaryReader reader = new BinaryReader(new MemoryStream(MainWindow.LoadedROM));

            // read room header
            reader.BaseStream.Seek(roomOffset + MainWindow.RomHeaderSize, SeekOrigin.Begin);
            GCHandle roomHeaderHandle = GCHandle.Alloc(reader.ReadBytes(Marshal.SizeOf(typeof(RoomHeader))), GCHandleType.Pinned);
            RoomHeader currentRoomHeader = (RoomHeader)Marshal.PtrToStructure(roomHeaderHandle.AddrOfPinnedObject(), typeof(RoomHeader));
            roomHeaderHandle.Free();

            room.Header = currentRoomHeader;

            // read room state list
            var StateHeaders = new List<(ushort StateCode, byte[] StateParams)>();
            while (true)
            {
                ushort stateCode = reader.ReadUInt16();

                // I think the "Standard" state code should always be the last one in the list?
                if (stateCode == 0xE5E6)
                {
                    StateHeaders.Add((stateCode, new byte[] { }));
                    break;
                }

                switch (stateCode)
                {
                    case 0xE5EB:
                        StateHeaders.Add((stateCode, reader.ReadBytes(4)));
                        break;

                    case 0xE612:
                    case 0xE629:
                        StateHeaders.Add((stateCode, reader.ReadBytes(3)));
                        break;

                    case 0xE5FF:
                    case 0xE640:
                    case 0xE652:
                    case 0xE669:
                    case 0xE678:
                        StateHeaders.Add((stateCode, reader.ReadBytes(2)));
                        break;
                }
            }

            // read room state data
            room.StateInfo = new List<((ushort StateCode, byte[] StateParams) StateHeader, RoomState StateData)>();
            for (int stateNum = 0; stateNum < StateHeaders.Count; ++stateNum)
            {
                GCHandle roomStateHandle = GCHandle.Alloc(reader.ReadBytes(Marshal.SizeOf(typeof(RoomState))), GCHandleType.Pinned);
                RoomState currentRoomState = (RoomState)Marshal.PtrToStructure(roomStateHandle.AddrOfPinnedObject(), typeof(RoomState));
                roomStateHandle.Free();

                room.StateInfo.Add((StateHeaders[stateNum], currentRoomState));
            }

            // DoorOut gets converted into pointer to door data (pointer table pointing to actual door data)
            BankedAddress doorListPointer;
            doorListPointer.Bank = 0x8F;
            doorListPointer.Offset = currentRoomHeader.DoorOut;

            // make a copy of the pointer to the pointer table for door data
            //DoorLabel.Caption = doorDataOffset.ToString("X6");

            // get the pointers from the current location
            room.DoorList = new List<DoorData>();
            reader.BaseStream.Seek(doorListPointer.ToPointer() + MainWindow.RomHeaderSize, SeekOrigin.Begin);
            while (true)
            {
                ushort doorPointer = reader.ReadUInt16();

                // if less than 0x8000, is not a valid door pointer, we've reached the end of the list
                if (doorPointer < 0x8000)
                    break;

                BankedAddress currentDoorPointer;
                currentDoorPointer.Bank = 0x83;
                currentDoorPointer.Offset = doorPointer;

                // read door data from pointer
                long oldPos = reader.BaseStream.Position;
                reader.BaseStream.Seek(currentDoorPointer.ToPointer() + MainWindow.RomHeaderSize, SeekOrigin.Begin);
                GCHandle doorHandle = GCHandle.Alloc(reader.ReadBytes(Marshal.SizeOf(typeof(DoorData))), GCHandleType.Pinned);
                DoorData currentDoorData = (DoorData)Marshal.PtrToStructure(doorHandle.AddrOfPinnedObject(), typeof(DoorData));
                doorHandle.Free();
                reader.BaseStream.Seek(oldPos, SeekOrigin.Begin);

                room.DoorList.Add(currentDoorData);
            }

            return room;
        }

        public static LevelData ReadLevel(Room room, int stateIndex = 0)
        {
            RoomState currentRoomState = room.StateInfo[stateIndex].StateData;
            uint levelDataPtr = currentRoomState.LevelData.ToPointer();

            //Lunar.LunarCompression.OpenFile(loadedROMPath, FileAccess.Read);
            //byte[] decompressedLevelData = Lunar.LunarCompression.Decompress(levelDataPtr);
            //Lunar.LunarCompression.CloseFile();

            using BinaryReader reader = new BinaryReader(new MemoryStream(MainWindow.LoadedROM));
            reader.BaseStream.Seek(levelDataPtr, SeekOrigin.Begin);
            byte[] decompressedLevelData = Compression.DecompressData(reader.ReadBytes(ushort.MaxValue), ushort.MaxValue);

            using BinaryReader levelReader = new BinaryReader(new MemoryStream(decompressedLevelData));
            LevelData levelData;
            levelData.Header = levelReader.ReadUInt16();
            int tileCount = (levelData.Header / 2);
            int tileCountX = room.Header.Width * 16;
            int tileCountY = room.Header.Height * 16;

            int tilesLoaded = 0;
            levelData.TileLayer1 = new Tile[tileCountX, tileCountY];
            for (int tileY = 0; tileY < tileCountY; ++tileY)
            {
                for (int tileX = 0; tileX < tileCountX; ++tileX)
                {
                    levelData.TileLayer1[tileX, tileY].BlockID = levelReader.ReadByte();
                    levelData.TileLayer1[tileX, tileY].PatternByte = levelReader.ReadByte();
                    ++tilesLoaded;
                }
            }

            // If we haven't reached the end of the level data, read the BTS data
            if (levelReader.BaseStream.Position + tileCount < levelReader.BaseStream.Length)
            {
                levelData.BtsData = new byte[tileCountX, tileCountY];
                for (int tileY = 0; tileY < tileCountY; ++tileY)
                {
                    for (int tileX = 0; tileX < tileCountX; ++tileX)
                    {
                        levelData.BtsData[tileX, tileY] = levelReader.ReadByte();
                        ++tilesLoaded;
                    }
                }
            }
            else
            {
                levelData.BtsData = new byte[0, 0];
            }

            // If we still haven't reached the end of the level data, read layer 2
            if ((levelData.TileLayer1.Length * 2) + levelData.BtsData.Length + (tileCount * 2) <= levelReader.BaseStream.Length)
            {
                levelData.TileLayer2 = new Tile[tileCountX, tileCountY];
                for (int tileY = 0; tileY < tileCountY; ++tileY)
                {
                    for (int tileX = 0; tileX < tileCountX; ++tileX)
                    {
                        levelData.TileLayer2[tileX, tileY].BlockID = levelReader.ReadByte();
                        levelData.TileLayer2[tileX, tileY].PatternByte = levelReader.ReadByte();
                        ++tilesLoaded;
                    }
                }
            }
            else
            {
                levelData.TileLayer2 = new Tile[0, 0];
            }

            return levelData;
        }
    }
}
