namespace TAOM.Adapters;

public interface IMusicEngineAdapter
{
    int GetFreeMusicChannelIndex();

    void LoadClip(int index, string pathToClip);

    void UnloadClip(int index);

    bool IsClipLoaded(int index);

    void PlayMusic(int index);

    void PlayDelayed(int index, int deltaMilliseconds);

    bool IsMusicPlaying(int index);

    void PauseMusic(int index);

    void StopMusic(int index);

    void SetVolume(int index, float volume);
}
