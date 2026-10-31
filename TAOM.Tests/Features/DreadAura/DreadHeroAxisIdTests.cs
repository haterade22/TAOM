using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.DreadAura.Hooks;

namespace TAOM.Tests.Features.DreadAura;

/// <summary>
/// The hero-axis id the tracker hands the registry. A campaign agent carries a CharacterObject whose hero (if any) names the id;
/// a Fords of War agent carries a plain BasicCharacterObject with no hero object, so its own StringId is the id (the heroes are
/// NPCCharacter rows keyed by the hero id, which is how the Nine are listed).
/// </summary>
[TestClass]
public class DreadHeroAxisIdTests
{
    [TestMethod]
    public void HeroAxisId_CampaignCharacterWithHero_UsesTheHeroId()
    {
        Assert.AreEqual("lord_1_15", DreadSourceTracker.HeroAxisId(true, "lord_1_15", "troop_template_x"));
    }

    [TestMethod]
    public void HeroAxisId_CampaignCharacterWithoutHero_IsNullNotTheCharacterId()
    {
        // The campaign path must not change: a regular troop has no hero, and its character id must not leak onto the hero axis.
        Assert.IsNull(DreadSourceTracker.HeroAxisId(true, null, "lord_1_15"));
    }

    [TestMethod]
    public void HeroAxisId_PlainCharacter_UsesTheCharacterId()
    {
        Assert.AreEqual("lord_1_48_1", DreadSourceTracker.HeroAxisId(false, null, "lord_1_48_1"));
    }

    [TestMethod]
    public void HeroAxisId_PlainCharacterWithNoId_IsNull()
    {
        Assert.IsNull(DreadSourceTracker.HeroAxisId(false, null, null));
    }
}
