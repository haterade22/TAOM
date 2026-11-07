# Development machines: the desktop is authoritative, the laptop is for fixes

TAOM is developed on two Windows machines against the same git repo and the same branch. They do
not have the same drive letters, and only one of them has the full content set. Almost every
absolute path written in this repo's docs was written on the desktop, so a path that reads as
fact is usually a fact about that machine only.

| | Desktop (primary) | Laptop (secondary) |
|---|---|---|
| Role | full content, asset work, in-game testing, releases | code and data fixes, review, docs |
| Repo | `E:\repos\TAOM` | `C:\Users\mikew\source\repos\TAOM` |
| Game | `E:\Steam\steamapps\common\Mount & Blade II Bannerlord` | `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord` |
| Decompile dump | `E:\Decompiled_Bannerlord\` | `C:\Decompiled_Bannerlord\` |
| LOTRAOM asset drop | `E:\LOTRAOMAssets\LOTRAOM_Jan_1_Patreon\` | not present |
| Content modules | complete | cooked installs, no loose `Assets/`; see the baseline below |

Neither machine is wrong. The desktop is where a claim about content gets settled, because it is
the only one holding all of it.

## How a path resolves, and where it does not

Three environment variables carry the difference. Set them per machine. A new tool reads its path from an
environment variable and may fall back to a desktop default; name the variable in the table below.

| Variable | Set where | Read by |
|---|---|---|
| `BANNERLORD_GAME_DIR` | Windows user env var (`setup-dev-env.ps1` writes it) | `tools/_gamedir.py`, so every validator and data tool; `Directory.Build.props`, so the build |
| `TAOM_DECOMPILE_ROOT` | Windows user env var | `tools/check_handbook_attributes.py`. Point it at the **category tree**, e.g. `C:\Decompiled_Bannerlord\_categories_v1.4.8` |
| `TAOM_PYBIN` | `.claude/settings.json` env block | `.claude/hooks/_pybin.sh`. The same value works on both machines because Python lives at `C:\Python314` on each; a stale pin degrades to discovery rather than failing |
| `GHIDRA_INSTALL_DIR` | Windows user env var, desktop only | `tools/native_decompile.py` and PyGhidra (see "Ghidra" below) |
| `TAOM_GHIDRA_PYTHON` | optional; unset on the desktop | `tools/native_decompile.py`: the interpreter that has PyGhidra, default `E:\Tools\ghidra-venv\Scripts\python.exe` |
| `TAOM_GHIDRA_PROJECTS` | Windows user env var on the laptop, unset there until the maintainer's plan 137 step 2 (checked 2026-11-06); the desktop uses the default | `tools/native_decompile.py`, `native_build_match.py` and `native_archive.py`: the folder for Ghidra projects, engine-method maps and matcher indexes, default `E:\ghidra\TAOM` |
| `TAOM_NATIVE_ARCHIVE` | Windows user env var on the laptop (for example `C:\Decompiled_Bannerlord\_native`), unset there until the maintainer's plan 137 step 2 (checked 2026-11-06) | `tools/native_archive.py`: the native DLL archive, default `E:\Decompiled_Bannerlord\_native` |

**Three things ignore all of that** and need the path passed by hand:

- `tools/decompile_bannerlord.ps1` and `tools/decompile_to_folder.ps1` take `-Out` / `-GameBin` /
  `-Source` / `-Destination` parameters and honour no environment variable (this is already noted
  in [tools/README.md](../../tools/README.md)).
- `.mcp.json` is committed with the desktop's `E:\` paths hardcoded, so on the laptop `serena`
  opens `E:/repos/TAOM` and `taom-moduledata` invokes `E:/repos/TAOM/tools/taom_mcp_server.py`
  (the `filesystem` server that also pointed at desktop folders was removed by WP116 (1.5.x removed it 2026-09-29); the
  desktop reads those folders as `additionalDirectories` in its own untracked `.claude/settings.local.json`). Editing that file to suit one machine breaks the
  other. The fix is a local-scope MCP override in user config, not a change to the committed file.
- `.codex/config.toml` hardcodes its own list of desktop `E:\` paths for Codex's `filesystem` server, with the same consequence on the laptop.

`.claude/settings.local.json` is per-user and untracked (since WP142 on this line; the 1.5.x line untracked it on 2026-09-25): each checkout, every worktree included, keeps its own copy (personal allow rules, `additionalDirectories`, `enabledMcpjsonServers`). A new worktree starts without one, so copy yours in. A checkout of a commit before the untrack (`1b00d1acd` and older) still tracks the file: switching to one overwrites your copy and switching back removes it, so keep a copy outside the repo. `git restore --source=1b00d1acd -- .claude/settings.local.json` brings back the last tracked version. Anything every clone needs lives in the tracked `.claude/settings.json`.

## The trap: a red validator on the laptop is usually the laptop

**Do not act on a content failure observed on a machine whose dependency modules are incomplete,
and do not "fix" the repo to make one go quiet.** TAOM's data spans this repo plus the live
`TAOM_Map` and `LOTRLOME_Armory` installs, which are unversioned and are not in git. If those
installs are partial, every reference into them fails, and the failure looks exactly like a repo
defect: thousands of broken item ids, every culture landless, settlement economies under their
floor. The repo can be perfectly correct and still produce that report.

Measured on the laptop on 2026-09-06: 6,894 `BROKEN_ITEM_REF`, 414 `LANDLESS_CULTURE` and 90
`SETTLEMENT_ECONOMY_FLOOR`, with `TAOM_Map\ModuleData` holding no `settlements.xml` at all. None of
it was a repo defect.

Measured on the laptop on 2026-11-07 with the public build installed: `TAOM`, `TAOM_Map` and
`LOTRLOME_Armory` all at `v2.0.29.5`, game `v1.4.8`, both extra roots swept (513 ModuleData files).
On the `bannerlord-1.4.5` port tree `validate_moduledata.py` reported 0 errors and 111 warnings:
93 `INCONSISTENT_ARMOUR_SLOT`, 7 `RANGED_LADDER_INVERSION`, 7 `CROSS_CULTURE_ARMOUR_INVERSION` and
4 `UPGRADE_ARMOUR_REGRESSION`. `BROKEN_ITEM_REF`, `BROKEN_ROSTER_REF`, `BROKEN_SETTLEMENT_REF` and
`LANDLESS_CULTURE` are all 0, the ModuleData commit gate's `--code` list exits 0, and
`audit_polearm_shield_parity.py` passes (11 rosters KNOWN under the #526 ratchet). That run is the
laptop baseline; a laptop result above it deserves a look before it is blamed on the machine. The
112th warning, a `GENERATOR_RETIRED_ITEM_REF` on `tools/generate_gondor_troops.py`, was a real
repo defect and was fixed the same day.

History: on 2026-11-04, with `LOTRLOME_Armory` still at `v2.0.26`, the same tree read 3768 errors
(all `BROKEN_ITEM_REF`) and 120 warnings, and 3771 after WP108 added the collision-body passes.
That was a PROVISIONAL baseline (decision D25 of the 2026-11-04 port programme), void for item
verdicts, and it is superseded by the run above.

Before believing a content failure, check that the module it points into is actually populated. The
cheap version is a file count under `Modules\<name>\ModuleData`; a handful of files where there
should be hundreds is the answer. `validate_moduledata.py` prints which extra roots it swept, so
its own header tells you what it could see.

The same caution applies to in-game checks. A townswoman on a machine with a partial Armory renders
with missing armour whatever the character XML says, so the laptop cannot confirm or refute an
appearance fix.

## Laptop toolchain, provisioned 2026-09-06

Installed that day: Python 3.14.7 at `C:\Python314` (matching `TAOM_PYBIN`), jq 1.8.2, the Python
packages the repo's tools import (lxml, Pillow, numpy, matplotlib, anthropic, mcp, pefile, capstone,
lz4, pyyaml, ilspy-mcp-server), and the decompile dump described above. `bash tools/test_hooks.sh`
went from refusing to run to 183 passed.

Already present and not reinstalled: PowerShell 7.6.5, `codex` 0.120.0, `gh` (authenticated), Node,
`uv`/`uvx`, `ilspycmd`, .NET SDK 9 and 10, and the Modding Kit build (`Win64_Shipping_wEditor`).

Known gaps on the laptop, both deliberate:

- **`yara-python`** has no wheel for Python 3.14 and building it needs MSVC Build Tools.
  `tools/audit_claude_config.py` treats the YARA scan as optional and prints an INFO note when the
  import fails, so `/security-scan` still runs, minus that one pass.
- **Blender** is absent, so creature-animation work (`/refine-creature-anim`, the Blender MCP,
  `tools/dump_engine_skeleton.ps1` round trips) has to happen on the desktop.

## Ghidra, desktop only (installed 2026-09-26, #688)

`tools/native_decompile.py` needs all of this; the procedure it serves is `/native-crash-triage`.

| Piece | Where | Why it is set up this way |
|---|---|---|
| Ghidra 12.1.4 | `E:\Tools\ghidra_12.1.4_PUBLIC` (zip, SHA-256 checked); user env `GHIDRA_INSTALL_DIR` points here | 12.1.4 patches the three advisories an analyst can reach ([review](../reviews/adopt-ghidra-hindsight-2026-09-26.md)) |
| Temurin JDK 25.0.4.1 | `E:\Tools\jdk-25.0.4.1+1` (zip) | Ghidra needs 25. Selected by `JAVA_HOME_OVERRIDE` in `support\launch.properties`; the machine's Java 8 stays first on PATH, and `JAVA_HOME` is untouched |
| PyGhidra 3.1.0 with JPype 1.5.2, and capstone 5.0.9 | venv `E:\Tools\ghidra-venv`, made with `py -3.13 -m venv`, installed offline with `pip install --no-index -f <ghidra>\Ghidra\Features\PyGhidra\pypkg\dist pyghidra`; capstone from its `py3-none-win_amd64` wheel | PyGhidra pins JPype 1.5.2, which has no wheel for the system Python 3.14. The tool re-runs itself under this venv, so the command stays `python tools/native_decompile.py`; the engine-method map needs capstone there too |
| Ghidra's settings, cache and temp directories | `E:\ghidra\user\{settings,cache,temp}`, set by three `VMARGS` lines in `support\launch.properties` | The 3.13 is the Microsoft Store build, which redirects `AppData` writes into its package folder; the JVM runs inside that process, and Ghidra's OSGi bundle host fails on the redirected paths |
| Analysed projects | `E:\ghidra\TAOM\<build folder>-<sha256[:16]>`, with `<key>.engine-methods.json` beside each | One per distinct binary. The v1.5.3 client DLL (14 MB): 213 s to analyse once, a 212 MB project, then 3 to 5 s per run |

A reinstall of Ghidra has to repeat the four `launch.properties` edits (`JAVA_HOME_OVERRIDE` and the
three directory lines).

On the laptop none of this is installed (checked 2026-11-04: `GHIDRA_INSTALL_DIR` unset, and neither
procdump nor WinDbgX on PATH), so `tools/native_decompile.py` exits 2 there naming this page and
`/native-crash-triage` falls back to hand-decoding the disassembly. The engine-method map and
`tools/native_build_match.py` need only capstone and `ilspycmd`, which the laptop has, so they run there
with `TAOM_GHIDRA_PROJECTS` set (verified 2026-11-04 on v1.4.8: 2,261 engine methods mapped with no
one-sided id, and a client-to-wEditor oracle with 0 wrong).

**TpacTool** (the PowerShell tpac tools) is set up on the desktop; the laptop has not been checked. They load ShortJake's TpacTool v0.5.1 (MIT, .NET Framework 4.6.2) from `E:\Release_v0.5.1\bin` by default (`-TpacToolBin`), since the old `E:\Bannerlord_Art\TpacTool_0.4.0` was found gone (2026-10-14, 1.5.x line). Unblock the folder once after a fresh download: the zip's Internet zone mark makes .NET Framework refuse the DLLs (`0x80131515` in Windows PowerShell 5.1; pwsh ignores it).

## Writing docs and tools on either machine

Prefer the environment variable to the literal path when you write a new tool. When you must write
a literal path into prose, say which machine it describes. The existing `E:\...` paths throughout
`docs/` are desktop paths and are being left as they are rather than rewritten, because they are
correct there and the register above is enough to translate them.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/codex-operating-guide.md](../ai-includes/codex-operating-guide.md)
- [docs/ai-includes/taleworlds-research-guide.md](../ai-includes/taleworlds-research-guide.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/bannerlord-engine-and-toolchain.md](./bannerlord-engine-and-toolchain.md)

<!-- backlinks-end -->
