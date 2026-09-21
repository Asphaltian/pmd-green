using System.Numerics;
using AGBModern;
using GBARenderer;
using LibRecomp;
using RecompiledFuncs;

namespace PMDGreen.Patches;

internal static class OpeningPicture
{
    private const int MapFile = 234;              // MAP_FILE_ID_INTRO
    private const uint PaletteFileOffset = 0x430; // GroundBg.bplFile
    private const uint PaletteFileHeader = 4;     // sizeof(BplHeader)

    private const int Background = 2;
    private const int GameBackground = 3;
    private const int ColorsPerPalette = WidePicture.ColorsPerPalette;
    private const int ColorsWritten = ColorsPerPalette - 1;
    private const uint ColorSize = WidePicture.ColorSize;
    private const byte ColorUnk4 = 0x80;

    private const uint FadeTable = 0x03000C00;      // sPaletteFadeTable
    private const uint FadeEntrySize = 0x14;        // sizeof(PaletteFadeEntry)
    private const uint FadeRowOffset = 0x10;        // PaletteFadeEntry.applyFadeToRow
    private const uint FadeUpdateFlag = 0x03001B58; // sUpdatePaletteFade
    private const uint RowsToCopy = 0x020251D0;     // sBGPaletteRowDirty
    private const uint FadedColors = 0x020251F0;    // sBGPaletteBuffer
    private const uint PaletteRAM = 0x05000000;     // PLTT

    private const int CameraEnd = 128;
    private const int PictureCameraEnd = 151;

    private static readonly HashSet<uint> RowsToShow = [];

    private static WidePicture _picture = null!;
    private static RecompFunc _setColors = null!;
    private static uint _changedPalette;

    private static Vector3[] _dayColors = [];
    private static Vector3[] _mapColors = [];

    private static uint _map;
    private static bool _isShown;
    private static int _gameCameraY;
    private static int _cameraY;

    private readonly record struct Color(byte Red, byte Green, byte Blue, byte Unk4);

    public static void Install()
    {
        _picture = new WidePicture("S03.bin");
        _changedPalette = Memory.Allocate(_picture.PaletteSize);

        var loadMap = Funcs.Patches.sub_80A2FBC;
        Funcs.Patches.sub_80A2FBC = ctx =>
        {
            uint groundBg = ctx.R0;
            int file = (short)ctx.R1;

            if (groundBg == _map)
            {
                Hide();
                _map = 0;
            }

            loadMap(ctx);

            if (file == MapFile)
            {
                _map = groundBg;
            }
        };

        var updateCamera = Funcs.Patches.UpdateMapCameraPosition;
        Funcs.Patches.UpdateMapCameraPosition = ctx =>
        {
            uint mapRender = ctx.R0, position = ctx.R1;
            if (GroundMap.IsInGroundBg(mapRender, _map) && GameFrame.Margins.Width > 0)
            {
                _gameCameraY = Math.Clamp(Memory.Peek<PixelPosition>(position).Y, 0, CameraEnd);
                Show(ctx);
            }

            (ctx.R0, ctx.R1) = (mapRender, position);
            updateCamera(ctx);
        };

        _setColors = Funcs.Patches.sub_809971C;
        Funcs.Patches.sub_809971C = ctx =>
        {
            uint first = ctx.R0, colors = ctx.R1, count = ctx.R2;
            _setColors(ctx);
            if (!_isShown || first % ColorsPerPalette == 0 || first + count > _picture.PaletteCount * ColorsPerPalette)
            {
                return;
            }

            for (uint i = 0; i < count; i++)
            {
                _mapColors[PaletteIndex(first + i)] = ReadColor(colors + (i * ColorSize));
            }

            SetPalettes(ctx);
        };

        var setBG2Offsets = Funcs.Patches.SetBG2RegOffsets;
        Funcs.Patches.SetBG2RegOffsets = ctx =>
        {
            if (_isShown)
            {
                (ctx.R0, ctx.R1) = (0, (uint)_cameraY);
            }

            setBG2Offsets(ctx);
        };

        var copyPalettes = Funcs.Patches.TransferBGPaletteBuffer;
        Funcs.Patches.TransferBGPaletteBuffer = ctx =>
        {
            copyPalettes(ctx);
            ShowRows();
        };

        GameFrame.Finished += Draw;
    }

    private static void Draw(RecompContext ctx)
    {
        if (!_isShown)
        {
            return;
        }

        MoveCamera();
        _picture.Draw(layer: 0, Background, firstRow: _cameraY / 8);
        _picture.SupplyMargins(layer: 0, Background, firstRow: _cameraY / 8);
        Scheduler.RunUntimed(() => Funcs.SetBG2RegOffsets(ctx));
    }

    private static void MoveCamera()
    {
        const float Scale = (float)PictureCameraEnd / CameraEnd;
        var gameLayer = GameFrame.Motion.Backgrounds[GameBackground];

        float from = (_gameCameraY - gameLayer.From.Y) * Scale;
        float to = (_gameCameraY - gameLayer.To.Y) * Scale;
        _cameraY = Math.Max((int)MathF.Floor(to), 0);

        GameFrame.Motion.Backgrounds[Background] = new Displacement
        {
            From = new Vector2(0, _cameraY - from),
            To = new Vector2(0, _cameraY - to),
        };
    }

    private static void Show(RecompContext ctx)
    {
        uint paletteFile = Memory.Peek<uint>(_map + PaletteFileOffset);
        if (_isShown || paletteFile == 0)
        {
            return;
        }

        uint colors = Memory.Peek<OpenedFile>(paletteFile).Data + PaletteFileHeader;
        _dayColors = new Vector3[_picture.PaletteCount * ColorsWritten];
        for (uint i = 0; i < _dayColors.Length; i++)
        {
            _dayColors[i] = ReadColor(colors + (i * ColorSize));
        }

        _mapColors = [.. _dayColors];
        _isShown = true;
        _picture.CopyTiles();
        SetPalettes(ctx);
    }

    private static void Hide()
    {
        if (_isShown)
        {
            _isShown = false;
            Tilemaps.Clear(Background);
        }
    }

    private static uint PaletteIndex(uint color) => (color / ColorsPerPalette * ColorsWritten) + (color % ColorsPerPalette) - 1;

    private static Vector3 ReadColor(uint address)
    {
        var color = Memory.Peek<Color>(address);
        return new Vector3(color.Red, color.Green, color.Blue);
    }

    private static void SetPalettes(RecompContext ctx)
    {
        for (uint i = 0; i < _picture.PaletteCount * ColorsPerPalette; i++)
        {
            var color = ReadColor(_picture.Palette + (i * ColorSize));
            var changed = Vector3.Clamp(color + ChangeNear(color), Vector3.Zero, new Vector3(255));
            Memory.Poke<Color>(_changedPalette + (i * ColorSize)) = new Color((byte)changed.X, (byte)changed.Y, (byte)changed.Z, ColorUnk4);
        }

        bool wasUpdating = Memory.Peek<byte>(FadeUpdateFlag) != 0;
        Scheduler.RunUntimed(() =>
        {
            for (uint palette = 0; palette < _picture.PaletteCount; palette++)
            {
                ref byte isFading = ref Memory.Poke<byte>(FadeTable + (palette * FadeEntrySize));
                bool wasFading = isFading != 0;
                uint first = (palette * ColorsPerPalette) + 1;
                (ctx.R0, ctx.R1, ctx.R2) = (first, _changedPalette + (first * ColorSize), ColorsWritten);
                _setColors(ctx);
                if (wasFading)
                {
                    continue;
                }

                ref byte isCopying = ref Memory.Poke<byte>(RowsToCopy + palette);
                byte wasCopying = isCopying;
                ctx.R0 = palette;
                Recomp.LookupFunc(Memory.Peek<uint>(FadeTable + (palette * FadeEntrySize) + FadeRowOffset))(ctx);
                isFading = 0;
                isCopying = wasCopying;
                RowsToShow.Add(palette);
            }
        });

        Memory.Poke<byte>(FadeUpdateFlag) = (byte)(wasUpdating ? 1 : 0);
    }

    private static void ShowRows()
    {
        if (RowsToShow.Count == 0)
        {
            return;
        }

        Scheduler.RunUntimed(() =>
        {
            foreach (uint row in RowsToShow)
            {
                for (uint i = row * ColorsPerPalette; i < (row + 1) * ColorsPerPalette; i++)
                {
                    Memory.Write16(PaletteRAM + (i * sizeof(ushort)), Memory.Peek<ushort>(FadedColors + (i * sizeof(ushort))));
                }
            }
        });
        RowsToShow.Clear();
    }

    private static Vector3 ChangeNear(Vector3 color)
    {
        var change = Vector3.Zero;
        float totalWeight = 0;

        for (int i = 0; i < _dayColors.Length; i++)
        {
            float distance = Vector3.DistanceSquared(color, _dayColors[i]);
            float weight = 1 / ((distance * distance) + 1);
            change += (_mapColors[i] - _dayColors[i]) * weight;
            totalWeight += weight;
        }

        return change / totalWeight;
    }
}
