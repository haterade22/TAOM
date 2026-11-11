using System.Collections.Generic;

namespace TAOM.Features.BannerInjection;

public interface IBannerConfigProvider
{
    Dictionary<string, string> GetKingdomBannerKeys();
    Dictionary<string, string> GetClanBannerKeys();

    /// <summary>Culture id (no "Culture." prefix) to the banner key of the kingdom of that culture; the first kingdom wins.</summary>
    Dictionary<string, string> GetCultureKingdomBannerKeys();
}
