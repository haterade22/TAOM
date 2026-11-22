using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.Diplomacy;

public class DiplomacyBehavior : CampaignBehaviorBase
{
    private readonly IDiplomacyService _service;
    private readonly IModLogger _logger;
    private readonly ICoopSessionProvider _coop;

    public DiplomacyBehavior(IDiplomacyService service, IModLogger logger, ICoopSessionProvider coop)
    {
        _service = service;
        _logger = logger;
        _coop = coop;
        _service.ResetBrokenAlliances();
    }

    public override void RegisterEvents()
    {
        _logger.LogInfo("[Diplomacy] DiplomacyBehavior registering events");
        CampaignEvents.OnNewGameCreatedPartialFollowUpEvent.AddNonSerializedListener(
            this, (_, index) => OnNewGameCreatedFollowUp(index));

        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(
            this, _ => OnSessionLaunched());

        // #1283: the war's own declaration line names only the called realm. The engine raises this event after its
        // ally check and before the declaration (AllianceCampaignBehavior.StartCallToWarAgreement), and Coop's server
        // copy of that method raises it too, so the line does not depend on patch order.
        CampaignEvents.OnCallToWarAgreementStartedEvent.AddNonSerializedListener(
            this, (calling, called, target) => _logger.LogInfo(CallToWarLine(calling?.StringId, called?.StringId, target?.StringId)));
    }

    internal static string CallToWarLine(string callingId, string calledId, string targetId)
    {
        return $"[Diplomacy] Call to war started: {callingId ?? "?"} calls {calledId ?? "?"} against {targetId ?? "?"}";
    }

    // CO-OP: the fifth diplomacy path, and the one both the internal review and the veto-scan test
    // missed — because it consults NO predicate. The scan looks for consumers of IsWarAllowed /
    // ShouldBlockPeace / IsAllianceDecisionAllowed; this reaches MakePeace and StartAlliance
    // directly from TAOM config, so it matched nothing.
    //
    // OnSessionLaunched fires on every peer, including a joining client. Ungated, a client would
    // locally end wars and restore alliances from ITS OWN config — so two peers whose TAOM config
    // or version differ end up with different diplomacy graphs, from the moment of join.
    //
    // Gated on PRESENCE, not authority: IsAuthority is BannerlordCoop-specific and fails open, so
    // under BannerlordTogether it would report true on both peers and this would not gate at all.
    internal void OnSessionLaunched()
    {
        if (_coop.ShouldDeferToHost)
        {
            _logger.LogInfo(
                "[Diplomacy][coop] permanent-alliance enforcement skipped — the host's diplomacy graph governs");
            return;
        }

        _service.EnforcePermanentAlliances();
    }

    // Campaign.cs:1448 LoadBehaviorData runs before Campaign.cs:1686 OnSessionStart, which raises OnSessionLaunchedEvent (CampaignEvents.cs:2090).
    public override void SyncData(IDataStore dataStore)
    {
        List<string>? pairs = dataStore.IsSaving ? new List<string>(_service.BrokenAlliancesForSave()) : null;
        dataStore.SyncData("_taom_brokenAlliances", ref pairs);
        if (dataStore.IsLoading)
            _service.RestoreBrokenAlliances(pairs);
    }

    // CO-OP: a first-time joiner builds a local campaign for character creation, and Coop's
    // StartAlliance prefix on a client asks the server instead of forming the alliance. The host's
    // save carries the alliances, so a Coop client skips them. IsCoopClient, not ShouldDeferToHost:
    // with a co-op mod TAOM cannot probe, a new campaign has no host save to carry them.
    internal void OnNewGameCreatedFollowUp(int index)
    {
        if (index == 0)
        {
            if (_coop.IsCoopClient)
            {
                _logger.LogInfo("[Diplomacy][coop] initial alliances skipped: the host's save carries them");
                return;
            }

            _logger.LogInfo("[Diplomacy] New game created — establishing initial alliances");
            _service.EstablishInitialAlliances();
        }
    }
}
