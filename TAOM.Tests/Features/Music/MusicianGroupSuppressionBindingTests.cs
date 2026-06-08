using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Engine;
using TAOM.Features.Music.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Music;

[TestClass]
public class MusicianGroupSuppressionBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ExplicitMusicianGroupPatches_BindExactV145Targets()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var settlementMusicData = ResolveType("SandBox.Objects.SettlementMusicData");
        Assert.IsNotNull(settlementMusicData, "SandBox.Objects.SettlementMusicData is missing.");

        AssertTarget(
            MusicianGroup_SetPlayList_Patch.TargetMethod(),
            "SetPlayList",
            isPublic: true,
            typeof(void),
            typeof(List<>).MakeGenericType(settlementMusicData));
        AssertTarget(
            MusicianGroup_CheckNewTrackStart_Patch.TargetMethod(),
            "CheckNewTrackStart",
            isPublic: false,
            typeof(void));
        AssertTarget(
            MusicianGroup_CheckTrackEnd_Patch.TargetMethod(),
            "CheckTrackEnd",
            isPublic: false,
            typeof(void));
        AssertTarget(
            MusicianGroup_SetupInstruments_Patch.TargetMethod(),
            "SetupInstruments",
            isPublic: false,
            typeof(void));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MusicianGroup_HasTrackEventFieldForVanillaReleaseAdapter()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var musicianGroup = ResolveType("SandBox.Objects.Usables.MusicianGroup");
        Assert.IsNotNull(musicianGroup, "SandBox.Objects.Usables.MusicianGroup is missing.");

        var field = musicianGroup.GetField("_trackEvent", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(field, "MusicianGroup._trackEvent is missing.");
        Assert.AreEqual(typeof(SoundEvent), field.FieldType, "MusicianGroup._trackEvent must remain a SoundEvent.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SoundEvent_HasExpectedV145TavernControlApi()
    {
        AssertMethod(nameof(SoundEvent.GetEventIdFromString), typeof(int), isStatic: true, typeof(string));
        AssertMethod(nameof(SoundEvent.CreateEvent), typeof(SoundEvent), isStatic: true, typeof(int), typeof(Scene));
        AssertMethod(nameof(SoundEvent.Play), typeof(bool), isStatic: false);
        AssertMethod(nameof(SoundEvent.Stop), typeof(void), isStatic: false);
        AssertMethod(nameof(SoundEvent.Release), typeof(void), isStatic: false);
        AssertMethod(nameof(SoundEvent.IsPlaying), typeof(bool), isStatic: false);
    }

    private static void AssertTarget(
        MethodBase method,
        string name,
        bool isPublic,
        Type returnType,
        params Type[] parameterTypes)
    {
        Assert.IsNotNull(method, $"MusicianGroup.{name} did not resolve.");
        Assert.AreEqual("SandBox.Objects.Usables.MusicianGroup", method.DeclaringType?.FullName);
        Assert.AreEqual(name, method.Name);
        Assert.AreEqual(isPublic, method.IsPublic, $"MusicianGroup.{name} has unexpected visibility.");

        var methodInfo = method as MethodInfo;
        Assert.IsNotNull(methodInfo, $"MusicianGroup.{name} should resolve to a method.");
        Assert.AreEqual(returnType, methodInfo.ReturnType, $"MusicianGroup.{name} has unexpected return type.");

        var actualParameters = method.GetParameters().Select(p => p.ParameterType).ToArray();
        CollectionAssert.AreEqual(parameterTypes, actualParameters, $"MusicianGroup.{name} has unexpected parameters.");
    }

    private static Type ResolveType(string fullName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(fullName, throwOnError: false))
            .FirstOrDefault(t => t != null);
    }

    private static void AssertMethod(string name, Type returnType, bool isStatic, params Type[] parameterTypes)
    {
        var method = typeof(SoundEvent).GetMethod(name, parameterTypes);

        Assert.IsNotNull(method, $"SoundEvent.{name}({string.Join(", ", parameterTypes.Select(t => t.Name))}) is missing.");
        Assert.AreEqual(returnType, method.ReturnType, $"SoundEvent.{name} has unexpected return type.");
        Assert.AreEqual(isStatic, method.IsStatic, $"SoundEvent.{name} has unexpected static/instance binding.");
    }
}
