# Harness retro, 2026-11-03 (the rules review)

Not a weekly retro. On Mike's word, this was a full review of every TAOM rule and guideline, with a verdict on each: keep, change, delete or add. This record holds the baseline, the decisions and what is still owed, so that a later retro can measure whether the changes helped. The findings on the code the review added are in [rca-rules-review-2026-11-03.md](rca-rules-review-2026-11-03.md).

## Window and coverage

- **Audited:** 1,933 rules in AGENTS.md, CLAUDE.md, the always-on and path rules, the hooks, skills, agents, ADRs and guides, by 16 read-only agents. A second set of agents tried to refute each proposal, and the skeptics adjusted 181, refuted 23 and split 9. No row-by-row pass covered `docs/features`, `docs/migration`, or the lessons and RCA archives, because they hold detail and records, not standing rules.
- **Evidence window:** `python tools/harness_retro.py --since 2026-10-21 --exclude-session 1196e135-e79e-4e10-8741-b5074435d142`, which leaves out this review's own session, so that #1078's usage comparison stays clean. 69 main transcripts, 1,852 subagent transcripts, 1,714,717 records, 0 parse failures, 80 compactions. Claude Code versions seen: 2.1.288 to 2.1.314.
- **Merged:** `7a4c4c00c` on `bannerlord-1.5.x`.

## The verdict

1,239 of the 1,933 rules (64%) stay as written. The core philosophy is right, and the numbers support it: subject label misses fell from 41 to 0 after `commit-msg` landed, 753 commit bodies hold no dash with no gate, and 343 of 359 C# commits touch tests. The faults sat in five places.

1. **Protection gap.** Only `commit-msg` bound every committer, while ADR-008, ADR-011 and the linter's doc described gates as if they covered everyone.
2. **Private memory overruled the repo.** 20 of 35 feedback files held durable decisions with no repo home.
3. **The costliest rules gave several answers.** "Which commit gets a review" had four answers, and "who authorizes Codex" had two.
4. **False text.** `IAdapterFactory` had 0 C# hits and 48 Markdown hits. Nine guides showed no read in the transcripts except this audit's.
5. **Prose where a machine could check.** 9 of 18 path rules exceeded their cap, and RCAs show repeats wherever only text guards a rule.

## Baseline for the next retro

Compare these at the next retro. Spend is at API list prices, a proxy for relative size, not a bill.

| Measure | Value at this review | Source |
|---|---|---|
| Spend by agent | `deep-reviewer` 51.4%, main 28.8%, implementer 6.4%, workflow agents 5.7%, architect 4.0% | retro section 11 |
| Main session mean context | 437,869 tokens; 54.2% of main spend at 500k or over | retro section 11 |
| Main cache rebuilds after an idle hour | 135, mean 472,656 tokens | retro section 11 |
| `MEMORY.md` | 13,807 B, 97 lines, against ADR-011's 4 KB and 40 lines | retro section 8 |
| RCAs added in the ten days to 2026-11-03 | 69 of 555 | git log |
| Reviews parsed | 86: 268 real bugs, 22 false positives, 39 misses | `analyze_reviews.py --summary` |
| Banners against skills | HARNESS RETRO DUE printed in 22 sessions and invoked 0 times; UPSTREAM AHEAD printed in 45 and invoked once | retro section 7 |

**Yield by lens,** from `analyze_reviews.py --summary`, which reads 346 findings from the 20 of 555 RCAs that record a lens. This is the measure the review pipeline had none of. Credits count every lens that found a row. Sole counts the rows that lens alone found, which is closer to what it adds. Not every lens runs on every review, so this has no denominator yet.

| Lens | Credits | Sole finder |
|---|---|---:|
| Data flow | 116 | 47 |
| Tooling | 92 | 49 |
| Completeness | 84 | 30 |
| Engine | 59 | 18 |
| XML | 52 | 14 |
| Standards | 50 | 16 |
| Design | 40 | 4 |
| Convergence | 19 | 15 |
| Efficiency | 17 | 5 |
| Codex (in an RCA's lens column) | 13 | 4 |

## Decisions

Mike answered each from the catalogue page, and then told the session to proceed with its recommendations.

| # | Question | Answer |
|---|---|---|
| D0 | An issue for this work | Waived |
| D1 | Gates for every committer | Yes, in stages: `.githooks/pre-commit` beside the Claude gates, then those gates go after two weeks of agreement ([commit gates](../features/commit-gates.md)) |
| D2 | A standing word for Codex | At tier 1. A skipped pass is reported as owed before merge. Tier 2 asks once per session |
| D3 | An RCA file for every confirmed finding | No: CRITICAL, HIGH, player-reached or a repeat of a recorded cause. Held until the comparison |
| D4 | The tier table's hole | Plain C#, repo XML and XSLT join tier 2. Held until the comparison |
| D5 | Serena | Removed |
| D6 | Contributor tests on this machine | Only on Mike's word per PR |
| D7 | `/release` item 4b's version grep | Stands, as the one read-only exception |
| D8 | Commit messages and PR text in the STE output style | Exempt; AGENTS.md "Human prose" governs them |
| D9 | ADR edits | Keep each decision, delete false samples and false enforcement claims, one dated amendment per ADR |
| D10 | Provenance text | Narrowed. The `TAOM.dll` line stays. The `HoNFormationPreset` name and base id, and the euphemism sweep, stay Mike's |
| D11 | Memory clean-up | Durable decisions move to where they fire, and each memory line goes once its home is committed |
| D12 | The agent teams flag | Removed, see below |

## The agent teams flag (D12)

`CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS` was thought to cause `MEMORY.md` to load in subagents, against the docs. Two headless sessions on the same model, one with the flag at 0 and one at 1 (sessions `7ac339dd-d16f-417a-ab1c-53a490af4f3a` and `810b1879-ac9f-4267-a88c-cf0ae078ec90`), each spawned one subagent. The subagent's `instructions` list named `MEMORY.md` both times, and `SendMessage` was offered both times. So the flag does not cause the load, and nothing here used what it adds: TeamCreate had 0 calls. It left both settings files. The load itself stays unexplained, so `MEMORY.md` stays small. The CLI that ran the probe was 2.1.288, older than the panel's 2.1.314.

A third run, with the flag unset in both settings files on the newest panel binary (2.1.317, session `f822fc4e-f52d-4e66-9206-70400ec482dc`), gave the same result: `MEMORY.md` loaded in the subagent and `SendMessage` was on the deferred tool list. `TeamCreate` was on no list, and no tracked file needs it.

## What changed

- **Gates.** `.githooks/pre-commit` and `pre-merge-commit`; `EntryPointLineCountGateTests`, `ServiceSignatureGateTests`, `ServiceLocatorGateTests`, `PrefabSpriteTests`, the #371 pairing test; validator codes `TROOP_ID_REMOVED`, `INCONSISTENT_WEAPON_SLOT` and `TOWNSFOLK_WITHOUT_BATTLE_ROSTER`; `tools/data_gates.py`; `lint_docs` rule text drift and the commit view.
- **Measures.** Yield per lens in `analyze_reviews.py`, and banner, version and rule-load tables in `harness_retro.py`.
- **Removed.** Seven hooks, three agents, six guides and Serena. `csharp-architecture.md` went from 29.7 to 9.9 KB, and `gui-ui.md` from 25.1 to 16.7 KB.
- **Corrected.** The Fix-first list in the plan, among them a short spear rule that left shield troops unarmed, an orientation row that was false since 2026-09-18, a lens that asked for a `DependedModule`, orders for paid calls without the maintainer's word, and `dotnet test` lines that could deploy over a running game.

## Owed

- **About 2026-11-10, after #1078's comparison reads out:** D3, D4 and the other review-spend rows, the second-occurrence rule (G8-048), the lens 5 deletion item (G8-A03), and narrowing the `harness-facts.md` paths.
- **About 2026-11-17:** compare `.githooks/pre-commit` with the Claude commit gates, then delete those gates. Decide first what to do about the two limits in [commit gates](../features/commit-gates.md): the ModuleData gates read the working tree, and the hook cannot see an amend.
- **Codex at tier 2 on the merged gates:** never run. Mike's word.
- **Next retro:** read the tables above again, and ask whether the lens table has grown enough to judge a lens. It reads 20 of 555 RCAs today.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/agent-teams.md](../ai-includes/agent-teams.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
