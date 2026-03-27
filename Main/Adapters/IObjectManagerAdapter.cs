using System.Collections.Generic;
using TaleWorlds.Core;

namespace TAOM.Adapters;

public interface IObjectManagerAdapter
{
    BasicCharacterObject GetBasicCharacter(string id);
    BasicCultureObject GetBasicCulture(string id);
    IReadOnlyList<CultureInfo> GetAllCultureInfos();
    IReadOnlyList<CharacterInfo> GetAllCharacterInfos();
}

public class CultureInfo
{
    public string Id { get; set; }
    public bool CanHaveSettlement { get; set; }
    public bool IsBandit { get; set; }
    public string MeleeMilitiaTroopId { get; set; }
    public string RangedMilitiaTroopId { get; set; }
    public string EliteBasicTroopId { get; set; }
    public string RangedEliteMilitiaTroopId { get; set; }
    public string BasicTroopId { get; set; }
}

public class CharacterInfo
{
    public string Id { get; set; }
    public bool IsHero { get; set; }
    public string CultureId { get; set; }
}
