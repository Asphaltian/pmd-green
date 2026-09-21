using SDL;
using Socotra;
using static SDL.SDL3;

namespace PMDGreen;

internal static partial class Input
{
    private const string GlyphFolder = "assets/ui/glyphs";

    private static string GlyphVendor
    {
        get
        {
            using var gamepads = SDL_GetGamepads();
            var type = gamepads is { Count: > 0 } ? SDL_GetRealGamepadTypeForID(gamepads[0]) : SDL_GamepadType.SDL_GAMEPAD_TYPE_UNKNOWN;
            return type switch
            {
                SDL_GamepadType.SDL_GAMEPAD_TYPE_PS3 or SDL_GamepadType.SDL_GAMEPAD_TYPE_PS4 or SDL_GamepadType.SDL_GAMEPAD_TYPE_PS5 => "playstation",
                SDL_GamepadType.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_PRO or SDL_GamepadType.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_JOYCON_LEFT
                    or SDL_GamepadType.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_JOYCON_RIGHT or SDL_GamepadType.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_JOYCON_PAIR => "switch",
                _ => "xbox",
            };
        }
    }

    private static Texture? LoadGlyphTexture(string vendor, string key, bool outline, InputGlyphSize size)
    {
        key = key.ToLowerInvariant();
        var px = size.ToPixels();

        if (outline && LoadSvg($"{GlyphFolder}/{vendor}/outline/{key}.svg", px) is { } outlined)
        {
            return outlined;
        }

        return LoadSvg($"{GlyphFolder}/{vendor}/{key}.svg", px);
    }

    private static Texture? LoadGlyphTexture(string file, InputGlyphSize size = InputGlyphSize.Small, bool outline = false, bool noController = false)
    {
        if (UsingController && !noController)
        {
            if (LoadGlyphTexture(GlyphVendor, file, outline, size) is { } vendorTexture)
            {
                return vendorTexture;
            }

            if (LoadGlyphTexture("xbox", file, outline, size) is { } xboxTexture)
            {
                return xboxTexture;
            }
        }

        return LoadGlyphTexture("default", file, outline, size);
    }

    private static Texture? LoadSvg(string path, int px) =>
        File.Exists(Path.Combine(AppContext.BaseDirectory, path)) ? Texture.FromFile($"{path}?w={px}&h={px}") : null;

    private static string GetButtonName(string key) => key.ToLowerInvariant() switch
    {
        "/" => "slash",
        "\\" => "backslash",
        "." => "period",
        "," => "comma",
        "-" => "minus",
        "=" => "equals",
        "'" => "apostrophe",
        "`" => "backquote",
        "[" => "leftbracket",
        "]" => "rightbracket",
        ";" => "semicolon",
        "rwin" or "lwin" or "left gui" or "right gui" => "win",
        "left shift" or "right shift" => "shift",
        "left ctrl" or "right ctrl" => "ctrl",
        "left alt" or "right alt" => "alt",
        "up" => "uparrow",
        "down" => "downarrow",
        "left" => "leftarrow",
        "right" => "rightarrow",
        "insert" => "ins",
        "delete" => "del",
        "pageup" => "pgup",
        "pagedown" => "pgdn",
        "keypad enter" => "kp_enter",
        "keypad +" => "kp_plus",
        var name => name,
    };

    private static string GetLocalKeyName(string key)
    {
        var scancode = SDL_GetScancodeFromName(key);
        return scancode == SDL_Scancode.SDL_SCANCODE_UNKNOWN ? key : SDL_GetKeyName(SDL_GetKeyFromScancode(scancode, SDL_Keymod.SDL_KMOD_NONE, true)) ?? key;
    }

    public static Texture? GetMoveGlyph(InputGlyphSize size = InputGlyphSize.Small, bool outline = false) => LoadGlyphTexture("move", size, outline);

    public static class Keyboard
    {
        public static Texture? GetGlyph(string key, InputGlyphSize size = InputGlyphSize.Small, bool outline = false)
        {
            if (string.IsNullOrEmpty(key))
            {
                key = "UNBOUND";
            }

            key = GetLocalKeyName(key);
            key = GetButtonName(key);
            return LoadGlyphTexture(key, size, outline, noController: true);
        }
    }

    public static class Gamepad
    {
        public static Texture? GetGlyph(string button, InputGlyphSize size = InputGlyphSize.Small, bool outline = false)
        {
            var code = (GamepadCode)SDL_GetGamepadButtonFromString(button);
            var key = code.ToString();
            return LoadGlyphTexture(GlyphVendor, key, outline, size)
                ?? LoadGlyphTexture("xbox", key, outline, size)
                ?? LoadGlyphTexture("default", "unknown", outline, size);
        }
    }
}

internal enum InputGlyphSize
{
    Small,
    Medium,
    Large,
}

internal static class InputGlyphSizeExtensions
{
    public static int ToPixels(this InputGlyphSize size) => size switch
    {
        InputGlyphSize.Small => 32,
        InputGlyphSize.Medium => 128,
        InputGlyphSize.Large => 256,
        _ => 32,
    };
}

internal enum GamepadCode
{
    None = -1,
    A = 0,
    B = 1,
    X = 2,
    Y = 3,
    SwitchLeftMenu = 4,
    Guide = 5,
    SwitchRightMenu = 6,
    LeftJoystickButton = 7,
    RightJoystickButton = 8,
    SwitchLeftBumper = 9,
    SwitchRightBumper = 10,
    DpadNorth = 11,
    DpadSouth = 12,
    DpadWest = 13,
    DpadEast = 14,
    Misc1 = 15,
    Paddle1 = 16,
    Paddle2 = 17,
    Paddle3 = 18,
    Paddle4 = 19,
    Touchpad = 20,
    LeftTrigger = 100,
    RightTrigger = 101,
}
