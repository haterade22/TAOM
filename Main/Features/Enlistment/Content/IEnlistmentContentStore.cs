using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Content;

/// <summary>
/// Owns the content-layer service record (rank/trust/duties/arrears). Persisted by the
/// content behavior as one string section under <c>_taom_enlistment_content</c>; a
/// malformed section resets clean (progression loss, never a softlock — the core record
/// owns everything softlock-relevant).
/// </summary>
public interface IEnlistmentContentStore
{
    ServiceContentRecord Record { get; }

    string Serialize();

    void Deserialize(string section);

    void Clear();
}
