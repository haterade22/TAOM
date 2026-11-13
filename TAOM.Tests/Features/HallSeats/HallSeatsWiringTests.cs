using System.Linq;
using System.Reflection;
using DryIoc;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.HallSeats;
using TAOM.Features.HallSeats.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.HallSeats;

/// <summary>
/// Wiring guards for Hall Seats. Drop the module line and Patch148 never applies; drop a category attribute and that
/// hook is dead code; drop the statics handshake and both hooks see no service and leave vanilla's spawn alone.
/// </summary>
[TestClass]
public class HallSeatsWiringTests
{
    private static Container BuildContainer(HallSeatsModule module)
    {
        var container = new Container();
        module.RegisterServices(container);
        container.RegisterInstance(Substitute.For<IModLogger>());
        return container;
    }

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<HallSeatsModule>().Count());
        var src = RepoPaths.ReadSource("Main/Composition/FeatureModules.cs", stripComments: true);
        Assert.IsTrue(src.Contains("new Features.HallSeats.HallSeatsModule()"));
    }

    [TestMethod]
    public void TheModule_IsNotParked_OwnsNoSaveData_AndAppliesOneGameInitCategory()
    {
        var module = new HallSeatsModule();

        Assert.AreEqual("HallSeats", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
        Assert.AreEqual(1, module.PatchCategories.Count);
        Assert.AreEqual("Patch148_HallSeats", module.PatchCategories[0].Category);
        Assert.AreEqual(ApplyPhase.GameInit, module.PatchCategories[0].Phase);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.GameModels.Count);
        Assert.AreEqual(0, module.MissionBehaviors.Count);
    }

    [TestMethod]
    public void BothHooks_CarryTheModulesCategory()
    {
        foreach (var hook in new[]
                 {
                     typeof(MissionAgentHandler_SpawnLocationCharacters_HallSeats_Patch),
                     typeof(MissionAgentHandler_SpawnDefaultLocationCharacter_HallSeats_Patch),
                 })
        {
            var category = hook.GetCustomAttribute<HarmonyPatchCategory>();
            Assert.IsNotNull(category, hook.Name + " has no [HarmonyPatchCategory]: it would never apply.");
            Assert.AreEqual(HallSeatsModule.PatchCategory, category.info.category);
        }
    }

    [TestMethod]
    public void TheModule_RegistersTheServiceTheAdapterAndTheSwitch()
    {
        var container = BuildContainer(new HallSeatsModule());

        Assert.IsNotNull(container.Resolve<HallSeatsService>());
        Assert.IsNotNull(container.Resolve<HallSeatsSettingsProvider>());
        Assert.IsInstanceOfType(container.Resolve<IHallSeatsAdapter>(), typeof(HallSeatsAdapter));
        Assert.AreSame(container.Resolve<HallSeatsService>(), container.Resolve<HallSeatsService>(), "one pass state per process");
    }

    [TestMethod]
    public void InitializeStatics_GivesBothHooksTheSameService()
    {
        var module = new HallSeatsModule();
        var container = BuildContainer(module);

        module.InitializeStatics(container);
        try
        {
            var service = container.Resolve<HallSeatsService>();
            Assert.AreSame(service, StaticField(typeof(MissionAgentHandler_SpawnLocationCharacters_HallSeats_Patch), "_service"));
            Assert.AreSame(service, StaticField(typeof(MissionAgentHandler_SpawnDefaultLocationCharacter_HallSeats_Patch), "_service"));
            Assert.IsNotNull(StaticField(typeof(MissionAgentHandler_SpawnLocationCharacters_HallSeats_Patch), "_settings"));
        }
        finally
        {
            MissionAgentHandler_SpawnLocationCharacters_HallSeats_Patch.Initialize(null, null, null);
            MissionAgentHandler_SpawnDefaultLocationCharacter_HallSeats_Patch.Initialize(null, null);
        }
    }

    [TestMethod]
    public void TheSwitch_ShipsOn_AndReadsOnBeforeMcmIsUp()
    {
        Assert.IsTrue(new TaomSettings().EnableHallSeats, "Hall Seats ships on.");
        Assert.IsTrue(HallSeatsSettingsProvider.From(null), "Before MCM builds its settings the compiled default (on) applies.");
        Assert.IsFalse(HallSeatsSettingsProvider.From(new TaomSettings { EnableHallSeats = false }));
    }

    [TestMethod]
    public void LordSpotTags_AreVanillasLordLadder()
    {
        CollectionAssert.AreEqual(new[] { "sp_notable", "npc_common_limited", "npc_common" }, HallSeatsAdapter.LordSpotTags);
        Assert.AreEqual("lordshall", HallSeatsAdapter.LordsHallId);
    }

    [TestMethod]
    public void FreeLordSpots_SumsOnlyTheLadderTags()
    {
        var unused = new System.Collections.Generic.Dictionary<string, int>
        {
            ["sp_notable"] = 4, ["npc_common_limited"] = 2, ["npc_common"] = 3, ["sp_guard"] = 9, ["sp_throne"] = 1,
        };

        Assert.AreEqual(9, HallSeatsAdapter.FreeLordSpots(unused));
        Assert.AreEqual(0, HallSeatsAdapter.FreeLordSpots(new System.Collections.Generic.Dictionary<string, int>()));
    }

    private static object? StaticField(System.Type type, string name) =>
        type.GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null);
}
