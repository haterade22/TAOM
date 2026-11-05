using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.SaveLoadDiagnostics;
using TAOM.Features.SaveLoadDiagnostics.Domain;
using TAOM.Features.SaveLoadDiagnostics.Hooks;

namespace TAOM.Tests.Features.SaveLoadDiagnostics;

// LOAD-TIME-03 part 1: a finalizer parameter `MethodBase __originalMethod` makes Harmony's wrapper call
// MethodBase.GetMethodFromHandle on every call, and these two run on every container and header of
// every load. Each target gets a tiny site class with a constant step name; the log text stays the same.
[TestClass]
public class SaveLoadHotFinalizerTests
{
    private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    private const BindingFlags AnyNested = BindingFlags.Public | BindingFlags.NonPublic;

    [TestMethod]
    public void HotLoadFinalizers_DoNotBindOriginalMethod()
    {
        var finalizers = new[] { typeof(ContainerLoadData_Fill_Patch), typeof(HeaderLoadData_Readers_Patch) }
            .SelectMany(holder => new[] { holder }.Concat(holder.GetNestedTypes(AnyNested)))
            .SelectMany(FinalizersOf)
            .ToList();

        Assert.AreEqual(7, finalizers.Count, "4 container sites plus 3 header sites");
        foreach (var finalizer in finalizers)
            Assert.IsFalse(finalizer.GetParameters().Any(p => p.Name == "__originalMethod"),
                $"{finalizer.DeclaringType!.FullName} binds __originalMethod");
    }

    [TestMethod]
    public void ContainerFillSites_EachReportItsOwnStep()
    {
        var svc = Substitute.For<ISaveLoadDiagnosticsService>();
        ContainerLoadData_Fill_Patch.Initialize(svc, Substitute.For<IModLogger>());
        try
        {
            var steps = new List<string>();
            foreach (var site in SitesOf(typeof(ContainerLoadData_Fill_Patch)))
            {
                var step = StepOf(site);
                steps.Add(step);
                FinalizersOf(site).Single().Invoke(null, new object[] { new InvalidOperationException("x"), new object() });
                svc.Received().LogFault(SaveLoadPhase.GraphFault,
                    Arg.Is<string>(d => d.StartsWith("kind=container step=" + step + " ")), Arg.Any<Exception>());
            }

            CollectionAssert.AreEquivalent(ContainerLoadData_Fill_Patch.PatchedMethodNames, steps);
        }
        finally
        {
            ContainerLoadData_Fill_Patch.Initialize(null!, null!);
        }
    }

    [TestMethod]
    public void HeaderReaderSites_EachReportItsOwnStep()
    {
        var svc = Substitute.For<ISaveLoadDiagnosticsService>();
        HeaderLoadData_Readers_Patch.Initialize(svc);
        try
        {
            var steps = new List<string>();
            foreach (var site in SitesOf(typeof(HeaderLoadData_Readers_Patch)))
            {
                var step = StepOf(site);
                steps.Add(step);
                FinalizersOf(site).Single().Invoke(null, new object[] { new InvalidOperationException("x"), new object() });
                svc.Received().LogFault(SaveLoadPhase.GraphFault, "kind=header step=" + step, Arg.Any<Exception>());
            }

            CollectionAssert.AreEquivalent(new[] { "InitialieReaders", "InitialieReaders", "CreateObject" }, steps);
        }
        finally
        {
            HeaderLoadData_Readers_Patch.Initialize(null!);
        }
    }

    private static IEnumerable<MethodInfo> FinalizersOf(Type type) =>
        type.GetMethods(AnyStatic).Where(m => m.GetCustomAttributes(typeof(HarmonyFinalizer), inherit: false).Any());

    private static IEnumerable<Type> SitesOf(Type holder) =>
        holder.GetNestedTypes(AnyNested).Where(t => FinalizersOf(t).Any());

    private static string StepOf(Type site) =>
        (string)site.GetField("Step", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.GetRawConstantValue()!;
}
