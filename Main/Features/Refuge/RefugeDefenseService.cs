using TAOM.Core.Validation;
using TAOM.Features.Refuge.Domain;

namespace TAOM.Features.Refuge;

/// <summary>
/// Narrow read seam over the refuge book, defined by its hot-path consumer. The pinned
/// <see cref="IRefugeService"/> exposes no per-id lookup, and handing the defense probe the whole
/// service would drag menu/lifecycle dependencies into the combat models, so the probe binds to
/// this one-method view instead. <see cref="RefugeService"/> implements it over the same
/// dictionary it persists; register both interfaces against the one singleton.
/// </summary>
public interface IRefugeBook
{
    /// <summary>The book row for this party StringId, or null. Null-tolerant, no allocation.</summary>
    RefugeData GetByPartyId(string partyId);
}

/// <summary>
/// Defender damage reduction consumed by the TAOM combat model chain (the orchestrator wires
/// TaomCombatMechanicsModel / TaomCombatSimulationModel to this; the source module's two Harmony
/// patches are deleted). Called per hit and per sim tick, so the implementation is a dictionary
/// probe and two settings reads: no allocation, no enumeration.
/// </summary>
public class RefugeDefenseService : IRefugeDefenseService
{
    private readonly IRefugeBook _book;
    private readonly IRefugeSettingsProvider _settings;

    public RefugeDefenseService(IRefugeBook book, IRefugeSettingsProvider settings)
    {
        _book = book;
        _settings = settings;
    }

    public float DefenderDamageReduction(string partyStringId)
    {
        if (partyStringId == null)
            return 0f;
        var refuge = _book.GetByPartyId(partyStringId);
        // A refuge still being raised (or rebuilt into a stronghold) grants nothing: IsReady is
        // Established AND not Building, so the bonus drops for the rebuild window by design.
        if (refuge == null || !refuge.IsReady)
            return 0f;

        float factor = refuge.TierEnum == RefugeTier.Stronghold
            ? _settings.StrongholdDefenseBonus
            : _settings.RefugeDefenseBonus;
        // Positive finite gate: NaN/Infinity/negative/out-of-range settings degrade to "no bonus"
        // rather than poisoning every damage number in the battle (csharp-architecture.md,
        // engine-float decision gates).
        return FiniteFloatValidator.IsFiniteInRange(factor, 0f, 1f) ? factor : 0f;
    }
}
