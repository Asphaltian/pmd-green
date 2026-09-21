using System.Numerics;

namespace PMDGreen.Patches;

internal readonly record struct PixelPosition(int X, int Y)
{
    public Vector2 Whole => new(X, Y);

    public Vector2 InPixels => new Vector2(X, Y) / 256;

    public Vector2 Shown => new(X / 256, Y / 256);
}
