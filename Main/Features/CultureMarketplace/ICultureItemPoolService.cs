using TAOM.Features.CultureMarketplace.Domain;

namespace TAOM.Features.CultureMarketplace;

public interface ICultureItemPoolService
{
    void BuildPools();
    CultureItemPool GetPool(string cultureId);
    int CultureCount { get; }
    int TotalItemCount { get; }
}
