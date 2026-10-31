using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Issue #970. MissionScreen registers views once, in OnMissionAfterStarting (MissionScreen.cs:4054-4065), before
/// Mission.AfterStart runs the submodules' OnMissionBehaviorInitialize (Mission.cs:3822-3825). A MissionView TAOM adds
/// there gets OnMissionScreenInitialize with a null screen and never ticks or finalizes. The engine builds a
/// [DefaultView] before that registration, so every TAOM MissionView must carry the attribute.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class MissionViewRegistrationGateTests
{
    // Type full name, reason. A view built by a TAOM-owned [ViewMethod] mission list (the HostReview shape) would go here.
    private static readonly Dictionary<string, string> Allowlist = new Dictionary<string, string>
    {
        ["TAOM.Client.Features.FordsOfWar.FordsCommanderCameraView"] = "built by the [ViewMethod] list of the TaomFordsOfWar mission (FordsOfWarMissionViews)",
        ["TAOM.Client.Features.FordsOfWar.FordsCommandView"] = "built by the [ViewMethod] list of the TaomFordsOfWar mission (FordsOfWarMissionViews)",
    };

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) =>
        TAOM.Tests.Migration.GameAssemblies.EnsureLoaded();

    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
    }

    [TestMethod]
    public void EveryTaomMissionView_IsADefaultView_WithAParameterlessConstructor()
    {
        var viewAssembly = Assembly.Load("TaleWorlds.MountAndBlade.View");
        var missionView = viewAssembly.GetType("TaleWorlds.MountAndBlade.View.MissionViews.MissionView", throwOnError: true)!;

        var assemblies = new[] { typeof(SubModule).Assembly, typeof(ClientSubModule).Assembly };
        var views = assemblies.SelectMany(LoadableTypes)
            .Where(t => !t.IsAbstract && missionView.IsAssignableFrom(t))
            .Where(t => !Allowlist.ContainsKey(t.FullName!))
            .ToList();

        var offenders = views
            .Where(t => t.GetCustomAttributesData().Count(a => a.AttributeType.FullName == "TaleWorlds.MountAndBlade.View.DefaultView") != 1
                        || t.GetConstructor(Type.EmptyTypes) == null)
            .Select(t => t.FullName)
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "These MissionViews lack exactly one [DefaultView] or a public parameterless constructor: " +
            string.Join(", ", offenders) + ". TAOM adds behaviors at " +
            "Mission.cs:3824, after MissionScreen.cs:4054-4065 registered the views, so a view added there never ticks (#970). " +
            "Mark it [DefaultView] in TAOM.Client, or list it in Allowlist with the reason.");
    }
}
