using System;
using System.Collections.Generic;
using System.Globalization;
using TAOM.Core.Logging;
using TAOM.Features.TroopProgression;

namespace TAOM.Features.CastleRecruitment;

/// <inheritdoc cref="ICastleRecruitmentService"/>
public class CastleRecruitmentService : ICastleRecruitmentService
{
    // Round-robin order. RuralNotable is intentionally absent (castle-NRE in vanilla GetBasicVolunteer).
    private static readonly CastleNotableOccupation[] OccupationOrder =
    {
        CastleNotableOccupation.GangLeader,
        CastleNotableOccupation.Headman,
        CastleNotableOccupation.Merchant,
        CastleNotableOccupation.Artisan,
    };

    public const string AiOffCheckId = "taom.castle-recruit.ai-off";
    public const string AiBlockedCounterId = "taom.castle-recruit.ai-blocked";
    public const string AiRecruitedCounterId = "taom.castle-recruit.ai-recruited";

    private readonly ICastleRecruitmentSettingsProvider _settings;
    private readonly VolunteerProductionService _production;
    private readonly IOutcomeLog _outcomes;
    private readonly OutcomeCounter _aiBlocked;
    private readonly OutcomeCounter _aiRecruited;

    // The #918 day tally. Main thread only: the recruit event and the daily tick run on the campaign thread.
    private long _blockedBase;
    private long _dayAiRecruits;

    public CastleRecruitmentService(ICastleRecruitmentSettingsProvider settings, VolunteerProductionService production,
        IOutcomeLog outcomes)
    {
        _settings = settings;
        _production = production;
        _outcomes = outcomes;
        _aiBlocked = outcomes.Counter(AiBlockedCounterId);
        _aiRecruited = outcomes.Counter(AiRecruitedCounterId);
    }

    public bool IsEnabled => _settings.IsEnabled;

    public bool IsAiEnabled => _settings.IsEnabled && _settings.IsAiEnabled;

    public bool BlocksAiRecruitAtCastle(bool sellerInCastle, bool buyerIsPlayer)
    {
        bool blocks = sellerInCastle && !buyerIsPlayer && !IsAiEnabled;
        if (blocks)
            _aiBlocked.Pass();
        return blocks;
    }

    public CastleRecruitKind ClassifyCastleRecruit(bool settlementIsCastle, bool hasSource, bool recruiterIsNull, bool recruiterIsPlayer)
    {
        if (!settlementIsCastle || !hasSource)
            return CastleRecruitKind.None;
        if (recruiterIsNull)
            return CastleRecruitKind.Garrison;
        return recruiterIsPlayer ? CastleRecruitKind.Player : CastleRecruitKind.Ai;
    }

    public void BeginCastleRecruitDays()
    {
        _blockedBase = _aiBlocked.Passes;
        _dayAiRecruits = 0;
    }

    public void NoteCastleRecruit(CastleRecruitKind kind, int day, string? settlementId, string? recruiterId, string? sourceId, string? troopId)
    {
        switch (kind)
        {
            case CastleRecruitKind.Ai when IsAiEnabled:
                _aiRecruited.Pass();
                break;
            case CastleRecruitKind.Ai:
                _aiRecruited.Fail();
                _dayAiRecruits++;
                // At once, so a recruit after the day's last line that is followed by an exit, a crash or a load
                // is still in the log, with the castle and the lord the daily counts cannot name.
                var dayText = N(day);
                _outcomes.Fail(AiOffCheckId, "ai-" + dayText, "day=" + dayText + " event=ai-recruit settlement=" + Id(settlementId)
                    + " recruiter=" + Id(recruiterId) + " source=" + Id(sourceId) + " troop=" + Id(troopId));
                break;
        }
    }

    public void ReportCastleRecruitDay(int day)
    {
        long passes = _aiBlocked.Passes;
        long blocked = passes - _blockedBase;
        _blockedBase = passes;
        var dayText = N(day);
        if (IsAiEnabled)
            _outcomes.Skip(AiOffCheckId, dayText, "reason=toggle-on day=" + dayText);
        else
        {
            var detail = "day=" + dayText + " blocked=" + N(blocked) + " ai_recruits=" + N(_dayAiRecruits);
            if (_dayAiRecruits > 0)
                _outcomes.Fail(AiOffCheckId, dayText, detail);
            else
                _outcomes.Pass(AiOffCheckId, dayText, detail);
        }
        _dayAiRecruits = 0;
    }

    public IReadOnlyDictionary<CastleNotableOccupation, int> GetOccupationTargets()
    {
        var dict = new Dictionary<CastleNotableOccupation, int>();
        int total = _settings.NotablesPerCastle;
        if (total < 0)
            total = 0;
        for (int i = 0; i < total; i++)
        {
            var occ = OccupationOrder[i % OccupationOrder.Length];
            dict[occ] = dict.TryGetValue(occ, out var count) ? count + 1 : 1;
        }
        return dict;
    }

    public float GetSlotProductionProbability(int slotIndex, string? kingdomKey, bool ownerIsPlayerClan)
    {
        if (slotIndex < 0)
            return 0f;
        // 0.75 * 0.85^(index+1): slot 0 ≈ 0.64, slot 5 ≈ 0.28. Mirrors vanilla's decaying shape
        // (0.75 * pow(~0.85, index+1)) without the faction-fief term that requires the engine
        // settlement graph (and NREs for castles).
        float p = 0.75f * (float)Math.Pow(0.85, slotIndex + 1);
        p = p < 0f ? 0f : p > 1f ? 1f : p;
        // The War Chronicle's VolunteerRate multiplier of the owner's kingdom (#765), as towns and
        // villages get it through TaomVolunteerModel. No culture: castles never had the respawn feats.
        return _production.Compute(p, null, kingdomKey, ownerIsPlayerClan);
    }

    private static string N(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Id(string? id) => string.IsNullOrEmpty(id) ? "none" : id!;
}
