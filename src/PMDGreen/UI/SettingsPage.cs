using Socotra;

namespace PMDGreen.UI;

/// <summary>A page of the game's settings menu.</summary>
public abstract class SettingsPage : Panel
{
    /// <summary>The settings the page changes.</summary>
    [Parameter] public Settings Settings { get; set; } = null!;

    /// <summary>Called after the page changes a setting.</summary>
    [Parameter] public Action? Changed { get; set; }

    /// <summary>The description of the option the player is on, or empty.</summary>
    protected string Description { get; private set; } = "";

    /// <summary>Shows <paramref name="description"/> in the page's description box.</summary>
    protected void Describe(string description) => Description = description;

    /// <summary>Clears the description once the mouse has left the page's options.</summary>
    protected void LeaveOptions(PanelEvent e)
    {
        if (e.This is { HasHovered: false })
        {
            Describe("");
        }
    }

    /// <summary>Makes <paramref name="change"/> to the settings and reports it through <see cref="Changed"/>.</summary>
    protected void Set(Action change)
    {
        change();
        Changed?.Invoke();
    }
}
