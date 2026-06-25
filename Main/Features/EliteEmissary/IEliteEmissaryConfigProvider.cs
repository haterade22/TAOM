using TAOM.Features.EliteEmissary.Domain;

namespace TAOM.Features.EliteEmissary;

/// <summary>Loads + validates <c>elite_emissary/elite_emissary_config.xml</c>. Missing/malformed →
/// <see cref="EliteEmissaryConfig.Empty"/>; unknown culture ids and troops lacking a
/// <c>merchant_cost</c> row are dropped with a warning (parse success ≠ validation success).</summary>
public interface IEliteEmissaryConfigProvider
{
    EliteEmissaryConfig GetConfig();
}
