# RCA — DeliverPersonnel captive count (#368), 2026-07-30

A player reported that Pelendur of Anduinbrethil would not take his prisoners for **Hands for the
Mines**. He was carrying 20 — Umbar Adûnaim Footmen and Bowmen, Mordor Nurn Warg Reavers, Black Uruk
Archers, Orc Reavers and Fighters — and the turn-in option stayed greyed out.

The quest counted a prisoner only when its `Occupation` was `Bandit`.
`grep -ro 'occupation="Bandit"' Main/_Module/ModuleData/` returns **8 matches in the entire mod**,
all hideout bosses (`dunland_raiders_boss`, `umbar_corsairs_boss`, `harad_raiders_boss`, …). Both
`DeliverPersonnel` configs were uncompletable in practice, not just the reported one.

The interesting part is not the filter. It is that the filter was **correct for vanilla and wrong for
TAOM**, and nothing in the pipeline was positioned to notice the difference.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | HIGH | `CountBanditPrisoners` / `RemoveBanditPrisoners` filtered on `Occupation.Bandit`. TAOM's LOTR bandit cultures point `bandit_bandit` / `bandit_raider` / `bandit_chief` at **ordinary faction troops** — `dunland_peasant` and `dunland_raider` are `occupation="Soldier"`, `culture="Culture.empire"`; `balcoth_volunteer` is `Culture.khuzait`; `harad_levy` is `Culture.aserai` — because those are the same entries Dunland, Rhûn and Harad recruit from. Only vanilla `looter`s (the one bandit clan TAOM's `spclans.xslt` deliberately keeps) ever passed. | Code↔data seam | The port carried the *vanilla* issue's dependency across without re-checking it against TAOM's data. The feature doc's own design survey records the dependency verbatim — row 33, `GangLeaderNeedsRecruits`: *"Player bandit-occupation troops; gold only"*. That line was analysis of what vanilla needs; it became an unexamined requirement of what TAOM shipped. | Rule is now "any non-hero prisoner", with troop type deliberately absent from the domain type. Lesson below. |
| 2 | HIGH (structural) | The rule lived in the quest shell, where TAOM's coverage policy (ADR-008: *Entry Points — not required*) exempts it from tests. Zero tests touched it. | Test placement | Nothing was skipped — the policy was followed. A completability-deciding predicate had simply been placed in the one layer the policy exempts. | Moved to `ILotrIssueService` as `CountDeliverableCaptives` + `PlanCaptiveHandover`; 13 tests, `LotrIssueServiceTests` 21 → 34. Lesson below. |
| 3 | MED | Gate and handover were two independently-written loops over the same roster, each re-stating the predicate. They happened to agree; nothing made them agree. | Duplicated invariant | Both were four lines long and visibly parallel, which is exactly the shape that reads as safe. | Both now call one private `IsDeliverable`. The data-flow agent verified the two are algebraically equivalent for a fixed snapshot. |
| 4 | LOW | `docs/features/lotr-issues.md` still documented the mechanic as *"hand over N bandit prisoners from the player's `PrisonRoster`"* (shipped-template table) and *"Deliver bandit prisoners as forced mine labor"* (design survey row 18). | Doc drift | Found by the completeness agent, verified and fixed in-session. I had grepped the code and the ModuleData for stale "bandit" wording and stopped there — `docs/` was outside the grep. | Both lines updated. `docs/features/lotr-issues.html` is a one-off June 16 export with no generator and is stale in other respects; left alone rather than selectively patched. |
| 5 | LOW | The inline comment justifying the omitted `woundedCount` said passing one "would double-count the wounded". | Imprecise rationale | Written from the observed clamp behaviour rather than from the engine's actual contract. | The compatibility agent read `AddToCountsAtIndex` in the installed v1.4.7 DLL: the engine derives the wounded delta from the count delta alone, and vanilla's own `TroopRoster.RemoveTroop` omits the argument identically. Comment now states that. |

### Not fixed

- **The 12 translated string files still say "bandit captives"** (German `Liefert Banditengefangene ab ({COUNT})`, Russian `пленных разбойников`, and so on for all 11 non-English languages). `tools/translate_with_claude.py` constructs `anthropic.Anthropic()` and `ANTHROPIC_API_KEY` is unset in this environment — reported rather than worked around, per `.claude/rules/environment-failures.md`. Recorded on #368 and in the CHANGELOG.
- **`LotrIssueDefinition.TroopSource`** is validated by the config provider against `{basic, elite, bandit, mount, prisoners}` and read by no template. Pre-existing, unrelated to this fix, confirmed inert by the data-flow trace. Left for its own change.

## Root-cause pattern

Findings 1 and 2 are the same failure seen from two sides. A predicate that decides whether a quest
can be completed was **written against vanilla's data model** and **placed in the layer TAOM does not
test**. Either alone is survivable: a wrong rule in a tested layer fails a test; a right rule in an
untested layer still works. Together they produce a feature that compiles, passes 4500 tests, passes
five review agents and an adversarial Codex pass, and does not work.

The load-bearing assumption was inherited rather than made. Nobody decided "TAOM prisoners will be
`Occupation.Bandit`" — the vanilla issue depended on it, the survey recorded that dependency, and the
port preserved it. Inherited assumptions are harder to see than authored ones precisely because no one
remembers choosing them.

Worth noting what TAOM's data did to make this true: `spclans.xslt` deletes **five** vanilla bandit
clans (`sea_raiders`, `mountain_bandits`, `forest_bandits`, `desert_bandits`, `steppe_bandits` —
lines 27-31) and `taom_spcultures.xml` declares **eight** `is_bandit="true"` cultures, of which five
are hideout-active and three (`gondor_soldiers`, `erebor_warriors`, `mirkwood_stalkers`) the file's
own comment marks INERT pending clans and hideouts. Every one of them points its `bandit_bandit` /
`bandit_raider` / `bandit_chief` slots at regular faction rosters. That was a deliberate, correct
content decision. It silently invalidated a C# predicate written in a different feature, months
apart. Neither side was wrong on its own.

**Both of those numbers were wrong when this RCA was first written** — "four clans", "five cultures" —
and a fact-check agent caught them. The four came from a grep filtered on `bandit`, which
structurally cannot match `sea_raiders`; the five came from reading the Wave-1 block and not
scrolling. A filtered search reported as a total is the same mistake as the bug this RCA is about:
a number inherited from a narrower context than the claim it was used to support. Corrected against
`grep -n 'xsl:template match="Faction\[@id=' spclans.xslt` and
`grep -c 'is_bandit="true"' taom_spcultures.xml`.

## Why each agent missed these

Against the ORIGINAL LotrIssues review pass (Wave 0/1 deep-review plus Codex #61):

- **Standards** — the code was compliant. `Occupation.Bandit` inside a quest shell is legal engine
  usage at a boundary; nothing about it reads as a violation.
- **Compatibility** — `Occupation.Bandit` exists in the engine with the right signature. The
  agent's question is "does this API exist", not "does our data ever produce this value".
- **Efficiency** — a four-line loop over a prison roster is unremarkable at any frequency.
- **Completeness** — tests existed for the service, and the quest shell is explicitly exempt from
  the coverage requirement. The absence it should have flagged was invisible because policy
  sanctioned it.
- **Data Flow** — the closest miss. Its brief covers XML→C# consumption in the *forward* direction
  (declared config that no code reads). This bug is the *reverse*: a C# constant whose matching data
  almost never exists. That direction was not in the prompt, and Codex #61 was aimed at the
  engine-type-collapse question, which it caught.

Against THIS review pass: four of five agents passed clean and were right to. Completeness found the
stale doc; compatibility corrected the wounded-count rationale and independently confirmed the
`woundedCount` omission is the engine's own idiom; the data-flow agent proved gate/handover
equivalence and index-shift safety by induction rather than asserting them.

## Verification state

- Suite 4542 passed / 0 failed / 2 skipped. `LotrIssueServiceTests` 21 → 34.
- `python tools/validate_moduledata.py` — PASS.
- Compatibility: 16 TaleWorlds API touch points verified against the installed v1.4.7 DLLs, 0
  incompatible.
- **Not yet verified in-game.** No saveable field moved, so an existing accepted quest should heal
  on the next `Refresh()` (`HourlyTickParty` fires hourly without player action). The proof is the
  reporter's save: journal reads `N/N`, the turn-in option is clickable, exactly N prisoners leave
  the roster, and the wounded split stays consistent — his roster is `12 + 8w`, so the wounded-clamp
  path runs on the first handover.

## Lesson recorded

`docs/reviews/lessons/data-content-cultures.md` — "A code-side filter on an engine enum must be
proven against TAOM's shipped data, not vanilla's."
