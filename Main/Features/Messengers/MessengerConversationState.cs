namespace TAOM.Features.Messengers;

/// <summary>
/// The in-memory flag behind <see cref="IMessengerConversationState"/>. Set when the behavior starts the arrived
/// messenger's conversation, cleared when its mission ends, when the mission fails to open, and at each session
/// launch (the service is a process singleton, so a flag left by a fault must not outlive its campaign).
/// </summary>
public sealed class MessengerConversationState : IMessengerConversationState
{
    private volatile bool _isOpen;

    public bool IsOpen => _isOpen;

    public void Open() => _isOpen = true;

    public void Close() => _isOpen = false;
}
