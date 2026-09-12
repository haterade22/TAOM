namespace TAOM.Features.LordPartyTemplates;

/// <summary>
/// Answers one question for the Patch88 seam: does this hero field a party template of his own
/// instead of the clan binding vanilla would hand him? Ids only, no engine types.
/// </summary>
public interface ILordPartyTemplateService
{
    bool TryGetTemplateId(string heroId, out string? templateId);
}
