using TAOM.Features.LordPartyTemplates.Domain;

namespace TAOM.Features.LordPartyTemplates;

public interface ILordPartyTemplateConfigProvider
{
    LordPartyTemplateConfig GetConfig();
}
