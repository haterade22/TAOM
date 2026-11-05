using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.PreloadBodyGuard;

/// <summary>
/// Issue #601. <c>HarmonyPatchBindingTests</c> already proves the patch's TARGET resolves. What it
/// cannot see is the field the prefix receives by injection: Harmony binds
/// <c>____uniqueDynamicPhysicsShapeName</c> by name at patch time, and a renamed or retyped field
/// on the next engine bump would fail the category at apply time, which the SubModule catch turns
/// into one log line and a guard that silently no longer guards. Pinning the field here turns that
/// into a red test.
/// </summary>
[TestClass]
public class PreloadBodyGuardBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static Type? Find(string fullName)
        => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => { try { return a.GetType(fullName, false); } catch { return null; } })
            .FirstOrDefault(t => t != null);

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PreloadHelper_KeepsThePrivateBodyNameSetThePrefixIsInjectedWith()
    {
        RequireGame();
        var type = Find("TaleWorlds.MountAndBlade.View.PreloadHelper");
        Assert.IsNotNull(type, "PreloadHelper not found; the wait the guard exists for has moved");

        var field = type!.GetField("_uniqueDynamicPhysicsShapeName", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field,
            "Patch90 receives this field as ____uniqueDynamicPhysicsShapeName. A rename here means the " +
            "category fails to apply and every mission load is unguarded again (#352, #599).");
        Assert.AreEqual(typeof(HashSet<string>), field!.FieldType,
            "the prefix parameter is typed HashSet<string>; a retype means the injection no longer binds");

        var wait = type.GetMethod("WaitForMeshesToBeLoaded", BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(wait, "WaitForMeshesToBeLoaded is the patch target");
        Assert.AreEqual(0, wait!.GetParameters().Length, "the target is parameterless; a new overload needs the attribute revisited");
    }
}
