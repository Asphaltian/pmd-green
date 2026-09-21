using System.Numerics;

namespace PMDGreen.Patches;

internal readonly record struct DungeonPosition(short X, short Y)
{
    public Vector2 Whole => new(X, Y);
}
