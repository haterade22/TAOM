namespace TAOM.Features.FactionPower;

/// <summary>The faction power part of the campaign start: pins the start screen's choice into a new campaign, then loads the values.</summary>
public interface IFactionPowerSession
{
    /// <summary>Pins the start screen's choice when the campaign is new, then reloads the store, so one campaign never inherits another's.</summary>
    void Begin(bool isNewCampaign);
}
