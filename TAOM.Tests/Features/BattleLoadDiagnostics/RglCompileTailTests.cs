using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleLoadDiagnostics;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

[TestClass]
public class RglCompileTailTests
{
    private const string Line = "[14:20:53.401] compile_shader: $BASE/Sources/pbr.rs, main_vs, vs_5_0, 13, 1032, 4198530, 0.\n";
    private static readonly long T0 = new DateTime(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc).Ticks;
    private static long At(double seconds) => T0 + TimeSpan.FromSeconds(seconds).Ticks;

    private string _dir = null!;
    private string _file = null!;

    [TestInitialize]
    public void Setup()
    {
        _dir = Path.Combine(Path.GetTempPath(), "taom_rgl_tail_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _file = Path.Combine(_dir, "rgl_log_1.txt");
        File.WriteAllText(_file, "");
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private RglCompileTail Tail() => new RglCompileTail(() => _file);

    [TestMethod]
    public void LinesAppendedAfterMarkFresh_AreCounted()
    {
        var sut = Tail();
        sut.MarkFresh();
        File.AppendAllText(_file, Line + Line + "other line\n" + Line);
        sut.ReadNew(At(1));
        Assert.AreEqual(3, sut.CompilesSinceFresh);
        Assert.IsTrue(sut.Available);
    }

    [TestMethod]
    public void LinesBeforeMarkFresh_AreNotCounted()
    {
        File.AppendAllText(_file, Line + Line);
        var sut = Tail();
        sut.MarkFresh();
        sut.ReadNew(At(1));
        Assert.AreEqual(0, sut.CompilesSinceFresh);
        File.AppendAllText(_file, Line);
        sut.ReadNew(At(2));
        Assert.AreEqual(1, sut.CompilesSinceFresh);
    }

    [TestMethod]
    public void MarkerSplitAcrossTwoReads_CountedOnce()
    {
        var sut = Tail();
        sut.MarkFresh();
        File.AppendAllText(_file, "[14:20:53.401] compile_sh");
        sut.ReadNew(At(1));
        File.AppendAllText(_file, "ader: x\n");
        sut.ReadNew(At(2));
        Assert.AreEqual(1, sut.CompilesSinceFresh);
    }

    [TestMethod]
    public void MissingFile_IsUnavailable()
    {
        var sut = new RglCompileTail(() => Path.Combine(_dir, "nope.txt"));
        sut.MarkFresh();
        sut.ReadNew(At(1));
        Assert.IsFalse(sut.Available);

        var unresolved = new RglCompileTail(() => null);
        unresolved.ReadNew(At(1));
        Assert.IsFalse(unresolved.Available);
    }

    [TestMethod]
    public void SecondsSinceLastCompile_NullUntilACompileThenMeasured()
    {
        var sut = Tail();
        sut.MarkFresh();
        sut.ReadNew(At(1));
        Assert.IsNull(sut.SecondsSinceLastCompile(At(1)));

        File.AppendAllText(_file, Line);
        sut.ReadNew(At(10));
        Assert.AreEqual(5d, sut.SecondsSinceLastCompile(At(15))!.Value, 0.001);

        sut.ReadNew(At(20));   // nothing new: the stamp stays on the poll that saw the count rise
        Assert.AreEqual(15d, sut.SecondsSinceLastCompile(At(25))!.Value, 0.001);
    }

    [TestMethod]
    public void OneReadIsCappedAtFourMegabytes()
    {
        var sut = Tail();
        sut.MarkFresh();
        int lines = (5 << 20) / Line.Length;
        var text = new StringBuilder(5 << 20);
        for (int i = 0; i < lines; i++) text.Append(Line);
        File.AppendAllText(_file, text.ToString());

        sut.ReadNew(At(1));
        int afterOne = sut.CompilesSinceFresh;
        Assert.IsTrue(afterOne < lines, $"one read saw {afterOne} of {lines}");
        Assert.IsTrue(afterOne >= ((4 << 20) / Line.Length) - 1, $"one read saw only {afterOne}");

        sut.ReadNew(At(2));
        Assert.AreEqual(lines, sut.CompilesSinceFresh);
    }
}
