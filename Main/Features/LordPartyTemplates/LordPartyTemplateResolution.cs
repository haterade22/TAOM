using System;

namespace TAOM.Features.LordPartyTemplates;

/// <summary>
/// The whole decision behind the Patch88 postfix on <c>Clan.DefaultPartyTemplate</c>, pure so it
/// can be tested without a campaign. A clan template read is overridden only when all of these
/// hold: a lord spawn is in progress (an ambient owner is set), the clan being read is that
/// owner's own clan, and the owner is mapped to a template. Everything else stays vanilla: reads
/// with no spawn in flight (naval capability, clan variables, bandit spawns), reads of some other
/// clan during a spawn, and every lord who is not in the map (his clanmates included).
///
/// Takes the service rather than a lookup delegate so the postfix, which runs on every read of
/// the getter, allocates nothing on the way here.
/// </summary>
public static class LordPartyTemplateResolution
{
    /// <returns>The template id to substitute, or null to leave vanilla's answer in place.</returns>
    public static string? Resolve(
        string? ambientOwnerId,
        string? ambientOwnerClanId,
        string? readClanId,
        ILordPartyTemplateService service)
    {
        if (string.IsNullOrEmpty(ambientOwnerId))
            return null;

        if (string.IsNullOrEmpty(ambientOwnerClanId) || string.IsNullOrEmpty(readClanId))
            return null;

        if (!string.Equals(ambientOwnerClanId, readClanId, StringComparison.Ordinal))
            return null;

        if (!service.TryGetTemplateId(ambientOwnerId!, out var templateId))
            return null;

        return string.IsNullOrWhiteSpace(templateId) ? null : templateId;
    }
}
