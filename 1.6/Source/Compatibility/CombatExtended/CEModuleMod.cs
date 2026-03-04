using FactionLoadout.Modules;
using Verse;

namespace TotalControlCECompat;

/// <summary>
/// Entry point for the Combat Extended compatibility module.
/// Registers the CE module with Total Control's module system.
/// This assembly is loaded conditionally via loadFolders.xml only when CE is active.
/// </summary>
public class CEModuleMod : Mod
{
    public CEModuleMod(ModContentPack content)
        : base(content)
    {
        ModuleRegistry.Register(new CEModule());
    }
}
