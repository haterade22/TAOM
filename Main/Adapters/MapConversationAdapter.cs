using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

public sealed class MapConversationAdapter : IMapConversationAdapter
{
    private readonly IModLogger _logger;

    public MapConversationAdapter(IModLogger logger) => _logger = logger;

    /// <summary>
    /// The guard the ENGINE does not perform. Verified against installed v1.4.7:
    /// <c>ConversationManager.OpenMapConversation</c> opens with
    /// <c>(GameStateManager.Current?.ActiveState as MapState).OnMapConversationStarts(...)</c> —
    /// an `as` cast dereferenced with no null check, so it throws when the state manager is null,
    /// when the state stack is empty, OR when the active state is any type other than MapState.
    /// Three separate routes to the same NRE, none guarded. <c>CampaignMapConversation.OpenConversation</c>
    /// adds a fourth by dereferencing <c>Campaign.Current</c> unguarded before that.
    /// </summary>
    public bool CanOpenConversation
    {
        get
        {
            try
            {
                if (Campaign.Current == null)
                    return false;
                if (!(GameStateManager.Current?.ActiveState is MapState))
                    return false;

                // Never stack a second conversation on a live one.
                return Campaign.Current.ConversationManager?.IsConversationFlowActive != true;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool OpenWithHero(string heroId)
    {
        if (string.IsNullOrEmpty(heroId) || !CanOpenConversation)
            return false;

        try
        {
            var hero = Campaign.Current.CampaignObjectManager.Find<Hero>(heroId);
            if (hero?.CharacterObject == null)
            {
                _logger?.LogWarning($"[Enlistment] map conversation: hero '{heroId}' did not resolve");
                return false;
            }

            // The partner's party may legitimately be null — SetupAndStartMapConversation accepts it
            // (`conversationPartnerData.Party?.MobileParty`). Only the CharacterObject is required.
            var partner = new ConversationCharacterData(hero.CharacterObject, hero.PartyBelongedTo?.Party);
            var player = new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty);

            CampaignMapConversation.OpenConversation(player, partner);
            return true;
        }
        catch (System.Exception ex)
        {
            _logger?.LogError($"[Enlistment] map conversation with '{heroId}' threw: {ex.Message}");
            return false;
        }
    }
}
