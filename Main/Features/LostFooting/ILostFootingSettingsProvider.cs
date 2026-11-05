namespace TAOM.Features.LostFooting;

/// <summary>The MCM values behind the Lost Footing census. The scan numbers are compiled defaults, not settings.</summary>
public interface ILostFootingSettingsProvider
{
    /// <summary>Writes the <c>[LostFooting]</c> start, census and summary lines. Reads and logs only; changes nothing in a battle.</summary>
    bool CensusEnabled { get; }
}
