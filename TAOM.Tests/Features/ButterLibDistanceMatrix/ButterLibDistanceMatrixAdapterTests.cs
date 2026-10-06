using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;

namespace TAOM.Tests.Features.ButterLibDistanceMatrix;

/// <summary>
/// The by-name switch on ButterLib's DistanceMatrixSubSystem (#740), over stand-in types with the same shape as
/// ButterLib's: static Instance, IsEnabled, and a Disable() that does nothing once a campaign has started.
/// </summary>
[TestClass]
public class ButterLibDistanceMatrixAdapterTests
{
    public class StandInSubSystem
    {
        public static StandInSubSystem? Instance { get; set; }
        public bool IsEnabled { get; set; } = true;
        public bool GameInitialized { get; set; }
        public int DisableCalls { get; private set; }

        public void Disable()
        {
            DisableCalls++;
            if (IsEnabled && !GameInitialized) IsEnabled = false;
        }
    }

    public class NoDisableSubSystem
    {
        public static NoDisableSubSystem? Instance { get; set; } = new();
        public bool IsEnabled { get; set; } = true;
    }

    public class InstanceFieldSubSystem
    {
        public static InstanceFieldSubSystem? Instance = new();
        public bool IsEnabled { get; set; } = true;
        public void Disable() => IsEnabled = false;
    }

    public class IntEnabledSubSystem
    {
        public static IntEnabledSubSystem? Instance { get; set; } = new();
        public int IsEnabled { get; set; } = 1;
        public void Disable() => IsEnabled = 0;
    }

    private static string? Disable(Type? type, out bool wasAlreadyOff) =>
        new ButterLibDistanceMatrixAdapter(_ => type).TryDisable(out wasAlreadyOff);

    [TestInitialize]
    public void Setup() => StandInSubSystem.Instance = new StandInSubSystem();

    [TestMethod]
    public void TryDisable_Enabled_SwitchesItOff()
    {
        Assert.IsNull(Disable(typeof(StandInSubSystem), out var wasAlreadyOff));

        Assert.IsFalse(wasAlreadyOff);
        Assert.IsFalse(StandInSubSystem.Instance!.IsEnabled);
        Assert.AreEqual(1, StandInSubSystem.Instance.DisableCalls);
    }

    [TestMethod]
    public void TryDisable_AlreadyOff_ReportsItWithoutCallingDisable()
    {
        StandInSubSystem.Instance!.IsEnabled = false;

        Assert.IsNull(Disable(typeof(StandInSubSystem), out var wasAlreadyOff));

        Assert.IsTrue(wasAlreadyOff);
        Assert.AreEqual(0, StandInSubSystem.Instance.DisableCalls);
    }

    [TestMethod]
    public void TryDisable_CampaignAlreadyStarted_ReportsThatDisableDidNotTake()
    {
        StandInSubSystem.Instance!.GameInitialized = true;

        var problem = Disable(typeof(StandInSubSystem), out _);

        StringAssert.Contains(problem, "did not take");
        Assert.IsTrue(StandInSubSystem.Instance.IsEnabled);
    }

    [TestMethod]
    public void TryDisable_TypeMissing_ReportsNotLoaded()
    {
        StringAssert.Contains(Disable(null, out _), "not loaded");
    }

    [TestMethod]
    public void TryDisable_NoInstance_ReportsIt()
    {
        StandInSubSystem.Instance = null;

        StringAssert.Contains(Disable(typeof(StandInSubSystem), out _), "no instance");
    }

    [TestMethod]
    public void ShapeProblem_InstanceIsAField_ReportsNoStaticInstance()
    {
        StringAssert.Contains(ButterLibDistanceMatrixAdapter.ShapeProblem(typeof(InstanceFieldSubSystem)), "no static Instance");
    }

    [TestMethod]
    public void ShapeProblem_IsEnabledNotBool_ReportsIt()
    {
        StringAssert.Contains(ButterLibDistanceMatrixAdapter.ShapeProblem(typeof(IntEnabledSubSystem)), "no bool IsEnabled");
    }

    [TestMethod]
    public void ShapeProblem_ShippedShape_IsNull()
    {
        Assert.IsNull(ButterLibDistanceMatrixAdapter.ShapeProblem(typeof(StandInSubSystem)));
    }

    [TestMethod]
    public void TryDisable_ShapeChanged_ReportsItAndLeavesItOn()
    {
        var problem = Disable(typeof(NoDisableSubSystem), out _);

        StringAssert.Contains(problem, "shape");
        Assert.IsTrue(NoDisableSubSystem.Instance!.IsEnabled);
    }
}
