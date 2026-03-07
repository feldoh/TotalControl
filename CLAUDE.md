# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Total Control** (assembly name: `FactionLoadout`) is a RimWorld mod that enables deep customization of faction pawns — appearance, equipment, genes, hediffs, and more. It uses Harmony patches to intercept pawn generation and apply user-defined edits.

## Build Commands

```bash
# Release build (formats code, creates zip, copies to Steam if configured)
dotnet build -c Release

# The solution file is also available
dotnet build FactionLoadout.sln
```

Output DLLs go to `{version}/Assemblies/`. Release builds produce `FactionLoadout.zip` at the repo root.

### Environment Variables (optional)
- `RIMWORLD_PATH` — RimWorld install directory (fallback: `../../../../` relative to Source)
- `STEAM_MODS_PATH` or `RIMWORLD_STEAM_MODS_PATH` — Steam workshop mods directory (release copies mod here)

If local RimWorld DLLs aren't found, the build falls back to the `Krafs.Rimworld.Ref` NuGet package.

## Code Structure

### Version Layout
Three parallel source trees for RimWorld 1.4, 1.5, and 1.6:
```
1.4/Source/  1.5/Source/  1.6/Source/   # C# source (1.6 is latest/primary)
1.4/Assemblies/  1.5/Assemblies/       # Compiled output
Common/                                # Shared translations and textures
Compatibility/{modname}/              # Conditional assembly for other mods where {modname} describes the mod
```
`loadFolders.xml` controls version-specific loading. Only ever work on the latest versioned tree unless told otherwise.

### Namespace
All code is in a single flat namespace: **`FactionLoadout`**. The compatibility modules each have their own projects and assemblies conditionally loaded via loadfolders.
This means modules can include other dlls to get easy access to classes without needing to place a dependency on that dll for the main mod.

### Data Model Hierarchy
```
Preset                    # Top-level: named collection of faction edits (XML-serialized)
  └─ FactionEdit          # Per-faction configuration
       └─ PawnKindEdit    # Per-pawnkind customization (appearance, apparel, weapons, etc.)
            └─ SpecRequirementEdit  # Specific forced item with material/quality/style options
            └─ InventoryOptionEdit  # Inventory item configuration
```
Presets are stored as XML files in `GenFilePaths.ConfigFolderPath/TotalControlData`.

### Key Entry Points
- **`ModCore`** — Mod initialization, Harmony patching, preset loading, logging (`Debug`, `Log`, `Warn`, `Error`)
- **`MySettings`** — Settings: `ActivePreset` GUID, `VanillaRestrictions`, `VerboseLogging`, `PatchKindInRequests`
- **`IO`** — XML file persistence for presets

### Harmony Patches (core generation pipeline)
- **`ApparelGenPatch`** — Postfix on `PawnApparelGenerator.GenerateStartingApparelFor`; forces apparel, hair, beards, colors
- **`WeaponGenPatch`** — Postfix on `PawnWeaponGenerator.TryGenerateWeaponFor`; forces weapons
- **`PawnGenPatchCore`** — Postfix on `PawnGenerator.GenerateNewPawnInternal`; applies forced hediffs and genes
- **`PawnGenAgePatchCore`** — Postfix on `PawnGenerator.GenerateRandomAge`; enforces age constraints
- **`PawnGenPatchBodyTypeDef`** — Postfix on `PawnGenerator.GetBodyTypeFor`; forces body types
- **`FactionLeaderPatch`** — Prefix on `Faction.TryGenerateNewLeader`; ensures leaders use patched kinds

### UI Windows
`Dialog_FactionLoadout` → `PresetUI` → `FactionEditUI` → `PawnKindEditUI` (tabbed: Appearance, Apparel, Weapon, Implants, Inventory, Raid Points, Raid Loot)

### Def Cloning System
`CloningUtility` deep-clones `PawnKindDef` and `FactionDef` objects via reflection. Faction-specific clones are named `{original}_TCCln_{faction}` and tracked in `FactionEdit.replacementToOriginal`. `FactionEdit.Normalize()` resolves a replacement back to its original def.

### Optional Mod Integration
VE Psycasts and VFE Ancients are integrated via reflection helpers (`VEPsycastsReflectionHelper`, `VFEAncientsReflectionHelper`) with lazy-loaded type references. These degrade gracefully if the mods are absent. VE Psycasts also has a separate conditional assembly in `Compatibility/VEPsycasts/`.

### Special Synthetic Factions
Two runtime-created factions (`SpecialWildManFaction`, `SpecialCreepjoinerFaction`) allow editing non-faction pawnkinds through the standard UI.

## Key Patterns

- **Edit accumulation**: `ApparelGenPatch`/`WeaponGenPatch` collect edits from all applicable `PawnKindEdit` objects (global + specific), categorize into always/chance/pool groups, then apply in order
- **DefRef\<T\>**: Wraps def references by name with lazy resolution; handles missing defs from removed mods
- **ForcedExtrasModExtension**: Custom `DefModExtension` attached to `PawnKindDef.modExtensions` carrying forced hediffs and genes
- **Publicizer**: `Krafs.Publicizer` makes internal RimWorld types accessible (configured in .csproj with `<Publicize Include="Assembly-CSharp" />`) you can publicize other mod dependencies if needed
- **Modules**: the module system allows use of other mods. They generally provide their own tab and data control


## Backwards Compatibility (CRITICAL)

**Never release a breaking change to the preset/settings XML format.** Users invest significant time configuring faction edits. When schema changes are needed:

1. **New fields must have sensible defaults** so old presets that lack the field behave identically to before
2. **Never remove or rename existing XML fields** — if a field is obsolete, keep reading it and migrate internally
3. **Add a migration step on import** — detect old format in `ExposeData()` and convert transparently
4. **Both old and new format files must load without errors**
5. **Never reorder enum values** (e.g. `SpecRequirementEdit.SelectionMode`) — only append new ones
6. **Don't change clone naming conventions** (`{original}_TCCln_{faction}`) or Preset GUID handling

## Performance (CRITICAL)

1. **Prefer to make changes to the defs rather than patch at runtime** Def changes only need to be made once, patches run on every pawn generated making raids slow
2. **Always cache reflective field accesses** Reflection is slow, so always cache agressively to avoid performance issues
3. **Prefer modules or subtyping to reflection** Reflection is slow direct references are better
4. **Reflection in patches should consider using publicizer instead** Publicizer avoids runtime costs for accessing private fields

## Coding Conventions

### Data Persistence
- **Never use `LookMode.Def` for user-configured lists** — if a mod is removed, defs silently become `null` and are lost on the next save cycle. Instead use `DefRef<T>` (serialized with `LookMode.Deep`) or raw `string` defNames with lazy resolution.
- **Always prefer lossless storage** using basic types (`string`, `int`, `float`, etc.) or `DefRef<T>` wrappers that preserve the defName even when the def can't be resolved.

### Control Flow
- **Never use `if`/`else` without braces.** Braceless `if` is only permitted for single-line statements (guard clauses, early returns, etc.).
  ```csharp
  // OK — single-line if
  if (value == null) return;

  // OK — braced if/else
  if (active) { field = newValue; }
  else { field = null; }

  // NEVER — braceless else
  if (active)
      field = newValue;
  else
      field = null;  // ← not allowed
  ```

### Translations
- **All user-facing strings must use translation keys**, never hardcoded English text. Add keys to `Common/Languages/English/Keyed/FactionLoadout_Keys.xml` and reference them via `"KeyName".Translate()`.
- This includes UI labels, button text, tooltips, section headers, and default/placeholder text.

## Adding a New Compatibility Module (Checklist)

When creating a new `Compatibility/{ModName}/` module, **all five of these files must be updated** or the module will silently be skipped or break the release zip:

1. **`1.6/Source/Compatibility/{ModName}/{AssemblyName}.csproj`** — new project; copy GiddyUp as template
2. **`1.6/Source/FactionLoadout.sln`** — add a `Project(...)` entry and all three configuration platform entries (`Debug`, `Release`, `DevRelease`) so IDEs and `dotnet build FactionLoadout.sln` see the project
3. **`1.6/Source/Packaging/Packaging.csproj`** — add a `<ProjectReference>` with `<ReferenceOutputAssembly>false</ReferenceOutputAssembly>`; this enforces build order so the DLL exists before the zip step runs
4. **`loadFolders.xml`** — add `<li IfModActive="mod.package.id">Compatibility/{ModName}/1.6</li>` under `<v1.6>`
5. **`About/About.xml`** — add `<li>mod.package.id</li>` to `<loadAfter>`

The Packaging project's glob (`../../../Compatibilit*/**`) already picks up any DLL under `Compatibility/`, but only after the project reference guarantees it has been built.

## Contributing Notes (from CONTRIBUTING.md)

- Isolate mod compatibility using `MayRequire`, `PatchOperationFindMod`, or `loadFolders.xml` entries
- Add `loadAfter` entries in `About/About.xml` for new mod dependencies
- Release builds auto-format with `dotnet csharpier` if installed

## Testing

No automated test suite. Changes require manual testing in RimWorld. Enable verbose logging via `MySettings.VerboseLogging` and check RimWorld's debug log for `[FacLoadout]` messages.
