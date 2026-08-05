using TAOM.Core.Logging;

namespace TAOM.Features.Enlistment;

/// <summary>
/// Compiled defaults for the core loop. The content phase replaces the body with the
/// JSON-loading + per-field validation pattern (CultureConversionConfigProvider): every
/// numeric range-checked, floats NaN/Inf-guarded, per-field revert-to-default + warning.
/// The interface seam is final; only the source of values changes.
/// </summary>
public sealed class EnlistmentConfigProvider : IEnlistmentConfigProvider
{
    private readonly EnlistmentCoreConfig _config = new EnlistmentCoreConfig();

    public EnlistmentConfigProvider(IModLogger logger)
    {
        _ = logger; // reserved for the JSON-validation phase
    }

    public EnlistmentCoreConfig GetConfig() => _config;
}
