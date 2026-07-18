# Blow Diagnostics

**Status:** shipped 2026-07-17 · **Feature dir:** `Main/Features/BlowDiagnostics/` · **Patch category:** `Patch63_BlowDiagnostics` · **MCM page:** "TAOM — Blow Diagnostics" (OFF by default)

Toggle-gated, durable-flush instrumentation of the agent blow / death / siege-shot path. Ships to root-cause a **native crash that leaves no managed stack** — the last durable line before the process dies names the fatal blow.

## Why it exists

A player reported two crashes-to-desktop in one siege, playing a **dwarf** (a TAOM custom race), attacker in `khuzait_castle_siege_001`:
1. Crashed **when the character got wounded**.
2. Reloaded → crashed **when a fire pot was about to land**.

Both are **native AccessViolations**. The managed debug log and the native `rgl_log` both stop mid-battle with **no crash record** — no managed exception, no `[CrashReport]` stamp. TAOM's crash pipeline (`Patch37`) suspends BUTR and only catches *managed* exceptions, so a pure native AV in `TaleWorlds.Native.dll` produces no bundle at all. The crash could not be root-caused from the two logs.

**Ruled out** (each by direct evidence, so don't re-chase):
- **Dwarf action-set parity gap** — `python tools/audit_action_set_parity.py` vs installed 1.4.7 → **0 gaps** across all humanoid sets. `as_dwarf_warrior` is complete.
- **The defender Trebuchet** (`TaomSiegeEventModel`) — `SiegeEventModel.GetAvailableDefenderSiegeEngines` is **dead API in 1.4.5/1.4.6/1.4.7** (zero call sites). The override never runs; the fire pot is a plain vanilla `FireCatapult` projectile. *(Side effect: the SiegeDefense "defenders get Trebuchets" feature is silently non-functional on 1.4.5+ — separate issue.)*
- **Generic "dwarf blow → AV"** — the player's all-dwarf army took casualties for 6 minutes with no crash, so the fault is a *specific* blow, not any blow.

**Surviving hypothesis:** a custom-race (dwarf) agent takes a *specific* blow — the player's wounding hit, or the fire-pot AoE hit — through native `Agent.HandleBlowAux` / `Agent.Die`. This is the same fault family the spider **Patch47** (`Die`) / **Patch48** (`HandleBlowAux`) guard, but both are gated to `IsSpiderMonster`, so a plain dwarf is unguarded. This feature captures the distinguishing blow attribute (flags, damage type, missile/fall, victim race/health) so the fix can be precise, not a blanket guard.

## How it works

Three thin Harmony prefixes (category `Patch63_BlowDiagnostics`) delegate to `IBlowDiagnosticService`, which stamps to the **durable** log level (`IModLogger.LogInfo` — synchronous flush). Durability is the whole point: a native AV kills the process without unwinding, so anything still queued async is lost — which is exactly why the plain debug log lost the final 20s of the original crash (CareerSystem uses `LogDebug`). The tag is `[BlowDiag]`.

| Hook | Target | Stamp |
|------|--------|-------|
| `Agent_HandleBlowAux_BlowDiag_Patch` | `Agent.HandleBlowAux(ref Blow)` (private — last managed frame before native `MBAPI.IMBAgent.HandleBlowAux`) | `[BlowDiag] blow victim='…' race=N player=… mounted=…[ mount='…'] hp=… flags=… dmgType=… dmg=… mag=… missile=… fall=… part=… attackerIdx=…` |
| `Agent_Die_BlowDiag_Patch` | `Agent.Die(Blow, KillInfo)` | `[BlowDiag] DIE …` (same record shape, killing blow) |
| `RangedSiegeWeapon_ShootProjectileAux_BlowDiag_Patch` | `RangedSiegeWeapon.ShootProjectileAux(ItemObject, bool)` (base method — `FireMangonel`/`Mangonel` don't override it) | `[BlowDiag] siege-shot item='…' side=…` |

Design notes:
- **Siblings of Patch47/48, separate classes** — the spider guards are untouched. `HandleBlowAux` and `Die` are each patched by two TAOM prefixes now (the spider guard + this diagnostic); Harmony runs both.
- **`Priority.First`** on the `HandleBlowAux`/`Die` prefixes so the diagnostic records the **pristine** blow before Patch48 can strip `CanDismount`.
- **OFF by default.** `HandleBlowAux` is a per-damaging-hit hot path; a synchronous log write per blow is fine for a controlled repro but should not tax normal play. When disabled, each hook costs one cached-service read + one bool. Fail-open is to **OFF** (an MCM hiccup must not silently switch every battle into per-blow logging).
- **Never propagates.** Every hook body + `BuildRecord` + every service emit is wrapped so the diagnostic can never turn a blow into a crash of its own (mirrors Patch47/48).
- **No separate finiteness hook.** `Blow.InflictedDamage` is the raw engine `int`; a NaN damage multiplier upstream casts to `int.MinValue` on net472, so a bizarre `dmg=` value is itself the signal for the "non-finite damage into native `CalculateDamage`" hypothesis.

## How to use it (repro loop)

1. Deploy the build (`./build.ps1`), launch, load the save.
2. MCM → **"TAOM — Blow Diagnostics"** → enable **"Enable Blow Diagnostics"**.
3. Reproduce the crash (both the wound and the fire-pot cases, across sieges). Optionally play one fight as a **human/vanilla race** to confirm custom-race-specificity.
4. Send `<game>/…/bin/Win64_Shipping_Client/Logs/taom_debug_*.log`.

**Reading the result — the last `[BlowDiag]` line before the log stops:**
- `blow`/`DIE` on the **dwarf/player** with a distinguishing `flags=`/`dmgType=` → confirmed native blow-reaction AV (Patch47/48 family). Fix: extend the spider-guard pattern to the dwarf / the specific fatal flag — strip the offending `BlowFlag` or hard-guard the native reaction for that race, evidence-driven, not a blanket knockdown disable.
- `siege-shot` with `item='<null>'` and no blow after → the launch/projectile-item path.
- `siege-shot` then **nothing** → the impact particle/decal/effect faulted pre-blow in native → escalate to the Windows Event Log fault offset + `python tools/native_crash_triage.py --rva 0x<offset>`, and/or a fire-asset/scene audit.

## Configuration

MCM page **"TAOM — Blow Diagnostics"** (`BlowDiagnosticsSettings`, id `TAOM.BlowDiagnostics`), one toggle `EnableBlowDiagnostics` (default `false`). No JSON config. `Reuse.Singleton` provider — a mid-session toggle takes effect on the next blow.

## Files

| File | Role |
|------|------|
| `IBlowDiagnosticService.cs` / `BlowDiagnosticService.cs` | Formats + emits `[BlowDiag]` stamps via `IModLogger.LogInfo`; `IsEnabled` gate; never propagates. Unit-tested (`TAOM.Tests/Features/BlowDiagnostics/BlowDiagnosticServiceTests.cs`, 14 tests). |
| `Domain/BlowDiagRecord.cs` | Primitive DTO (no sealed TaleWorlds types cross into the service — ADR-007). |
| `IBlowDiagnosticsSettingsProvider.cs` / `BlowDiagnosticsSettingsProvider.cs` | MCM read, fail-open OFF. |
| `BlowDiagnosticsSettings.cs` | MCM page. |
| `BlowDiagnosticsIoC.cs` | DI registration (called from `Main/IoC.cs`). |
| `Hooks/Agent_HandleBlowAux_BlowDiag_Patch.cs` | Primary instrument + shared `BuildRecord`. |
| `Hooks/Agent_Die_BlowDiag_Patch.cs` | Death case. |
| `Hooks/RangedSiegeWeapon_ShootProjectileAux_BlowDiag_Patch.cs` | Siege-shot marker. |

Registration: IoC at `Main/IoC.cs` (with the other diagnostics features); patch category applied in `Main/SubModule.cs` alongside Patch47/48/50.

## Related

- `docs/features/spider.md` — Patch47/48, the mounted-death/hit native-AV fault family this diagnostic is hunting a dwarf-side analog of.
- `docs/features/battle-load-diagnostics.md`, `save-load-diagnostics.md` — the durable-appender precedent (`Main/Core/Logging/FileLogger.cs`).
- `.claude/skills/native-crash-triage/SKILL.md` — the fallback path (Event Log fault offset) when the diagnostic points at a pre-blow native impact effect.
