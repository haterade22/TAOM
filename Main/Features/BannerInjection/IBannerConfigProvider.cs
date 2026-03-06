using System.Collections.Generic;

namespace TAOM.Features.BannerInjection;

public interface IBannerConfigProvider
{
    Dictionary<string, string> GetKingdomBannerKeys();
    Dictionary<string, string> GetClanBannerKeys();
}
