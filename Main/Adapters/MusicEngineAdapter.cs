using TaleWorlds.Engine;

namespace TAOM.Adapters;

public sealed class MusicEngineAdapter : IMusicEngineAdapter
{
    public int GetFreeMusicChannelIndex()
    {
        return Music.GetFreeMusicChannelIndex();
    }

    public void LoadClip(int index, string pathToClip)
    {
        Music.LoadClip(index, pathToClip);
    }

    public void UnloadClip(int index)
    {
        Music.UnloadClip(index);
    }

    public bool IsClipLoaded(int index)
    {
        return Music.IsClipLoaded(index);
    }

    public void PlayMusic(int index)
    {
        Music.PlayMusic(index);
    }

    public void PlayDelayed(int index, int deltaMilliseconds)
    {
        Music.PlayDelayed(index, deltaMilliseconds);
    }

    public bool IsMusicPlaying(int index)
    {
        return Music.IsMusicPlaying(index);
    }

    public void PauseMusic(int index)
    {
        Music.PauseMusic(index);
    }

    public void StopMusic(int index)
    {
        Music.StopMusic(index);
    }

    public void SetVolume(int index, float volume)
    {
        Music.SetVolume(index, volume);
    }
}
