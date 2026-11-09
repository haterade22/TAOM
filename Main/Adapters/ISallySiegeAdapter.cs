using TAOM.Features.MapEventGuard;

namespace TAOM.Adapters;

/// <summary>The engine reads behind the sally-out guard (#1099).</summary>
public interface ISallySiegeAdapter
{
    /// <summary>Engine-free data for the check; null when the siege has no besieger leader or there is no main party.</summary>
    SallySiegeSnapshot? Read();
}
