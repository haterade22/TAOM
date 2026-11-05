namespace TAOM.Features.TextureAdvice;

/// <summary>The one-time memory advice at the first main menu (docs/features/texture-advice.md).</summary>
public interface ITextureAdviceService
{
    /// <summary>Decides once per launch, writes one log line, shows the advice when the rule says so; never throws.</summary>
    void AdviseOnce();
}
