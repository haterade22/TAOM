using System.Globalization;
using TAOM.Core.Logging;

namespace TAOM.Features.DwarfAttacks;

/// <summary>
/// The in-game evidence for the dwarf attacks (#1087), judged by tools/ingame_checks/1087.toml. An attack is a per-hit
/// event, so each one only counts: per kind, a counter whose pass is a landed hit and whose fail is an impact that
/// found no victim, reported in the <c>[Session]</c> summary. The <c>[Check]</c> line is written once per kind for the
/// process: a pass on the first landed hit, with what the smoke needs to tune the reach, and a fail on the first
/// attack whose clip never played (a lost Armory binding or clip package). Main thread only: the trees tick from
/// <c>DwarfAttacksMissionBehavior.OnMissionTick</c>.
/// </summary>
public sealed class DwarfAttackChecks
{
    public const string HeadButtId = "taom.dwarf-attacks.head-butt";
    public const string DropKickId = "taom.dwarf-attacks.drop-kick";

    private readonly IOutcomeLog _log;
    private readonly OutcomeCounter[] _hits;
    private readonly OutcomeCounter[] _cutShort;
    private readonly bool[] _passWritten = new bool[2];
    private readonly bool[] _failWritten = new bool[2];

    public DwarfAttackChecks(IOutcomeLog log)
    {
        _log = log;
        _hits = new[] { log.Counter(HeadButtId + "-hits"), log.Counter(DropKickId + "-hits") };
        _cutShort = new[] { log.Counter(HeadButtId + "-cut-short"), log.Counter(DropKickId + "-cut-short") };
    }

    /// <summary>Pass: hits that landed. Fail: impacts that found no victim in reach.</summary>
    public OutcomeCounter Counter(DwarfAttack attack) => _hits[(int)attack];

    public void RecordHit(DwarfAttack attack, string dwarf, string victim, float distance, float progress,
        float travelled, int damage, bool knockedDown)
    {
        int i = (int)attack;
        _hits[i].Pass();
        if (_passWritten[i]) return;
        _passWritten[i] = true;
        _log.Pass(Id(attack), "", string.Format(CultureInfo.InvariantCulture,
            "dwarf={0} victim={1} distance={2:0.00} progress={3:0.00} travelled={4:0.00} damage={5} knockdown={6}",
            dwarf, victim, distance, progress, travelled, damage, knockedDown ? 1 : 0));
    }

    public void RecordNoVictim(DwarfAttack attack) => _hits[(int)attack].Fail();

    /// <summary>A hit reaction, a fall or death replaced the clip before its impact: normal in a melee.</summary>
    public void RecordCutShort(DwarfAttack attack) => _cutShort[(int)attack].Pass();

    public void RecordNeverPlayed(DwarfAttack attack, string dwarf)
    {
        int i = (int)attack;
        _cutShort[i].Fail();
        if (_failWritten[i]) return;
        _failWritten[i] = true;
        _log.Fail(Id(attack), "", "dwarf=" + dwarf + " the action never played: Armory binding or clip package missing");
    }

    private static string Id(DwarfAttack attack) => attack == DwarfAttack.HeadButt ? HeadButtId : DropKickId;
}
