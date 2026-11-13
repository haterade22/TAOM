using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TAOM.Core.Logging;
using TAOM.Features.LordSpawnGuard;
using TAOM.Features.LordSpawnGuard.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.LordSpawnGuard;

/// <summary>
/// #482: vanilla <c>SpawnCaravan</c> dereferences a null template when no caravan template fits
/// the merchant's settlement. Pins the rule that mirrors vanilla's pick
/// (<see cref="CaravanTemplateGuard"/>), the prefix's wiring to it on staged engine objects, the
/// shape of the patch (a skip prefix and no finalizer of its own) and the target binding.
/// </summary>
[TestClass]
public class Patch147CaravanTemplateGuardTests
{
    private static readonly int?[] Land = { 0, 0 };
    private static readonly int?[] Naval = { 2 };
    private static readonly int?[] Mixed = { 0, 3 };
    private static readonly int?[] None = new int?[0];

    private const float NormalRoll = 0f;   // chance 0 is not above any RandomFloat: the normal list
    private const float EliteRoll = 0.9f;  // above the 0.5 RandomFloat used below: the elite list

    private static CaravanPickFailure Judge(bool inSettlement, bool port, int?[] normal, int?[] elite,
        float chance = NormalRoll) =>
        CaravanTemplateGuard.Judge(chance, 0.5f, inSettlement, port, normal, elite, out _);

    // ---- the rule ----

    [TestMethod]
    public void AllLandTemplates_InPortTown_NoTemplateFits()
    {
        // The issue's main case: a fully populated list that matches nothing at a port.
        Assert.AreEqual(CaravanPickFailure.NoTemplateFits, Judge(true, true, Land, Mixed));
    }

    [TestMethod]
    public void AllLandTemplates_InLandTown_Resolves()
    {
        Assert.AreEqual(CaravanPickFailure.None, Judge(true, false, Land, None));
    }

    [TestMethod]
    public void MixedTemplates_ResolveEitherWay()
    {
        Assert.AreEqual(CaravanPickFailure.None, Judge(true, true, Mixed, None));
        Assert.AreEqual(CaravanPickFailure.None, Judge(true, false, Mixed, None));
    }

    [TestMethod]
    public void NavalOnlyTemplates_InLandTown_NoTemplateFits()
    {
        Assert.AreEqual(CaravanPickFailure.NoTemplateFits, Judge(true, false, Naval, Mixed));
    }

    [TestMethod]
    public void EmptyOrMissingList_NoTemplateFits()
    {
        Assert.AreEqual(CaravanPickFailure.NoTemplateFits, Judge(true, false, None, Mixed));
        Assert.AreEqual(CaravanPickFailure.NoTemplateFits, Judge(true, false, null, Mixed));
    }

    [TestMethod]
    public void NoCurrentSettlement_Fails()
    {
        // The predicate reads hero.CurrentSettlement unguarded; with an empty list the pick is null
        // instead. Vanilla throws either way, before it changes any state.
        Assert.AreEqual(CaravanPickFailure.NoCurrentSettlement, Judge(false, false, Mixed, Mixed));
        Assert.AreEqual(CaravanPickFailure.NoTemplateFits, Judge(false, false, None, Mixed));
    }

    [TestMethod]
    public void EntryWithNoTemplateData_Fails_EvenBesideAMatch()
    {
        // A caravan child with no id adds a null to the list, and an id no template defines adds a
        // template whose ShipHulls is null. Vanilla runs the predicate on every entry, so it throws
        // on that one whatever else the list holds.
        Assert.AreEqual(CaravanPickFailure.BrokenTemplateEntry, Judge(true, false, new int?[] { 0, null }, None));
        Assert.AreEqual(CaravanPickFailure.BrokenTemplateEntry, Judge(true, true, new int?[] { null, 2 }, None));
    }

    [TestMethod]
    public void OnlyTheListVanillaReadsIsJudged()
    {
        // Normal list read: a failing elite list does not matter, a failing normal list does.
        Assert.AreEqual(CaravanPickFailure.None, Judge(true, false, Land, Naval, NormalRoll));
        Assert.AreEqual(CaravanPickFailure.NoTemplateFits, Judge(true, false, Naval, Land, NormalRoll));
        // Elite list read: the reverse.
        Assert.AreEqual(CaravanPickFailure.None, Judge(true, false, Naval, Land, EliteRoll));
        Assert.AreEqual(CaravanPickFailure.NoTemplateFits, Judge(true, false, Land, Naval, EliteRoll));
    }

    [TestMethod]
    public void Judge_ReportsWhichListItRead()
    {
        CaravanTemplateGuard.Judge(EliteRoll, 0.5f, true, false, Land, Land, out var elite);
        Assert.IsTrue(elite);
        CaravanTemplateGuard.Judge(NormalRoll, 0.5f, true, false, Land, Land, out elite);
        Assert.IsFalse(elite);
    }

    [TestMethod]
    public void ReadsEliteList_MirrorsVanillasStrictComparison()
    {
        // Vanilla: GetEliteCaravanSpawnChance(hero) > hero.RandomFloat(). The hero's RandomFloat is
        // his saved RandomValue, so the same hero gets the same list on every call.
        Assert.IsTrue(CaravanTemplateGuard.ReadsEliteList(0.6f, 0.5f));
        Assert.IsFalse(CaravanTemplateGuard.ReadsEliteList(0.5f, 0.5f));
        Assert.IsFalse(CaravanTemplateGuard.ReadsEliteList(0f, 0.5f));
        Assert.IsFalse(CaravanTemplateGuard.ReadsEliteList(float.NaN, 0.5f));
    }

    // ---- the patch ----

    [TestMethod]
    public void Patch_IsASkipPrefix_WithNoFinalizerOfItsOwn()
    {
        // The first design contained a NullReferenceException in a finalizer, which cannot tell
        // the null template from any other NRE on the call. The guard must stay a prefix that
        // predicts the pick, so it swallows nothing.
        var methods = typeof(Patch147_CaravanTemplateGuard)
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsFalse(methods.Any(m => m.GetCustomAttribute<HarmonyFinalizer>() != null || m.Name == "Finalizer"),
            "Patch147 must not have a finalizer.");
        var prefix = methods.Single(m => m.GetCustomAttribute<HarmonyPrefix>() != null);
        Assert.AreEqual(typeof(bool), prefix.ReturnType, "The prefix decides whether vanilla runs.");
    }

    [TestMethod]
    public void Prefix_NullHero_RunsVanilla()
    {
        // Not this guard's case: vanilla decides, as it would with no patch.
        Assert.IsTrue(Patch147_CaravanTemplateGuard.Prefix(null));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Prefix_LandTemplatesInALandTown_RunsVanilla_AndLogsNothing()
    {
        Staged(power: 0f, port: false, normal: Templates(0, 0), elite: Templates(2), (hero, logger) =>
        {
            Assert.IsTrue(Patch147_CaravanTemplateGuard.Prefix(hero));
            logger.DidNotReceive().LogWarning(Arg.Any<string>());
        });
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Prefix_LandTemplatesAtAPort_SkipsVanilla_AndWarnsOnce()
    {
        Staged(power: 0f, port: true, normal: Templates(0, 0), elite: Templates(2), (hero, logger) =>
        {
            Assert.IsFalse(Patch147_CaravanTemplateGuard.Prefix(hero));
            Assert.IsFalse(Patch147_CaravanTemplateGuard.Prefix(hero));

            logger.Received(1).LogWarning(Arg.Is<string>(m =>
                m.Contains("Patch147") && m.Contains("staged_culture") && m.Contains("caravan_party_templates")
                && !m.Contains("elite_caravan_party_templates")));
        });
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Prefix_HighPowerHero_JudgesTheEliteList()
    {
        // Power 200 gives an elite chance of 0.4, above the staged hero's RandomFloat of 0.
        Staged(power: 200f, port: false, normal: Templates(2), elite: Templates(0), (hero, logger) =>
            Assert.IsTrue(Patch147_CaravanTemplateGuard.Prefix(hero), "the elite list fits; the normal one is not read"));

        Staged(power: 200f, port: false, normal: Templates(0), elite: Templates(2), (hero, logger) =>
        {
            Assert.IsFalse(Patch147_CaravanTemplateGuard.Prefix(hero), "the elite list does not fit");
            logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("elite_caravan_party_templates")));
        });
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Prefix_EntryWithNoTemplateData_SkipsVanilla()
    {
        var withNull = Templates(0);
        withNull.Add(null);
        Staged(power: 0f, port: false, normal: withNull, elite: Templates(0), (hero, logger) =>
        {
            Assert.IsFalse(Patch147_CaravanTemplateGuard.Prefix(hero));
            logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("no template data")));
        });
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SpawnCaravan_BindingResolves_AgainstInstalledEngine()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var type = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignBehaviors.CaravansCampaignBehavior");
        Assert.IsNotNull(type, "CaravansCampaignBehavior did not resolve; Patch147's target type is gone.");

        var method = AccessTools.Method(type, "SpawnCaravan");
        Assert.IsNotNull(method, "SpawnCaravan did not resolve; Patch147 would apply to nothing.");
        Assert.AreEqual(typeof(void), method.ReturnType, "SpawnCaravan returns a value now; a skip must set it.");

        var first = method.GetParameters().FirstOrDefault();
        Assert.IsNotNull(first, "SpawnCaravan takes no parameters; signature drifted.");
        Assert.AreEqual("Hero", first.ParameterType.Name, "SpawnCaravan's first parameter is no longer a Hero.");
        Assert.AreEqual("hero", first.Name, "Harmony binds the prefix's `hero` argument by name.");
    }

    // ---- staging: bare engine objects holding only what the prefix reads ----

    private static void Staged(float power, bool port, MBList<PartyTemplateObject> normal,
        MBList<PartyTemplateObject> elite, Action<Hero, IModLogger> body)
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var culture = Bare<CultureObject>();
        culture.StringId = "staged_culture";
        Set(culture, nameof(CultureObject.CaravanPartyTemplates), normal);
        Set(culture, nameof(CultureObject.EliteCaravanPartyTemplates), elite);

        var settlement = Bare<Settlement>();
        Set(settlement, nameof(Settlement.HasPort), port);

        var hero = Bare<Hero>();
        hero.Culture = culture;
        SetField(hero, "_power", power);
        SetField(hero, "_stayingInSettlement", settlement);

        var models = Bare<GameModels>();
        Set(models, nameof(GameModels.CaravanModel), new DefaultCaravanModel());
        var campaign = Bare<Campaign>();
        SetField(campaign, "_gameModels", models);

        var current = typeof(Campaign).GetProperty(nameof(Campaign.Current), BindingFlags.Public | BindingFlags.Static)!;
        var setCurrent = current.GetSetMethod(nonPublic: true);
        Assert.IsNotNull(setCurrent, "Campaign.Current lost its setter; the test cannot stage the campaign.");
        var saved = current.GetValue(null);
        var logger = Substitute.For<IModLogger>();
        setCurrent!.Invoke(null, new object[] { campaign });
        Patch147_CaravanTemplateGuard.UseLogger(logger);
        try
        {
            body(hero, logger);
        }
        finally
        {
            Patch147_CaravanTemplateGuard.UseLogger(null);
            setCurrent.Invoke(null, new[] { saved });
        }
    }

    private static MBList<PartyTemplateObject> Templates(params int[] hullCounts)
    {
        var list = new MBList<PartyTemplateObject>();
        foreach (var count in hullCounts)
        {
            var template = Bare<PartyTemplateObject>();
            template.ShipHulls = new MBList<ShipTemplateStack>();
            for (var i = 0; i < count; i++)
                template.ShipHulls.Add(default);
            list.Add(template);
        }
        return list;
    }

    private static T Bare<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

    private static void Set(object target, string property, object value)
    {
        var setter = target.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetSetMethod(nonPublic: true);
        Assert.IsNotNull(setter, $"{target.GetType().Name}.{property} lost its setter; the test cannot stage it.");
        setter!.Invoke(target, new[] { value });
    }

    private static void SetField(object target, string field, object value)
    {
        var info = target.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(info, $"{target.GetType().Name}.{field} is gone; the test cannot stage it.");
        info!.SetValue(target, value);
    }
}
