using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.InitialChildGeneration.Config;

namespace TAOM.Features.InitialChildGeneration;

public class InitialChildGenerationService : IInitialChildGenerationService
{
    private readonly IClanPopulationAdapter _clanAdapter;
    private readonly IChildCreatorAdapter _childCreator;
    private readonly IInitialChildGenerationConfigProvider _configProvider;
    private readonly IRandomSource _random;
    private readonly IModLogger _logger;

    public InitialChildGenerationService(
        IClanPopulationAdapter clanAdapter,
        IChildCreatorAdapter childCreator,
        IInitialChildGenerationConfigProvider configProvider,
        IRandomSource random,
        IModLogger logger)
    {
        _clanAdapter = clanAdapter;
        _childCreator = childCreator;
        _configProvider = configProvider;
        _random = random;
        _logger = logger;
    }

    public void GenerateInitialChildren()
    {
        var config = _configProvider.LoadConfig();
        var clans = _clanAdapter.GetMajorFactionClans();
        int totalCreated = 0;
        int clansProcessed = 0;
        int clansExcluded = 0;

        foreach (var clan in clans)
        {
            if (IsExcluded(clan, config))
            {
                clansExcluded++;
                continue;
            }

            clansProcessed++;
            var settings = ResolveSettings(clan, config);
            int childCount = CalculateChildCount(clan, settings);

            bool hasMales = clan.AdultMaleHeroIds.Count > 0;

            for (int i = 0; i < childCount; i++)
            {
                bool isFemale = DetermineGender(i, hasMales, settings.FemaleRatio);
                int age = _random.Next(settings.MinAge, settings.MaxAge + 1);
                string templateId = SelectTemplate(clan, isFemale);

                _childCreator.CreateChild(templateId, clan.ClanId, isFemale, age);
                totalCreated++;
            }
        }

        _logger.LogInfo(
            $"InitialChildGeneration: created {totalCreated} children across {clansProcessed} clans ({clansExcluded} excluded)");
    }

    private static bool IsExcluded(ClanPopulationInfo clan, InitialChildGenerationConfig config)
    {
        return config.ExcludedClans.Contains(clan.ClanId, StringComparer.OrdinalIgnoreCase)
            || config.ExcludedCultures.Contains(clan.CultureId, StringComparer.OrdinalIgnoreCase);
    }

    private static EffectiveSettings ResolveSettings(ClanPopulationInfo clan, InitialChildGenerationConfig config)
    {
        var d = config.Defaults;
        int minAge = d.MinAge;
        int maxAge = d.MaxAge;
        double femaleRatio = d.FemaleRatio;
        double multiplier = d.ChildCountMultiplier;
        int? fixedCount = null;

        var cultureOverride = config.CultureOverrides
            .FirstOrDefault(c => string.Equals(c.CultureId, clan.CultureId, StringComparison.OrdinalIgnoreCase));

        if (cultureOverride != null)
        {
            minAge = cultureOverride.MinAge ?? minAge;
            maxAge = cultureOverride.MaxAge ?? maxAge;
            femaleRatio = cultureOverride.FemaleRatio ?? femaleRatio;
            multiplier = cultureOverride.ChildCountMultiplier ?? multiplier;
        }

        var clanOverride = config.ClanOverrides
            .FirstOrDefault(c => string.Equals(c.ClanId, clan.ClanId, StringComparison.OrdinalIgnoreCase));

        if (clanOverride != null)
        {
            minAge = clanOverride.MinAge ?? minAge;
            maxAge = clanOverride.MaxAge ?? maxAge;
            femaleRatio = clanOverride.FemaleRatio ?? femaleRatio;
            multiplier = clanOverride.ChildCountMultiplier ?? multiplier;
            fixedCount = clanOverride.FixedChildCount;
        }

        return new EffectiveSettings(minAge, maxAge, femaleRatio, multiplier, fixedCount);
    }

    private static int CalculateChildCount(ClanPopulationInfo clan, EffectiveSettings settings)
    {
        if (settings.FixedChildCount.HasValue)
            return settings.FixedChildCount.Value;

        int totalAdults = clan.AdultMaleHeroIds.Count + clan.AdultFemaleHeroIds.Count;
        int baseCount = (int)Math.Ceiling(totalAdults / 2.0);
        int scaled = (int)Math.Ceiling(baseCount * settings.Multiplier);
        return Math.Max(0, scaled - clan.ExistingChildCount);
    }

    private bool DetermineGender(int childIndex, bool clanHasMales, double femaleRatio)
    {
        if (!clanHasMales && childIndex == 0)
            return false;

        return _random.NextDouble() < femaleRatio;
    }

    private string SelectTemplate(ClanPopulationInfo clan, bool isFemale)
    {
        var pool = isFemale ? clan.AdultFemaleHeroIds : clan.AdultMaleHeroIds;

        if (pool.Count == 0)
        {
            var fallback = isFemale ? clan.AdultMaleHeroIds : clan.AdultFemaleHeroIds;
            if (fallback.Count == 0)
                return clan.AdultMaleHeroIds.Count > 0 ? clan.AdultMaleHeroIds[0] : clan.AdultFemaleHeroIds[0];
            int idx = _random.Next(0, fallback.Count);
            return fallback[idx];
        }

        int index = _random.Next(0, pool.Count);
        return pool[index];
    }

    private readonly struct EffectiveSettings
    {
        public int MinAge { get; }
        public int MaxAge { get; }
        public double FemaleRatio { get; }
        public double Multiplier { get; }
        public int? FixedChildCount { get; }

        public EffectiveSettings(int minAge, int maxAge, double femaleRatio, double multiplier, int? fixedChildCount)
        {
            MinAge = minAge;
            MaxAge = maxAge;
            FemaleRatio = femaleRatio;
            Multiplier = multiplier;
            FixedChildCount = fixedChildCount;
        }
    }
}
