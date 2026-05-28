# RCA — Scene/Bandit Tooling Deep Review (2026-05-28)

**Scope:** `/deep-review` over the uncommitted changeset (post-commit `4ed298b`): `sp_battle_scenes.xml` battle-terrain fix, `taom_spcultures.xml` `faction_banner_key` additions, 4 new Python audit/remap tools, scoped rule + docs. No new C# (the bandit C# was committed + Codex-reviewed earlier this session).

## Top-line

Launched 3 targeted agents (Data Flow, Tooling Correctness, Docs/Rule Consistency) instead of the 5 C#-core agents, because the changeset has zero new C# — Standards/Compat/Efficiency had no surface. Data Flow and Docs passed clean. Tooling Correctness found a BOM-handling fragility/inconsistency spread across the new Python script family + one LOW case-inconsistency. **No live data corruption** — verified the script-written files have correct BOM state (repo XML: no BOM, preserved; external `TAOM_Map/settlements.xml`: BOM kept; all parse). All findings fixed in-session.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED (not HIGH — output verified correct) | `migrate_hideouts_to_lotr.py` read/wrote with `utf-8` (not `utf-8-sig`); 3 sibling scripts wrote the BOM via a U+FEFF source literal `"﻿"`. Works today but fragile (literal could change if the `.py` is re-encoded) and inconsistent across the family. | Tooling robustness / convention drift | The scripts were authored across many separate turns as one-offs; no shared BOM/encoding convention was established up front, so each turn reinvented the I/O and they drifted. The standard 5 deep-review agents are C#-focused and don't review Python tooling — this was only caught because a dedicated Tooling-Correctness agent was launched for this changeset. | Fixed: all 6 scripts now use the byte-level pattern — `read_bytes().startswith(b"\xef\xbb\xbf")` to detect, `read_text("utf-8-sig")` to decode, `write_bytes((b"\xef\xbb\xbf" if had_bom else b"") + text.encode("utf-8"))` to write. Convention documented in this RCA + `tools/README.md`. |
| 2 | LOW | `audit_scene_names.py` vanilla-inventory printout used `s in folders` (case-sensitive) while the crash-suspect check used `s.lower() in folders`. Cosmetic mislabel only (read-only audit). | Internal inconsistency | Copy-paste: the case-insensitive fix was applied to the crash-suspect path but not the inventory path when case-insensitivity was added mid-session. | Fixed: line uses `s.lower()`. |

## Root-cause pattern: new multi-script tool family without a shared I/O convention

Both findings trace to the same cause — a family of data-mutating scripts grown incrementally across turns, each re-implementing file I/O, with no agreed BOM/encoding/case convention. The result was drift: `remap` got the cleanest pattern (it was written last and most carefully), `migrate` got the oldest/weakest (plain `utf-8`), and the case-insensitivity fix landed on some paths but not all.

**Generalizable rule (for future TAOM data-mutating Python scripts that edit ModuleData XML):** preserve byte-exact encoding. Always: detect BOM via `read_bytes().startswith(b"\xef\xbb\xbf")`, decode via `utf-8-sig`, and write via `write_bytes((b"\xef\xbb\xbf" if had_bom else b"") + text.encode("utf-8"))`. Never write a BOM as a string literal (fragile under re-encoding) and never read with plain `utf-8` (leaves the BOM as a stray U+FEFF in the decoded string). Scene/settlement-data comparisons and replacements must also be case-insensitive (Windows scene/asset lookup is). This is now the documented pattern in `tools/README.md` and is referenced by `.claude/rules/vanilla-data-comparison.md`.

## Why each core deep-review agent didn't catch this

The 5 standard deep-review agents are C#-centric (adapter pattern, TaleWorlds API, hot-path allocations, test coverage, C#/XML data flow). None reviews Python tooling correctness. The BOM/encoding issue lives entirely in `.py` files and would never have been flagged by the standard set. It was caught only because this changeset's risk profile (data-mutating scripts touching an external game-install file) warranted a **dedicated Tooling-Correctness agent** — exactly the "scale the review to match the risk / launch additional focused agents" clause in the deep-review skill.

**Deep-review skill enhancement worth considering:** when a changeset includes new/modified `tools/**/*.py` or `*.ps1` that WRITE files (especially outside the repo), add a standing "Tooling Correctness" agent (encoding/BOM preservation, idempotency, dry-run gating, backup-before-write, regex over-match). Captured here rather than editing the skill mid-review.

## Verification after fixes

- All 6 scripts: `ast.parse` syntax OK.
- Idempotent re-run (data already applied): `add_bandit_faction_banner_keys` 0 added / 8 skipped; `remap_stale_scene_names` 0 replacements; `migrate_hideouts_to_lotr` 0 modified — no spurious writes.
- Script-written files re-checked: repo XML have no BOM (preserved), external settlements.xml retains its BOM, no stray/double BOM, all parse.

## Verdict

Data Flow PASS, Docs PASS, Tooling PASS (post-hardening). Ready for commit. No HIGH findings; the 2 confirmed findings (1 MED, 1 LOW) are fixed.
