using System.Runtime.Serialization;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TAOM.Adapters;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.StaleCharacterRepair;

/// <summary>
/// Runs the adapter's decisions and the engine's real copy on the shape a save restores: objects built with no
/// constructor, as <c>ObjectLoadData</c> builds them. A hollow character has a null culture AND a null body range, and
/// <c>Helpers.CharacterHelper:48</c> reads the body range right after the <c>Hero</c> constructor, so restoring the
/// culture alone moves the crash to a later line (#1076).
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class HeroCharacterStateAdapterTests
{
    private const string Id = "lord_x";

    private static readonly CultureObject HeroCulture = Culture("hero_culture");
    private static readonly CultureObject ClanCulture = Culture("clan_culture");
    private static readonly CultureObject TwinCulture = Culture("twin_culture");

    private MBObjectManager? _objects;

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    [TestInitialize]
    public void Setup()
    {
        // MBObjectManager.Instance is process-wide and null in every other test; Cleanup puts it back.
        _objects = MBObjectManager.Init();
        _objects.RegisterType<CharacterObject>("NPCCharacter", "NPCCharacters", 43u);
        _objects.RegisterType<MBBodyProperty>("BodyProperty", "BodyProperties", 50u);
    }

    [TestCleanup]
    public void Cleanup() => _objects?.Destroy();

    private static T Bare<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

    private static CultureObject Culture(string id)
    {
        var culture = Bare<CultureObject>();
        culture.StringId = id;
        return culture;
    }

    private static CharacterObject Hollow(string id = Id)
    {
        var character = Bare<CharacterObject>();
        character.StringId = id;
        return character;
    }

    private static void SetBody(CharacterObject character, MBBodyProperty body) =>
        AccessTools.PropertySetter(typeof(BasicCharacterObject), nameof(BasicCharacterObject.BodyPropertyRange))
            .Invoke(character, new object[] { body });

    private CharacterObject RegisterTwin(CultureObject? culture, bool withBody = true, int race = 4)
    {
        var twin = _objects!.RegisterObject(new CharacterObject { StringId = Id });
        ((BasicCharacterObject)twin).Culture = culture;
        SetBody(twin, withBody ? Bare<MBBodyProperty>() : null!);
        twin.Race = race;
        return twin;
    }

    // A hero joined to its character the way a loaded save joins them.
    private static Hero HeroOf(CharacterObject character, CultureObject? heroCulture = null, CultureObject? clanCulture = null)
    {
        var hero = Bare<Hero>();
        hero.StringId = character.StringId;
        hero.Culture = heroCulture!;
        AccessTools.Field(typeof(Hero), "_characterObject").SetValue(hero, character);
        AccessTools.Field(typeof(Hero), "_name").SetValue(hero, new TextObject("a hero"));
        AccessTools.Field(typeof(CharacterObject), "_heroObject").SetValue(character, hero);
        if (clanCulture != null)
        {
            var clan = Bare<Clan>();
            clan.Culture = clanCulture;
            AccessTools.Field(typeof(Hero), "_clan").SetValue(hero, clan);
        }
        return hero;
    }

    private static CharacterObject Healthy(CultureObject culture)
    {
        var character = Hollow();
        ((BasicCharacterObject)character).Culture = culture;
        SetBody(character, Bare<MBBodyProperty>());
        AccessTools.Field(typeof(BasicCharacterObject), "DefaultCharacterSkills").SetValue(character, new MBCharacterSkills());
        AccessTools.PropertySetter(typeof(CharacterObject), nameof(CharacterObject.UpgradeTargets))
            .Invoke(character, new object[] { new CharacterObject[0] });
        return character;
    }

    // ---- the gate ---- //
    [TestMethod]
    public void IsCharacterHollow_RawCultureNullButHeroHasCulture_IsTrue()
    {
        // The new CharacterObject.Culture answers from the hero, so a read without the base type never fires.
        var character = Healthy(HeroCulture);
        ((BasicCharacterObject)character).Culture = null;

        Assert.IsTrue(new HeroCharacterStateAdapter(HeroOf(character, HeroCulture)).IsCharacterHollow);
    }

    [TestMethod]
    public void IsCharacterHollow_CultureSetButBodyNull_IsTrue()
    {
        // The shape a co-op host sees: Coop's getter postfix has written the culture on a read.
        var character = Healthy(HeroCulture);
        SetBody(character, null!);

        Assert.IsTrue(new HeroCharacterStateAdapter(HeroOf(character, HeroCulture)).IsCharacterHollow);
    }

    [TestMethod]
    public void IsCharacterHollow_EveryFieldSet_IsFalse()
        => Assert.IsFalse(new HeroCharacterStateAdapter(HeroOf(Healthy(HeroCulture), HeroCulture)).IsCharacterHollow);

    // ---- the repair ---- //
    [TestMethod]
    public void TryRepairCharacter_HealthyTwinRegistered_AdoptsTheTwin()
    {
        var twin = RegisterTwin(TwinCulture);
        var hollow = Hollow();

        Assert.IsTrue(new HeroCharacterStateAdapter(HeroOf(hollow, HeroCulture)).TryRepairCharacter());

        Assert.AreSame(twin, hollow.OriginalCharacter);
        Assert.AreSame(TwinCulture, ((BasicCharacterObject)hollow).Culture, "the XML value, not the hero's");
        Assert.AreEqual(4, hollow.Race, "an orc lord's child must not be born human");
    }

    [TestMethod]
    public void TryRepairCharacter_GeneratedHeroOfAHollowRoot_FindsTheTwinByTheRootsId()
    {
        var twin = RegisterTwin(TwinCulture);
        var child = Hollow("CharacterObject_77");
        AccessTools.Field(typeof(CharacterObject), "_originCharacter").SetValue(child, Hollow());

        Assert.IsTrue(new HeroCharacterStateAdapter(HeroOf(child, HeroCulture)).TryRepairCharacter());

        Assert.AreSame(twin, child.OriginalCharacter);
        Assert.AreEqual(4, child.Race);
    }

    [TestMethod]
    public void TryRepairCharacter_RegisteredHeroWithARestoredRace_KeepsThatRaceAndTakesTheRest()
    {
        // RacePersistenceService reaches a registered character and restored this race at session launch. The twin's
        // race would replace it mid-session, and the next save would capture the wrong one (Codex F1, #1076).
        var twin = RegisterTwin(TwinCulture, race: 4);
        var child = _objects!.RegisterObject(new CharacterObject { StringId = "CharacterObject_77" });
        AccessTools.Field(typeof(CharacterObject), "_originCharacter").SetValue(child, Hollow());
        child.Race = 7;

        Assert.IsTrue(new HeroCharacterStateAdapter(HeroOf(child, HeroCulture)).TryRepairCharacter());

        Assert.AreEqual(7, child.Race);
        Assert.AreSame(twin, child.OriginalCharacter);
        Assert.AreSame(TwinCulture, ((BasicCharacterObject)child).Culture);
        Assert.IsNotNull(child.BodyPropertyRange);
    }

    [TestMethod]
    public void TryRepairCharacter_TheRegisteredObjectIsTheHerosOwn_DoesNotAdoptItself()
    {
        var own = _objects!.RegisterObject(new CharacterObject { StringId = Id });

        Assert.IsTrue(new HeroCharacterStateAdapter(HeroOf(own, HeroCulture)).TryRepairCharacter());

        Assert.IsTrue(own.IsOriginalCharacter);
        Assert.AreSame(HeroCulture, ((BasicCharacterObject)own).Culture);
    }

    [DataTestMethod]
    [DataRow(false, true, DisplayName = "twin with no culture")]
    [DataRow(true, false, DisplayName = "twin with no body range")]
    public void TryRepairCharacter_TwinIsItselfHollow_FallsBackToTheHero(bool twinHasCulture, bool twinHasBody)
    {
        RegisterTwin(twinHasCulture ? TwinCulture : null, twinHasBody);
        var hollow = Hollow();

        Assert.IsTrue(new HeroCharacterStateAdapter(HeroOf(hollow, HeroCulture)).TryRepairCharacter());

        Assert.IsTrue(hollow.IsOriginalCharacter, "adopting this twin brings its nulls with it");
        Assert.AreSame(HeroCulture, ((BasicCharacterObject)hollow).Culture);
    }

    [TestMethod]
    public void TryRepairCharacter_NoTwin_TakesTheHerosCultureAndFillsWhatABirthReads()
    {
        var hollow = Hollow();

        Assert.IsTrue(new HeroCharacterStateAdapter(HeroOf(hollow, HeroCulture)).TryRepairCharacter());

        Assert.AreSame(HeroCulture, ((BasicCharacterObject)hollow).Culture);
        Assert.IsNotNull(hollow.BodyPropertyRange, "CharacterHelper:48 reads this right after the Hero constructor");
        Assert.IsNotNull(hollow.GetDefaultCharacterSkills());
        Assert.IsFalse(StaleCharacterAdapter.IsBasicNameNull(hollow));
    }

    [TestMethod]
    public void TryRepairCharacter_NoTwinNoHeroCulture_TakesTheClansCulture()
    {
        var hollow = Hollow();

        Assert.IsTrue(new HeroCharacterStateAdapter(HeroOf(hollow, null, ClanCulture)).TryRepairCharacter());

        Assert.AreSame(ClanCulture, ((BasicCharacterObject)hollow).Culture);
    }

    [TestMethod]
    public void TryRepairCharacter_NoSourceAtAll_ReturnsFalseAndInventsNothing()
    {
        var hollow = Hollow();

        Assert.IsFalse(new HeroCharacterStateAdapter(HeroOf(hollow)).TryRepairCharacter());

        Assert.IsNull(((BasicCharacterObject)hollow).Culture);
    }

    // ---- the facts the RCA's census table reads ---- //
    [TestMethod]
    public void DescribeState_HollowWithTwin_StatesTheFactsThatNameTheOrigin()
    {
        RegisterTwin(TwinCulture);

        var state = new HeroCharacterStateAdapter(HeroOf(Hollow(), HeroCulture, ClanCulture)).DescribeState();

        foreach (var fact in new[]
                 {
                     "hero=lord_x ", "character=lord_x ", "original=True ", "origin=none ", "ready=False ",
                     "registeredSameInstance=False ", "registeredCulture=twin_culture ", "skillsNull=True ",
                     "bodyNull=True ", "basicNameNull=True ", "race=0 ", "heroCulture=hero_culture ",
                     "clanCulture=clan_culture ",
                 })
            StringAssert.Contains(state, fact);
    }

    [TestMethod]
    public void DescribeState_GeneratedChildOfAHollowRoot_ReportsTheRootsTwin()
    {
        // With the character's own id this printed registeredCulture=none, which the RCA reads as "id gone".
        RegisterTwin(TwinCulture);
        var child = Hollow("CharacterObject_77");
        AccessTools.Field(typeof(CharacterObject), "_originCharacter").SetValue(child, Hollow());

        var state = new HeroCharacterStateAdapter(HeroOf(child, HeroCulture)).DescribeState();

        StringAssert.Contains(state, "original=False ");
        StringAssert.Contains(state, "origin=lord_x ");
        StringAssert.Contains(state, "registeredCulture=twin_culture ");
    }

    [TestMethod]
    public void DescribeState_IdGoneFromModuleData_ReportsNoRegisteredCulture()
    {
        var state = new HeroCharacterStateAdapter(HeroOf(Hollow(), HeroCulture)).DescribeState();

        StringAssert.Contains(state, "registeredCulture=none ");
    }

    // ---- the four-field fill, shared with the sweep ---- //
    [TestMethod]
    public void FillNullFields_NonHeroWithNoName_GetsItsIdAsAName()
    {
        var troop = Hollow("taom_removed_militia");

        StaleCharacterAdapter.FillNullFields(troop);

        Assert.AreEqual("taom_removed_militia", troop.Name.ToString());
    }

    [TestMethod]
    public void FillNullFields_NonHeroWithAName_KeepsIt()
    {
        var name = new TextObject("Militia");
        var troop = Hollow("taom_militia");
        AccessTools.Field(typeof(BasicCharacterObject), "_basicName").SetValue(troop, name);

        StaleCharacterAdapter.FillNullFields(troop);

        Assert.AreSame(name, troop.Name);
    }

    // ---- the engine's copy ---- //
    [TestMethod]
    public void TryAdoptOrigin_HollowCharacter_TakesTheFieldsABirthReadsFromTheTwin()
    {
        var body = Bare<MBBodyProperty>();
        var skills = new MBCharacterSkills();
        var name = new TextObject("Gromash");
        var twin = RegisterTwin(TwinCulture);
        SetBody(twin, body);
        AccessTools.Field(typeof(BasicCharacterObject), "DefaultCharacterSkills").SetValue(twin, skills);
        AccessTools.Field(typeof(BasicCharacterObject), "_basicName").SetValue(twin, name);
        var hollow = Hollow();

        Assert.IsTrue(HeroCharacterStateAdapter.TryAdoptOrigin(hollow, twin));

        Assert.AreSame(TwinCulture, ((BasicCharacterObject)hollow).Culture, "FillFrom copies this onto the child");
        Assert.AreSame(body, hollow.BodyPropertyRange);
        Assert.AreSame(skills, hollow.GetDefaultCharacterSkills());
        Assert.AreSame(name, AccessTools.Field(typeof(BasicCharacterObject), "_basicName").GetValue(hollow));
        Assert.AreEqual(4, hollow.Race);
    }

    [TestMethod]
    public void TryAdoptOrigin_HollowCharacter_SetsTheSavedOriginSoTheEngineRepeatsTheCopyOnLoad()
    {
        var twin = RegisterTwin(TwinCulture);
        var hollow = Hollow();

        HeroCharacterStateAdapter.TryAdoptOrigin(hollow, twin);

        Assert.AreSame(twin, hollow.OriginalCharacter);
        Assert.IsFalse(hollow.IsOriginalCharacter, "SandBoxManager.InitializeCharactersAfterLoad copies only for a non-original");
        Assert.IsTrue(hollow.IsReady);
    }
}
