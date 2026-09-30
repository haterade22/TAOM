using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.RealmBorders;
using TAOM.Features.RealmBorders.Hooks;
using TAOM.Features.RealmBorders.UI;
using TAOM.Features.TimeAcceleration;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.RealmBorders;

/// <summary>
/// Wiring regression guard for Realm Borders (#698). Nothing in the feature is reached by a patch or a
/// model: the module list adds the campaign behavior, the behavior attaches the map view, and the view
/// reads two game keys whose Options labels live in global_strings.xml. Drop any link and the borders
/// never draw, or the keys show as raw ids, with no error anywhere.
/// </summary>
[TestClass]
public class RealmBordersWiringTests
{
    private static string ModuleDataPath => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Main", "_Module", "ModuleData"));

    [TestMethod]
    public void FeatureModules_ListTheRealmBordersModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<RealmBordersModule>().Count(),
            "RealmBordersModule must be listed exactly once in Main/Composition/FeatureModules.cs, or the borders "
            + "behavior is never added (or added twice).");
    }

    [TestMethod]
    public void IoC_DoesNotRegisterTheFeatureByHand()
    {
        var src = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);

        Assert.IsFalse(src.Contains("RegisterRealmBordersFeature"),
            "Main/IoC.cs registers RealmBorders by hand AND through its module: every service gets a second "
            + "default registration and Resolve throws at campaign start.");
    }

    [TestMethod]
    public void GlobalStrings_NameAndDescribeBothKeys()
    {
        // The Options screen looks up str_key_name.<category>_<id> in this file and no other.
        var ids = XDocument.Load(Path.Combine(ModuleDataPath, "global_strings.xml")).Descendants("string")
            .Select(s => (string)s.Attribute("id")).ToList();

        foreach (var id in new[] { TaomRealmBordersHotKeyCategory.ToggleBordersKeyId, TaomRealmBordersHotKeyCategory.CycleMapModeKeyId })
        {
            CollectionAssert.Contains(ids, $"str_key_name.{TaomRealmBordersHotKeyCategory.CategoryId}_{id}");
            CollectionAssert.Contains(ids, $"str_key_description.{TaomRealmBordersHotKeyCategory.CategoryId}_{id}");
        }
    }

    [TestMethod]
    public void Behavior_RegistersNothingOnADedicatedServerOrBesideKingdomBorders()
    {
        // The gate must run before the first listener: the tick listener reaches MapScreen (client-only
        // SandBox.View), and a second set of lines beside the Kingdom Borders mod's is what the review ruled out.
        var src = RepoPaths.ReadSource("Main/Features/RealmBorders/Hooks/RealmBordersCampaignBehavior.cs", stripComments: true);

        StringAssert.Matches(src, new Regex(@"RegisterEvents\(\)\s*\{\s*if \(!ShouldDraw\(\)\)\s*return;"));
        StringAssert.Contains(src, "_server.IsDedicatedServer");
        Assert.AreEqual("KingdomBorders", RealmBordersCampaignBehavior.KingdomBordersModuleId,
            "the Id in the Kingdom Borders mod's SubModule.xml");
    }

    [TestMethod]
    public void Behavior_LooksAgainOnEveryChangeTheMapShows()
    {
        // Ownership moves the lines; kingdoms, wars and alliances move the war map's relations, and the
        // player's own contract moves the gold cord. The service skips a repaint that would change nothing.
        var src = RepoPaths.ReadSource("Main/Features/RealmBorders/Hooks/RealmBordersCampaignBehavior.cs", stripComments: true);

        foreach (var campaignEvent in new[]
                 {
                     "OnSettlementOwnerChangedEvent", "OnClanChangedKingdomEvent", "KingdomCreatedEvent", "KingdomDestroyedEvent",
                     "WarDeclared", "MakePeace", "OnAllianceStartedEvent", "OnAllianceEndedEvent",
                 })
        {
            StringAssert.Matches(src, new Regex(@"CampaignEvents\." + campaignEvent + @"\.AddNonSerializedListener\(this, [^;]*MarkDirty\(\)\);"),
                campaignEvent + " no longer marks the borders");
        }
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Module_RegistersTheServiceGraph_AndItsBehaviorDeclResolvesTheSingleton()
    {
        using var container = new Container();
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(ModuleDataPath);
        container.RegisterInstance(paths);
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IAlignmentService>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        var module = new RealmBordersModule();

        module.RegisterServices(container);

        Assert.AreEqual(1, module.CampaignBehaviors.Count);
        var decl = module.CampaignBehaviors[0];
        Assert.AreEqual(typeof(RealmBordersCampaignBehavior), decl.BehaviorType);
        var behavior = decl.Create(container);
        Assert.IsInstanceOfType(behavior, typeof(RealmBordersCampaignBehavior));
        Assert.AreSame(behavior, decl.Create(container), "one behavior per process, like every module behavior");
        Assert.AreSame(container.Resolve<RealmBorderService>(), container.Resolve<RealmBorderService>());
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void HotKeyCategory_RegistersBothKeysWhereTheOptionsScreenShowsThem()
    {
        var category = new TaomRealmBordersHotKeyCategory();
        var keys = category.RegisteredGameKeys.Where(k => k != null).ToArray();

        CollectionAssert.AreEquivalent(
            new[] { TaomRealmBordersHotKeyCategory.ToggleBordersKeyId, TaomRealmBordersHotKeyCategory.CycleMapModeKeyId },
            keys.Select(k => k.Id).ToArray());
        Assert.AreEqual(GameKeyContext.GameKeyContextType.Default, category.Type);
        foreach (var key in keys)
        {
            // Below TotalGameKeyCount the Options label would reuse a vanilla key's name.
            Assert.IsTrue(key.Id >= (int)GameKeyDefinition.TotalGameKeyCount, $"{key.StringId} id {key.Id}");
            Assert.AreEqual(GameKeyMainCategories.CampaignMapCategory, key.MainCategoryId, key.StringId);
            Assert.AreEqual(TaomRealmBordersHotKeyCategory.CategoryId, key.GroupId, key.StringId);
            Assert.IsNotNull(key.KeyboardKey, $"{key.StringId} ships unbound");
        }
        Assert.AreEqual(InputKey.M, keys.Single(k => k.Id == TaomRealmBordersHotKeyCategory.ToggleBordersKeyId).KeyboardKey.InputKey);
        Assert.AreEqual(InputKey.G, keys.Single(k => k.Id == TaomRealmBordersHotKeyCategory.CycleMapModeKeyId).KeyboardKey.InputKey);
    }

    [TestMethod]
    public void HotKeyIds_DoNotInterleaveWithTheOtherTaomCategories()
    {
        var others = new[]
        {
            TaomTimeControlHotKeyCategory.FastForwardKeyId,
            TaomTimeControlHotKeyCategory.ExtraFastForwardKeyId,
            TaomTimeControlHotKeyCategory.TurboKeyId,
            TaomCareerHotKeyCategory.AbilityActivationKeyId,
        };

        Assert.IsTrue(TaomRealmBordersHotKeyCategory.ToggleBordersKeyId > others.Max(),
            "the realm border keys follow every other TAOM key id");
    }
}
