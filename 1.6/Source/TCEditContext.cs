namespace FactionLoadout;

/// <summary>
/// Ambient "currently being edited" context for the fullscreen Total Control UI.
///
/// Some draw helpers (notably <c>SpecificGearDrawer</c>) historically resolved the
/// active faction by looking for a <c>FactionEditUI</c> window on the WindowStack. The
/// fullscreen shell renders the equivalent screens without that window present, so it
/// publishes the faction being edited here instead. The old windows fall back to this
/// too, so both UIs work during the transition.
///
/// Set when a faction is opened in the shell; cleared when the shell closes.
/// </summary>
public static class TCEditContext
{
    public static FactionEdit CurrentFaction;
}
