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
    private readonly Dictionary<Agent, SignatureAgentEntry> _entries = new Dictionary<Agent, SignatureAgentEntry>();

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

        _entries[agent] = new SignatureAgentEntry();
        _logger.LogInfo($"[SignatureStrikes] {agent.Name} registered as a signature agent (race {character.Race})");
        return true;
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
            _entries.Remove(agent);
    }

    public void Clear() => _entries.Clear();
}
