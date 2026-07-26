# RCA — BannerBearers reinforcement AV (siege CTD) + Patch63 review findings

**Date:** 2026-07-25 · **Issue:** #360 · **Crash bundle:** `taom_crash_20260726_012011_67b75cb4` (signature `67b75cb4`)
**Prior related RCAs:** [rca-banner-bearers-2026-07-16.md](rca-banner-bearers-2026-07-16.md) (culture-key CRITICAL) · [rca-banner-bearers-siege-ctd-2026-07-23.md](rca-banner-bearers-siege-ctd-2026-07-23.md) (heraldry-tableau AV, `0x28ac0e`)

## Top-line

`AccessViolationException` in `Agent.GetWeaponEntityFromEquipmentSlot(ExtraWeaponSlot)` from `BannerBearerLogic.SpawnBannerBearer` during a defender **reinforcement wave** ~6 minutes into the siege of Glad Thaw (Mirkwood castle, `SiegeMissionWithDeployment`, Bannerlord v1.4.7.117484, TAOM v2.0.13). Third banner-bearers incident; second in a siege; first on the reinforcement path — which the feature doc had explicitly listed as verification still owed.

## Crash root cause

**The engine read:** `SpawnBannerBearer` (decompiled `BannerBearerLogic.cs:764-774`, byte-identical across 1.4.5–1.4.7) spawns the troop with the formation banner, then unconditionally reads the agent's slot-4 native weapon entity through an unvalidated P/Invoke (`Agent.cs:2708`). An absent native slot record is a `0xC0000005`.

**Why slot 4 can be empty on this path only:** the reinforcement branch (`MissionBattleSideSpawnContext.cs:364-378`, `Mode != Deployment && GetMissingBannerCount > 0 && !IsHero`, `wieldInitialWeapons: true`) installs the banner through `Mission.SpawnTroop`'s **validating** gate and then wields initial weapons **natively**. Deployment bearers use the permissive `UpdateAgent`/`CreateBannerEquipmentForAgent` path that writes slot 4 unconditionally and never runs the crashing code — hence the 6-minute fuse: the first wave that found a missing banner was the first execution.

**The TAOM trigger [Likely — guard-instrumented, not statically provable]:** Mirkwood's `<banner_bearer_replacement_weapons>` were three `TwoHandedPolearm` `<CraftedItem>`s + one sword (`taom_spcultures.xml:825-830`, mirrored in `mirkwood_stalkers`; Isengard shipped two pikes). **Every vanilla culture ships only 1H swords there, and `SandboxBattleBannerBearersModel.GetBannerBearerReplacementWeapon` tier-matches with no weapon-class filter** — an undeclared engine invariant. The banner item is `HeldInOffHand + HasToBeHeldUp + DropOnWeaponChange`; `Equipment.GetInitialWeaponIndicesToEquip` (`Equipment.cs:618`) classifies the banner as the off-hand wield and the sidearm as main-hand, and the native wield of a two-handed main-hand (`TryToWieldWeaponInSlot` — pure native, undecidable from managed decompile) plausibly drops the slot-4 banner before the unguarded read. Patch63's anomaly log is the standing confirmation channel: a recurrence names the sidearm and weapon class instead of crashing.

**Ruled out by direct verification:** bad banner item (`scouts_flag_t1`/`standard_of_fury_t1` pass `IsBannerItem && BannerComponent != null`; no `CannotBePickedUp` flag), creatures in this battle (no spider/troll stacks in either side's party set), native-offset triage (no WER entry — TAOM's CrashReport finalizer caught the AV; managed stack is the site evidence).

## The fix (three layers)

1. **Data** — `taom_spcultures.xml`: mirkwood/mirkwood_stalkers keep only `mirkwood_sword_a01`; isengard's pikes replaced with `isengard_1h_sword_a` + `isengard_berserker_sword`. Restores the engine's 1H invariant. No save impact (XML-loaded, never persisted).
2. **Test** — `BannerBearerReplacementWeaponDataTests` pins the invariant build-time: every replacement weapon across `taom_spcultures.xml` + `spcultures.xslt` must be 1H, classified against the **installed** Armory + SandBoxCore item XML (failed on all 5 polearm entries before the data fix).
3. **Code** — `Patch63_BannerBearerSpawnGuard`: prefix-replacement of `SpawnBannerBearer` with (a) a toggle-folded race/formation-group eligibility gate (the engine's reinforcement path applies no per-agent policy — a cave troll could be handed a banner), (b) a managed slot-4 check before the native read (anomaly → mechanism-naming WARN, no CTD), (c) an AV-only catch on the clean-path read (Patch62 precedent). Fail-open on binding drift; pins in `BannerBearersBindingTests`.

## Review findings (deep-review 2026-07-25, 5 agents)

| # | Sev | Finding | Category | Why missed | Outcome |
|---|-----|---------|----------|------------|---------|
| 1 | HIGH | First-cut prefix folded `Enabled=false` into "ineligible" via `IsFormationGroupAllowed`, silently starving **vanilla** hero-captain formations of mid-battle replacement bearers when the feature is off — strictly-worse-than-vanilla, the exact regression class the 2026-07-16 review caught on the model layer | toggle-fold | The master-toggle-fold lesson was scoped to GameModel overrides; this was the same bug in a Harmony prefix. Author reused `IsFormationGroupAllowed` without tracing its `!Enabled => false` branch under the disabled state | FIXED — `IsReinforcementBearerAllowed` folds the toggle in the service (disabled ⇒ allowed ⇒ vanilla parity; the crash guard stays active); 6 tests |
| 2 | MED | Reinforcement gate omits the deployment gate's agent-level base checks (`IsHuman`, `Character is CharacterObject`) — a future non-humanoid race absent from `ExcludedRaces` would pass | gate-parity | Agent-level checks cannot run pre-spawn; the divergence was real but undocumented | DOCUMENTED in the patch doc-comment as accepted; the slot-4 guard is the backstop (a creature rig surfaces as `SlotEmpty` and is declined safely) |
| 3 | LOW | Patch file 153 lines (ADR-002 ceiling 150) | thin-entry | Doc-comment growth during authoring | FIXED — trimmed to exactly 150 |
| 4 | NOTE | The 1H test reads the **installed** Armory; `lotraom-assets` ↔ install sync is a manual `robocopy` with no CI gate, so a repo-side weapon-class change unsynced to the install is a silent false-green | tooling-drift | Known operational property of the asset pipeline, out of this changeset's scope | Recorded here; housekeeping candidate: `sync-modules.ps1` module map still names `v1.2`/`v1.3` folders |
| 5 | NOTE | Enabled + fully-ineligible formation (e.g. all-troll) logs one info line per reinforcement troop per wave | log-noise | Deliberate simplicity trade-off | Accepted — bounded by wave size, info-level |

## Why each agent missed the crash-class originally (feature review, 2026-07-16)

The feature shipped with both reviews clean because: the reinforcement path is engine-internal (no TAOM code on the crashing frames — per-file review had nothing to read); the heraldry guard added on 2026-07-23 sampled deployment-time agents only, and its RCA's own codified lesson ("broadening an engine call re-opens every precondition its vanilla callers relied on") was applied one scope too narrowly — deployment-time preconditions were re-checked, mission-lifetime ones were not; and the 1H sidearm invariant exists nowhere in engine XML schemas or docs — it is visible only by *diffing TAOM's culture data against every vanilla culture's*, a comparison no agent prompt asked for.

## Lessons appended

- `docs/reviews/lessons/data-content-cultures.md` — declare-and-pin undeclared vanilla data invariants: when TAOM data feeds an engine consumer with no validation, diff the vanilla corpus for the implicit invariant and pin it with a build-time test.
- `docs/reviews/lessons/harmony-il.md` — master-toggle fold applies to **any** TAOM policy consulted from an engine-replacing patch, not just GameModel overrides; a service method that folds `!Enabled` into a *policy denial* is wrong for callers needing *vanilla parity* — fold at the decision method, per caller intent.

## Verification state

- Build green, suite 4426/4428 (2 pre-existing skips), `validate_moduledata` PASS, binding pins resolve against installed v1.4.7.
- **In-game siege smoke owed (user):** replay the Glad Thaw save past the reinforcement phase — expect no CTD, bearers with 1H sidearm + banner; any `[BannerBearers] Patch63 ANOMALY` WARN in `Logs/taom_debug_*.log` confirms the drop mechanism in the wild (either outcome is signal — record it here). Also: field battle both sides; a Mordor battle for the troll gate; feature-off battle for vanilla parity.
