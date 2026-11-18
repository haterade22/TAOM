using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.EngineBugGuards.Hooks;
using TAOM.Tests.Migration;
using TaleWorlds.Engine;

namespace TAOM.Tests.Features.EngineBugGuards;

/// <summary>
/// Patch161 (#1429, from Silas He's v1.5.6 fix package, crash a0dfdff3): <c>SceneTableau.SetTargetSize</c> calls
/// <c>CreateTexture</c> before the widget has a scene, and <c>SceneView.SetScene</c> then throws a
/// NullReferenceException on the null scene (v1.5.7 SceneView.cs:20-23). The guard skips the texture of that throwaway
/// tableau; <c>SceneTextureProvider</c> builds a new tableau when the scene arrives.
/// </summary>
[TestClass]
public class Patch161SceneTableauNullSceneGuardTests
{
    private const string TableauTypeName = "TaleWorlds.MountAndBlade.View.Tableaus.SceneTableau";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static System.Type Tableau()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var type = AccessTools.TypeByName(TableauTypeName);
        Assert.IsNotNull(type, TableauTypeName + " did not resolve against the installed engine; Patch161's target type is gone.");
        return type;
    }

    [TestInitialize]
    public void Setup() => Patch161_SceneTableauNullSceneGuard.Initialize(Substitute.For<IModLogger>());

    [TestMethod]
    public void Prefix_NoSceneYet_SkipsTheTexture()
        => Assert.IsFalse(Patch161_SceneTableauNullSceneGuard.Prefix(null!));

    [TestMethod]
    public void ShouldCreateTexture_FollowsTheScene()
    {
        Assert.IsFalse(Patch161_SceneTableauNullSceneGuard.ShouldCreateTexture(hasScene: false));
        Assert.IsTrue(Patch161_SceneTableauNullSceneGuard.ShouldCreateTexture(hasScene: true));
    }

    [TestMethod]
    public void Prefix_NoScene_WarnsOncePerProcess()
    {
        var logger = Substitute.For<IModLogger>();
        Patch161_SceneTableauNullSceneGuard.Initialize(logger);

        Patch161_SceneTableauNullSceneGuard.Prefix(null!);
        Patch161_SceneTableauNullSceneGuard.Prefix(null!);

        logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SceneTableau_PrivateTargetAndField_ResolveAgainstInstalledEngine()
    {
        var create = AccessTools.DeclaredMethod(Tableau(), "CreateTexture");
        Assert.IsNotNull(create, "SceneTableau.CreateTexture did not resolve; Patch161 binds it by name.");
        Assert.AreEqual(0, create.GetParameters().Length);

        var scene = AccessTools.Field(Tableau(), "_tableauScene");
        Assert.IsNotNull(scene, "SceneTableau._tableauScene did not resolve; Patch161 injects it as ____tableauScene.");
        Assert.AreEqual(typeof(Scene), scene.FieldType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SceneTableau_CreateTexture_IsNotOverloaded()
    {
        var overloads = Tableau()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Count(m => m.Name == "CreateTexture");
        Assert.AreEqual(1, overloads);
    }
}
