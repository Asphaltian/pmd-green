using System.Numerics;
using AGBModern;
using GBARenderer;
using LibRecomp;
using RecompiledFuncs;

namespace PMDGreen.Patches;

internal static class DungeonMotion
{
    private const uint DungeonSpriteIdOffset = 0x98; // EntityInfo.dungeonSpriteId
    private const uint LiftOffset = 0x174;           // EntityInfo.unk174
    private const uint StatusSpriteIdOffset = 0x04;  // DungeonPokemonSprite.id

    private static readonly Vector2 TargetOnScreen = new(120, 96);
    private static readonly Dictionary<uint, Displacement> MotionBySpriteId = [];

    private static Displacement? _body;

    public static void Install()
    {
        Funcs.Patches.UpdateCamera = UpdateCamera(Funcs.Patches.UpdateCamera);
        Funcs.Patches.sub_803F878 = MoveCamera(Funcs.Patches.sub_803F878);
        GameFrame.Finished += _ => MotionBySpriteId.Clear();
        Funcs.Patches.UpdateMonsterSprite = UpdateMonsterSprite(Funcs.Patches.UpdateMonsterSprite);
        Funcs.Patches.DoAxFrame_800558C = DrawBody(Funcs.Patches.DoAxFrame_800558C);
        Funcs.Patches.UpdateStatusSprite = UpdateStatusSprite(Funcs.Patches.UpdateStatusSprite);
        Funcs.Patches.sub_80462AC = DrawItem(Funcs.Patches.sub_80462AC);

        Funcs.Patches.sub_800E90C = WorldSprites.InWorld(Funcs.Patches.sub_800E90C);
        Funcs.Patches.sub_8083568 = WorldSprites.InWorld(Funcs.Patches.sub_8083568);
    }

    private static RecompFunc UpdateCamera(RecompFunc updateCamera) => ctx =>
    {
        updateCamera(ctx);

        var camera = DungeonCamera.Current;
        if (camera.Target == 0)
        {
            return;
        }

        var target = Memory.Peek<Entity>(camera.Target);
        if (target.Type != Entity.Nothing)
        {
            ReportCamera(target.Position.InPixels - TargetOnScreen);
        }
    };

    private static RecompFunc MoveCamera(RecompFunc moveCamera) => ctx =>
    {
        var center = new PixelPosition((int)ctx.R0, (int)ctx.R1);
        moveCamera(ctx);
        ReportCamera(center.InPixels - TargetOnScreen);
    };

    private static void ReportCamera(Vector2 position)
    {
        var camera = DungeonCamera.Current;
        var mapOffset = new Vector2(0, camera.MapOffsetY);

        MotionReport.SetCamera(position, camera.Position.Whole);
        MotionReport.SetBackground(DungeonCamera.MapBackground, -mapOffset, -mapOffset);
        if (camera.ScrollsBG2 != 0)
        {
            MotionReport.SetBackground(DungeonCamera.GridBackground, Vector2.Zero, Vector2.Zero);
        }
    }

    private static RecompFunc UpdateMonsterSprite(RecompFunc updateMonster) => ctx =>
    {
        uint monster = ctx.R0;
        var entity = Memory.Peek<Entity>(monster);

        int lift = Memory.Peek<int>(entity.Info + LiftOffset);
        var raised = entity.Position with { Y = entity.Position.Y - entity.Height - lift };

        var body = WorldSprites.Track(monster, raised.InPixels, raised.Shown);
        var shadow = WorldSprites.Track(monster + Entity.PositionOffset, entity.Position.InPixels, entity.Position.Shown);
        MotionBySpriteId[Memory.Peek<uint>(entity.Info + DungeonSpriteIdOffset)] = body;

        _body = body;
        WorldSprites.Draw(ctx, updateMonster, shadow);
        _body = null;
    };

    private static RecompFunc DrawBody(RecompFunc drawBody) => ctx =>
    {
        if (_body is { } body)
        {
            WorldSprites.Draw(ctx, drawBody, body);
        }
        else
        {
            drawBody(ctx);
        }
    };

    private static RecompFunc DrawItem(RecompFunc drawItem) => ctx =>
    {
        uint item = ctx.R0;
        var entity = Memory.Peek<Entity>(item);
        var raised = entity.Position with { Y = entity.Position.Y - entity.Height };
        WorldSprites.Draw(ctx, drawItem, WorldSprites.Track(item, raised.InPixels, raised.Shown));
    };

    private static RecompFunc UpdateStatusSprite(RecompFunc updateStatus) => ctx =>
    {
        uint id = Memory.Peek<uint>(ctx.R0 + StatusSpriteIdOffset);
        WorldSprites.Draw(ctx, updateStatus, MotionBySpriteId.GetValueOrDefault(id));
    };
}
