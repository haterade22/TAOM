using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.MountAndBlade;
using TAOM.Client;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// No TAOM code pauses the vanilla music system. <c>MBMusicManager.PauseMusicManagerSystem</c> sets one process-wide flag, and only the main
/// menu and the end of a video clear it (v1.5.7 MBMusicManager.cs:332, 499-518), so a pause from a mission or the map silences the campaign
/// until the player next reaches the main menu. To quiet vanilla music for one mission, take the silenced handler slot and release it on both
/// teardown hooks, as <c>MissionMusicAdapter</c> does ([music and sound events](docs/reference/engine/music-and-sound-events.md), #1105).
/// The IL scan finds every direct call in the built assemblies, whatever the source spells; the source scan also finds the name in a string,
/// which is how a reflection call would reach it.
/// </summary>
[TestClass]
public class MusicPauseGateTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void NoTaomSource_NamesThePause()
    {
        var pausers = RepoPaths.CSharpFiles()
            .Where(f => RepoPaths.MentionsOutsideComments(f, "PauseMusicManagerSystem", wholeWord: true))
            .Select(Path.GetFileName)
            .ToList();

        Assert.AreEqual(0, pausers.Count, "these files pause all vanilla music for the whole process: " + string.Join(", ", pausers));
    }

    [TestMethod]
    public void NoTaomAssembly_CallsThePause()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        foreach (var assembly in new[] { typeof(SubModule).Assembly, typeof(ClientSubModule).Assembly })
        {
            AssertEveryTypeLoads(assembly);
            var callers = IlCallScanner.FindCallers(assembly, IsPause, out var unreadable, out var scanned);

            Assert.IsTrue(scanned > 0, $"No method bodies scanned in {assembly.GetName().Name}: the scan failed rather than passed.");
            Assert.AreEqual(0, unreadable.Count, $"Unreadable bodies in {assembly.GetName().Name}, so the gate cannot vouch: " + string.Join("; ", unreadable.Take(20)));
            Assert.AreEqual(0, callers.Count, "these methods pause all vanilla music for the whole process: " + string.Join("; ", callers));
        }
    }

    [TestMethod]
    public void TheScan_FindsADirectPause()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var method = IlCallScanner.EnumerateMethods(typeof(PausesMusic).Assembly)
            .SingleOrDefault(m => m.DeclaringType == typeof(PausesMusic) && m.Name == nameof(PausesMusic.Pause));
        Assert.IsNotNull(method, "The scanner's enumeration did not reach a private nested method, so the assembly gate could miss one.");

        var calls = IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody()!.GetILAsByteArray());

        Assert.IsTrue(calls.Any(IsPause), "The scanner did not see a direct pause, so the assembly gate would pass vacuously.");
    }

    private static bool IsPause(MethodBase method) =>
        method.DeclaringType?.FullName == "TaleWorlds.MountAndBlade.MBMusicManager" && method.Name == "PauseMusicManagerSystem";

    // IlCallScanner.EnumerateMethods keeps the types that loaded and drops the rest without a word, so a type that fails to load would pass this gate unscanned.
    private static void AssertEveryTypeLoads(Assembly assembly)
    {
        try
        {
            assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            Assert.Fail($"{ex.LoaderExceptions.Length} types of {assembly.GetName().Name} did not load, so the IL scan cannot vouch for them: "
                + string.Join("; ", ex.LoaderExceptions.Take(5).Select(e => e?.Message)));
        }
    }

    // Never run: the canary that proves the scanner sees the call.
    private sealed class PausesMusic
    {
        public void Pause() => MBMusicManager.Current.PauseMusicManagerSystem();
    }
}
