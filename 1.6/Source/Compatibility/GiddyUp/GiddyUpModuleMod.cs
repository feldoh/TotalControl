using FactionLoadout;
using Verse;

namespace TotalControlGiddyUpCompat;

/// <summary>
/// Entry point for the GiddyUp compatibility module.
/// Registers the GiddyUp module with Total Control's module system.
/// This assembly is loaded conditionally via loadFolders.xml only when GiddyUp is active.
/// </summary>
public class GiddyUpModuleMod : Mod
{
    public GiddyUpModuleMod(ModContentPack content)
        : base(content)
    {
        ModuleRegistry.Register(new GiddyUpModule());
    }
}
