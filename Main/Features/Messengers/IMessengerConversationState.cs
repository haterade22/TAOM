namespace TAOM.Features.Messengers;

/// <summary>
/// Whether a messenger conversation is open. The conversation sets up a real <c>PlayerEncounter</c> and, for a
/// target inside a settlement, moves the main party into that settlement, so no engine state tells it apart from a
/// visit. Features that need the player to be physically with someone (a troop gift) read this flag instead.
/// </summary>
public interface IMessengerConversationState
{
    bool IsOpen { get; }

    void Open();

    void Close();
}
