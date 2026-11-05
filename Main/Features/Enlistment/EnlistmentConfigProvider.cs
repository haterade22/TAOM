namespace TAOM.Features.Enlistment;

/// <summary>
/// Compiled defaults for the core loop. Nothing here is read from JSON: the tunable content
/// settings live in <c>EnlistmentContentConfigProvider</c>.
/// </summary>
public sealed class EnlistmentConfigProvider : IEnlistmentConfigProvider
{
    private readonly EnlistmentCoreConfig _config = new EnlistmentCoreConfig();

    public EnlistmentCoreConfig GetConfig() => _config;
}
