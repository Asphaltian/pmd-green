using AGBModern;

namespace PMDGreen.Patches;

internal static class ScreenEdgeLine
{
    private const int LineTile = 0x279;
    private const int TileSize = 32;          // TILE_SIZE_4BPP
    private const int CharBlockSize = 0x4000; // BG_CHAR_SIZE

    private static readonly byte[] Line = [3, 0, 0, 0, 3, 0, 0, 0, 3, 0, 0, 0, 3, 0, 0, 0, 3, 0, 0, 0, 3, 0, 0, 0, 3, 0, 0, 0, 3, 0, 0, 0];

    private static bool _isBlanked;

    public static void Install()
    {
        GameFrame.Finished += _ => Update();
    }

    private static void Update()
    {
        int tileBase = ((IO.Read16(IO.BG0CNT) >> 2) & 3) * CharBlockSize;
        var tile = Memory.VRAM.AsSpan(tileBase + (LineTile * TileSize), TileSize);

        if (GameFrame.Margins.Width > 0 && tile.SequenceEqual(Line))
        {
            tile.Clear();
            _isBlanked = true;
        }
        else if (GameFrame.Margins.Width == 0 && _isBlanked && !tile.ContainsAnyExcept((byte)0))
        {
            Line.CopyTo(tile);
            _isBlanked = false;
        }
    }
}
