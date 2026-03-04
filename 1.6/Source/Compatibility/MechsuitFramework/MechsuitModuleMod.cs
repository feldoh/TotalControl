using FactionLoadout;
using Verse;

namespace TotalControlMechsuitCompat;

/// <summary>
/// Entry point for the MechsuitFramework compatibility module.
/// Registers the module with Total Control's module system.
/// This assembly is loaded conditionally via loadFolders.xml only when Exosuit Framework is active.
/// </summary>
public class MechsuitModuleMod : Mod
{
    public MechsuitModuleMod(ModContentPack content)
        : base(content)
    {
        ModuleRegistry.Register(new MechsuitModule());
    }
}
