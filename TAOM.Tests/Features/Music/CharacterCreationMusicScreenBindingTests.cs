using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Music.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Music;

[TestClass]
public class CharacterCreationMusicScreenBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ExplicitCharacterCreationScreenPatches_BindExactV145LifetimeTargets()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        AssertTarget(
            CharacterCreationScreen_OnFrameTick_MusicPatch.TargetMethod(),
            "OnFrameTick",
            typeof(void),
            typeof(float));

        AssertTarget(
            CharacterCreationScreen_OnFinalize_MusicPatch.TargetMethod(),
            "TaleWorlds.Core.IGameStateListener.OnFinalize",
            typeof(void));

        AssertTarget(
            CharacterCreationAmbientSuppressor.ResolveStopSoundMethod(),
            "StopSound",
            typeof(void));

        AssertTarget(
            CharacterCreationCultureVM_ExecuteSelectCulture_MusicPatch.TargetMethod(),
            "TaleWorlds.CampaignSystem.ViewModelCollection.CharacterCreation.CharacterCreationCultureVM",
            "ExecuteSelectCulture",
            typeof(void),
            expectedIsPublic: true);
    }

    private static void AssertTarget(
        MethodBase method,
        string name,
        Type returnType,
        params Type[] parameterTypes)
    {
        AssertTarget(
            method,
            "SandBox.View.CharacterCreation.CharacterCreationScreen",
            name,
            returnType,
            expectedIsPublic: false,
            parameterTypes);
    }

    private static void AssertTarget(
        MethodBase method,
        string declaringType,
        string name,
        Type returnType,
        bool expectedIsPublic,
        params Type[] parameterTypes)
    {
        Assert.IsNotNull(method, $"{declaringType}.{name} did not resolve.");
        Assert.AreEqual(
            declaringType,
            method.DeclaringType?.FullName);
        Assert.AreEqual(name, method.Name);
        Assert.AreEqual(expectedIsPublic, method.IsPublic, $"{declaringType}.{name} has unexpected visibility.");

        var methodInfo = method as MethodInfo;
        Assert.IsNotNull(methodInfo, $"{declaringType}.{name} should resolve to a method.");
        Assert.AreEqual(returnType, methodInfo.ReturnType, $"{declaringType}.{name} has unexpected return type.");

        var actualParameters = method.GetParameters().Select(p => p.ParameterType).ToArray();
        CollectionAssert.AreEqual(parameterTypes, actualParameters, $"{declaringType}.{name} has unexpected parameters.");
    }
}
