using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ShaderCompileNotice;

namespace TAOM.Tests.Features.ShaderCompileNotice;

[TestClass]
public class ShaderCompileProgressTests
{
    private static readonly ShaderCompileNoticeTexts Texts = new(
        titleWithTotal: "Compiling shaders: %COUNT% of about %TOTAL%",
        titleCountOnly: "Compiling shaders: %COUNT%",
        detail: "detail");

    private const string Line = "[14:20:53.401] compile_shader: $BASE/Shaders/Sources/pbr_metallic_gbuffer.rs, main_vs, vs_5_0, 13, 1032, 4198530, 0.\n";

    [TestMethod]
    public void Feed_OneChunkWithThreeCompileLines_CountsThree()
    {
        var progress = new ShaderCompileProgress();

        progress.Feed(Line + "[14:20:53.401] Missing shader from sack: pbr_metallic_gbuffer\n" + Line + Line);

        Assert.AreEqual(3, progress.Count);
    }

    [TestMethod]
    public void Feed_MarkerSplitAcrossTwoChunks_CountsItOnce()
    {
        var progress = new ShaderCompileProgress();

        progress.Feed("[14:20:53.401] compile_sh");
        progress.Feed("ader: $BASE/x.rs\n");

        Assert.AreEqual(1, progress.Count);
    }

    [TestMethod]
    public void Feed_ChunkEndingRightAfterAMarker_DoesNotCountItAgainNextTime()
    {
        var progress = new ShaderCompileProgress();

        progress.Feed("x compile_shader");
        progress.Feed(": y\n");

        Assert.AreEqual(1, progress.Count);
    }

    [TestMethod]
    public void Feed_NullOrEmpty_CountsNothing()
    {
        var progress = new ShaderCompileProgress();

        progress.Feed(null);
        progress.Feed("");

        Assert.AreEqual(0, progress.Count);
    }

    [TestMethod]
    public void ShouldShow_BelowTheThreshold_IsFalse()
    {
        var progress = new ShaderCompileProgress();
        for (int i = 0; i < ShaderCompileProgress.ShowAfter - 1; i++) progress.Feed(Line);

        Assert.IsFalse(progress.ShouldShow);
    }

    [TestMethod]
    public void ShouldShow_AtTheThreshold_IsTrue()
    {
        var progress = new ShaderCompileProgress();
        for (int i = 0; i < ShaderCompileProgress.ShowAfter; i++) progress.Feed(Line);

        Assert.IsTrue(progress.ShouldShow);
    }

    [TestMethod]
    public void ShouldRememberTotal_AFewHundredCompiles_IsTrueOnlyAboveTheFloor()
    {
        var progress = new ShaderCompileProgress();
        for (int i = 0; i < ShaderCompileProgress.RememberAfter; i++) progress.Feed(Line);
        Assert.IsFalse(progress.ShouldRememberTotal);

        progress.Feed(Line);

        Assert.IsTrue(progress.ShouldRememberTotal);
    }

    [TestMethod]
    public void Title_WithAKnownTotalAtLeastTheCount_ShowsCountOfTotal()
    {
        var progress = new ShaderCompileProgress();
        progress.Feed(Line + Line);

        Assert.AreEqual("Compiling shaders: 2 of about 1340", progress.Title(Texts, 1340));
    }

    [TestMethod]
    public void Title_WithNoRememberedTotal_ShowsTheCountOnly()
    {
        var progress = new ShaderCompileProgress();
        progress.Feed(Line);

        Assert.AreEqual("Compiling shaders: 1", progress.Title(Texts, 0));
    }

    [TestMethod]
    public void Title_WhenTheCountPassesTheRememberedTotal_ShowsTheCountOnly()
    {
        var progress = new ShaderCompileProgress();
        progress.Feed(Line + Line + Line);

        Assert.AreEqual("Compiling shaders: 3", progress.Title(Texts, 2));
    }

    [TestMethod]
    public void ParseRememberedTotal_APlainNumber_ReturnsIt()
    {
        Assert.AreEqual(1336, ShaderCompileProgress.ParseRememberedTotal(" 1336\r\n"));
    }

    [TestMethod]
    public void ParseRememberedTotal_GarbageNegativeOrAbsurd_ReturnsZero()
    {
        Assert.AreEqual(0, ShaderCompileProgress.ParseRememberedTotal(null));
        Assert.AreEqual(0, ShaderCompileProgress.ParseRememberedTotal("lots"));
        Assert.AreEqual(0, ShaderCompileProgress.ParseRememberedTotal("-5"));
        Assert.AreEqual(0, ShaderCompileProgress.ParseRememberedTotal("0"));
        Assert.AreEqual(0, ShaderCompileProgress.ParseRememberedTotal("999999999"));
    }

    [TestMethod]
    public void ShouldStart_OnAPlayersGameWithoutVanillaTuning_IsTrue()
    {
        Assert.IsTrue(ShaderCompileProgress.ShouldStart(isDedicatedServer: false, new[] { "Native", "TAOM", "TAOM_Map" }));
    }

    [TestMethod]
    public void ShouldStart_OnADedicatedServer_IsFalse()
    {
        Assert.IsFalse(ShaderCompileProgress.ShouldStart(isDedicatedServer: true, new[] { "Native", "TAOM" }));
    }

    [TestMethod]
    public void ShouldStart_WhenVanillaTuningIsActive_IsFalse()
    {
        // VanillaTuning shows the same notice; two windows over the game would be one too many.
        Assert.IsFalse(ShaderCompileProgress.ShouldStart(isDedicatedServer: false, new[] { "Native", "vanillatuning", "TAOM" }));
    }

    [TestMethod]
    public void ShouldStart_WithNoModuleList_IsTrue()
    {
        Assert.IsTrue(ShaderCompileProgress.ShouldStart(isDedicatedServer: false, null));
    }
}
