# RCA: Enlistment battle-exit block (#827, deep-review, 2026-10-18)

## Top-line

`/deep-review` (six lenses in two waves: Standards, Engine compatibility, Data flow, Efficiency, then
Completeness and Design) on the #827 change found **2 MEDIUM and 7 LOW** findings, plus four optional
nits. The change adds a Patch66 postfix on `Helpers.MapEventHelper.CanMainPartyLeaveBattleCommonCondition()`
that hides vanilla 'Leave' and 'Abandon army' while an enlisted soldier serves in his commander's
battle. Every finding was fixed in the same session before the first commit; one convergence reviewer
then checked the applied fixes (see "Convergence pass").

The finding that mattered: hiding 'Leave' could leave a soldier with no enabled option at all. A
soldier who joined without an army (the commander has no kingdom, so the army merge fails, #495) and
whose player holds a safe-passage truce with the enemy faction sees vanilla grey out both 'Attack' and
'Send troops' (`MenuHelper.CheckEnemyAttackableHonorably`, v1.5.5 `Helpers.MenuHelper.cs:452-463`).
`MapEventManager` never advances the player's own map event, so with 'Leave' hidden the commander's
battle could never finish. The plan claimed 'Send troops' always remains; it was checked for visibility
and for whether it reads the hooked predicate, never for the paths that disable it.

## Findings

| # | Sev | Finding | Category | Why missed | Fix and preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM (Data flow) | Truce softlock: a no-army soldier under a safe-passage truce has 'Attack' and 'Send troops' disabled and 'Leave' hidden; the battle never resolves. | Menu loses its last option | The spec's verifier and the plan checked that 'Send troops' is shown (it counts every healthy troop on the side) and that it does not read the predicate. Neither walked the option's disable paths (`args.IsEnabled = false`): the honour check at `MenuHelper.cs:477`, zero morale at `:512-516`. | `IEncounterAdapter.IsAttackBarredByTruce` asks vanilla's own check with a probe `MenuCallbackArgs`, and `ShouldBlockBattleExit` releases the exits when it is true. Test `ShouldBlockBattleExit_TruceBarsAttackAndSendTroops_ReturnsFalse`. Lesson appended to `lessons/campaign-mechanics.md`. |
| F2 | MEDIUM (Completeness) | The postfix itself had no test: the true-to-false-only rule, the early return, the lazy resolve and the fail-open catch were unpinned, though #827 Testing names the fail-open path. | Hook coverage | The sibling Patch66 hooks have no direct tests either, so the gap looked conventional. `tests.md` asks 80% for hooks. | `MapEventHelperCanMainPartyLeaveBattlePatchTests` (five cases, IoC container swap as in `Patch96TournamentMaxBetTests`). One-off. |
| F3 | LOW (Engine compat, corrected by Completeness) | The patch comment and registry said surrender "stays hidden" because the commander shares the side. Wrong reason, and surrender has clauses that never read the predicate. | Engine text | The surrender clause was read once for the negated predicate; its other clauses and the predicate's defender branch were not traced. | Rewritten: the predicate's defender branch is unreachable in v1.5.5 (`MobileParty.SiegeEvent` is `BesiegerCamp?.SiegeEvent`), surrender's predicate clause needs the defender side, and its other clauses still apply as in vanilla. |
| F4 | LOW (Engine compat) | The "not closed" list missed raiding_village 'End Raiding' and the encounter menu's sally-out 'Return to {SETTLEMENT}'. | Scope text | The list came from the spec, which named only the exits its verifier had traced; nobody read the menu's full option list. | Both added. 'End Raiding' belongs to the raid item (#831). |
| F5 | LOW (Standards, Engine compat) | The registry said the IL caller pin makes "a new caller fail loudly", but the scan covers TaleWorlds.CampaignSystem only, and the installed co-op module also reads the predicate. | Claim wider than its test | The test comment stated its scope; the registry sentence did not carry it. | Registry scoped to CampaignSystem, with the v1.5.5 fact that no other TaleWorlds or official module assembly references the predicate. |
| F6 | LOW (Standards) | `docs/reference/taleworlds-api-snapshot/patch-targets.md` had no row for the new patch. | Generated doc not refreshed | The snapshot README asks for a regeneration after any patch change; the plan did not list it. | Regenerated (the new row, plus a row an earlier diplomacy commit left out). |
| F7 | LOW (Standards, Data flow, Design) | Consumer lists went stale: `BattleCommandPolicy` names three consumers, `IEncounterAdapter.IsMainPartyLeadingItsBattleSide` names one reader. | Stale cross-reference | The plan reused the policy without searching for prose that enumerates its consumers. | Both updated; the policy now says the menu gate starts from it but releases the exits under a truce. |
| F8 | LOW (Completeness) | Docs gaps: the feature doc lacked the owed in-game checks and the scope limit, the co-op stance was half written, and the rationale described only the army case. | Doc completeness | The docs were written from the spec's army-case framing. | Feature doc owed item with three checks; registry co-op stance (host-only joins and normalizer, so a client never holds `EnlistedBattle`); no-army rationale in the patch, interface and registry. |
| F9 | LOW (nits) | Test name order, the `??=` idiom, no pin of the `Patch66_Enlistment` category literal. | Conventions | Copied from the sibling patches. | All applied in the new code; the siblings are left as they are. |

Not applied, each with its reason: the `SubModule.cs` Patch66 comment (single-owner file, recommended
to its owner); the naval raft corner where 'Send troops' hides (NavalTravel is parked and no path to it
is shown); narrowing the hook to the two private menu conditions (the shared predicate is proven
harmless to surrender, and the co-op module's surrender postfix stays inert because a client never
holds `EnlistedBattle`).

Follow-up, outside this change: `pwsh tools/snapshot_api_surface.ps1 -Check` reports drift on every
tree, because it compares the whole committed file with generated output that has no backlinks footer
(`tools/snapshot_api_surface.ps1:250-258`). Running `tools/build_backlinks.py` in this branch also
rewrote 292 unrelated docs whose generated blocks were stale; those edits were reverted.

## Convergence pass

One reviewer checked the applied fixes. It confirmed the truce probe is side-effect free, leaves the
army path unchanged, and can throw only where vanilla's own conditions already throw in the same menu
refresh. It found two LOW defects, both fixed: the not-in-battle test missed `Discharging` (now a loop
over every state but `EnlistedBattle`, so a new state is covered without a new row), and the registry
still claimed any new caller fails the IL pin (now scoped to TaleWorlds.CampaignSystem). Its optional
notes were also applied: the fail-open hook test asserts the service was asked, a repeated surrender
sentence was removed, and the owed in-game check now expects the state to return to
`EnlistedAttached` after a truce 'Leave'.

## Root-cause pattern

F1, F3 and F4 share one shape: a claim about vanilla menu options was checked along one axis and stated
as if it covered all of them. 'Send troops' was checked for visibility, not for being enabled;
surrender was checked for the predicate clause, not its other clauses; the open exits were listed from
the predicate's readers, not from the menu's option list. The earlier lesson "A wait menu's condition
delegate hides EVERY option" (`lessons/campaign-mechanics.md`, #567) already says to walk every option
on a menu before calling a menu change verified. This change walked the options for visibility only,
so the lesson's scope is widened to the enable state.

## Why each lens caught or missed these

- **Standards:** found F5, F6, F7 and the nits. Correctly passed every ADR check; menu reachability is
  outside its rule set.
- **Engine compatibility:** found F3, F4 and F5, and proved the caller set across every installed
  assembly. It checked that 'Send troops' does not read the predicate and noted the zero-morale corner,
  but did not follow the honour check into the no-army case.
- **Efficiency:** no findings, correctly; it confirmed the predicate runs per menu refresh, not per
  frame.
- **Data flow:** found F1 by walking every option on the encounter menu in the no-army case, and F7.
- **Completeness:** found F2 and F8, corrected F3's first rewrite, and listed the follow-on work F1
  implies.
- **Design:** proposed the F1 fix that asks vanilla's check instead of copying its conditions.

## Feedback memories to codify

None. The preventive rule widens an existing lesson; no new memory is warranted.
