using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.DwarfAttacks;

// The dwarf attacks' actions live in LOTRLOME_Armory, which is UNVERSIONED: a reinstall drops them (#1087). These
// tests pin the data the C# relies on, in the repo's snapshot of the Armory (always) and in the live install
// (LiveInstall, inconclusive where it is not installed): the dwarf Monster names the set, each action is declared
// once as actt_kick, and as_dwarf_warrior binds each once to its own clip. tools/bind_dwarf_attack_actions.py
// writes both, and its --check reads the same four files.

namespace TAOM.Tests.Features.DwarfAttacks;

[TestClass]
public class DwarfAttacksWiringTests
{
    private static readonly (string Action, string Clip)[] Bindings =
    {
        (DwarfAttacksConfig.HeadButtActionName, "dwarf_head_butt"),
        (DwarfAttacksConfig.DropKickActionName, "dwarf_drop_kick"),
    };

    [TestMethod]
    public void Snapshot_DeclaresBothActionsOnce_AsKicks()
    {
        AssertDeclared(Path.Combine(Snapshot(), "action_types.xml"));
    }

    [TestMethod]
    public void Snapshot_BindsBothActionsOnce_InTheWarriorSet()
    {
        AssertBound(Path.Combine(Snapshot(), "action_sets.xml"));
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void LiveArmory_DeclaresAndBindsBothActions()
    {
        AssertDeclared(Path.Combine(ArmoryModuleData(), "action_types.xml"));
        AssertBound(Path.Combine(ArmoryModuleData(), "action_sets.xml"));
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void LiveArmory_DwarfMonster_NamesTheWarriorSet()
    {
        var dwarf = XDocument.Load(Path.Combine(ArmoryModuleData(), "monsters.xml")).Descendants("Monster")
            .SingleOrDefault(m => (string?)m.Attribute("id") == DwarfAttacksConfig.DwarfMonsterId);
        Assert.IsNotNull(dwarf, "the dwarf Monster is missing from the Armory's monsters.xml");
        Assert.AreEqual(DwarfAttacksConfig.ActionSetId, (string?)dwarf!.Attribute("action_set"));
    }

    [TestMethod]
    public void Feature_IsRegisteredAndAddedToMissions()
    {
        string root = RepoRoot();
        StringAssert.Contains(File.ReadAllText(Path.Combine(root, "Main", "IoC.cs")),
            "Features.DwarfAttacks.DwarfAttacksIoC.RegisterDwarfAttacksFeature(container);");
        StringAssert.Contains(File.ReadAllText(Path.Combine(root, "Main", "SubModule.cs")),
            "AddTaomBehavior(new Features.DwarfAttacks.DwarfAttacksMissionBehavior());");
    }

    // BehaviorTreeMissionLogic keeps ONE tree per agent, and every dwarf soldier carries the race-ability tree (Stand
    // Fast). A dwarf-attack tree attached as an agent component takes that slot, and RaceAbilityRuntime.CarriesTree
    // then skips the dwarf (deep review 2026-11-06, HIGH). The dwarf trees must be held and run by the feature itself.
    [TestMethod]
    public void Feature_NeverTakesTheAgentsTreeSlot()
    {
        string folder = Path.Combine(RepoRoot(), "Main", "Features", "DwarfAttacks");
        foreach (string file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
        {
            string code = string.Join("\n", File.ReadAllLines(file).Where(l => !l.TrimStart().StartsWith("//")));
            foreach (string banned in new[] { "new BehaviorTreeAgentComponent", "CreatureTreeTracker", "AddComponent(", "AddBehaviorTree(" })
                Assert.IsFalse(code.Contains(banned), $"{Path.GetFileName(file)} uses {banned}: it would take the race-ability tree's slot");
        }
    }

    // IsAiAgentDecorator reads only the controller, and the Battle Command seat, Fords of War and the Long Defeat hand
    // the player's hero to the AI. The MCM hint promises the player's own dwarf never attacks (deep review 2026-11-06).
    [TestMethod]
    public void ReadyDecorator_ExcludesThePlayersOwnDwarf()
    {
        string code = Code("BehaviorTreeElements", "DwarfAttackReadyDecorator.cs");
        StringAssert.Contains(code, "MayAttack(");
        StringAssert.Contains(code, ".IsPlayerTroop");
    }

    // In co-op every machine builds the dwarves' trees; only the agent's owner may run one, or a puppet's tree lands a
    // second blow (#967). The component that applied this gate is gone (DwarfTreeHost), so the host must keep it.
    [TestMethod]
    public void TreeHost_RunsOnlyTheTreesOwnedHere()
    {
        string code = Code("Hooks", "DwarfTreeHost.cs");
        StringAssert.Contains(code, "if (!CoopMissionOwnership.IsOwnedHere(agent)) continue;");
        Assert.IsTrue(code.IndexOf("IsOwnedHere", StringComparison.Ordinal) < code.IndexOf("tree.RunTree()", StringComparison.Ordinal),
            "the ownership gate must come before the tree runs");
    }

    // The behavior resolves the services once and hands them to every tree; a node never reaches the container.
    [TestMethod]
    public void TreeNodes_TakeTheirServicesFromTheBuild()
    {
        foreach (string file in Directory.GetFiles(Path.Combine(RepoRoot(), "Main", "Features", "DwarfAttacks", "BehaviorTreeElements"), "*.cs"))
            Assert.IsFalse(File.ReadAllText(file).Contains("IoC.Resolve"), $"{Path.GetFileName(file)} resolves a service itself");
    }

    private static string Code(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot(), "Main", "Features", "DwarfAttacks" }.Concat(parts).ToArray()));

    private static void AssertDeclared(string path)
    {
        var actions = XDocument.Load(path).Descendants("action").ToList();
        foreach (var (action, _) in Bindings)
        {
            var declared = actions.Where(a => (string?)a.Attribute("name") == action).ToList();
            Assert.AreEqual(1, declared.Count, $"{action} must be declared exactly once in {path}");
            Assert.AreEqual("actt_kick", (string?)declared[0].Attribute("type"), $"{action} must stay actt_kick");
        }
    }

    private static void AssertBound(string path)
    {
        var sets = XDocument.Load(path).Descendants("action_set").ToList();
        var warrior = sets.SingleOrDefault(s => (string?)s.Attribute("id") == DwarfAttacksConfig.ActionSetId);
        Assert.IsNotNull(warrior, $"{DwarfAttacksConfig.ActionSetId} is missing from {path}");
        foreach (var (action, clip) in Bindings)
        {
            var rows = sets.SelectMany(s => s.Elements("action")).Where(a => (string?)a.Attribute("type") == action).ToList();
            Assert.AreEqual(1, rows.Count, $"{action} must be bound exactly once in {path}");
            Assert.AreSame(warrior, rows[0].Parent, $"{action} is bound outside {DwarfAttacksConfig.ActionSetId}");
            Assert.AreEqual(clip, (string?)rows[0].Attribute("animation"));
        }
    }

    private static string Snapshot() => Path.Combine(RepoRoot(), "docs", "reference", "lotrlome-armory-snapshot");

    private static string ArmoryModuleData()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        string game = string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env!;
        string dir = Path.Combine(game, "Modules", "LOTRLOME_Armory", "ModuleData");
        if (!Directory.Exists(dir))
            Assert.Inconclusive("LOTRLOME_Armory not installed on this machine; the live data cannot be checked here.");
        return dir;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        Assert.IsNotNull(dir, "repo root not found");
        return dir!.FullName;
    }
}
