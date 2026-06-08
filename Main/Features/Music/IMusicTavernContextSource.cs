namespace TAOM.Features.Music;

public interface IMusicTavernContextSource
{
    bool IsInTavernContext { get; }

    void EnterTavernContext();

    void ExitTavernContext();
}
