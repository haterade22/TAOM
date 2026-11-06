using System.Linq;
using System.Reflection;
using DryIoc;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.AlignmentRecruitment;
using TAOM.Features.AlignmentRecruitment.Hooks;

namespace TAOM.Tests.Features.AlignmentRecruitment;

/// <summary>
/// Wiring regression guard for the AI trip-scoring postfix (#887). Drop the module line and the patch category is never applied;
/// drop the category on the hook and the postfix never attaches.
/// </summary>
[TestClass]
public class AlignmentRecruitmentWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheAlignmentRecruitmentModuleOnce()
        => Assert.AreEqual(1, FeatureModules.All.OfType<AlignmentRecruitmentModule>().Count());

    [TestMethod]
    public void TheModule_IsNamedAlignmentRecruitment_AndIsNotParked_AndOwnsNoSaveData()
    {
        var module = new AlignmentRecruitmentModule();

        Assert.AreEqual("AlignmentRecruitment", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
    }

    [TestMethod]
    public void TheModule_AppliesItsOneCategoryAtProcessLoad()
    {
        var decls = new AlignmentRecruitmentModule().PatchCategories;

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual(AlignmentRecruitmentModule.PatchCategory, decls[0].Category);
        Assert.AreEqual(ApplyPhase.ProcessLoad, decls[0].Phase);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheHook_CarriesTheModulesPatchCategory_AndAPostfix()
    {
        var type = typeof(Patch114_AiVolunteerEstimate_Postfix);

        var categories = type.GetCustomAttributes<HarmonyPatchCategory>().Select(c => c.info.category).ToList();

        CollectionAssert.AreEqual(new[] { AlignmentRecruitmentModule.PatchCategory }, categories);
        Assert.IsTrue(type.GetMethod("Postfix", BindingFlags.Public | BindingFlags.Static)!.GetCustomAttributes<HarmonyPostfix>().Any());
    }

    [TestMethod]
    public void TheModule_AddsNoBehaviorAndNoModel()
    {
        var module = new AlignmentRecruitmentModule();

        Assert.AreEqual(0, module.MissionBehaviors.Count);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.GameModels.Count);
    }

    [TestMethod]
    public void TheModule_InitializesItsStaticsFromTheContainer()
    {
        var service = Substitute.For<IRecruitmentAlignmentService>();
        var logger = Substitute.For<IModLogger>();
        var serviceField = typeof(Patch114_AiVolunteerEstimate_Postfix).GetField("_service", BindingFlags.NonPublic | BindingFlags.Static);
        var loggerField = typeof(Patch114_AiVolunteerEstimate_Postfix).GetField("_logger", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(serviceField, "Patch114_AiVolunteerEstimate_Postfix._service not found.");
        Assert.IsNotNull(loggerField, "Patch114_AiVolunteerEstimate_Postfix._logger not found.");
        try
        {
            using var container = new Container();
            container.RegisterInstance(service);
            container.RegisterInstance(logger);

            new AlignmentRecruitmentModule().InitializeStatics(container);

            // Without this handoff the postfix returns on a null service and the feature is silently off.
            Assert.AreSame(service, serviceField!.GetValue(null));
            Assert.AreSame(logger, loggerField!.GetValue(null));
        }
        finally
        {
            serviceField!.SetValue(null, null);
            loggerField!.SetValue(null, null);
        }
    }
}
