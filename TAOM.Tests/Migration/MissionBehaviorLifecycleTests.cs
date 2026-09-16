using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Migration;

/// <summary>
/// `MissionBehavior.OnBehaviorInitialize` never runs for a behavior TAOM adds from
/// `SubModule.OnMissionBehaviorInitialize` (#606): `Mission.AfterStart` dispatches it to the
/// behaviors already in the list (v1.5.3 Mission.cs:3827) BEFORE it calls the submodules that add
/// TAOM's (:3831), and `AddMissionBehavior` itself calls only `OnCreated` (:4699). `EarlyStart`
/// and `AfterStart` do reach the late-added ones. SignatureStrikes shipped a mission gate in that
/// override and was inert in Mike's first battle with no log line to say why.
///
/// This is a ratchet: every TAOM behavior that overrides the dead callback must be on the
/// allowlist below with its reason, so a new one fails here rather than in a battle. The three
/// AddTaomBehavior siblings it started with moved their setup to `AfterStart` the same day.
/// </summary>
[TestClass]
public class MissionBehaviorLifecycleTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static readonly string[] KnownOverriders =
    {
        // Added from a postfix on the mission-opening call, BEFORE AfterStart, so it is in the
        // list when :3827 runs and the override does fire. Legitimate. The three AddTaomBehavior
        // siblings that used to sit here moved to AfterStart on 2026-09-16 (#606).
        "CustomBattleTeamFixBehavior",
    };

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void OnBehaviorInitialize_IsOverriddenOnlyByTheKnownBehaviors()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var missionBehavior = Type.GetType("TaleWorlds.MountAndBlade.MissionBehavior, TaleWorlds.MountAndBlade", throwOnError: true)!;

        Type[] types;
        try
        {
            types = typeof(TAOM.IoC).Assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t != null).ToArray()!;
        }

        var overriders = types
            .Where(t => missionBehavior.IsAssignableFrom(t) && !t.IsAbstract)
            .Where(t => t.GetMethod("OnBehaviorInitialize", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly) != null)
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            KnownOverriders.OrderBy(n => n, StringComparer.Ordinal).ToArray(),
            overriders,
            "A TAOM MissionBehavior overrides OnBehaviorInitialize. If it is added from " +
            "SubModule.OnMissionBehaviorInitialize that override never runs (#606): put the setup in " +
            "OnCreated (state reset), AfterStart (mission initialized, agents not yet spawned) or a " +
            "lazy first-use gate. If the add path is one where it does run, add it to KnownOverriders " +
            $"with the reason. Declared: [{string.Join(", ", overriders)}]");
    }
}
