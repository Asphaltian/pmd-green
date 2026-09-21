using System.Numerics;
using AGBModern;
using GBARenderer;
using LibRecomp;
using RecompiledFuncs;

namespace PMDGreen.Patches;

internal static class GroundMotion
{
    private const uint StandingPlaceOffset = 0x74; // UnkGroundSpriteStruct.unk74

    private static Displacement _standing;

    public static void Install()
    {
        Funcs.Patches.sub_80A6E80 = DrawSprites(Funcs.Patches.sub_80A6E80);
        Funcs.Patches.sub_80A7094 = DrawCharacter(Funcs.Patches.sub_80A7094);

        Funcs.Patches.sub_80A7524 = DrawCharacter(Funcs.Patches.sub_80A7524);
        Funcs.Patches.AddShadowSprite = DrawShadow(Funcs.Patches.AddShadowSprite);

        GroundMap.LayerDrawn += layer =>
        {
            var shown = layer.Camera.Whole - GroundCamera.Shown;
            var map = Memory.Peek<GroundMap.MapRender>(layer.MapRender);
            MotionReport.SetBackground(
                layer.Background,
                shown + GroundMap.DriftFraction(layer),
                shown,
                repeatsEvery: map.Repeats != 0 ? map.MapSize.Whole : null);
        };

        GroundMap.Selected += _ => MotionReport.Forget();

        var selectWeather = Funcs.Patches.GroundWeather_Select;
        Funcs.Patches.GroundWeather_Select = ctx =>
        {
            selectWeather(ctx);
            MotionReport.Forget();
        };
    }

    private static RecompFunc DrawSprites(RecompFunc drawSprites) => ctx =>
    {
        drawSprites(ctx);
        MotionReport.SetCamera(GroundCamera.Position, GroundCamera.Shown);
    };

    private static RecompFunc DrawCharacter(RecompFunc drawCharacter) => ctx =>
    {
        uint character = ctx.R0;
        var position = Memory.Peek<PixelPosition>(ctx.R2);
        int height = (int)ctx.R3;

        var body = WorldSprites.Track(
            character,
            position.InPixels - new Vector2(0, height / 256f),
            position.Shown - new Vector2(0, height / 256));
        _standing = WorldSprites.Track(character + StandingPlaceOffset, position.InPixels, position.Shown);

        WorldSprites.Draw(ctx, drawCharacter, body);
    };

    private static RecompFunc DrawShadow(RecompFunc addShadow) => ctx =>
    {
        WorldSprites.Draw(ctx, addShadow, _standing);
    };
}
