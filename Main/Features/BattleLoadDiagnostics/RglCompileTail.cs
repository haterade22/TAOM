using System;
using System.IO;
using System.Text;
using TAOM.Features.ShaderCompileNotice;

namespace TAOM.Features.BattleLoadDiagnostics;

/// <summary>
/// File tail over the engine log. Opens with shared read so the engine keeps writing, restarts if the
/// file shrank, and reports itself unavailable after an IO error until the next success. The engine
/// may buffer its log, so a count can lag (UNVERIFIED on v1.4.8).
/// </summary>
internal sealed class RglCompileTail : IRglCompileTail
{
    private const int MaxReadBytes = 4 << 20;
    private static readonly long ResolveRetryTicks = TimeSpan.FromSeconds(30).Ticks;

    private readonly Func<string?> _resolvePath;
    private string? _path;
    private long _nextResolveTicks;
    private long _position;
    private bool _marked;
    private bool _ioFailed;
    private ShaderCompileProgress _progress = new();
    private long _lastCompileTicks;

    public RglCompileTail(Func<string?> resolvePath)
    {
        _resolvePath = resolvePath ?? throw new ArgumentNullException(nameof(resolvePath));
    }

    public bool Available => _path != null && !_ioFailed;

    public string? Path
    {
        get
        {
            TryResolve(DateTime.UtcNow.Ticks);
            return _path;
        }
    }

    public int CompilesSinceFresh => _progress.Count;

    public void MarkFresh()
    {
        _progress = new ShaderCompileProgress();
        _lastCompileTicks = 0L;
        _marked = false;
        if (!TryResolve(DateTime.UtcNow.Ticks)) return;
        try
        {
            using var stream = Open(_path!);
            _position = stream.Length;
            _marked = true;
            _ioFailed = false;
        }
        catch (Exception)
        {
            _ioFailed = true;
        }
    }

    public void ReadNew(long nowUtcTicks)
    {
        if (!TryResolve(nowUtcTicks)) return;
        if (!_marked)
        {
            // No mark on this file yet: everything already in it predates the mark.
            MarkFresh();
            return;
        }

        try
        {
            using var stream = Open(_path!);
            if (stream.Length < _position) _position = 0L;
            _ioFailed = false;
            if (stream.Length == _position) return;

            stream.Position = _position;
            var bytes = new byte[(int)Math.Min(stream.Length - _position, MaxReadBytes)];
            int read = stream.Read(bytes, 0, bytes.Length);
            _position += read;
            int before = _progress.Count;
            _progress.Feed(Encoding.ASCII.GetString(bytes, 0, read));
            if (_progress.Count > before) _lastCompileTicks = nowUtcTicks;
        }
        catch (Exception)
        {
            _ioFailed = true;
        }
    }

    public double? SecondsSinceLastCompile(long nowUtcTicks)
    {
        if (_lastCompileTicks == 0L) return null;
        return TimeSpan.FromTicks(nowUtcTicks - _lastCompileTicks).TotalSeconds;
    }

    private bool TryResolve(long nowUtcTicks)
    {
        if (_path != null) return true;
        if (nowUtcTicks < _nextResolveTicks) return false;
        _nextResolveTicks = nowUtcTicks + ResolveRetryTicks;
        try { _path = _resolvePath(); }
        catch (Exception) { _path = null; }
        return _path != null;
    }

    private static FileStream Open(string path)
        => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
}
