using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;
using TaleWorlds.Engine.Options;

namespace TAOM.Tests.Features.TextureAdvice;

/// <summary>The engine members the adapter calls, by shape only: none of them is called here.</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class TextureAdviceBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void NativeOptionsGetConfig_IsPublicStaticAndReturnsFloat()
    {
        RequireGame();

        var method = typeof(NativeOptions).GetMethod("GetConfig", BindingFlags.Public | BindingFlags.Static, null,
            new[] { typeof(NativeOptions.NativeOptionsType) }, null);

        Assert.IsNotNull(method, "NativeOptions.GetConfig(NativeOptionsType) did not resolve.");
        Assert.AreEqual(typeof(float), method.ReturnType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void NativeOptionsType_DefinesTextureQuality()
    {
        RequireGame();

        Assert.IsTrue(Enum.IsDefined(typeof(NativeOptions.NativeOptionsType), "TextureQuality"));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void GetOptionsLimit_IsPublicStaticAndReturnsInt()
    {
        RequireGame();

        var method = typeof(NativeSelectionOptionData).GetMethod("GetOptionsLimit", BindingFlags.Public | BindingFlags.Static, null,
            new[] { typeof(NativeOptions.NativeOptionsType) }, null);

        Assert.IsNotNull(method, "NativeSelectionOptionData.GetOptionsLimit(NativeOptionsType) did not resolve.");
        Assert.AreEqual(typeof(int), method.ReturnType);
    }
}
