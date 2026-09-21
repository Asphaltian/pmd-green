using System.Runtime.InteropServices;
using AGBModern;
using GBARenderer;
using LibRecomp;
using RecompiledFuncs;

namespace PMDGreen.Patches;

internal static class GroundMargins
{
    private const int RowsSupplied = 24;

    private static readonly ushort[] KeptTilemaps = new ushort[2 * Tilemaps.EntriesPerTilemap];
    private static readonly byte[] KeptMapRender = new byte[GroundMap.MapRenderSize];

    public static void Install()
    {
        GameFrame.Finished += SupplyColumns;
    }

    private static void SupplyColumns(RecompContext ctx)
    {
        int count = GameFrame.Margins.ColumnsShown;
        if (count == 0)
        {
            return;
        }

        if (GroundMap.IsAPicture)
        {
            foreach (var layer in GroundMap.DrawnLayers)
            {
                ContinueBorder(layer, screenX: 4, firstColumn: 0, step: -1);
                ContinueBorder(layer, screenX: 235, firstColumn: GameFrame.ScreenColumns, step: 1);
            }

            return;
        }

        var tilemaps = MemoryMarshal.CreateSpan(ref Tilemaps.Entry(bg: 2, 0, 0), KeptTilemaps.Length);
        tilemaps.CopyTo(KeptTilemaps);

        foreach (var layer in GroundMap.DrawnLayers)
        {
            Redraw(ctx, layer, count);
        }

        KeptTilemaps.CopyTo(tilemaps);
    }

    private static void ContinueBorder(GroundMap.Layer layer, int screenX, int firstColumn, int step)
    {
        var scroll = Memory.Peek<GroundMap.MapRender>(layer.MapRender).Scroll;
        int column = (scroll.X + screenX) >> 3;
        int firstRow = scroll.Y >> 3;

        ushort border = Tilemaps.Entry(layer.Background, column, firstRow);
        for (int row = 1; row < 20; row++)
        {
            if (Tilemaps.Entry(layer.Background, column, firstRow + row) != border)
            {
                return;
            }
        }

        for (int i = 0; i < FrameMargins.ColumnsPerSide; i++)
        {
            GameFrame.Margins.Supply(layer.Background, firstColumn + (i * step)).Fill(border);
        }
    }

    private static void Redraw(RecompContext ctx, GroundMap.Layer layer, int count)
    {
        var map = Memory.Peek<GroundMap.MapRender>(layer.MapRender);

        var state = MemoryMarshal.CreateSpan(ref Memory.Poke<byte>(layer.MapRender), (int)GroundMap.MapRenderSize);
        state.CopyTo(KeptMapRender);

        int onTheLeft = map.Repeats != 0 ? count : Math.Min(count, (map.Camera.X >> 3) + 1);
        SupplySide(ctx, layer, map, firstColumn: 1 - onTheLeft, onTheLeft);
        SupplySide(ctx, layer, map, firstColumn: GameFrame.ScreenColumns, count);

        KeptMapRender.CopyTo(state);
    }

    private static void SupplySide(RecompContext ctx, GroundMap.Layer layer, GroundMap.MapRender map, int firstColumn, int count)
    {
        var moved = map.Camera with { X = map.Camera.X + (firstColumn * 8) };
        if (map.Repeats != 0)
        {
            moved = moved with { X = ((moved.X % map.MapSize.X) + map.MapSize.X) % map.MapSize.X };
        }

        Scheduler.RunUntimed(() =>
        {
            ctx.R13 -= 8;
            Memory.Poke<PixelPosition>(ctx.R13) = moved;
            (ctx.R0, ctx.R1) = (layer.MapRender, ctx.R13);
            Funcs.Original.UpdateMapCameraPosition(ctx);

            ctx.R0 = layer.MapRender;
            Recomp.LookupFunc(map.Draw)(ctx);
            ctx.R13 += 8;
        });

        var scroll = Memory.Peek<GroundMap.MapRender>(layer.MapRender).Scroll;
        var (left, right) = GroundCamera.ShowableX;

        for (int i = 0; i < count; i++)
        {
            int mapX = (((int)GroundCamera.Shown.X >> 3) + firstColumn + i) * 8;
            if (map.Repeats == 0 && (mapX + 8 <= left || mapX >= right))
            {
                continue;
            }

            var supplied = GameFrame.Margins.Supply(layer.Background, firstColumn + i);
            for (int row = 0; row < RowsSupplied; row++)
            {
                supplied[row] = Tilemaps.Entry(layer.Background, (scroll.X >> 3) + i, (scroll.Y >> 3) + row);
            }
        }
    }
}
