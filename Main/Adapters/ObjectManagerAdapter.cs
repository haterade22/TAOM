using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
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

            Dictionary<string, CultureInfo>? xmlTroops = null;
            return cultures.Select(c =>
            {
                var info = new CultureInfo
                {
                    Id = c.StringId,
                    CanHaveSettlement = c.CanHaveSettlement,
                    IsBandit = c.IsBandit,
                    HasFactionBanner = c.Banner?.BannerDataList?.Count > 0
                };

                if (c is CultureObject culture)
                {
                    info.BasicTroopId = culture.BasicTroop?.StringId;
                    info.MeleeMilitiaTroopId = culture.MeleeMilitiaTroop?.StringId;
                    info.RangedMilitiaTroopId = culture.RangedMilitiaTroop?.StringId;
                    info.EliteBasicTroopId = culture.EliteBasicTroop?.StringId;
                    info.RangedEliteMilitiaTroopId = culture.RangedEliteMilitiaTroop?.StringId;
                }
                else
                {
                    // A Custom Battle loads cultures as BasicCultureObject, which keeps no troops.
                    xmlTroops ??= ReadCultureTroopsFromXml();
                    if (xmlTroops.TryGetValue(c.StringId, out var troops))
                    {
                        info.BasicTroopId = troops.BasicTroopId;
                        info.MeleeMilitiaTroopId = troops.MeleeMilitiaTroopId;
                        info.RangedMilitiaTroopId = troops.RangedMilitiaTroopId;
                        info.EliteBasicTroopId = troops.EliteBasicTroopId;
                        info.RangedEliteMilitiaTroopId = troops.RangedEliteMilitiaTroopId;
                    }
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

    // Re-reads the SPCultures files the current game type loaded, as MonsterSizeCatalogAdapter does for Monsters:
    // skipValidation because the engine validated them at load, and the game-type filter so a Custom Battle reads
    // exactly what it loaded.
    private Dictionary<string, CultureInfo> ReadCultureTroopsFromXml()
    {
        try
        {
            string? gameType = Game.Current?.GameType?.GameTypeStringId;
            XmlDocument merged = gameType == null
                ? MBObjectManager.GetMergedXmlForManaged("SPCultures", skipValidation: true)
                : MBObjectManager.GetMergedXmlForManaged("SPCultures", skipValidation: true, ignoreGameTypeInclusionCheck: false, gameType: gameType);
            var troops = CultureTroopIdReader.Read(merged);
            _logger.LogInfo($"ObjectManagerAdapter: read troop ids for {troops.Count} cultures from the merged SPCultures XML ({gameType ?? "all game types"})");
            return troops;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"ObjectManagerAdapter: could not read culture troops from SPCultures XML: {ex.Message}");
            return new Dictionary<string, CultureInfo>();
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
                CultureId = c.Culture?.StringId,
                IsSoldier = c.IsSoldier,
                IsObsolete = c.IsObsolete,
                DefaultFormationClass = c.DefaultFormationClass
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError($"ObjectManagerAdapter: Failed to get all characters: {ex.Message}");
            return new List<CharacterInfo>();
        }
    }
}
