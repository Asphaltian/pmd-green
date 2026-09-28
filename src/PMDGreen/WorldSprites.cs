using System.Numerics;
using AGBModern;
using GBARenderer;
using LibRecomp;
using PMDGreen.Patches;
using RecompiledFuncs;

namespace PMDGreen;

/// <summary>
/// <c>WorldSprites</c> is where you draw sprites that belong to the world rather than the screen,
/// like a sign in town or an arrow over a monster. Sprites you draw here scroll smoothly with the
/// camera, move smoothly when they move by themselves, and carry on into the widescreen margins.
/// </summary>
/// <example>
/// <code>
/// WorldSprites.Drawing += (ctx, camera) =>
/// {
///     // A sign that stays where it is on the map
///     DrawSign(ctx, signPosition - camera);
///
///     // An arrow over a monster moves with it, so track where it is
///     var motion = WorldSprites.Track(arrowAddress, arrowPosition, arrowPosition);
///     WorldSprites.Draw(ctx, c => DrawArrow(c, arrowPosition - camera), motion);
/// };
/// </code>
/// </example>
public static class WorldSprites
{
    /// <summary>
    /// Runs every frame in towns and dungeons, right after the game draws its own sprites of the world.
    /// You also get the point of the world at the top-left corner of the screen, in pixels, so draw each
    /// sprite at its place in the world minus that. Whatever you do in here doesn't change the game's
    /// timing.
    /// </summary>
    public static event Action<RecompContext, Vector2>? Drawing;

    /// <summary>Draws sprites that move by themselves. Pass how they moved since the last frame, which you get from <see cref="Track"/>.</summary>
    public static void Draw(RecompContext ctx, RecompFunc draw, Displacement motion)
    {
        var first = StagedSprites.SoFar;
        draw(ctx);
        MotionReport.MarkWorldSprites(first, motion);
    }

    /// <summary>
    /// Works out how something moved since the last frame, for <see cref="Draw"/>. Call it every frame
    /// you draw that thing, with a key that belongs to it alone, like the address of something of yours
    /// from <see cref="Memory.Allocate"/>. <paramref name="position"/> is where it is in the world in
    /// pixels, fractions included, and <paramref name="shown"/> is the whole pixel you draw it at.
    /// </summary>
    public static Displacement Track(uint key, Vector2 position, Vector2 shown)
    {
        return MotionReport.Track(key, position, shown);
    }

    internal static RecompFunc InWorld(RecompFunc draw) => ctx => Draw(ctx, draw, default);

    internal static void Install()
    {
        Funcs.Patches.sub_80A6E80 = DrawGround(Funcs.Patches.sub_80A6E80);
        Funcs.Patches.sub_804522C = DrawDungeon(Funcs.Patches.sub_804522C);
    }

    private static RecompFunc DrawGround(RecompFunc drawSprites) => ctx =>
    {
        int offset = GroundCamera.ShownOffset;
        var first = StagedSprites.SoFar;
        StagedSprites.ShownOffset = offset;
        drawSprites(ctx);
        RaiseDrawing(ctx, GroundCamera.ShownByGame);
        StagedSprites.ShownOffset = 0;
        StagedSprites.MoveX(first, -offset);
    };

    private static RecompFunc DrawDungeon(RecompFunc drawMonsters) => ctx =>
    {
        drawMonsters(ctx);
        RaiseDrawing(ctx, DungeonCamera.Current.Position.Whole);
    };

    private static void RaiseDrawing(RecompContext ctx, Vector2 camera)
    {
        if (Drawing is { } drawing)
        {
            Scheduler.RunUntimed(() => Draw(ctx, c => drawing(c, camera), default));
        }
    }
}
