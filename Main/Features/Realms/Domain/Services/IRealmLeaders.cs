namespace TAOM.Features.Realms.Domain.Services;

/// <summary>Current leaders as the game knows them. Implemented by the game layer; faked in tests.</summary>
public interface IRealmLeaders
{
    /// <summary>The clan's living leader; null when the clan has none yet.</summary>
    string? ClanLeaderOf(string clanId);

    /// <summary>False once the clan is gone for good, so a waiting title falls vacant.</summary>
    bool ClanExists(string clanId);

    string? RulerOf(string kingdomId);

    bool KingdomExists(string kingdomId);
}
