using AGBModern;
using LibRecomp;
using LibRecomp.Mods;
using PMDGreen;
using PMDGreen.Patches;
using PMDGreen.UI;
using RecompiledFuncs;

const string Title = "Pokémon Mystery Dungeon: Green Rescue Team";
const string GameId = "pmd_green";

Recomp.RegisterConfigPath(GamePaths.UserData);
Recomp.RegisterGame(new GameEntry(
    GameId: GameId,
    ModGameId: GameId,
    InternalName: "POKE DUNGEON",
    ROMHash: "9F4CFC5B5F4859D17169A485462E977C7AAC2B89",
    SaveType: SaveType.Flash128K,
    GPIO: GPIODevices.None,
    EntryPoint: Funcs.Init));
Recomp.RegisterFunctions(Funcs.Table);
Recomp.RegisterRAMFunctions(Funcs.RAMFunctions);
GamePatches.Install();

var settings = Settings.Load();
using var window = new GameWindow(Title, settings);
Input.Configure(settings, window.ShowError);
var version = typeof(Program).Assembly.GetName().Version!;
Recomp.Start(new SemanticVersion(version.Major, version.Minor, version.Build), window.ShowError);

if (args.Length > 0 && !Recomp.IsROMValid(GameId))
{
    SelectROM(args[0]);
}

if (ModManager.Errors.Count > 0)
{
    window.ShowError(string.Join(Environment.NewLine, ModManager.Errors.Select(error => $"{error.Mod} is not a mod: {error.Message}")));
}

using var audio = new AudioOutput(settings.Volume);
MusicVolume.Set(settings.MusicVolume);
APU.SamplesReady += audio.Play;
Video.FrameFinished += frame => window.Renderer.Submit(frame, GameFrame.Motion, GameFrame.Margins);

var launcher = new Launcher
{
    HasROM = Recomp.IsROMValid(GameId),
    Version = $"{version.Major}.{version.Minor}.{version.Build}",
    Settings = settings,
    StartGame = StartGame,
    SettingsChanged = ApplySettings,
    Exit = window.Close,
    ChooseFiles = chosen => window.ShowOpenFileDialog("Mods", "zip", allowMany: true, chosen),
};
launcher.SelectROM = () => window.ShowOpenFileDialog("GBA ROM", "gba", allowMany: false, paths => launcher.HasROM = SelectROM(paths[0]));
window.Interface.Root.AddChild(launcher);

window.Run();
Recomp.Quit();
return 0;

bool StartGame()
{
    if (!Recomp.StartGame(GameId))
    {
        return false;
    }

    window.Interface.MenuRequested = OpenMenu;
    return true;
}

void OpenMenu()
{
    Input.Release();
    var menu = new ConfigMenu { Settings = settings, Changed = ApplySettings, Quit = window.Close };
    menu.Closed = () => menu.Delete();
    window.Interface.Root.AddChild(menu);
}

void ApplySettings()
{
    settings.Save();
    window.ApplySettings();
    Input.Configure(settings, window.ShowError);
    audio.SetVolume(settings.Volume);
    MusicVolume.Set(settings.MusicVolume);
}

bool SelectROM(string path)
{
    string problem = Recomp.SelectROM(path, GameId) switch
    {
        ROMValidationError.Good => "",
        ROMValidationError.FailedToOpen => "The ROM couldn't be opened.",
        ROMValidationError.NotAROM => "That file isn't a GBA ROM.",
        ROMValidationError.IncorrectROM => "That's a ROM of another game.",
        ROMValidationError.IncorrectVersion => "That's another version of the game.",
        _ => "The ROM couldn't be stored.",
    };

    if (problem.Length > 0)
    {
        window.ShowError($"{problem} Select a Pokémon Mystery Dungeon: Red Rescue Team (USA, Australia) ROM.");
    }

    return problem.Length == 0;
}
