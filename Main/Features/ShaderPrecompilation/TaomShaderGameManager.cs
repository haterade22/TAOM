using System;
using System.Linq;
using TAOM.Core.Logging;
using TAOM.Features.ShaderPrecompilation.Domain;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.CustomBattle;
using TaleWorlds.MountAndBlade.CustomBattle.CustomBattle;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.ShaderPrecompilation;

// Loads ONE precompile item's custom battle so its shaders compile:
//   - CharacterBattle: all TAOM/vanilla troops split across both sides on the default scene
//     (compiles character + equipment material shaders).
//   - ScenePass: a minimal battle on the item's actual battle scene (compiles that scene's
//     terrain + forced-atmosphere shaders — the #287 class).
// The ShaderPrecompileRunner chains these: when an item's shaders settle, it EndGame()s and
// StartNewGame()s the next item. Extends CustomGameManager so CustomBattle module data loads.
public class TaomShaderGameManager : CustomGameManager
{
    private const int MaxTroopsPerSide = 3000;
    private const int SoldierCopies = 2;
    private const int HeroCopies = 1;

    private readonly PrecompileItem _item;
    private readonly int _generation;  // echoed back to the runner so a late callback is matched to its item
    private readonly IShaderPrecompilationService _service;
    private readonly IModLogger _logger;

    public TaomShaderGameManager(PrecompileItem item, int generation, IShaderPrecompilationService service, IModLogger logger)
    {
        _item = item;
        _generation = generation;
        _service = service;
        _logger = logger;
    }

    public override void OnLoadFinished()
    {
        base.OnLoadFinished();
        try
        {
            _logger.LogInfo($"[ShaderPrecompilation] Starting item: {_item.Description} (scene={_item.SceneId})");
            var data = _item.Kind == PrecompileItemKind.CharacterBattle
                ? BuildCharacterBattleData()
                : BuildScenePassData(_item.SceneId);
            CustomBattleHelper.StartGame(data);
            ShaderPrecompileRunner.NotifyItemRendering(_generation);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[ShaderPrecompilation] Failed to start item '{_item.Description}': {ex.Message}");
            ShaderPrecompileRunner.NotifyItemFailed(_generation);
        }
    }

    // ---- CharacterBattle: all troops (the original feature's data) ---- //
    private CustomBattleData BuildCharacterBattleData()
    {
        var characterIds = _service.GetCharacterIdsForShaderBattle();
        var cultureIds = _service.GetCultureIdsForShaderBattle();
        _logger.LogInfo($"[ShaderPrecompilation] {characterIds.Count} characters from {cultureIds.Count} cultures");

        var firstCulture = ResolveFirstCulture(cultureIds);
        var playerChar = characterIds
            .Select(id => MBObjectManager.Instance?.GetObject<BasicCharacterObject>(id))
            .FirstOrDefault(c => c != null)
            ?? throw new InvalidOperationException("[ShaderPrecompilation] No player character resolved");

        var banner = Banner.CreateRandomBanner();
        var playerParty = new CustomBattleCombatant(new TextObject("{=!}TAOM Shader Player"), firstCulture, banner) { Side = BattleSideEnum.Attacker };
        playerParty.SetGeneral(playerChar);
        var enemyParty = new CustomBattleCombatant(new TextObject("{=!}TAOM Shader Enemy"), firstCulture, banner) { Side = BattleSideEnum.Defender };

        int addedToPlayer = 0, addedToEnemy = 0, charactersLoaded = 0;
        foreach (var id in characterIds)
        {
            var obj = MBObjectManager.Instance?.GetObject<BasicCharacterObject>(id);
            if (obj == null) continue;
            int copies = obj.IsSoldier ? SoldierCopies : HeroCopies;
            if (addedToPlayer <= addedToEnemy && addedToPlayer + copies <= MaxTroopsPerSide)
            { playerParty.AddCharacter(obj, copies); addedToPlayer += copies; charactersLoaded++; }
            else if (addedToEnemy + copies <= MaxTroopsPerSide)
            { enemyParty.AddCharacter(obj, copies); addedToEnemy += copies; charactersLoaded++; }
        }
        if (enemyParty.NumberOfAllMembers == 0) enemyParty.AddCharacter(playerChar, 1);

        int dropped = characterIds.Count - charactersLoaded;
        _logger.LogInfo($"[ShaderPrecompilation] Loaded {charactersLoaded} characters — player: {addedToPlayer}, enemy: {addedToEnemy}");
        if (dropped > 0) _logger.LogWarning($"[ShaderPrecompilation] {dropped} characters skipped (both sides full at {MaxTroopsPerSide})");

        return MakeBattleData(ShaderPrecompilePlanner.CharacterBattleScene, playerChar, firstCulture, playerParty, enemyParty);
    }

    // ---- ScenePass: minimal battle on the real scene so its terrain/atmosphere shaders compile ---- //
    private CustomBattleData BuildScenePassData(string sceneId)
    {
        var cultureIds = _service.GetCultureIdsForShaderBattle();
        var firstCulture = ResolveFirstCulture(cultureIds);
        var playerChar = _service.GetCharacterIdsForShaderBattle()
            .Select(id => MBObjectManager.Instance?.GetObject<BasicCharacterObject>(id))
            .FirstOrDefault(c => c != null)
            ?? throw new InvalidOperationException("[ShaderPrecompilation] No player character for scene pass");

        var banner = Banner.CreateRandomBanner();
        // A handful of troops per side — enough to render agents, but the point of a scene pass is
        // the scene's own terrain/atmosphere shaders, so we keep it light to move through scenes fast.
        var playerParty = new CustomBattleCombatant(new TextObject("{=!}TAOM Scene Player"), firstCulture, banner) { Side = BattleSideEnum.Attacker };
        playerParty.SetGeneral(playerChar);
        playerParty.AddCharacter(playerChar, 5);
        var enemyParty = new CustomBattleCombatant(new TextObject("{=!}TAOM Scene Enemy"), firstCulture, banner) { Side = BattleSideEnum.Defender };
        enemyParty.AddCharacter(playerChar, 5);

        return MakeBattleData(sceneId, playerChar, firstCulture, playerParty, enemyParty);
    }

    private BasicCultureObject ResolveFirstCulture(System.Collections.Generic.IReadOnlyList<string> cultureIds)
    {
        var c = cultureIds
            .Select(id => MBObjectManager.Instance?.GetObject<BasicCultureObject>(id))
            .FirstOrDefault(x => x != null);
        if (c != null) return c;
        _logger.LogWarning("[ShaderPrecompilation] No valid culture — falling back to 'empire'");
        return MBObjectManager.Instance?.GetObject<BasicCultureObject>("empire")
               ?? throw new InvalidOperationException("No fallback culture available");
    }

    private static CustomBattleData MakeBattleData(string sceneId, BasicCharacterObject playerChar,
        BasicCultureObject culture, CustomBattleCombatant player, CustomBattleCombatant enemy)
        => new CustomBattleData
        {
            GameTypeStringId = "Battle",
            SceneId = sceneId,
            SeasonId = "spring",
            PlayerCharacter = playerChar,
            PlayerSideGeneralCharacter = playerChar,
            PlayerParty = player,
            EnemyParty = enemy,
            IsPlayerGeneral = true,
            IsPlayerAttacker = true,
            SceneLevel = "",
            TimeOfDay = 6f,
        };
}
