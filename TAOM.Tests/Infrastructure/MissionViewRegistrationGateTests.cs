using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Issue #970. MissionScreen registers views once, in OnMissionAfterStarting (v1.4.8 MissionScreen.cs:3991-4002), which
/// MissionState.FinishMissionLoading calls before Mission.AfterStart runs the submodules' OnMissionBehaviorInitialize
/// (MissionState.cs:343 and :345, Mission.cs:3815). A MissionView TAOM adds there never ticks or finalizes. The engine
/// builds a [DefaultView] with the mission (ViewCreatorManager.CreateDefaultMissionBehaviors, Activator.CreateInstance),
/// so every TAOM MissionView carries exactly one attribute and a public parameterless constructor.
/// </summary>
[TestClass]
public class MissionViewRegistrationGateTests
{
    // Type full name, reason. A view built by a TAOM-owned [ViewMethod] mission list would go here.
    private static readonly Dictionary<string, string> Allowlist = new Dictionary<string, string>();

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
        var missionView = Assembly.Load("TaleWorlds.MountAndBlade.View")
            .GetType("TaleWorlds.MountAndBlade.View.MissionViews.MissionView", throwOnError: true)!;
        var views = LoadableTypes(typeof(SubModule).Assembly)
            .Where(t => !t.IsAbstract && missionView.IsAssignableFrom(t) && !Allowlist.ContainsKey(t.FullName!))
            .ToList();
        Assert.IsTrue(views.Count >= 2, "expected at least the warg rage controller and the battle action bar");

        var offenders = views
            .Where(t => t.GetCustomAttributesData().Count(a => a.AttributeType.FullName == "TaleWorlds.MountAndBlade.View.DefaultView") != 1
                        || t.GetConstructor(Type.EmptyTypes) == null)
            .Select(t => t.FullName)
            .ToList();
        Assert.AreEqual(0, offenders.Count,
            "These MissionViews lack exactly one [DefaultView] or a public parameterless constructor: " +
            string.Join(", ", offenders) + ". A view added in OnMissionBehaviorInitialize never ticks (#970). " +
            "Mark it [DefaultView], or list it in Allowlist with the reason.");
    }
}
