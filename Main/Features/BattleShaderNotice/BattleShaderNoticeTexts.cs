using TAOM.Features.ShaderCompileNotice;

namespace TAOM.Features.BattleShaderNotice;

/// <summary>The battle notice's texts, resolved on the game thread: the window's (shared format with the startup notice) and the loading screen's line.</summary>
public sealed class BattleShaderNoticeTexts
{
    public BattleShaderNoticeTexts(ShaderCompileNoticeTexts window, string loadingLine)
    {
        Window = window;
        LoadingLine = loadingLine ?? "";
    }

    public ShaderCompileNoticeTexts Window { get; }

    public string LoadingLine { get; }
}

/// <summary>Resolves <see cref="BattleShaderNoticeTexts"/>; the engine boundary for the texts.</summary>
public interface IBattleShaderNoticeTextSource
{
    BattleShaderNoticeTexts Resolve();
}

/// <summary>Starts and stops the battle notice's window on a thread of its own.</summary>
public interface IBattleShaderNoticeRunner
{
    void Start(ShaderCompileNoticeTexts texts);

    void Stop();
}
