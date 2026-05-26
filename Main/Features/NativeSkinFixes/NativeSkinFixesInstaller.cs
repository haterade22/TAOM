using System;
using System.Diagnostics;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Core.Logging;
using TAOM.Features.NativeSkinFixes.Interop;

namespace TAOM.Features.NativeSkinFixes;

/// <summary>
/// Entry point for the NativeSkinFixes feature. Loads
/// <c>TAOM.NativeSkinFixes.dll</c> and installs the three native hooks
/// (covers_head morph fix, hair cloth rescue, face mesh render-list
/// suppression). Called once from <c>TaomSubModule.OnBeforeInitialModuleScreenSetAsRoot</c>
/// — after all modules are loaded, before the main menu renders.
/// </summary>
/// <remarks>
/// <para><b>Graceful degradation.</b> If the native DLL is missing, has stale
/// exports, or its byte-pattern signatures don't match the running game build,
/// each hook fails individually and is logged. The game keeps running; the
/// only visible effect is that the original rendering bugs remain.</para>
/// <para><b>Editor mode skipped.</b> The Bannerlord editor process (path
/// contains <c>wEditor</c>) does not install hooks — modifying engine code in
/// the editor can corrupt scene state.</para>
/// </remarks>
public static class NativeSkinFixesInstaller
{
    /// <summary>True after <see cref="Install"/> has run; prevents double-install.</summary>
    public static bool HasInstalled { get; private set; }

    /// <summary>True if every hook came up cleanly. False if any was skipped or failed.</summary>
    public static bool AllHooksInstalled { get; private set; }

    /// <summary>
    /// Returns true when the host process is the Bannerlord editor, in which
    /// case the hooks must be skipped. Public for unit testing.
    /// </summary>
    public static bool IsEditorProcess(string? processFileName)
    {
        if (string.IsNullOrEmpty(processFileName)) return false;
        return processFileName!.IndexOf("wEditor", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Localization key shown when ALL three hooks installed successfully.
    /// Public for unit testing alongside the localization XML entry.
    /// </summary>
    public static string LoadedMessageKey => "{=taom_nativeskinfixes_loaded}";

    /// <summary>
    /// Default English text for <see cref="LoadedMessageKey"/>. Used when
    /// every hook is live and patching engine code.
    /// </summary>
    public static string LoadedMessageDefault =>
        "NativeSkinFixes active — covers_head morph fix + hair/beard cloth simulation";

    /// <summary>
    /// Localization key shown when one or more hooks failed to install
    /// (typically: byte-pattern stubs for an unauthored Bannerlord version).
    /// Distinct from the success banner so the user understands the feature
    /// is degraded.
    /// </summary>
    public static string DegradedMessageKey => "{=taom_nativeskinfixes_degraded}";

    /// <summary>
    /// Default English text for <see cref="DegradedMessageKey"/>. Used when
    /// the DLL loaded but at least one hook is inert.
    /// </summary>
    public static string DegradedMessageDefault =>
        "NativeSkinFixes degraded — pattern signatures unauthored for this Bannerlord build; vanilla rendering";

    /// <summary>
    /// Boot-time entry point. Idempotent — calling twice is safe.
    /// </summary>
    public static void Install(IModLogger logger)
    {
        if (HasInstalled) return;
        HasInstalled = true;

        if (logger == null) throw new ArgumentNullException(nameof(logger));

        // Editor-mode skip — modifying native code while the editor runs can
        // corrupt scene authoring state. The Bannerlord editor process always
        // contains "wEditor" in its filename.
        string? processName = SafeGetProcessFileName();
        if (IsEditorProcess(processName))
        {
            logger.LogInfo("[NativeSkinFixes] editor process detected — skipping native hook install");
            return;
        }

        if (!NativeHookLoader.EnsureLoaded())
        {
            logger.LogWarning(
                "[NativeSkinFixes] TAOM.NativeSkinFixes.dll failed to load — feature inert. " +
                $"Detail: {NativeHookLoader.LastLoadError ?? "(no error captured)"}");
            return;
        }

        // Install each hook. They're independent — if one fails the others
        // still get a chance, and the user gets the partial benefit.
        bool a = CoversHeadHookInterop.TryInstall(logger);
        bool b = HairClothHookInterop.TryInstall(logger);
        bool c = FaceMeshObserveHookInterop.TryInstall(logger);
        AllHooksInstalled = a && b && c;

        // User-facing banner. Distinct text for success vs degraded so the
        // user understands whether engine hooks are live or vanilla rendering
        // is still in effect. Color reinforces the same signal.
        string bannerKey = AllHooksInstalled ? LoadedMessageKey   : DegradedMessageKey;
        var    bannerCol = AllHooksInstalled
            ? new Color(0.6f, 0.8f, 1f)        // soft blue for full success
            : new Color(1f, 0.7f, 0.3f);       // amber for degraded
        InformationManager.DisplayMessage(new InformationMessage(
            new TextObject(bannerKey).ToString(),
            bannerCol));

        if (!AllHooksInstalled)
        {
            logger.LogWarning(
                "[NativeSkinFixes] at least one hook failed — see NativeSkinFixes.log under " +
                "Documents\\Mount and Blade II Bannerlord\\Logs\\TAOM\\ for details");
        }
    }

    /// <summary>
    /// Reverses install — called from <see cref="SubModule.OnSubModuleUnloaded"/>
    /// so DLL unload during reload-in-same-process doesn't leak hook state.
    /// </summary>
    public static void Uninstall()
    {
        if (!HasInstalled) return;
        CoversHeadHookInterop.Uninstall();
        HairClothHookInterop.Uninstall();
        FaceMeshObserveHookInterop.Uninstall();
        HasInstalled = false;
        AllHooksInstalled = false;
    }

    private static string? SafeGetProcessFileName()
    {
        try
        {
            return Process.GetCurrentProcess().MainModule?.FileName;
        }
        catch
        {
            // Some hosts deny MainModule access; treat as "not the editor".
            return null;
        }
    }
}
