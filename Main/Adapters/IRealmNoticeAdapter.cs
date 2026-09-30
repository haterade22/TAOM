using TAOM.Features.RealmBorders.Domain;

namespace TAOM.Adapters;

/// <summary>The realm borders' player-facing messages, localized in the adapter.</summary>
public interface IRealmNoticeAdapter
{
    /// <summary>"You enter the lands of {REALM}."</summary>
    void ShowEnteringRealm(string realmName);

    /// <summary>"The map shows the realms." and its two siblings, when the map mode key is pressed.</summary>
    void ShowMapMode(MapMode mode);
}
