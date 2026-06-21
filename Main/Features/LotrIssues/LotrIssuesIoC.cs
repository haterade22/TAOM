using DryIoc;

namespace TAOM.Features.LotrIssues;

/// <summary>
/// Registers the LOTR issue system services. The config provider + pure service are singletons; the
/// <see cref="LotrIssuesCampaignBehavior"/> is constructed in <c>SubModule.OnGameStart</c> (resolving
/// the service from IoC) and the <see cref="LotrIssueSaveableTypeDefiner"/> is engine-auto-discovered.
/// </summary>
public static class LotrIssuesIoC
{
    public static void RegisterLotrIssuesFeature(IContainer container)
    {
        container.Register<ILotrIssueConfigProvider, LotrIssueConfigProvider>(Reuse.Singleton);
        container.Register<ILotrIssueService, LotrIssueService>(Reuse.Singleton);
    }
}
