using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;
using TAOM.Features.CultureConversion.GarrisonSwap.Domain;
using TAOM.Features.TroopProgression;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="IGarrisonCultureSwapAdapter"/>.
///
/// Three engine behaviours drive the shape of this class:
///
/// 1. <c>TroopRoster.GetTroopRoster()</c> hands back the roster's LIVE cached list, rebuilt only on
///    the next call. A <c>foreach</c> that writes therefore keeps enumerating pre-mutation copies,
///    silently, and throws only if something re-enters <c>GetTroopRoster()</c> mid-loop. Both the
///    snapshot and the apply use index loops over <c>GetElementCopyAtIndex</c> and
///    <c>AddToCountsAtIndex</c>, which is the idiom <c>Settlement.RemoveMilitiasFromParty</c> uses
///    against these very rosters.
/// 2. <c>TroopRoster.RemoveTroop</c> passes a not-found index (-1) straight through to the backing
///    array, so it throws for an absent troop. Nothing here calls it; removals go through
///    <c>AddToCountsAtIndex</c> with <c>removeDepleted: false</c> plus one
///    <c>RemoveZeroCounts()</c> at the end, so indices never shift mid-loop.
/// 3. <c>TroopRosterElement.Number</c>'s setter throws <c>MBUnderFlowException</c> on a negative
///    value and <c>AddToCountsAtIndex</c> assigns straight into it, so clamping the swap count to
///    what the roster actually holds is what prevents a throw, not defensive decoration.
/// </summary>
public class GarrisonCultureSwapAdapter : IGarrisonCultureSwapAdapter
{
    private static readonly IReadOnlyList<GarrisonTroopInfo> NoTroops = new GarrisonTroopInfo[0];

    private readonly IVolunteerRecruitmentService _recruitment;
    private readonly IModLogger _logger;

    // Derived from recruitment pools + troop XML (ids, tiers, roles) so the values are identical
    // across campaigns, but rebuilt when the Campaign changes anyway: CharacterObject.Tier routes
    // through Campaign.Current.Models.CharacterStatsModel, and a cache built against a torn-down
    // campaign is the stale-singleton trap the architecture rules call out.
    //
    // WeakReference, not a strong field: Campaign.OnDestroy nulls Campaign.Current, after which a
    // process-lifetime singleton holding the finished campaign would be the last root for its whole
    // object graph (every settlement, hero, party and roster) until the next conversion happened to
    // rebuild. MapReachAdapter carries the same note for the same reason.
    private readonly WeakReference _cacheCampaign = new WeakReference(null);
    private Dictionary<string, CultureTroopIndex>? _indexByCulture;
    private Dictionary<string, CultureMilitiaTroops>? _militiaByCulture;

    public GarrisonCultureSwapAdapter(IVolunteerRecruitmentService recruitment, IModLogger logger)
    {
        _recruitment = recruitment;
        _logger = logger;
    }

    public IReadOnlyList<GarrisonTroopInfo> GetGarrisonRoster(string settlementId)
        => Snapshot(Settlement.Find(settlementId)?.Town?.GarrisonParty?.MemberRoster);

    public IReadOnlyList<GarrisonTroopInfo> GetMilitiaRoster(string settlementId)
        => Snapshot(Settlement.Find(settlementId)?.MilitiaPartyComponent?.MobileParty?.MemberRoster);

    public CultureTroopIndex? GetCultureTroopIndex(string cultureId)
    {
        if (string.IsNullOrEmpty(cultureId))
            return null;
        EnsureCaches();
        return _indexByCulture != null && _indexByCulture.TryGetValue(cultureId, out var index) ? index : null;
    }

    public CultureMilitiaTroops? GetCultureMilitiaTroops(string cultureId)
    {
        if (string.IsNullOrEmpty(cultureId))
            return null;
        EnsureCaches();
        return _militiaByCulture != null && _militiaByCulture.TryGetValue(cultureId, out var militia) ? militia : null;
    }

    public int ApplyGarrisonSwaps(string settlementId, IReadOnlyList<TroopSwap> swaps)
        => ApplySwaps(Settlement.Find(settlementId)?.Town?.GarrisonParty?.MemberRoster, swaps, settlementId, "garrison");

    public int ApplyMilitiaSwaps(string settlementId, IReadOnlyList<TroopSwap> swaps)
        => ApplySwaps(Settlement.Find(settlementId)?.MilitiaPartyComponent?.MobileParty?.MemberRoster, swaps, settlementId, "militia");

    public void NotifyPlayerTroopsSwapped(string settlementId, int troopCount)
    {
        try
        {
            var settlement = Settlement.Find(settlementId);
            var text = new TextObject("{=taom_garrison_culture_swap}{COUNT} troops at {SETTLEMENT} have been replaced by soldiers of its new culture.");
            text.SetTextVariable("COUNT", troopCount);
            text.SetTextVariable("SETTLEMENT", settlement?.Name ?? new TextObject(settlementId));
            MBInformationManager.AddQuickInformation(text, extraTimeInMs: 3000);
        }
        catch (Exception ex)
        {
            // A notice that fails must never abort the conversion that produced it.
            _logger.LogWarning($"GarrisonCultureSwapAdapter: could not show the swap notice for '{settlementId}': {ex.Message}");
        }
    }

    // --- snapshot / apply ---

    private static IReadOnlyList<GarrisonTroopInfo> Snapshot(TroopRoster? roster)
    {
        if (roster == null || roster.Count == 0)
            return NoTroops;

        var result = new List<GarrisonTroopInfo>(roster.Count);
        for (var i = 0; i < roster.Count; i++)
        {
            var element = roster.GetElementCopyAtIndex(i);
            var character = element.Character;
            if (character == null || string.IsNullOrEmpty(character.StringId))
                continue;

            result.Add(new GarrisonTroopInfo(
                character.StringId,
                character.Culture?.StringId,
                SafeTier(character),
                MapRole(character),
                element.Number,
                element.WoundedNumber,
                character.IsHero));
        }
        return result;
    }

    private int ApplySwaps(TroopRoster? roster, IReadOnlyList<TroopSwap> swaps, string settlementId, string rosterName)
    {
        if (roster == null || swaps == null || swaps.Count == 0)
            return 0;

        var replaced = 0;
        try
        {
            foreach (var swap in swaps)
            {
                if (swap == null || string.Equals(swap.OldTroopId, swap.NewTroopId, StringComparison.Ordinal))
                    continue;

                // TroopSwap validates nothing and Math.Min below only clamps downward, so a
                // non-positive count would turn the removal into an addition of old-culture troops.
                // The mapper cannot produce one; this is the public surface's own guard.
                if (swap.Count <= 0)
                    continue;

                var oldCharacter = ResolveCharacter(swap.OldTroopId);
                var newCharacter = ResolveCharacter(swap.NewTroopId);
                if (oldCharacter == null || newCharacter == null)
                    continue;

                // Re-find every iteration: an earlier swap in this loop may have shifted indices.
                var index = roster.FindIndexOfTroop(oldCharacter);
                if (index < 0)
                    continue;

                var present = roster.GetElementNumber(index);
                if (present <= 0)
                    continue;

                // Never take more than the roster actually holds — the plan was built from a
                // snapshot, and a concurrent daily tick may have trimmed the stack since.
                var count = Math.Min(swap.Count, present);
                var woundedPresent = roster.GetElementWoundedNumber(index);
                var wounded = Math.Min(swap.WoundedCount, Math.Min(count, woundedPresent));

                // Add BEFORE remove: a throw between the two would otherwise leave the roster
                // short by `count`. AddToCounts with index -1 appends, so it cannot shift `index`.
                roster.AddToCounts(newCharacter, count, insertAtFront: false, woundedCount: wounded);
                // removeDepleted:false keeps indices stable for the rest of the loop; one
                // RemoveZeroCounts below does the compaction.
                roster.AddToCountsAtIndex(index, -count, -wounded, 0, removeDepleted: false);
                replaced += count;
            }

        }
        catch (Exception ex)
        {
            // Partial application is survivable: each swap adds before it removes, so a throw
            // leaves the roster equal or over, never short. An exception escaping into the daily
            // tick is not survivable, hence the catch.
            _logger.LogError($"GarrisonCultureSwapAdapter: applying {rosterName} swaps for '{settlementId}' failed after {replaced} troops: {ex.Message}");
        }
        finally
        {
            // Compaction must happen even when the loop threw, or the roster keeps zero-count rows
            // until some later engine mutation tidies them.
            try { roster.RemoveZeroCounts(); } catch { /* nothing useful left to do */ }
        }
        return replaced;
    }

    // --- culture caches ---

    /// <summary>
    /// Builds, once per campaign, each culture's militia slots and its garrison candidate index.
    ///
    /// The candidates are the upgrade-closure of the culture's RECRUITMENT POOL, not the troops
    /// tagged with that culture. Grouping by <c>CharacterObject.Culture</c> looks equivalent and is
    /// not, for two reasons measured against the shipped data on 2026-09-21:
    ///
    ///   * Several cultures deliberately field another's line. Lothlorien recruits Rivendell troops,
    ///     Khand (<c>battania</c>) the Rhun line, Shaghana and Abanissa the Harad line. Their own
    ///     culture tag carries almost nothing: Lothlorien's ONLY non-hero Soldier is
    ///     <c>gear_practice_dummy_lothlorien</c>, so a culture-tag index re-manned a whole captured
    ///     city with Practice Dummies, and Khand's six were all guards and practice dummies.
    ///   * <c>CharacterObject.All</c> is every loaded module's characters, so a culture-tag index
    ///     also swept in 73 arena dummies, settlement guards, caravan guards and the spider creature,
    ///     plus vanilla Calradian troops for the six culture ids TAOM retags.
    ///
    /// The pool is also the same authority <c>CultureConversionService</c> gates conversion on
    /// (<c>HasCulturePool</c>), so the set of cultures with an index and the set of cultures a fief
    /// can convert TO are the same set by construction, rather than by coincidence.
    /// </summary>
    private void EnsureCaches()
    {
        var campaign = Campaign.Current;
        if (_indexByCulture != null && ReferenceEquals(campaign, _cacheCampaign.Target))
            return;

        var militiaByCulture = new Dictionary<string, CultureMilitiaTroops>(StringComparer.Ordinal);
        var indexByCulture = new Dictionary<string, CultureTroopIndex>(StringComparer.Ordinal);
        var militiaTroopIds = new HashSet<string>(StringComparer.Ordinal);
        var failed = false;

        try
        {
            var cultures = MBObjectManager.Instance?.GetObjectTypeList<CultureObject>();
            if (cultures == null)
            {
                // Same outcome the catch below guards against, reached without throwing: no militia
                // ids means the index is built with no militia exclusion, and stamping it would
                // freeze that for the campaign. Treat it as a failure so the next conversion retries.
                failed = true;
                _logger.LogWarning("GarrisonCultureSwapAdapter: culture list unavailable, will retry");
            }
            else
            {
                foreach (var culture in cultures)
                {
                    if (culture == null || string.IsNullOrEmpty(culture.StringId))
                        continue;

                    var militia = new CultureMilitiaTroops(
                        culture.StringId,
                        culture.MeleeMilitiaTroop?.StringId,
                        culture.MeleeEliteMilitiaTroop?.StringId,
                        culture.RangedMilitiaTroop?.StringId,
                        culture.RangedEliteMilitiaTroop?.StringId);
                    militiaByCulture[culture.StringId] = militia;

                    // Militia troops are the militia party's job. Keeping them out of the garrison
                    // index stops a converted town being re-manned with militia-grade bodies.
                    foreach (MilitiaSlot slot in Enum.GetValues(typeof(MilitiaSlot)))
                    {
                        var id = militia.TroopFor(slot);
                        if (!string.IsNullOrEmpty(id))
                            militiaTroopIds.Add(id!);
                    }
                }
            }

            foreach (var cultureId in _recruitment.GetPooledCultureIds())
            {
                if (string.IsNullOrEmpty(cultureId))
                    continue;
                indexByCulture[cultureId] = new CultureTroopIndex(
                    cultureId, BuildCandidates(cultureId, militiaTroopIds));
            }
        }
        catch (Exception ex)
        {
            // Leave the campaign stamp unset below so the next conversion retries. A half-built
            // cache is fail-safe (a missing culture means "no swap"), but latching it would turn one
            // transient failure into the feature being silently off for the rest of the campaign.
            failed = true;
            _logger.LogWarning($"GarrisonCultureSwapAdapter: building the culture troop index failed, will retry: {ex.Message}");
        }

        _indexByCulture = indexByCulture;
        _militiaByCulture = militiaByCulture;
        _cacheCampaign.Target = failed ? null : campaign;

        if (!failed)
            _logger.LogDebug($"GarrisonCultureSwapAdapter: indexed {indexByCulture.Count} recruitable cultures, militia slots for {militiaByCulture.Count}");
    }

    /// <summary>
    /// Walks the culture's pool roots through <c>CharacterObject.UpgradeTargets</c> to reach its
    /// whole line, keeping regular Soldier troops and dropping militia. Breadth-first over a visited
    /// set, because TAOM upgrade graphs branch and rejoin.
    /// </summary>
    private List<CultureTroopCandidate> BuildCandidates(string cultureId, HashSet<string> militiaTroopIds)
    {
        var candidates = new List<CultureTroopCandidate>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<CharacterObject>();

        foreach (var rootId in _recruitment.GetCulturePoolTroopIds(cultureId))
        {
            var root = ResolveCharacter(rootId);
            if (root != null && visited.Add(root.StringId))
                pending.Enqueue(root);
        }

        while (pending.Count > 0)
        {
            var troop = pending.Dequeue();

            // Regular line troops only: heroes are not roster fodder, and a militia troop belongs to
            // the militia party rather than the wall.
            if (!troop.IsHero && troop.IsRegular && troop.Occupation == Occupation.Soldier
                && !militiaTroopIds.Contains(troop.StringId))
            {
                candidates.Add(new CultureTroopCandidate(troop.StringId, SafeTier(troop), MapRole(troop)));
            }

            var upgrades = troop.UpgradeTargets;
            if (upgrades == null)
                continue;
            foreach (var next in upgrades)
            {
                if (next != null && !string.IsNullOrEmpty(next.StringId) && visited.Add(next.StringId))
                    pending.Enqueue(next);
            }
        }

        return candidates;
    }

    private static CharacterObject? ResolveCharacter(string troopId)
        => string.IsNullOrEmpty(troopId) ? null : MBObjectManager.Instance?.GetObject<CharacterObject>(troopId);

    /// <summary>
    /// <c>CharacterObject.Tier</c> is computed through <c>Campaign.Current.Models.CharacterStatsModel</c>,
    /// so it dereferences the live campaign and can throw rather than return a default. Tier 0 is the
    /// safe answer: it still matches, it just matches low.
    /// </summary>
    private static int SafeTier(CharacterObject troop)
    {
        try
        {
            return troop.Tier;
        }
        catch
        {
            return 0;
        }
    }

    private static TroopRole MapRole(CharacterObject troop)
    {
        try
        {
            return troop.DefaultFormationClass switch
            {
                FormationClass.Infantry => TroopRole.Infantry,
                FormationClass.Ranged => TroopRole.Ranged,
                FormationClass.Cavalry => TroopRole.Cavalry,
                FormationClass.HorseArcher => TroopRole.HorseArcher,
                _ => TroopRole.Unknown,
            };
        }
        catch
        {
            return TroopRole.Unknown;
        }
    }
}
