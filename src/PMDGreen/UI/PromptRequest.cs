namespace PMDGreen.UI;

/// <summary>How a button in one of the game's prompts looks.</summary>
public enum PromptButton
{
    /// <summary>The theme's main color.</summary>
    Primary,

    /// <summary>Red, for something that can't be undone.</summary>
    Danger,

    /// <summary>No color.</summary>
    Plain,
}

/// <summary>A question the game's menus ask the player, with up to two buttons.</summary>
/// <param name="Title">The heading.</param>
/// <param name="Message">The text under the heading.</param>
public sealed record PromptRequest(string Title, string Message)
{
    /// <summary>The confirm button's text, or null for no confirm button.</summary>
    public string? ConfirmText { get; init; }

    /// <summary>The cancel button's text, or null for no cancel button.</summary>
    public string? CancelText { get; init; }

    /// <summary>How the confirm button looks.</summary>
    public PromptButton ConfirmStyle { get; init; } = PromptButton.Primary;

    /// <summary>How the cancel button looks.</summary>
    public PromptButton CancelStyle { get; init; } = PromptButton.Plain;

    /// <summary>Whether the confirm button starts focused instead of the cancel button.</summary>
    public bool FocusConfirm { get; init; }

    /// <summary>Called when the player confirms.</summary>
    public Action? Confirmed { get; init; }

    /// <summary>Called when the player cancels, or when another prompt replaces this one.</summary>
    public Action? Canceled { get; init; }
}
