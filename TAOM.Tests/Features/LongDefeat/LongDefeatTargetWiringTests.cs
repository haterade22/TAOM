using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The lines that carry the troop and class boons into the engine and that no unit test can run without a mission (phase2-plan.md Slice 6): the damage model's reduction side, the
/// launcher's troop groups, the director's order of publish, refresh and spawn, and the new body's refresh and health. Read off the source, so deleting one fails here.
/// </summary>
[TestClass]
public class LongDefeatTargetWiringTests
{
    private static string BodyOf(string source, string marker)
    {
        var at = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, marker + " not found");
        var next = source.IndexOf("\n    public ", at + marker.Length, StringComparison.Ordinal);
        var nextPrivate = source.IndexOf("\n    private ", at + marker.Length, StringComparison.Ordinal);
        var end = new[] { next, nextPrivate }.Where(i => i > 0).DefaultIfEmpty(source.Length).Min();
        return source.Substring(at, end - at);
    }

    [TestMethod]
    public void TheCustomBattleDamageModel_ReducesThroughTheLongDefeatHook_AfterFordsOfWar()
    {
        var source = RepoPaths.ReadSource("Main/Features/CombatMechanics/Models/TaomCustomBattleDamageModel.cs", stripComments: true);
        var reductions = BodyOf(source, "public override float ApplyDamageReductions(");

        var fords = reductions.IndexOf("FordsUpgradeHooks.Reduce(", StringComparison.Ordinal);
        var longDefeat = reductions.IndexOf("LongDefeatCombatHooks.Reduce(", StringComparison.Ordinal);
        Assert.IsTrue(fords >= 0 && longDefeat >= 0 && longDefeat < fords, "the Long Defeat call wraps the Fords call, so it scales what the army upgrades left");
    }

    [TestMethod]
    public void TheLauncher_ReadsTheTroopGroupsFromTheEngine_IntoThePlanItRuns()
    {
        var source = RepoPaths.ReadSource("Main/Features/LongDefeat/Hooks/LongDefeatMissionLauncher.cs", stripComments: true);

        StringAssert.Contains(source, "TroopGroups = LongDefeatTroopGroups.Read(plan)");
        var groups = RepoPaths.ReadSource("Main/Features/LongDefeat/Hooks/LongDefeatTroopGroups.cs", stripComments: true);
        StringAssert.Contains(groups, "BoonTargets.OfCharacter(character.IsMounted, character.IsRanged)");
    }

    [TestMethod]
    public void TheDirector_PublishesAndAppliesTheBoonBeforeAnySoldierSpawns()
    {
        var source = RepoPaths.ReadSource("Main/Features/LongDefeat/LongDefeatDirector.Effects.cs", stripComments: true);
        var apply = BodyOf(source, "private void Apply(RunEffects fx)");

        var publish = apply.IndexOf("PublishStats(", StringComparison.Ordinal);
        var boon = apply.IndexOf("ApplyBoonToStanding(", StringComparison.Ordinal);
        var spawn = apply.IndexOf("SpawnBatch(", StringComparison.Ordinal);
        Assert.IsTrue(publish >= 0 && boon > publish && spawn > boon, "the refresh reads the table just published, and a soldier spawned this tick is built after both");
    }

    [TestMethod]
    public void TheDirector_RefreshesTheNewBodyAndSetsThePlayersHealth_WhenHeIsAdopted()
    {
        var source = RepoPaths.ReadSource("Main/Features/LongDefeat/LongDefeatDirector.cs", stripComments: true);
        var adopt = BodyOf(source, "private void AdoptIfNew()");
        var body = RepoPaths.ReadSource("Main/Features/LongDefeat/LongDefeatDirector.TargetBoons.cs", stripComments: true);
        var dress = BodyOf(body, "private void DressNewBody()");

        Assert.IsTrue(adopt.IndexOf("_field.AdoptMain(", StringComparison.Ordinal) < adopt.IndexOf("DressNewBody()", StringComparison.Ordinal));
        StringAssert.Contains(dress, "_effects.RefreshStats(BoonTarget.Player)");
        StringAssert.Contains(dress, "_effects.SetPlayerHealth(_run.HealthFactor(BoonTarget.Player))");
    }

    [TestMethod]
    public void TheEffectsAdapter_RefreshesTheAgentAndItsMount_AndKeepsTheHealthShareWhenItRescales()
    {
        var source = RepoPaths.ReadSource("Main/Adapters/LongDefeatEffectsAdapter.cs", stripComments: true);
        var refresh = BodyOf(source, "public LdTouched RefreshStats(");
        var rescale = BodyOf(source, "private static void Rescale(");

        StringAssert.Contains(refresh, "agent.UpdateAgentProperties();");
        StringAssert.Contains(refresh, "agent.MountAgent?.UpdateAgentProperties();");
        StringAssert.Contains(rescale, "agent.Health / agent.HealthLimit");
        StringAssert.Contains(rescale, "agent.BaseHealthLimit = limit;");
        StringAssert.Contains(rescale, "agent.HealthLimit = limit;");
        StringAssert.Contains(rescale, "agent.Health = share * limit;");
    }

    [TestMethod]
    public void TheEffectsAdapter_ReadsAHorsesMountSpeedFromTheHorse_AgainstTheModelsAnswerForIt()
    {
        var source = RepoPaths.ReadSource("Main/Adapters/LongDefeatEffectsAdapter.cs", stripComments: true);
        var readings = BodyOf(source, "private static List<LdStatReading> Readings(");
        var mount = BodyOf(source, "private static LdStatReading MountReading(");

        StringAssert.Contains(readings, "agent.MountAgent");
        StringAssert.Contains(readings, "MountReading(");
        StringAssert.Contains(mount, "model.UpdateAgentStats(mount, scratch)");
        StringAssert.Contains(mount, "mount.GetAgentDrivenPropertyValue(DrivenProperty.MountSpeed)");
        StringAssert.Contains(mount, "LongDefeatStatHooks.Baseline(");
    }

    [TestMethod]
    public void TheEffectsAdapter_ScalesRelatively_AndSetsThePlayerFromTheCharacterMaximum()
    {
        var source = RepoPaths.ReadSource("Main/Adapters/LongDefeatEffectsAdapter.cs", stripComments: true);

        StringAssert.Contains(BodyOf(source, "public LdTouched ScaleHealth("), "Rescale(agent, agent.BaseHealthLimit * factor)");
        StringAssert.Contains(BodyOf(source, "public void SetPlayerHealth("), "Rescale(main, main.Character.MaxHitPoints() * factor)");
    }
}
