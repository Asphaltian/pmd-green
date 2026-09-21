using System.Buffers.Binary;
using System.Runtime.InteropServices;
using AGBModern;

namespace PMDGreen.Patches;

internal sealed class WidePicture
{
    public const int ColorsPerPalette = 16; // BG_PALETTE_ROW_SIZE
    public const int ColorSize = 4;         // sizeof(RGB_Struct)

    private const int TileData = 0x8000;    // BG_CHAR_ADDR(2)
    private const int TileSize = 32;        // TILE_SIZE_4BPP
    private const int ScreenRows = 21;

    private readonly ushort[][] _layers;
    private readonly byte[] _tiles;

    public WidePicture(string name)
    {
        byte[] data = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "assets", "pictures", name));

        int tileCount = BinaryPrimitives.ReadUInt16LittleEndian(data);
        (Columns, Rows, PaletteCount, int layerCount) = (data[2], data[3], data[4], data[5]);
        var rest = data.AsSpan(6);

        Palette = Memory.Allocate(PaletteSize);
        rest[..PaletteSize].CopyTo(MemoryMarshal.CreateSpan(ref Memory.Poke<byte>(Palette), PaletteSize));
        rest = rest[PaletteSize..];

        int layerSize = Columns * Rows * sizeof(ushort);
        _layers = new ushort[layerCount][];
        for (int i = 0; i < layerCount; i++)
        {
            _layers[i] = MemoryMarshal.Cast<byte, ushort>(rest[..layerSize]).ToArray();
            rest = rest[layerSize..];
        }

        _tiles = rest[..(tileCount * TileSize)].ToArray();
    }

    public int Columns { get; }

    public int Rows { get; }

    public int PaletteCount { get; }

    public int PaletteSize => PaletteCount * ColorsPerPalette * ColorSize;

    public uint Palette { get; }

    private int ColumnsPerSide => (Columns - GameFrame.ScreenColumns) / 2;

    public void CopyTiles()
    {
        _tiles.CopyTo(Memory.VRAM, TileData);
    }

    public void Draw(int layer, int bg, int firstRow = 0)
    {
        for (int row = firstRow; row < Math.Min(firstRow + ScreenRows, Rows); row++)
        {
            for (int column = 0; column < GameFrame.ScreenColumns; column++)
            {
                Tilemaps.Entry(bg, column, row) = _layers[layer][(row * Columns) + column + ColumnsPerSide];
            }
        }

        Tilemaps.Show(bg);
    }

    public void SupplyMargins(int layer, int bg, int firstRow = 0)
    {
        for (int i = 0; i < ColumnsPerSide; i++)
        {
            Supply(layer, bg, firstRow, screenColumn: -1 - i, pictureColumn: ColumnsPerSide - 1 - i);
            Supply(layer, bg, firstRow, screenColumn: GameFrame.ScreenColumns + i, pictureColumn: ColumnsPerSide + GameFrame.ScreenColumns + i);
        }
    }

    private void Supply(int layer, int bg, int firstRow, int screenColumn, int pictureColumn)
    {
        var supplied = GameFrame.Margins.Supply(bg, screenColumn);
        for (int row = 0; row < Math.Min(ScreenRows, Rows - firstRow); row++)
        {
            supplied[row] = _layers[layer][((firstRow + row) * Columns) + pictureColumn];
        }
    }
}
