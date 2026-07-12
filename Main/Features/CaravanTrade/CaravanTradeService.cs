using System;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.Execution;

namespace TAOM.Features.CaravanTrade;

/// <summary>
/// Pure decision logic for the CaravanTrade feature. All four Harmony hooks plus the caravan
/// GameModel delegate here; no TaleWorlds types cross the boundary. See <see cref="ICaravanTradeService"/>.
/// </summary>
public class CaravanTradeService : ICaravanTradeService
{
    private readonly ICaravanTradeSettingsProvider _settings;
    private readonly IAlignmentService _alignment;
    private readonly IModLogger _logger;

    public CaravanTradeService(ICaravanTradeSettingsProvider settings, IAlignmentService alignment, IModLogger logger)
    {
        _settings = settings;
        _alignment = alignment;
        _logger = logger;
    }

    public float ReweightTradeScore(float rawScore, float days, bool isNaval, bool isHomeTown, float recencyPenaltyFactor, bool isPlayerCaravan)
    {
        if (!IsActiveFor(isPlayerCaravan))
            return rawScore;

        // Positive-requirement gate: NaN rawScore and vanilla rejections (<= 0) pass through untouched.
        if (!(rawScore > 0f))
            return rawScore;

        // Naval uses a different vanilla distance factor (not 1/days); the shuttle is a land problem.
        if (isNaval)
            return rawScore;

        float result = rawScore;

        // Strip vanilla's land 1/days spike and re-apply a gentler curve:
        //   newScore = rawScore * days / (nearFieldFlatten + days)^alpha
        // For an equal base profit P0 (rawScore = P0/days) this is P0 / (flatten+days)^alpha, so near
        // towns lose their runaway advantage and the built-in profit estimate becomes the differentiator.
        // The home town is compressed like everyone else (removing its "rubber-band" proximity edge)
        // UNLESS the HomeDistanceReweight escape hatch is off; either way vanilla's upstream home-gravity
        // (num5, already in rawScore) is untouched, so caravans still return home on the payout cadence.
        // Positive-requirement gate on days keeps NaN/non-positive days out of Math.Pow.
        bool applyDistanceReweight = (!isHomeTown || _settings.HomeDistanceReweight) && days > 0f;
        if (applyDistanceReweight)
        {
            double denom = Math.Pow(_settings.NearFieldFlattenDays + days, _settings.DistanceDecayExponent);
            float multiplier = denom > 0d ? (float)(days / denom) : 1f;

            float maxComp = _settings.MaxCompensation;
            if (multiplier > maxComp)
                multiplier = maxComp;

            result = rawScore * multiplier;
        }

        // Recency penalty (home + non-home): deprioritize just-visited towns so caravans circulate.
        // Engine-Float gate: a NaN / out-of-range factor is ignored rather than emitting a corrupted
        // score. A valid factor is in (0,1], so this never turns a positive score into a rejection.
        if (FiniteFloatValidator.IsFiniteInRange(recencyPenaltyFactor, 0f, 1f))
            result *= recencyPenaltyFactor;

        return result;
    }

    public float ScaleVeryFarDistance(float vanillaVeryFarDays)
    {
        // Applied globally (the vanilla cache is a single shared field, not per-caravan) — it only
        // widens the candidate set; the re-weight and war gate remain player-scoped.
        if (!_settings.Enabled)
            return vanillaVeryFarDays;
        return vanillaVeryFarDays * _settings.RangeMultiplier;
    }

    public bool AllowWartimeTrade(string caravanKingdomId, string caravanCultureId, string targetKingdomId, string targetCultureId, bool isPlayerCaravan)
    {
        // false = keep the vanilla war veto; true = lift it for this pairing.
        if (!IsActiveFor(isPlayerCaravan))
            return false;

        switch (_settings.WarTradePolicy)
        {
            case WarTradePolicy.IgnoreWar:
                return true;
            case WarTradePolicy.SameAlignmentAndNeutral:
                // Resolve sides directly — do NOT use IAlignmentService.AreEnemyAlignments, whose Neutral
                // semantics are inverted for this purpose (it treats Neutral as an enemy of everyone). Here
                // Neutral on either side is a mercantile "trade with anyone", and otherwise only the same
                // side trades (Free↔Free / Evil↔Evil), never across the Free/Evil line. Mirrors the sibling
                // AlignmentRecruitment feature's deliberate work-around.
                var caravanSide = ResolveSide(caravanKingdomId, caravanCultureId);
                var targetSide = ResolveSide(targetKingdomId, targetCultureId);
                if (caravanSide == FactionSide.Neutral || targetSide == FactionSide.Neutral)
                    return true;
                return caravanSide == targetSide;
            case WarTradePolicy.None:
            default:
                return false;
        }
    }

    // Side by kingdom id, falling back to culture id when the kingdom isn't classified in alignment.json.
    // A player-founded / dynamically created kingdom (id like "new_kingdom") resolves Neutral by kingdom
    // id but IS sided by its culture — without this, a Free/Evil-cultured player kingdom would read Neutral
    // and trade across the Free/Evil line. Mirrors WarOfTheRingMomentum's MomentumEnrollmentService.ResolveSide.
    private FactionSide ResolveSide(string kingdomId, string cultureId)
    {
        var side = _alignment.GetKingdomSide(kingdomId);
        if (side != FactionSide.Neutral)
            return side;
        return string.IsNullOrEmpty(cultureId) ? FactionSide.Neutral : _alignment.GetCultureSide(cultureId);
    }

    public float ApplyBudgetFactorFloor(float vanillaBudgetFactor, bool isPlayerCaravan)
    {
        if (!IsActiveFor(isPlayerCaravan))
            return vanillaBudgetFactor;

        // Engine-sourced float: defer to vanilla on garbage rather than emit a corrupted floor.
        if (!FiniteFloatValidator.IsFinite(vanillaBudgetFactor))
            return vanillaBudgetFactor;

        return Math.Max(vanillaBudgetFactor, _settings.BudgetFactorFloor);
    }

    public int ResolveInitialTradeGold(int vanillaValue, bool isPlayerCaravan)
    {
        if (!IsActiveFor(isPlayerCaravan))
            return vanillaValue;
        // Never lower — preserve vanilla's large-caravan / main-hero bonuses.
        return Math.Max(vanillaValue, _settings.InitialTradeGold);
    }

    public int ResolveMaxGoldPerCategory(int vanillaValue, bool isPlayerCaravan)
    {
        if (!IsActiveFor(isPlayerCaravan))
            return vanillaValue;
        return _settings.MaxGoldPerCategory;
    }

    private bool IsActiveFor(bool isPlayerCaravan)
    {
        if (!_settings.Enabled)
            return false;
        if (isPlayerCaravan && !_settings.ApplyToPlayerCaravans)
            return false;
        return true;
    }
}
