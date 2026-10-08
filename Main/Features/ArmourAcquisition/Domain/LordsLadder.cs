using System;
using System.Collections.Generic;

namespace TAOM.Features.ArmourAcquisition.Domain;

/// <summary>
/// The six rungs of the lord's gear ladder (#693), one per equipment slot. The save keys each hero's
/// claimed rungs on these values (<c>ArmourAcquisitionState.LadderClaimed</c>, one bit each): never renumber.
/// </summary>
public enum LadderSlot
{
    Hands = 0,
    Legs = 1,
    Shoulders = 2,
    Head = 3,
    Body = 4,
    Weapon = 5,
}

public static class LadderSlotRules
{
    /// <summary>
    /// A slot name in any case. Never a number or a comma list, which Enum.TryParse alone would take (as
    /// the fourth member, or as the members OR'd together: "legs,shoulders" is Head).
    /// </summary>
    public static bool TryParse(string? raw, out LadderSlot slot)
    {
        slot = default;
        var text = raw?.Trim();
        foreach (var name in Enum.GetNames(typeof(LadderSlot)))
        {
            if (string.Equals(name, text, StringComparison.OrdinalIgnoreCase))
            {
                slot = (LadderSlot)Enum.Parse(typeof(LadderSlot), name);
                return true;
            }
        }
        return false;
    }

    /// <summary>The bit a claimed rung sets in a hero's saved mask.</summary>
    public static int Bit(LadderSlot slot) => 1 << (int)slot;

    /// <summary>Every rung's bit: the most a saved mask can hold.</summary>
    public const int AllBits = (1 << 6) - 1;

    /// <summary>The armour slot a rung rewards; None for the weapon rung.</summary>
    public static ArmourSlot ArmourSlotOf(LadderSlot slot) => slot switch
    {
        LadderSlot.Hands => ArmourSlot.Hand,
        LadderSlot.Legs => ArmourSlot.Leg,
        LadderSlot.Shoulders => ArmourSlot.Cape,
        LadderSlot.Head => ArmourSlot.Head,
        LadderSlot.Body => ArmourSlot.Body,
        _ => ArmourSlot.None,
    };
}

/// <summary>
/// One rung of the ladder: the career-quest definition whose deeds complete it, and how many lord's
/// materials complete it instead when handed to an armourer.
/// </summary>
public sealed class LadderStep
{
    public LadderStep(LadderSlot slot, string questId, int materials)
    {
        Slot = slot;
        QuestId = questId;
        Materials = materials;
    }

    public LadderSlot Slot { get; }

    public string QuestId { get; }

    public int Materials { get; }
}

/// <summary>The main hero's lord's materials against what the current rung asks.</summary>
public sealed class LadderHandIn
{
    public LadderHandIn(string materialId, int needed, int carried)
    {
        MaterialId = materialId;
        Needed = needed;
        Carried = carried;
    }

    public string MaterialId { get; }

    public int Needed { get; }

    public int Carried { get; }

    public bool IsEnough => Carried >= Needed;
}

/// <summary>
/// The chance that a battle the player's side wins yields lord's materials, rising with the enemies the
/// player's hero struck down in it, and how many units a find holds.
/// </summary>
public sealed class MaterialDrop
{
    public MaterialDrop(float baseChance, float chancePerTenKills, float maxChance, int minUnits, int maxUnits)
    {
        BaseChance = baseChance;
        ChancePerTenKills = chancePerTenKills;
        MaxChance = maxChance;
        MinUnits = minUnits;
        MaxUnits = maxUnits;
    }

    public float BaseChance { get; }

    public float ChancePerTenKills { get; }

    public float MaxChance { get; }

    public int MinUnits { get; }

    public int MaxUnits { get; }

    /// <summary>The find chance after a won battle in which the hero struck down <paramref name="kills"/> enemies.</summary>
    public float ChanceFor(int kills) =>
        Math.Min(MaxChance, BaseChance + ChancePerTenKills * (Math.Max(0, kills) / 10));
}

/// <summary>The lord's gear ladder as armour_acquisition_config.xml sets it.</summary>
public sealed class LordsLadderConfig
{
    public LordsLadderConfig(IReadOnlyList<LadderStep> steps, bool countsKnockouts,
        IReadOnlyDictionary<string, string> materials, MaterialDrop drop,
        IReadOnlyDictionary<string, IReadOnlyList<string>> weapons,
        IReadOnlyDictionary<(string Culture, LadderSlot Slot), IReadOnlyList<string>>? pieces = null)
    {
        Steps = steps;
        CountsKnockouts = countsKnockouts;
        Materials = materials;
        Drop = drop;
        Weapons = weapons;
        Pieces = pieces ?? NoPieces;
    }

    private static readonly IReadOnlyDictionary<(string Culture, LadderSlot Slot), IReadOnlyList<string>> NoPieces =
        new Dictionary<(string Culture, LadderSlot Slot), IReadOnlyList<string>>();

    /// <summary>The key of <see cref="Pieces"/>: the culture id without case, and an armour rung's slot.</summary>
    public static (string Culture, LadderSlot Slot) PieceKey(string cultureId, LadderSlot slot) =>
        (cultureId.ToLowerInvariant(), slot);

    /// <summary>The rungs in the order they are climbed.</summary>
    public IReadOnlyList<LadderStep> Steps { get; }

    /// <summary>Whether an enemy the hero knocks out counts as struck down, as well as one killed.</summary>
    public bool CountsKnockouts { get; }

    /// <summary>Culture id to its lord's material item id.</summary>
    public IReadOnlyDictionary<string, string> Materials { get; }

    public MaterialDrop Drop { get; }

    /// <summary>Culture id to the weapons its weapon rung may award, in the order offered.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Weapons { get; }

    /// <summary>
    /// Named pieces a culture's armour rung offers before its ordinary choices, by <see cref="PieceKey"/>; empty
    /// when the config has none. Looked up for the hero's own culture only, never through its armour donor.
    /// </summary>
    public IReadOnlyDictionary<(string Culture, LadderSlot Slot), IReadOnlyList<string>> Pieces { get; }
}
