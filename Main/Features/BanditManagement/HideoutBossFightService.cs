using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.BanditManagement;

/// <inheritdoc cref="IHideoutBossFightService"/>
public sealed class HideoutBossFightService : IHideoutBossFightService
{
    private const int MinBodyguards = 0;
    private const int MaxBodyguards = 10;

    private readonly IBanditScalingSettingsProvider _settings;

    public HideoutBossFightService(IBanditScalingSettingsProvider settings)
    {
        _settings = settings;
    }

    public int BodyguardCount =>
        Math.Max(MinBodyguards, Math.Min(MaxBodyguards, _settings.HideoutBossBodyguards));

    public int BossPhaseTroopCap => 1 + BodyguardCount;

    public HideoutAssaultSplit PlanAssault(IReadOnlyList<HideoutTroopCandidate> troops)
    {
        var heroOrBossCount = 0;
        var regulars = new List<int>();
        for (var i = 0; i < troops.Count; i++)
        {
            var troop = troops[i];
            if (troop.IsWounded) continue;
            if (troop.IsHeroOrBoss) heroOrBossCount++;
            else regulars.Add(i);
        }

        var healthy = heroOrBossCount + regulars.Count;
        if (healthy == 0)
            return new HideoutAssaultSplit(0, 0, Array.Empty<int>());

        // Vanilla asserts phase 2 < total (HideoutMissionController.InitializeMission), and
        // SelectBossAgent derefs its pick, so the boss phase sits in [1, healthy - 1] whenever
        // there are two or more troops. A lone troop is the boss phase, as in vanilla.
        var bossPhase = healthy <= 1
            ? healthy
            : Math.Max(1, Math.Min(healthy - 1, heroOrBossCount + BodyguardCount));
        var bodyguards = Math.Max(0, bossPhase - heroOrBossCount);

        // OrderByDescending is stable, like vanilla's, so ties keep roster order.
        var heldBack = new HashSet<int>(regulars
            .OrderByDescending(i => troops[i].Level)
            .Take(bodyguards));
        var firstPhase = regulars.Where(i => !heldBack.Contains(i)).ToList();

        return new HideoutAssaultSplit(healthy - bossPhase, bossPhase, firstPhase);
    }

    public HideoutAmbushSelection PlanAmbush(IReadOnlyList<int> unspawnedLevels, bool hasBossOrigin, bool canPad)
    {
        var wanted = BodyguardCount;
        if (!hasBossOrigin && wanted == 0) wanted = 1;

        var keep = Enumerable.Range(0, unspawnedLevels.Count)
            .OrderByDescending(i => unspawnedLevels[i])
            .Take(wanted)
            .OrderBy(i => i)
            .ToList();

        var spawnCount = canPad ? wanted : Math.Min(wanted, keep.Count);
        return new HideoutAmbushSelection(spawnCount, keep);
    }
}
