using System;
using FactionLoadout.Modules;
using Verse;

namespace FactionLoadout.UISupport;

/// <summary>
/// Represents a tab in the PawnKindEdit editor UI.
/// External modules use this type to contribute their own tabs via
/// <see cref="ITotalControlModule.AddTabs"/>.
/// </summary>
public class Tab
{
    public readonly string Name;

    private readonly Action<Listing_Standard> draw;

    public Tab(string name, Action<Listing_Standard> draw)
    {
        Name = name;
        this.draw = draw;
    }

    public virtual void Draw(Listing_Standard ui)
    {
        DrawRegionTitle(ui, Name);
        draw?.Invoke(ui);
    }

    protected static void DrawRegionTitle(Listing_Standard ui, string title)
    {
        ui.GapLine(26);
        Widgets.Label(ui.GetRect(42), $"<size=26><b><color=#73fff2>{title}</color></b></size>");
    }
}
