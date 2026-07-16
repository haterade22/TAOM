using TAOM.Features;

namespace TAOM.Features.PrisonerRecruitment;

/// <summary>
/// MCM live values over compiled defaults. Unlike the sibling alignment features there is no JSON
/// defaults layer: every setting here is a boolean, so there is no range, ordering or sign invariant
/// for a config provider to validate. <c>TaomSettings.Instance</c> can be null very early in startup
/// or if MCM fails to load — the <c>?? default</c> fallback keeps every read safe.
/// </summary>
public sealed class PrisonerRecruitmentSettingsProvider : IPrisonerRecruitmentSettingsProvider
{
    public bool IsEnabled => TaomSettings.Instance?.EnablePrisonerRecruitmentWaiver ?? true;

    public bool ApplyToPlayer => TaomSettings.Instance?.EnablePrisonerRecruitmentWaiverPlayer ?? true;

    public bool ApplyToAi => TaomSettings.Instance?.EnablePrisonerRecruitmentWaiverAi ?? true;
}
