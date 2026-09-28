using System.Numerics;
using RecompiledFuncs;

namespace PMDGreen;

/// <summary>
/// <c>RecompInput</c> gives you the controls a GBA doesn't have: the right stick and the mouse. Usually
/// you may need it if your mod moves a camera around or aims at something. The GBA's own buttons still
/// go to the game, so read those the same way the game does.
/// </summary>
/// <example>
/// <code>
/// mod.HookReturn(ref Funcs.Patches.sub_8000400, _ =>
/// {
///     // Move your camera 4 pixels a frame with the stick tilted all the way
///     _camera += (RecompInput.CameraInputs * 4) + RecompInput.MouseDeltas;
/// });
/// </code>
/// </example>
public static class RecompInput
{
    private const float RadialDeadzone = 0.05f;

    /// <summary>
    /// How far the right stick is tilted. Each axis goes from about -1 to 1, right and down are
    /// positive, and a stick at rest reads zero.
    /// </summary>
    public static Vector2 CameraInputs
    {
        get
        {
            var stick = Input.RightAnalog;
            float magnitude = stick.Length();
            return magnitude < RadialDeadzone
                ? Vector2.Zero
                : stick / magnitude * ((magnitude - RadialDeadzone) / (1 - RadialDeadzone));
        }
    }

    /// <summary>
    /// How far the mouse moved over the window during the last frame. Right and down are positive.
    /// Please note that it only changes once a frame, so read it once a frame too.
    /// </summary>
    public static Vector2 MouseDeltas { get; private set; }

    internal static void Install()
    {
        var readKeys = Funcs.Patches.ReadKeyInput;
        Funcs.Patches.ReadKeyInput = ctx =>
        {
            MouseDeltas = Input.TakeMouseDelta();
            readKeys(ctx);
        };
    }
}
