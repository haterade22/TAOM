using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Core.Diagnostics;
using TAOM.Features.LoadTimeStamps;
using TAOM.Features.XmlMerge;

namespace TAOM.Tests.Core.Diagnostics;

/// <summary>
/// ShieldExclusionCheck answers "does the loaded TAOM.Dependencies.dll keep PatchShield off these engine
/// methods?" without ever throwing, including against a Dependencies DLL too old to have the question (#371).
/// </summary>
[TestClass]
public class ShieldExclusionCheckTests
{
    private static readonly IReadOnlyList<(string Type, string Method)> Targets = new[]
    {
        ("Engine.TypeA", "First"),
        ("Engine.TypeB", "Second"),
    };

    [TestMethod]
    public void Problem_EveryTargetExcluded_IsNull()
    {
        Assert.IsNull(ShieldExclusionCheck.Problem(Targets, (_, _) => true));
    }

    [TestMethod]
    public void Problem_ATargetNotExcluded_NamesItAndThePairingRule()
    {
        var problem = ShieldExclusionCheck.Problem(Targets, (type, _) => type != "Engine.TypeB");

        StringAssert.Contains(problem, "Engine.TypeB.Second");
        StringAssert.Contains(problem, "#371");
    }

    [TestMethod]
    public void Problem_AnOlderDependenciesWithoutTheMethod_SaysSoInsteadOfThrowing()
    {
        var problem = ShieldExclusionCheck.Problem(Targets,
            (_, _) => throw new MissingMethodException("PatchShieldPolicy", "IsExcludedTargetMethod"));

        StringAssert.Contains(problem, "IsExcludedTargetMethod");
        StringAssert.Contains(problem, "#371");
    }

    [TestMethod]
    public void Problem_AnyOtherFault_IsReportedNotThrown()
    {
        var problem = ShieldExclusionCheck.Problem(Targets, (_, _) => throw new InvalidOperationException("boom"));

        StringAssert.Contains(problem, "InvalidOperationException");
    }

    // The pairing pin: the TAOM.Dependencies this repo builds keeps PatchShield off every load-time stamp target.
    [TestMethod]
    public void Problem_AgainstTheBuiltDependencies_TheLoadTimeStampTargetsAreExcluded()
    {
        Assert.IsNull(ShieldExclusionCheck.Problem(LoadTimeStampsModule.ShieldTargets));
    }

    [TestMethod]
    public void Problem_AgainstTheBuiltDependencies_TheXmlMergeTargetIsExcluded()
    {
        Assert.IsNull(ShieldExclusionCheck.Problem(XmlMergeModule.ShieldTargets));
    }
}
