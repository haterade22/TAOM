using System;
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

    public MessengerValidationResult CanSendMessenger(HeroSnapshot target, int playerGold)
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

        if (playerGold < _settings.MessengerGoldCost)
            return MessengerValidationResult.InsufficientGold;

        if (_store.Contains(target.HeroId))
            return MessengerValidationResult.AlreadyPending;

        return MessengerValidationResult.Ok;
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
