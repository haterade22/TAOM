using System;
using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.DreadAura.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.DreadAura;

/// <summary>
/// Drift-guards for the engine members the dread aura reaches. The aura writes morale through
/// extension methods and reads a mission-scope game model, none of which the compiler pins across
/// an engine bump the way a direct reference would.
///
/// These catch SIGNATURE drift. They cannot catch BEHAVIOUR drift in the constants the feature is
/// balanced against (<c>_recoveryMorale = _initialMorale * 0.5f</c>, the 0.01 panic threshold, the
/// +0.4/sec regeneration, the 30% formation-flee threshold). Those are recorded with their engine
/// file and line in docs/features/dread-aura.md and are a control-battle item on the
/// <c>/engine-bump</c> checklist.
/// </summary>
[TestClass]
public class DreadAuraBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MissionLogic_IsTheBaseClass_NotMissionBehavior()
    {
        // The BehaviorTreeMissionLogic regression: a class inheriting MissionBehavior while
        // declaring BehaviorType => Logic makes vanilla AddMissionBehavior push
        // `this as MissionLogic` (null) into _missionLogics and NRE every tick.
        // docs/reviews/rca-looter-battle-nre-2026-05-24.md
        RequireGame();

        var baseType = typeof(DreadAuraMissionLogic).BaseType;

        Assert.IsNotNull(baseType);
        Assert.AreEqual("MissionLogic", baseType!.Name,
            "DreadAuraMissionLogic must inherit MissionLogic directly. Inheriting MissionBehavior " +
            "while reporting BehaviorType.Logic NREs Mission.CheckMissionEnded every tick.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void AgentMoraleExtensions_ResolveAgainstInstalledEngine()
    {
        RequireGame();

        var extensions = AccessTools.TypeByName("TaleWorlds.MountAndBlade.AgentComponentExtensions");
        Assert.IsNotNull(extensions, "AgentComponentExtensions is gone — the aura has no way to write morale.");

        foreach (var name in new[] { "GetMorale", "ChangeMorale" })
        {
            Assert.IsNotNull(
                extensions!.GetMethods().FirstOrDefault(m => m.Name == name),
                $"AgentComponentExtensions.{name} did not resolve.");
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CommonAIComponent_MoraleProperty_Resolves()
    {
        // The aura's whole effect is this property, reached through the extensions above, and
        // CanAffect gates on the component being non-null.
        RequireGame();

        var component = AccessTools.TypeByName("TaleWorlds.MountAndBlade.CommonAIComponent");
        Assert.IsNotNull(component, "CommonAIComponent is gone — agent morale has moved.");
        Assert.IsNotNull(AccessTools.Property(component, "Morale"), "CommonAIComponent.Morale did not resolve.");

        var agentProperty = AccessTools.Property(
            AccessTools.TypeByName("TaleWorlds.MountAndBlade.Agent"), "CommonAIComponent");
        Assert.IsNotNull(agentProperty,
            "Agent.CommonAIComponent did not resolve — CanAffect's null guard would stop compiling, " +
            "and without it SetMorale NREs on every player-controlled agent.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void BattleMoraleModel_CalculateMoraleChangeToCharacter_Resolves()
    {
        // CALLED, never overridden — it is how tier and hero resistance reach the aura at parity
        // with vanilla kill-morale.
        RequireGame();

        var model = AccessTools.TypeByName("TaleWorlds.MountAndBlade.ComponentInterfaces.BattleMoraleModel");
        Assert.IsNotNull(model, "BattleMoraleModel is gone.");

        var method = AccessTools.Method(model, "CalculateMoraleChangeToCharacter");
        Assert.IsNotNull(method, "BattleMoraleModel.CalculateMoraleChangeToCharacter did not resolve.");

        var parameters = method!.GetParameters();
        Assert.AreEqual(2, parameters.Length);
        Assert.AreEqual("Agent", parameters[0].ParameterType.Name);
        Assert.AreEqual(typeof(float), parameters[1].ParameterType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Mission_GetNearbyEnemyAgents_ResolvesWithTheExpectedOverload()
    {
        RequireGame();

        var mission = AccessTools.TypeByName("TaleWorlds.MountAndBlade.Mission");
        var method = AccessTools.Method(mission, "GetNearbyEnemyAgents");

        Assert.IsNotNull(method, "Mission.GetNearbyEnemyAgents did not resolve.");
        CollectionAssert.AreEqual(
            new[] { "Vec2", "Single", "Team", "MBList`1" },
            method!.GetParameters().Select(p => p.ParameterType.Name).ToArray(),
            "GetNearbyEnemyAgents overload drifted. The team parameter is what filters allies out " +
            "NATIVE-side; losing it would put every nearby agent through the managed loop.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MissionTypeMembers_UsedByTheGate_Resolve()
    {
        RequireGame();

        var mission = AccessTools.TypeByName("TaleWorlds.MountAndBlade.Mission");

        foreach (var name in new[] { "IsFieldBattle", "IsSiegeBattle", "IsSallyOutBattle", "CombatType" })
        {
            Assert.IsNotNull(AccessTools.Property(mission, name),
                $"Mission.{name} did not resolve — DreadMissionGate's allowlist would stop compiling. " +
                "Do NOT relax it to a blocklist: arenas and tournaments must stay excluded.");
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MissionTeamAiTypes_StillLackAHideoutOrArenaMember()
    {
        // The gate excludes hideouts and arenas by relying on them being NoTeamAI. If a future
        // engine adds a dedicated member, that reasoning silently stops holding and the allowlist
        // needs revisiting rather than the enum.
        RequireGame();

        var enumType = AccessTools.TypeByName("TaleWorlds.MountAndBlade.Mission+MissionTeamAITypeEnum");
        Assert.IsNotNull(enumType, "Mission.MissionTeamAITypeEnum did not resolve.");

        CollectionAssert.AreEquivalent(
            new[] { "NoTeamAI", "FieldBattle", "Siege", "SallyOut", "NavalBattle", "NavalRaid" },
            Enum.GetNames(enumType!),
            "MissionTeamAITypeEnum changed. DreadMissionGate allows FieldBattle/Siege/SallyOut and " +
            "relies on hideouts and arenas being NoTeamAI — re-derive the allowlist before updating this list.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SkinVoiceManager_FearVoiceType_Resolves()
    {
        RequireGame();

        var voiceType = AccessTools.TypeByName("TaleWorlds.MountAndBlade.SkinVoiceManager+VoiceType");
        Assert.IsNotNull(voiceType, "SkinVoiceManager.VoiceType did not resolve.");
        Assert.IsNotNull(AccessTools.Field(voiceType, "Fear"),
            "SkinVoiceManager.VoiceType.Fear is gone — the aura's only audible cue.");
    }
}
