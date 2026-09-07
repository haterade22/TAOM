using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.CharacterSkillsRepair;

/// <summary>
/// Save-load repair for a character with no skill set.
///
/// <para><b>The failure chain</b> (crash bundle 065939b6, 2026-09-05, engine v1.4.8, each link read
/// from the decompiled source):</para>
/// <list type="number">
/// <item><c>Campaign.OnGameLoaded</c> :688 calls <c>CampaignObjectManager.AfterLoad()</c>, which
/// runs <c>Clan.AfterLoad</c> -> <c>UpdateCurrentStrength</c> -> <c>PartyBase.EstimatedStrength</c>
/// -> <c>DefaultMilitaryPowerModel.GetPowerOfParty</c> -> <c>MobileParty.Morale</c> -> the
/// registered <c>PartyMoraleModel</c>.</item>
/// <item><c>DefaultPartyMoraleModel.GetMoraleEffectsFromSkill</c> :206-213 resolves the character
/// through <c>SkillHelper.GetEffectivePartyLeaderForSkill</c> and null-checks it, so the character
/// is NOT null.</item>
/// <item><c>SkillHelper.GetEffectivePartyLeaderForSkill</c> :78-94 returns
/// <c>party.MemberRoster.GetCharacterAtIndex(0)</c> for a party with no leader hero — a plain
/// TROOP, not a hero. Garrisons and militia are exactly the leaderless parties
/// <c>Clan.UpdateCurrentStrength</c> walks.</item>
/// <item><c>CharacterObject.GetSkillValue</c> :791-798 routes a non-hero to
/// <c>BasicCharacterObject.GetSkillValue</c> :292-295, which is
/// <c>DefaultCharacterSkills.Skills.GetPropertyValue(skill)</c> with no guard. That one-liner
/// inlines, which is why the crash report names the <c>CharacterObject</c> frame above it.</item>
/// </list>
///
/// <para><b>Why the field can be null.</b> <c>MBCharacterSkills.Skills</c> is assigned in its
/// constructor, so it is never null on a live object, and <c>BasicCharacterObject.Deserialize</c>
/// :337-345 always assigns <c>DefaultCharacterSkills</c> (the referenced <c>skill_template</c>, or
/// a fresh one). A character that came from module XML is therefore safe. The null belongs to a
/// character restored from a SAVE whose XML definition no longer exists: its
/// <c>[LoadInitializationCallback]</c> runs <c>CharacterObject.Init()</c> :408-414, which never
/// touches the field. Renaming or removing a troop between mod versions is enough to produce one.</para>
///
/// <para><b>Why a repair rather than a guard at the read.</b> The read site is
/// <c>GetSkillValue</c>, which the engine calls per agent per hit in combat — a Harmony prefix
/// there would tax the hottest path in the game to fix a load-time data defect. And the morale
/// model is only the path that happened to crash first: <c>SkillHelper.AddSkillBonusForCharacter</c>
/// and <c>AddSkillBonusForTown</c> reach the same unguarded line, so a guard in one game model
/// would leave the rest exposed. Making the character well-formed fixes every consumer at once and
/// costs one sweep per load.</para>
///
/// <para>The repair is idempotent and touches only broken objects, so it is safe on the load path
/// in the sense <c>csharp-architecture.md</c> means: it cannot corrupt an entity that was already
/// healthy, because a healthy entity fails the null test and is skipped.</para>
/// </summary>
public class CharacterSkillsRepairService : ICharacterSkillsRepairService
{
    // A save that lost a whole culture's troops could produce hundreds of ids. Naming a bounded
    // sample keeps the line readable while the count carries the true scale — the same shape
    // Patch65's report uses.
    internal const int MaxNamedIds = 20;

    private readonly ICharacterSkillsAdapter _adapter;
    private readonly IModLogger _logger;

    public CharacterSkillsRepairService(ICharacterSkillsAdapter adapter, IModLogger logger)
    {
        _adapter = adapter;
        _logger = logger;
    }

    public int RepairMissingSkillSets()
    {
        IReadOnlyList<string> broken;
        try { broken = _adapter.FindCharactersWithNoSkillSet(); }
        catch (Exception ex)
        {
            SafeWarn($"scan failed: {ex.GetType().Name}: {ex.Message}");
            return 0;
        }

        // The healthy case, and it must stay completely silent: this runs on every load, and a
        // line saying "repaired 0" on every launch trains the reader to skip the one that matters.
        if (broken == null || broken.Count == 0) return 0;

        var repaired = new List<string>();
        var failed = new List<string>();
        foreach (var id in broken)
        {
            bool ok;
            try { ok = _adapter.TryGiveEmptySkillSet(id); }
            catch (Exception ex)
            {
                ok = false;
                SafeWarn($"repair of '{id}' threw {ex.GetType().Name}: {ex.Message}");
            }
            (ok ? repaired : failed).Add(id);
        }

        Report(repaired, failed);
        return repaired.Count;
    }

    private void Report(List<string> repaired, List<string> failed)
    {
        if (repaired.Count > 0)
        {
            SafeWarn(
                $"gave an empty skill set to {repaired.Count} character(s) that had none. " +
                $"Vanilla BasicCharacterObject.GetSkillValue derefs that field unguarded, so any " +
                $"skill read on one of these was a hard NRE (crash bundle 065939b6). This is a " +
                $"DATA problem: each id below is defined in the save but not in current " +
                $"ModuleData. {Describe(repaired)}");
        }

        if (failed.Count > 0)
        {
            SafeWarn(
                $"could NOT repair {failed.Count} character(s) with no skill set. A skill read " +
                $"on one of these can still crash the campaign. {Describe(failed)}");
        }
    }

    // Pure seam: the naming policy is what a future reader will want to change, and it is the
    // part that would otherwise only be exercised by a save with hundreds of broken troops.
    internal static string Describe(IReadOnlyList<string> ids)
    {
        if (ids == null || ids.Count == 0) return "(none)";
        var named = string.Join(", ", ids.Take(MaxNamedIds));
        return ids.Count <= MaxNamedIds
            ? $"Ids: {named}"
            : $"First {MaxNamedIds} ids: {named} (and {ids.Count - MaxNamedIds} more)";
    }

    private void SafeWarn(string message)
    {
        try { _logger?.LogWarning($"[CharacterSkillsRepair] {message}"); }
        catch { /* a load-path repair must never fail over its own logging */ }
    }
}
