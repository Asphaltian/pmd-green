using System.Runtime.InteropServices;
using AGBModern;
using LibRecomp;
using RecompiledFuncs;

namespace PMDGreen.Patches;

internal static class MarginSprites
{
    private const uint GroundCameraAddress = 0x02039DD8; // gUnknown_2039DD8
    private const uint RisingSprite = 0x0202EDE8;        // gUnknown_202EDE8
    private const int RisingSpriteSize = 0x14;           // sizeof(UnkStruct_202EDE8)
    private const uint MarkerSprite = 0x0202EDC0;        // gUnknown_202EDC0

    private const int Middle = 120;
    private const int DungeonNearest = -32;
    private const int DungeonFarthest = 272;
    private const int ShadowFarthest = 271;
    private const int ShadowReach = 16;
    private const int GroundNearest = -64;
    private const int GroundFarthest = 303;

    private const int TileSize = 24;
    private const int TilesAround = 6;
    private const int RowsAround = 5;
    private const int LowestMarkerY = -32;
    private const int HighestMarkerY = 192;
    private const ushort TerrainStairs = 1 << 9;  // TERRAIN_TYPE_STAIRS
    private const uint TileObjectOffset = 0x14;   // Tile.object

    private const int MarkerPriority = 3;
    private const int MarkerPalette = 10;
    private const int MarkerTile = 0x1FC;
    private const int OBJModeMask = 0x0C00;       // SPRITEOAM_MASK_OBJMODE
    private const int PriorityShift = 10;         // SPRITEOAM_SHIFT_PRIORITY
    private const int PaletteShift = 12;          // SPRITEOAM_SHIFT_PALETTENUM

    private const int AnimationSize = 0x3C;         // sizeof(axdata)
    private const uint AnimationTimerOffset = 0x6E; // UnkGroundSpriteStruct.unk6E
    private const int AnimationTimerSize = 3;       // UnkGroundSpriteStruct.unk6E, unk70
    private const uint LastPoseOffset = 0x22;       // UnkGroundSpriteStruct.axdata.sub1.lastPoseId

    private static readonly Dictionary<uint, (byte[] Game, byte[] Shown)> ShownAnimations = [];
    private static readonly HashSet<uint> ShowingOtherPoses = [];

    private static bool _isDrawingGroundSprite;

    public static void Install()
    {
        Funcs.Patches.UpdateMonsterSprite = DrawMonster(Funcs.Patches.UpdateMonsterSprite);
        Funcs.Patches.sub_80462AC = DrawItem(Funcs.Patches.sub_80462AC);
        Funcs.Patches.sub_803F428 = SeeIntoMargins(Funcs.Patches.sub_803F428);

        var drawStatus = Funcs.Patches.DrawStatusSprite;
        Funcs.Patches.DrawStatusSprite = ctx => DrawStatus(ctx, drawStatus);

        var drawRisingSprite = Funcs.Patches.sub_803EDF0;
        Funcs.Patches.sub_803EDF0 = ctx => DrawRisingSprite(ctx, drawRisingSprite);

        var drawMarkers = Funcs.Patches.sub_807FA9C;
        Funcs.Patches.sub_807FA9C = WorldSprites.InWorld(ctx =>
        {
            drawMarkers(ctx);
            DrawMarkersInMargins(ctx);
        });

        Funcs.Patches.sub_80A7094 = DrawGroundSprite(Funcs.Patches.sub_80A7094);
        Funcs.Patches.sub_80A7524 = DrawGroundSprite(Funcs.Patches.sub_80A7524);
        GroundMap.Selected += _ =>
        {
            ShownAnimations.Clear();
            ShowingOtherPoses.Clear();
        };
    }

    private static int ShiftToMiddle(int screenX, int nearest, int farthest)
    {
        int margin = GameFrame.Margins.Width;
        int shown = screenX - StagedSprites.ShownOffset;
        if (margin == 0 || (screenX >= nearest && screenX <= farthest) || shown < nearest - margin || shown > farthest + margin)
        {
            return 0;
        }

        return screenX - Middle;
    }

    private static void ShiftDungeonCamera(int shift)
    {
        ref var camera = ref DungeonCamera.Changeable;
        camera.Position = camera.Position with { X = (short)(camera.Position.X + shift) };
    }

    private static int EntityScreenX(uint entity)
    {
        return (Memory.Peek<Entity>(entity).Position.X / 256) - DungeonCamera.Current.Position.X;
    }

    private static RecompFunc DrawMonster(RecompFunc draw) => ctx =>
    {
        uint entity = ctx.R0;
        int screenX = EntityScreenX(entity);
        int margin = GameFrame.Margins.Width;
        bool isNearAnEdge = screenX < DungeonNearest + ShadowReach || screenX > ShadowFarthest - ShadowReach;
        bool mayBeShown = screenX >= DungeonNearest - margin - ShadowReach && screenX <= DungeonFarthest + margin + ShadowReach;
        if (margin == 0 || MarginDraw.IsDrawing || !isNearAnEdge || !mayBeShown)
        {
            draw(ctx);
            return;
        }

        var before = MarginDraw.Save();
        draw(ctx);

        int shadowX = screenX + Memory.Peek<Entity>(entity).Shadow.X;
        int shift = ShiftToMiddle(screenX, DungeonNearest, DungeonFarthest);
        if (shift == 0 && (shadowX < DungeonNearest || shadowX > ShadowFarthest) && shadowX >= DungeonNearest - margin && shadowX <= ShadowFarthest + margin)
        {
            shift = screenX - Middle;
        }

        if (shift == 0)
        {
            MarginDraw.Forget(before);
            return;
        }

        MarginDraw.DrawAgain(ctx, before, shift, () =>
        {
            ShiftDungeonCamera(shift);
            ctx.R0 = entity;
            draw(ctx);
        });
    };

    private static RecompFunc DrawItem(RecompFunc draw) => ctx =>
    {
        var (entity, r1, r2, r3) = (ctx.R0, ctx.R1, ctx.R2, ctx.R3);
        int shift = MarginDraw.IsDrawing ? 0 : ShiftToMiddle(EntityScreenX(entity), DungeonNearest, DungeonFarthest);
        if (shift == 0)
        {
            draw(ctx);
            return;
        }

        var before = MarginDraw.Save();
        draw(ctx);
        MarginDraw.DrawAgain(ctx, before, shift, () =>
        {
            ShiftDungeonCamera(shift);
            (ctx.R0, ctx.R1, ctx.R2, ctx.R3) = (entity, r1, r2, r3);
            draw(ctx);
        });
    };

    private static RecompFunc SeeIntoMargins(RecompFunc see) => ctx =>
    {
        uint position = ctx.R0;
        see(ctx);
        if (!MarginDraw.IsDrawing || (byte)ctx.R0 != 0)
        {
            return;
        }

        var camera = DungeonCamera.Current;
        var target = Memory.Peek<DungeonPosition>(position);
        int across = Math.Abs(camera.Tile.X - target.X);
        int extraTiles = ((GameFrame.Margins.Width + TileSize - 1) / TileSize) + 1;
        if (across <= TilesAround || across > TilesAround + extraTiles || Math.Abs(camera.Tile.Y - target.Y) > RowsAround)
        {
            return;
        }

        if (camera.AllTilesRevealed != 0 || camera.Unk1820C != 0 || camera.Target == 0)
        {
            ctx.R0 = 1;
            return;
        }

        (ctx.R0, ctx.R1) = (DungeonCamera.Address, position);
        Funcs.IsPositionActuallyInSight(ctx);
    };

    private static void DrawStatus(RecompContext ctx, RecompFunc draw)
    {
        var (species, status, position, offset) = (ctx.R0, ctx.R1, ctx.R2, ctx.R3);
        uint screen = Memory.Peek<uint>(ctx.R13);
        var first = StagedSprites.SoFar;
        draw(ctx);

        int margin = GameFrame.Margins.Width;
        if (margin == 0 || StagedSprites.Count != first.Sprites)
        {
            return;
        }

        foreach (int shift in (ReadOnlySpan<int>)[margin, -margin])
        {
            ref short screenX = ref Memory.Poke<short>(screen);
            screenX += (short)shift;
            Scheduler.RunUntimed(() =>
            {
                (ctx.R0, ctx.R1, ctx.R2, ctx.R3) = (species, status, position, offset);
                draw(ctx);
            });
            screenX -= (short)shift;

            if (StagedSprites.Count != first.Sprites)
            {
                StagedSprites.MoveX(first, shift);
                StagedSprites.SetAside(first.Sprites);
                return;
            }
        }
    }

    private static void DrawRisingSprite(RecompContext ctx, RecompFunc draw)
    {
        var live = MemoryMarshal.CreateSpan(ref Memory.Poke<byte>(RisingSprite), RisingSpriteSize);
        Span<byte> before = stackalloc byte[RisingSpriteSize];
        live.CopyTo(before);

        var first = StagedSprites.SoFar;
        draw(ctx);

        int margin = GameFrame.Margins.Width;
        if (margin == 0 || StagedSprites.Count != first.Sprites)
        {
            return;
        }

        Span<byte> after = stackalloc byte[RisingSpriteSize];
        live.CopyTo(after);
        foreach (int shift in (ReadOnlySpan<int>)[margin, -margin])
        {
            before.CopyTo(live);
            ShiftDungeonCamera(shift);
            Scheduler.RunUntimed(() => draw(ctx));
            ShiftDungeonCamera(-shift);

            if (StagedSprites.Count != first.Sprites)
            {
                StagedSprites.MoveX(first, shift);
                StagedSprites.SetAside(first.Sprites);
                break;
            }
        }

        after.CopyTo(live);
    }

    private static void DrawMarkersInMargins(RecompContext ctx)
    {
        int margin = GameFrame.Margins.Width;
        if (margin == 0)
        {
            return;
        }

        var sprite = Memory.Peek<SpriteOAM>(MarkerSprite);
        int first = StagedSprites.Count;
        var camera = DungeonCamera.Current;
        int extraTiles = ((margin + TileSize - 1) / TileSize) + 1;
        for (int y = camera.Tile.Y - RowsAround; y < camera.Tile.Y + RowsAround; y++)
        {
            for (int x = camera.Tile.X - TilesAround - extraTiles; x < camera.Tile.X + TilesAround + extraTiles; x++)
            {
                int screenX = (x * TileSize) - camera.Position.X;
                int screenY = (y * TileSize) - camera.Position.Y;
                bool isOnScreenY = screenY >= LowestMarkerY && screenY <= HighestMarkerY;
                bool isDrawnByGame = x >= camera.Tile.X - TilesAround && x < camera.Tile.X + TilesAround
                    && screenX >= DungeonNearest && screenX <= DungeonFarthest;
                bool isInMargins = screenX >= DungeonNearest - margin && screenX <= DungeonFarthest + margin;
                if (isOnScreenY && !isDrawnByGame && isInMargins && HasMarker(ctx, x, y, camera.ShowsInvisibles != 0))
                {
                    DrawMarker(ctx, screenX, screenY);
                }
            }
        }

        Memory.Poke<SpriteOAM>(MarkerSprite) = sprite;
        StagedSprites.SetAside(first);
    }

    private static bool HasMarker(RecompContext ctx, int x, int y, bool showsInvisibles)
    {
        Scheduler.RunUntimed(() =>
        {
            (ctx.R0, ctx.R1) = ((uint)x, (uint)y);
            Funcs.GetTile(ctx);
        });

        uint tile = ctx.R0;
        uint entity = Memory.Peek<uint>(tile + TileObjectOffset);
        bool isTrap = entity != 0 && Memory.Peek<Entity>(entity) is { Type: Entity.Trap } trap
            && (trap.IsVisible != 0 || showsInvisibles);
        return isTrap || (Memory.Peek<ushort>(tile) & TerrainStairs) != 0;
    }

    private static void DrawMarker(RecompContext ctx, int x, int y)
    {
        ref var sprite = ref Memory.Poke<SpriteOAM>(MarkerSprite);
        sprite.Attribute0 = (ushort)(sprite.Attribute0 & ~OBJModeMask);
        sprite.SetWorkingY(y);
        sprite.SetX(x);
        sprite.Attribute2 = (ushort)((MarkerPriority << PriorityShift) | (MarkerPalette << PaletteShift) | MarkerTile);

        Scheduler.RunUntimed(() =>
        {
            (ctx.R0, ctx.R1, ctx.R2, ctx.R3) = (MarkerSprite, 0, 0, 0);
            Funcs.AddSprite(ctx);
        });
    }

    private static RecompFunc DrawGroundSprite(RecompFunc draw) => ctx =>
    {
        if (_isDrawingGroundSprite || MarginDraw.IsDrawing)
        {
            draw(ctx);
            return;
        }

        var (sprite, r1, r2, r3) = (ctx.R0, ctx.R1, ctx.R2, ctx.R3);
        int screenX = (Memory.Peek<int>(r2) / 256) - Memory.Peek<PixelPosition>(GroundCameraAddress).X;
        int shift = ShiftToMiddle(screenX, GroundNearest, GroundFarthest);
        bool isDrawnByGame = screenX >= GroundNearest && screenX <= GroundFarthest;
        if (shift == 0 && !(isDrawnByGame && ShowingOtherPoses.Contains(sprite)))
        {
            _isDrawingGroundSprite = true;
            draw(ctx);
            _isDrawingGroundSprite = false;
            return;
        }

        var before = MarginDraw.Save();
        _isDrawingGroundSprite = true;
        draw(ctx);
        _isDrawingGroundSprite = false;

        if (shift == 0)
        {
            bool uploaded = MarginDraw.DrawAgain(ctx, before, 0, () =>
            {
                Memory.Poke<short>(sprite + LastPoseOffset) = short.MaxValue;
                (ctx.R0, ctx.R1, ctx.R2, ctx.R3) = (sprite, r1, r2, r3);
                draw(ctx);
            });

            if (uploaded)
            {
                ShowingOtherPoses.Remove(sprite);
            }

            return;
        }

        bool uploadedOtherPose = MarginDraw.DrawAgain(ctx, before, shift, () =>
        {
            ref var camera = ref Memory.Poke<PixelPosition>(GroundCameraAddress);
            camera = camera with { X = camera.X + shift };

            byte[] game = ReadAnimation(sprite);
            if (ShownAnimations.TryGetValue(sprite, out var kept) && kept.Game.AsSpan().SequenceEqual(game))
            {
                WriteAnimation(sprite, kept.Shown);
            }

            (ctx.R0, ctx.R1, ctx.R2, ctx.R3) = (sprite, r1, r2, r3);
            draw(ctx);
            ShownAnimations[sprite] = (game, ReadAnimation(sprite));
        });

        if (uploadedOtherPose)
        {
            ShowingOtherPoses.Add(sprite);
        }
    };

    private static byte[] ReadAnimation(uint sprite)
    {
        var animation = new byte[AnimationSize + AnimationTimerSize];
        MemoryMarshal.CreateReadOnlySpan(ref Memory.Poke<byte>(sprite), AnimationSize).CopyTo(animation);
        MemoryMarshal.CreateReadOnlySpan(ref Memory.Poke<byte>(sprite + AnimationTimerOffset), AnimationTimerSize).CopyTo(animation.AsSpan(AnimationSize));
        return animation;
    }

    private static void WriteAnimation(uint sprite, byte[] animation)
    {
        animation.AsSpan(0, AnimationSize).CopyTo(MemoryMarshal.CreateSpan(ref Memory.Poke<byte>(sprite), AnimationSize));
        animation.AsSpan(AnimationSize).CopyTo(MemoryMarshal.CreateSpan(ref Memory.Poke<byte>(sprite + AnimationTimerOffset), AnimationTimerSize));
    }
}
