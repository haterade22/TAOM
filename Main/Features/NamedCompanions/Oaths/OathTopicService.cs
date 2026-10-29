using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.NamedCompanions.Domain;

namespace TAOM.Features.NamedCompanions.Oaths;

/// <summary>Why a visible topic cannot be chosen right now.</summary>
public enum TopicBlocker
{
    None,
    Coop,
    Cooldown,
    Purse,

    /// <summary>The party lacks some of the topic's take_items.</summary>
    Items,

    /// <summary>The topic is not in the config (or is null).</summary>
    Unknown,
}

/// <summary>
/// The extra dialog topics a named companion offers (#904): which are visible now, what blocks one, and what
/// finishing one does. Pure over adapters; the dialog behavior only converts engine objects to ids.
/// </summary>
public sealed class OathTopicService
{
    private readonly INamedCompanionConfigProvider _config;
    private readonly OathLedger _ledger;
    private readonly INamedCompanionAdapter _adapter;
    private readonly ICareerPurseAdapter _purse;
    private readonly IPartyItemRosterAdapter _roster;
    private readonly IModLogger _logger;

    public OathTopicService(
        INamedCompanionConfigProvider config,
        OathLedger ledger,
        INamedCompanionAdapter adapter,
        ICareerPurseAdapter purse,
        IPartyItemRosterAdapter roster,
        IModLogger logger)
    {
        _config = config;
        _ledger = ledger;
        _adapter = adapter;
        _purse = purse;
        _roster = roster;
        _logger = logger;
    }

    /// <summary>The topics offered now. <paramref name="today"/> feeds the daily roll of a <c>random_chance</c> topic.</summary>
    public IReadOnlyList<TopicDefinition> Visible(string characterId, bool recruited, int today)
    {
        var visible = new List<TopicDefinition>();
        var companion = Find(characterId);
        if (companion?.Topics == null) return visible;

        var relation = _adapter.GetRelationWithPlayer(characterId);
        foreach (var topic in companion.Topics)
        {
            if (topic == null) continue;
            if (topic.WhenKind == TopicWhen.Recruited && !recruited) continue;
            if (topic.WhenKind == TopicWhen.Unrecruited && recruited) continue;
            // A recorded day means the topic was used: Complete records one every time.
            if (topic.Once && _ledger.TryGetTopicLastDay(characterId, topic.Id, out _)) continue;
            if (!AllFlagsSet(topic.RequiresFlags)) continue;
            if (relation < topic.MinRelation || relation > topic.MaxRelation) continue;
            if (topic.RandomChance < 100 && OathDailyRoll.Percent(characterId, topic.Id, today) >= topic.RandomChance) continue;
            visible.Add(topic);
        }
        return visible;
    }

    /// <summary><see cref="TopicBlocker.None"/> when the topic can be chosen now.</summary>
    public TopicBlocker Blocker(string characterId, TopicDefinition topic, int today, bool isCoopClient)
    {
        if (!CoopSessionPolicy.MayWriteSaveBackedState(isCoopClient)) return TopicBlocker.Coop;
        if (topic == null) return TopicBlocker.Unknown;

        if (topic.CooldownDays > 0
            && _ledger.TryGetTopicLastDay(characterId, topic.Id, out var last)
            && last + topic.CooldownDays > today)
            return TopicBlocker.Cooldown;

        if (topic.GoldCost > _purse.Gold) return TopicBlocker.Purse;
        if (MissingTakeItems(characterId, topic).Count > 0) return TopicBlocker.Items;
        return TopicBlocker.None;
    }

    /// <summary>What the party lacks of the topic's take_items (unmodified stacks only); empty when nothing is short.</summary>
    public IReadOnlyList<MissingTerm> MissingTakeItems(string characterId, TopicDefinition topic)
    {
        var missing = new List<MissingTerm>();
        if (topic?.TakeItems == null) return missing;
        foreach (var item in topic.TakeItems)
        {
            var have = _roster.GetItemCount(item.ItemId);
            if (have < item.Count)
                missing.Add(new MissingTerm { Kind = MissingTermKind.Item, ItemId = item.ItemId, Count = item.Count - have });
        }
        return missing;
    }

    /// <summary>Applies a topic's effects. False, with nothing changed, when it is unknown or blocked.</summary>
    public bool Complete(string characterId, string topicId, int today, bool isCoopClient)
    {
        var topic = FindTopic(characterId, topicId);
        if (topic == null || Blocker(characterId, topic, today, isCoopClient) != TopicBlocker.None) return false;

        var taken = new List<KeyValuePair<string, int>>();
        if (topic.TakeItems != null)
            foreach (var item in topic.TakeItems)
            {
                var removed = _roster.RemoveItem(item.ItemId, item.Count);
                if (removed > 0) taken.Add(new KeyValuePair<string, int>(item.ItemId, removed));
                if (removed < item.Count)
                {
                    _logger.LogWarning($"[Oaths] topic {topicId} of {characterId}: only {removed} of {item.Count} {item.ItemId} could be taken; refunded");
                    Refund(taken);
                    return false;
                }
            }

        if (topic.GoldCost > 0 && !_purse.ChargeGold(topic.GoldCost))
        {
            _logger.LogWarning($"[Oaths] topic {topicId} of {characterId}: the purse refused {topic.GoldCost} gold; items refunded");
            Refund(taken);
            return false;
        }

        // Given items land in the party inventory without any notification.
        if (topic.GiveItems != null)
            foreach (var item in topic.GiveItems)
                if (!_roster.AddItem(item.ItemId, item.Count))
                    _logger.LogWarning($"[Oaths] topic {topicId} of {characterId}: could not give {item.Count} {item.ItemId}");

        if (topic.GoldReward > 0 && !_purse.GrantGold(topic.GoldReward))
            _logger.LogWarning($"[Oaths] topic {topicId} of {characterId}: could not grant {topic.GoldReward} gold");
        if (topic.RelationGain != 0) _adapter.ChangeRelationWithPlayer(characterId, topic.RelationGain);
        if (topic.SetsFlags != null)
            foreach (var flag in topic.SetsFlags)
                _ledger.Set(flag);
        _ledger.SetTopicLastDay(characterId, topic.Id, today);
        return true;
    }

    private void Refund(List<KeyValuePair<string, int>> taken)
    {
        foreach (var pair in taken)
            _roster.AddItem(pair.Key, pair.Value);
    }

    /// <summary>
    /// The ledger topic id of the one-time backstory intro (#1061). The dot can never be a config topic id
    /// (<c>^[a-z0-9_]{1,48}$</c>, OathConfigValidator), so the record cannot hide or alter a real topic.
    /// </summary>
    public const string IntroTopicId = "intro.backstory";

    /// <summary>True when this configured, enabled companion has not yet told their backstory to a player who has not recruited them.</summary>
    public bool IntroDue(string characterId, bool recruited, bool isCoopClient, bool hireAllowed)
    {
        // Every backstory ends with an offer to join, so a companion the hire gate (#575) would refuse keeps it.
        if (recruited || !hireAllowed || !CoopSessionPolicy.MayWriteSaveBackedState(isCoopClient)
            || Find(characterId) == null) return false;
        return !_ledger.TryGetTopicLastDay(characterId, IntroTopicId, out _);
    }

    /// <summary>Records that the intro was shown. False, with nothing recorded, on a co-op client.</summary>
    public bool RecordIntro(string characterId, int today, bool isCoopClient)
    {
        if (!CoopSessionPolicy.MayWriteSaveBackedState(isCoopClient)) return false;
        return _ledger.SetTopicLastDay(characterId, IntroTopicId, today);
    }

    public TopicDefinition FindTopic(string characterId, string topicId)
    {
        var companion = Find(characterId);
        if (companion?.Topics == null || topicId == null) return null;
        foreach (var topic in companion.Topics)
            if (topic != null && topic.Id == topicId) return topic;
        return null;
    }

    private NamedCompanionDefinition Find(string characterId)
    {
        var companions = _config.GetCompanions();
        if (companions == null || characterId == null) return null;
        foreach (var companion in companions)
            if (companion != null && companion.Enabled && companion.CharacterId == characterId) return companion;
        return null;
    }

    private bool AllFlagsSet(List<string> flags)
    {
        if (flags == null) return true;
        foreach (var flag in flags)
            if (!_ledger.Has(flag)) return false;
        return true;
    }
}
