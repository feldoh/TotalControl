using System.Collections.Generic;
using Verse;

namespace FactionLoadout;

/// <summary>
/// Interface for external modules that extend Total Control with support for additional mods.
/// Modules register themselves via <see cref="ModuleRegistry.Register"/> in their Mod constructor.
///
/// Each module gets its own XML sub-node inside PawnKindEdit's &lt;modules&gt; element, identified by
/// <see cref="ModuleKey"/>. Module data is preserved as raw XML when the module is absent, so users
/// don't lose configuration when temporarily disabling a dependency mod.
///
/// Modules manage their own per-PawnKindEdit state internally (e.g. a static dictionary keyed by
/// PawnKindEdit instance). Core does not hold module data — it only delegates serialization, UI, and
/// apply calls.
/// </summary>
public interface ITotalControlModule
{
    /// <summary>
    /// Unique stable key used as the XML node name for this module's data (e.g. "vePsycasts").
    /// Must be a valid XML element name. Once shipped, never change this value — it's the
    /// identity used to match saved data back to the module.
    /// </summary>
    string ModuleKey { get; }

    /// <summary>
    /// Human-readable display name for logging (e.g. "VE Psycasts").
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Whether this module's dependency mod is currently loaded and active.
    /// Checked before calling <see cref="AddTabs"/>, <see cref="ExposeData"/>, and <see cref="Apply"/>.
    /// A module that reports inactive will have its previously-saved XML preserved by core.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Called once after all modules are registered and before presets are loaded.
    /// Use this to set up any static state, caches, or def lookups.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Add UI tabs to the PawnKindEdit editor. Called during tab list construction.
    /// Only called when <see cref="IsActive"/> is true.
    /// Add zero or more <see cref="Tab"/> entries to the provided list.
    /// </summary>
    /// <param name="edit">The current PawnKindEdit being edited.</param>
    /// <param name="defaultKind">The default (unmodified) PawnKindDef for this editor.</param>
    /// <param name="tabs">The tab list to add to. Append your tabs here.</param>
    void AddTabs(PawnKindEdit edit, PawnKindDef defaultKind, List<Tab> tabs);

    /// <summary>
    /// Serialize or deserialize module-specific data for a PawnKindEdit.
    /// Called inside a Scribe enter/exit node block for this module's <see cref="ModuleKey"/>.
    /// Use standard Scribe_Values, Scribe_Collections, etc. — the Scribe cursor is already
    /// positioned inside this module's XML node.
    /// </summary>
    /// <param name="edit">The PawnKindEdit whose data is being serialized.</param>
    void ExposeData(PawnKindEdit edit);

    /// <summary>
    /// Apply module-specific edits to a PawnKindDef at runtime.
    /// Called during <see cref="PawnKindEdit.Apply"/> after core edits are applied.
    /// </summary>
    /// <param name="edit">The PawnKindEdit containing the user's configuration.</param>
    /// <param name="def">The PawnKindDef being modified.</param>
    /// <param name="global">The global PawnKindEdit for this faction, or null if none.</param>
    void Apply(PawnKindEdit edit, PawnKindDef def, PawnKindEdit global);
}
