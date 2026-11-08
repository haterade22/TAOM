# Reviewing heygen-com/hyperframes (2026-11-08)

Procedure: `/adopt-external` ([external-repo-adoption.md](../ai-includes/external-repo-adoption.md)).
**Outcome: nothing taken.** No skill, tool or text came over. HyperFrames renders HTML to MP4 for coding agents, so
most of the repository is out of domain. The generic gates either duplicate TAOM's or have too little to catch here.

## Source, and what was read

Repository: `heygen-com/hyperframes` at `327e20208c3f542841de670cce54754473f58d30` (Apache-2.0, TypeScript, 9,888 files,
16 packages, created 2026-03-10, pushed 2026-11-08, plugin v0.9.25). It holds 9 published skills and 6 internal skills.
Files came through the GitHub API and a sparse clone in a scratch folder. Nothing was installed and no file from it was
run. The API reads tracked `main` during a day of pushes; the audit and the full Claude hook are at the commit above.

| Depth | Files |
|---|---|
| Read in full | `plugin.json`, `.claude-plugin/plugin.json`, `.claude-plugin/marketplace.json`, `.claude/settings.json`, `.codex/hooks.json`, `.env.example`, `SECURITY.md`, `skills-manifest.json`, `scripts/check-docs-cli-commands.mjs` |
| Read in part | `README.md`, `CLAUDE.md`, `TRIAGE.md`, `lefthook.yml`, the opening of `skills/hyperframes/SKILL.md`, `skills/reverse-any-video/SKILL.md`, `.claude/skills/changelog-video/SKILL.md` and `skills/hyperframes/references/plugin-installation.md`, `packages/cli/src/telemetry/{config,client,policy}.ts`, `packages/cli/src/media-use/lib/telemetry.mjs`, the openings of `scripts/check-comment-citations.mjs`, `comment-ratchet.mjs` and `check-skill-mirror.mjs` |
| Searched by pattern | `telemetry/transport.ts`, `telemetry/system.ts`, the lifecycle scripts and dependencies of the root and 15 package manifests |
| Scanned by the audit | 370 files of agent config and skills |

**Not read:** `packages/sdk-playground/package.json`, the `bin/` launchers, the Studio desktop app, `registry/`, upstream
`AGENTS.md`, and the bodies of most skills. Their verdicts rest on the descriptions in `CLAUDE.md` and `README.md`.

## Security

Safe to learn from: yes. Safe to install into TAOM: no.

- No manifest read has a `preinstall`, `install` or `postinstall` script. The root `prepare` runs `lefthook install` in a
  git checkout only, and `packages/share` builds itself on `prepare`.
- CLI telemetry is on by default (`telemetry/config.ts:621`) and goes to PostHog (`telemetry/transport.ts:13`). Each
  event carries the OS, CPU model, memory and agent runtime and env hints, and the HeyGen username of a signed-in user
  (`telemetry/client.ts`). The first-run notice says file paths and composition content are never collected. That claim
  was not traced through the event code.
- `HYPERFRAMES_NO_TELEMETRY`, `DO_NOT_TRACK` and `hyperframes telemetry disable` turn it off.
- All 9 published skills tell the agent to run `npx hyperframes events --skill=<name>` as the skill starts. The skill
  text calls this a usage beacon, and it is account-linked when the user is signed in.
- A standalone skill install runs `npx hyperframes` with no pinned version. The plugin launcher pins one.
- `.claude/settings.json` and `.codex/hooks.json` each hold one `git commit` hook that runs build, lint and typecheck.
  The Claude hook exits unless `package.json` names `hyperframes-monorepo`. The Codex hook has no such check.

**Automated vet:** `python -I tools/audit_claude_config.py --root <sparse clone> --external` scanned 370 files, matching
the 370 files on disk, and reported CRITICAL 2, HIGH 10, MED 2, LOW 1, INFO 1. Each was checked against its source:

| Finding | Result |
|---|---|
| CRITICAL `secret-tracked-key`, two files in `packages/cli/src/capture/fixtures/` | A `capture-fixture-key.pem` beginning `BEGIN PRIVATE KEY`, and its certificate. The name and folder say test fixture. The test that loads them was not traced. TAOM takes neither file |
| HIGH `secret-generic`, 7 lines in `skills/media-use/audio/scripts/**/*.test.mjs` | Placeholders in tests: `expired-fixture`, `secret-token`, `saved-wallet`, `at_login`, `other-wallet`, `hg_stored` |
| HIGH `output-shell-response` and MED `ast-subprocess`, `extract-audio-data.py:60` | One call: a fixed `ffmpeg` argument list and no shell |
| HIGH `agency-wildcard-tools`, `remove-background.md:97`; HIGH `promptleak-reveal`, `slideshow/SKILL.md:60` | False positives on ordinary prose |
| MED `toolmisuse-no-verify`, `hyperframes-cli/references/cloud.md:150` | A documented `--no-confirm` flag on `cloud delete` |
| LOW `ast-dyn-getattr`, `lyria-recipe.py:78` | `getattr` with a `None` default |
| INFO `yara-unavailable` | The YARA layer was skipped, because `yara-python` is not installed |

The two CRITICAL paths lie outside the sparse tree. The audit read them from the git index.

## Verdict by item

| Item | Verdict | Reason |
|---|---|---|
| Video skills, block registry, `motion-doctrine`, `seam-craft`, `changelog-video` | Skip | Out of domain. TAOM's in-game menu videos are `.ivf` and `.ogg`, and HyperFrames outputs MP4 |
| `scripts/check-comment-citations.mjs` | Skip | See below |
| `scripts/comment-ratchet.mjs`, comment share may not rise | Skip | TAOM comments cite engine lines on purpose (902 citations with a line number). A ratchet would work against those notes |
| Only lines a diff added can fail a gate | Already done | The `lint_docs.py` dash check reports new lines only |
| `scripts/check-skill-mirror.mjs` | Skip | TAOM's `.agents/skills` holds 5 `taom-*` skills of its own, not a mirror of `.claude/skills` |
| `scripts/check-docs-cli-commands.mjs`, the catalog lists every command | Skip | 1 script of the 287 `tools/*.py` is absent from `tools/README.md`; `_` helper modules were not counted as misses |
| `Fix:` line in validator output | Skip | A small win. The `lint_docs.py` rule-symbol message already names its remedy |
| `TRIAGE.md` difficulty labels | Skip | One maintainer, and TAOM has its own `triage-*` labels |
| Usage beacon in a skill | Rejected | A skill that makes the agent report home is the risk the never-install rule exists to stop |

## The citation measurement

`check-comment-citations.mjs` fails a PR when a comment it added cites a path or symbol that does not resolve. The review
counted file citations in the `//` comments of the 6,484 tracked `.cs` files under `Main/` and `TAOM.Tests/`.

- 3,568 citations name a file. 1,064 of them name a file that git does not track. The top names are engine files such as
  `Mission.cs` (133), `Agent.cs` (72) and `Formation.cs` (57). A gate cannot resolve these from the repo, and the
  `~/.taom-src` cache holds only the types already decompiled.
- 895 citations give a repo path (`Main/`, `TAOM.Tests/`, `tools/`, `docs/`, `.claude/`, `plans/`, `Modules/`). Of those,
  4 are not tracked: `Main/Core/Diagnostics/BuildStampReport.cs:12` and `TAOM.Tests/Migration/GameAssemblies.cs:13` (both
  cite `Main/TAOM.cs`), `Main/Features/AdvancedCombat/CustomAttacksUtils.cs:228`, and
  `Main/Features/CharacterCreation/Hooks/CharacterCreationContent_SetSelectedCulture_Patch.cs:13`.

Four misses in 895 do not pay for a new gate ([simplicity-criterion.md](../../.claude/rules/simplicity-criterion.md)).
Whether each of the four is an error was not examined.

## Using it as a tool

Making a trailer or a release video with HyperFrames would be a tool choice, not an adoption, and nothing in TAOM asks
for one today. A capped search of `docs/` for trailer or promo work returned only git and format trailers. If Mike wants
one, it is a measured trial on his explicit decision: a project outside the repo, no plugin or skill install,
`HYPERFRAMES_NO_TELEMETRY=1`, and `git status` compared before and after.
