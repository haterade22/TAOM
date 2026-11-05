namespace TAOM.Adapters;

/// <summary>The engine and file side of the one-time texture advice (docs/features/texture-advice.md). Main thread.</summary>
public interface ITextureAdviceAdapter
{
    /// <summary>Texture Quality as the engine stores it (0 Low, 1 Medium, 2 High on v1.4.8); -1 when unreadable.</summary>
    int TextureQuality { get; }

    /// <summary>The highest Texture Quality level this game build offers (2 on v1.4.8).</summary>
    int TopTextureQuality { get; }

    /// <summary>Installed physical memory in MB (GlobalMemoryStatusEx); 0 when unreadable.</summary>
    long TotalPhysicalMemoryMb { get; }

    bool WasAdviceShown();

    void RememberAdviceShown();

    /// <summary>One popup with one button; <paramref name="totalGigabytes"/> fills the text's {RAM}.</summary>
    void ShowAdvice(int totalGigabytes);
}
