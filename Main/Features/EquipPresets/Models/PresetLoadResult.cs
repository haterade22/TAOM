using System.Collections.Generic;

namespace TAOM.Features.EquipPresets.Models;

/// <summary>
/// Report returned by <see cref="IEquipmentPresetService.LoadPresetWithReport"/>. Captures every
/// failure mode separately so the VM can build an accurate user-facing message
/// (per <c>feedback_user_facing_promise_must_match_code.md</c>) and so tests can assert each path.
/// </summary>
public sealed class PresetLoadResult
{
    public PresetLoadResult(
        int equippedCount,
        IReadOnlyList<string> missingItems,
        IReadOnlyList<string> missingModifiers,
        IReadOnlyList<int> invalidSlots,
        bool disabled,
        bool notFound,
        bool noActiveHero)
    {
        EquippedCount = equippedCount;
        MissingItems = missingItems;
        MissingModifiers = missingModifiers;
        InvalidSlots = invalidSlots;
        Disabled = disabled;
        NotFound = notFound;
        NoActiveHero = noActiveHero;
    }

    public int EquippedCount { get; }
    public IReadOnlyList<string> MissingItems { get; }
    public IReadOnlyList<string> MissingModifiers { get; }

    /// <summary>Slots where the item type doesn't fit the slot (e.g., helmet in a weapon slot
    /// from a tampered save). Adapter ran <see cref="TaleWorlds.Core.Equipment.IsItemFitsToSlot"/>
    /// and rejected the assignment.</summary>
    public IReadOnlyList<int> InvalidSlots { get; }

    public bool Disabled { get; }
    public bool NotFound { get; }
    public bool NoActiveHero { get; }

    public bool HasIssues => MissingItems.Count > 0 || MissingModifiers.Count > 0 || InvalidSlots.Count > 0;

    public static PresetLoadResult ForDisabled() =>
        new(0, EmptyStrings, EmptyStrings, EmptyInts, disabled: true, notFound: false, noActiveHero: false);

    public static PresetLoadResult ForNotFound() =>
        new(0, EmptyStrings, EmptyStrings, EmptyInts, disabled: false, notFound: true, noActiveHero: false);

    public static PresetLoadResult ForNoActiveHero() =>
        new(0, EmptyStrings, EmptyStrings, EmptyInts, disabled: false, notFound: false, noActiveHero: true);

    private static readonly IReadOnlyList<string> EmptyStrings = System.Array.Empty<string>();
    private static readonly IReadOnlyList<int> EmptyInts = System.Array.Empty<int>();
}
