# rea and Ghidra re-review (2026-11-08)

Second pass through `/adopt-external` over [morluto/rea](https://github.com/morluto/rea) and
[Ghidra](https://github.com/NationalSecurityAgency/ghidra), to catch upstream changes and anything the first reviews
missed. Earlier records: [rea, 2026-10-24](adopt-rea-2026-10-24.md) and
[Ghidra, 2026-09-26](adopt-ghidra-hindsight-2026-09-26.md). Issue
[#1098](https://github.com/haterade22/TAOM/issues/1098).

## Coverage

- **rea:** `main` at `25d3c7237`, 1,393 commits past `d361c53c5`. Read through `gh api` only; nothing was cloned, installed
  or run. Read as diffs against the old commit: `AGENTS.md`, `SECURITY.md` (head), the `reverse-engineer-anything` skill and its
  `evidence-workflows` and `native-and-artifacts` references, `docs/native-function-matching.md`, the moved native and
  managed guides, `tool-design.md`, the "Boundary regressions" table of `testing.md`, the three other agent skills that
  changed, `prepare.mjs`, `prepack.mjs`, `.claude-plugin/plugin.json` and `.agents/plugins/marketplace.json`.
- **Ghidra:** release 12.2 (2026-10-22). Read: the What's New head, a filtered Change History, `GhidraProject.java`,
  PyGhidra's `pyproject.toml` and `api.py`, `ProgramDB` versions, and every advisory title.
- **Not read:** rea's `src/**` beyond commit file lists, the bridge code, the submodules, `docs/specs/**` except titles and
  `docs/verification/**`. `plugin-mcp-launcher.mjs` was searched for network and process calls, not read in full. Six
  Ghidra advisories were read in full; the other 26 fixed in 12.2 were judged by title.

## Security

- **rea, safe to learn from:** yes. The lifecycle script names are unchanged, and `prepare.mjs` and `prepack.mjs` have no diff.
  Ten process files hold no hidden Unicode. `audit_claude_config.py --external` scanned all 22 files of `.agents/`,
  `.claude-plugin/` and `.codex-plugin/` with no finding.
- **rea, safe to install:** no, and not needed. New since the last review: a Claude Code plugin that registers an MCP server through a
  Node launcher, and a marketplace entry for the npm package. `plugin.json` declares no hooks.
- **Ghidra advisories:** the project lists 64, and 32 are fixed in 12.2. None is on TAOM's path. They cover archives,
  extensions, themes, DEX, Mach-O, the Ghidra and BSim servers and the Debugger. The decompiler memory bugs need a crafted
  binary, and TAOM feeds only TaleWorlds' own DLLs. The symlink advisory (GHSA-jv87) needs a planted Linux `/tmp` directory.
  The upgrade had no security urgency.
- **Download:** the SHA-256 of `ghidra_12.2_PUBLIC_20261016.zip` matched the release body (`2c2cd776...7be67`).

## Decisions

| # | Item | Source | Decision |
|---|---|---|---|
| 1 | `native_decompile.py --summary` | rea's `detail: "summary"` | Adopted. The v1.5.3 helper run fell from 1,902 lines to 19 |
| 2 | Search siblings for the same failure | rea `AGENTS.md` | Adopted: `/investigate` Phase 5 step 5, and a `Siblings` row in its report |
| 3 | Cache keys and partial failure | rea "Boundary regressions" | Adopted: tooling lens item 19 |
| 4 | Ghidra 12.2 | Ghidra | Adopted after a trial; see below |
| 5 | RTTI class recovery | Ghidra, missed in 2026-09 | Spike only; see below. Not adopted |
| 6 | `importProgram` to `ProgramLoader` | TAOM's own note | Declined for now. The method is still in 12.2, and its body holds the consumer and calls a private initializer that `saveAs` needs |
| 7 | A per-bump list of changed native functions | rea `find_changed_behavior` | Declined. Hashes cover thin wrappers only, so an unchanged wrapper says nothing about deep code |
| 8 | Matcher changes | rea | None to take. The matching method did not change; the doc adds byte-source paging and a configurable memory allowance |
| 9 | PE load config, TLS, hardening, field constants, retained Evidence, MCP catalog evaluation, the "fix the tool before the skill" rule | rea | Declined. No TAOM question needs them, or CLAUDE.md routing item 1 covers them |
| 10 | X1 to X11 of the 2026-10-24 record | rea | Stand. Windows Ghidra is still marked experimental, and rea has no engine-method seeding |

## Ghidra 12.2 trial

Run side by side on the v1.5.6 client DLL (`fa3eb85fcabc2e9e`), with a separate venv and project folder.

| Check | Result |
|---|---|
| Cold analysis | 213 s analysis, 224 s wall |
| Opt-in integration tests | 105 passed on the trial, 106 passed after the switch, none skipped |
| A copied 12.1.4 project, opened read-only by 12.2 | Opens, 33,339 functions |
| Function count, 12.1.4 against 12.2 | 33,339 against 33,337, 0 added, 0 renamed, 3 resized |
| Five pinned decompiles | Four identical. `IMBAgent_GetCurrentActionType` inverts one branch, same logic |
| Decompiler Parameter ID on this DLL | Off in both versions |

**The gate.** The approved plan said to stay on 12.1.4 if functions were lost. Two were: `0x788C50` and `0x78AB50`. Their code was not.
12.2 merged each one into the only function that jumps to it, and the sizes add up (81 + 106 = 187 and 28 + 243 = 271 bytes). `--rva` still
answers at both addresses, under the absorbing function's name. This met the gate's words but not its reason, so the question went to
Mike, who chose to switch on 2026-11-08.

**No decompile gain was measured.** The output is close to identical. The value of the move is staying on the supported release.

**After the switch:** only the v1.5.6 client is analysed by 12.2. The 12 other projects were made by 12.1.4. The 12.1.4 install, its venv
(`E:\Tools\ghidra-venv-12.1.4`) and the old v1.5.6 project (`E:\ghidra\TAOM-12.1.4`) stay until Mike removes them.

## RTTI class recovery spike

`RecoverClassesFromRTTIScript` ran on a copy of the seeded 12.2 project in 506 s. It identified 597 classes and 6,007 member functions. The script is
marked "PROTOTYPE" by its authors.

- **Gain:** 3,903 functions were renamed, and default-named functions fell from 18,626 to 14,834. Class-member C improves strongly.
  `FUN_18033f960(longlong *param_1, ...)` became `rglDefault_anim_system::vfunction29(rglDefault_anim_system *this, ...)`, and
  `(*(code *)((undefined **)*param_1)[0x1f])(param_1)` became `(*this->vftablePtr->vfunction32)(this)`.
- **Harm:** it overwrote 111 of TAOM's seeded engine-method names with wrong ones. `IDebug_RenderDebugText` at `0x4A8890` became
  `rglString_base`. The cause is UNVERIFIED. The name suggests the script took an inlined string destructor for the function's identity.
- **Cost:** the project grew from 214 MB to 438 MB.
- **No change** to the three other known functions tried (`0x153C80`, `0x6E0360`, `0x4A1930`), which are not class members.

**Verdict:** worth a separate issue. An adoption must run the script before seeding, or re-apply the seeded names afterwards, and must check what the other 3,792 renames
got right. Any `rglString`-family name on a function that does more than string work is suspect, and the 744 `rglString` plus 453 `scalar` renames are the place to
sample. The output project is kept at `E:\ghidra\TAOM-12.2-spike` until Mike decides.

## Review

One `deep-reviewer` tooling-lens pass on the working tree found no HIGH defect. Every finding was checked against the code before it was fixed.

| Severity | Finding | Fix |
|---|---|---|
| MEDIUM | The `--summary` footer omitted `--dll`, so a reader of an archived build would follow it to the installed build at the same RVA, with exit 0 | The footer names the DLL |
| MEDIUM | Two tests named "decompiles" asserted only rows; removing caller or string C passed | Both assert the C text |
| LOW | Caller, thunk and string labels and both string cap rows were unpinned under `--summary` | Tests added |
| LOW | The sibling list had no row in the `/investigate` report, and a confirmed sibling skipped the test step | A `Siblings` row, and "apply steps 1 to 4 to it" |
| LOW | Item 19 was unmet by the tool it was motivated by | "Staleness and safety" states why an older project is harmless |
| LOW | Item 19 read as condemning all-or-nothing caches | Reworded |

The reviewer's mutation script then killed 18 of 19 mutants. The survivor changes a default that `main` never uses.

## Corrections made during the review

- A first advisory count of 26 came from a query capped at 30 rows. The paged total is 32.
- The feature doc named `0x6E05B0` as the v1.5.6 pin for `IMBAgent.SetAttackState`. The test pins `0x6E16F0`, the v1.5.6 hotfix build. The doc now agrees.
