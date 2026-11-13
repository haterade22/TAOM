namespace TAOM.Features.LostFooting;

/// <summary>The MCM values behind the Lost Footing census. The scan numbers are compiled defaults, not settings.</summary>
public interface ILostFootingSettingsProvider
{
    /// <summary>Writes the <c>[LostFooting]</c> census and sample lines; the start and summary lines also come while the rescue
    /// is on. Reads and logs only; changes nothing in a battle.</summary>
    bool CensusEnabled { get; }

    /// <summary>Moves stuck soldiers and riderless mounts back to where they can walk (#894 phase 2).</summary>
    bool RescueEnabled { get; }
}
