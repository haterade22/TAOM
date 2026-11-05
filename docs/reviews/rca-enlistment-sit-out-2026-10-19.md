# RCA: Enlistment "Leave it to the others" gate (#830, deep-review, 2026-10-19)

## Top-line

`/deep-review` (seven lenses in two waves, then one convergence pass) on #830 found **no way-out
softlock**: the gate only narrows vanilla's Send-troops option, and every Refused verdict requires the
Attack option to be available, so a soldier always keeps an enabled way forward where vanilla gave him
one. It found **8 MEDIUM and a set of LOW** findings, all fixed before the first commit.

Two findings mattered. First, the payout could be beaten at allowed odds: click Attack, retreat at once
from the mission, then "Leave it to the others". The retreat left a merit sample, and the payout treated
any sample as a fought battle, so the soldier kept renown and paid no trust. Second, the verdict that
decides the pay was carried in a ledger that was written only while the gate was open and read with a
fallback for "empty", so a battle the soldier led, a failed adapter read and a fought battle could all be
paid as the wrong case.

## Findings

| # | Sev | Finding | Category | Why missed | Fix and preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM (Data flow) | A walkout sample (Attack, then retreat, then simulate) counted as a fought battle: renown kept, no trust cost. | Two signals of "did he fight" | The payout read the mission sample and the simulation flag but not the sample's own walkout flag. | Sat out = simulated, an allowed click-time verdict, and no sample or a never-downed walkout; the walkout keeps its band XP and gold. Lesson appended to `lessons/campaign-mechanics.md`. |
| F2 | MEDIUM (Data flow, Completeness, Design) | The ledger was written only inside the gate and read with an "empty means forced open" fallback, so a led battle, a fail-open adapter read and a stale verdict could be paid as a sit-out. | One-slot carrier with a default that means something | "Empty" was given a meaning instead of meaning nothing. | The verdict is recorded on every menu showing (not after the battle has a winner) and consumed at payout; only an allowed verdict can make a sit-out. |
| F3 | MEDIUM (Engine compat, Data flow) | The lord's-hall verdict could never fire: the hall phase is inside the siege-assault map event, so the siege rule always won. | Unreachable branch | The plan read the hall state, not where the engine keeps it. | Deleted with its string, rows and tests. |
| F4 | MEDIUM (Standards) | `EnlistmentContentBehavior` grew past 150 lines with player wording in it. | Entry-point size | The line was added where the battle ends. | The merit toast and the sat-out lines live in the announcer; 148 lines. |
| F5 | MEDIUM (Standards) | The ledger had its own reset path in the behavior, untested, beside the feature's single reset method. | Session-reset convention | Added where the events were. | Consumed in `ServiceMaintenanceService.ResetSessionCaches`, with the merit accumulator, and tested. |
| F6 | MEDIUM (XML, Standards) | "Paid half the battle's due" hardcoded a configurable factor, and rewording it later would have left 12 English seeds the paid run skips. | Text tied to a setting | Written for the shipped value. | Reworded without a fraction in the C#, the English row and all 12 seeds. |
| F7 | MEDIUM (Completeness) | Dropping the payout's simulation argument compiled and kept every test green. | Optional parameter on a load-bearing input | A default kept old tests compiling. | Required parameter. |
| F8 | MEDIUM (Completeness) | Nothing pinned that the Attack probe mirrors vanilla's Attack condition; the never-stranded proof rests on it. | Engine mirror unpinned | The probe was argued, not tested. | IL pins for the attack wrapper's callees and for the only writer of the simulation flag. |
| F9 | LOW (several) | "Siege" caught relief battles and blockades; the strength share used two sums instead of the engine's read-only method; the allowed verdicts dropped vanilla's tooltip; one gate written twice; an interface nothing fakes; the smoke step for #577 could pass without the crash path; docs and registry counts. | Scope and polish | Various. | All fixed; the #577 step now requires one enabled click per run. |

## Convergence pass

One reviewer checked the applied fixes against the installed v1.5.5 sources and the installed co-op
module. It found no behaviour defect and found no sequence that overwrites a click-time verdict or pays
a led or fought battle as a sit-out. It confirmed that the winner check must stay before the gate:
`MapEventSide.RemovePartyInternal` promotes a new side leader when the leader party leaves, so inside the
gate an aftermath refresh could record Unchanged over the click-time verdict. It found six LOW items, all
fixed: three comments or doc lines that overclaimed (the two probes are not the same probe; the ledger
recording; the win and loss counters), no test of the composed tooltip (now tested, with a proven RED),
a rounding test that could not tell away-from-zero from to-even, and a stale announcer summary.

## Root-cause pattern

F1 and F2 share a cause: the payout decided "did he stay back" from signals that each meant something
slightly different (a sample, a flag, an empty slot) without one owner of the question. The fix gives
that question one source, the verdict the soldier saw when he clicked, and makes every other signal a
narrowing of it.

## Why each lens caught or missed these

- **Standards:** F4, F5, the interface, the naming and the registry count.
- **Engine compatibility:** F3 and the siege scope, with every member verified on v1.5.5.
- **Data flow:** F1, F2, F3 and the accumulator lifecycle.
- **XML:** F6 and the threshold note; all gates passed.
- **Efficiency:** no findings.
- **Completeness:** F7, F8, the walkout rule conflict, the ledger states and the smoke step.
- **Design:** the ledger shape that fixed F2, the shared gate, the engine strength read and the tooltip.

## Feedback memories to codify

None; the lesson below covers the pattern.
