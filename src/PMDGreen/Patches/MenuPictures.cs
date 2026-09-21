using System.Text;
using AGBModern;
using RecompiledFuncs;

namespace PMDGreen.Patches;

internal static class MenuPictures
{
    private const uint TitlePaletteFile = 0x0203B038; // sTitlePaletteFile

    private const int LowerBackground = 3;
    private const int UpperBackground = 2;

    private static readonly Dictionary<string, WidePicture> Pictures = [];

    private static WidePicture? _shown;
    private static uint _gameColors;

    public static void Install()
    {
        for (int i = 0; i < 3; i++)
        {
            Pictures[$"titlen{i}p"] = new WidePicture($"titlen{i}.bin");
        }

        var loadTitleScreen = Funcs.Patches.LoadTitleScreen;
        Funcs.Patches.LoadTitleScreen = ctx =>
        {
            loadTitleScreen(ctx);
            Show();
        };

        var closeFile = Funcs.Patches.CloseFile;
        Funcs.Patches.CloseFile = ctx =>
        {
            if (ctx.R0 == Memory.Peek<uint>(TitlePaletteFile))
            {
                _shown = null;
            }

            closeFile(ctx);
        };

        var setColor = Funcs.Patches.SetBGPaletteBufferColorRGB;
        Funcs.Patches.SetBGPaletteBufferColorRGB = ctx =>
        {
            var (index, color, brightness, ramp) = (ctx.R0, ctx.R1, ctx.R2, ctx.R3);
            setColor(ctx);
            if (_shown is { } picture && color - _gameColors < picture.PaletteSize)
            {
                Scheduler.RunUntimed(() =>
                {
                    (ctx.R0, ctx.R1, ctx.R2, ctx.R3) = (index, picture.Palette + (color - _gameColors), brightness, ramp);
                    setColor(ctx);
                });
            }
        };

        GameFrame.Finished += _ =>
        {
            _shown?.SupplyMargins(layer: 0, LowerBackground);
            _shown?.SupplyMargins(layer: 1, UpperBackground);
        };
    }

    private static void Show()
    {
        if (GameFrame.Margins.Width == 0)
        {
            return;
        }

        var paletteFile = Memory.Peek<OpenedFile>(Memory.Peek<uint>(TitlePaletteFile));
        if (Memory.TryGetSpan(Memory.Peek<uint>(paletteFile.File), 8, out var name)
            && Pictures.TryGetValue(Encoding.ASCII.GetString(name), out _shown))
        {
            _gameColors = paletteFile.Data;
            _shown.CopyTiles();
            _shown.Draw(layer: 0, LowerBackground);
            _shown.Draw(layer: 1, UpperBackground);
        }
    }
}
