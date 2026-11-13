using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CoopInterop;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Hooks;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Relations;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Relations;

/// <summary>
/// The disband-cost override needs no engine state. The pair and succession overrides read <c>Hero.MainHero</c>, which cannot
/// exist outside a campaign, so their decisions are <see cref="PersonalRelationServiceTests"/> and what is pinned here is
/// the shape of their IL: the cheap IsActive read before the hero, the pair override's base call first, nothing heavy on a path
/// that runs for every relation read.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class TaomDiplomacyModelPersonalRelationsTests
{
    private IPersonalRelationService _personal = null!;
    private TaomDiplomacyModel _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _personal = Substitute.For<IPersonalRelationService>();
        _sut = new TaomDiplomacyModel(
            Substitute.For<IWarOfTheRingService>(), Substitute.For<ICoopSessionProvider>(),
            Substitute.For<IOnAllianceAction>(), _personal,
            new TAOM.Features.KingdomAllegiance.KingdomAllegianceService(
                Substitute.For<TAOM.Features.KingdomAllegiance.IKingdomAllegianceSettingsProvider>(),
                Substitute.For<TAOM.Features.Execution.IAlignmentService>(), Substitute.For<ICoopSessionProvider>(),
                new TAOM.Core.Logging.OutcomeLog(Substitute.For<TAOM.Core.Logging.IModLogger>())),
            new TAOM.Features.KingdomAllegiance.Models.AllegianceFactsReader(
                Substitute.For<IPlayerControlQuery>(), Substitute.For<TAOM.Core.Logging.IOutcomeLog>()));
    }

    [TestMethod]
    public void GetRelationCostOfDisbandingArmy_TheServiceAnswers_ItsNumberIsUsed()
    {
        _personal.DisbandCost(true).Returns(-30);

        Assert.AreEqual(-30, _sut.GetRelationCostOfDisbandingArmy(true));
    }

    [TestMethod]
    public void GetRelationCostOfDisbandingArmy_TheServiceDefers_VanillasFourAndOneRemain()
    {
        _personal.DisbandCost(Arg.Any<bool>()).Returns((int?)null);

        Assert.AreEqual(-4, _sut.GetRelationCostOfDisbandingArmy(true));
        Assert.AreEqual(-1, _sut.GetRelationCostOfDisbandingArmy(false));
    }

    [TestMethod]
    public void GetRelationCostOfDisbandingArmy_ATruncatedZeroCost_IsNotMistakenForVanilla()
    {
        _personal.DisbandCost(true).Returns(0);

        Assert.AreEqual(0, _sut.GetRelationCostOfDisbandingArmy(true));
    }

    // --- IL shape of the two overrides that read Hero.MainHero ---

    private static List<MethodBase> Calls(string name)
    {
        var method = typeof(TaomDiplomacyModel).GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.IsNotNull(method, name);
        return IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody()!.GetILAsByteArray()).ToList();
    }

    private static bool IsHeavy(MethodBase m) =>
        m.DeclaringType == typeof(TAOM.IoC)
        || m.DeclaringType == typeof(TAOM.Core.Logging.IModLogger)
        || m.DeclaringType == typeof(IPersonalRelationSettingsProvider)
        || m.Name == "get_IsHumanPlayerCharacter"
        || m.Name == "Get";

    [TestMethod]
    public void GetHeroesForEffectiveRelation_CallsBaseBeforeTheService_AndNothingHeavy()
    {
        var calls = Calls("GetHeroesForEffectiveRelation");

        var baseCall = calls.FindIndex(m => m.Name == "GetHeroesForEffectiveRelation" && m.DeclaringType != typeof(TaomDiplomacyModel));
        var isActive = calls.FindIndex(m => m.Name == "get_IsActive" && m.DeclaringType == typeof(IPersonalRelationService));
        var resolve = calls.FindIndex(m => m.Name == "Resolve" && m.DeclaringType == typeof(IPersonalRelationService));

        Assert.IsTrue(baseCall >= 0, "the base call");
        var mainHero = calls.FindIndex(m => m.Name == "get_MainHero");
        Assert.IsTrue(isActive > baseCall, $"IsActive (#{isActive}) comes after the base call (#{baseCall})");
        Assert.IsTrue(mainHero > isActive, $"Hero.MainHero (#{mainHero}) is read after the cheap IsActive read (#{isActive})");
        Assert.IsTrue(resolve > isActive, $"Resolve (#{resolve}) comes after the cheap IsActive read (#{isActive})");
        var heavy = calls.Where(IsHeavy).Select(m => m.DeclaringType?.Name + "." + m.Name).ToList();
        Assert.AreEqual(0, heavy.Count, "nothing heavy on the relation read path: " + string.Join(", ", heavy));
    }

    [TestMethod]
    public void GetRelationChangeAfterClanLeaderIsDead_ReadsIsActiveBeforeTheHero_ThenAsksTheService_ThenBase_AndNothingHeavy()
    {
        var calls = Calls("GetRelationChangeAfterClanLeaderIsDead");

        var isActive = calls.FindIndex(m => m.Name == "get_IsActive" && m.DeclaringType == typeof(IPersonalRelationService));
        var mainHero = calls.FindIndex(m => m.Name == "get_MainHero");
        var inherited = calls.FindIndex(m => m.Name == "InheritedRelationChange" && m.DeclaringType == typeof(IPersonalRelationService));
        var baseCall = calls.FindIndex(m => m.Name == "GetRelationChangeAfterClanLeaderIsDead" && m.DeclaringType != typeof(TaomDiplomacyModel));

        Assert.IsTrue(isActive >= 0 && mainHero > isActive, $"IsActive (#{isActive}) comes before Hero.MainHero (#{mainHero})");
        Assert.IsTrue(inherited > mainHero, $"the service answer (#{inherited}) comes after the hero is read (#{mainHero})");
        Assert.IsTrue(baseCall > inherited, $"base (#{baseCall}) is the fall-through after the service (#{inherited})");
        Assert.AreEqual(0, calls.Count(IsHeavy));
    }
}
