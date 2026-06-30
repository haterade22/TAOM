using System;
using System.Reflection;
using HarmonyLib;
using SandBox;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;

namespace TAOM.Features.SkipCampaignIntro.Hooks;

/// <summary>
/// Patch58 — Prefix on <c>SandBoxGameManager.OnLoadFinished</c> that skips the vanilla SandBox campaign
/// intro video (<c>Modules/SandBox/Videos/CampaignIntro/campaign_intro.ivf</c>) when starting a NEW game,
/// dropping straight into character creation instead of forcing the player to press Escape through it.
/// <para>
/// It mirrors the engine's own no-video path: vanilla <c>OnLoadFinished</c> already calls the private
/// <c>LaunchSandboxCharacterCreation()</c> directly — instead of building and pushing a
/// <see cref="VideoPlaybackState"/> — when <c>Game.Current.IsDevelopmentMode</c> is true. This patch
/// forces that same branch for a normal new game, so no video state is ever created.
/// </para>
/// <para>
/// Save-game loads (<c>LoadingSavedGame</c>) never play the intro and run vanilla untouched. Any missing
/// reflection binding (engine drift) or thrown exception falls back to vanilla — the intro just plays —
/// so the patch can never break new-game start. Hardcoded always-skip; no MCM toggle.
/// </para>
/// <para>
/// The two <see cref="MethodInfo"/> bindings are resolved once at type load and exposed
/// <c>internal</c> so <c>TAOM.Tests</c> can drift-guard them against the installed engine. The patch's
/// own <c>[HarmonyPatch]</c> target (<c>OnLoadFinished</c>) is covered by <c>HarmonyPatchBindingTests</c>.
/// </para>
/// </summary>
[HarmonyPatchCategory("Patch58_SkipCampaignIntro")]
[HarmonyPatch(typeof(SandBoxGameManager), nameof(SandBoxGameManager.OnLoadFinished))]
public static class Patch58_SkipCampaignIntro
{
    /// <summary>Private <c>SandBoxGameManager.LaunchSandboxCharacterCreation()</c> — pushes the CC state.</summary>
    internal static readonly MethodInfo? LaunchCc =
        AccessTools.Method(typeof(SandBoxGameManager), "LaunchSandboxCharacterCreation");

    /// <summary>Protected <c>MBGameManager.IsLoaded</c> setter — the original sets it at the end of every branch.</summary>
    internal static readonly MethodInfo? SetIsLoaded =
        AccessTools.PropertySetter(typeof(MBGameManager), "IsLoaded");

    private static IModLogger? _logger;

    public static void Initialize(IModLogger logger) => _logger = logger;

    [HarmonyPrefix]
    public static bool Prefix(SandBoxGameManager __instance)
    {
        try
        {
            // Save-game loads take the engine's own load branch (no intro video) — leave it untouched.
            if (__instance.LoadingSavedGame)
                return true;

            // Engine drift renamed a binding → fail safe to vanilla (the intro plays, no crash).
            if (LaunchCc == null || SetIsLoaded == null)
            {
                _logger?.LogWarning(
                    "Patch58_SkipCampaignIntro: a reflection binding did not resolve; the vanilla intro video plays.");
                return true;
            }

            // Mirror the engine's IsDevelopmentMode bypass: launch character creation directly
            // (== OnLoadFinished's dev-mode branch) and mark loading complete (== its trailing
            // IsLoaded = true), skipping the VideoPlaybackState the original would otherwise build.
            LaunchCc.Invoke(__instance, null);
            SetIsLoaded.Invoke(__instance, new object[] { true });
            return false;
        }
        catch (Exception e)
        {
            _logger?.LogWarning($"Patch58_SkipCampaignIntro Prefix threw; falling back to the vanilla intro: {e}");
            return true;
        }
    }
}
