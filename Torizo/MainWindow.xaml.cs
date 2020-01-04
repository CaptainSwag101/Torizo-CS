using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
using SkiaSharp;
using Torizo.Graphics;

namespace Torizo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [Flags]
        private enum RoomLayers
        {
            TileLayer1 = 1,
            TileLayer2 = 2,
            BtsLayer = 4,
            Enemies = 8,
        }

        private delegate void UpdateDrawRectDelegate();

        public static byte[] LoadedROM;
        public static string LoadedROMPath = "";
        public static ushort RomHeaderSize;

        private Room currentRoom;
        private int currentRoomState;
        private TilesetInfo currentTilesetInfo;
        private Tileset currentTileset;
        private SKBitmap currentTilesetBitmap;
        private List<ushort[]> palettes = new List<ushort[]>();
        private LevelData currentLevelData;
        private RoomLayers layersToDraw = RoomLayers.TileLayer1 | RoomLayers.TileLayer2 | RoomLayers.BtsLayer | RoomLayers.Enemies;

        private double levelEditorWidth = 0d, levelEditorHeight = 0d;
        private double zoomScale = 1.0d;
        private double refreshRate = 60d;

        private BitmapImage roomRenderOutput;
        private Timer screenRefreshTimer = new Timer();

        private static RoutedCommand toggleLayer1Hotkey = new RoutedCommand();
        private static RoutedCommand toggleLayer2Hotkey = new RoutedCommand();
        private static RoutedCommand toggleBtsLayerHotkey = new RoutedCommand();
        private static RoutedCommand toggleEnemyLayerHotkey = new RoutedCommand();


        public MainWindow()
        {
            InitializeComponent();

            // Set up hotkey shortcuts
            toggleLayer1Hotkey.InputGestures.Add(new KeyGesture(Key.F1));
            toggleLayer2Hotkey.InputGestures.Add(new KeyGesture(Key.F2));
            toggleBtsLayerHotkey.InputGestures.Add(new KeyGesture(Key.F3));
            toggleEnemyLayerHotkey.InputGestures.Add(new KeyGesture(Key.F4));
            CommandBindings.Add(new CommandBinding(toggleLayer1Hotkey, ToggleLayer1));
            CommandBindings.Add(new CommandBinding(toggleLayer2Hotkey, ToggleLayer2));
            CommandBindings.Add(new CommandBinding(toggleBtsLayerHotkey, ToggleBtsLayer));
            CommandBindings.Add(new CommandBinding(toggleEnemyLayerHotkey, ToggleEnemyLayer));
            UpdateVisibleLayersStatus();


            // This will sharpen the pixels when the system tries to place the image on a subpixel boundary,
            // but it has weird jagged edges and inconsistent scaling.
            //RenderOptions.SetBitmapScalingMode(roomEditor_OutputImage, BitmapScalingMode.NearestNeighbor);
            //RenderOptions.SetBitmapScalingMode(tileset_OutputImage, BitmapScalingMode.NearestNeighbor);


            // Load the ROM image
            Microsoft.Win32.OpenFileDialog openROMDialog = new Microsoft.Win32.OpenFileDialog();
            openROMDialog.Filter = "SNES ROM files (*.sfc;*.smc)|*.sfc;*.smc|All files|*.*";
            openROMDialog.ShowDialog();

            if (string.IsNullOrWhiteSpace(openROMDialog.FileName))
            {
                MessageBox.Show("Must specify a Super Metroid ROM file.");
                Application.Current.Shutdown();
                return;
            }
            LoadROM(openROMDialog.FileName);

            // Populate room offset list
            using StreamReader mdbStream = new StreamReader(new FileStream(@"Resources\mdb.txt", FileMode.Open, FileAccess.Read, FileShare.Read), Encoding.ASCII);
            while (mdbStream.BaseStream.Position < mdbStream.BaseStream.Length)
            {
                string line = mdbStream.ReadLine();
                if (!string.IsNullOrWhiteSpace(line))
                    roomSelect_ComboBox.Items.Add(line);
            }
            roomSelect_ComboBox.SelectedIndex = 0;

            // Set up drawing routines
            screenRefreshTimer.Interval = (1d / refreshRate) * 1000; // (refreshRate) frames per second
            screenRefreshTimer.AutoReset = true;
            screenRefreshTimer.Elapsed += timer_ScreenRefresh_Elapsed;
            screenRefreshTimer.Start();
        }

        private void LoadROM(string romPath)
        {
            LoadedROMPath = romPath;
            LoadedROM = File.ReadAllBytes(LoadedROMPath);

            using BinaryReader reader = new BinaryReader(new MemoryStream(LoadedROM));

            // Check if ROM is headered or unheadered
            if (LoadedROM.Length % (short.MaxValue + 1) == 0)
                RomHeaderSize = 0;    // unheadered
            else
                RomHeaderSize = 512;  // headered

            reader.BaseStream.Seek(RomHeaderSize + 0x7FD9, SeekOrigin.Begin);
            byte romRegion = reader.ReadByte();

            // check if PAL
            if (romRegion > 2)
            {
                MessageBox.Show("This rom is PAL and will not work properly with Torizo.", "PAL ROM", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
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



            // TODO: THIS IS UNFINISHED!!!



        }

        #region Drawing Functions
        private void DrawRoom()
        {
            if (currentRoom.Header.Height == 0 || currentRoom.Header.Width == 0 || currentLevelData.Header == 0)
                return;

            int tileCountX = currentRoom.Header.Width * 16;
            int tileCountY = currentRoom.Header.Height * 16;

            // Set up the output area
            levelEditorWidth = tileCountX * 16;
            levelEditorHeight = tileCountY * 16;

            SKImageInfo imageInfo = new SKImageInfo((int)levelEditorWidth, (int)levelEditorHeight);
            using SKSurface surface = SKSurface.Create(imageInfo);
            SKCanvas canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            using SKPaint tilePaint = new SKPaint();
            tilePaint.IsAntialias = false;
            tilePaint.StrokeWidth = 1;
            tilePaint.Style = SKPaintStyle.Fill;


            // Render the tiles
            // Layer 1
            if (layersToDraw.HasFlag(RoomLayers.TileLayer1))
            {
                for (int tileY = 0; tileY < tileCountY; ++tileY)
                {
                    for (int tileX = 0; tileX < tileCountX; ++tileX)
                    {
                        SKRect tileRect = new SKRect(tileX * 16, tileY * 16, tileX * 16 + 16, tileY * 16 + 16);
                        byte blockId = currentLevelData.TileLayer1[tileX, tileY].BlockID;

                        tilePaint.Color = SKColor.Parse((blockId * 100).ToString("X6"));
                        canvas.DrawRect(tileRect, tilePaint);
                    }
                }
            }

            // Layer 2
            if (layersToDraw.HasFlag(RoomLayers.TileLayer2))
            {
                if (currentLevelData.TileLayer2.Length > 0)
                {
                    for (int tileY = 0; tileY < tileCountY; ++tileY)
                    {
                        for (int tileX = 0; tileX < tileCountX; ++tileX)
                        {
                            SKRect tileRect = new SKRect(tileX * 16, tileY * 16, tileX * 16 + 16, tileY * 16 + 16);
                            byte blockId = currentLevelData.TileLayer2[tileX, tileY].BlockID;

                            tilePaint.Color = SKColor.Parse($"7F{(blockId << 16).ToString("X6")}");
                            canvas.DrawRect(tileRect, tilePaint);
                        }
                    }
                }
            }

            using SKImage image = surface.Snapshot();
            using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
            using MemoryStream mStream = new MemoryStream(data.ToArray());
            BitmapImage bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = mStream;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();

            roomRenderOutput = bmp;
            slider_Zoom_ValueChanged(this, new RoutedPropertyChangedEventArgs<double>(100, 100));
        }

        private void DrawTileset()
        {
            //currentTilesetInfo = TilesetInfo.ReadTilesetInfo(currentRoom.StateInfo[currentRoomState].StateData.TilesetIndex);
            currentTilesetInfo = TilesetInfo.ReadTilesetInfo(3);
            currentTileset = Tileset.ReadTilesetFromInfo(currentTilesetInfo);

            // Load global palette list
            for (int i = 0; i < 9; ++i)
            {
                TilesetInfo info = TilesetInfo.ReadTilesetInfo(i);
                palettes.Add(Tileset.ReadPalette(info.PaletteAddress.ToPointer()));
            }

            // Combine common and unique tile graphics data
            //byte[] allTileData = new byte[currentTileset.TileGraphics.CommonTileGraphics.Length + currentTileset.TileGraphics.UniqueTileGraphics.Length];
            byte[] allTileData = new byte[0x5000 + currentTileset.TileGraphics.CommonTileGraphics.Length];  // should be 0x8000 instead of 0x5000 for tileset 26 or 17?
            Array.Copy(currentTileset.TileGraphics.UniqueTileGraphics, 0, allTileData, 0, currentTileset.TileGraphics.UniqueTileGraphics.Length);
            Array.Copy(currentTileset.TileGraphics.CommonTileGraphics, 0, allTileData, 0x5000, currentTileset.TileGraphics.CommonTileGraphics.Length);

            // Convert indexed tile graphics to a paletted pixelmap
            byte[] allTilePixelData = Tileset.IndexedGraphicsToPixelData(allTileData);

            // Apparently used to add 0x5000 in earlier versions of SMILE, but was later changed to 0x6000.
            // I've opted to simply add the length of unique tile graphics just to be more precise.
            int tileCount = allTileData.Length / 32;

            // DISABLED UNTIL I CAN FIGURE OUT WHAT'S WRONG
            
            int tileDrawSize = 8;
            int lineSize = 32;  // Number of blocks to draw per line in the block preview
            SKImageInfo imageInfo = new SKImageInfo((tileCount * tileDrawSize * 2) / lineSize, (lineSize * tileDrawSize * 2));
            using SKSurface surface = SKSurface.Create(imageInfo);
            SKCanvas canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            byte[] combinedTileTable = new byte[currentTileset.TileTables.CommonTileTable.Length + currentTileset.TileTables.UniqueTileTable.Length];
            Array.Copy(currentTileset.TileTables.CommonTileTable, 0, combinedTileTable, 0, currentTileset.TileTables.CommonTileTable.Length);
            Array.Copy(currentTileset.TileTables.UniqueTileTable, 0, combinedTileTable, currentTileset.TileTables.CommonTileTable.Length, currentTileset.TileTables.UniqueTileTable.Length);

            for (int blockNum = 0; blockNum < combinedTileTable.Length; ++blockNum)
            {
                ushort[] blockEntry = Tileset.GetTileTableEntry(blockNum, combinedTileTable);

                int lineNum = blockNum / lineSize;
                for (int e = 0; e < 4; ++e)
                {
                    SKBitmap tileBitmap = new SKBitmap(8, 8);
                    ushort tileData = blockEntry[e];
                    ushort tileNum = (ushort)(tileData & 0x3FF);
                    bool xFlip = ((tileData & 0b0100000000000000) >> 14) == 1;
                    bool yFlip = ((tileData & 0b1000000000000000) >> 15) == 1;
                    byte paletteNum = (byte)((tileData & 0b0001110000000000) >> 10);

                    for (byte y = 0; y < 8; ++y)
                    {
                        for (byte x = 0; x < 8; ++x)
                        {
                            // Get palette color for pixel
                            int trueOffset = (tileNum * 64) + (y * 8) + x;
                            uint colorIndex = allTilePixelData[trueOffset];

                            // Get tile palette
                            uint[] palette = PaletteConverter.SnesPaletteToPcPalette(currentTileset.Palette, false);
                            uint[] paletteMask = PaletteConverter.SnesPaletteToPcPalette(currentTileset.Palette, true);
                            
                            SKColor pixelColor = SKColor.Parse(palette[colorIndex].ToString("X6"));

                            if (xFlip && yFlip)
                            {
                                tileBitmap.SetPixel(7 - x, 7 - y, pixelColor);
                            }
                            else if (xFlip)
                            {
                                tileBitmap.SetPixel(7 - x, y, pixelColor);
                            }
                            else if (yFlip)
                            {
                                tileBitmap.SetPixel(x, 7 - y, pixelColor);
                            }
                            else
                            {
                                tileBitmap.SetPixel(x, y, pixelColor);
                            }
                        }
                    }

                    int drawStartX, drawStartY, drawEndX, drawEndY;
                    drawStartX = (((blockNum % lineSize) * 2) + (e % 2)) * tileDrawSize;
                    drawEndX = (((blockNum % lineSize) * 2) + (e % 2) + 1) * tileDrawSize;
                    drawStartY = ((lineNum * 2) + (e / 2)) * tileDrawSize;
                    drawEndY = ((lineNum * 2) + (e / 2) + 1) * tileDrawSize;

                    canvas.DrawBitmap(tileBitmap, new SKRect(drawStartX, drawStartY, drawEndX, drawEndY));
                }
            }


            /*
            // Draw the raw tileset image instead of block data
            int lineSize = 16;
            int tileDrawSize = 32;
            SKImageInfo imageInfo = new SKImageInfo(lineSize * tileDrawSize, (tileCount / lineSize) * tileDrawSize);
            using SKSurface surface = SKSurface.Create(imageInfo);
            SKCanvas canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            for (int tileNum = 0; tileNum < tileCount; ++tileNum)
            {
                int lineNum = tileNum / lineSize;
                SKBitmap tileBitmap = new SKBitmap(8, 8);
                for (byte y = 0; y < 8; ++y)
                {
                    for (byte x = 0; x < 8; ++x)
                    {
                        // Get palette color for pixel
                        uint colorIndex = allTilePixelData[(tileNum * 64) + (y * 8) + x];
                        uint pixelColor = pcPalette[colorIndex];
                        tileBitmap.SetPixel(x, y, SKColor.Parse(pixelColor.ToString("X6")));
                    }
                }
                int drawStartX, drawEndX, drawStartY, drawEndY;
                drawStartX = (tileNum % lineSize) * tileDrawSize;
                drawEndX = ((tileNum % lineSize) + 1) * tileDrawSize;
                drawStartY = lineNum * tileDrawSize;
                drawEndY = (lineNum + 1) * tileDrawSize;
                canvas.DrawBitmap(tileBitmap, new SKRect(drawStartX, drawStartY, drawEndX, drawEndY));

                //Mark each tile with its index for debugging assistance
                SKTextBlobBuilder textBuilder = new SKTextBlobBuilder();
                SKPaint fontPaint = new SKPaint();
                fontPaint.Color = SKColors.Red;
                fontPaint.StrokeWidth = 5;
                fontPaint.Typeface = SKTypeface.FromFamilyName("Courier New", SKFontStyle.Bold);
                fontPaint.TextSize = 14;
                canvas.DrawText(tileNum.ToString(), drawStartX, drawEndY, fontPaint);
            }
            */

            // Copy data to output Image
            using SKImage image = surface.Snapshot();
            using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
            using MemoryStream mStream = new MemoryStream(data.ToArray());
            BitmapImage bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = mStream;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();

            tileset_OutputImage.Source = bmp;
        }
        #endregion

        #region Hotkey Events
        private void ToggleLayer1(object sender, RoutedEventArgs e)
        {
            layersToDraw ^= RoomLayers.TileLayer1;
            DrawRoom();
            UpdateVisibleLayersStatus();
        }

        private void ToggleLayer2(object sender, RoutedEventArgs e)
        {
            layersToDraw ^= RoomLayers.TileLayer2;
            DrawRoom();
            UpdateVisibleLayersStatus();
        }

        private void ToggleBtsLayer(object sender, RoutedEventArgs e)
        {
            layersToDraw ^= RoomLayers.BtsLayer;
            DrawRoom();
            UpdateVisibleLayersStatus();
        }

        private void ToggleEnemyLayer(object sender, RoutedEventArgs e)
        {
            layersToDraw ^= RoomLayers.Enemies;
            DrawRoom();
            UpdateVisibleLayersStatus();
        }
        #endregion

        #region UI Events
        private void UpdateVisibleLayersStatus()
        {
            StringBuilder statusText = new StringBuilder("Visible Layers: ");

            if (layersToDraw.HasFlag(RoomLayers.TileLayer1))
                statusText.Append("Layer1, ");

            if (layersToDraw.HasFlag(RoomLayers.TileLayer2))
                statusText.Append("Layer2, ");

            if (layersToDraw.HasFlag(RoomLayers.BtsLayer))
                statusText.Append("BTS, ");

            if (layersToDraw.HasFlag(RoomLayers.Enemies))
                statusText.Append("Enemies, ");

            // Remove trailing comma and space
            statusText.Replace(", ", "", statusText.Length - 2, 2);

            // If no layers are visible
            if (statusText.ToString() == "Visible Layers: ")
                statusText.Append("None");

            statustext_EnabledLayers.Text = statusText.ToString();
        }

        private void slider_Zoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            zoomScale = (e.NewValue / 100d);

            if (statustext_Zoom != null)
                statustext_Zoom.Text = $"{e.NewValue}%";

            if (roomEditor_OutputImage != null && roomEditor_OutputImage.Source != null)
            {
                roomEditor_OutputImage.Width = levelEditorWidth * zoomScale;
                roomEditor_OutputImage.Height = levelEditorHeight * zoomScale;
            }
        }

        private void roomSelect_ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (uint.TryParse((string)((ComboBox)sender).SelectedItem, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out uint roomAddress))
            {
                currentRoom = Room.ReadRoom(roomAddress);
                stateSelect_ComboBox.Items.Clear();
                foreach (var state in currentRoom.StateInfo)
                {
                    stateSelect_ComboBox.Items.Add($"{currentRoom.StateInfo.IndexOf(state)}: {state.StateHeader.StateCode.ToString("X4")}");
                }
                stateSelect_ComboBox.SelectedIndex = 0;
            }
        }

        private void stateSelect_ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int state = stateSelect_ComboBox.SelectedIndex;
            if (state == -1)
            {
                return;
            }

            currentLevelData = Room.ReadLevel(currentRoom, state);
            currentRoomState = state;
            DrawRoom();
            DrawTileset();
        }

        private void timer_ScreenRefresh_Elapsed(object sender, ElapsedEventArgs args)
        {
            roomEditor_OutputImage.Dispatcher.Invoke(new UpdateDrawRectDelegate(UpdateDrawRect));
        }

        private void UpdateDrawRect()
        {
            roomEditor_OutputImage.Source = roomRenderOutput;
        }
        #endregion
    }
}
