using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.HeroRace;

namespace TAOM.Tests.Features.HeroRace;

/// <summary>
/// Pins the two race-framing configs actually shipped to players.
///
/// <para>Every other test in this feature builds its own JSON in a temp directory, so nothing
/// touched the files players load. <c>validate_moduledata.py</c> does not cover
/// <c>ModuleData/configs</c> either, which means a green suite and a passing validator together
/// said nothing about these two files.</para>
///
/// <para>That matters because the loader is fail-soft by design: a malformed file, or a row with a
/// non-finite or out-of-range offset, is dropped and the race silently falls back to vanilla
/// framing. A typo in a shipped row therefore looks exactly like a race nobody has tuned yet. Same
/// reasoning as <c>ShippedCombatMechanicsConfigTests</c>.</para>
/// </summary>
[TestClass]
public class ShippedRacePositionConfigTests
{
    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static string ConfigDir => Path.Combine(RepoRoot, @"Main\_Module\ModuleData\configs");

    private IModLogger _logger;
    private RacePositionStore _sut;

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ConfigPath.Returns(ConfigDir + Path.DirectorySeparatorChar);
        _logger = Substitute.For<IModLogger>();
        _sut = new RacePositionStore(pathService, _logger);
    }

    [TestMethod]
    public void ShippedConfigs_BothFilesExist()
    {
        Assert.IsTrue(File.Exists(Path.Combine(ConfigDir, "CharacterAvatarPatch.json")),
            "CharacterAvatarPatch.json is missing from ModuleData/configs.");
        Assert.IsTrue(File.Exists(Path.Combine(ConfigDir, "CharacterImagePatch.json")),
            "CharacterImagePatch.json is missing from ModuleData/configs.");
    }

    // The load-bearing assertion. The loader never throws, so a warning is the ONLY signal that a
    // shipped row was rejected.
    [TestMethod]
    public void ShippedConfigs_LoadWithNoDroppedOrCollapsedRows()
    {
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void ShippedAvatarConfig_ResolvesEveryRowItDeclares()
    {
        Assert.IsNotNull(_sut.ResolveAvatar("dwarf"), "dwarf lost its 3D tableau offsets.");
        Assert.IsNotNull(_sut.ResolveAvatarMount("dwarf"), "mount_dwarf lost its 3D tableau offsets.");
        Assert.IsNotNull(_sut.ResolveAvatar("cave_troll"), "cave_troll lost its 3D tableau offsets.");
    }

    [TestMethod]
    public void ShippedImageConfig_ResolvesEveryRowItDeclares()
    {
        Assert.IsNotNull(_sut.ResolveImage("dwarf"), "dwarf lost its 2D portrait offsets.");
    }

    // The two surfaces are tuned independently and are never merged. Pinning the asymmetry stops a
    // future "helpful" fallback from quietly framing 2D portraits with 3D numbers.
    [TestMethod]
    public void ShippedConfigs_DoNotBleedAcrossSurfaces()
    {
        Assert.IsNull(_sut.ResolveImage("cave_troll"),
            "cave_troll has no 2D row; resolving one means the surfaces got merged.");
        Assert.IsNull(_sut.ResolveImage("mount_dwarf"),
            "The 2D portrait path never looks up a mount row.");
    }

    // Every shipped row must survive the same finiteness gate the runtime applies, and stay inside
    // the authored range. A row outside it is dropped at load and the race renders vanilla.
    [TestMethod]
    public void ShippedConfigs_EveryRowIsFiniteAndInRange()
    {
        foreach (var surface in new[] { RacePositionSurface.Avatar, RacePositionSurface.Image })
        {
            foreach (var row in _sut.List(surface))
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(row.Race), $"{surface}: a row has no race name.");
                AssertUsable(surface, row.Race, "Horizontal", row.Horizontal);
                AssertUsable(surface, row.Race, "Vertical", row.Vertical);
                AssertUsable(surface, row.Race, "Zoom", row.Zoom);
            }
        }
    }

    private static void AssertUsable(RacePositionSurface surface, string race, string axis, float value)
    {
        Assert.IsFalse(float.IsNaN(value) || float.IsInfinity(value),
            $"{surface} '{race}' {axis} is not finite.");
        Assert.IsTrue(
            value >= TAOM.Features.HeroRace.Configuration.RacePositionConfigValidator.MinOffset &&
            value <= TAOM.Features.HeroRace.Configuration.RacePositionConfigValidator.MaxOffset,
            $"{surface} '{race}' {axis} = {value} is outside the accepted offset range, so the row "
            + "is dropped at load and the race renders with vanilla framing.");
    }
}
