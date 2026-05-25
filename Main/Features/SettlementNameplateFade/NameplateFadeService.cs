using TAOM.Core.Validation;

namespace TAOM.Features.SettlementNameplateFade;

public class NameplateFadeService : INameplateFadeService
{
    private readonly INameplateFadeSettingsProvider _settings;

    public NameplateFadeService(INameplateFadeSettingsProvider settings)
    {
        _settings = settings;
    }

    public float ComputeAlphaMultiplier(float distanceToCamera)
    {
        if (!_settings.Enabled)
            return 1f;

        // NaN / Infinity / negative distance: treat as "no fade" rather than poison the
        // widget brush with NaN through the multiplier (vanilla lerps toward NaN -> NaN
        // GlobalAlphaFactor -> renders garbage). This guard fires for engine edge cases
        // (off-screen settlements may report unusual distance values).
        if (!FiniteFloatValidator.IsFinite(distanceToCamera) || distanceToCamera < 0f)
            return 1f;

        var near = _settings.NearDistance;
        var far = _settings.FarDistance;

        // Defensive: invalid or collapsed range (NaN/Infinity from MCM-bypass JSON edit, or
        // user sets Far <= Near). Fail-safe to vanilla rather than divide-by-zero or NaN.
        if (!FiniteFloatValidator.IsFinite(near) || !FiniteFloatValidator.IsFinite(far) || far <= near)
            return 1f;

        if (distanceToCamera <= near) return 1f;
        if (distanceToCamera >= far) return 0f;

        return 1f - (distanceToCamera - near) / (far - near);
    }
}
