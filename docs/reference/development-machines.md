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
| Content modules | complete | incomplete, see the warning below |

Neither machine is wrong. The desktop is where a claim about content gets settled, because it is
the only one holding all of it.

## How a path resolves, and where it does not

Three environment variables carry the difference. Set them per machine; never hardcode a drive
letter into new code.

| Variable | Set where | Read by |
|---|---|---|
| `BANNERLORD_GAME_DIR` | Windows user env var (`setup-dev-env.ps1` writes it) | `tools/_gamedir.py`, so every validator and data tool; `Directory.Build.props`, so the build |
| `TAOM_DECOMPILE_ROOT` | Windows user env var | `tools/check_handbook_attributes.py`. Point it at the **category tree**, e.g. `C:\Decompiled_Bannerlord\_categories_v1.4.8` |
| `TAOM_PYBIN` | `.claude/settings.json` env block | `.claude/hooks/_pybin.sh`. The same value works on both machines because Python lives at `C:\Python314` on each; a stale pin degrades to discovery rather than failing |

**Three things ignore all of that** and need the path passed by hand:

- `tools/decompile_bannerlord.ps1` and `tools/decompile_to_folder.ps1` take `-Out` / `-GameBin` /
  `-Source` / `-Destination` parameters and honour no environment variable (this is already noted
  in [tools/README.md](../../tools/README.md)).
- `.mcp.json` is committed with the desktop's `E:\` paths hardcoded, so on the laptop the
  `filesystem` server points at directories that do not exist and `taom-moduledata` invokes
  `E:/repos/TAOM/tools/taom_mcp_server.py`. Editing that file to suit one machine breaks the
  other. The fix is a local-scope MCP override in user config, not a change to the committed file.
- `.claude/settings.local.json` is **tracked**, despite the name, so it is not a machine-local
  slot either. Machine-specific values belong in Windows user environment variables.

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
- **Blender** is absent, so creature-animation work (`/refine-creature-anim`, the Blender MCP,
  `tools/dump_engine_skeleton.ps1` round trips) has to happen on the desktop.

## Writing docs and tools on either machine

Prefer the environment variable to the literal path when you write a new tool. When you must write
a literal path into prose, say which machine it describes. The existing `E:\...` paths throughout
`docs/` are desktop paths and are being left as they are rather than rewritten, because they are
correct there and the register above is enough to translate them.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reference/bannerlord-engine-and-toolchain.md](./bannerlord-engine-and-toolchain.md)

<!-- backlinks-end -->
