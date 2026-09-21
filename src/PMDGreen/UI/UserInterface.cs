using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using GBARenderer;
using SDL;
using Silk.NET.Vulkan;
using Socotra;
using Socotra.Vulkan;
using static SDL.SDL3;

namespace PMDGreen.UI;

internal sealed unsafe class UserInterface(SDL_Window* window) : IRenderHooks
{
    private const float MouseWakeDistance = 100;
    private const float StickPress = 0.5f;
    private const float StickRelease = 0.15f;
    private const float StickRange = 32768;

    private static readonly TimeSpan RepeatDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan RepeatRate = TimeSpan.FromMilliseconds(50);

    private readonly DrawList _drawList = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private VulkanRenderer? _renderer;
    private TimeSpan _lastDraw;
    private bool _usingMouse;
    private Vector2 _lastMouse;
    private string? _stickX;
    private string? _stickY;
    private string? _repeating;
    private TimeSpan _nextRepeat;
    private string? _cursorName;
    private SDL_Cursor* _cursor;
    private bool _textInput;
    private bool _relativeMouse;

    public RootPanel Root { get; } = new();

    public Action? MenuRequested { get; set; }

    public bool Supports(Vk api, PhysicalDevice physicalDevice) => VulkanRenderer.IsSupported(api, physicalDevice);

    public void Configure(DeviceConfiguration configuration)
    {
        configuration.Extensions.AddRange(VulkanRenderer.DeviceExtensions);
        VulkanRenderer.EnableFeatures(ref configuration.Features, ref configuration.Vulkan12Features, ref configuration.DynamicRenderingFeatures);
        configuration.TargetUsage |= ImageUsageFlags.TransferSrcBit;
    }

    public void Init(RenderDevice device)
    {
        Fonts.LoadFolder(Path.Combine(AppContext.BaseDirectory, "assets", "fonts"));
        Clipboard.GetTextHandler = () => SDL_HasClipboardText() ? SDL_GetClipboardText() : null;
        Clipboard.SetTextHandler = text => SDL_SetClipboardText(text);
        _renderer = new VulkanRenderer(device.Api, device.PhysicalDevice, device.Device, framesInFlight: 1);
    }

    public void Draw(CommandBuffer commandBuffer, GBARenderer.RenderTarget target)
    {
        var now = _clock.Elapsed;
        if (_repeating is not null && now >= _nextRepeat)
        {
            Root.AddButtonEvent(new ButtonEvent(_repeating, true));
            _nextRepeat += RepeatRate;
        }

        Root.Update(new Rect(0, 0, target.Size.Width, target.Size.Height), (float)(now - _lastDraw).TotalSeconds);
        _lastDraw = now;
        UpdateCursor();
        UpdateTextInput();
        Root.Paint(_drawList);
        _renderer!.Record(commandBuffer, _drawList, new Socotra.Vulkan.RenderTarget(target.Image, target.View, target.Format, target.Size), target.Layout, ImageLayout.PresentSrcKhr);
    }

    public void Deinit()
    {
        _renderer?.Dispose();
        _renderer = null;
        if (_cursor is not null)
        {
            SDL_DestroyCursor(_cursor);
            _cursor = null;
        }
    }

    public bool HandleEvent(in SDL_Event e)
    {
        if (!Root.HasChildren)
        {
            if (MenuRequested is { } open && Input.IsMenuToggle(e))
            {
                open();
                return true;
            }

            return false;
        }

        switch (e.Type)
        {
            case SDL_EventType.SDL_EVENT_MOUSE_MOTION when _relativeMouse:
                Root.AddMouseDelta(new Vector2(e.motion.xrel, e.motion.yrel) * SDL_GetWindowPixelDensity(window));
                return true;

            case SDL_EventType.SDL_EVENT_MOUSE_MOTION:
                UseMouse(new Vector2(e.motion.x, e.motion.y), pressed: false);
                return true;

            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN or SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP:
                UseMouse(new Vector2(e.button.x, e.button.y), pressed: true);
                if (MouseButtonOf(e.button.button) is { } button)
                {
                    Root.SetMouseButton(button, e.button.down, ModifiersOf(SDL_GetModState()));
                }

                return true;

            case SDL_EventType.SDL_EVENT_MOUSE_WHEEL:
                Root.AddMouseWheel(new Vector2(e.wheel.x, -e.wheel.y), ModifiersOf(SDL_GetModState()));
                return true;

            case SDL_EventType.SDL_EVENT_WINDOW_MOUSE_LEAVE:
                Root.SetMousePosition(null);
                return false;

            case SDL_EventType.SDL_EVENT_KEY_DOWN or SDL_EventType.SDL_EVENT_KEY_UP when KeyName(e.key.scancode) is { } key:
                if (!_textInput)
                {
                    UseButtons();
                }

                Root.AddButtonEvent(new ButtonEvent(key, e.key.down, ModifiersOf(e.key.mod)));
                return true;

            case SDL_EventType.SDL_EVENT_TEXT_INPUT:
                Root.TypeText(Marshal.PtrToStringUTF8((nint)e.text.text) ?? "");
                return true;

            case SDL_EventType.SDL_EVENT_TEXT_EDITING:
                Root.SetImeComposition(Marshal.PtrToStringUTF8((nint)e.edit.text));
                return true;

            case SDL_EventType.SDL_EVENT_DROP_FILE:
                Root.DropFile(Marshal.PtrToStringUTF8((nint)e.drop.data) ?? "");
                return true;

            case SDL_EventType.SDL_EVENT_DROP_TEXT:
                Root.DropText(Marshal.PtrToStringUTF8((nint)e.drop.data) ?? "");
                return true;

            case SDL_EventType.SDL_EVENT_DROP_COMPLETE:
                Root.DropComplete(new Vector2(e.drop.x, e.drop.y) * SDL_GetWindowPixelDensity(window));
                return true;

            case SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_DOWN or SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_UP
                when GamepadButtonName((SDL_GamepadButton)e.gbutton.button) is { } name:
                UseButtons();
                PressGamepad(name, e.gbutton.down);
                return true;

            case SDL_EventType.SDL_EVENT_GAMEPAD_AXIS_MOTION when (SDL_GamepadAxis)e.gaxis.axis is SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTX:
                MoveStick(ref _stickX, e.gaxis.value / StickRange, "left", "right");
                return true;

            case SDL_EventType.SDL_EVENT_GAMEPAD_AXIS_MOTION when (SDL_GamepadAxis)e.gaxis.axis is SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTY:
                MoveStick(ref _stickY, e.gaxis.value / StickRange, "up", "down");
                return true;

            default:
                return false;
        }
    }

    private static MouseButtons? MouseButtonOf(byte button) => button switch
    {
        1 => MouseButtons.Left,
        2 => MouseButtons.Middle,
        3 => MouseButtons.Right,
        4 => MouseButtons.Back,
        5 => MouseButtons.Forward,
        _ => null,
    };

    private static string? KeyName(SDL_Scancode scancode) => scancode switch
    {
        >= SDL_Scancode.SDL_SCANCODE_A and <= SDL_Scancode.SDL_SCANCODE_Z => ((char)('a' + (scancode - SDL_Scancode.SDL_SCANCODE_A))).ToString(),
        >= SDL_Scancode.SDL_SCANCODE_1 and <= SDL_Scancode.SDL_SCANCODE_9 => ((char)('1' + (scancode - SDL_Scancode.SDL_SCANCODE_1))).ToString(),
        SDL_Scancode.SDL_SCANCODE_0 => "0",
        SDL_Scancode.SDL_SCANCODE_UP => "up",
        SDL_Scancode.SDL_SCANCODE_DOWN => "down",
        SDL_Scancode.SDL_SCANCODE_LEFT => "left",
        SDL_Scancode.SDL_SCANCODE_RIGHT => "right",
        SDL_Scancode.SDL_SCANCODE_RETURN or SDL_Scancode.SDL_SCANCODE_KP_ENTER => "enter",
        SDL_Scancode.SDL_SCANCODE_SPACE => "space",
        SDL_Scancode.SDL_SCANCODE_ESCAPE => "escape",
        SDL_Scancode.SDL_SCANCODE_TAB => "tab",
        SDL_Scancode.SDL_SCANCODE_BACKSPACE => "backspace",
        SDL_Scancode.SDL_SCANCODE_DELETE => "delete",
        SDL_Scancode.SDL_SCANCODE_INSERT => "insert",
        SDL_Scancode.SDL_SCANCODE_HOME => "home",
        SDL_Scancode.SDL_SCANCODE_END => "end",
        SDL_Scancode.SDL_SCANCODE_PAGEUP => "pageup",
        SDL_Scancode.SDL_SCANCODE_PAGEDOWN => "pagedown",
        _ => null,
    };

    private void UpdateCursor()
    {
        var name = _usingMouse ? Root.Cursor ?? "auto" : "none";
        if (name == _cursorName)
        {
            return;
        }

        _cursorName = name;
        if (name == "none")
        {
            SDL_HideCursor();
            return;
        }

        var shape = name == "auto" || !Enum.TryParse<SDL_SystemCursor>("SDL_SYSTEM_CURSOR_" + name.Replace('-', '_'), ignoreCase: true, out var parsed)
            ? SDL_SystemCursor.SDL_SYSTEM_CURSOR_DEFAULT
            : parsed;
        var cursor = SDL_CreateSystemCursor(shape);
        SDL_SetCursor(cursor);
        SDL_ShowCursor();
        if (_cursor is not null)
        {
            SDL_DestroyCursor(_cursor);
        }

        _cursor = cursor;
    }

    private void UpdateTextInput()
    {
        var typing = Root.Focused is { AcceptsImeInput: true };
        if (typing != _textInput)
        {
            _textInput = typing;
            if (typing)
            {
                SDL_StartTextInput(window);
            }
            else
            {
                SDL_StopTextInput(window);
            }
        }

        if (typing)
        {
            var density = SDL_GetWindowPixelDensity(window);
            var caret = Root.Focused!.ImeCaretRect;
            var area = new SDL_Rect { x = (int)(caret.Left / density), y = (int)(caret.Top / density), w = (int)(caret.Width / density), h = (int)(caret.Height / density) };
            SDL_SetTextInputArea(window, &area, 0);
        }

        var relative = Root.MouseCapture is not null;
        if (relative != _relativeMouse)
        {
            _relativeMouse = relative;
            SDL_SetWindowRelativeMouseMode(window, relative);
        }
    }

    private static string? GamepadButtonName(SDL_GamepadButton button) => Input.MenuInputFor(button) switch
    {
        MenuInput.Accept => "enter",
        MenuInput.Toggle => "escape",
        _ => button switch
        {
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_UP => "up",
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_DOWN => "down",
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_LEFT => "left",
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_RIGHT => "right",
            _ => null,
        },
    };

    private static KeyboardModifiers ModifiersOf(SDL_Keymod mod) =>
        ((mod & SDL_Keymod.SDL_KMOD_SHIFT) != 0 ? KeyboardModifiers.Shift : KeyboardModifiers.None)
        | ((mod & SDL_Keymod.SDL_KMOD_CTRL) != 0 ? KeyboardModifiers.Ctrl : KeyboardModifiers.None)
        | ((mod & SDL_Keymod.SDL_KMOD_ALT) != 0 ? KeyboardModifiers.Alt : KeyboardModifiers.None);

    private void UseMouse(Vector2 position, bool pressed)
    {
        if (!_usingMouse)
        {
            if (!pressed && Vector2.Distance(position, _lastMouse) < MouseWakeDistance)
            {
                return;
            }

            _usingMouse = true;
            Root.ClearFocus();
        }

        _lastMouse = position;
        Root.SetMousePosition(position * SDL_GetWindowPixelDensity(window));
    }

    private void UseButtons()
    {
        if (!_usingMouse)
        {
            return;
        }

        _usingMouse = false;
        Root.Hovered?.Focus();
        Root.SetMousePosition(null);
    }

    private void MoveStick(ref string? held, float value, string negative, string positive)
    {
        if (held is null && MathF.Abs(value) > StickPress)
        {
            held = value < 0 ? negative : positive;
            UseButtons();
            PressGamepad(held, true);
        }
        else if (held is not null && MathF.Abs(value) < StickRelease)
        {
            PressGamepad(held, false);
            held = null;
        }
    }

    private void PressGamepad(string button, bool down)
    {
        Root.AddButtonEvent(new ButtonEvent(button, down));
        if (down && button is "up" or "down" or "left" or "right")
        {
            _repeating = button;
            _nextRepeat = _clock.Elapsed + RepeatDelay;
        }
        else if (_repeating == button)
        {
            _repeating = null;
        }
    }
}
