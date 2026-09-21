using AGBModern;
using SDL;
using static SDL.SDL3;

namespace PMDGreen;

internal static unsafe partial class Input
{
    private const int StickRange = 32767;

    private static readonly Dictionary<SDL_Scancode, Buttons> Keys = [];
    private static readonly Dictionary<SDL_GamepadButton, Buttons> GamepadButtons = [];
    private static readonly Dictionary<SDL_GamepadButton, MenuInput> MenuButtons = [];
    private static readonly HashSet<SDL_Scancode> HeldKeys = [];
    private static readonly HashSet<SDL_GamepadButton> HeldGamepadButtons = [];

    private static int _stickThreshold;
    private static Action<string?>? _listener;
    private static bool _listensToKeyboard;
    private static bool _listensForMenu;

    private static Buttons _keyboard;
    private static Buttons _gamepad;
    private static Buttons _stick;

    public static bool UsingController { get; private set; }

    public static void Configure(Settings settings, Action<string> showError)
    {
        Keys.Clear();
        GamepadButtons.Clear();
        MenuButtons.Clear();
        var unknown = new List<string>();
        Bind(settings.Keyboard, name => SDL_GetScancodeFromName(name), SDL_Scancode.SDL_SCANCODE_UNKNOWN, "a key", unknown,
            (key, button) => Keys[key] = Keys.GetValueOrDefault(key) | button);
        Bind(settings.Controller, name => SDL_GetGamepadButtonFromString(name), SDL_GamepadButton.SDL_GAMEPAD_BUTTON_INVALID, "a controller button", unknown,
            (pad, button) => GamepadButtons[pad] = GamepadButtons.GetValueOrDefault(pad) | button);
        Bind(settings.ControllerMenu, name => SDL_GetGamepadButtonFromString(name), SDL_GamepadButton.SDL_GAMEPAD_BUTTON_INVALID, "a controller button", unknown,
            (pad, input) => MenuButtons[pad] = input);

        _stickThreshold = Math.Clamp(settings.StickDeadzone, 1, 100) * StickRange / 100;
        SDL_SetHint(SDL_HINT_JOYSTICK_ALLOW_BACKGROUND_EVENTS, settings.BackgroundInput ? "1" : "0");
        if (unknown.Count > 0)
        {
            showError(string.Join(Environment.NewLine, unknown));
        }
    }

    public static void Listen(bool keyboard, bool menu, Action<string?> bound)
    {
        _listener = bound;
        _listensToKeyboard = keyboard;
        _listensForMenu = menu;
    }

    public static void StopListening() => _listener = null;

    public static bool IsMenuToggle(in SDL_Event e) => e.Type switch
    {
        SDL_EventType.SDL_EVENT_KEY_DOWN => e.key.scancode == SDL_Scancode.SDL_SCANCODE_ESCAPE && !e.key.repeat,
        SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_DOWN => MenuInputFor((SDL_GamepadButton)e.gbutton.button) == MenuInput.Toggle,
        _ => false,
    };

    public static MenuInput? MenuInputFor(SDL_GamepadButton button) => MenuButtons.TryGetValue(button, out var input) ? input : null;

    public static void Release()
    {
        HeldKeys.Clear();
        HeldGamepadButtons.Clear();
        _keyboard = Buttons.None;
        _gamepad = Buttons.None;
        _stick = Buttons.None;
        Keypad.Pressed = Buttons.None;
    }

    public static void Track(in SDL_Event e)
    {
        switch (e.Type)
        {
            case SDL_EventType.SDL_EVENT_KEY_DOWN or SDL_EventType.SDL_EVENT_KEY_UP
                or SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN or SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP:
                UsingController = false;
                break;
            case SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_DOWN or SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_UP:
                UsingController = true;
                break;
            case SDL_EventType.SDL_EVENT_GAMEPAD_AXIS_MOTION when Math.Abs((int)e.gaxis.value) > _stickThreshold:
                UsingController = true;
                break;
        }
    }

    public static bool TryCapture(in SDL_Event e)
    {
        if (_listener is not { } listener)
        {
            return false;
        }

        string? name;
        switch (e.Type)
        {
            case SDL_EventType.SDL_EVENT_KEY_DOWN when e.key.scancode == SDL_Scancode.SDL_SCANCODE_ESCAPE:
                name = null;
                break;
            case SDL_EventType.SDL_EVENT_KEY_DOWN when _listensToKeyboard && !e.key.repeat:
                name = SDL_GetScancodeName(e.key.scancode);
                break;
            case SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_DOWN when !_listensToKeyboard && MenuInputFor((SDL_GamepadButton)e.gbutton.button) == MenuInput.Toggle:
                name = null;
                break;
            case SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_DOWN when !_listensToKeyboard && !(_listensForMenu && IsDPad((SDL_GamepadButton)e.gbutton.button)):
                name = SDL_GetGamepadStringForButton((SDL_GamepadButton)e.gbutton.button);
                break;
            case SDL_EventType.SDL_EVENT_KEY_DOWN or SDL_EventType.SDL_EVENT_KEY_UP
                or SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_DOWN or SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_UP:
                return true;
            default:
                return false;
        }

        _listener = null;
        listener(string.IsNullOrEmpty(name) ? null : name);
        return true;
    }

    public static void HandleEvent(in SDL_Event e)
    {
        switch (e.Type)
        {
            case SDL_EventType.SDL_EVENT_KEY_DOWN or SDL_EventType.SDL_EVENT_KEY_UP:
                _keyboard = Held(HeldKeys, Keys, e.key.scancode, e.key.down);
                break;

            case SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_DOWN or SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_UP:
                _gamepad = Held(HeldGamepadButtons, GamepadButtons, (SDL_GamepadButton)e.gbutton.button, e.gbutton.down);
                break;

            case SDL_EventType.SDL_EVENT_GAMEPAD_AXIS_MOTION:
                HandleStick((SDL_GamepadAxis)e.gaxis.axis, e.gaxis.value);
                break;

            case SDL_EventType.SDL_EVENT_GAMEPAD_ADDED:
                SDL_OpenGamepad(e.gdevice.which);
                break;

            case SDL_EventType.SDL_EVENT_GAMEPAD_REMOVED:
                SDL_CloseGamepad(SDL_GetGamepadFromID(e.gdevice.which));
                HeldGamepadButtons.Clear();
                _gamepad = Buttons.None;
                _stick = Buttons.None;
                break;
        }

        Keypad.Pressed = _keyboard | _gamepad | _stick;
    }

    private static bool IsDPad(SDL_GamepadButton button) => button is SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_UP
        or SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_DOWN or SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_LEFT or SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_RIGHT;

    private static void Bind<TAction, TInput>(Dictionary<TAction, string[]> names, Func<string, TInput> find, TInput invalid, string kind, List<string> unknown, Action<TInput, TAction> add)
        where TAction : notnull
        where TInput : struct, Enum
    {
        foreach (var (action, namesForAction) in names)
        {
            foreach (string name in namesForAction)
            {
                var input = find(name);
                if (input.Equals(invalid))
                {
                    unknown.Add($"\"{name}\" isn't {kind}.");
                    continue;
                }

                add(input, action);
            }
        }
    }

    private static Buttons Held<T>(HashSet<T> held, Dictionary<T, Buttons> bindings, T input, bool isDown)
        where T : notnull
    {
        if (isDown)
        {
            held.Add(input);
        }
        else
        {
            held.Remove(input);
        }

        return held.Aggregate(Buttons.None, (buttons, each) => buttons | bindings.GetValueOrDefault(each));
    }

    private static void HandleStick(SDL_GamepadAxis axis, int value)
    {
        var (negative, positive) = axis switch
        {
            SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTX => (Buttons.Left, Buttons.Right),
            SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTY => (Buttons.Up, Buttons.Down),
            _ => (Buttons.None, Buttons.None),
        };

        _stick &= ~(negative | positive);
        if (value <= -_stickThreshold)
        {
            _stick |= negative;
        }
        else if (value >= _stickThreshold)
        {
            _stick |= positive;
        }
    }
}
