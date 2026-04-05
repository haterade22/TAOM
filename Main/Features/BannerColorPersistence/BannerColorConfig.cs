namespace TAOM.Features.BannerColorPersistence;

public class BannerColorConfig
{
    public bool EnableColorPersistence { get; set; } = true;
    public bool EnableDriftGuard { get; set; } = true;
    public bool EnableBannerPaste { get; set; } = true;
    public bool EnableUniqueSecondaryColor { get; set; } = true;
    public bool EnableLayerLimitTranspiler { get; set; } = true;
}
