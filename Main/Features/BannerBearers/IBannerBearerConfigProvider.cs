using TAOM.Features.BannerBearers.Domain;

namespace TAOM.Features.BannerBearers;

public interface IBannerBearerConfigProvider
{
    BannerBearerConfig GetConfig();
}
