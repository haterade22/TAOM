using System;
using System.Linq;
using TAOM.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.CustomBattle;
using TaleWorlds.MountAndBlade.CustomBattle.CustomBattle;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.ShaderPrecompilation;

// Not IoC-managed — instantiated manually: MBGameManager.StartNewGame(new TaomShaderGameManager(...))
// Extends CustomGameManager so Bannerlord loads the CustomBattle module data, which CustomBattleHelper.StartGame() requires.
// Call chain: GameLoadingState.OnTick() -> DoLoadingForGameManager() completes -> OnLoadFinished()
//   base.OnLoadFinished() sets IsLoaded=true then pushes CustomBattleState
//   we then call CustomBattleHelper.StartGame() which opens the mission on top of it
// Research: CustomGameManager (TaleWorlds.MountAndBlade.CustomBattle.dll), confirmed v1.3.12
public class TaomShaderGameManager : CustomGameManager
{
    private const int MaxTroopsPerSide = 500;
    private const string BattleScene = CustomBattleData.CoreContentDefaultSceneName;

    // Set when the shader battle begins loading; used by LoadingScreen_ShaderProgress_Patch
    // to enable stuck detection and auto-abort only during TAOM shader precompilation.
    public static bool IsShaderBattleActive { get; private set; }

    public static void ResetShaderBattleActive() => IsShaderBattleActive = false;

    private readonly IShaderPrecompilationService _service;
    private readonly IModLogger _logger;

    public TaomShaderGameManager(IShaderPrecompilationService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public override void OnLoadFinished()
    {
        base.OnLoadFinished();

        try
        {
            _logger.LogInfo("[ShaderPrecompilation] Starting shader pre-compilation battle");
            IsShaderBattleActive = true;
            var data = BuildBattleData();
            CustomBattleHelper.StartGame(data);
        }
        catch (Exception ex)
        {
            IsShaderBattleActive = false;
            _logger.LogError($"[ShaderPrecompilation] Failed to start shader battle: {ex.Message}");
        }
    }

    private CustomBattleData BuildBattleData()
    {
        var characterIds = _service.GetCharacterIdsForShaderBattle();
        var cultureIds = _service.GetCultureIdsForShaderBattle();

        _logger.LogInfo($"[ShaderPrecompilation] {characterIds.Count} characters from {cultureIds.Count} cultures");

        var firstCulture = cultureIds
            .Select(id => MBObjectManager.Instance?.GetObject<BasicCultureObject>(id))
            .FirstOrDefault(c => c != null);

        if (firstCulture == null)
        {
            _logger.LogWarning("[ShaderPrecompilation] No valid culture found, falling back to 'empire'");
            firstCulture = MBObjectManager.Instance?.GetObject<BasicCultureObject>("empire")
                           ?? throw new InvalidOperationException("No fallback culture available");
        }

        var playerChar = characterIds
            .Select(id => MBObjectManager.Instance?.GetObject<BasicCharacterObject>(id))
            .FirstOrDefault(c => c != null);

        if (playerChar == null)
            throw new InvalidOperationException("[ShaderPrecompilation] No player character resolved");

        var banner = Banner.CreateRandomBanner();

        var playerParty = new CustomBattleCombatant(
            new TextObject("{=!}TAOM Shader Player"),
            firstCulture,
            banner);
        playerParty.Side = BattleSideEnum.Attacker;
        playerParty.SetGeneral(playerChar);

        var enemyParty = new CustomBattleCombatant(
            new TextObject("{=!}TAOM Shader Enemy"),
            firstCulture,
            banner);
        enemyParty.Side = BattleSideEnum.Defender;

        int addedToPlayer = 0;
        int addedToEnemy = 0;

        foreach (var id in characterIds)
        {
            var obj = MBObjectManager.Instance?.GetObject<BasicCharacterObject>(id);
            if (obj == null) continue;

            if (addedToPlayer <= addedToEnemy && addedToPlayer < MaxTroopsPerSide)
            {
                playerParty.AddCharacter(obj, 1);
                addedToPlayer++;
            }
            else if (addedToEnemy < MaxTroopsPerSide)
            {
                enemyParty.AddCharacter(obj, 1);
                addedToEnemy++;
            }
        }

        if (enemyParty.NumberOfAllMembers == 0)
            enemyParty.AddCharacter(playerChar, 1);

        _logger.LogInfo($"[ShaderPrecompilation] Player side: {addedToPlayer}, Enemy side: {addedToEnemy}");

        return new CustomBattleData
        {
            GameTypeStringId = "Battle",
            SceneId = BattleScene,
            SeasonId = "spring",
            PlayerCharacter = playerChar,
            PlayerSideGeneralCharacter = playerChar,
            PlayerParty = playerParty,
            EnemyParty = enemyParty,
            IsPlayerGeneral = true,
            IsPlayerAttacker = true,
            SceneLevel = "",
            TimeOfDay = 6f
        };
    }
}
