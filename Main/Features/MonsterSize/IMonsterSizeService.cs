namespace TAOM.Features.MonsterSize;

public interface IMonsterSizeService
{
    /// <summary>Copies each Monster's <see cref="MonsterSizeConfig.AttributeName"/> into the body_length of every
    /// Horse item naming that Monster, the Monster winning over the item's own value. Run once per game init, after
    /// the items load and before any mission builds an agent. Returns how many items were written; never throws.</summary>
    int ApplyMonsterSizes();
}
