# Engine bump: branch detail

Each section serves one step of `.claude/skills/engine-bump/SKILL.md`, which sends you here when
that step's case arises. The step itself keeps its gate and its stop rules.

## Phase 1 item 2: the archive check failed

A mismatch or no row means the installed build is not archived (a silent run can mean the lock is
held): copy it to `_native\TaleWorlds.Native.v<ver>.{client,wEditor}.dll` with `<ver>` from that
folder's `Version.xml`. `E:\Decompiled_Bannerlord\_native\` must hold the OLD
`TaleWorlds.Native.v<old>.{client,wEditor}.dll` pair; if it does not, copy both `bin/Win64_Shipping_{Client,wEditor}/TaleWorlds.Native.dll` aside by hand
now:
that copy is the only baseline the NEXT bump will have, and `native_build_match.py` reads it. If the OLD pair
is already missing it is gone: record that and expect `oracle=unscored`. What a missing copy cost once:
[v1.4.8-impact.md](../../../../docs/migration/v1.4.8-impact.md), "The Modding Kit jump and native-crash triage".

## Phase 4 item 3: fixing a humanoid gap or a root-level action

For each humanoid gap, fix with
`python tools/patch_dwarf_action_parity.py --target <action_sets.xml> --set-id <id> --apply`.
**`--target` is `required=True`** (the command previously written in SKILL.md omitted it and argparse
rejects it outright), and it patches ONE file per run, so run it twice: the live
`<game>\Modules\LOTRLOME_Armory\ModuleData\action_sets.xml` and the tracked
`docs/reference/lotrlome-armory-snapshot/action_sets.xml`.

For a root-level `<action>`, fix with
`python tools/oneoff/fix_orphaned_tavern_conversation_actions.py --apply` (idempotent; rewrites the live Armory
file and the tracked snapshot together).
