using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The order and the wiring of the live gear swap and the bot's errand (phase4-plan.md Slice 5, spike S11) that the director tests over the fake field cannot see, pinned from source: the swap is the kit build,
/// the engine's own equipment swap, the mission's own stat model and an update, in that order, on the lord's own live body and with no health write; the errand walks only a live AI-driven main agent and releases only
/// the body it walked; the director serves both after the rise and the care and before the volleys, releases an open walk when the run ends, and every spawn of the lord takes the run's effective loadout.
/// </summary>
[TestClass]
public class LongDefeatArmouryPinTests
{
    private const string Armoury = "Main/Adapters/LongDefeatBattlefieldAdapter.Armoury.cs";
    private const string Effects = "Main/Features/LongDefeat/LongDefeatDirector.Effects.cs";
    private const string Director = "Main/Features/LongDefeat/LongDefeatDirector.cs";

    private static string Source(string path) => RepoPaths.ReadSource(path, stripComments: true).Replace("\r\n", "\n");

    private static string Body(string source, string marker)
    {
        var at = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, marker + " not found");
        var open = source.IndexOf('{', at);
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            depth += source[i] == '{' ? 1 : source[i] == '}' ? -1 : 0;
            if (depth == 0)
                return source.Substring(at, i - at + 1);
        }

        throw new AssertFailedException(marker + " has no closing brace");
    }

    private static int At(string body, string part)
    {
        var at = body.IndexOf(part, StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, $"'{part}' is not in:\n{body}");
        return at;
    }

    [TestMethod]
    public void TheSwap_IsBuildKitThenTheEnginesSwapThenTheModelsInitThenAnUpdate()
    {
        var swap = Body(Source(Armoury), "public LdSwap SwapGear(");

        var build = At(swap, "BuildKit(player, loadout)");
        var engine = At(swap, "lord.UpdateSpawnEquipmentAndRefreshVisuals(kit)");
        var init = At(swap, ".InitializeAgentStats(lord, lord.SpawnEquipment, lord.AgentDrivenProperties, null!)");
        var update = At(swap, "lord.UpdateAgentProperties()");
        var readBack = At(swap, "ReadBack(lord, loadout)");
        Assert.IsTrue(build < engine, "the kit is the spawn's own build, so a swapped body and a respawned one start alike");
        Assert.IsTrue(engine < init, "the model's init reads the new spawn equipment");
        Assert.IsTrue(init < update, "the update reads ArmorEncumbrance, which the init writes: the updates inside the engine's swap ran with the old weight");
        Assert.IsTrue(update < readBack, "the kit is read back from the body after every write");
        Assert.AreEqual(1, Regex.Matches(swap, @"UpdateSpawnEquipmentAndRefreshVisuals").Count, "one swap");
    }

    [TestMethod]
    public void TheSwap_ActsOnlyOnTheLordsOwnLiveBody_AndWritesNoHealth()
    {
        var swap = Body(Source(Armoury), "public LdSwap SwapGear(");

        var guard = At(swap, "ReferenceEquals(lord, _mission.MainAgent)");
        Assert.IsTrue(At(swap, "Live(lord)") < At(swap, "lord.UpdateSpawnEquipmentAndRefreshVisuals"), "a dead body is never written");
        Assert.IsTrue(guard < At(swap, "lord.UpdateSpawnEquipmentAndRefreshVisuals"), "only the lord's own body is dressed");
        StringAssert.Contains(swap, "_lordWatch.Lord as Agent");
        Assert.IsFalse(Regex.IsMatch(swap, @"\.Health\s*=[^=]"), "the swap writes no health");
        Assert.IsFalse(Regex.IsMatch(swap, @"HealthLimit\s*=[^=]"), "the swap writes no health limit");
        Assert.IsFalse(swap.Contains("MountAgent ="), "the swap never touches the horse");
    }

    [TestMethod]
    public void TheSwap_AsksTheMissionsOwnStatModel()
    {
        var source = Source(Armoury);

        Assert.IsTrue(Regex.IsMatch(source, @"MissionGameModels\.Current\??\.AgentStatCalculateModel"), "the model the mission runs, whichever game it is");
        Assert.IsFalse(Regex.IsMatch(source, @"new\s+\w*AgentStatCalculateModel|CustomBattleAgentStatCalculateModel|SandboxAgentStatCalculateModel"), "no concrete model type: the mode picks it");
    }

    [TestMethod]
    public void TheErrand_WalksOnlyALiveAiDrivenMainAgent_AndReleasesOnlyTheBodyItWalked()
    {
        var source = Source(Armoury);
        var start = Body(source, "public LdErrandMove StartErrand(");
        var teleport = Body(source, "public LdErrandMove TeleportErrand(");
        var end = Body(source, "public LdErrandMove EndErrand(");

        Assert.IsTrue(At(start, "Live(main)") < At(start, "ScriptPosition(main, place)"), "a dead body is never given a walk");
        Assert.IsTrue(At(start, "IsAIControlled") < At(start, "ScriptPosition(main, place)"), "a person walks himself");
        Assert.IsTrue(At(start, "CanBeAssignedForScriptedMovement()") < At(start, "ScriptPosition(main, place)"), "the engine's answer is read before the walk is set");
        Assert.IsTrue(At(start, "ScriptPosition(main, place)") < At(start, "_errandBody = main"), "the body is held only once it walks");
        Assert.AreEqual(1, Regex.Matches(source, @"SetScriptedPosition\(").Count, "one place sets a walk");
        StringAssert.Contains(teleport, "ReferenceEquals(body, _mission.MainAgent)");
        Assert.IsTrue(At(teleport, "Live(body)") < At(teleport, "TeleportPlayer(place)"));
        Assert.IsTrue(At(teleport, "TeleportPlayer(place)") < At(teleport, "ScriptPosition(body, place)"), "the position is scripted again after the teleport, so the AI does not walk him back");
        Assert.IsTrue(At(end, "_errandBody = null") < At(end, "DisableScriptedMovement()"), "the body is dropped before the release");
        Assert.IsTrue(At(end, "Live(body)") < At(end, "DisableScriptedMovement()"), "only a live body is released");
        Assert.AreEqual(1, Regex.Matches(source, @"DisableScriptedMovement\(\)").Count, "one place releases a walk");
        Assert.IsFalse(Regex.IsMatch(end, @"_mission\.MainAgent"), "the release acts on the body held, never on whoever is the main agent now");
    }

    [TestMethod]
    public void TheErrandAndTheSwap_AreServedAfterTheRiseAndTheCare_BeforeTheVolleys()
    {
        var apply = Body(Source(Effects), "private void Apply(RunEffects fx)");

        var rise = At(apply, "RiseLord(rise)");
        var care = At(apply, "ApplyFieldCare(fx.FieldCare)");
        var errand = At(apply, "fx.Errand is { } errand");
        var gear = At(apply, "fx.SwapGear is { } gear");
        var volleys = At(apply, "FireVolleys(fx.FireVolleys)");
        Assert.IsTrue(rise < errand && care < errand, "the walk and the swap follow the rise and the care, which may spawn the lord");
        Assert.IsTrue(errand < gear, "the walk is released, then he is dressed");
        Assert.IsTrue(gear < volleys, "a volley reads the field the swap left");
    }

    [TestMethod]
    public void TheRunsEnd_ReleasesAnOpenWalk_FromFinishAndShutdown_BeforeTheFieldIsCleared()
    {
        var finish = Body(Source(Effects), "private void Finish()");
        var shutdown = Body(Source(Director), "public void Shutdown()");

        StringAssert.Contains(finish, "ReleaseErrandWalk(\"run-ended\")");
        Assert.IsTrue(At(shutdown, "ReleaseErrandWalk(\"run-ended\")") < At(shutdown, "_field.Clear()"), "the field's clear drops the held body, so the release must come first");
    }

    [TestMethod]
    public void EverySpawnOfTheLord_TakesTheRunsEffectiveLoadout()
    {
        var folder = RepoPaths.RepoPath("Main", "Features", "LongDefeat");
        foreach (var file in Directory.GetFiles(folder, "LongDefeatDirector*.cs"))
        {
            var text = RepoPaths.StripComments(File.ReadAllText(file));
            Assert.IsFalse(Regex.IsMatch(text, @"(SpawnPlayer|RiseLord|RemountPlayer)\([^;]*_plan\.Loadout"), $"{Path.GetFileName(file)} hands a spawn of the lord the class kit alone: a bought set is lost at the next body (D35)");
        }

        Assert.AreEqual(3, new[] { "LongDefeatDirector.Player.cs", "LongDefeatDirector.Lord.cs", "LongDefeatDirector.Sally.cs" }
            .Count(f => RepoPaths.ReadSource("Main/Features/LongDefeat/" + f, stripComments: true).Contains("_run.EffectiveLoadout")), "the spawn, the rise and the remount each read it");
        StringAssert.Contains(RepoPaths.ReadSource("Main/Features/LongDefeat/LongDefeatDirector.Player.cs", stripComments: true), "_field.SpawnPlayer(respawn, loadout,", "the spawn hands it on, not a null or the plan's kit");
        StringAssert.Contains(RepoPaths.ReadSource("Main/Features/LongDefeat/LongDefeatDirector.Lord.cs", stripComments: true), "_lord.RiseLord(place, loadout)", "the rise hands it on");
        StringAssert.Contains(RepoPaths.ReadSource("Main/Features/LongDefeat/LongDefeatDirector.Sally.cs", stripComments: true), "_field.RemountPlayer(point, loadout)", "the remount hands it on");
    }

    [TestMethod]
    public void TheArmouryAdapter_KeysNothingOnAnAgentIndex_AndReachesNoEngineCallback()
    {
        var source = Source(Armoury);

        Assert.IsFalse(Regex.IsMatch(source, @"\.Index\b"), "Agent.Index is handed to the next agent the engine builds");
        Assert.IsFalse(Regex.IsMatch(source, @"Dictionary<\s*int"), "no index-keyed store");
        Assert.IsFalse(Regex.IsMatch(source, @"OnAgent\w*|AddHandler|MissionBehavior|RunOrDefer"), "the swap and the walk run from the director's tick, never from a removal or hit callback");
        Assert.AreEqual(1, Regex.Matches(source, @"private\s+Agent\?\s+_\w+").Count, "the one held agent is the errand's body");
    }
}
