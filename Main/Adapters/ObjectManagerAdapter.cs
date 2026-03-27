using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

public class ObjectManagerAdapter : IObjectManagerAdapter
{
    private readonly IModLogger _logger;

    public ObjectManagerAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public BasicCharacterObject GetBasicCharacter(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        try
        {
            return MBObjectManager.Instance?.GetObject<BasicCharacterObject>(id);
        }
        catch (Exception ex)
        {
            _logger.LogError($"ObjectManagerAdapter: Failed to resolve character '{id}': {ex.Message}");
            return null;
        }
    }

    public BasicCultureObject GetBasicCulture(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        try
        {
            return MBObjectManager.Instance?.GetObject<BasicCultureObject>(id);
        }
        catch (Exception ex)
        {
            _logger.LogError($"ObjectManagerAdapter: Failed to resolve culture '{id}': {ex.Message}");
            return null;
        }
    }

    public IReadOnlyList<CultureInfo> GetAllCultureInfos()
    {
        try
        {
            var cultures = MBObjectManager.Instance?.GetObjectTypeList<BasicCultureObject>();
            if (cultures == null) return new List<CultureInfo>();

            return cultures.Select(c =>
            {
                var info = new CultureInfo
                {
                    Id = c.StringId,
                    CanHaveSettlement = c.CanHaveSettlement,
                    IsBandit = c.IsBandit
                };

                if (c is CultureObject culture)
                {
                    info.BasicTroopId = culture.BasicTroop?.StringId;
                    info.MeleeMilitiaTroopId = culture.MeleeMilitiaTroop?.StringId;
                    info.RangedMilitiaTroopId = culture.RangedMilitiaTroop?.StringId;
                    info.EliteBasicTroopId = culture.EliteBasicTroop?.StringId;
                    info.RangedEliteMilitiaTroopId = culture.RangedEliteMilitiaTroop?.StringId;
                }

                return info;
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError($"ObjectManagerAdapter: Failed to get all cultures: {ex.Message}");
            return new List<CultureInfo>();
        }
    }

    public IReadOnlyList<CharacterInfo> GetAllCharacterInfos()
    {
        try
        {
            var characters = MBObjectManager.Instance?.GetObjectTypeList<BasicCharacterObject>();
            if (characters == null) return new List<CharacterInfo>();

            return characters.Select(c => new CharacterInfo
            {
                Id = c.StringId,
                IsHero = c.IsHero,
                CultureId = c.Culture?.StringId
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError($"ObjectManagerAdapter: Failed to get all characters: {ex.Message}");
            return new List<CharacterInfo>();
        }
    }
}
