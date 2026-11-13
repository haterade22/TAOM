using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// "Constructor injection only, no service locator in services" (csharp-architecture.md). A service, engine or view model gets
/// its dependencies from its constructor; only a boundary class (an entry point, a patch, a client map view or screen) calls
/// <c>IoC.Resolve</c>. This gate fails on any <c>IoC.Resolve</c>, <c>IoC.ResolveAll</c> or <c>IoC.Resolver</c> use outside
/// comments in a file of Main or Client that sits in a <c>Services</c>, <c>Engine</c> or <c>Engines</c> folder or is named
/// <c>*Service.cs</c>, <c>*Engine.cs</c>, <c>*VM.cs</c> or <c>*ViewModel.cs</c>, including a partial file such as
/// <c>FooService.Envoy.cs</c>. No file holds one today, so there is
/// no baseline: the count is zero everywhere.
/// </summary>
[TestClass]
public class ServiceLocatorGateTests
{
    private static readonly Regex Call = new(@"\bIoC\s*\.\s*Resolve(?:All|r)?\b", RegexOptions.Compiled);

    private static readonly HashSet<string> ScopedFolders = new(new[] { "Services", "Engine", "Engines" }, StringComparer.OrdinalIgnoreCase);

    private static readonly Regex ScopedName = new(@"(?:(?i:Service)|VM|ViewModel|Engine)(?:\.\w+)?\.cs$", RegexOptions.Compiled);

    [TestMethod]
    public void NoServiceOrViewModel_CallsIoCResolve()
    {
        var repo = RepoPaths.RepoPath();
        var failures = new List<string>();
        var scanned = 0;
        foreach (var file in RepoPaths.CSharpFiles())
        {
            var relative = file.Substring(repo.Length).TrimStart('\\', '/').Replace('\\', '/');
            if (!InScope(relative))
                continue;

            scanned++;
            var count = Calls(RepoPaths.Text(file));
            if (count > 0)
                failures.Add($"{relative}: {count} IoC.Resolve call(s); take the dependency in the constructor and resolve it in the boundary class");
        }

        Assert.IsTrue(scanned > 100, "the Main and Client services were not found (" + scanned + " files in scope)");
        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    [TestMethod]
    public void Calls_CountResolveResolveAllAndResolverOutsideComments()
    {
        Assert.AreEqual(2, Calls("var a = IoC.Resolve<IFoo>();\nvar b = IoC.ResolveAll<IBar>();"));
        Assert.AreEqual(1, Calls("var a = IoC . Resolve<IFoo>();"));
        Assert.AreEqual(1, Calls("var r = IoC.Resolver;"));
        Assert.AreEqual(0, Calls("// IoC.Resolve<IFoo>()\n/* IoC.Resolve<IFoo>() */ var x = 1;"));
        Assert.AreEqual(0, Calls("var a = Resolver.Resolve<IFoo>();"));
    }

    [DataTestMethod]
    [DataRow("Main/Features/X/Services/Thing.cs", true)]
    [DataRow("Main/Features/X/ThingService.cs", true)]
    [DataRow("Main/Features/X/ThingService.Envoy.cs", true)]
    [DataRow("Main/Features/X/UI/ThingVM.cs", true)]
    [DataRow("Client/Features/X/ThingVM.cs", true)]
    [DataRow("Client/Features/X/ThingViewModel.cs", true)]
    [DataRow("Main/Features/X/Engines/Thing.cs", true)]
    [DataRow("Main/Features/X/ThingEngine.cs", true)]
    [DataRow("Main/Features/X/Hooks/ThingPatch.cs", false)]
    [DataRow("Main/Features/X/ThingBehavior.cs", false)]
    [DataRow("Main/Composition/FeatureModuleHooks.cs", false)]
    public void InScope_MatchesServicesFoldersAndServiceAndVmFiles(string path, bool expected)
    {
        Assert.AreEqual(expected, InScope(path));
    }

    private static bool InScope(string relative)
    {
        var parts = relative.Split('/');
        var name = parts[parts.Length - 1];
        return parts.Take(parts.Length - 1).Any(p => ScopedFolders.Contains(p))
            || ScopedName.IsMatch(name);
    }

    private static int Calls(string source) => Call.Matches(RepoPaths.StripComments(source)).Count;
}
