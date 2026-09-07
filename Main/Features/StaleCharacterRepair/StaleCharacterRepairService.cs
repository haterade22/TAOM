using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.StaleCharacterRepair.Domain;

namespace TAOM.Features.StaleCharacterRepair;

/// <summary>
/// Save-load repair for a character whose ModuleData definition no longer exists.
///
/// <para><b>The crash that produced it</b> (bundle 065939b6, 2026-09-05, engine v1.4.8, each link
/// read from the decompiled source):</para>
/// <list type="number">
/// <item><c>Campaign.OnGameLoaded:688</c> calls <c>CampaignObjectManager.AfterLoad()</c>, which runs
/// <c>Clan.AfterLoad</c> -> <c>UpdateCurrentStrength</c> -> <c>PartyBase.EstimatedStrength</c> ->
/// <c>DefaultMilitaryPowerModel.GetPowerOfParty</c> -> <c>MobileParty.Morale</c> -> the registered
/// <c>PartyMoraleModel</c>.</item>
/// <item><c>DefaultPartyMoraleModel.GetMoraleEffectsFromSkill:206-213</c> resolves the character
/// through <c>SkillHelper.GetEffectivePartyLeaderForSkill</c> and null-checks it, so the character
/// is NOT null.</item>
/// <item><c>SkillHelper.GetEffectivePartyLeaderForSkill:78-94</c> returns
/// <c>party.MemberRoster.GetCharacterAtIndex(0)</c> for a party with no leader hero: a plain TROOP.
/// Garrisons and militia are exactly the leaderless parties step 1 walks.</item>
/// <item><c>BasicCharacterObject.GetSkillValue:292-295</c> is
/// <c>DefaultCharacterSkills.Skills.GetPropertyValue(skill)</c> with no guard. It inlines, which is
/// why the report names the <c>CharacterObject.GetSkillValue</c> frame above it.</item>
/// </list>
///
/// <para><b>Why the fields are null, proven rather than assumed.</b> A save-restored object is built
/// by <c>FormatterServices.GetUninitializedObject</c> (no ctor, no field initializer) and
/// self-registers via <c>MBObjectBase</c>'s <c>[LoadInitializationCallback]</c>; ModuleData XML then
/// upgrades those registered instances IN PLACE through
/// <c>MBObjectManager.CreateObjectFromXmlNode</c>'s <c>GetPresumedObject</c>. A character whose id
/// was removed from ModuleData is never reached by that second pass. The bundle's own timestamps
/// show the two passes eight seconds apart.</para>
///
/// <para><b>Why the repair covers four fields, not one.</b> <c>CharacterObject</c> persists exactly
/// two fields, so the stale object is null in several places at once and the skills NRE is merely
/// the first one the load path happens to hit. Repairing only that converts a deterministic
/// load-time crash into a load that succeeds and then crashes in the party screen
/// (<c>PartyCharacterVM.cs:1113</c>, null <c>UpgradeTargets</c>) or on agent spawn
/// (<c>BasicCharacterObject.cs:221</c>, null <c>BodyPropertyRange</c>) with a stack that names
/// nothing about save staleness. The adapter fills every field that has an unguarded dereference.</para>
///
/// <para><b>What this deliberately does NOT fix.</b> <c>_culture</c> stays null (inventing one is a
/// silent gameplay lie; a null surfaces through TAOM's own adapter chokepoints) and <c>Level</c>
/// stays at the 1 that <c>Init()</c> set, so the troop's tier and power are wrong. The character is
/// made INERT, not correct. The real fix is the data: the ids are logged and shown in game so the
/// player knows before they overwrite their last clean save.</para>
///
/// <para><b>Why a repair rather than a guard at each read.</b> The read sites are per-agent
/// per-hit combat paths and per-frame UI paths; guarding them all would tax the hottest code in the
/// game for a load-time data defect, and each new consumer would need its own guard. Making the
/// object well-formed fixes every consumer at once and costs one sweep per save load.</para>
/// </summary>
public class StaleCharacterRepairService : IStaleCharacterRepairService
{
    // A save that lost a whole culture's troops could produce hundreds of ids. Naming a bounded
    // sample keeps the line readable while the count carries the true scale.
    internal const int MaxNamedIds = 20;

    private readonly IStaleCharacterAdapter _adapter;
    private readonly IModLogger _logger;

    public StaleCharacterRepairService(IStaleCharacterAdapter adapter, IModLogger logger)
    {
        _adapter = adapter;
        _logger = logger;
    }

    public int RepairStaleCharacters()
    {
        IReadOnlyList<string> stale;
        try { stale = _adapter.FindStaleCharacters(); }
        catch (Exception ex)
        {
            SafeWarn($"scan failed: {ex.GetType().Name}: {ex.Message}");
            return 0;
        }

        // The healthy case, and it must stay completely silent: this runs on every save load, and
        // a line saying "repaired 0" on every launch trains the reader to skip the one that matters.
        if (stale == null || stale.Count == 0) return 0;

        var repaired = new List<string>();
        var dangerous = new List<string>();
        var bindingLost = false;

        for (var i = 0; i < stale.Count; i++)
        {
            var id = stale[i];
            StubRepairOutcome outcome;
            try { outcome = _adapter.TryMakeInert(id); }
            catch (Exception ex)
            {
                outcome = StubRepairOutcome.Failed;
                SafeWarn($"repair of '{id}' threw {ex.GetType().Name}: {ex.Message}");
            }

            switch (outcome)
            {
                case StubRepairOutcome.Repaired:
                    repaired.Add(id);
                    break;
                case StubRepairOutcome.AlreadyHealthy:
                    // Benign: something filled the fields between the scan and the write. Counting
                    // this as a failure would tell the player a safe character can still crash them.
                    break;
                case StubRepairOutcome.BindingUnavailable:
                    bindingLost = true;
                    break;
                default:
                    dangerous.Add(id);
                    break;
            }
        }

        Report(repaired, dangerous, bindingLost);
        // The log alone is not enough here. The stale ids ride into every future save while the
        // repair does not, so a player who never opens the log will overwrite their last
        // recoverable file without ever being told the save is damaged.
        if (repaired.Count > 0)
        {
            try { _adapter.ShowNoticeOnSessionStart(repaired.Count); }
            catch (Exception ex) { SafeWarn($"could not queue the player notice: {ex.Message}"); }
        }
        return repaired.Count;
    }

    private void Report(List<string> repaired, List<string> dangerous, bool bindingLost)
    {
        // Reported once for the whole sweep, and it is a code problem rather than a save problem:
        // without this, an engine rename makes every character fail and the log blames the player's
        // data while naming nothing that would let anyone find the real cause.
        if (bindingLost)
        {
            SafeWarn(
                "an engine member this repair reflects did not resolve, so NO stale character " +
                "could be repaired this session. This is an ENGINE BINDING problem, not a save " +
                "problem: run the Patch83 binding tests against the installed game version.");
        }

        if (repaired.Count > 0)
        {
            SafeWarn(
                $"made {repaired.Count} stale character(s) inert. Each was restored from the save " +
                $"under an id that current ModuleData no longer defines, so several of its fields " +
                $"were null and the engine dereferences them unguarded (crash bundle 065939b6). " +
                $"This is a DATA problem and the save still carries these ids: they are written " +
                $"back into every future save, while the repair is not. {Describe(repaired)}");
        }

        if (dangerous.Count > 0)
        {
            SafeWarn(
                $"could NOT repair {dangerous.Count} stale character(s). A skill read, a party " +
                $"screen or an agent spawn touching one of these can still crash the campaign. " +
                $"{Describe(dangerous)}");
        }
    }

    // Pure seam: the naming policy is what a future reader will want to change, and it is the part
    // that would otherwise only be exercised by a save with hundreds of stale troops.
    internal static string Describe(IReadOnlyList<string> ids)
    {
        if (ids == null || ids.Count == 0) return "(none)";
        var named = string.Join(", ", ids.Take(MaxNamedIds).Select(id => string.IsNullOrEmpty(id) ? "(blank id)" : id));
        return ids.Count <= MaxNamedIds
            ? $"Ids: {named}"
            : $"First {MaxNamedIds} ids: {named} (and {ids.Count - MaxNamedIds} more)";
    }

    private void SafeWarn(string message)
    {
        try { _logger?.LogWarning($"[StaleCharacterRepair] {message}"); }
        catch { /* a load-path repair must never fail over its own logging */ }
    }
}
