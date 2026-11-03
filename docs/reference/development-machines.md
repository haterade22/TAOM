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
| Launcher repo (`haterade22/LOTRAOM`, branch `dev`) | `C:\Users\mikew\source\repos\LOTRAOM`; `E:\repos\lotraom-launcher-staging` is a commit-less staging copy, not the launcher | not recorded |
| Content modules | complete | incomplete, see the warning below |

Neither machine is wrong. The desktop is where a claim about content gets settled, because it is
the only one holding all of it.

Desktop only: make no finding, test or skip around the laptop's partial install. A crash on any partial install is still a bug (Mike, 2026-09-18).

## How a path resolves, and where it does not

These environment variables carry the difference. Set them per machine. A new tool reads its path from an
environment variable and may fall back to a desktop default; name the variable in the table below.

| Variable | Set where | Read by |
|---|---|---|
| `BANNERLORD_GAME_DIR` | Windows user env var (`setup-dev-env.ps1` writes it) | `tools/_gamedir.py`, so every validator and data tool; `Directory.Build.props`, so the build |
| `TAOM_DECOMPILE_ROOT` | Windows user env var | `tools/check_handbook_attributes.py`. Point it at the **category tree**, e.g. `C:\Decompiled_Bannerlord\_categories_v1.5.6` (the script's default is the desktop's `E:\Decompiled_Bannerlord\_categories_v1.5.6`) |
| `TAOM_PYBIN` | `.claude/settings.json` env block | `.claude/hooks/_pybin.sh`. The same value works on both machines because Python lives at `C:\Python314` on each; a stale pin degrades to discovery rather than failing |
| `GHIDRA_INSTALL_DIR` | Windows user env var, desktop only | `tools/native_decompile.py` and PyGhidra (see "Ghidra" below) |
| `TAOM_GHIDRA_PYTHON` | optional; unset on the desktop | `tools/native_decompile.py`: the interpreter that has PyGhidra, default `E:\Tools\ghidra-venv\Scripts\python.exe` |
| `TAOM_SHELLCHECK` | optional; unset on the desktop | `tools/tests/test_shell_lint_gate.py`: the binary, else `shellcheck` on PATH, else the `E:\Tools` default (see "Lint gates" below) |
| `TAOM_GITLEAKS` | optional; unset on the desktop | the `/security-scan` history step: the binary, else the `E:\Tools` default (no PATH lookup) |
| `TAOM_ALLOW_NO_RUFF`, `TAOM_ALLOW_NO_YAML`, `TAOM_ALLOW_NO_SHELLCHECK` | set only on a machine that lacks the tool | turn that lint gate's failure into a reported skip |

**Three things ignore all of that** and need the path passed by hand:

- `tools/decompile_bannerlord.ps1` and `tools/decompile_to_folder.ps1` take `-Out` / `-GameBin` /
  `-Source` / `-Destination` parameters and honour no environment variable (this is already noted
  in [tools/README.md](../../tools/README.md)).
- `.mcp.json` is committed with the desktop's `E:\` paths hardcoded, so on the laptop
  `taom-moduledata` invokes `E:/repos/TAOM/tools/taom_mcp_server.py`
  (the `filesystem` server that also pointed at desktop folders was removed 2026-09-29; the
  desktop reads those folders as `additionalDirectories` in its own `settings.local.json`). Editing that file to suit one machine breaks the
  other. The fix is a local-scope MCP override in user config, not a change to the committed file.
- `.codex/config.toml` hardcodes its own list of desktop `E:\` paths for Codex's `filesystem`
  server, with the same consequence on the laptop.

`.claude/settings.local.json` is per-user and untracked: each checkout, every worktree included,
keeps its own copy (personal allow rules, `additionalDirectories`, `enabledMcpjsonServers`). A new
worktree starts without one, so copy yours in. `bannerlord-1.4.5` still tracks the file, so
switching a checkout to that branch overwrites your copy and switching back removes it; do 1.4.5
work in its own worktree. Anything every clone needs, such as the MCP write-tool deny list, lives
in the tracked `.claude/settings.json`.

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
- **Blender** is absent, so creature-animation work (`docs/ai-includes/creature-animation-blender-mcp-workflow.md`, the Blender MCP,
  `tools/dump_engine_skeleton.ps1` round trips) has to happen on the desktop.
- **Unreal Engine** is desktop-only as well: launcher installs `E:\UE_5.7` (5.7.4, since May), `E:\UE_5.4`
  (5.4.4) and `E:\UE_5.3` (5.3.2, both 2026-09-17), each with the Fab plugin; `E:\UE_5.6` is an empty shell
  with a stale registry key. The Fab purchases and their UE projects live under `E:\LOTRAOMAssets\`
  (`Troll_Animation_5_4`). Pipeline: [ue-to-bannerlord-asset-pipeline.md](ue-to-bannerlord-asset-pipeline.md).
- **TpacTool** is set up on the desktop; the laptop has not been checked. The PowerShell tpac tools load
  ShortJake's TpacTool v0.5.1 (MIT, .NET Framework 4.6.2) from `E:\Release_v0.5.1\bin` by default
  (`-TpacBin` / `-TpacToolBin`), since 2026-10-14, when the old `E:\Bannerlord_Art\TpacTool_0.4.0` was found
  gone. The folder is byte-identical to the GitHub release (123 files; zip SHA-256 `81896c0a...5e78`).
  Unblock it once after a fresh download: the zip's Internet zone mark makes .NET Framework refuse the DLLs
  (`0x80131515` in Windows PowerShell 5.1; pwsh ignores it). The API changes from 0.4.0 that the tools
  account for: [tools/README.md](../../tools/README.md), "Which TpacTool the PowerShell tools load".
- **Ghidra** is desktop-only (below). On the laptop `tools/native_decompile.py` exits 2 naming this
  page, and `/native-crash-triage` falls back to hand-decoding the disassembly.
- **The lint gates' tools** (Ruff, ShellCheck, Gitleaks) were installed on the desktop only
  (below). Without them the Python suite fails those gates by design; install them, or set the
  matching `TAOM_ALLOW_NO_*` variable so the gate reports a skip.

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

## Lint gates, desktop (installed 2026-10-26, #1020)

The Python suite (`/verify` Step 2b) runs three gates that need these tools, and `/security-scan`
runs Gitleaks. Ruff and PyYAML are found by Python's import. ShellCheck is found through
`TAOM_SHELLCHECK`, then PATH, then its default; Gitleaks through `TAOM_GITLEAKS`, then its default.
Neither binary is on PATH here.

| Tool | Where | Install on another machine |
|---|---|---|
| Ruff 0.17.2 | user site of `C:\Python314` | `python -m pip install --user ruff` |
| PyYAML | user site of `C:\Python314` | `python -m pip install --user pyyaml` |
| ShellCheck 0.11.0 | `E:\Tools\shellcheck-0.11.0\shellcheck.exe` | `shellcheck-v0.11.0.zip` from https://github.com/koalaman/shellcheck/releases, SHA-256 checked against the asset's digest, unzipped to a folder named for the version; set `TAOM_SHELLCHECK` when it is not at the default |
| Gitleaks 8.30.1 | `E:\Tools\gitleaks-8.30.1\gitleaks.exe` | `gitleaks_8.30.1_windows_x64.zip` from https://github.com/gitleaks/gitleaks/releases, checked against `checksums.txt`; set `TAOM_GITLEAKS` when it is not at the default |

Check: `python -m unittest tools.tests.test_python_lint_gate tools.tests.test_claude_config_lint tools.tests.test_shell_lint_gate`
passes. Ruff and PyYAML live in the user site, so `python -I` and `python -s` cannot see them.

## Writing docs and tools on either machine

Prefer the environment variable to the literal path when you write a new tool. When you must write
a literal path into prose, say which machine it describes. The existing `E:\...` paths throughout
`docs/` are desktop paths and are being left as they are rather than rewritten, because they are
correct there and the register above is enough to translate them.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/codex-operating-guide.md](../ai-includes/codex-operating-guide.md)
- [docs/ai-includes/orientation.md](../ai-includes/orientation.md)
- [docs/ai-includes/taleworlds-research-guide.md](../ai-includes/taleworlds-research-guide.md)
- [docs/features/ghidra-native-decompile.md](../features/ghidra-native-decompile.md)
- [docs/features/orc-goblin-locomotion.md](../features/orc-goblin-locomotion.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/bannerlord-engine-and-toolchain.md](./bannerlord-engine-and-toolchain.md)

<!-- backlinks-end -->
