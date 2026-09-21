using System.Runtime.InteropServices;
using AGBModern;
using LibRecomp;
using RecompiledFuncs;

namespace PMDGreen.Patches;

internal static class DungeonMargins
{
    private const int RowsSupplied = 22;
    private const int GridRows = 21;
    private const int GridColumns = 31;

    private static bool _cameraWasUpdated;
    private static Grid? _grid;

    private readonly record struct Grid(DungeonPosition Target, uint Direction, uint IsTargeting, uint Scrolls);

    public static void Install()
    {
        var updateCamera = Funcs.Patches.UpdateCamera;
        Funcs.Patches.UpdateCamera = ctx =>
        {
            updateCamera(ctx);
            _cameraWasUpdated = true;
        };

        var moveCamera = Funcs.Patches.sub_803F878;
        Funcs.Patches.sub_803F878 = ctx =>
        {
            moveCamera(ctx);
            _cameraWasUpdated = true;
        };

        var drawGrid = Funcs.Patches.ChangeDungeonCameraPos;
        Funcs.Patches.ChangeDungeonCameraPos = ctx =>
        {
            _grid = new Grid(Memory.Peek<DungeonPosition>(ctx.R0), ctx.R1, ctx.R2, ctx.R3);
            drawGrid(ctx);
        };

        GameFrame.Finished += SupplyColumns;
    }

    private static void SupplyColumns(RecompContext ctx)
    {
        bool isInDungeon = _cameraWasUpdated;
        _cameraWasUpdated = false;

        if (!isInDungeon)
        {
            return;
        }

        int count = GameFrame.Margins.ColumnsShown;
        for (int i = 0; i < count; i++)
        {
            SupplyColumn(ctx, -i);
            SupplyColumn(ctx, GameFrame.ScreenColumns + i);
        }

        if (count > 0 && DungeonCamera.Current.ScrollsBG2 != 0 && _grid is { } grid)
        {
            SupplyGrid(ctx, grid, firstColumn: 1 - count, count);
            SupplyGrid(ctx, grid, firstColumn: GameFrame.ScreenColumns, count);
        }
    }

    private static void SupplyGrid(RecompContext ctx, Grid grid, int firstColumn, int count)
    {
        var camera = DungeonCamera.Current.Position;
        int mapColumn = (camera.X >> 3) + firstColumn;
        int mapRow = camera.Y >> 3;

        var tilemap = MemoryMarshal.CreateSpan(ref Tilemaps.Entry(DungeonCamera.GridBackground, 0, 0), Tilemaps.EntriesPerTilemap);
        ushort[] kept = tilemap.ToArray();
        byte wasCopyScheduled = Tilemaps.IsCopyScheduled(DungeonCamera.GridBackground);

        Scheduler.RunUntimed(() =>
        {
            ref var changeable = ref DungeonCamera.Changeable;
            changeable.Position = camera with { X = (short)(camera.X + (firstColumn * 8)) };
            ctx.R13 -= 8;
            Memory.Poke<DungeonPosition>(ctx.R13) = grid.Target;
            (ctx.R0, ctx.R1, ctx.R2, ctx.R3) = (ctx.R13, grid.Direction, grid.IsTargeting, grid.Scrolls);
            Funcs.Original.ChangeDungeonCameraPos(ctx);
            ctx.R13 += 8;
            changeable.Position = camera;
        });

        for (int i = 0; i < Math.Min(count, GridColumns); i++)
        {
            var supplied = GameFrame.Margins.Supply(DungeonCamera.GridBackground, firstColumn + i);
            for (int row = 0; row < GridRows; row++)
            {
                supplied[row] = Tilemaps.Entry(DungeonCamera.GridBackground, mapColumn + i, mapRow + row);
            }
        }

        kept.CopyTo(tilemap);
        Tilemaps.IsCopyScheduled(DungeonCamera.GridBackground) = wasCopyScheduled;
    }

    private static void SupplyColumn(RecompContext ctx, int column)
    {
        var camera = DungeonCamera.Current;
        int mapColumn = (camera.Position.X >> 3) + column;
        int mapRow = (camera.Position.Y - camera.MapOffsetY) >> 3;

        Span<ushort> kept = stackalloc ushort[32];
        for (int row = 0; row < kept.Length; row++)
        {
            kept[row] = Tilemaps.Entry(DungeonCamera.MapBackground, mapColumn, row);
        }

        byte wasCopyScheduled = Tilemaps.IsCopyScheduled(DungeonCamera.MapBackground);

        Scheduler.RunUntimed(() =>
        {
            (ctx.R0, ctx.R1) = ((uint)(column * 8), 0);
            Funcs.sub_804A1F0(ctx);
        });

        var supplied = GameFrame.Margins.Supply(DungeonCamera.MapBackground, column);
        for (int row = 0; row < RowsSupplied; row++)
        {
            supplied[row] = Tilemaps.Entry(DungeonCamera.MapBackground, mapColumn, mapRow + row);
        }

        for (int row = 0; row < kept.Length; row++)
        {
            Tilemaps.Entry(DungeonCamera.MapBackground, mapColumn, row) = kept[row];
        }

        Tilemaps.IsCopyScheduled(DungeonCamera.MapBackground) = wasCopyScheduled;
    }
}
