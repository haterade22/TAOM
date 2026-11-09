using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TAOM.Core.Logging;
using TAOM.Features.MapEventGuard;
using TAOM.Features.MapEventGuard.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;
using static TAOM.Tests.Features.MapEventGuard.SallySiegeTestObjects;

namespace TAOM.Tests.Features.MapEventGuard;

/// <summary>
/// Patch142 (#1099, crash bundles 9d1db520 and 66372c43): what the prefix does with the service's answer, and what
/// it binds to.
/// </summary>
[TestClass]
public class Patch142SallyOutEmptyPartyGuardTests
{
    private const string BehaviorTypeName = "TaleWorlds.CampaignSystem.CampaignBehaviors.SallyOutsCampaignBehavior";
    private const string HarmonyId = "taom.tests.patch142";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        Patch142_SallyOutEmptyPartyGuard.Initialize(new SallyOutGuardService(Substitute.For<IOutcomeLog>(), _logger), _logger);
    }

    [TestCleanup]
    public void Cleanup() => Patch142_SallyOutEmptyPartyGuard.Initialize(null, null);

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static void InCampaign(TaleWorlds.CampaignSystem.Party.MobileParty main, Action body)
    {
        var campaign = Bare<Campaign>();
        SetProperty(campaign, nameof(Campaign.MainParty), main);

        var current = typeof(Campaign).GetProperty(nameof(Campaign.Current), BindingFlags.Public | BindingFlags.Static)!;
        var setCurrent = current.GetSetMethod(nonPublic: true);
        Assert.IsNotNull(setCurrent, "Campaign.Current lost its setter; the test cannot stage the campaign.");
        var saved = current.GetValue(null);
        setCurrent!.Invoke(null, new object[] { campaign });
        try
        {
            body();
        }
        finally
        {
            setCurrent.Invoke(null, new[] { saved });
        }
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Prefix_LeaderOnlyHasHeroMarkedDiedInBattle_StopsVanilla()
    {
        var battle = Battle();
        var scene = Scene(battle, battle, new[] { HeroEntry(KillCharacterAction.KillCharacterActionDetail.DiedInBattle) });

        InCampaign(scene.Main, () =>
        {
            bool salliedOut = true;

            Assert.IsFalse(Patch142_SallyOutEmptyPartyGuard.Prefix(scene.Settlement, ref salliedOut));
            Assert.IsFalse(salliedOut);
        });
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Prefix_LeaderStillHasTroops_RunsVanillaAndLeavesSalliedOut()
    {
        var battle = Battle();
        var scene = Scene(battle, battle, new[] { TroopEntry(12) });

        InCampaign(scene.Main, () =>
        {
            bool salliedOut = true;

            Assert.IsTrue(Patch142_SallyOutEmptyPartyGuard.Prefix(scene.Settlement, ref salliedOut));
            Assert.IsTrue(salliedOut);
        });
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Prefix_NoCampaign_RunsVanillaAndLogsTheManifestFailLine()
    {
        bool salliedOut = true;

        bool runVanilla = Patch142_SallyOutEmptyPartyGuard.Prefix(null!, ref salliedOut);

        Assert.IsTrue(runVanilla);
        Assert.IsTrue(salliedOut);
        var manifest = RepoPaths.ReadSource("tools/ingame_checks/1099.toml");
        string pattern = Regex.Match(manifest, @"^fail = \['(.+)'\]", RegexOptions.Multiline).Groups[1].Value;
        Assert.AreNotEqual(string.Empty, pattern, "1099.toml has no fail pattern.");
        var failPattern = new Regex(pattern);
        _logger.Received(1).LogError(Arg.Is<string>(m => failPattern.IsMatch(m)));
    }

    [TestMethod]
    public void Prefix_NotInitialized_RunsVanilla()
    {
        Patch142_SallyOutEmptyPartyGuard.Initialize(null, null);
        bool salliedOut = true;

        Assert.IsTrue(Patch142_SallyOutEmptyPartyGuard.Prefix(null!, ref salliedOut));
        Assert.IsTrue(salliedOut);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Prefix_PatchesTheRealTargetThroughHarmony()
    {
        RequireGame();

        var harmony = new Harmony(HarmonyId);
        try
        {
            harmony.CreateClassProcessor(typeof(Patch142_SallyOutEmptyPartyGuard)).Patch();

            var target = AccessTools.Method(AccessTools.TypeByName(BehaviorTypeName), "CheckSallyOut");
            var prefixes = Harmony.GetPatchInfo(target)?.Prefixes;
            Assert.IsNotNull(prefixes, "Harmony did not patch CheckSallyOut.");
            Assert.IsTrue(prefixes.Any(p => p.owner == HarmonyId && p.PatchMethod.DeclaringType == typeof(Patch142_SallyOutEmptyPartyGuard)));
        }
        finally
        {
            harmony.UnpatchAll(HarmonyId);
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CheckSallyOut_ResolvesWithThePatchedParameterNames()
    {
        RequireGame();

        var type = AccessTools.TypeByName(BehaviorTypeName);
        Assert.IsNotNull(type, "SallyOutsCampaignBehavior did not resolve: Patch142 has no target.");

        var overloads = type.GetMethods(AccessTools.all).Where(m => m.Name == "CheckSallyOut").ToList();
        Assert.AreEqual(1, overloads.Count, "CheckSallyOut gained an overload: the by-name [HarmonyPatch] is now ambiguous.");

        // Harmony binds the prefix's parameters by name, so a rename makes it throw at patch time and the category does not apply.
        var parameters = overloads[0].GetParameters();
        CollectionAssert.AreEqual(
            new[] { "settlement", "checkForNavalSallyOut", "salliedOut" },
            parameters.Select(p => p.Name).ToArray());
        Assert.IsTrue(parameters[2].ParameterType.IsByRef, "salliedOut stopped being by-ref: the prefix cannot write it.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PatchClass_IsRegisteredInAllThreePlaces()
    {
        var patch = typeof(Patch142_SallyOutEmptyPartyGuard);

        Assert.AreEqual(1, patch.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length,
            "Patch142 lost its [HarmonyPatch] target attribute.");

        var categories = patch.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false)
            .Cast<HarmonyPatchCategory>()
            .Select(c => c.info.category)
            .ToList();
        CollectionAssert.Contains(categories, Patch142_SallyOutEmptyPartyGuard.Category);

        var subModule = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        StringAssert.Matches(subModule, new Regex(@"TryPatchCategory\([^)]*Patch142_SallyOutEmptyPartyGuard"));
        StringAssert.Contains(subModule, "IoC.Resolve<TAOM.Features.MapEventGuard.SallyOutGuardService>()");
    }
}
