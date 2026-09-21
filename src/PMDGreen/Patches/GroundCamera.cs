using System.Numerics;
using System.Runtime.InteropServices;
using AGBModern;
using GBARenderer;
using LibRecomp;
using RecompiledFuncs;

namespace PMDGreen.Patches;

internal static class GroundCamera
{
    private const uint StateAddress = 0x020399E8; // sUnknown_20399E8

    private const int OffsetX = 0x80;
    private const int OffsetY = 2944;
    private const int OriginX = 30976;
    private const int OriginY = 20480;

    private static bool _isUpdating;
    private static (PixelPosition Min, PixelPosition Max)? _limits;
    private static (PixelPosition Min, PixelPosition Max) _mapLimits;

    [StructLayout(LayoutKind.Explicit)]
    private struct State
    {
        [FieldOffset(0x14)] // unkStruct_20399E8.unk14
        public PixelPosition Target;

        [FieldOffset(0x34)] // unkStruct_20399E8.unk34
        public int ShakeX;

        [FieldOffset(0x38)] // unkStruct_20399E8.unk38
        public int ShakeY;

        [FieldOffset(0x3C)] // unkStruct_20399E8.unk3C
        public PixelPosition Shown;

        [FieldOffset(0x44)] // unkStruct_20399E8.unk44
        public byte IsSet;
    }

    public static int ShownOffset { get; private set; }

    public static Vector2 Shown => ShownByGame + new Vector2(ShownOffset, 0);

    public static Vector2 ShownByGame
    {
        get
        {
            var state = Memory.Peek<State>(StateAddress);
            return state.IsSet != 0 ? state.Shown.Whole : Vector2.Zero;
        }
    }

    public static Vector2 Position
    {
        get
        {
            var state = Memory.Peek<State>(StateAddress);
            if (state.IsSet == 0 || _limits is not var (min, max))
            {
                return Shown;
            }

            int x = Limit(state.Target.X + state.ShakeX - OffsetX, min.X, max.X) - OriginX;
            int y = Limit(state.Target.Y + state.ShakeY - OffsetY, min.Y, max.Y) - OriginY;
            var rounded = new Vector2(x - 0x80, y - 0x80) / 256;

            var half = new Vector2(0.5f);
            return Shown + Vector2.Clamp(rounded - Shown, -half, half);
        }
    }

    public static (int Left, int Right) ShowableX
    {
        get
        {
            if (GroundMap.GoesOnSideways)
            {
                return (int.MinValue / 2, int.MaxValue / 2);
            }

            int shown = (int)Shown.X;
            if (!GroundMap.IsAPlace || _limits is null || Memory.Peek<State>(StateAddress).IsSet == 0)
            {
                return (shown, shown + FrameRenderer.Width);
            }

            var (min, max) = _mapLimits;
            int last = (max.X - 1 - OriginX) / 256;
            int first = Math.Min((min.X - OriginX) / 256, last);
            return (first, last + FrameRenderer.Width);
        }
    }

    public static void Install()
    {
        Funcs.Patches.sub_809D25C = Update(Funcs.Patches.sub_809D25C);
        Funcs.Patches.sub_80A579C = GetLimits(Funcs.Patches.sub_80A579C);
        GroundMap.Selected += _ => ShownOffset = 0;
    }

    private static int Limit(int value, int min, int max)
    {
        value = Math.Max(value, min);
        return value >= max ? max - 1 : value;
    }

    private static RecompFunc Update(RecompFunc update) => ctx =>
    {
        _isUpdating = true;
        _limits = null;
        update(ctx);
        _isUpdating = false;

        var state = Memory.Peek<State>(StateAddress);
        if (state.IsSet == 0 || _limits is not var (min, max))
        {
            ShownOffset = 0;
            return;
        }

        int x = Limit(state.Target.X + state.ShakeX - OffsetX, min.X, max.X);
        ShownOffset = ((x - OriginX) / 256) - state.Shown.X;
    };

    private static RecompFunc GetLimits(RecompFunc getLimits) => ctx =>
    {
        uint min = ctx.R0;
        uint max = ctx.R1;
        getLimits(ctx);

        if (!_isUpdating || (byte)ctx.R0 == 0)
        {
            return;
        }

        _mapLimits = (Memory.Peek<PixelPosition>(min), Memory.Peek<PixelPosition>(max));
        var (shownMin, shownMax) = _mapLimits;
        KeepWiderPictureOnTheMap(ref shownMin, ref shownMax);
        _limits = (shownMin, shownMax);
    };

    private static void KeepWiderPictureOnTheMap(ref PixelPosition min, ref PixelPosition max)
    {
        int margin = GameFrame.Margins.Width * 256;
        if (margin == 0)
        {
            return;
        }

        int start = Math.Max(min.X, OriginX);

        if (min.X >= max.X)
        {
            (min, max) = (min with { X = OriginX }, max with { X = OriginX + 1 });
        }
        else if (!GroundMap.IsAPlace)
        {
            min = min with { X = Math.Min(start, max.X - 1) };
        }
        else if (start + margin < max.X - margin)
        {
            (min, max) = (min with { X = start + margin }, max with { X = max.X - margin });
        }
        else
        {
            int middle = (min.X + max.X) / 2;
            (min, max) = (min with { X = middle }, max with { X = middle + 1 });
        }
    }
}
