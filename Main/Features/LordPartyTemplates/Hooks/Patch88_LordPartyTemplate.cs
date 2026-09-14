using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;

namespace TAOM.Features.LordPartyTemplates.Hooks;

/// <summary>
/// Patch88: a named lord fields his own party template instead of his clan's (#580).
///
/// THE ENGINE HAS NO PER-HERO TEMPLATE. Both reads that pick a lord's roster go through the clan:
/// <c>LordPartyComponent.InitializationArgs.InitializeLordPartyProperties</c> (installed v1.4.8,
/// <c>LordPartyComponent.cs:38</c>) takes <c>owner.Clan.DefaultPartyTemplate</c> for the initial
/// roster on every creation path, and <c>HeroSpawnCampaignBehavior.SpawnLordParty</c> (:265) reads
/// <c>Owner.Clan.DefaultPartyTemplate</c> again for the new-game top-up. <c>Clan.DefaultPartyTemplate</c>
/// (<c>Clan.cs:112-122</c>) is a plain getter: the clan binding, else the culture default.
///
/// THE SEAM. This postfix sits on that getter and substitutes the mapped template only while a
/// lord spawn is in flight. The two scope patches (<see cref="Patch88_SpawnLordPartyScope"/>,
/// <see cref="Patch88_InitializeLordPartyPropertiesScope"/>) set <see cref="AmbientOwner"/> to the
/// spawning hero for the duration of their target and restore the previous value afterwards; they
/// nest, because <c>SpawnLordParty</c> reaches the component initializer through
/// <c>MobilePartyHelper.SpawnLordParty</c>. With no ambient owner the postfix returns on its first
/// line, so every other read of the getter (<c>Clan.HasNavalNavigationCapability</c>,
/// <c>ClanVariablesCampaignBehavior</c>, the bandit and caravan-ambush spawns) pays one static read.
///
/// WHAT IT NEVER TOUCHES. A read of any clan other than the owner's, any owner the JSON does not
/// name (his clanmates keep the clan binding), the rebel branch (<c>Culture.RebelsPartyTemplate</c>
/// is a different getter), and the player's clan (<c>InitializeLordPartyProperties</c> :32 takes the
/// position-only path for it before any template is read). The decision is
/// <see cref="LordPartyTemplateResolution.Resolve"/>, pure and tested; this class only feeds it.
///
/// THE WINDOW IS THE WHOLE CALL, not the one statement. Any read of the owner's clan template that
/// runs synchronously inside either scope sees the swap. In the installed engine (re-checked on
/// v1.5.2: the getter and both scoped readers are byte-identical to v1.4.8, and the 13 readers of
/// <c>DefaultPartyTemplate</c> in the assembly are the same 13) nothing else does:
/// <c>MobilePartyCreated</c> fires inside <c>MobileParty.CreateParty</c> while the scope is open and
/// none of its three listeners reads the getter, and <c>LordPartyComponent</c> overrides
/// <c>CanHaveNavalNavigationCapability</c> outright (<c>true</c> on v1.4.8,
/// <c>_leader?.CanHaveFleet ?? true</c> since v1.5.x), never falling through to the clan's naval
/// capability, which is the one path that would read the template. A future listener or another
/// mod's hook that reads it inside the window would see the override.
///
/// FAILURE. An id the JSON names but the object manager cannot resolve leaves vanilla's answer in
/// place and warns once per hero, so a typo is a lord on his clan roster plus a log line, never a
/// null template into <c>FindAppropriateInitialRosterForMobileParty</c>.
/// </summary>
[HarmonyPatch(typeof(Clan), nameof(Clan.DefaultPartyTemplate), MethodType.Getter)]
[HarmonyPatchCategory(Category)]
public static class Patch88_LordPartyTemplate
{
    internal const string Category = "Patch88_LordPartyTemplate";

    // The campaign ticks on one thread, but the scope is per call stack by nature and a
    // thread-static costs nothing extra.
    [ThreadStatic] private static Hero? _ambientOwner;

    private static ILordPartyTemplateService? _service;
    private static IModLogger? _logger;

    // Named once per hero; the daily clan tick would otherwise repeat it.
    private static readonly HashSet<string> _warned = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>The hero whose party is being built right now, or null outside a lord spawn.</summary>
    internal static Hero? AmbientOwner
    {
        get => _ambientOwner;
        set => _ambientOwner = value;
    }

    internal static void Initialize(ILordPartyTemplateService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>The service and logger are process-global; a module reload must not keep them.</summary>
    internal static void ResetForUnload()
    {
        _service = null;
        _logger = null;
        _ambientOwner = null;
        _warned.Clear();
    }

    [HarmonyPostfix]
    public static void Postfix(Clan __instance, ref PartyTemplateObject __result)
    {
        var owner = _ambientOwner;
        if (owner == null)
            return;

        try
        {
            var service = _service;
            if (service == null)
                return;

            var templateId = LordPartyTemplateResolution.Resolve(
                owner.StringId,
                owner.Clan?.StringId,
                __instance?.StringId,
                service);
            if (templateId == null)
                return;

            var template = MBObjectManager.Instance?.GetObject<PartyTemplateObject>(templateId);
            if (template == null)
            {
                WarnOnce(owner.StringId, templateId);
                return;
            }

            __result = template;
        }
        catch (Exception ex)
        {
            // Vanilla's answer is already in __result and is a working default.
            _logger?.LogError($"[LordPartyTemplates] override failed for '{owner.StringId}', clan roster stands: {ex}");
        }
    }

    private static void WarnOnce(string heroId, string templateId)
    {
        if (!_warned.Add(heroId))
            return;

        _logger?.LogWarning(
            $"[LordPartyTemplates] '{heroId}' is mapped to party template '{templateId}', which the object " +
            "manager cannot resolve; he fields his clan roster instead. Check taom_partyTemplates.xml.");
    }
}
