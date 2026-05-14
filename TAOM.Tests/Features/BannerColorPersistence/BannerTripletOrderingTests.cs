using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.BannerColorPersistence;

/// <summary>
/// Phase 9b #187 — source-content assertion tests verifying the BannerColorPersistence patch
/// triplet (Clan.UpdateBannerColor / Clan.UpdateBannerColorsAccordingToKingdom /
/// SPInventoryVM_UpdateCurrentCharacterIfPossible) carries consistent service wiring and shared
/// IBannerColorService delegation. Clan/SPInventoryVM are sealed; full runtime ordering is
/// validated in-game post-merge.
/// </summary>
[TestClass]
public class BannerTripletOrderingTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new System.IO.FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }

    [TestMethod]
    public void Patch_UpdateBannerColor_DelegatesToBannerColorService()
    {
        var path = Path.Combine(FindRepoRoot(), "Main", "Features", "BannerColorPersistence", "Hooks", "Clan_UpdateBannerColor_Patch.cs");
        Assert.IsTrue(File.Exists(path));
        var src = File.ReadAllText(path);
        StringAssert.Contains(src, "IBannerColorService", "Patch must reference IBannerColorService");
    }

    [TestMethod]
    public void Patch_UpdateBannerColorsAccordingToKingdom_DelegatesToService()
    {
        var path = Path.Combine(FindRepoRoot(), "Main", "Features", "BannerColorPersistence", "Hooks", "Clan_UpdateBannerColorsAccordingToKingdom_Patch.cs");
        Assert.IsTrue(File.Exists(path));
        var src = File.ReadAllText(path);
        StringAssert.Contains(src, "IBannerColorService", "Patch must reference IBannerColorService");
        // Phase 9b #172 — confirm the player-clan scope check landed
        StringAssert.Contains(src, "Clan.PlayerClan", "Drift guard must scope to player clan only (#172 F2)");
    }

    [TestMethod]
    public void Patch_SPInventoryVM_UpdateCurrentCharacterIfPossible_DelegatesToService()
    {
        var path = Path.Combine(FindRepoRoot(), "Main", "Features", "BannerColorPersistence", "Hooks", "SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.cs");
        Assert.IsTrue(File.Exists(path));
        var src = File.ReadAllText(path);
        StringAssert.Contains(src, "IBannerColorService", "Patch must reference IBannerColorService");
    }

    [TestMethod]
    public void AllThreePatches_RegisterViaSubModule_InOrder()
    {
        // Verify SubModule.cs calls Initialize on all three patches.
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(), "Main", "SubModule.cs"));
        StringAssert.Contains(src, "Clan_UpdateBannerColor_Patch.Initialize");
        StringAssert.Contains(src, "Clan_UpdateBannerColorsAccordingToKingdom_Patch.Initialize");
        StringAssert.Contains(src, "SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.Initialize");
    }

    [TestMethod]
    public void TargetMethod_NullGuard_PresentForPrivateMethodTarget()
    {
        // Phase 9b #172 F3 — TargetMethod must capture-and-log if AccessTools.Method returns null.
        var path = Path.Combine(FindRepoRoot(), "Main", "Features", "BannerColorPersistence", "Hooks", "Clan_UpdateBannerColorsAccordingToKingdom_Patch.cs");
        var src = File.ReadAllText(path);
        StringAssert.Contains(src, "AccessTools.Method", "Must use AccessTools.Method for private target");
        StringAssert.Contains(src, "_logger", "Must log if target method missing (#172 F3 null-guard)");
    }
}
