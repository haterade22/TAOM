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
    }

    public override void RegisterEvents()
    {
        _logger.LogInfo("[Diplomacy] DiplomacyBehavior registering events");
        CampaignEvents.OnNewGameCreatedPartialFollowUpEvent.AddNonSerializedListener(
            this, (_, index) => OnNewGameCreatedFollowUp(index));

        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(
            this, _ => OnSessionLaunched());
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

    public override void SyncData(IDataStore dataStore)
    {
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
