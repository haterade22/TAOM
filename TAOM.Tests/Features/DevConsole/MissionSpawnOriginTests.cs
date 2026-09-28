using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.DevConsole.Cheats;
using TAOM.Tests.Migration;

// taom.spawn_troops in Custom Battle (#692): Custom Battle registers NPCCharacter as BasicCharacterObject
// (v1.5.3 CustomGame.cs:135), not the campaign's CharacterObject (Campaign.cs:1558), so the command's
// GetObject<CharacterObject> found no troop at all there, and SimpleAgentOrigin hard-casts to
// CharacterObject (SimpleAgentOrigin.cs:136). Outside a campaign the origin must be BasicBattleAgentOrigin.

namespace TAOM.Tests.Features.DevConsole;

[TestClass]
[TestCategory("RequiresGame")]
public class MissionSpawnOriginTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void CreateOrigin_NonCampaignCharacter_UsesBasicBattleAgentOrigin()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var origin = MissionSpawnCheats.CreateOrigin(new BasicCharacterObject());

        Assert.IsInstanceOfType(origin, typeof(BasicBattleAgentOrigin));
    }

    [TestMethod]
    public void CreateOrigin_CampaignCharacter_KeepsSimpleAgentOrigin()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var origin = MissionSpawnCheats.CreateOrigin(new TaleWorlds.CampaignSystem.CharacterObject());

        Assert.IsInstanceOfType(origin, typeof(TaleWorlds.CampaignSystem.AgentOrigins.SimpleAgentOrigin),
            "a campaign troop keeps the campaign origin, which its party and XP code expect");
    }
}
