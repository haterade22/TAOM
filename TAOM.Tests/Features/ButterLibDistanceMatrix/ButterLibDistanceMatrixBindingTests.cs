using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.ButterLibDistanceMatrix;

/// <summary>
/// Binding gate for the by-name switch on ButterLib's DistanceMatrixSubSystem (#740). The type is internal to
/// <c>Bannerlord.ButterLib.Implementation.*.dll</c>, which ReflectionSiteBindingTests cannot see, so this loads the newest
/// implementation DLL TAOM ships (the repo's tracked copy in Dependencies/_Module, so a ButterLib update fails here before
/// it is deployed) and checks the members the adapter calls.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ButterLibDistanceMatrixBindingTests
{
    private static string? _shapeProblem;
    private static string? _skipReason;
    private static string? _failReason;

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        if (!GameAssemblies.EnsureLoaded())
        {
            _skipReason = "Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics);
            return;
        }

        var folder = RepoPaths.RepoPath("Dependencies/_Module/bin/Win64_Shipping_Client");
        var newest = Directory.Exists(folder)
            ? Directory.GetFiles(folder, "Bannerlord.ButterLib.Implementation.*.dll").OrderByDescending(VersionOf).FirstOrDefault()
            : null;
        if (newest == null)
        {
            // The folder is tracked in git, so an empty one is a botched ButterLib update, not a missing install.
            _failReason = "No Bannerlord.ButterLib.Implementation.*.dll under " + folder;
            return;
        }

        // LoadFrom binds the DLL's own references (Bannerlord.ButterLib.dll) from the same folder.
        var type = Assembly.LoadFrom(newest).GetType(ButterLibDistanceMatrixAdapter.SubSystemTypeName, throwOnError: false);
        _shapeProblem = type == null
            ? ButterLibDistanceMatrixAdapter.SubSystemTypeName + " is missing from " + Path.GetFileName(newest)
            : ButterLibDistanceMatrixAdapter.ShapeProblem(type);
    }

    private static Version VersionOf(string path)
    {
        var text = Path.GetFileNameWithoutExtension(path).Replace("Bannerlord.ButterLib.Implementation.", "");
        return Version.TryParse(text, out var v) ? v : new Version(0, 0);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SubSystem_HasTheMembersTheAdapterCalls()
    {
        if (_skipReason != null) Assert.Inconclusive(_skipReason);
        if (_failReason != null) Assert.Fail(_failReason);

        Assert.IsNull(_shapeProblem);
    }
}
