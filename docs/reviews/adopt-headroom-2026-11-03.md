# Headroom adoption review (2026-11-03)

Review of [headroomlabs-ai/headroom](https://github.com/headroomlabs-ai/headroom) against TAOM's AI
harness, through `/adopt-external`. Mike asked about it during the usage analysis (#1078), because
yotthani reported that it helped their usage, and on 2026-11-03 he approved an offline measured
trial. **Verdict: not installed, nothing ported.** On TAOM's traffic the default proxy mode would save
about 1% of spend, and it changes code text that a reviewer reads.

## Coverage

- **Repository:** `main` at `7e0094d` (2026-11-03), Apache-2.0, read through a clone in the session
  scratchpad; nothing in the clone was run. Read in full: `headroom/compress.py`. Read in part or
  searched: `headroom/offline.py`, `headroom/config.py`, `headroom/telemetry/beacon.py`,
  `headroom/update_check.py`, `headroom/onnx_runtime.py`, `headroom/transforms/content_router.py`,
  `headroom/transforms/kompress_compressor.py`, `headroom/proxy/server.py`,
  `headroom/proxy/handlers/anthropic.py`, `headroom/learn/writer.py` and `wiki/LIMITATIONS.md`.
- **Package:** the wheel `headroom_ai-0.42.0-cp310-abi3-win_amd64.whl` from PyPI (uploaded
  2026-10-26) is what ran. The clone is newer and some files differ, so the defaults this review
  relies on were also checked in the installed wheel.
- **yotthani:** `gh search code headroom --owner yotthani` and `gh search commits headroom` with
  `--author yotthani` and with `--owner yotthani` came back empty. That covers GitHub's index of public
  repositories only and says nothing about their local setup.
- **TAOM:** every local transcript since 2026-10-24, main sessions and subagents.

## What Headroom is

A Python package with a Rust core. `headroom wrap claude` points Claude Code at a local proxy that
rewrites each request before it reaches Anthropic. It has compressors for JSON arrays, search
results, logs and diffs, plus Kompress, an ONNX model that drops words from plain text. A lossy result
carries a marker such as `[240 words compressed to 198 (from 91 source lines). Original content
preserved. Retrieve more: hash=de76d328246d3def7d65463d]`, and the model can fetch the original through
a `headroom_retrieve` tool.

Two defaults set the limit for TAOM:

- **The proxy's default mode is `cache`.** Every earlier turn is frozen and only the newest tool
  results can change (`_strict_previous_turn_frozen_count`), so a cached prefix stays intact and nothing
  old ever shrinks.
- **Read, Glob, Grep, Write and Edit are excluded by name** (`DEFAULT_EXCLUDE_TOOLS`), and
  `protect_recent_reads_fraction` defaults to 0.0, so their results are never lossy-compressed in this
  mode. Read alone is 38.8% of TAOM's tool output.

## Security

- **Safe to learn from:** yes.
- **Safe to install into the harness:** no.
  - `headroom wrap` sends all API traffic, the login token included, through a third-party local
    process.
  - The beacon is "on by default, opt-out" and "Uploads an anonymous, content-free session summary to
    Headroom Labs" (`headroom/telemetry/beacon.py`). `HEADROOM_OFFLINE=1`, `HEADROOM_BEACON=off`,
    `HEADROOM_TELEMETRY=off` and `DO_NOT_TRACK=1` each turn it off.
  - The originals behind lossy results are kept in `~/.headroom/ccr_store.db`. The trial's store grew
    to 12 MB, and a text search of it found TAOM source paths in plaintext.
  - `headroom/update_check.py` probes PyPI at most once per day.
  - `headroom learn` writes into CLAUDE.md (`headroom/learn/writer.py`), which TAOM guards.
- **Automated vet:** `python tools/audit_claude_config.py --root <clone> --external --no-repo-secrets`
  scanned 2,724 files: CRITICAL 29, HIGH 149, MED 211, LOW 79. The critical rows are 3 `ast-exec-chain`
  in `tests/test_log_safety.py`, 2 `hook-revshell` in `scripts/install.sh` (read: `127.0.0.1` port
  probes), and 24 secret patterns under tests, benchmarks, `#[cfg(test)]` modules and
  `.gitguardian.yaml`. The three of those 24 that were read are fixtures; the others were judged by path.
  The HIGH and MED rows were not read one by one.

## The trial

The conditions of the measured-trial exception in
[external-repo-adoption.md](../ai-includes/external-repo-adoption.md):

- An isolated venv in the scratchpad on `E:`, wheels only (`--only-binary :all:`), so no build script
  ran. `wrap`, `init` and `learn` never ran, the proxy never started, and no hook or MCP entry was added.
- `HEADROOM_OFFLINE=1`, `HF_HUB_OFFLINE=1`, proxy variables at a dead local port, profile folders
  redirected to the scratchpad, an empty working folder, and `python -I`.
- A Python audit hook blocked and logged every non-loopback connection and name lookup: 0 in the
  sample runs and the replays. A `netstat` poll every 3 seconds watched for native code in the sample
  runs: 0 hits, though a short connection could slip between polls. The two small follow-up scripts that
  read examples and counted lost tokens ran in the same environment without the hook.
- Two online steps, both before the runs and with no TAOM data in the process: pip fetched the wheels,
  and a script that did not import Headroom fetched the Kompress model
  `chopratejas/kompress-v2-base` (`onnx/kompress-int8-wo.onnx` at Headroom's pinned revision `b1563631`)
  and the ModernBERT tokenizer from Hugging Face.
- Afterwards `git status --porcelain` showed no file from the trial, the hash of
  `~/.claude/settings.json` was unchanged, the real profile held no `.headroom` folder, and
  `~/.claude.json` listed no Headroom MCP server. The venv, clone, model, store and sample were deleted.

## Measurements

### Where TAOM's tool output goes

Since 2026-10-24: 1,462 transcripts, 97,783 tool results, 274 M characters. Read 38.8%, shell grep
12.4%, sed 11.7%, cat 6.8%, git diff 6.4%, the Grep tool 3.8%, Python scripts 3.7%. By agent class:
`deep-reviewer` 61.5%, main sessions 7.8%, other agents 30.7%.

### Per output

Up to 200 outputs per family with a fixed seed (6,654 in all), each compressed with the message before
it frozen, as the proxy does for the newest turn. Savings are character-weighted with bootstrap 95%
intervals. "Marked" is the share of characters in outputs that came back with a retrieval marker.

| Family | Share of output | Default, no Kompress | Default, with Kompress | Marked |
|---|---|---|---|---|
| Read | 38.8% | 0.0% | 0.0% | 0.0% |
| grep | 12.4% | 8.8% | 9.9% (5.0 to 15.9) | 24.4% |
| sed | 11.7% | 1.3% | 3.7% (2.5 to 5.1) | 45.6% |
| cat | 6.8% | 1.1% | 6.2% (4.5 to 8.0) | 52.9% |
| git diff | 6.4% | 1.1% | 2.2% (1.7 to 2.9) | 25.8% |
| Grep tool | 3.8% | 0.9% | 0.9% (0.4 to 1.7) | 0.0% |
| Python scripts | 3.7% | 0.6% | 1.7% (0.7 to 3.1) | 25.7% |
| other shell | 3.1% | 0.9% | 3.3% (1.8 to 5.2) | 38.6% |
| ls | 2.7% | 2.7% | 4.9% (2.8 to 7.1) | 22.8% |
| echo | 2.4% | 9.0% | 7.5% (4.3 to 12.4) | 38.2% |

| Regime, all families projected | Saving of all tool output |
|---|---|
| Default, no Kompress | 2.20% (0.95 to 3.83) |
| Default, with Kompress | 3.25% (1.92 to 4.90) |
| `mode="agent"` (lossless only) | 0.00%: its verifier accepts only a densified JSON block, so it reverted every fold |
| Ceiling: no tool exclusion, no code protection, no Kompress | 6.02% (2.32 to 10.34) |
| Ceiling, with Kompress | 8.79% (5.28 to 13.17) |

### Spend, replayed request by request

68 transcripts: 66 drawn with a fixed seed, plus the two largest main session files in the window. They
hold about 8% of the window's spend at list prices. Each request keeps its real usage from the transcript. The tokens
Headroom would have removed come off cache reads first and then cache writes, and a context drop of
over 40% counts as a compaction and resets the saving. The range is 3.6 to 2.1 characters per token.

| Class | Weight in spend | Transcripts | Requests | Tool characters saved, default / ceiling | Spend saved, default | Spend saved, ceiling |
|---|---|---|---|---|---|---|
| `deep-reviewer` | 52% | 33 | 2,658 | 2.42% / 7.51% | 0.50% to 0.86% | 1.74% to 2.98% |
| Main session | 27% | 9 | 1,613 | 4.05% / 4.93% | 0.26% to 0.44% | 0.32% to 0.54% |
| Other agents | 21% | 26 | 971 | 3.77% / 6.52% | 1.02% to 1.76% | 1.53% to 2.62% |
| **Weighted total** | | 68 | 5,242 | | **0.55% to 0.94%** | **1.31% to 2.25%** |

With a bootstrap over transcripts the totals span 0.35% to 1.25% (default) and 1.00% to 2.77% (ceiling).
For scale, the plan in #1078 simulates up to 8.5% for compacting the main session at 400k. An estimate
of about 3% made from the docs before this trial was too high.

### The cost of the saving

Kompress drops single words and whitespace, not whole blocks, so a small saving touches many lines.
Over 989 code outputs (cat, sed, head, tail, git show), 344 changed, 248 of them with a marker, and the
344 went from 1,705,863 to 1,567,040 characters.

| Token in the changed outputs | Before | After | Lost |
|---|---|---|---|
| `?` | 783 | 776 | 0.9% |
| `!` | 768 | 765 | 0.4% |
| `&&` | 324 | 324 | 0.0% |
| `\|\|` | 207 | 207 | 0.0% |
| `==` | 853 | 825 | 3.3% |
| `return` | 626 | 613 | 2.1% |
| `null` | 598 | 596 | 0.3% |
| the word `not` | 760 | 732 | 3.7% |

One real line, from a C# file through `cat`: line 23, `? "na"`, came back as `23 "na"`, with the
ternary operator gone and a marker elsewhere in that output. A reviewer who reasons from exact code can raise a false finding on that or miss
a real one, unless the model spends a retrieve call. A control with `HEADROOM_DETECT_BACKEND=rust`, the
native detector that Headroom calls unsafe by default on Windows, changed 266 outputs instead of 344 and
produced the same line.

### Bias

- The replay is a best case. It does not price a retrieve call, the added tool definition, or a re-read
  after a lossy result.
- The proxy itself did not run, so its session logic was not exercised: each output went through the
  library's pipeline with the frozen prefix the handler would pass. `--mode token` did not run either.
  The ceiling bounds its characters but not the cache rewrites it would pay.
- List prices stand in for subscription weights, which are not published.
- This holds for TAOM's traffic only. A workload rich in JSON or logs could gain far more, so yotthani's
  report may well be true of theirs.

## Which Headroom claims held for TAOM

| Claim (`wiki/LIMITATIONS.md`) | For TAOM |
|---|---|
| Agentic conversations 56 to 81% | Tool characters 2.4% to 4.1% in default mode, spend about 1% |
| JSON arrays 86 to 100% | Not tested on its own: the tabular route carried about 0.4% of TAOM's tool output in the sample, so it cannot move the total |
| Code: Passthrough | No: 344 of 989 code outputs changed, on both detectors |
| A user message with "review", "fix" or "debug" protects all code | Not in a tool loop: `_detect_analysis_intent` reads only the newest user message and only when its content is a string, and there that message is a list of tool results (read at source) |

## Decisions

| # | Item | Decision |
|---|---|---|
| X1 | Install Headroom or run `headroom wrap claude` | Declined: about 1% of spend at best, for the costs under Security and altered code text |
| X2 | `--mode token`, or the protections off | Declined: a ceiling of about 2% before cache rewrites, on more altered text |
| X3 | `mode="agent"` in a TAOM tool | Declined: 0.00% on TAOM's output |
| X4 | Port the grep path fold | Declined: 9.9% of 12.4% is about 1.2% of tool output, a tiny win ([simplicity criterion](../../.claude/rules/simplicity-criterion.md)) |

Nothing was ported. The usage work stays on the context window and reviewer effort. Any later idea to
trim tool output comes back with numbers from sections 9 and 11 of `tools/harness_retro.py`.
