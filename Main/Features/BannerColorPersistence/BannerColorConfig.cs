namespace TAOM.Features.BannerColorPersistence;

public class BannerColorConfig
{
    public bool EnableColorPersistence { get; set; } = true;
    public bool EnableDriftGuard { get; set; } = true;
    public bool EnableBannerPaste { get; set; } = true;
    public bool EnableUniqueSecondaryColor { get; set; } = true;
    // v1.4.7 made banner layers natively unlimited (engine removed the 32-layer cap from
    // Banner.TryGetBannerDataFromCode), so the layer-limit transpiler is now a no-op — off by
    // default. See Banner_TryGetBannerDataFromCode_Transpiler + docs/migration/v1.4.7-impact.md.
    public bool EnableLayerLimitTranspiler { get; set; } = false;
    public bool EnableAgentVisualColors { get; set; } = true;
    public bool EnableConversationTableauColors { get; set; } = true;
}
