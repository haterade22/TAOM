namespace TAOM.Features.Music;

public sealed class MusicTavernContextSource : IMusicTavernContextSource
{
    private readonly object _sync = new object();
    private bool _isInTavernContext;

    public bool IsInTavernContext
    {
        get
        {
            lock (_sync)
            {
                return _isInTavernContext;
            }
        }
    }

    public void EnterTavernContext()
    {
        lock (_sync)
        {
            _isInTavernContext = true;
        }
    }

    public void ExitTavernContext()
    {
        lock (_sync)
        {
            _isInTavernContext = false;
        }
    }
}
