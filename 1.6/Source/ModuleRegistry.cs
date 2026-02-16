using System.Collections.Generic;
using System.Linq;

namespace FactionLoadout;

/// <summary>
/// Central registry for Total Control modules. External assemblies register their
/// <see cref="ITotalControlModule"/> implementations here during their Mod constructor.
/// Core iterates registered modules to delegate UI, serialization, and apply logic.
/// </summary>
public static class ModuleRegistry
{
    private static readonly List<ITotalControlModule> modules = [];
    private static bool initialized;

    /// <summary>
    /// All registered modules. Safe to iterate at any time; the list grows as modules register.
    /// </summary>
    public static IReadOnlyList<ITotalControlModule> Modules => modules;

    /// <summary>
    /// Register a module with core. Call this in your Mod constructor.
    /// Modules should be registered before presets load (i.e. before ModCore.LoadLate completes).
    /// Late registration is allowed but logged as a warning since previously-loaded presets
    /// won't have had their module data parsed.
    /// </summary>
    public static void Register(ITotalControlModule module)
    {
        if (module == null)
        {
            ModCore.Warn("Attempted to register a null module.");
            return;
        }

        if (string.IsNullOrEmpty(module.ModuleKey))
        {
            ModCore.Warn($"Module '{module.ModuleName}' has a null or empty ModuleKey. Skipping registration.");
            return;
        }

        if (modules.Any(m => m.ModuleKey == module.ModuleKey))
        {
            ModCore.Warn($"Duplicate module key '{module.ModuleKey}' from '{module.ModuleName}'. " + "A module with this key is already registered. Skipping.");
            return;
        }

        modules.Add(module);
        ModCore.Log($"Registered module: '{module.ModuleName}' (key: {module.ModuleKey}, active: {module.IsActive})");

        if (initialized)
        {
            ModCore.Warn($"Module '{module.ModuleName}' registered after InitializeAll() was called. " + "Data from already-loaded presets will not include this module's data.");
            module.Initialize();
        }
    }

    /// <summary>
    /// Initialize all registered modules. Called once by ModCore before presets are loaded.
    /// </summary>
    internal static void InitializeAll()
    {
        initialized = true;
        ModCore.Log($"Initializing {modules.Count} registered module(s)...");
        foreach (ITotalControlModule module in modules)
        {
            ModCore.Debug($"Initializing module: '{module.ModuleName}' (key: {module.ModuleKey})");
            module.Initialize();
        }
    }

    /// <summary>
    /// Find a registered module by its key. Returns null if not found.
    /// </summary>
    public static ITotalControlModule GetModule(string moduleKey)
    {
        return modules.FirstOrDefault(m => m.ModuleKey == moduleKey);
    }
}
