using Common.Messaging;

namespace TAOM.CoopCompat.Messages;

/// <summary>
/// A server-to-client message that carries the session token of the client it goes to (Codex P1 on #1471). The client
/// picks the token, sends it only to the server in its <see cref="CoopCompatHello"/>, and drops a server message whose
/// token differs, so a mission peer cannot forge one directly or through Coop's relay.
/// </summary>
public interface ISessionStamped : IMessage
{
    string SessionToken { get; set; }
}
