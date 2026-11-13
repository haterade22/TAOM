using System;
using TAOM.Core.Validation;
using TAOM.Features.Messengers.Domain;

namespace TAOM.Features.Messengers;

public class MessengerService : IMessengerService
{
    private readonly IMessengerSettingsProvider _settings;
    private readonly IMessengerConfigProvider _config;
    private readonly IMessengerStateStore _store;
    private readonly IMessengerRandomSource _random;

    public MessengerService(
        IMessengerSettingsProvider settings,
        IMessengerConfigProvider config,
        IMessengerStateStore store,
        IMessengerRandomSource random)
    {
        _settings = settings;
        _config = config;
        _store = store;
        _random = random;
    }

    public MessengerValidationResult CanSendMessenger(HeroSnapshot? target, int playerGold, int quotedCost, bool inPerson)
    {
        if (target == null)
            return MessengerValidationResult.NullTarget;

        if (target.IsHumanPlayerCharacter)
            return MessengerValidationResult.HumanPlayerCharacter;

        if (!target.IsAlive)
            return MessengerValidationResult.HeroDead;

        if (target.IsPrisoner)
            return MessengerValidationResult.HeroPrisoner;

        if (target.IsChild)
            return MessengerValidationResult.HeroChild;

        if (target.IsFugitive)
            return MessengerValidationResult.HeroFugitive;

        var canBeReached = target.IsActive
                           || (target.IsWanderer && !target.IsActive);
        if (!canBeReached)
            return MessengerValidationResult.TargetUnavailable;

        if (target.IsInPlayerParty)
            return MessengerValidationResult.TargetInPlayerParty;

        // In person the player is already with the hero, so being together is no reason to refuse.
        if (!inPerson && target.SameSettlement)
            return MessengerValidationResult.SameSettlement;

        if (!inPerson && target.SameArmy)
            return MessengerValidationResult.SameArmy;

        if (playerGold < quotedCost)
            return MessengerValidationResult.InsufficientGold;

        if (_store.Contains(target.HeroId))
            return MessengerValidationResult.AlreadyPending;

        return MessengerValidationResult.Ok;
    }

    public MessengerOffer GetOffer(HeroSnapshot? target, MessengerQuoteInput trip, int playerGold, bool inPerson)
    {
        var cost = QuoteCost(trip);
        return new MessengerOffer(CanSendMessenger(target, playerGold, cost, inPerson), cost, QuoteTravelDays(trip));
    }

    public MessengerArrivalVerdict ClassifyArrival(HeroSnapshot target, bool sharesMainHeroParty, bool playerAvailable, bool targetPartyInMapEvent)
    {
        // Reachable on arrival only while Active, or as a wanderer the engine has not spawned yet. Every other state is
        // lost, a prisoner included. The send check (CanSendMessenger) also accepts a wanderer that is Released, Traveling
        // or Disabled; arrival does not.
        var reachable = target.HeroState == MessengerHeroState.Active
                        || (target.IsWanderer && target.HeroState == MessengerHeroState.NotSpawned);
        if (!reachable)
            return MessengerArrivalVerdict.Lost;

        if (sharesMainHeroParty)
            return MessengerArrivalVerdict.Confused;

        if (!playerAvailable || target.IsChild || targetPartyInMapEvent)
            return MessengerArrivalVerdict.Wait;

        return MessengerArrivalVerdict.Ready;
    }

    private const int CostCeiling = 100000;

    public int QuoteCost(MessengerQuoteInput input)
    {
        // The provider has validated the four pricing values and the speed multiplier, as it has for CalculateMessengerSpeed.
        var config = _config.GetConfig();
        var baseCost = _settings.MessengerGoldCost;
        var raw = (baseCost + (double)config.GoldPerTravelDay * TripDays(input))
                  * (input.AtWar ? config.AtWarMultiplier : 1.0)
                  * (input.NeverMet ? config.NeverMetMultiplier : 1.0)
                  * (input.Besieged ? config.BesiegedMultiplier : 1.0);

        // Clamp as a double before the cast: a float-to-int conversion of a huge or NaN value is undefined.
        if (!FiniteFloatValidator.IsFinite(raw))
            return baseCost;
        if (raw >= CostCeiling)
            return CostCeiling;
        return Math.Max(baseCost, (int)Math.Ceiling(raw));
    }

    public int QuoteTravelDays(MessengerQuoteInput input) =>
        Math.Max(1, (int)Math.Ceiling(TripDays(input)));

    // Days of travel, 0 to TravelDays, from the same per-hour speed the hourly tick moves the messenger at
    // (CalculateMessengerSpeed, 24 hours a day). An unknown or negative distance, a map with no usable size or a speed
    // that is not a positive finite number is a full trip.
    private double TripDays(MessengerQuoteInput input)
    {
        var full = Math.Max(input.TravelDays, 1);
        if (!FiniteFloatValidator.IsFinite(input.Distance) || input.Distance < 0f)
            return full;

        var speed = CalculateMessengerSpeed(input.MapDiagonal, input.TravelDays);
        if (!FiniteFloatValidator.IsFinite(speed) || speed <= 0f)
            return full;

        var days = input.Distance / (24.0 * speed);
        if (!FiniteFloatValidator.IsFinite(days))
            return full;
        return Math.Min(full, days);
    }

    public bool RollAccident()
    {
        if (!_settings.MessengerAccidents)
            return false;

        var chance = _config.GetConfig().AccidentChancePerHour;
        if (chance <= 0f)
            return false;
        if (chance >= 1f)
            return true;

        return _random.NextFloat() < chance;
    }

    public PositionUpdate AdvancePosition(MapCoord currentPosition, MapCoord targetPosition, float speed)
    {
        if (!targetPosition.IsValid)
            return new PositionUpdate(currentPosition, false);

        var direction = targetPosition - currentPosition;
        var distance = direction.Length;

        if (distance <= speed)
            return new PositionUpdate(targetPosition, true);

        var stepped = currentPosition + (direction.Normalized() * speed);
        return new PositionUpdate(stepped, false);
    }

    public float CalculateMessengerSpeed(float mapDiagonal, int travelDays)
    {
        var clampedDays = Math.Max(travelDays, 1);
        var baseSpeed = mapDiagonal / (24f * clampedDays);
        var multiplier = _config.GetConfig().TravelSpeedMultiplier;
        return baseSpeed * multiplier;
    }
}
