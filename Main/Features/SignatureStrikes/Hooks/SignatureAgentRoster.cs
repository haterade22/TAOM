using System.Collections.Concurrent;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.SignatureStrikes.Hooks;

/// <summary>
/// Reference-keyed roster of signature agents. Identity comes from <see cref="ISignatureStrikeRegistry"/>
/// on the DreadSourceTracker's two axes: <c>HeroObject.StringId</c> (an <c>as CharacterObject</c>
/// cast, because a mission agent carries a plain <c>BasicCharacterObject</c> outside a campaign)
/// and <c>Character.Race</c>.
/// </summary>
public sealed class SignatureAgentRoster : ISignatureAgentRoster
{
    private readonly ISignatureStrikeRegistry _registry;
    private readonly IModLogger _logger;
    // OnAgentDeleted (Remove) and OnMeleeHit (TryGet) are native's to place (#634); OnAgentBuild
    // (TryRegister) is main-thread. A concurrent map keeps the three from meeting inside it.
    private readonly ConcurrentDictionary<Agent, SignatureAgentEntry> _entries = new ConcurrentDictionary<Agent, SignatureAgentEntry>();

    public SignatureAgentRoster(ISignatureStrikeRegistry registry, IModLogger logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public int Count => _entries.Count;

    public bool TryRegister(Agent? agent)
    {
        if (agent == null || !agent.IsHuman || agent.IsMount)
            return false;

        if (_entries.ContainsKey(agent))
            return false;

        var character = agent.Character;
        if (character == null)
            return false;

        var heroStringId = (character as CharacterObject)?.HeroObject?.StringId;
        if (!_registry.IsSignatureAgent(heroStringId, character.Race))
            return false;

        if (!_entries.TryAdd(agent, new SignatureAgentEntry()))
            return false;
        _logger.LogInfo($"[SignatureStrikes] {agent.Name} registered as a signature agent (race {character.Race})");
        return true;
    }

    public int RegisterAll(IEnumerable<Agent>? agents)
    {
        var added = 0;
        if (agents == null)
            return 0;
        foreach (var agent in agents)
        {
            if (TryRegister(agent))
                added++;
        }
        return added;
    }

    public bool TryGet(Agent? agent, out SignatureAgentEntry entry)
    {
        if (agent != null)
            return _entries.TryGetValue(agent, out entry);

        entry = null!;
        return false;
    }

    public void Remove(Agent? agent)
    {
        if (agent != null)
            _entries.TryRemove(agent, out _);
    }

    public void Clear() => _entries.Clear();
}
