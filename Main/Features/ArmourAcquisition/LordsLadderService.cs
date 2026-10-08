using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Features.ArmourAcquisition.Domain;
using TAOM.Features.CultureMarketplace;

namespace TAOM.Features.ArmourAcquisition;

/// <summary>
/// The lord's gear ladder (#693; Mike, 2026-09-28; docs/features/armour-acquisition.md): one rung per slot
/// (hands, legs, shoulders, head, body, weapon), climbed in the order the config lists them. A rung is done
/// by its career quest (the hero's own kills, battles won, lords captured) or by handing an armourer the
/// culture's lord's materials; either way it then waits to be claimed at an armoury of the lord level, where
/// the hero picks the piece. Per hero the state is which slots are claimed and which are done, waiting for
/// their claim (masks, so a config that reorders or drops a rung leaves every hero at their first unclaimed
/// one and a rung's readiness with its own slot); whether a quest is running is the quest manager's to say.
/// A culture with no armour of its own uses its armour donor's (culture_marketplace_config.xml, armour_from)
/// for pieces, weapons and materials.
/// </summary>
public sealed class LordsLadderService
{
    private readonly ArmourAcquisitionState _state;
    private readonly IArmourAcquisitionConfigProvider _config;
    private readonly IArmourGateService _gate;
    private readonly IArmouryPlayerAdapter _player;
    private readonly ICultureMarketplaceConfigProvider _marketplace;

    public LordsLadderService(ArmourAcquisitionState state, IArmourAcquisitionConfigProvider config, IArmourGateService gate,
        IArmouryPlayerAdapter player, ICultureMarketplaceConfigProvider marketplace)
    {
        _state = state;
        _config = config;
        _gate = gate;
        _player = player;
        _marketplace = marketplace;
    }

    private LordsLadderConfig Ladder => _config.GetConfig().Ladder;

    /// <summary>The rung the hero stands on (the first whose slot is unclaimed), or null once every rung is claimed.</summary>
    public LadderStep? CurrentStep(string heroId)
    {
        var claimed = MaskOf(_state.LadderClaimed, heroId);
        return Ladder.Steps.FirstOrDefault(s => (claimed & LadderSlotRules.Bit(s.Slot)) == 0);
    }

    /// <summary>The current rung is done and waits to be claimed.</summary>
    public bool IsReady(string heroId) =>
        CurrentStep(heroId) is { } step && (MaskOf(_state.LadderReady, heroId) & LadderSlotRules.Bit(step.Slot)) != 0;

    /// <summary>The current rung's quest may start: a rung remains, it is not done, and the hero's quest for it is not running.</summary>
    public bool CanStart(string heroId, bool questRunning) =>
        !questRunning && !IsReady(heroId) && CurrentStep(heroId) != null;

    /// <summary>The quest a battle's kills count toward: the current rung's, until the rung is done.</summary>
    public string? QuestToCredit(string heroId) => IsReady(heroId) ? null : CurrentStep(heroId)?.QuestId;

    /// <summary>
    /// A quest ended. Success on its owner's current rung readies the rung for that owner (the hero who took the
    /// quest, even after a Player Switcher change) and returns it; any other quest changes nothing.
    /// </summary>
    public LadderStep? OnQuestEnded(string? ownerHeroId, string? questId, bool success)
    {
        if (!success || ownerHeroId is null || ownerHeroId.Length == 0)
            return null;
        var step = CurrentStep(ownerHeroId);
        if (step == null || step.QuestId != questId || IsReady(ownerHeroId))
            return null;
        MarkReady(ownerHeroId, step.Slot);
        return step;
    }

    /// <summary>The lord's material a culture's heroes use: its own, else its armour donor's, else none.</summary>
    public string? MaterialFor(string? cultureId) => ForCulture(Ladder.Materials, cultureId);

    /// <summary>
    /// The main hero's materials against the current rung; null with no rung left, the rung done, no material,
    /// or nothing the rung could award (a hand-in must never ready a rung that cannot be claimed).
    /// </summary>
    public LadderHandIn? HandInStatus()
    {
        var heroId = _player.HeroId;
        var step = CurrentStep(heroId);
        if (step == null || IsReady(heroId) || MaterialFor(_player.CultureId) is not { } material
            || RewardChoices(step, _player.CultureId).Count == 0)
            return null;
        var carried = _player.ReadInventory().Where(p => p.ItemId == material).Sum(p => p.Count);
        return new LadderHandIn(material, step.Materials, carried);
    }

    /// <summary>Takes the current rung's materials from the party and readies the rung; false, taking nothing, when short.</summary>
    public bool HandIn()
    {
        var heroId = _player.HeroId;
        if (HandInStatus() is not { IsEnough: true } status || CurrentStep(heroId) is not { } step
            || !_player.RemoveItem(status.MaterialId, status.Needed))
            return false;
        MarkReady(heroId, step.Slot);
        return true;
    }

    /// <summary>
    /// What a rung may award a culture's hero. An armour rung: every lord piece of the culture for its slot, else
    /// the culture's single best elite piece there, else its best heavy piece, else any culture's lord pieces for
    /// the slot, with the culture's configured named pieces for the slot (LadderPieces) that are loaded offered
    /// first. The weapon rung: the culture's configured weapons that are loaded.
    /// </summary>
    public IReadOnlyList<string> RewardChoices(LadderStep step, string? cultureId)
    {
        if (step.Slot == LadderSlot.Weapon)
        {
            var weapons = ForCulture(Ladder.Weapons, cultureId) ?? Array.Empty<string>();
            return weapons.Where(id => _gate.GetRecord(id) != null).ToList();
        }

        var named = NamedPieces(step.Slot, cultureId);
        var ordinary = OrdinaryChoices(LadderSlotRules.ArmourSlotOf(step.Slot), cultureId);
        return named.Count == 0 ? ordinary : named.Concat(ordinary).Distinct().ToList();
    }

    // Only the hero's own culture, never its armour donor's: a named piece belongs to one culture's story.
    private IReadOnlyList<string> NamedPieces(LadderSlot step, string? cultureId)
    {
        if (cultureId is null || cultureId.Length == 0
            || !Ladder.Pieces.TryGetValue(LordsLadderConfig.PieceKey(cultureId, step), out var pieces))
            return Array.Empty<string>();
        // Loaded, and fitting the rung's slot: a piece of another slot would settle this one.
        var slot = LadderSlotRules.ArmourSlotOf(step);
        return pieces.Where(id => _gate.GetRecord(id)?.Slot == slot).ToList();
    }

    private IReadOnlyList<string> OrdinaryChoices(ArmourSlot slot, string? cultureId)
    {
        if (Kit(cultureId) is { } kit)
        {
            var lord = _gate.GetPieces(ArmourClass.Lord, kit, slot);
            if (lord.Count > 0)
                return lord;
            if (Best(_gate.GetPieces(ArmourClass.Elite, kit, slot)) is { } elite)
                return new[] { elite };
            if (Best(_gate.GetPieces(ArmourClass.Heavy, kit, slot)) is { } heavy)
                return new[] { heavy };
        }
        return _gate.GetPieces(ArmourClass.Lord, null, slot);
    }

    public bool CanClaimAt(int townLevel) => townLevel >= _config.GetConfig().LordLevel;

    /// <summary>
    /// Fits the main hero with the chosen piece of their ready rung and marks its slot claimed. A piece that is not
    /// one of the rung's choices, or cannot be given, changes nothing.
    /// </summary>
    public bool Claim(string itemId)
    {
        var heroId = _player.HeroId;
        if (!IsReady(heroId) || CurrentStep(heroId) is not { } step
            || !RewardChoices(step, _player.CultureId).Contains(itemId) || !_player.AddPiece(itemId, null, 1))
            return false;
        _state.LadderClaimed[heroId] = MaskOf(_state.LadderClaimed, heroId) | LadderSlotRules.Bit(step.Slot);
        var waiting = MaskOf(_state.LadderReady, heroId) & ~LadderSlotRules.Bit(step.Slot);
        if (waiting == 0)
            _state.LadderReady.Remove(heroId);
        else
            _state.LadderReady[heroId] = waiting;
        return true;
    }

    /// <summary>
    /// After a battle: whether the hero finds their culture's lord's material among the spoils, and how many. Only
    /// a battle won, and only while a rung that takes materials remains, one neither claimed nor done (nothing
    /// spends a material otherwise); the chance rises with the enemies the hero struck down in it
    /// (<see cref="MaterialDrop.ChanceFor"/>).
    /// </summary>
    public (string ItemId, int Units)? RollMaterialDrop(string heroId, bool won, int heroKills, string? cultureId, Random rng)
    {
        var settled = MaskOf(_state.LadderClaimed, heroId) | MaskOf(_state.LadderReady, heroId);
        if (!won || Ladder.Steps.All(s => (settled & LadderSlotRules.Bit(s.Slot)) != 0)
            || MaterialFor(cultureId) is not { } material)
            return null;
        var drop = Ladder.Drop;
        if (rng.NextDouble() >= drop.ChanceFor(heroKills))
            return null;
        return (material, rng.Next(drop.MinUnits, drop.MaxUnits + 1));
    }

    private static int MaskOf(Dictionary<string, int> masks, string heroId) => masks.TryGetValue(heroId, out var mask) ? mask : 0;

    private void MarkReady(string heroId, LadderSlot slot) =>
        _state.LadderReady[heroId] = MaskOf(_state.LadderReady, heroId) | LadderSlotRules.Bit(slot);

    // The culture whose armour stands for this one's: its armour donor when it has one, else itself.
    private string? Kit(string? cultureId)
    {
        if (cultureId is null || cultureId.Length == 0)
            return null;
        return _marketplace.GetArmourDonor(cultureId) is { Length: > 0 } donor ? donor : cultureId;
    }

    // A culture's own entry, else its armour donor's.
    private T? ForCulture<T>(IReadOnlyDictionary<string, T> byCulture, string? cultureId) where T : class
    {
        if (cultureId is null || cultureId.Length == 0)
            return null;
        if (byCulture.TryGetValue(cultureId, out var own))
            return own;
        return _marketplace.GetArmourDonor(cultureId) is { Length: > 0 } donor && byCulture.TryGetValue(donor, out var given)
            ? given
            : null;
    }

    private string? Best(IReadOnlyList<string> pieces) =>
        pieces.OrderByDescending(id => _gate.GetRecord(id)?.Value ?? 0).ThenBy(id => id, StringComparer.Ordinal).FirstOrDefault();
}
