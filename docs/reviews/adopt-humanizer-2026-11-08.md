# Reviewing blader/humanizer v3.1.0 (2026-11-08)

Procedure: `/adopt-external` ([external-repo-adoption.md](../ai-includes/external-repo-adoption.md)).
**Outcome:** the pattern list came over as text in TAOM's own words, and nothing was installed. The strongest tells
now load in every Claude session through [output-style.md](../../.claude/rules/output-style.md), and the full list is
[writing-tells.md](../reference/writing-tells.md), which the AGENTS.md "Human prose" row links for Codex.

## Source, and what was read

Mike asked for every output to use this skill or something like it, with ASD-STE100 kept as before. The review used commit
`0ef9fe5bc3572734fd3ae6c448fe00a30cd5dcf0`, version 3.1.0. The license is MIT, and the last push was on 2026-10-28.

TAOM ported an earlier version as `/humanizer` and an always-loaded rule. The [2026-10-08 skill audit](skill-usage-audit-2026-10-08.md)
removed the skill because it found 1 dash in 863 commit commands and 0 corrections. `output-style.md` Part 2 kept a short
form. That port never got a provenance row, so this review adds one.

| Depth | Files |
|---|---|
| Read in full | `SKILL.md`, `AGENTS.md`, `CHANGELOG.md`, `scripts/validate-package.py`, `.github/workflows/validate.yml`, all three plugin manifests, `agents/openai.yaml` |
| Searched | `README.md`, for URLs, commands and sponsorship |

## Security

Safe to learn from: yes. Safe to install: no need arose, and the adoption rule keeps it uninstalled.

- The package is markdown, manifests and one validator. It has no hooks, no install script and no code that runs on use.
- `scripts/validate-package.py` uses the standard library and reads only the package's own files.
- CI runs `npx skills` and installs Claude Code, on upstream's runner only.
- `tools/audit_claude_config.py --external` scanned all 14 tracked files and reported no findings. A separate scan of the same 14 files found no hidden Unicode characters.

The clone stayed in a scratch folder, and nothing from it ran.

## Verdict by item

| Item | Verdict | Reason |
|---|---|---|
| Strength order, with "weak alone" tells | Taken | The old list was flat, so one formal word weighed the same as a repeated closer |
| Not-X-but-Y, one-line closers, arguing with no one | Taken | Absent or only hinted at in Part 2 |
| Text about the document (§25) | Taken, narrowed | Commit bodies, RCAs and adoption records describe change and method by design |
| Re-explaining what the reader knows (§26) | Taken | Fits chat and PR replies. Evidence the rules require stays beside its claim |
| Compare the rewrite with the draft | Taken | The only pattern here that guards facts as well as style |
| The other patterns | Reference doc only | The unscoped rules had 1,324 bytes free |
| Bold labels (§19) | Declined | House style, carved out in the first port too |
| `/humanizer` skill | Declined | The audit showed nobody invoked it, and a skill cannot reach every output |
| Phrase gate in `lint_docs.py` and `check_public_text.py` | Declined after measurement | See below |
| Validator and plugin manifests | Skip | They check upstream's own package |

## The gate that was not built

The plan included a phrase list for chatbot residue and staged lines. The list of 31 phrases was run first over the 981 commit
bodies since 2026-10-08 (19,043 lines) and 107,985 added markdown lines. Controls confirmed that the scan worked:
"rather than" matched 15 and 388 lines. The phrases had 0 hits in the commit bodies and 2 in the markdown: the banned-opener
text in `output-style.md` itself, and an ordinary use of "the thing is" in an RCA. A check that never fires has no win to set
against its cost ([simplicity-criterion.md](../../.claude/rules/simplicity-criterion.md)).

Structural tells do occur: "not just" matched 30 added markdown lines and "rather than" 388. A phrase match cannot separate a
real contrast from a staged one, so the rule and `/deep-review` lens H4 carry those.

## Limits

- Chat has no mechanical check. The always-loaded rule is the only control there.
- Player-facing strings are out of scope. They keep their Tolkien voice, and an English edit stales 12 languages.
- Existing prose was not rewritten, the same scope as the dash rule.
- ASD-STE100 keeps its scope, on Mike's answer: chat, status, procedures and code comments. STE wins where the two conflict.
