using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace TAOM.Tests.Migration;

/// <summary>
/// Loads the Bannerlord module assemblies (SandBox, SandBoxCore, CustomBattle, StoryMode, Native)
/// into the test AppDomain so the binding-verification suite can resolve TaleWorlds types by name.
///
/// Why this exists: Main/TAOM.csproj references these module DLLs with <c>Private=False</c>, so they
/// are NOT copied into the test bin (only <c>TaleWorlds.*.dll</c> is, via TAOM.Tests.csproj's
/// <c>Private=True</c> reference). Harmony's <c>AccessTools.TypeByName("SandBox.View.Map....")</c> and
/// several patch <c>TargetMethod()</c> bodies only resolve types from *loaded* assemblies, so without
/// this pre-load they would return null in tests and produce false binding failures.
///
/// Game dir resolution mirrors Directory.Build.props: BANNERLORD_OVERRIDE_DIR (if its bin holds
/// Bannerlord.exe) else BANNERLORD_GAME_DIR. If neither resolves, <see cref="EnsureLoaded"/> returns
/// false and the binding tests should Assert.Inconclusive (e.g. CI without a game install).
/// </summary>
internal static class GameAssemblies
{
    private static readonly object Gate = new object();
    private static bool _attempted;

    public static string GameDir { get; private set; }
    public static string BinFolder { get; private set; }
    public static List<string> Diagnostics { get; } = new List<string>();

    // StoryMode/CustomBattle/Native carry types some TAOM patches and adapters touch by name.
    private static readonly string[] ModuleFolders =
        { "SandBox", "SandBoxCore", "CustomBattle", "StoryMode", "Native" };

    public static bool EnsureLoaded()
    {
        lock (Gate)
        {
            if (_attempted) return GameDir != null;
            _attempted = true;

            GameDir = ResolveGameDir();
            if (GameDir == null)
            {
                Diagnostics.Add("Game dir unresolved — BANNERLORD_OVERRIDE_DIR/BANNERLORD_GAME_DIR unset or invalid.");
                return false;
            }

            BinFolder = ResolveBinFolder(GameDir);
            if (BinFolder == null)
            {
                Diagnostics.Add($"No bin folder with Bannerlord.exe under '{GameDir}'.");
                GameDir = null;
                return false;
            }

            AppDomain.CurrentDomain.AssemblyResolve += ResolveFromGameFolders;

            // Eagerly load every TaleWorlds.*.dll already copied into the test bin so simple-name
            // type resolution sees the full engine surface up front (otherwise lazily-referenced
            // types like TaleWorlds.Engine's PathReuseCache aren't in GetAssemblies() until touched).
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (Directory.Exists(baseDir))
                foreach (var dll in Directory.GetFiles(baseDir, "TaleWorlds.*.dll"))
                    TryLoad(dll);

            // SandBox / SandBoxCore / CustomBattle / StoryMode / Native are NOT copied to the test bin
            // (Main references them Private=False) — load them from the game module folders.
            foreach (var module in ModuleFolders)
            {
                var dir = Path.Combine(GameDir, "Modules", module, "bin", BinFolder);
                if (!Directory.Exists(dir)) continue;
                foreach (var dll in Directory.GetFiles(dir, "*.dll"))
                    TryLoad(dll);
            }
            return true;
        }
    }

    private static void TryLoad(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        // TaleWorlds.Native is a native/mixed image — a managed load throws BadImageFormatException.
        if (name.Equals("TaleWorlds.Native", StringComparison.OrdinalIgnoreCase)) return;

        // Skip anything already loaded (e.g. TaleWorlds.* copied into the test bin) to avoid a
        // second LoadFrom-context copy of the same identity.
        if (AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase)))
            return;

        try { Assembly.LoadFrom(path); }
        catch (Exception ex) { Diagnostics.Add($"load-skip {name}: {ex.GetType().Name}"); }
    }

    private static Assembly ResolveFromGameFolders(object sender, ResolveEventArgs args)
    {
        var simple = new AssemblyName(args.Name).Name + ".dll";
        var candidates = new List<string> { Path.Combine(GameDir, "bin", BinFolder, simple) };
        candidates.AddRange(ModuleFolders.Select(m => Path.Combine(GameDir, "Modules", m, "bin", BinFolder, simple)));
        foreach (var c in candidates)
        {
            if (!File.Exists(c)) continue;
            try { return Assembly.LoadFrom(c); }
            catch { /* fall through to next candidate */ }
        }
        return null;
    }

    private static string ResolveGameDir()
    {
        var over = Environment.GetEnvironmentVariable("BANNERLORD_OVERRIDE_DIR");
        if (!string.IsNullOrEmpty(over) &&
            File.Exists(Path.Combine(over, "bin", "Win64_Shipping_Client", "Bannerlord.exe")))
            return over;

        var game = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        if (!string.IsNullOrEmpty(game) && Directory.Exists(game)) return game;

        return null;
    }

    private static string ResolveBinFolder(string gameDir)
    {
        if (File.Exists(Path.Combine(gameDir, "bin", "Win64_Shipping_Client", "Bannerlord.exe")))
            return "Win64_Shipping_Client";
        if (File.Exists(Path.Combine(gameDir, "bin", "Gaming.Desktop.x64_Shipping_Client", "Bannerlord.exe")))
            return "Gaming.Desktop.x64_Shipping_Client";
        return null;
    }
}
