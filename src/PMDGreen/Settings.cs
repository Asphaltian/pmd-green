using System.Text.Json;
using System.Text.Json.Serialization;
using AGBModern;
using LibRecomp;

namespace PMDGreen;

/// <summary>How the game's window fills the screen.</summary>
public enum WindowMode
{
    /// <summary>A normal window the player can move and resize.</summary>
    Windowed,

    /// <summary>A window without a border that covers the whole display.</summary>
    Borderless,

    /// <summary>Exclusive full screen.</summary>
    Fullscreen,
}

/// <summary>How the picture fits a window that's wider than the GBA's screen.</summary>
public enum AspectRatio
{
    /// <summary>Keeps the GBA screen's shape, with black bars at the sides.</summary>
    Original,

    /// <summary>Carries the picture on past the screen's edges to fill the window.</summary>
    Expand,
}

/// <summary>A controller button the menus listen for.</summary>
public enum MenuInput
{
    /// <summary>Opens and closes the menu while playing, and backs out of it.</summary>
    Toggle,

    /// <summary>Picks the highlighted option.</summary>
    Accept,
}

/// <summary>The player's settings, kept in <c>settings.json</c> in the game's data folder.</summary>
public sealed class Settings
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        RespectNullableAnnotations = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>How the window fills the screen.</summary>
    public WindowMode WindowMode { get; set; }

    /// <summary>How the picture fits a window that's wider than the GBA's screen.</summary>
    public AspectRatio AspectRatio { get; set; } = AspectRatio.Expand;

    /// <summary>Whether the picture is only ever scaled by whole numbers.</summary>
    public bool IntegerScaling { get; set; }

    /// <summary>Whether movement is smoothed between frames: true or false, or null to decide from the display's refresh rate.</summary>
    public bool? InterpolateMotion { get; set; }

    /// <summary>The overall volume, from 0 to 100.</summary>
    public int Volume { get; set; } = 100;

    /// <summary>The music's volume, from 0 to 100. Sound effects stay at <see cref="Volume"/>.</summary>
    public int MusicVolume { get; set; } = 100;

    /// <summary>How far a stick has to be tilted before it counts, from 1 to 100 percent.</summary>
    public int StickDeadzone { get; set; } = 50;

    /// <summary>Whether controllers still work while the window isn't focused.</summary>
    public bool BackgroundInput { get; set; } = true;

    /// <summary>The keys for each GBA button, by SDL key name, like <c>Return</c>.</summary>
    public Dictionary<Buttons, string[]> Keyboard { get; set; } = new()
    {
        [Buttons.A] = ["X"],
        [Buttons.B] = ["Z"],
        [Buttons.Select] = ["Backspace"],
        [Buttons.Start] = ["Return"],
        [Buttons.Right] = ["Right"],
        [Buttons.Left] = ["Left"],
        [Buttons.Up] = ["Up"],
        [Buttons.Down] = ["Down"],
        [Buttons.R] = ["S"],
        [Buttons.L] = ["A"],
    };

    /// <summary>The controller buttons for each GBA button, by SDL button name, like <c>dpup</c>.</summary>
    public Dictionary<Buttons, string[]> Controller { get; set; } = new()
    {
        [Buttons.A] = ["b"],
        [Buttons.B] = ["a"],
        [Buttons.Select] = ["back"],
        [Buttons.Start] = ["start"],
        [Buttons.Right] = ["dpright"],
        [Buttons.Left] = ["dpleft"],
        [Buttons.Up] = ["dpup"],
        [Buttons.Down] = ["dpdown"],
        [Buttons.R] = ["rightshoulder"],
        [Buttons.L] = ["leftshoulder"],
    };

    /// <summary>The controller buttons the menus listen for, by SDL button name.</summary>
    public Dictionary<MenuInput, string[]> ControllerMenu { get; set; } = new()
    {
        [MenuInput.Toggle] = ["rightstick"],
        [MenuInput.Accept] = ["a"],
    };

    private static string FilePath => Path.Combine(GamePaths.UserData, "settings.json");

    /// <summary>Reads the saved settings. If there are none, or they can't be read, it saves the defaults and gives you those.</summary>
    public static Settings Load()
    {
        if (BackedUpFile.TryRead(FilePath, Parse, out var settings))
        {
            return settings;
        }

        settings = new Settings();
        settings.Save();
        return settings;
    }

    /// <summary>Saves the settings.</summary>
    public void Save()
    {
        BackedUpFile.Write(FilePath, JsonSerializer.SerializeToUtf8Bytes(this, Options));
    }

    private static Settings Parse(byte[] json)
    {
        var settings = JsonSerializer.Deserialize<Settings>(json, Options);
        if (settings is null || HasNullBinding(settings.Keyboard) || HasNullBinding(settings.Controller) || HasNullBinding(settings.ControllerMenu))
        {
            throw new InvalidDataException("The settings have a null binding.");
        }

        return settings;
    }

    private static bool HasNullBinding<T>(Dictionary<T, string[]> bindings)
        where T : notnull
    {
        return bindings.Values.Any(names => names is null || names.Contains(null));
    }
}
