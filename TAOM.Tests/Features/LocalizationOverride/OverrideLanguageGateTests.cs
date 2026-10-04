using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.LocalizationOverride;

namespace TAOM.Tests.Features.LocalizationOverride;

/// <summary>
/// The English override table is for English only (#706): vanilla reads a translated row for every other
/// language, and an override there would hide it. The language ids below are the ones the engine uses
/// (<c>Languages/language_data.xml</c> and each language folder's <c>language_data.xml</c>).
/// </summary>
[TestClass]
public class OverrideLanguageGateTests
{
    private ITextLocalizerAdapter _localizer = null!;
    private RecordingLogger _log = null!;
    private OverrideLanguageGate _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _localizer = Substitute.For<ITextLocalizerAdapter>();
        _localizer.ActiveLanguage.Returns("English");
        _log = new RecordingLogger();
        _sut = new OverrideLanguageGate(_localizer, () => _log);
    }

    private void SwitchTo(string? language) => _localizer.ActiveLanguage.Returns(language!);

    private void LookUp(int times)
    {
        for (int i = 0; i < times; i++)
            _sut.AllowsOverrides();
    }

    [TestMethod]
    public void AllowsOverrides_English_ReturnsTrue()
    {
        Assert.IsTrue(_sut.AllowsOverrides());
    }

    [DataTestMethod]
    [DataRow("Deutsch")]
    [DataRow("Français")]
    [DataRow("Português (BR)")]
    [DataRow("简体中文")]
    public void AllowsOverrides_NonEnglishLanguage_ReturnsFalse(string language)
    {
        SwitchTo(language);

        Assert.IsFalse(_sut.AllowsOverrides());
    }

    // Vanilla compares the id with == "English" (ordinal), and the gate stands in for that branch.
    [TestMethod]
    public void AllowsOverrides_LanguageIdDiffersFromEnglishInCase_ReturnsFalse()
    {
        SwitchTo("english");

        Assert.IsFalse(_sut.AllowsOverrides());
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow(null)]
    public void AllowsOverrides_NoLanguageId_ReturnsFalseWithoutThrowing(string? language)
    {
        SwitchTo(language);

        Assert.IsFalse(_sut.AllowsOverrides());
    }

    // The language can change while the game runs, so the gate reads it on every call.
    [TestMethod]
    public void AllowsOverrides_LanguageChangesWhileRunning_FollowsTheActiveLanguage()
    {
        Assert.IsTrue(_sut.AllowsOverrides());

        SwitchTo("Deutsch");
        Assert.IsFalse(_sut.AllowsOverrides());

        SwitchTo("English");
        Assert.IsTrue(_sut.AllowsOverrides());

        SwitchTo("Français");
        Assert.IsFalse(_sut.AllowsOverrides());
    }

    [TestMethod]
    public void AllowsOverrides_English_LogsNothing()
    {
        LookUp(50);

        Assert.AreEqual(0, _log.Lines.Count);
    }

    [TestMethod]
    public void AllowsOverrides_NonEnglishManyLookups_LogsOneInfoLineNamingTheLanguage()
    {
        SwitchTo("Deutsch");

        LookUp(1000);

        Assert.AreEqual(1, _log.Lines.Count, string.Join(Environment.NewLine, _log.Lines));
        StringAssert.StartsWith(_log.Lines[0], "INFO ");
        StringAssert.Contains(_log.Lines[0], "[LocalizationOverride]");
        StringAssert.Contains(_log.Lines[0], "Deutsch");
    }

    // The start in English is silent; every later switch writes one line, the return to English included.
    [TestMethod]
    public void AllowsOverrides_EnglishThenGermanThenEnglishThenGerman_LogsEachSwitchButNotTheStart()
    {
        LookUp(3);
        SwitchTo("Deutsch");
        LookUp(3);
        SwitchTo("English");
        LookUp(3);
        SwitchTo("Deutsch");
        LookUp(3);

        Assert.AreEqual(3, _log.Lines.Count, string.Join(Environment.NewLine, _log.Lines));
        StringAssert.Contains(_log.Lines[0], "'Deutsch' is not English");
        StringAssert.Contains(_log.Lines[1], "Text language is English again");
        StringAssert.Contains(_log.Lines[2], "'Deutsch' is not English");
    }

    [TestMethod]
    public void AllowsOverrides_BackToEnglishFromGermanManyLookups_LogsOneInfoLineThatTheOverridesApplyAgain()
    {
        SwitchTo("Deutsch");
        LookUp(3);
        SwitchTo("English");

        LookUp(1000);

        Assert.AreEqual(2, _log.Lines.Count, string.Join(Environment.NewLine, _log.Lines));
        StringAssert.StartsWith(_log.Lines[1], "INFO ");
        StringAssert.Contains(_log.Lines[1], "[LocalizationOverride]");
        StringAssert.Contains(_log.Lines[1], "Text language is English again");
    }

    [TestMethod]
    public void AllowsOverrides_GermanThenFrench_LogsEachLanguageOnce()
    {
        SwitchTo("Deutsch");
        LookUp(5);
        SwitchTo("Français");
        LookUp(5);

        Assert.AreEqual(2, _log.Lines.Count, string.Join(Environment.NewLine, _log.Lines));
        StringAssert.Contains(_log.Lines[0], "Deutsch");
        StringAssert.Contains(_log.Lines[1], "Français");
    }

    // The engine hands out its own string for the language; a second instance with the same id is
    // the same language, not a change.
    [TestMethod]
    public void AllowsOverrides_SameLanguageIdAsAnotherStringInstance_LogsOnce()
    {
        SwitchTo("Deutsch");
        LookUp(3);
        SwitchTo(new string("Deutsch".ToCharArray()));
        LookUp(3);

        Assert.AreEqual(1, _log.Lines.Count, string.Join(Environment.NewLine, _log.Lines));
    }

    // Logging is a courtesy: a container that is not ready, or a logger that throws, must not take a
    // text lookup down, and one failed attempt is the cost, not one per lookup.
    [TestMethod]
    public void AllowsOverrides_LoggerCannotBeResolved_SkipsTheOverridesAndTriesOnce()
    {
        int resolves = 0;
        var sut = new OverrideLanguageGate(_localizer, () =>
        {
            resolves++;
            throw new InvalidOperationException("container not ready");
        });
        SwitchTo("Deutsch");

        for (int i = 0; i < 100; i++)
            Assert.IsFalse(sut.AllowsOverrides());

        Assert.AreEqual(1, resolves);
    }

    // AllowsOverrides never calls the latch step for the language it saw last, so only a direct call
    // reaches the step's own guard. Two threads that both read the old language before either latches the
    // new one reach it the same way, and the guard is what keeps them to one line.
    [TestMethod]
    public void NoteLanguageChange_TheSameLanguageTwice_WritesOneLine()
    {
        _sut.NoteLanguageChange("Deutsch");
        _sut.NoteLanguageChange("Deutsch");

        Assert.AreEqual(1, _log.Lines.Count, string.Join(Environment.NewLine, _log.Lines));
    }

    [TestMethod]
    public void NoteLanguageChange_BackToEnglishTwice_WritesTheEnglishLineOnce()
    {
        _sut.NoteLanguageChange("Deutsch");
        _sut.NoteLanguageChange("English");
        _sut.NoteLanguageChange("English");

        Assert.AreEqual(2, _log.Lines.Count, string.Join(Environment.NewLine, _log.Lines));
    }

    // A smoke test, not the proof: whether two threads race past the first check in a given run is up to
    // the scheduler, so a latch without its guard can still pass this. The NoteLanguageChange tests above
    // pin the guard deterministically.
    [TestMethod]
    public void AllowsOverrides_ManyThreadsOnANonEnglishLanguage_LogsOnce()
    {
        SwitchTo("Deutsch");

        Parallel.For(0, 8000, new ParallelOptions { MaxDegreeOfParallelism = 8 }, _ => _sut.AllowsOverrides());

        Assert.AreEqual(1, _log.Lines.Count, string.Join(Environment.NewLine, _log.Lines));
    }

    private sealed class RecordingLogger : IModLogger
    {
        private readonly ConcurrentQueue<string> _lines = new();

        public IReadOnlyList<string> Lines => _lines.ToList();

        public void LogInfo(string message) => _lines.Enqueue("INFO " + message);
        public void LogDebug(string message) => _lines.Enqueue("DEBUG " + message);
        public void LogWarning(string message) => _lines.Enqueue("WARN " + message);
        public void LogError(string message) => _lines.Enqueue("ERROR " + message);
        public string? LogFilePath => null;
        public void Dispose() { }
    }
}
