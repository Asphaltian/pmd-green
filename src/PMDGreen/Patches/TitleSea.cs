using AGBModern;
using GBARenderer;
using LibRecomp;
using RecompiledFuncs;

namespace PMDGreen.Patches;

internal static class TitleSea
{
    private const int ProhibitedShape = 3;
    private const int LowestX = -128;

    private static readonly byte[] Widths = [8, 16, 32, 64, 16, 32, 32, 64, 8, 8, 16, 32];

    public static void Install()
    {
        var linkSprites = Funcs.Patches.sub_8005180;
        Funcs.Patches.sub_8005180 = ctx =>
        {
            if (GroundMap.GoesOnSideways && GameFrame.Margins.Width > 0)
            {
                ContinueStrips(ctx);
            }

            linkSprites(ctx);
        };
    }

    private static void ContinueStrips(RecompContext ctx)
    {
        int margin = GameFrame.Margins.Width;
        int count = StagedSprites.Count;
        var extras = StagedSprites.ExtraSprites.ToList();
        var pieces = Enumerable.Range(0, count).Select(StagedSprites.Get).Concat(extras.Select(extra => extra.Sprite)).ToList();
        for (int i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            if ((piece.Attribute0 & SpriteOAM.AffineFlag) != 0 || piece.Attribute0 >> 14 == ProhibitedShape)
            {
                continue;
            }

            int width = Width(piece), x = X(piece);
            bool hasLeft = pieces.Any(other => IsBeside(other, piece, -width));
            bool hasRight = pieces.Any(other => IsBeside(other, piece, width));
            int step = hasRight && !hasLeft && x < 0 ? -width
                : hasLeft && !hasRight && x + width > FrameRenderer.Width ? width
                : 0;
            if (step == 0)
            {
                continue;
            }

            int order = i < count ? OrderOf(i) : extras[i - count].Order;
            if (order < 0)
            {
                continue;
            }

            for (int copyX = x + step; copyX + width > -margin && copyX >= LowestX && copyX < FrameRenderer.Width + margin; copyX += step)
            {
                AddCopy(ctx, piece, copyX, order);
            }
        }

        StagedSprites.SetAside(count);
    }

    private static bool IsBeside(SpriteOAM other, SpriteOAM piece, int offset)
    {
        return other.Attribute0 == piece.Attribute0
            && other.Attribute2 == piece.Attribute2
            && (other.Attribute1 & ~SpriteOAM.XMask) == (piece.Attribute1 & ~SpriteOAM.XMask)
            && X(other) == X(piece) + offset;
    }

    private static void AddCopy(RecompContext ctx, SpriteOAM piece, int x, int order)
    {
        var copy = piece;
        copy.SetX(x);
        copy.SetWorkingY(piece.Attribute0 & 0xFF);
        Scheduler.RunUntimed(() =>
        {
            ctx.R13 -= StagedSprites.Size;
            Memory.Poke<SpriteOAM>(ctx.R13) = copy;
            (ctx.R0, ctx.R1, ctx.R2, ctx.R3) = (ctx.R13, (uint)order, 0, 0);
            Funcs.AddSprite(ctx);
            ctx.R13 += StagedSprites.Size;
        });
    }

    private static int OrderOf(int index)
    {
        uint link = StagedSprites.LinksAddress + (uint)(index * StagedSprites.LinkSize);
        for (int order = 0; order < StagedSprites.Orders; order++)
        {
            uint next = Memory.Peek<StagedSprites.Link>(StagedSprites.ListAddress + (uint)(order * StagedSprites.LinkSize)).Next;
            while (next != 0 && next - StagedSprites.ListAddress >= StagedSprites.ListSize)
            {
                if (next == link)
                {
                    return order;
                }

                next = Memory.Peek<StagedSprites.Link>(next).Next;
            }
        }

        return -1;
    }

    private static int Width(SpriteOAM piece) => Widths[((piece.Attribute0 >> 14) * 4) + (piece.Attribute1 >> 14)];

    private static int X(SpriteOAM piece)
    {
        int x = piece.Attribute1 & SpriteOAM.XMask;
        return x >= 512 + LowestX ? x - 512 : x;
    }
}
