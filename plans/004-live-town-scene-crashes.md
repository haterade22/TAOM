# Plan 004: Fix 3 LIVE town-center scene crashes (Isengard, Helm's Deep, Calembel)

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md` — unless a reviewer dispatched you and told you they
> maintain the index.
>
> **This plan writes NO C# and runs NO build/test.** It is a DATA fix to an
> EXTERNAL (non-git-tracked) module file. The only write is to the LIVE game
> install file named in Scope — and ONLY after the user gives explicit go.
>
> **Drift check (run first)**: re-run the audit (read-only) and confirm the
> three crash suspects below still appear exactly as written:
> `python tools/audit_scene_names.py`
> Expected: a `[TAOM_Map] 3 missing from SceneObj:` block listing
> `HART_isengard`, `Helms_Deep_Town_forceatmo`, `lotrtaom_hat_gondor_town_calembel`,
> each tagged `*** NO SCENE ANYWHERE ***`. If the count or names differ, treat
> it as a STOP condition (the scene set on disk has changed since this was
> planned).

## Status

- **Priority**: P1
- **Effort**: S
- **Risk**: MED
- **Depends on**: none
- **Category**: data (crash)
- **Planned at**: commit `141b749`, 2026-06-13
- **Issue**: create before implementation lands — orchestrator (TAOM issue-first mandate)

## Why this matters

Three major, frequently-contested TAOM settlements — **Orthanc/Isengard** (`town_isengard`),
**Helm's Deep** (`town_V2`), and **Calembel** (`town_EW9`) — point their town-center
`Location` at `scene_name` values whose SceneObj folder no longer exists on disk
anywhere across the game's Modules. Entering any of these town centers (Take a walk,
sneak-in, lord's-hall transitions routed through the center, and siege missions keyed
off the center scene) crashes the campaign session. This is the same "battles/visits
near specific places crash" class that commit `4c68256` already fixed for 61 other
stale refs in this same file; this is a regression introduced by a later SceneObj
rename wave that the post-rename re-audit step in
`.claude/rules/vanilla-data-comparison.md` was skipped for. Remapping the three dead
refs to existing scenes makes all three settlements enterable again.

## Current state

This is a DATA finding, not C#. **No TAOM repo source is in scope.** The file that
actually ships to the player is the EXTERNAL, non-git-tracked
`E:/Steam/.../Modules/TAOM_Map/ModuleData/settlements.xml`. The repo's
`Main/_Module/ModuleData/settlements.xml` is a STALE SHADOW (last touched 2026-04-06,
NOT registered in Main's `SubModule.xml`); editing it changes nothing in-game. Do not
touch the shadow.

### The three broken settlement blocks (verified from the LIVE file this session)

Each town-center `Location` repeats the dead scene across all four level slots
(`scene_name`, `scene_name_1`, `scene_name_2`, `scene_name_3`):

- `E:/Steam/.../Modules/TAOM_Map/ModuleData/settlements.xml:9847` — `town_isengard`
  (display "Orthanc", `culture="Culture.isengard"`). Its center at **line 9867**:
  ```xml
  <Location id="center" scene_name="HART_isengard" scene_name_1="HART_isengard" scene_name_2="HART_isengard" scene_name_3="HART_isengard" />
  ```
- `E:/Steam/.../Modules/TAOM_Map/ModuleData/settlements.xml:6080` — `town_V2`
  (display "Helm's Deep", `culture="Culture.vlandia"`). Its center at **line 6100**:
  ```xml
  <Location id="center" scene_name="Helms_Deep_Town_forceatmo" scene_name_1="Helms_Deep_Town_forceatmo" scene_name_2="Helms_Deep_Town_forceatmo" scene_name_3="Helms_Deep_Town_forceatmo" />
  ```
- `E:/Steam/.../Modules/TAOM_Map/ModuleData/settlements.xml:3054` — `town_EW9`
  (display "Calembel", `culture="Culture.gondor"`). Its center at **line 3074**:
  ```xml
  <Location id="center" scene_name="lotrtaom_hat_gondor_town_calembel" scene_name_1="lotrtaom_hat_gondor_town_calembel" scene_name_2="lotrtaom_hat_gondor_town_calembel" scene_name_3="lotrtaom_hat_gondor_town_calembel" />
  ```

> Line numbers were read from the live file this session; the executor MUST NOT
> trust them blindly — the remap tool (Step 3) matches by string, not line, so
> it is line-number-independent. The grep verifications below re-confirm each
> ref at run time.

### Replacement scenes (each verified present on disk this session, case-insensitive)

| Dead scene_name (4× per center) | Settlement | Replacement scene (exists on disk) | Rationale |
|---|---|---|---|
| `HART_isengard` | `town_isengard` | `taom_isengard_town_orthanc_forceatmo` | The surviving renamed Orthanc/Isengard town scene. Currently referenced by NO settlement (orphaned), so reusing it is safe and lore-correct. |
| `Helms_Deep_Town_forceatmo` | `town_V2` | `taom_rohan_castle_helms_deep_forceatmo` | The surviving renamed Helm's Deep scene. Also orphaned. (Scene id contains `castle` but it is the authored Helm's Deep environment — the engine does not require the scene id to match the settlement's `is_castle`; the four `scene_name*` slots only name an environment to load.) |
| `lotrtaom_hat_gondor_town_calembel` | `town_EW9` | `empire_town_h` (recommended) — OR `taom_gondor_town_lossarnach_forceatmo` | **No Calembel scene exists on disk** (confirmed: `ls .../SceneObj/*calembel*` returns nothing). Pick a Gondor-appropriate town that already works in-game. `empire_town_h` is the vanilla town scene used by sibling Gondor town `town_EW4`; `taom_gondor_town_lossarnach_forceatmo` is a custom Gondor town scene used by `town_EW7`. Either stops the crash; this is an **aesthetic stopgap** until a real Calembel scene is exported (Option B). |

Verified-working sibling Gondor town center scenes that already exist on disk
(for context on the Calembel choice): `empire_town_a` (`town_EW10`), `empire_town_b`
(`town_EW11`), `empire_town_h` (`town_EW4`), `taom_gondor_town_lossarnach_forceatmo`
(`town_EW7`), `taom_gondor_town_minas_tirith_forceatmo` (`town_EW1`).

### Localization is NOT in scope (finding correction)

The audit finding's scope line said "the LIVE TAOM_Map/settlements.xml + its
loc_settlements.xml". **That is wrong for a scene remap.** Verified this session:
the per-language `Languages/<LANG>/loc_settlements.xml` files (BR, CNs, CNt, DE, FR,
IT, JP, KO, PL, RU, SP, TR — there is no EN dir; English lives in the base file's
`name=` attribute) carry only display-name string translations and contain **zero**
`scene_name` references (`grep -rl 'scene_name' .../Languages/` → none). A scene
remap changes an environment id, not a player-facing name, so **no loc file is
touched.** Do not edit any loc_settlements.xml.

### Save-compat

None. Scene names are not serialized into save files — they are looked up live from
`settlements.xml` each time a location is entered. The remap is safe for in-progress
saves.

### The remap tool already exists (and what's already in it vs. what's NOT)

`tools/remap_stale_scene_names.py` was built for exactly this in commit `4c68256`.
Its `REMAP` dict (read this session, lines 29–41) currently contains the EARLIER wave
of fixes (house-interior renames, an Osgiliath typo, and three Isengard *village/castle*
scenes) but **does NOT yet contain the three town-center scenes this plan fixes.** Step 2
adds them. The tool: verifies every replacement exists on disk before writing (aborts
otherwise), targets the LIVE file by default, optionally writes a `.xml.bak_scenes`
backup with `--backup`, and preserves the BOM. It edits the SHADOW only with `--shadow`
(do NOT pass it).

## Commands you will need

| Purpose | Command | Expected on success |
|---|---|---|
| Re-audit (read-only) | `python tools/audit_scene_names.py` | Before fix: `[TAOM_Map] 3 missing`. After fix: that TAOM_Map block is GONE (0 TAOM_Map misses). |
| Confirm a dead ref is present in the live file | `grep -c 'scene_name="HART_isengard"' "$LIVE"` | `4` before fix, `0` after |
| Dry-run the remap (writes nothing) | `python tools/remap_stale_scene_names.py --dry-run` | prints planned replacements + `[DRY RUN]`, exit 0 |
| Apply the remap (LIVE file — ONLY on explicit user go) | `python tools/remap_stale_scene_names.py --apply --backup` | prints `[APPLIED]`, writes `.xml.bak_scenes`, exit 0 |

Where `LIVE="E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/TAOM_Map/ModuleData/settlements.xml"`.

There is NO `dotnet build` / `dotnet test` for this plan — it touches no C#. Do not
run `./build.ps1`. Do not run `validate_moduledata.py` (it validates the repo's
`Main/_Module/ModuleData`, not the external TAOM_Map module, and would not see this
file).

## Scope

**In scope** (the only files that may be modified):
- `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/TAOM_Map/ModuleData/settlements.xml`
  — the LIVE external module file. (Editing it via `remap_stale_scene_names.py --apply`.)
- `tools/remap_stale_scene_names.py` — add the 3 new entries to its `REMAP` dict
  (Step 2). This IS a git-tracked repo file; the edit is a 3-line data addition,
  no logic change.

**Out of scope** (do NOT touch, even though they look related):
- `Main/_Module/ModuleData/settlements.xml` — the STALE SHADOW; editing it changes
  nothing in-game. Do NOT pass `--shadow` to the tool.
- Any `Languages/<LANG>/loc_settlements.xml` — carry no scene refs (see "Localization
  is NOT in scope" above).
- Any C# file, any GameModel, any Harmony patch. This is data only.
- Authoring/exporting a new 3D scene (Option B) — out of scope to execute; documented
  below for the maintainer only.

## Git workflow

- The LIVE file is NOT in the repo — it has no git history. That is exactly why
  `--backup` (writes `.xml.bak_scenes`) is mandatory on apply.
- The only git-tracked change is the 3-line `REMAP` addition in
  `tools/remap_stale_scene_names.py`. Commit it with the orchestrator's normal flow
  (50/72, no AI attribution), e.g.
  `data(scenes): remap 3 dead town-center refs (Isengard/Helm's Deep/Calembel)`.
  Suggested trailers: `Save-compat: none — scene names not serialized.`
  `Constraint: Calembel has no scene on disk — remapped to empire_town_h as a stopgap.`
- Do NOT push or open a PR from an executor.

## Steps

### Step 1: Drift-check — re-confirm the three crash suspects (read-only)

Run the audit and confirm the exact three TAOM_Map misses. Also confirm each dead
ref is present 4× in the live file and that each chosen replacement exists on disk.

```bash
python tools/audit_scene_names.py
LIVE="E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/TAOM_Map/ModuleData/settlements.xml"
grep -c 'scene_name="HART_isengard"' "$LIVE"
grep -c 'scene_name="Helms_Deep_Town_forceatmo"' "$LIVE"
grep -c 'scene_name="lotrtaom_hat_gondor_town_calembel"' "$LIVE"
```

**Verify**: audit prints `[TAOM_Map] 3 missing from SceneObj:` with the three names
each tagged `*** NO SCENE ANYWHERE ***`; each grep prints `4`. If the audit shows a
different TAOM_Map miss set, or a grep prints something other than `4`, STOP and report
(drift — the live file or scene set changed since this plan was written).

### Step 2: Add the three town-center entries to the remap tool's `REMAP` dict

Edit `tools/remap_stale_scene_names.py`. Inside the `REMAP = { ... }` dict (currently
lines 29–41, ending at the `village_isengard_a` entry), add three entries. Insert them
as a clearly-commented block so the diff is self-explanatory:

```python
    # Isengard custom scenes absent on disk -> rugged vanilla of matching type
    "castle_orthanc_gate": "battania_castle_a",
    "castle_village_isengard_a": "battania_village_c",
    "village_isengard_a": "battania_village_e",
    # town-center scenes deleted by the post-2026-05-28 SceneObj rename wave (plan 004)
    "HART_isengard": "taom_isengard_town_orthanc_forceatmo",
    "Helms_Deep_Town_forceatmo": "taom_rohan_castle_helms_deep_forceatmo",
    "lotrtaom_hat_gondor_town_calembel": "empire_town_h",
}
```

(The three existing Isengard lines above are shown for placement context only — do
not duplicate them; add only the three new lines under the new comment.)

> **Calembel choice**: the recommendation is `empire_town_h` (a vanilla Gondor-region
> town scene already in use by sibling `town_EW4`). If the user prefers a more bespoke
> look, `taom_gondor_town_lossarnach_forceatmo` is the alternative (used by `town_EW7`).
> If you change it, change ONLY the value string — both are verified present on disk.

**Verify**: `python tools/remap_stale_scene_names.py --dry-run` prints
`All N replacement scenes verified present on disk.` (NOT the `ABORT — replacement
scene(s) not found on disk:` branch), then lists the three new
`old -> new (4)` lines among the replacements, and ends `[DRY RUN]` with exit 0.
If it ABORTs, a replacement name is wrong — STOP and report (do not invent a different
scene).

### Step 3: Apply the remap to the LIVE file — ONLY after explicit user go

This writes to the external game install. Per TAOM's environment + autonomous-loop
rules, an irreversible write to a shared/external file requires explicit user
authorization for this run. **Do not run `--apply` until the user has said go.**
When authorized:

```bash
python tools/remap_stale_scene_names.py --apply --backup
```

This rewrites the LIVE `settlements.xml`, writes a `settlements.xml.bak_scenes` backup
beside it, and preserves the BOM. Do NOT pass `--shadow`.

**Verify**: the tool prints the three new `old -> new (4)` lines plus the carried-over
earlier entries, a non-zero `total replacements`, and `[APPLIED]`, exit 0.

### Step 4: Confirm zero TAOM_Map crash suspects (read-only)

```bash
python tools/audit_scene_names.py
LIVE="E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/TAOM_Map/ModuleData/settlements.xml"
grep -c 'scene_name="HART_isengard"' "$LIVE"
grep -c 'scene_name="Helms_Deep_Town_forceatmo"' "$LIVE"
grep -c 'scene_name="lotrtaom_hat_gondor_town_calembel"' "$LIVE"
grep -c 'scene_name="taom_isengard_town_orthanc_forceatmo"' "$LIVE"
```

**Verify**: the audit output NO LONGER contains a `[TAOM_Map] ... missing from SceneObj`
block (TAOM_shadow misses are pre-existing and out of scope — ignore them); the three
dead-ref greps now print `0`; the Isengard replacement grep prints `4`. If a TAOM_Map
miss remains, STOP and report.

## Test plan

There is no unit-test surface — this is external XML, not C#, and the repo's test
project does not load the external TAOM_Map module. Verification is the audit script
plus the greps in Steps 1–4 (a referenced-vs-on-disk consistency check).

- **Structurally untestable in `TAOM.Tests`**: the live-game in-mission load (does the
  player actually enter Orthanc/Helm's Deep/Calembel without CTD). Note it for the
  commit's `Not-tested:` trailer: `Not-tested: in-game town entry (external module,
  requires a live campaign session).`
- **Recommended manual smoke** (the maintainer/orchestrator, not the executor): in a
  live campaign, enter each of the three town centers ("Take a walk") and confirm no
  crash. This is the only check that proves the loaded scene actually renders.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `python tools/audit_scene_names.py` shows NO `[TAOM_Map] ... missing from SceneObj` block
- [ ] `grep -c 'scene_name="HART_isengard"' "$LIVE"` → `0`
- [ ] `grep -c 'scene_name="Helms_Deep_Town_forceatmo"' "$LIVE"` → `0`
- [ ] `grep -c 'scene_name="lotrtaom_hat_gondor_town_calembel"' "$LIVE"` → `0`
- [ ] `grep -c 'scene_name="taom_isengard_town_orthanc_forceatmo"' "$LIVE"` → `4`
- [ ] `tools/remap_stale_scene_names.py` has the three new `REMAP` entries (git diff shows +3 data lines, no logic change)
- [ ] A `settlements.xml.bak_scenes` backup exists beside the live file
- [ ] No file outside the in-scope list was modified (the shadow `Main/_Module/ModuleData/settlements.xml` is unchanged; no loc file changed)
- [ ] `plans/README.md` status row for plan 004 updated

## STOP conditions

Stop and report back (do not improvise) if:

- Step 1's audit shows a different set of TAOM_Map misses than the three named here
  (the scene set on disk drifted since this plan was written), or a dead-ref grep does
  not print `4`.
- The `--dry-run` in Step 2 hits the `ABORT — replacement scene(s) not found on disk`
  branch (a chosen replacement scene was renamed/removed) — report which one; do NOT
  substitute a guessed scene.
- The user has NOT explicitly authorized writing to the external game install — do NOT
  run `--apply`. Stop after the dry-run and report the planned changes.
- After `--apply`, Step 4's audit still lists a TAOM_Map miss (the write did not take,
  or a slot was missed).
- You find the fix would require touching a C# file, a loc file, or the shadow
  settlements.xml — it should not; report the surprise.

## Maintenance notes

For the human/agent who owns this after the change lands:

- **This is a stopgap, not a restoration.** Isengard and Helm's Deep now load their
  correct surviving scenes (a true fix). **Calembel** loads a generic Gondor town
  scene (`empire_town_h`) because no Calembel scene exists on disk — it stops the
  crash but is the wrong environment cosmetically.
  - **Option B (deferred, out of scope here)**: author/export a real Calembel town
    scene (SceneObj folder + `_anm.tpac`/`scene.xscene` etc.), name it e.g.
    `taom_gondor_town_calembel_forceatmo`, then add a one-line remap
    (`empire_town_h` → the new scene, or revert Calembel's refs directly) and re-run
    the audit. Scene authoring is asset/editor work (cannot be done from this repo's
    tooling); it needs the Bannerlord editor build, an FBX→tpac pipeline pass, and a
    SceneObj export. Track it as a follow-up issue.
- **Why this regressed**: a SceneObj rename wave AFTER the 2026-05-28 audit snapshot
  (where all three were `[OK]`) renamed the scene folders but did not repoint these
  three settlement refs. The prevention is in `.claude/rules/vanilla-data-comparison.md`
  / `docs/reference/scene-reference-audit.md`: **add "after ANY SceneObj
  rename/delete" as an explicit trigger to re-run `audit_scene_names.py`.** Consider
  making that doc edit a small follow-up (out of scope for this data fix).
- **What a reviewer should scrutinize**: that the remap touched the LIVE file (not the
  shadow), that `--shadow` was NOT passed, that all four `scene_name*` slots per center
  were rewritten (the tool replaces by exact `scene_name="..."` string, so all four
  slots are caught — the `(4)` count per entry confirms it), and that no loc file was
  touched.
- **Interaction**: any future `remap_stale_scene_names.py` run now also carries these
  three town-center entries — they are idempotent (replacing an already-correct ref is
  a no-op `count==0`).
