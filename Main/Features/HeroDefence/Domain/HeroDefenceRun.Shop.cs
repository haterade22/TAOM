using System;
using System.Linq;

namespace TAOM.Features.HeroDefence.Domain;

internal sealed partial class HeroDefenceRun
{
    public BuyResult TryBuyHero(string heroId)
    {
        if (_phase != DefencePhase.Pause)
            return BuyResult.WrongPhase;

        var hero = Find(heroId);
        if (hero == null)
            return BuyResult.UnknownHero;

        if (hero.State != DefenceHeroState.NotOwned)
            return BuyResult.AlreadyOwned;

        if (hero.Def.Price > Gold)
            return BuyResult.NotEnoughGold;

        Gold -= hero.Def.Price;
        hero.State = DefenceHeroState.Owned;
        return BuyResult.Bought;
    }

    /// <summary>The first placement orders a spawn. A later placement in a pause moves the hero, free of charge.</summary>
    public PlaceResult TryPlaceHero(string heroId, PlacementProbe probe)
    {
        if (_phase != DefencePhase.Pause)
            return PlaceResult.WrongPhase;

        var hero = Find(heroId);
        if (hero == null || hero.State == DefenceHeroState.NotOwned)
            return PlaceResult.NotOwned;

        var isNew = hero.State == DefenceHeroState.Owned;
        if (isNew && _roster.Count(other => other.State == DefenceHeroState.Placed) >= _r.MaxPlaced)
            return PlaceResult.FieldFull;

        var otherPosts = _roster.Where(other => other != hero && other.Post != null).Select(other => other.Post!.Value);
        var result = PlacementRules.Check(hero.Def.Class, probe, otherPosts, _plan.Map, _r);
        if (result != PlaceResult.Placed)
            return result;

        hero.Post = probe.Point;
        if (isNew)
        {
            hero.State = DefenceHeroState.Placed;
            _pending.HeroSpawns.Add(hero.Order);
        }
        else
        {
            _pending.HeroMoves.Add(hero.Order);
        }

        return PlaceResult.Placed;
    }

    /// <summary>The Ready button. It needs one placed hero, and it pays gold for the time the player gives up.</summary>
    public StartResult TryStartWave()
    {
        if (_phase != DefencePhase.Pause)
            return StartResult.WrongPhase;

        if (_roster.All(hero => hero.State != DefenceHeroState.Placed))
            return StartResult.NoHeroPlaced;

        AddGold(Math.Floor(Math.Min(_left * _r.EarlyGoldPerSecond, _r.EarlyGoldCap)));
        StartWave();
        return StartResult.Started;
    }
}
