using AGBModern;
using GBARenderer;
using LibRecomp;
using PMDGreen.Patches;
using RecompiledFuncs;

namespace PMDGreen;

/// <summary>
/// <c>GameFrame</c> is the frame the game is drawing right now. Usually you may need it to add your
/// own tiles to the widescreen margins, or to turn the margins and smooth motion off while your mod
/// shows something they don't suit.
/// </summary>
/// <example>
/// <code>
/// GameFrame.Finished += ctx =>
/// {
///     // Carry BG1 on past both edges of the screen
///     for (int i = 0; i &lt; GameFrame.Margins.ColumnsShown; i++)
///     {
///         CopyMapColumn(GameFrame.Margins.Supply(1, -i), firstColumn - i);
///         CopyMapColumn(GameFrame.Margins.Supply(1, 30 + i), firstColumn + 30 + i);
///     }
/// };
///
/// // Keep the picture 240 pixels wide while your screen is up
/// GameFrame.ShowsMargins = false;
/// </code>
/// </example>
public static class GameFrame
{
    internal const int ScreenColumns = FrameRenderer.Width / 8;

    private static volatile bool _showsMargins = true;

    /// <summary>
    /// Runs once a frame, after the game has finished drawing it and we've filled in the margins. This
    /// is where you supply your own columns. Whatever you do in here doesn't change the game's timing.
    /// </summary>
    public static event Action<RecompContext>? Finished;

    /// <summary>The margins of the frame the game is drawing. Supply your columns from <see cref="Finished"/>.</summary>
    public static FrameMargins Margins { get; } = new();

    /// <summary>
    /// Set this to false to hide the widescreen margins while your mod shows something that doesn't
    /// carry on past the edges of the screen. Don't forget to set it back afterwards.
    /// </summary>
    public static bool ShowsMargins
    {
        get => _showsMargins;
        set => _showsMargins = value;
    }

    /// <summary>Set this to false while your mod shows something that smooth motion gets wrong. Don't forget to set it back afterwards.</summary>
    public static bool InterpolatesMotion { get; set; } = true;

    internal static FrameMotion Motion { get; } = new();

    internal static void Install()
    {
        var copySprites = Funcs.Patches.CopySpritesToOam;
        Funcs.Patches.CopySpritesToOam = ctx =>
        {
            copySprites(ctx);
            StagedSprites.ShowExtras(ctx, copySprites);

            Margins.Clear();
            if (Finished is { } finished)
            {
                Scheduler.RunUntimed(() => finished(ctx));
            }

            StagedSprites.ShowGameSprites();

            if (!InterpolatesMotion)
            {
                Array.Clear(Motion.Backgrounds);
                Array.Clear(Motion.Objects);
                Array.Clear(Motion.Windows);
            }
        };
    }
}
