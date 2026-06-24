namespace TAOM.Features.NavalTravel;

/// <summary>Loads + validates the naval-travel JSON config (cached for the process lifetime).</summary>
public interface INavalTravelConfigProvider
{
    NavalTravelConfig GetConfig();
}
