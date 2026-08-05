# Investigation — dwarf-vs-Rhûn battle CTD (in flight)

**Status:** root cause NOT established. Evidence collection + repro in progress.
**Reporter symptom:** *"When playing as Dwarves and fighting Rhun Lords my Game constantly crashes."*
**Artifacts received:** `taom_debug_2026-08-02_15-01-18.log`, `rgl_log_24728.txt` (one session).
**Build:** TAOM `v2.0.0.0 build.20260802-015330Z+0f3f5ef7` (pair OK), engine v1.4.7.117484.

Rename to `rca-*.md` once the root cause lands.

## Session facts

| | |
|---|---|
| Save | `save149`, character `Drel`, culture `erebor`, kingdom `erebor`, WotR day 93 |
| Battle | Carndûr defence, `battle_terrain_031`, **not** a siege, not a raid |
| Sides | Player = **defender**. ATK 746 across 12+ Rhûn parties; DEF 1835 |
| Action sets live | `as_dwarf_warrior`, `as_dwarf_female_warrior`, `as_human_warrior`, `as_human_female_warrior`, `as_horse`, **`as_chariot`** |
| Live modules | TAOM stack + `MBSuperSpeed` only (11 active) |
| Timeline | mission start `15:02:11` · battle plan `15:02:16.751` · last TAOM line `15:02:15` · last rgl line `15:02:57.737` · **process dies, no error record** |

## Established

1. **Hard native crash.** `rgl_log` truncates mid-stream; no exception, no assert, no crash dialog
   trace. Managed code never reported anything.
2. **The 42 s of TAOM silence is real evidence, not a lost buffer.**
   `Main/Core/Logging/FileLogger.cs` drains INFO/WARNING/ERROR **synchronously with a flush** on
   the calling thread precisely so the tail survives a native AV. So: no TAOM managed exception and
   no `Patch63` banner-bearer ANOMALY fired.
3. **No TAOM crash bundle was produced** (none sent, and consistent with #1). `CrashReport`
   attaches 247 finalizers over 10 lifecycle methods + the Native2Managed shims; a fault that never
   crosses a managed boundary yields no `taom_crash_*.zip`.
4. **The fault is at first melee contact**, not spawn/deployment. Battle plan at `:16.7`; combat
   particle shaders first compile at `:48.9` and `:52.5`; a new metallic shadowmap at `:57.7`;
   death immediately after. ~41 s of closing distance then contact.
5. **The chariot is Rhûn's only non-vanilla field unit.** `wainrider_swift_chariot` /
   `wainrider_warlord_chariot` ride `Item.taom_chariot_a`. Chariot agents confirmed present in this
   battle (`ActionSet 'as_chariot' … first agent: 'Wainrider War Chariot'`).

## Ruled out (checked, negative — do not re-litigate without new evidence)

| Suspect | Why it's out |
|---|---|
| **Chariot animation wiring** | All 24 `animation=` targets in `as_chariot` / `monster_usage_sets` resolve to real deployed clips — 0 missing, 0 orphaned. Both formerly-untagged gait clips (`chariot_gait_walkfast`, `chariot_gait_walkbackfast`) **do** carry `quad_movement` in the shipped `_anm.tpac`. The latent AV `docs/features/chariot.md` flagged is fixed in the deployed assets. |
| **Banner-bearer reinforcement AV (#360)** | Fix `9758eb22` is in this build. `Patch63_BannerBearerSpawnGuard` logs an ANOMALY line when it fires; it did not fire, and that log level is durable (see Established #2). |
| **Third-party mods** (`BeardsFix`, `CharacterReload`, `BetterTime`) | They appear only in the engine's `Unofficial modules are used:` line, which enumerates `Campaign.PreviouslyUsedModules` (`DumpIntegrityCampaignBehavior.CheckIfModulesAreDefault`) — a **historical** list of everything ever loaded with this save. `ModuleHelper.GetActiveModules()` reports 11, none of them these. Not loaded; not a confound. |
| **Chariot Monster missing death attributes** | The chariot lacks `fall_blow_damage_bone` and all `ragdoll_bone_to_check_for_corpses_*` / `ragdoll_fall_sound_bone_*` vs the vanilla horse. **Control test kills it:** across all 94 Monster definitions in the load order, only `chariot` and `warg` lack `fall_blow_damage_bone`, and the warg is battle-proven. `body_rotation_reference_bone` is absent on 81/94 including `human` and `dwarf` — normal for non-horse monsters. Absence alone is survivable. |

## Still unknown

Which native site faulted. Nothing in the two supplied files answers it.

## Phase 1 — artifact request (send to reporter)

> Thanks — those two logs told us a lot. They show the game died inside the engine itself rather
> than in mod code, which means the answer is in one of three places we don't have yet. All three
> are quick:
>
> **1. Any TAOM crash bundle.** Look in
> `…\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\Logs\` for files named
> `taom_crash_<date>_<code>.zip`. Send the newest one — **and if there aren't any at all, tell us
> that too**, because "no bundle" is itself a useful answer.
>
> **2. The BUTR crash report.** If a crash window appears with a "Save report" / HTML option, save
> and send it.
>
> **3. The Windows fault offset.** Open PowerShell and paste this right after a crash:
> ```powershell
> Get-WinEvent -FilterHashtable @{LogName='Application'; ProviderName='Application Error'; StartTime=(Get-Date).AddHours(-6)} |
>   Where-Object { $_.Message -match "Bannerlord" } |
>   ForEach-Object { ($_.Message -split "`n" | Select-Object -First 8) -join "`n"; "---" }
> ```
> Send the output. **If you can, do this after two or three separate crashes** — if the "Fault
> offset" is the same every time it's one bug, and if it changes we're chasing more than one.
>
> Two questions that would narrow this a lot:
> - Does it crash against **other** factions when you play dwarves, or only against Rhûn?
> - If you **auto-resolve** the same battle instead of fighting it, does it still crash?

Caveat to remember when reading the reply: a crash held by a debugger never reaches WER, so there
will be no Event Log entry in that case.

## Phase 2 — in-house repro protocol

The chariot is the only element unique to a Rhûn field army, so one A/B implicates or exonerates it
in a single sitting. Uses the shipped dev console only — no new code. `taom.spawn_troops` uses the
**mission** gate, so it works in Custom Battle.

Setup for every arm: Custom Battle → player on the dwarf/Erebor side vs Rhûn, largest battle size,
then open the console (`~`) once the mission is live.

| Arm | Console | Watching for |
|---|---|---|
| **A** — chariot present | `taom.spawn_troops wainrider_warlord_chariot 20 enemy`<br>`taom.spawn_troops <dwarf infantry id> 40 ally` | CTD once the lines meet |
| **B** — chariot absent | `taom.spawn_troops far_rhun_cataphract 20 enemy`<br>`taom.spawn_troops <dwarf infantry id> 40 ally` | Should run clean if the chariot is the cause |
| **C** — isolate the event | Arm A, then `taom.damage_agent` on chariots one at a time from range | Separates mount **death** from mount **movement** from **blow-on-rider** |

`taom.print_agent_info *` before each run records what actually spawned (race, monster, action set,
skeleton) rather than guessing from the model.

**Capture the Event Log `Fault offset` for every arm that crashes and compare them.** Identical
offsets = one site. This is the same discrimination that exonerated Patch47.

If A **and** B both crash, the chariot is exonerated and the next target is the dwarf-vs-human melee
path — `AdvancedCombat` / CombatMechanics runs on every blow and matches the first-contact timing
(prior art: `docs/reviews/rca-combat-mechanics-2026-07-02.md`).

## Side-findings — all three FIXED 2026-08-02 (none is the crash)

**S1. `tools/audit_mount_parity.py` never covered the chariot.**
`MOUNTS = ["spider", "warg", "elephant", "mumakil"]` — the exact follow-up `docs/features/chariot.md`
named as pending. Added **section F**, baselined on the vanilla horse (the chariot is a ridden
vehicle with no BT, so the horse is its reference class — comparing it against the warg produced a
false-positive HIGH once already). Section F re-derives from the deployed artifacts, not from the
doc: monster attribute/flag delta, usage-verb and row coverage, action binding, every `animation=`
target resolving, and `quad_movement` on every `monster_usage_movements` clip. **Result: clean.**

Two calibration notes worth keeping, both caught by controls rather than reasoning:
- The `quad_movement` check must read **only** `monster_usage_movements`. A first draft also swept
  the upper-body table and flagged 7 clips — all `*_head` look overlays, which correctly must NOT
  carry the tag.
- `act_run_forward_adder` is unbound on the chariot **and on the vanilla horse** — a vanilla-tolerated
  hole, exempted (section D already had the same exemption).

**S2. Three dangling ID refs** (`Null object reference found with ID: …`, rgl 4841-4843 →
`MBObjectManager.UnregisterNonReadyObjects`). Each was a typo against an overwhelming local majority:

| Ref | File | Fix | Local evidence |
|---|---|---|---|
| `BodyProperty.fighter_umbar` ×5 | `Main/_Module/ModuleData/characters/lords.xml` | → `fighter_haradrim` | `fighter_umbar` is defined nowhere; `fighter_haradrim` is defined in `TAOM_bodyproperties.xml` and used by the other 102 lords in the file |
| `Culture.rhun` ×22 | `LOTRLOME_Armory/…/LOTRLOME_items/rhun/head_armors.xml` | → `Culture.khuzait` | 609 items in the same `rhun/` folder already use `khuzait` |
| `Culture.rohan` ×6 | `LOTRLOME_Armory/…/LOTRLOME_items/LOTRAOM_horses.xml` | → `Culture.vlandia` | `.claude/rules/xml-data.md` names both of these as the canonical mistake |

The two Armory files are **not under version control** (that module is not a git repo) — originals
saved beside them as `*.bak-dangling-culture-20260802`.

**S3. MissionDiag could not read the campaign time.**
`Campaign: <time read failed: DivideByZeroException>` — the session snapshot runs before
`Campaign.Models` is built. `CampaignTime.ToString()` (`CampaignTime.cs:357`) evaluates `GetYear`
first, which integer-divides by the static `TimeTicksPerYear` while it is still `0`; the constants
are assigned in `CampaignTime.Initialize()` (`:178-198`) from `CampaignTimeModel`. Every crash
report we receive therefore lacked the in-game day, which is a routine correlation key.

The window is **structural, not a race**, and the existing `GameStarted` guard does not close it —
both established by decompiling installed v1.4.7 during the deep review:
- `Campaign.OnInitialize` calls `GameManager.OnGameStart` (`Campaign.cs:1391`), which is what
  invokes TAOM's `SubModule.OnGameStart`, **three lines before** `CampaignTime.Initialize()` at
  `:1394`. Every session start hits this ordering.
- On a **save-load**, `Campaign.GameStarted` is already `true` at that point (set by
  `SetLoadingParameters`), so the guard passes and the read is attempted. On a **new campaign**
  `GameStarted` is still `false`, so the guard blocks it. That asymmetry is why this is a
  save-load-only symptom.

> Correction (deep review, 2026-08-02): the first draft of this section — and the shipped code
> comments and CHANGELOG entry — named `GetDayOfSeason` / `TimeTicksPerDay` as the throw site.
> That was inferred from grepping for a division rather than tracing `ToString()`'s evaluation
> order, and it is wrong: `GetYear` is evaluated first and throws first. The conclusion (guard and
> catch; there is no public non-dividing tick accessor) is unaffected. Recorded rather than quietly
> overwritten, per `.claude/rules/evidence-over-claims.md` §C.

`MapTimeTracker` and `NumTicks` are both `internal`, so there is no earlier readable source — the
fix is not a fallback but a second emission: `LogMissionStartSnapshot` now logs the campaign
context too, where models are always up. The guard logic moved into a pure, tested
`CampaignContextFormatter` (6 tests) that pins the real contract — the time and hero halves are
guarded **independently**, so one failing never blanks the other.

### Follow-ups NOT taken (deliberate — out of this change's scope)

Both are real gaps that let S2 ship; neither is a one-liner, so each wants its own change:
1. `validate_moduledata.py` has an `UNKNOWN_CULTURE` check but its registry scope is TAOM's
   `Main/_Module/ModuleData` — it does not sweep `culture=` on **Armory item** files, which is
   where 28 of the 33 dangling refs lived.
2. `BodyProperty.*` references are not cross-checked at all by any validator.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/chariot.md](../features/chariot.md)
- [docs/reviews/investigation-dunland-tournament-ctd-2026-08-02.md](./investigation-dunland-tournament-ctd-2026-08-02.md)

<!-- backlinks-end -->
