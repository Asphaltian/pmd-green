using System.Numerics;
using System.Runtime.InteropServices;
using AGBModern;
using LibRecomp;
using PMDGreen.Patches;
using RecompiledFuncs;

namespace PMDGreen;

/// <summary>The map the game shows in towns, cutscenes and everywhere else outside of dungeons.</summary>
/// <example>
/// <code>
/// GroundMap.Selected += id =>
/// {
///     // MAP_SQUARE
///     if (id == 0)
///     {
///         AddVisitor();
///     }
/// };
/// </code>
/// </example>
public static class GroundMap
{
    internal const uint MapRenderSize = 0x50; // sizeof(MapRender)

    private const uint GroundBgSize = 0x55C;          // sizeof(GroundBg)
    private const uint MapFileIdOffset = 0x444;       // GroundBg.mapFileId
    private const uint LayerCountOffset = 0x474;      // GroundBg.unk474
    private const uint LayerCamerasOffset = 0x478;    // GroundBg.cameraPixelPosition
    private const uint LayerCameraSize = 8;           // sizeof(PixelPos)
    private const uint MapRendersOffset = 0x488;      // GroundBg.mapRender
    private const uint FirstBGOffset = 0x536;         // GroundBg.unk52C.unkA
    private const uint MapPointer = 0x03001B70;       // gGroundMapDungeon_3001B70
    private const uint MapActionPointer = 0x03001B6C; // gGroundMapAction
    private const uint MapIdOffset = 0xE4;            // GroundMapAction.groundMapId
    private const uint DriftFractionOffset = 0xF0;    // GroundMapAction.unkF0
    private const uint WeatherPointer = 0x03001B74;   // gUnknown_3001B74
    private const uint WeatherBGPointer = 0x03001B78; // gUnknown_3001B78
    private const uint WeatherMaps = 0x0811E5F4;      // gUnknown_811E5F4
    private const uint WeatherMapSize = 4;            // sizeof(gUnknown_811E5F4[0])

    private const int WeatherDriftingDiagonally = 9;
    private const int WeatherDriftingSideways = 14;
    private const int WeatherDriftingSidewaysFaster = 15;

    private const int Square = 0;                      // MAP_SQUARE
    private const int PelipperPostOffice = 4;          // MAP_PELIPPER_POST_OFFICE
    private const int FriendAreaFinalIsland = 161;     // MAP_FRIEND_AREA_FINAL_ISLAND
    private const int PersonalityTestCyan = 162;       // MAP_PERSONALITY_TEST_CYAN
    private const int PersonalityTestPurple = 163;     // MAP_PERSONALITY_TEST_PURPLE
    private const int FugitivesSnowRoad = 168;         // MAP_FUGITIVES_SNOW_ROAD
    private const int FugitivesBlizzardRoad = 170;     // MAP_FUGITIVES_BLIZZARD_ROAD
    private const int Nightmare = 174;                 // MAP_NIGHTMARE
    private const int NightSky1 = 175;                 // MAP_NIGHT_SKY_1
    private const int NightSky2 = 176;                 // MAP_NIGHT_SKY_2
    private const int TinyWoodsEntry = 178;            // MAP_TINY_WOODS_ENTRY
    private const int SilentChasmEntry = 186;          // MAP_SILENT_CHASM_ENTRY
    private const int D25 = 222;                       // MAP_D25
    private const int PersonalityTestMulticolor = 223; // MAP_PERSONALITY_TEST_MULTICOLOR
    private const int TitleScreen = 224;               // MAP_TITLE_SCREEN

    private static int _selecting;
    private static bool _isDrawing;
    private static int _layersDrawn;
    private static uint _mapRender;
    private static PixelPosition _camera;

    internal readonly record struct Layer(int Index, uint MapRender, int Background, PixelPosition Camera);

    [StructLayout(LayoutKind.Explicit)]
    internal struct MapRender
    {
        [FieldOffset(0x02)] // MapRender.unk2
        public short FirstBG;

        [FieldOffset(0x04)] // MapRender.numBgs
        public short BGCount;

        [FieldOffset(0x06)] // MapRender.wrapAround
        public byte Repeats;

        [FieldOffset(0x10)] // MapRender.mapSizePixels
        public PixelPosition MapSize;

        [FieldOffset(0x18)] // MapRender.tilemapRenderFunc
        public uint Draw;

        [FieldOffset(0x30)] // MapRender.cameraPos
        public PixelPosition Camera;

        [FieldOffset(0x48)] // MapRender.bgRegOffsets
        public PixelPosition Scroll;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct Weather
    {
        [FieldOffset(0x00)] // unkStruct_3001B74.unk0
        public short Kind;

        [FieldOffset(0x04)] // unkStruct_3001B74.unk4
        public int DriftX;

        [FieldOffset(0x08)] // unkStruct_3001B74.unk8
        public int DriftY;
    }

    /// <summary>
    /// Runs when the game has selected a new map and loaded it, with the map's id. You get -1 if the
    /// game cleared the map instead.
    /// </summary>
    public static event Action<int>? Selected;

    /// <summary>The map the game shows right now, as its number in the decompilation's <c>GroundMapID</c>, or -1 if there's none.</summary>
    public static int Id
    {
        get
        {
            uint action = Memory.Peek<uint>(MapActionPointer);
            return action == 0 ? -1 : Memory.Peek<short>(action + MapIdOffset);
        }
    }

    internal static event Action<Layer>? LayerDrawn;

    internal static List<Layer> DrawnLayers { get; } = [];

    internal static bool IsAPlace => Id is (>= Square and <= FriendAreaFinalIsland) or (>= TinyWoodsEntry and <= D25);

    internal static bool IsAPicture => !IsAPlace && Id != TitleScreen;

    internal static bool GoesOnSideways => Id == TitleScreen;

    internal static bool IsInGroundBg(uint mapRender, uint groundBg)
    {
        return groundBg != 0 && mapRender - groundBg < GroundBgSize;
    }

    internal static Vector2 DriftFraction(Layer layer)
    {
        if (IsInGroundBg(layer.MapRender, Memory.Peek<uint>(WeatherBGPointer)))
        {
            return WeatherDriftFraction();
        }

        uint action = Memory.Peek<uint>(MapActionPointer);
        if (action == 0)
        {
            return Vector2.Zero;
        }

        var counted = Memory.Peek<PixelPosition>(action + DriftFractionOffset).Whole;
        var (x, y) = (counted.X, counted.Y);

        return (Id, layer.Index) switch
        {
            (PersonalityTestCyan or PersonalityTestPurple or PersonalityTestMulticolor, 0) => new(0, y / 4),
            (PersonalityTestCyan or PersonalityTestPurple or PersonalityTestMulticolor, 1) => new(0, -y / 4),
            (FugitivesSnowRoad or FugitivesBlizzardRoad or NightSky2, 0) => new(x / 8, y / 8),
            (Nightmare, _) => new(x / 8, 0),
            (NightSky1, 1) => new(x / 8, y / 8),
            (SilentChasmEntry, 0) => new(x / 4, 0),
            (PelipperPostOffice or TitleScreen, 1) => new(x / 8, 0),
            _ => Vector2.Zero,
        };
    }

    internal static void Install()
    {
        Funcs.Patches.GroundMap_Select = Select(Funcs.Patches.GroundMap_Select);
        Funcs.Patches.GroundMap_SelectDungeon = Select(Funcs.Patches.GroundMap_SelectDungeon);
        Funcs.Patches.sub_80A4764 = Draw(Funcs.Patches.sub_80A4764);
        Funcs.Patches.GroundBg_FreeAll = Free(Funcs.Patches.GroundBg_FreeAll);
        Funcs.Patches.UpdateMapCameraPosition = UpdateMapCameraPosition(Funcs.Patches.UpdateMapCameraPosition);
        Funcs.Patches.SetBG2RegOffsets = SetRegOffsets(2, Funcs.Patches.SetBG2RegOffsets);
        Funcs.Patches.SetBG3RegOffsets = SetRegOffsets(3, Funcs.Patches.SetBG3RegOffsets);
    }

    private static RecompFunc Select(RecompFunc select) => ctx =>
    {
        _selecting++;
        try
        {
            select(ctx);
        }
        finally
        {
            _selecting--;
        }

        if (_selecting == 0)
        {
            Selected?.Invoke(Id);
        }
    };

    private static RecompFunc Draw(RecompFunc draw) => ctx =>
    {
        uint groundBg = ctx.R0;
        StartDrawing(groundBg);
        draw(ctx);
        _isDrawing = false;

        int offset = GroundCamera.ShownOffset;
        if (offset != 0 && groundBg != 0 && Memory.Peek<short>(groundBg + MapFileIdOffset) != -1)
        {
            DrawShown(ctx, groundBg, offset);
        }
    };

    private static void StartDrawing(uint groundBg)
    {
        ForgetLayers(groundBg);
        _layersDrawn = 0;
        _isDrawing = true;
    }

    private static void DrawShown(RecompContext ctx, uint groundBg, int offset)
    {
        StartDrawing(groundBg);
        Scheduler.RunUntimed(() =>
        {
            int count = Memory.Peek<int>(groundBg + LayerCountOffset);
            for (int i = 0; i < count; i++)
            {
                uint mapRender = groundBg + MapRendersOffset + ((uint)i * MapRenderSize);
                var camera = Memory.Peek<PixelPosition>(groundBg + LayerCamerasOffset + ((uint)i * LayerCameraSize));
                if (MovesWithTheCamera(groundBg, i))
                {
                    ctx.R13 -= LayerCameraSize;
                    Memory.Poke<PixelPosition>(ctx.R13) = camera with { X = camera.X + offset };
                    (ctx.R0, ctx.R1, ctx.R2) = (groundBg, (uint)i, ctx.R13);
                    Funcs.sub_80A4580(ctx);
                    camera = Memory.Peek<PixelPosition>(ctx.R13);
                    ctx.R13 += LayerCameraSize;
                }

                ctx.R13 -= LayerCameraSize;
                Memory.Poke<PixelPosition>(ctx.R13) = camera;
                (ctx.R0, ctx.R1) = (mapRender, ctx.R13);
                Funcs.UpdateMapCameraPosition(ctx);
                ctx.R13 += LayerCameraSize;

                var map = Memory.Peek<MapRender>(mapRender);
                ctx.R0 = mapRender;
                Recomp.LookupFunc(map.Draw)(ctx);

                int firstBG = map.FirstBG + Memory.Peek<short>(groundBg + FirstBGOffset);
                for (int bg = firstBG; bg < firstBG + map.BGCount; bg++)
                {
                    (ctx.R0, ctx.R1) = ((uint)map.Scroll.X, (uint)map.Scroll.Y);
                    switch (bg)
                    {
                        case 0:
                            Funcs.SetBG2RegOffsets(ctx);
                            break;
                        case 1:
                            Funcs.SetBG3RegOffsets(ctx);
                            break;
                    }
                }
            }
        });
        _isDrawing = false;
    }

    private static bool MovesWithTheCamera(uint groundBg, int layer)
    {
        if (groundBg == Memory.Peek<uint>(MapPointer))
        {
            return (Id, layer) is not ((FriendAreaFinalIsland or TitleScreen), 1);
        }

        if (groundBg != Memory.Peek<uint>(WeatherBGPointer))
        {
            return false;
        }

        int kind = Memory.Peek<Weather>(Memory.Peek<uint>(WeatherPointer)).Kind;
        return kind switch
        {
            < 0 => false,
            >= WeatherDriftingDiagonally and <= WeatherDriftingSidewaysFaster => true,
            _ => Memory.Peek<short>(WeatherMaps + ((uint)kind * WeatherMapSize)) == 0,
        };
    }

    private static RecompFunc Free(RecompFunc free) => ctx =>
    {
        ForgetLayers(groundBg: ctx.R0);
        free(ctx);
    };

    private static void ForgetLayers(uint groundBg)
    {
        DrawnLayers.RemoveAll(layer => IsInGroundBg(layer.MapRender, groundBg));
    }

    private static RecompFunc UpdateMapCameraPosition(RecompFunc updateCamera) => ctx =>
    {
        _mapRender = ctx.R0;
        _camera = Memory.Peek<PixelPosition>(ctx.R1);
        updateCamera(ctx);

        if (_isDrawing)
        {
            _layersDrawn++;
        }
    };

    private static RecompFunc SetRegOffsets(int bg, RecompFunc setOffsets) => ctx =>
    {
        setOffsets(ctx);

        if (_isDrawing)
        {
            var layer = new Layer(_layersDrawn - 1, _mapRender, bg, _camera);
            DrawnLayers.Add(layer);
            LayerDrawn?.Invoke(layer);
        }
    };

    private static Vector2 WeatherDriftFraction()
    {
        var weather = Memory.Peek<Weather>(Memory.Peek<uint>(WeatherPointer));
        return weather.Kind switch
        {
            WeatherDriftingDiagonally => new(-weather.DriftX / 4f, weather.DriftY / 4f),
            WeatherDriftingSideways or WeatherDriftingSidewaysFaster => new(-weather.DriftX / 4f, 0),
            _ => Vector2.Zero,
        };
    }
}
