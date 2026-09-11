using System;
using System.Collections.Generic;
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

// Loads ONE precompile item's custom battle so its shaders compile. A CharacterBattle is one batch of
// the roster: the engine's CustomBattle preload view hands every character of BOTH combatants to
// PreloadHelper, which compiles every equipment mesh's shaders before the first frame, so coverage is
// roster membership and each character is added exactly once (copies only cost slots; they overflowed
// the old 6,000-slot budget and dropped TAOM's own troops, #560). The player party is the ONE
// designated character and the batch is the enemy: Mission.SpawnTroop flags Game.Current.PlayerTroop
// as the player agent when it spawns on the player side (the engine's own writer of
// Mission.InitialPlayerAgent, dereferenced unconditionally by DeploymentMissionController), and a
// party under 20 entries keeps deployment on the auto-finish path (no Order of Battle screen waiting
// for a click nobody makes). A ScenePass is a minimal battle on the item's real scene (terrain + atmosphere
// shaders, the #287 class). The runner chains the items; this extends CustomGameManager so the
// CustomBattle module data loads.
public class TaomShaderGameManager : CustomGameManager
{
    // Ceiling on the enemy roster. Unreachable at ShaderPrecompilePlanner.DefaultCharacterBatchSize;
    // kept so a future batch-size change cannot silently overflow a combatant.
    private const int MaxEnemyRoster = 3000;
    private const int ScenePassEnemyTroops = 5;

    private readonly PrecompileItem _item;
    private readonly int _generation;  // echoed back to the runner so a late callback is matched to its item
    private readonly IShaderPrecompilationService _service;
    private readonly IModLogger _logger;
    private string _batchLabel = "?";
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
                ? BuildCharacterBattleData(ResolveBatchIds())
                : BuildScenePassData(_item.SceneId);
            CustomBattleHelper.StartGame(data);
            // The deployment fallback guard (ShaderPrecompilePlayerAgentGuard) is added from
            // SubModule.OnMissionBehaviorInitialize (gated on ShaderPrecompileRunner.TryClaimMission),
            // NOT here: at this point Mission.Current is not yet the battle mission, so an
            // AddMissionBehavior call silently no-ops (confirmed in-game 2026-07-10).
            ShaderPrecompileRunner.NotifyItemRendering(_generation);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[ShaderPrecompilation] Failed to start item '{_item.Description}': {ex.Message}");
            ShaderPrecompileRunner.NotifyItemFailed(_generation);
        }
    }

    // Batches 2..B carry their ids. The bootstrap batch (no ids) is the first custom game of the walk
    // and so the first place MBObjectManager exists: it discovers the roster, hands it to the runner
    // (which re-plans the remaining batches) and takes batch 0's share itself.
    private IReadOnlyList<string> ResolveBatchIds()
    {
        if (_item.CharacterIds.Count > 0)
        {
            _batchLabel = $"{_item.BatchIndex + 1}/{_item.BatchCount}";
            return _item.CharacterIds;
        }
        var roster = _service.GetCharacterIdsForShaderBattle();
        ShaderPrecompileRunner.NotifyRosterDiscovered(_generation, roster);
        var slice = ShaderPrecompilePlanner.SliceBatch(roster, 0);
        if (slice.Count == 0) throw new InvalidOperationException("[ShaderPrecompilation] roster discovery returned no characters");
        _batchLabel = $"1/{ShaderPrecompilePlanner.CountBatches(roster.Count)}";
        return slice;
    }

    private CustomBattleData BuildCharacterBattleData(IReadOnlyList<string> ids)
    {
        var resolved = ids.Select(Resolve).Where(c => c != null).ToList();
        var playerChar = resolved.FirstOrDefault(c => c.IsHero) ?? resolved.FirstOrDefault()
            ?? throw new InvalidOperationException($"[ShaderPrecompilation] batch {_batchLabel}: none of its {ids.Count} ids resolved");
        var data = BuildBattle(ShaderPrecompilePlanner.CharacterBattleScene, playerChar, resolved, out int dropped);
        int unresolved = ids.Count - resolved.Count;
        var line = $"[ShaderPrecompilation] batch {_batchLabel}: loaded {resolved.Count} of {ids.Count} ids, player 1, " +
                   $"enemy {data.EnemyParty.NumberOfAllMembers}, {unresolved} unresolved, {dropped} skipped";
        if (unresolved > 0 || dropped > 0) _logger.LogWarning(line); else _logger.LogInfo(line);
        return data;
    }

    // A handful of troops: enough to render agents, but the point of a scene pass is the scene's own
    // terrain/atmosphere shaders, so it stays light to move through scenes fast.
    private CustomBattleData BuildScenePassData(string sceneId)
    {
        var playerChar = _service.GetCharacterIdsForShaderBattle().Select(Resolve).FirstOrDefault(c => c != null)
            ?? throw new InvalidOperationException("[ShaderPrecompilation] No player character for scene pass");
        return BuildBattle(sceneId, playerChar, Enumerable.Repeat(playerChar, ScenePassEnemyTroops).ToList(), out _);
    }

    // Player party = the one designated character; enemy party = `enemies`, each entry once.
    private CustomBattleData BuildBattle(string sceneId, BasicCharacterObject playerChar,
        IReadOnlyList<BasicCharacterObject> enemies, out int dropped)
    {
        var culture = ResolveFirstCulture(_service.GetCultureIdsForShaderBattle());
        var banner = Banner.CreateRandomBanner();
        var playerParty = new CustomBattleCombatant(new TextObject("{=!}TAOM Shader Player"), culture, banner) { Side = BattleSideEnum.Attacker };
        playerParty.SetGeneral(playerChar);
        playerParty.AddCharacter(playerChar, 1);
        var enemyParty = new CustomBattleCombatant(new TextObject("{=!}TAOM Shader Enemy"), culture, banner) { Side = BattleSideEnum.Defender };
        dropped = 0;
        foreach (var c in enemies)
        {
            if (enemyParty.NumberOfAllMembers >= MaxEnemyRoster) { dropped++; continue; }
            enemyParty.AddCharacter(c, 1);
        }
        // CustomBattleCombatant.IsUnderPlayersCommand dereferences General; give the enemy one (a
        // different character when the batch has one) so no engine path finds it null.
        enemyParty.SetGeneral(enemies.FirstOrDefault(c => c != playerChar) ?? playerChar);
        return new CustomBattleData
        {
            GameTypeStringId = "Battle", SceneId = sceneId, SeasonId = "spring", SceneLevel = "", TimeOfDay = 6f,
            PlayerCharacter = playerChar, PlayerSideGeneralCharacter = playerChar,
            PlayerParty = playerParty, EnemyParty = enemyParty,
            IsPlayerGeneral = true, IsPlayerAttacker = true,
        };
    }

    private static BasicCharacterObject Resolve(string id) => MBObjectManager.Instance?.GetObject<BasicCharacterObject>(id);
    private BasicCultureObject ResolveFirstCulture(IReadOnlyList<string> cultureIds)
    {
        var c = cultureIds.Select(id => MBObjectManager.Instance?.GetObject<BasicCultureObject>(id)).FirstOrDefault(x => x != null);
        if (c != null) return c;
        _logger.LogWarning("[ShaderPrecompilation] No valid culture, falling back to 'empire'");
        return MBObjectManager.Instance?.GetObject<BasicCultureObject>("empire")
               ?? throw new InvalidOperationException("No fallback culture available");
    }
}
