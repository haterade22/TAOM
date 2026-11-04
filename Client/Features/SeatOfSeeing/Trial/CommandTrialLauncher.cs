using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

/// <summary>Opens the field trial's battle (build-spec section 12): the vanilla custom battle mission with the two armies of <see cref="SeatTrialRoster"/>, refusing a roster id the game does not have.</summary>
internal sealed class CommandTrialLauncher
{
    private const float Noon = 12f;

    private readonly CommandTrialOptions _options;
    private readonly IModLogger _logger;

    internal CommandTrialLauncher(CommandTrialOptions options, IModLogger logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <summary>Null once the mission is open, else the reason the run cannot start (<c>start_refused</c> or <c>start_failed</c>). Never throws.</summary>
    internal string? Launch()
    {
        try
        {
            var objects = MBObjectManager.Instance;
            var missing = SeatTrialRoster.Missing(id => objects.GetObject<BasicCultureObject>(id) != null,
                id => objects.GetObject<BasicCharacterObject>(id) != null);
            if (missing.Count > 0)
            {
                _logger.LogError($"[SeatTrial] refused to start: the game has no [{string.Join(", ", missing)}]");
                return "start_refused";
            }

            var hero = objects.GetObject<BasicCharacterObject>(SeatTrialRoster.Hero);
            Game.Current.PlayerTroop = hero;

            var player = Combatant("{=!}Seat trial, defenders", objects.GetObject<BasicCultureObject>(SeatTrialRoster.PlayerCulture), BattleSideEnum.Defender);
            player.AddCharacter(hero, 1);
            player.SetGeneral(hero);
            Fill(player, SeatTrialRoster.Player, objects);

            var enemy = Combatant("{=!}Seat trial, attackers", objects.GetObject<BasicCultureObject>(SeatTrialRoster.EnemyCulture), BattleSideEnum.Attacker);
            Fill(enemy, SeatTrialRoster.Enemy, objects);

            _logger.LogInfo($"[SeatTrial] launching scene {_options.Scene}: {player.CountOfCharacters} defenders with {SeatTrialRoster.Hero}, {enemy.CountOfCharacters} attackers");
            BannerlordMissions.OpenCustomBattleMission(_options.Scene, hero, player, enemy, true, null, "", "", Noon);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[SeatTrial] opening the mission failed: {ex}");
            return "start_failed";
        }
    }

    private static void Fill(CustomBattleCombatant side, IEnumerable<SeatTrialGroup> groups, MBObjectManager objects)
    {
        foreach (var group in groups)
            side.AddCharacter(objects.GetObject<BasicCharacterObject>(group.Id), group.Count);
    }

    private static CustomBattleCombatant Combatant(string name, BasicCultureObject culture, BattleSideEnum side) =>
        new(new TextObject(name), culture, new Banner(culture.Banner, culture.Color, culture.Color2)) { Side = side };
}
