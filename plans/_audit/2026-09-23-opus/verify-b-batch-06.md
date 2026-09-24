# Verify B, batch 06 (adversarial checker, baseline `b2e387db`)

## F5: Tracked files that should not be (`.claude/settings.local.json`, `_taom_loc.pkl`, `crashz/`)

- **Outcome**: CONFIRMED on the facts, with a correction to the fix for two of the three files.
- **Impact today**: LOW (hygiene and clone weight; no runtime effect; no secret found).

**What I re-measured (all against `b2e387db`):**

- `git ls-tree -r -l b2e387db -- .claude/settings.local.json _taom_loc.pkl crashz` lists all three:
  `.claude/settings.local.json` 2,627 bytes; `_taom_loc.pkl` 37,216,114 bytes (35.5 MiB, "37 MB" holds
  in decimal units); `crashz/` 4 files (`manifest.txt`, `report.json`, `report.txt`, `rgl_log.txt`),
  489,786 bytes. All six paths are still tracked at the working HEAD (`git ls-files ... | wc -l` = 6).
- `git check-ignore -v --no-index` on all of them: exit 1, no rule matches, so a delete without an
  ignore line would be re-added by the next sweep commit. The baseline `.gitignore` has `*.log` (which
  is why the bundle's `taom_debug.log`, listed in `crashz/manifest.txt`, is absent) but nothing for
  `*.pkl`, `crashz/` or `settings.local.json`.
- Provenance (`git log --diff-filter=A`): `settings.local.json` in `78892259` (2026-01-24 "Updpdate",
  3 files); `crashz/` in `d9817f89` (2026-08-08, 8 files, 6,862 insertions); `_taom_loc.pkl` in
  `b930fd8a` (2026-08-29, 42 files). Delta vs `141b749`: pkl and crashz absent there (introduced);
  settings.local.json present there (pre-existing). Lane 6's DEPS-L6-05 "F5 extended" numbers all
  reproduce.

**Refutation attempts, per file:**

1. **`_taom_loc.pkl`: no refutation holds.** `git grep -E "\.pkl|pickle"` over `*.py *.ps1 *.sh *.md`
   finds no reader or writer (only a generic line in the audit playbook), and `git grep _taom_loc`
   finds nothing. The header is a protocol-5 pickle of a dict keyed `old` holding `TAOM_*` string ids
   and English text, i.e. a scratch localization snapshot from some one-off session. Orphaned, 37 MB,
   and a pickle (unsafe to load) sitting at the repo root. Untracking it does not shrink the existing
   pack (the blob stays in history).
2. **`crashz/`: real, but not orphaned.** No personal data found: a scan of all three files for
   `Users\<name>` paths, Steam ids, machine names and email patterns gave 0 real hits (3 false
   positives in `rgl_log.txt`, FMOD event names). But three tracked docs cite it by path as "the open
   player report": `.claude/skills/engine-bump/SKILL.md:29`, `.claude/skills/native-crash-triage/SKILL.md:74`,
   `docs/migration/v1.4.8-impact.md:136`. A bare untrack breaks those references; the fix is to move
   the bundle (for example under `docs/reviews/` or onto its GitHub issue) and repoint the three lines,
   or delete them once the v1.4.7 report is written off.
3. **`.claude/settings.local.json`: the "should not be tracked" judgment holds, but the file is
   load-bearing and documented as tracked, so the naive fix is harmful.**
   - `docs/reference/development-machines.md:40` (added `bd5bf0a5`, 2026-09-06): "`.claude/settings.local.json`
     is **tracked**, despite the name". This records the fact; it is not an ADR and not in the
     BRIEF decided-tradeoffs list, so it is not a by-design exemption, but it is known.
   - It carries a security control: `permissions.deny` lists the nine MCP write tools
     (`docs/reference/mcp-servers.md:23`, added in `e3dd955e` "fix(harness): clear the remaining audit
     findings"), plus `enabledMcpjsonServers` (`docs/reference/mcp-servers.md:65`,
     `docs/features/moduledata-validation.md:686`), plus `additionalDirectories`.
   - A plain `git rm --cached` plus ignore line would, on the next pull on the other machine, delete
     the file from that working tree and silently drop the deny list there. The corrected fix: move
     `deny`, `enabledMcpjsonServers` and the shared allow rules into the tracked `.claude/settings.json`
     first, then untrack, then add the ignore line, and update the two docs above.
   - Content is not secret (no credentials; checked the whole file), but it ships personal and broad
     pre-approvals to every clone: `Read(//c/Users/mikew/**)`, `Bash(node:*)`, `Bash(git checkout *)`,
     `Skill(codex:rescue)`, and one path to a machine-specific `E:\LOTRAOMAssets\...` folder. That is
     a better reason to untrack than the name alone.

**Corrected evidence**: `git ls-tree -r -l b2e387db` (sizes above); `.gitignore` at `b2e387db` has no
matching rule; load-bearing references `docs/reference/mcp-servers.md:23,65`,
`docs/reference/development-machines.md:40`, `.claude/skills/engine-bump/SKILL.md:29`,
`.claude/skills/native-crash-triage/SKILL.md:74`, `docs/migration/v1.4.8-impact.md:136`.

## What I did not cover

- Whether Claude Code actually honours `enabledMcpjsonServers` and `permissions.deny` from a tracked
  `settings.local.json` on a fresh clone (harness behaviour, not read here).
- Whether the `crashz` report has an open GitHub issue it could move to (GitHub MCP not authorised).
- No other tracked-file hygiene beyond the three F5 paths (Lane 6's pattern sweep covers that).
