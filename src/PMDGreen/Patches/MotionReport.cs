using System.Numerics;
using AGBModern;
using GBARenderer;

namespace PMDGreen.Patches;

internal static class MotionReport
{
    private const float MaxStep = 16;
    private const uint CameraId = FrameMotion.BackgroundCount;

    private static readonly bool[] IsInWorld = new bool[FrameMotion.ObjectCount];
    private static readonly Displacement[] Own = new Displacement[FrameMotion.ObjectCount];
    private static readonly bool[] IsWindowInWorld = new bool[FrameMotion.WindowCount];
    private static readonly Displacement?[] BackgroundOffsets = new Displacement?[FrameMotion.BackgroundCount];

    private static Displacement _camera;
    private static Dictionary<uint, Tracked> _previous = [];
    private static Dictionary<uint, Tracked> _current = [];

    private readonly record struct Tracked(Vector2 Position, Vector2 Drawn);

    public static void Install()
    {
        GameFrame.Finished += _ => Report();
    }

    public static Displacement Track(uint id, Vector2 position, Vector2 shown, Vector2? repeatsEvery = null)
    {
        var displacement = default(Displacement);

        if (_previous.TryGetValue(id, out var before))
        {
            var step = position - before.Position;
            var from = before.Drawn - shown;
            if (repeatsEvery is { } size)
            {
                step = Remainder(step, size);
                from = Remainder(from, size);
            }

            if (MathF.Abs(step.X) <= MaxStep && MathF.Abs(step.Y) <= MaxStep)
            {
                displacement.From = from;
                displacement.To = new Vector2(step.X != 0 ? position.X - shown.X : 0, step.Y != 0 ? position.Y - shown.Y : 0);
            }
        }

        _current[id] = new Tracked(position, shown + displacement.To);
        return displacement;
    }

    public static void Forget() => _previous.Clear();

    public static void SetCamera(Vector2 position, Vector2 shown)
    {
        var scrolled = Track(CameraId, position, shown);
        _camera = new Displacement { From = -scrolled.From, To = -scrolled.To };
    }

    public static void SetBackground(int bg, Vector2 offset, Vector2 shown, Vector2? repeatsEvery = null)
    {
        var scrolled = Track((uint)bg, offset, shown, repeatsEvery);
        BackgroundOffsets[bg] = new Displacement { From = -scrolled.From, To = -scrolled.To };
    }

    public static void MarkWorldSprites(StagedSprites.Staged first, Displacement own = default)
    {
        int end = Math.Min(StagedSprites.Count, FrameMotion.ObjectCount);
        for (int i = first.Sprites; i < end; i++)
        {
            if (!IsInWorld[i])
            {
                IsInWorld[i] = true;
                Own[i] = own;
            }
        }

        StagedSprites.MarkExtras(first.Extras, own);
    }

    public static (bool IsInWorld, Displacement Own) TakeMark(int index)
    {
        if (index >= FrameMotion.ObjectCount)
        {
            return default;
        }

        var mark = (IsInWorld[index], Own[index]);
        (IsInWorld[index], Own[index]) = (false, default);
        return mark;
    }

    public static void Mark(int index, bool isInWorld, Displacement own)
    {
        if (index < FrameMotion.ObjectCount)
        {
            (IsInWorld[index], Own[index]) = (isInWorld, own);
        }
    }

    public static void MarkWorldWindow(int window)
    {
        IsWindowInWorld[window] = true;
    }

    private static Vector2 Remainder(Vector2 value, Vector2 size)
    {
        return new Vector2(MathF.IEEERemainder(value.X, size.X), MathF.IEEERemainder(value.Y, size.Y));
    }

    private static Displacement Sum(Displacement a, Displacement b)
    {
        return new Displacement { From = a.From + b.From, To = a.To + b.To };
    }

    private static void Report()
    {
        for (int bg = 0; bg < FrameMotion.BackgroundCount; bg++)
        {
            GameFrame.Motion.Backgrounds[bg] = BackgroundOffsets[bg] is { } offset ? Sum(_camera, offset) : default;
        }

        for (int window = 0; window < FrameMotion.WindowCount; window++)
        {
            GameFrame.Motion.Windows[window] = IsWindowInWorld[window] ? _camera : default;
        }

        ReportSprites();

        (_previous, _current) = (_current, _previous);
        _current.Clear();

        _camera = default;
        Array.Clear(BackgroundOffsets);
        Array.Clear(IsInWorld);
        Array.Clear(IsWindowInWorld);
    }

    private static void ReportSprites()
    {
        int slot = FrameMotion.ObjectCount;
        for (uint address = StagedSprites.ListAddress; address != 0 && slot > 1;)
        {
            var link = Memory.Peek<StagedSprites.Link>(address);
            if (link.Sprite != 0)
            {
                slot--;
                int staged = (int)(link.Sprite - StagedSprites.Address) / StagedSprites.Size;
                GameFrame.Motion.Objects[slot] = IsInWorld[staged] ? Sum(_camera, Own[staged]) : default;
            }

            address = link.Next;
        }

        Array.Clear(GameFrame.Motion.Objects, 0, slot);
    }
}
