using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Torizo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private delegate void UpdateDrawRectDelegate(BitmapImage source);

        private byte[] loadedROM;
        private string loadedROMPath = "";
        private ushort romHeaderOffset;

        private List<Room> RoomList = new List<Room>();

        private double levelEditorWidth = 0d, levelEditorHeight = 0d;
        private double zoomScale = 1.0d;
        private double refreshRate = 60d;

        private BitmapImage levelEditorImage = new BitmapImage();
        private Timer screenRefreshTimer = new Timer();


        public MainWindow()
        {
            InitializeComponent();

            Microsoft.Win32.OpenFileDialog openROMDialog = new Microsoft.Win32.OpenFileDialog();
            openROMDialog.Filter = "SNES ROM files (*.sfc;*.smc)|*.sfc;*.smc|All files|*.*";
            openROMDialog.ShowDialog();

            if (string.IsNullOrWhiteSpace(openROMDialog.FileName))
                return;

            LoadROM(openROMDialog.FileName);

            screenRefreshTimer.Interval = (1d / refreshRate) * 1000; // (refreshRate) frames per second
            screenRefreshTimer.AutoReset = true;
            screenRefreshTimer.Elapsed += timer_ScreenRefresh_Elapsed;
            screenRefreshTimer.Start();
        }

        private void LoadROM(string romPath)
        {
            loadedROMPath = romPath;
            loadedROM = File.ReadAllBytes(loadedROMPath);

            using BinaryReader reader = new BinaryReader(new MemoryStream(loadedROM));

            // Check if ROM is headered or unheadered
            if (loadedROM.Length % (short.MaxValue + 1) == 0)
                romHeaderOffset = 0;    // unheadered
            else
                romHeaderOffset = 512;  // headered

            reader.BaseStream.Seek(romHeaderOffset + 0x7FD9, SeekOrigin.Begin);
            byte romRegion = reader.ReadByte();

            // check if PAL
            if (romRegion > 2)
            {
                MessageBox.Show("This rom is PAL and will not work properly with Torizo.", "PAL ROM", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            reader.BaseStream.Seek(0x16B20, SeekOrigin.Begin);
            byte checkRoomVar = reader.ReadByte();
            reader.BaseStream.Seek(0x204AC, SeekOrigin.Begin);
            byte PLMBank = reader.ReadByte();
            reader.BaseStream.Seek(0x20B60, SeekOrigin.Begin);
            byte scrollPLMBank = reader.ReadByte();

            // show/hide RoomVarData menus
            if (checkRoomVar == 0x20)
            {
                // show RoomVar menu
            }
            else
            {
                // hide RoomVar menu
            }

            // Temporary, for testing only
            ReadRoom(0x0791F8);
        }

        private void ReadRoom(uint roomOffset)
        {
            Room room = new Room();
            room.DoorList = new List<DoorData>();

            using BinaryReader reader = new BinaryReader(new MemoryStream(loadedROM));

            // read room header
            reader.BaseStream.Seek(roomOffset + romHeaderOffset, SeekOrigin.Begin);
            GCHandle roomHeaderHandle = GCHandle.Alloc(reader.ReadBytes(Marshal.SizeOf(typeof(RoomHeader))), GCHandleType.Pinned);
            RoomHeader currentRoomHeader = (RoomHeader)Marshal.PtrToStructure(roomHeaderHandle.AddrOfPinnedObject(), typeof(RoomHeader));
            roomHeaderHandle.Free();

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
            var StateInfo = new List<((ushort StateCode, byte[] StateParams) StateHeader, RoomState StateData)>();
            for (int stateNum = 0; stateNum < StateHeaders.Count; ++stateNum)
            {
                GCHandle roomStateHandle = GCHandle.Alloc(reader.ReadBytes(Marshal.SizeOf(typeof(RoomState))), GCHandleType.Pinned);
                RoomState currentRoomState = (RoomState)Marshal.PtrToStructure(roomStateHandle.AddrOfPinnedObject(), typeof(RoomState));
                roomStateHandle.Free();

                StateInfo.Add((StateHeaders[stateNum], currentRoomState));

                // TESTING ONLY: Load level data pointer
                uint levelDataPtr = currentRoomState.LevelData.ToPointer();

                //Lunar.LunarCompression.LoadLunarDll();

                Lunar.LunarCompression.OpenFile(loadedROMPath, FileAccess.Read);
                byte[] decompressedLevelData = Lunar.LunarCompression.Decompress(levelDataPtr);
                byte[] decompressedLevelData2 = Lunar.LunarCompression.DecompressNew(loadedROMPath, levelDataPtr);

                bool same = true;
                for (int i = 0; i < Math.Min(decompressedLevelData.Length, decompressedLevelData2.Length); ++i)
                {
                    if (decompressedLevelData[i] != decompressedLevelData2[i])
                        same = false;
                }

                if (same)
                    MessageBox.Show("HOORAY!");

                //Lunar.LunarCompression.UnloadLunarDll();

            }
            

            // DoorOut gets converted into pointer to door data (pointer table pointing to actual door data)
            BankedAddress doorListPointer;
            doorListPointer.Bank = 0x8F;
            doorListPointer.Offset = currentRoomHeader.DoorOut;

            // make a copy of the pointer to the pointer table for door data
            //DoorLabel.Caption = doorDataOffset.ToString("X6");

            // get the pointers from the current location
            reader.BaseStream.Seek(doorListPointer.ToPointer() + romHeaderOffset, SeekOrigin.Begin);
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
                reader.BaseStream.Seek(currentDoorPointer.ToPointer() + romHeaderOffset, SeekOrigin.Begin);
                GCHandle doorHandle = GCHandle.Alloc(reader.ReadBytes(Marshal.SizeOf(typeof(DoorData))), GCHandleType.Pinned);
                DoorData currentDoorData = (DoorData)Marshal.PtrToStructure(doorHandle.AddrOfPinnedObject(), typeof(DoorData));
                doorHandle.Free();
                reader.BaseStream.Seek(oldPos, SeekOrigin.Begin);

                room.DoorList.Add(currentDoorData);
            }
        }

        private void slider_Zoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            zoomScale = (e.NewValue / 100d);

            if (statustext_Zoom != null)
                statustext_Zoom.Text = $"{e.NewValue}%";

            if (drawrect_LevelEditor != null && drawrect_LevelEditor.Source != null)
            {
                drawrect_LevelEditor.Width = levelEditorWidth * zoomScale;
                drawrect_LevelEditor.Height = levelEditorHeight * zoomScale;
            }
        }

        private void timer_ScreenRefresh_Elapsed(object sender, ElapsedEventArgs args)
        {
            
        }

        private void UpdateDrawRect(BitmapImage source)
        {
            drawrect_LevelEditor.Source = source;
        }
    }
}
