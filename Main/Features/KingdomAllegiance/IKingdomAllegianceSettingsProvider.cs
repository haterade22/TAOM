namespace TAOM.Features.KingdomAllegiance;

/// <summary>The MCM "World/Kingdom Allegiance" switch. Read live at each question, none per frame.</summary>
public interface IKingdomAllegianceSettingsProvider
{
    /// <summary>True when AI clans keep to their side of the Free/Evil line.</summary>
    bool Enabled { get; }
}
