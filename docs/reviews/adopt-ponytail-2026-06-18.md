# Adoption Review: DietrichGebert/ponytail

**Date:** 2026-06-18 · **Source:** https://github.com/DietrichGebert/ponytail · **License:** MIT
**Procedure:** [`docs/ai-includes/external-repo-adoption.md`](../ai-includes/external-repo-adoption.md) · **Outcome:** SELECTIVE — one rule edit; plugin not installed.

## What it is

Ponytail is a YAGNI / minimalism enforcement plugin for AI coding agents (Claude Code, Codex, Copilot CLI, Gemini CLI, Cursor, and ~13 others). Its surface:

- A `SKILL.md` **decision ladder** — *does this need to exist? → use stdlib → use a native platform feature → reuse an installed dependency → can it be one line? → only then build minimal.*
- **Intensity levels** `lite / full / ultra / off`, toggled by keywords in the prompt.
- Two **hooks** — `SessionStart` injects the ruleset; `UserPromptSubmit` tracks the active mode.
- Commands `/ponytail-review` (over-engineering in current changes), `/ponytail-audit` (whole-repo), `/ponytail-debt` (deferred-optimization notes), `/ponytail-help`.
- A **benchmark** harness (promptfoo + a local Ollama variant) reporting ~54% fewer LOC / ~20% cost / ~27% faster.

Primarily JavaScript + Python. Actively maintained (benchmark results files dated 2026-06-17/18 at review time).

## Security vet (gating — passed, LEARN-FROM only)

Pulled the runnable surface (`hooks/*.js`, `hooks.json`, `package.json`) and read it. Findings by category:

- **No outbound network and no phone-home/telemetry** — the hooks are filesystem-bound; output goes only to the agent via the documented hook-output channel.
- **No credential/env exfiltration** — the activation hook reads `process.platform` for OS detection only; it does not read tokens, ssh, or arbitrary environment.
- **No install-time code execution of concern** — `package.json` has no lifecycle install scripts that fetch-and-run; hooks gate on Node being present and otherwise no-op.
- **Local writes only** — writes a small activation-flag file under the user's `~/.claude/` and reads `settings.json`. Benign, transparent, silent-fail.

**Verdict — two ways:** safe to **LEARN FROM** (read the text). We do **NOT INSTALL** — per the adoption procedure, installing eagerly loads its skill descriptions every session (context tax) and its `SessionStart` bootstrap is injected high-authority context (prompt-injection-by-design; a future-compromised fork could weaponize it). We port reviewed text into TAOM-owned files instead.

**Benchmark credibility:** self-reported, single-shot, on tiny generic JS/Python tasks (email validator, JS debounce, CSV sum, React countdown, FastAPI rate-limit) with Haiku-class models. The authors themselves note single-shot ≠ multi-turn agent sessions. Not transferable to a C#/.NET Bannerlord codebase — cited only as directional, never as a TAOM expectation.

## Novel vs. duplicative

| Ponytail idea | TAOM already has | Verdict |
|---|---|---|
| YAGNI decision matrix | [`simplicity-criterion.md`](../../.claude/rules/simplicity-criterion.md) — a *deterministic* keep/reject matrix, stronger than ponytail's prose ladder | Duplicative |
| Find over-engineering in current changes | `/deslop` (deletion-first) + `/deep-review` Agent 3 (efficiency) | Duplicative |
| Whole-repo over-engineering scan | `/improve` (10-category audit) | Duplicative |
| Research / reuse before building | *Research First* + *Verify Before Reference* (Critical Rules) + ADR-007 / ADR-002 + `think-before-coding.md` | Mostly covered — pieces exist, not stated as one ordered sequence |
| Track deferred optimizations | GitHub Issues + labels + `docs/reviews/rca-*.md` + `/improve` `plans/` | Covered (a new registry = redundant infra) |
| Intensity toggles (`lite`/`ultra`) for *rule strictness* | — | Novel, but a misfit (see below) |
| Explicit **ordered reuse ladder** | the pieces, scattered across several rules | **Novel kernel — adopted** |

## Decision

**Adopted (1):** the **reuse ladder**, TAOM-translated and folded into the existing always-load rule [`.claude/rules/think-before-coding.md`](../../.claude/rules/think-before-coding.md) (engine API → existing TAOM service/adapter → one-line delegation → minimal new code). ~12 lines into a rule that already loads — no new skill, hook, or eager-description cost. This is the one place TAOM had the pieces but never the ordered sequence.

**Consciously skipped:**

- **Intensity toggles** — misfit. TAOM rules are always-on *by design*; each traces to an RCA. A mode that relaxes simplicity checks is a footgun, and ponytail's "ultra = challenge whether the requirements are necessary" is the wrong default for a save-compat-sensitive, sealed-type game mod.
- **`/ponytail-debt` registry** — redundant. Deferred work already lives in GitHub Issues (labelled), RCA docs, and `/improve` `plans/`. A second system is the "new abstraction for a tiny win = reject" case in `simplicity-criterion.md`.
- **Inline "simplification ceiling" markers** — conflicts with TAOM's comment-density discipline; the keep-with-cost trade-off already goes to the PR / CHANGELOG per `simplicity-criterion.md`.
- **The plugin / skills / hooks / commands themselves** — duplicative of `simplicity-criterion.md` / `/deslop` / `/deep-review` / `/improve`, and installing carries the context-tax + injection risk above. Importing a YAGNI plugin we already out-implement would itself violate YAGNI.

## Attribution

Reuse-ladder framing imported from **ponytail** (DietrichGebert), MIT-licensed, and rewritten for TAOM's TaleWorlds/ADR domain. No ponytail code or files were vendored.
