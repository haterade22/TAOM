using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using EngineMusic = TaleWorlds.Engine.Music;

namespace TAOM.Tests.Features.Music;

[TestClass]
public class MusicEngineAdapterBindingTests
{
    [TestMethod]
    public void TaleWorldsMusic_HasExpectedV145StaticApi()
    {
        AssertMethod(nameof(EngineMusic.GetFreeMusicChannelIndex), typeof(int));
        AssertMethod(nameof(EngineMusic.LoadClip), typeof(void), typeof(int), typeof(string));
        AssertMethod(nameof(EngineMusic.UnloadClip), typeof(void), typeof(int));
        AssertMethod(nameof(EngineMusic.IsClipLoaded), typeof(bool), typeof(int));
        AssertMethod(nameof(EngineMusic.PlayMusic), typeof(void), typeof(int));
        AssertMethod(nameof(EngineMusic.PlayDelayed), typeof(void), typeof(int), typeof(int));
        AssertMethod(nameof(EngineMusic.IsMusicPlaying), typeof(bool), typeof(int));
        AssertMethod(nameof(EngineMusic.PauseMusic), typeof(void), typeof(int));
        AssertMethod(nameof(EngineMusic.StopMusic), typeof(void), typeof(int));
        AssertMethod(nameof(EngineMusic.SetVolume), typeof(void), typeof(int), typeof(float));
    }

    [TestMethod]
    public void MusicEngineAdapter_ImplementsEveryInterfaceMethod()
    {
        var adapterMethods = new HashSet<string>(
            typeof(MusicEngineAdapter)
                .GetMethods()
                .Where(m => m.DeclaringType == typeof(MusicEngineAdapter))
                .Select(m => m.Name),
            StringComparer.Ordinal);

        foreach (var method in typeof(IMusicEngineAdapter).GetMethods())
            Assert.IsTrue(adapterMethods.Contains(method.Name), $"MusicEngineAdapter does not implement {method.Name}.");
    }

    private static void AssertMethod(string name, Type returnType, params Type[] parameterTypes)
    {
        var method = typeof(EngineMusic).GetMethod(name, parameterTypes);
        Assert.IsNotNull(method, $"TaleWorlds.Engine.Music.{name}({string.Join(", ", parameterTypes.Select(t => t.Name))}) is missing.");
        Assert.AreEqual(returnType, method.ReturnType, $"Unexpected return type for TaleWorlds.Engine.Music.{name}.");
        Assert.IsTrue(method.IsStatic, $"TaleWorlds.Engine.Music.{name} must remain static.");
    }

}
