namespace TAOM.Features.LordPartyTemplates;

public class LordPartyTemplateService : ILordPartyTemplateService
{
    private readonly ILordPartyTemplateConfigProvider _configProvider;

    public LordPartyTemplateService(ILordPartyTemplateConfigProvider configProvider)
    {
        _configProvider = configProvider;
    }

    public bool TryGetTemplateId(string heroId, out string? templateId)
    {
        templateId = null;
        if (string.IsNullOrEmpty(heroId))
            return false;

        return _configProvider.GetConfig().Overrides.TryGetValue(heroId, out templateId);
    }
}
