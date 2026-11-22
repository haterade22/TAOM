using System;
using System.Linq;
using TAOM.Core.Logging;

namespace TAOM.CoopCompat.LoadOrder;

/// <summary>The host's answer about one player's load order. Names are the host's module names, not translated.</summary>
public sealed class LoadOrderVerdict
{
    public static readonly LoadOrderVerdict None = new LoadOrderVerdict(false, "", "");

    public LoadOrderVerdict(bool differs, string hostFirst, string hostSecond)
    {
        Differs = differs;
        HostFirst = hostFirst;
        HostSecond = hostSecond;
    }

    public bool Differs { get; }

    /// <summary>On the host this module loads before <see cref="HostSecond"/>; for the player after it.</summary>
    public string HostFirst { get; }

    public string HostSecond { get; }
}

/// <summary>
/// The co-op load order check. A client sends its order in the settings hello; the host compares it with its own,
/// logs the result and answers in the reply, and the client tells the player. It warns and never blocks. The switch
/// is read on every check, in the co-op server process (see CoopSettingsProvider for where that process takes it from).
/// </summary>
public sealed class LoadOrderCheckService
{
    private readonly ILoadOrderAdapter _modules;
    private readonly Func<bool> _warningEnabled;
    private readonly IModLogger _logger;
    private bool _loggedCompared;

    public LoadOrderCheckService(ILoadOrderAdapter modules, Func<bool> warningEnabled, IModLogger logger)
    {
        _modules = modules;
        _warningEnabled = warningEnabled;
        _logger = logger;
    }

    /// <summary>This game's module ids in load order, for the hello. Empty, with a warning, when the list cannot be read.</summary>
    public string[] LocalOrder()
    {
        try
        {
            return LoadOrderRules.LocalOrder(_modules.ActiveModules());
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[CoopCompat] load order: could not read this game's module list, so the hello carries none: "
                + ex.GetType().Name + ": " + ex.Message);
            return Array.Empty<string>();
        }
    }

    /// <summary>Host side: compares a player's order with this game's. Never throws, so the hello reply always goes.</summary>
    public LoadOrderVerdict Check(string peer, string[] playerOrder)
    {
        try
        {
            if (!_warningEnabled())
            {
                _logger.LogInfo("[CoopCompat] load order peer=" + peer + " not compared: the switch is off on this server");
                return LoadOrderVerdict.None;
            }

            if (playerOrder == null || playerOrder.Length == 0)
            {
                _logger.LogInfo("[CoopCompat] load order peer=" + peer + " not reported (an older TAOM.CoopCompat, or the player could not read its module list)");
                return LoadOrderVerdict.None;
            }

            var problem = LoadOrderRules.Problem(playerOrder);
            if (problem != null)
            {
                _logger.LogWarning("[CoopCompat] load order peer=" + peer + " ignored: " + problem);
                return LoadOrderVerdict.None;
            }

            return Compare(peer, playerOrder);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[CoopCompat] load order peer=" + peer + " not compared: " + ex.GetType().Name + ": " + ex.Message);
            return LoadOrderVerdict.None;
        }
    }

    private LoadOrderVerdict Compare(string peer, string[] playerOrder)
    {
        var local = _modules.ActiveModules();
        var hostOrder = local.Select(m => m.Id).ToList();
        var compared = LoadOrderRules.Compared(local);
        var hostCompared = string.Join(", ", hostOrder.Where(compared.Contains));

        if (!_loggedCompared)
        {
            _loggedCompared = true;
            _logger.LogInfo("[CoopCompat] load order compared modules (" + hostOrder.Count(compared.Contains) + "): " + hostCompared);
        }

        if (!LoadOrderRules.TryFindDifference(hostOrder, playerOrder, compared, out var first, out var second))
        {
            _logger.LogInfo("[CoopCompat] load order peer=" + peer + " match");
            return LoadOrderVerdict.None;
        }

        _logger.LogInfo("[CoopCompat] load order peer=" + peer + " differ: on the host " + first + " loads before " + second
            + ", for the player after it (host order: " + hostCompared + "; player order: "
            + string.Join(", ", playerOrder.Where(compared.Contains)) + ")");
        return new LoadOrderVerdict(true, LoadOrderRules.Describe(first!, NameOf(local, first!)), LoadOrderRules.Describe(second!, NameOf(local, second!)));
    }

    private static string? NameOf(System.Collections.Generic.IEnumerable<ModuleEntry> modules, string id) =>
        modules.FirstOrDefault(m => m.Id == id)?.Name;
}
