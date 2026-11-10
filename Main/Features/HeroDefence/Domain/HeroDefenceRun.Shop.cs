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
        hero.Level = 1;
        return BuyResult.Bought;
    }

    /// <summary>
    /// Buys the next level in a pause. A placed hero is reset at its post, so the director spawns it with the new level's numbers. A
    /// price of <see cref="int.MaxValue"/> (a bad setting, or a price past the purse's cap) is never sold, even from a full purse:
    /// the level is refused as <see cref="LevelResult.NotEnoughGold"/>.
    /// </summary>
    public LevelResult TryLevelHero(string heroId)
    {
        if (_phase != DefencePhase.Pause)
            return LevelResult.WrongPhase;

        var hero = Find(heroId);
        if (hero == null)
            return LevelResult.UnknownHero;

        if (hero.State == DefenceHeroState.NotOwned)
            return LevelResult.NotOwned;

        if (hero.Level >= _plan.Levels.MaxLevel)
            return LevelResult.TopLevel;

        var price = _plan.Levels.PriceOf(hero.Def.Price, hero.Level + 1);
        if (price == int.MaxValue || price > Gold)
            return LevelResult.NotEnoughGold;

        Gold -= price;
        hero.Level++;
        if (hero.State == DefenceHeroState.Placed)
            Need(hero, HeroWork.Reset);

        return LevelResult.Levelled;
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
            hero.State = DefenceHeroState.Placed;

        Need(hero, isNew ? HeroWork.Spawn : HeroWork.Move);
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
