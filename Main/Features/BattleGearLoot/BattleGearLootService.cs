using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.BattleGearLoot.Domain;
using TAOM.Features.CoopInterop;
using TAOM.Features.TroopProgression;

namespace TAOM.Features.BattleGearLoot;

/// <summary>
/// The player's body loot after a won battle is the gear the beaten enemy wore (docs/features/battle-gear-loot.md).
/// Each eligible piece rolls on its own against the MCM chance. Single-player only.
/// </summary>
public sealed class BattleGearLootService
{
    internal const string CheckId = "taom.battle-gear-loot.applied";

    private readonly IBattleGearLootSettingsProvider _settings;
    private readonly IRandomProvider _random;
    private readonly IArmourAcquisitionConfigProvider _armourConfig;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _dedicated;
    private readonly IOutcomeLog _outcomes;
    private HashSet<string>? _namedWeapons;

    public BattleGearLootService(
        IBattleGearLootSettingsProvider settings,
        IRandomProvider random,
        IArmourAcquisitionConfigProvider armourConfig,
        ICoopSessionProvider coop,
        IDedicatedServerProvider dedicated,
        IOutcomeLog outcomes)
    {
        _settings = settings;
        _random = random;
        _armourConfig = armourConfig;
        _coop = coop;
        _dedicated = dedicated;
        _outcomes = outcomes;
    }

    public bool Applies() =>
        _settings.Get().Enabled && CoopSessionPolicy.IsSinglePlayerAuthority(_coop, _dedicated);

    /// <summary>Rolls and grants this body's gear, or nothing when vanilla would give this body nothing.</summary>
    public void LootCasualty(ICasualtyGearAdapter casualty, string battleKey)
    {
        if (casualty.VanillaGivesNothing)
            return;

        int chance = _settings.Get().ChancePercent;
        var picked = PickSlots(casualty.Pieces, chance);
        if (picked.Count > 0)
            casualty.Grant(picked);

        _outcomes.Pass(CheckId, battleKey, $"chance={chance}");
    }

    internal IReadOnlyList<int> PickSlots(IReadOnlyList<GearPiece> pieces, int chancePercent)
    {
        var picked = new List<int>();
        if (chancePercent <= 0)
            return picked;

        foreach (var piece in pieces)
        {
            if (!GearLootPolicy.IsEligible(piece, IsNamedWeapon))
                continue;
            if (chancePercent >= 100 || _random.Next(100) < chancePercent)
                picked.Add(piece.Slot);
        }

        return picked;
    }

    private bool IsNamedWeapon(string itemId) =>
        (_namedWeapons ??= new HashSet<string>(_armourConfig.GetConfig().NamedWeapons, System.StringComparer.Ordinal)).Contains(itemId);
}
