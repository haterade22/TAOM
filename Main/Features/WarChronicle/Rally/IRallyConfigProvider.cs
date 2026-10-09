namespace TAOM.Features.WarChronicle.Rally;

public interface IRallyConfigProvider
{
    /// <summary>The validated rally data; the compiled defaults when the file is missing or unreadable.</summary>
    RallyConfig GetConfig();
}
