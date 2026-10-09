namespace TAOM.Features.FiefManagement;

/// <summary>Why the fief hub can or cannot open right now.</summary>
public enum FiefHubAvailability
{
    /// <summary>The hub can open.</summary>
    Available,

    /// <summary>The MCM "Enable Fief Management" toggle is off.</summary>
    FeatureDisabled,

    /// <summary>The campaign map cannot take a menu now: another screen or modal is up, or the player is enlisted and attached.</summary>
    MapBusy,

    /// <summary>The player owns no town or castle.</summary>
    NoFiefs,
}
