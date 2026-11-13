using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HallSeats.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.HallSeats;

/// <summary>
/// Drift-guards for Patch148 (Hall Seats). Both hooks bind the targets' parameters by NAME, the skip hook returns the
/// target's own value, and the adapter reads the private <c>MissionConversationLogic._teleportNearCharacter</c>, so a rename
/// or a changed signature on an engine bump would otherwise turn the feature into a silent no-op. The decisions are
/// covered by <c>HallSeatPlannerTests</c> and <c>HallSeatsServiceTests</c>.
/// </summary>
[TestClass]
public class Patch148HallSeatsBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static Type Engine(string fullName)
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var type = AccessTools.TypeByName(fullName);
        Assert.IsNotNull(type, fullName + " did not resolve.");
        return type;
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SpawnLocationCharacters_IsPublicInstance_WithTheOverrideTagParameter()
    {
        var handler = Engine("SandBox.Missions.MissionLogics.MissionAgentHandler");
        var method = handler.GetMethod("SpawnLocationCharacters", BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(method, "SpawnLocationCharacters is gone: Patch148 opens no spawn pass.");
        Assert.AreEqual(typeof(void), method.ReturnType);
        CollectionAssert.AreEqual(new[] { "overridenTagValue" }, method.GetParameters().Select(p => p.Name).ToArray());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SpawnDefaultLocationCharacter_ReturnsAnAgent_AndNamesItsCharacterParameter()
    {
        var handler = Engine("SandBox.Missions.MissionLogics.MissionAgentHandler");
        var method = handler.GetMethod("SpawnDefaultLocationCharacter", BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(method, "SpawnDefaultLocationCharacter is gone: Patch148 cannot hold a lord back.");
        Assert.AreEqual("TaleWorlds.MountAndBlade.Agent", method.ReturnType.FullName);
        CollectionAssert.AreEqual(new[] { "locationCharacter", "simulateAgentAfterSpawn" }, method.GetParameters().Select(p => p.Name).ToArray());
        Assert.AreEqual("TaleWorlds.CampaignSystem.Settlements.Locations.LocationCharacter", method.GetParameters()[0].ParameterType.FullName);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Hooks_BindTheTargetParametersByName()
    {
        var open = typeof(MissionAgentHandler_SpawnLocationCharacters_HallSeats_Patch);
        CollectionAssert.AreEqual(new[] { "__instance", "__state" }, open.GetMethod("Prefix")!.GetParameters().Select(p => p.Name).ToArray());
        Assert.IsTrue(open.GetMethod("Prefix")!.GetParameters()[1].IsOut, "The prefix must set __state on every path.");
        Assert.AreEqual(typeof(void), open.GetMethod("Prefix")!.ReturnType);
        var finalizer = open.GetMethod("Finalizer")!;
        Assert.AreEqual(typeof(void), finalizer.ReturnType, "A void finalizer keeps Harmony on rethrow (lesson: value-returning finalizers erase the throw site).");
        CollectionAssert.AreEqual(new[] { "__state" }, finalizer.GetParameters().Select(p => p.Name).ToArray());

        var skip = typeof(MissionAgentHandler_SpawnDefaultLocationCharacter_HallSeats_Patch).GetMethod("Prefix")!;
        Assert.AreEqual(typeof(bool), skip.ReturnType);
        CollectionAssert.AreEqual(new[] { "locationCharacter", "__result" }, skip.GetParameters().Select(p => p.Name).ToArray());
        Assert.IsTrue(skip.GetParameters()[1].ParameterType.IsByRef);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ConversationTarget_PrivateField_StillResolves()
    {
        var logic = Engine("SandBox.Conversation.MissionLogics.MissionConversationLogic");
        var field = logic.GetField("_teleportNearCharacter", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "_teleportNearCharacter is gone: the lord the player asked to see is no longer kept in the hall.");
        Assert.AreEqual("TaleWorlds.CampaignSystem.CharacterObject", field.FieldType.FullName);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SpotCountingMembers_StayPublic()
    {
        var handler = Engine("SandBox.Missions.MissionLogics.MissionAgentHandler");
        Assert.IsNotNull(handler.GetMethod("FindUnusedUsablePointCount", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null));
        Assert.IsNotNull(handler.GetMethod("HasUsablePointWithTag", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(string) }, null));

        var character = Engine("TaleWorlds.CampaignSystem.Settlements.Locations.LocationCharacter");
        Assert.IsNotNull(character.GetProperty("SpecialTargetTag"));
        Assert.IsNotNull(character.GetProperty("ForceSpawnInSpecialTargetTag"));
        Assert.IsNotNull(character.GetProperty("AgentOrigin"));
    }
}
