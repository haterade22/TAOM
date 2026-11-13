using TAOM.Core.Logging;
using TAOM.Features.RaceAbilities;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.HeroAuras.Hooks;

/// <summary>
/// The hero auras' mission-time state: the ledger the models read through RaceAbilityHooks, plus the rules and the
/// settings the mission logic needs. A singleton; the mission logic clears it at every mission end.
/// </summary>
public sealed class HeroAuraRuntime
{
    public HeroAuraRuntime(HeroAuraService service, HeroAuraSettingsProvider settings, RaceAbilityService raceAbilities, IModLogger logger,
        IOutcomeLog outcomes)
    {
        Service = service;
        Settings = settings;
        RaceAbilities = raceAbilities;
        Logger = logger;
        Outcomes = outcomes;
    }

    public HeroAuraService Service { get; }

    public HeroAuraSettingsProvider Settings { get; }

    // For the morale top-up arithmetic the race abilities already use.
    public RaceAbilityService RaceAbilities { get; }

    public IModLogger Logger { get; }

    // The [Check] line the in-game verdict reader reads (tools/ingame_checks/1127.toml); main thread only.
    public IOutcomeLog Outcomes { get; }

    public HeroAuraLedger<Agent> Ledger { get; } = new HeroAuraLedger<Agent>();
}
