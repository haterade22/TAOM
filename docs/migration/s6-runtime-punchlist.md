# S6 Runtime Verification Punch-List (human-in-the-loop)

The v1.3.15 → v1.4.5 migration's formal S6 smoke-test gate was never run as a discrete step (see [`TRACKING.md`](./TRACKING.md)). Its **offline-checkable portion is now a standing test gate** — see below. This file is the **residue**: the checks that genuinely require a running game or human judgement. They cannot be self-served by an agent; work through them at the keyboard with Bannerlord launched + TAOM enabled.

## Already covered offline — do NOT re-do by hand

These ran as part of S6's intent and are now permanent `dotnet test` gates (`TAOM.Tests/Migration/`, `TestCategory=BindingVerification`). If they're green, the corresponding S6 concern is closed:

- ✅ **All 110 Harmony patch targets bind** against the installed v1.4.5 engine (`HarmonyPatchBindingTests`). Replaces the "verify patch target methods exist" line of S4/S6.
- ✅ **All 39 GameModels are registered + override correctly** (`GameModelOverrideBindingTests`).
- ✅ **32 auxiliary reflection members resolve** (`ReflectionSiteBindingTests`).

Run before touching the items below: `dotnet test TAOM.Tests/TAOM.Tests.csproj --filter "TestCategory=BindingVerification"`.

---

## Needs a running game (agent cannot self-serve)

### 1. Launch smoke test — patches apply, campaign loads
- [ ] Launch Bannerlord with TAOM (+ TAOM.Dependencies, TAOM_Map) enabled, start a **new campaign**.
- [ ] Check the RGL log (`%USERPROFILE%\Documents\Mount and Blade II Bannerlord\logs\rgl_log_*.txt`) for Harmony patch-apply exceptions or `MissingMethod/MissingField/TypeLoad` from `PatchShield`/`SaveShield` (DR3 defensive infra writes to `<game>/Modules/TAOM.Dependencies/diag.log`).
- [ ] Confirm no "could not be found" / "failed to patch" lines. The offline gate proves *resolution*; this proves *application + no runtime throw*.

### 2. Six `VerticalBottomToTop` ListPanel sites — visual order
v1.4.0 fixed inverted `VerticalTopToBottom` / `VerticalBottomToTop` semantics (`TRACKING.md` S5). Visual order may now be wrong at these sites:
- [ ] `Main/_Module/GUI/Prefabs/FacGen/PreBuildCharacterSelection.xml` (lines 38, 40, 51, 59) — CC pre-build character selection list.
- [ ] `Main/Features/Messengers/UI/MessengerEncyclopediaPrefabExtension.cs:24` (string-injected) — messenger encyclopedia panel.
- [ ] If a list renders bottom-to-top / reversed, swap `VerticalBottomToTop` → `VerticalTopToBottom`.

### 3. Equipment-roster ruler fallback — no naked rulers
4 of 12 optional rosters per culture (`IsKingdomRulerTemplate` × {M/F} × {Battle/Civilian}) were deferred (`TRACKING.md` S5b); the engine should fall back to `IsLordTemplate`.
- [ ] In a new campaign, inspect each custom culture's **ruler** (king/faction leader) in the encyclopedia — confirm fully clothed, not in underwear.
- [ ] If any ruler is naked/underdressed, author dedicated `IsKingdomRulerTemplate` rosters in `taom_lord_template_equipment.xml`.

### 4. `CanMakeAlliance` veto — diplomacy still works
v1.4.5 `DefaultAllianceModel.CanMakeAlliance` adds score-threshold + player-support gates that can independently veto, despite TAOM's `MaxNumberOfAlliances => int.MaxValue` (`TRACKING.md`).
- [ ] In a campaign with War of the Ring active, confirm AI factions still form the expected alliances. If alliances never form, evaluate overriding `CanMakeAlliance` in `TaomAllianceModel`.

### 5. Naval-event behaviour — no crash on naval events
v1.4.5 added naval methods (`Ship`/`Figurehead`/`IsTargetingPort`) to BattleReward / CombatSimulation / MilitaryPower / TargetScore models; TAOM doesn't override them but vanilla may invoke them (`TRACKING.md`).
- [ ] If any naval/port event can fire on the TAOM map, confirm no crash. TAOM is land-only by design — document naval as out-of-scope if confirmed unreachable. (Tracked at #120 for NavalDLC port support.)

### 6. `SpecialResourcesBehavior.OnHideoutCompleted` — gating not too permissive
TAOM earns the resource for any `winnerSide == Attacker` regardless of `battleEndState` (accepted-permissive deferral, `TRACKING.md:142`).
- [ ] Clear a hideout and confirm the resource award fires only when intended. If a non-victory end-state (Retreated/SendTroops) wrongly awards, gate on `battleEndState == Victory`.

### 7. Category C runtime-dynamic reflection — verify in-game
These reflection sites resolve their *type* from a live instance (`instance.GetType()`), so they can't be checked offline (see [`../reference/taleworlds-api-snapshot/reflection-sites.md`](../reference/taleworlds-api-snapshot/reflection-sites.md) Category C):
- [ ] **FactionMap CC culture flow** — open Character Creation, confirm culture selection / stage progression works (CultureSettingService, CultureStageView hooks).
- [ ] **CrashReport collectors** — trigger/observe a crash report and confirm the MCM-settings + stack-frame collectors don't throw (they reflect over arbitrary frames + optional ButterLib/MCM types).

---

## Closing the punch-list

When all boxes are checked, note completion in [`TRACKING.md`](./TRACKING.md) and close any remaining migration tracking issue. The offline gate stays green-on-every-build going forward; this human list only needs re-running after a Bannerlord version bump that the offline gate flags.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/migration/TRACKING.md](./TRACKING.md)
- [docs/reference/taleworlds-api-snapshot/reflection-sites.md](../reference/taleworlds-api-snapshot/reflection-sites.md)

<!-- backlinks-end -->
