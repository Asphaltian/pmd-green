using AGBModern;
using GBARenderer;
using LibRecomp;
using RecompiledFuncs;

namespace PMDGreen.Patches;

internal static class PictureMapMargins
{
    private const int PictureColumns = 60;
    private const int PictureRows = 40;
    private const int PictureStride = 64;
    private const int ColumnsFilled = 31;
    private const int RowsFilled = 21;

    private const uint WorldMapPointer = 0x0203B0E8;       // sWorldMapPtr
    private const uint WorldMapBG3Tilemap = 0x1114;        // WorldMap.unk1114
    private const uint WorldMapBG2Tilemap = 0x3114;        // WorldMap.unk3114
    private const uint WorldMapScroll = 0x52D8;            // WorldMap.bgPos
    private const uint FriendAreasMapPointer = 0x0203B0E4; // gFriendAreasMapPtr
    private const uint FriendAreasMapBG3Tilemap = 0x14;    // FriendAreasMap.unk14
    private const uint FriendAreasMapBG2Tilemap = 0x2014;  // FriendAreasMap.unk2014
    private const uint FriendAreasMapScroll = 0x4DD4;      // FriendAreasMap.bgPos

    private static readonly PictureMap WorldMap = new(WorldMapPointer, WorldMapBG3Tilemap, WorldMapBG2Tilemap, WorldMapScroll);
    private static readonly PictureMap FriendAreasMap = new(FriendAreasMapPointer, FriendAreasMapBG3Tilemap, FriendAreasMapBG2Tilemap, FriendAreasMapScroll);

    private static PictureMap? _shown;

    private readonly record struct PictureMap(uint Pointer, uint BG3Tilemap, uint BG2Tilemap, uint Scroll);

    public static void Install()
    {
        Funcs.Patches.WorldMap_RunFrameActions = Showing(WorldMap, Funcs.Patches.WorldMap_RunFrameActions);
        Funcs.Patches.FriendAreasMap_RunFrameActions = Showing(FriendAreasMap, Funcs.Patches.FriendAreasMap_RunFrameActions);
        Funcs.Patches.UpdateBg = FillShownColumns(WorldMap, Funcs.Patches.UpdateBg);
        Funcs.Patches.FriendAreasMap_UpdateBg = FillShownColumns(FriendAreasMap, Funcs.Patches.FriendAreasMap_UpdateBg);
        Funcs.Patches.AnimateSprites = DrawSprites(WorldMap, Funcs.Patches.AnimateSprites);
        Funcs.Patches.AnimateSprites_08010F28 = DrawSprites(FriendAreasMap, Funcs.Patches.AnimateSprites_08010F28);
        Funcs.Patches.SetBG2RegOffsets = ScrollShown(Funcs.Patches.SetBG2RegOffsets);
        Funcs.Patches.SetBG3RegOffsets = ScrollShown(Funcs.Patches.SetBG3RegOffsets);
        GameFrame.Finished += _ => SupplyColumns();
    }

    private static DungeonPosition Scroll(PictureMap map)
    {
        uint data = Memory.Peek<uint>(map.Pointer);
        return data == 0 ? default : Memory.Peek<DungeonPosition>(data + map.Scroll);
    }

    private static int ShownOffset(PictureMap map)
    {
        int margin = GameFrame.Margins.Width;
        int x = Scroll(map).X;
        return Math.Clamp(x, margin, (PictureColumns * 8) - FrameRenderer.Width - margin) - x;
    }

    private static RecompFunc Showing(PictureMap map, RecompFunc runFrame) => ctx =>
    {
        _shown = map;
        runFrame(ctx);
        _shown = null;
    };

    private static RecompFunc ScrollShown(RecompFunc setOffsets) => ctx =>
    {
        if (_shown is { } map)
        {
            ctx.R0 += (uint)ShownOffset(map);
        }

        setOffsets(ctx);
    };

    private static RecompFunc DrawSprites(PictureMap map, RecompFunc draw) => ctx =>
    {
        int offset = ShownOffset(map);
        var first = StagedSprites.SoFar;
        StagedSprites.ShownOffset = offset;
        draw(ctx);
        StagedSprites.ShownOffset = 0;
        StagedSprites.MoveX(first, -offset);
    };

    private static RecompFunc FillShownColumns(PictureMap map, RecompFunc fill) => ctx =>
    {
        fill(ctx);

        int offset = ShownOffset(map);
        if (offset == 0)
        {
            return;
        }

        uint data = Memory.Peek<uint>(map.Pointer);
        var scroll = Scroll(map);
        int firstColumn = (scroll.X + offset) >> 3;
        int firstRow = scroll.Y >> 3;
        for (int row = firstRow; row < firstRow + RowsFilled; row++)
        {
            for (int column = firstColumn; column < firstColumn + ColumnsFilled; column++)
            {
                Tilemaps.Entry(2, column, row) = PictureEntry(data, map.BG2Tilemap, column, row);
                Tilemaps.Entry(3, column, row) = PictureEntry(data, map.BG3Tilemap, column, row);
            }
        }
    };

    private static void SupplyColumns()
    {
        if (_shown is not { } map)
        {
            return;
        }

        uint data = Memory.Peek<uint>(map.Pointer);
        var scroll = Scroll(map);
        scroll = scroll with { X = (short)(scroll.X + ShownOffset(map)) };
        for (int i = 0; i < GameFrame.Margins.ColumnsShown; i++)
        {
            SupplyColumn(data, map, scroll, -i);
            SupplyColumn(data, map, scroll, GameFrame.ScreenColumns + i);
        }
    }

    private static void SupplyColumn(uint data, PictureMap map, DungeonPosition scroll, int column)
    {
        int pictureColumn = (scroll.X >> 3) + column;
        if (pictureColumn is < 0 or >= PictureColumns)
        {
            return;
        }

        var bg3 = GameFrame.Margins.Supply(3, column);
        var bg2 = GameFrame.Margins.Supply(2, column);
        for (int row = 0; row < RowsFilled; row++)
        {
            int pictureRow = Math.Min((scroll.Y >> 3) + row, PictureRows - 1);
            bg3[row] = PictureEntry(data, map.BG3Tilemap, pictureColumn, pictureRow);
            bg2[row] = PictureEntry(data, map.BG2Tilemap, pictureColumn, pictureRow);
        }
    }

    private static ushort PictureEntry(uint data, uint tilemap, int column, int row)
    {
        return Memory.Peek<ushort>(data + tilemap + (uint)(((row * PictureStride) + column) * sizeof(ushort)));
    }
}
