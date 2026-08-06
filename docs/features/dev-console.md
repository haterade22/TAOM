# Dev Console (`taom.*` commands)

**Status:** Phase 0 (shared contract) built 2026-07-31 (#369, successor to #365). In-game discovery gate not yet run — see [The launch gate](#the-launch-gate).

## Overview

The `taom.*` console command group and the shared contract every command in it routes through. The
commands exist to collapse in-game smoke tests that currently cost minutes-to-hours of manual setup —
"start a siege and survive a reinforcement wave", "play ~50 in-game days until the momentum payload
crosses 32 KB" — into a single console line.

This doc owns the **engine contract** for console commands. Individual commands are documented in
their own feature's doc; the per-command reference table below links out.

## Why This Exists

- **Vanilla behavior:** ships 80 `campaign.*` and 10 `mission.*` cheats in the shipping client. They
  cover gold, troops, prisoners, settlement ownership and prosperity well.
- **TAOM requirement:** none of them can spawn a troop into an open mission, dump TAOM's own
  subsystem state (momentum payload size, banner-bearer spawn verdicts, weight-deflated party
  limits), or reload a TAOM config without restarting the process.
- **Without this feature:** every one of those checks is a manual play session, and several are
  measured in hours.

**Commands that duplicate vanilla are not built.** Before adding one, check the vanilla list — the
following already exist and must not be reimplemented: `campaign.add_troops`,
`campaign.add_prisoner_to_party`, `campaign.give_settlement_to_player`,
`campaign.give_settlement_to_kingdom`, `campaign.set_prosperity_of_settlement`,
`campaign.set_hero_culture`, `campaign.set_clan_culture`, `campaign.print_tournaments`,
`campaign.print_strength_of_lord_parties`, `campaign.declare_war`, `campaign.declare_peace`,
`mission.kill_all_allies`, `mission.kill_n_allies`, `mission.flee_team`, `mission.flee_enemies`,
`mission.killAgent`.

**Check the SHIPPING build, not the dump root, when deciding whether vanilla already has a command.**
`E:\Decompiled_Bannerlord\` contains a dual `{_shipping_build,_editor_build}` pair, and a naive grep
across the tree returns editor-only commands that do not exist in the game TAOM ships against.
`mission.list_agent_ids` is exactly that trap — it appears in `_editor_build` and **zero** times in
`_shipping_build`, and it was wrongly listed here as already-existing until the Phase 0 review caught
it. An agent-listing diagnostic is therefore still an open gap TAOM may want to fill. Grep
`_shipping_build/TaleWorlds.MountAndBlade.cs` specifically.

## The engine contract

Read from the v1.4.7 dump, `Core/TaleWorlds.Library/TaleWorlds.Library/CommandLineFunctionality.cs`.

### Discovery

`CollectCommandLineFunctions` walks every loaded assembly that references `TaleWorlds.Library`, then
every type via `GetTypesSafe()`, then every method with
`BindingFlags.Static | Public | NonPublic`. For each method carrying
`[CommandLineFunctionality.CommandLineArgumentFunction(name, group)]` it:

1. skips the method if `ReturnType != typeof(string)` — silently, no throw;
2. builds the key `group + "." + name`;
3. **if that key is already present, skips it entirely** — `if (!AllFunctions.ContainsKey(text))`;
4. otherwise calls `Delegate.CreateDelegate(typeof(Func<List<string>, string>), method)` and adds it.

**No registration is needed anywhere.** The attribute *is* the registration — nothing goes in
`SubModule.cs`, `IoC.cs`, or the csproj.

### The two hazards this doc exists to prevent

**1. A wrongly-shaped method can take out more than the console.** Step 4 has no try/catch. A static
`string`-returning method whose parameter list is anything other than `(List<string>)` throws there.
That exception escapes into `ManagedExtensions.CollectCommandLineFunctions` — an `[EngineCallback]`
invoked from `TaleWorlds.Native.dll` through a bare `[MonoPInvokeCallback]` — and there is no managed
backstop anywhere in the decompiled tree (no `AppDomain.UnhandledException`, no
`FirstChanceException`). `Utilities.AddCommandLineFunction` only runs *after* the collect returns, so
even the commands that did bind never reach native autocomplete.

What the native side does with that exception could not be determined from the managed dump. We plan
for a startup crash rather than a degraded console, which is why the binding test is treated as
protecting game launch, not just convenience.

**2. Duplicate names are silent and non-deterministic.** Step 3 keeps whichever colliding method the
walk reaches first. Nothing is logged. Neither `AppDomain.GetAssemblies()`, `GetTypesSafe()`, nor
`Type.GetMethods()` specifies an order, so *which* one wins can change between runs. Cross-group
collision is harmless (`taom.add_gold_to_hero` and `campaign.add_gold_to_hero` coexist fine); it is
only within-group collision that bites.

### Dispatch

`native → Managed_CallCommandlineFunction → Managed.CallCommandlineFunction →
CommandLineFunctionality.CallFunction → the delegate`. There is no try/catch at any level, so a
command body that throws crosses the same native boundary as hazard 1. **A read-only command is not
a safe command** — these walk `Settlement.All`, `Mission.Agents` and similar, and TaleWorlds computed
getters routinely throw before your null check (`.claude/rules/adapters.md`).

Arguments arrive as a space-split `List<string>`.

## Authoring a command

### Shape

```csharp
[CommandLineFunctionality.CommandLineArgumentFunction("print_something", "taom")]
public static string PrintSomething(List<string> strings) =>
    TaomConsole.RunInCampaign(strings, Usage, args =>
    {
        // gate, help and the catch-all already ran. Just do the work.
        return "…";
    });
```

`public static string Name(List<string> strings)`, always. Never touch engine state from a static
field initializer — the class's `.cctor` runs during reflection, before any gate. (TOR_Core's command
class opens with a static field initialised from its ability factory; that pattern fails our tests,
which is why we do not copy it.)

### Gates — pick one

`CampaignCheats.CheckCheatUsage` requires `Campaign.Current != null`, which would lock mission
commands out of **custom battles** — TAOM's primary venue for testing creatures, mounts and
equipment. So [`DevConsoleGuard`](../../Main/Features/DevConsole/DevConsoleGuard.cs) offers three,
reached through [`TaomConsole`](../../Main/Features/DevConsole/TaomConsole.cs):

| Entry point | Requires | For |
|---|---|---|
| `TaomConsole.RunInCampaign` | campaign + cheat mode (vanilla's gate) | settlements, clans, heroes, the map |
| `TaomConsole.RunInMission` | cheat mode + an open mission, **no campaign** | agents, spawning, scenes — works in custom battles |
| `TaomConsole.RunAnywhere` | cheat mode only | config, registries, the Harmony patch table |

All three answer `"Campaign was not started."` at the main menu, which is what lets the binding test
assert a single value — but note they do not check the *same* thing to get there. `RunInCampaign`
delegates to vanilla's gate, which keys off `Campaign.Current`; the other two key off `Game.Current`.
Those two states coincide at the main menu and nowhere else is currently reachable, so the gates agree
in practice rather than by construction. A hypothetical live-Campaign-with-torn-down-Game state would
NRE inside vanilla's own gate (it dereferences `Game.Current.CheatMode` unguarded after its
`Campaign.Current` check) — caught by `TaomConsole`'s catch-all, which returns a `Command failed:`
string instead.

`RunInMission` is strictly tighter than vanilla's own `mission.*` cheats, which gate on nothing but
`!GameNetwork.IsSessionActive` — several do not even null-check `Mission.Current` before iterating
`Mission.Current.AllAgents`. TAOM's gate deliberately does **not** replicate the multiplayer check,
because TAOM has no multiplayer mode; if that ever changes, add it here.

### Argument parsing

Use [`DevConsoleArgs`](../../Main/Features/DevConsole/DevConsoleArgs.cs) — `TryParseCount` and
`TryParseAmount`. Two rules are encoded there so no command author has to remember them:

- **Invariant culture, always.** Current-culture parsing reads `"12.5"` as `125` on a comma-decimal
  locale.
- **Non-finite floats are rejected, not clamped.** `float.TryParse` accepts `"NaN"` and `"Infinity"`,
  and every subsequent comparison against a NaN returns `false`, so the value sails past whatever
  range check comes next. That bug class has shipped five times in TAOM
  (`.claude/rules/csharp-architecture.md`, "Engine-Float Decision Gates").

When you add a parser for an enum-ish argument, an unrecognised value must be an **error**, never a
silent fall-through to a default — a typo'd `"enemey"` quietly spawning allies is the
parsed-but-unresolvable trap. `DevConsoleArgs` holds only the parsers that have a caller today;
per-command parsers land with the command that needs them, not ahead of it.

For id resolution use `CampaignCheats.TryGetObject<T>`: it matches StringId then name across eight
case/space permutations and hands back a ready error string. Not the `taom-moduledata` MCP or
`tools/validate_*.py` — those are build-time XML tools, and a console command must resolve against
the live `CampaignObjectManager`.

### Naming

Enumerated across all 130 vanilla commands in the v1.4.7 dump: `print_` appears **9 times**, `dump_`
appears **zero times**.

| Rule | Value |
|---|---|
| Group | `taom`, always |
| Case | `^[a-z][a-z0-9_]*$` |
| Order | `<verb>_<object>[_of_<owner>\|_to_<target>]`, matching vanilla |
| Read-only verb | **`print_` by default.** `audit_` is the single admitted alternative, for a command that returns a verdict plus a computed replacement value rather than printing current state (`audit_settlement_entrances`). `dump_`, `list_`, `get_` stay banned — they are pure synonyms for `print_`, and admitting synonyms is how the convention drifts |
| Mutating verbs | `add_ set_ remove_ clear_ give_ toggle_ spawn_ damage_ requeue_` |
| `show_` / `hide_` | Reserved for genuine visibility toggles, as in `campaign.show_settlements` |
| Redundancy | Never put `taom` in the name; the group carries it |
| Mirroring vanilla | Encouraged — `taom.add_special_resources` deliberately echoes `campaign.add_gold_to_hero` |

`print_` also sorts adjacent to `campaign.print_*` in console autocomplete, which is a real
discoverability win.

`ConsoleCommandBindingTests` enforces the ban list literally — `dump_`, `list_`, `get_` — not the
`print_`-first rule, so any other new read-only verb compiles and passes the suite. It has to be
argued into the table above first; that argument is the only gate there is.

### Risk tiers — apply ceremony only where it is earned

Cheat mode is a per-install flag in `engine_config.txt`, not a per-command intent signal. A player who
enabled it to add gold has not consented to an irreversible mutation of persisted state.

| Tier | Definition | Safeguard |
|---|---|---|
| **A** | Read-only (`print_*`, `audit_*`) | Cheat gate only. Nothing else. |
| **B** | Reversible, clamped mutation | Cheat gate + an honest before→after echo |
| **C** | Mutates **persisted** state, not undoable from the console | Cheat gate + dry-run default + a literal positional `confirm` token + validate-before-lookup + an Entity State Matrix (`.claude/rules/csharp-architecture.md`) |

Known Tier C territory: settlement culture conversion (`ICultureConversionStore` persists and
re-applies on load), hero race change (`RacePersistenceService`; `CharacterObject.Race` accepts
arbitrary integers and `GetRaceNameFromId` coerces unknown ids to `"human"`), career set/switch, and
anything removing roster entries. `taom.add_special_resources` is explicitly **not** Tier C — it is
clamped to the XML cap, floored at 0, and reversible with a negative amount.

**Not built, deliberately:** a `trigger_fatal_crash` equivalent (TOR ships one). In a publicly
released mod it would generate crash reports indistinguishable from real ones and pollute
`Main/Features/CrashReport/`'s signal. TAOM already has MCM dev triggers for that
(`CrashReportSettings`, group "QA — Dev Triggers").

### Localization: raw English, by decision

Console output is **not** wrapped in `{=KEY}` and must not be swept into the localization pipeline.

Vanilla's own `CampaignCheats` strings are raw English consts (`"Please enter a number"`,
`"Campaign was not started."`, `"Cheat mode is disabled!"`) and TAOM commands return several of them
verbatim through the shared gate. Localizing ours would produce a mixed-language console — the
player's language for `taom.*` lines interleaved with English for every `campaign.*` line in the same
session. For an audience that has hand-edited `engine_config.txt` to get here, uniform English is
better. `docs/localization/TRANSLATOR_GUIDE.md` has no console surface and creating one would add a
pipeline stage and a validation gate for a developer tool.

**Caveat:** this covers TAOM's *own* console prose. When printing a settlement, hero or item name,
print the `TextObject` — that content is already translated, and stripping it would be a regression.

## Architecture

```
[CommandLineArgumentFunction] static method     <- thin entry point (ADR-002)
        |  delegates immediately to
   TaomConsole.RunIn*                           <- gate, help, catch-all
        |                       \
   DevConsoleGuard          DevConsoleArgs      <- gates            parsers (pure)
        |
   feature service / adapter                    <- all real logic (ADR-007)
```

`IoC.Resolve<T>()` inside a command body is the sanctioned service-locator exception: a static console
method has no constructor to inject through. Convert sealed TaleWorlds types to string ids **in the
entry point** so only primitives cross into services.

### Why a shared dispatch shell

Per `.claude/rules/simplicity-criterion.md`, stated so it stays re-evaluable:

- **Win:** converts an unguarded exception at a native reverse-P/Invoke boundary into a printed string
  for every command, and makes the cheat gate structurally unforgettable.
- **Cost:** one ~40-line static helper plus a lambda per command.
- **Verdict:** *improvement dominates complexity cost → keep, flag the trade-off.* It is also net
  line-negative, replacing a `private static string _errorType` field plus two guard lines per command.

**At one or two commands this helper would be a REJECT** — a tiny win for a new indirection. It earns
its place only at suite scale and because the downside it contains is unbounded. If the suite ever
shrinks back to a handful, inline it again.

## The launch gate

Whether the engine's discovery pass ever reaches the TAOM assembly **has never been proven**. The
call site is in `TaleWorlds.Native.dll` and is not decompilable
(`docs/reviews/rca-specialresources-cheat-2026-07-30.md` rates it `[Likely]`). It cannot be settled by
disassembly in a way that generalises either, because module load order is data-dependent. It can
only be answered at runtime.

**The field check:** at the main menu with no campaign loaded, open the console with
<kbd>Alt</kbd>+<kbd>~</kbd> and type `taom.add_special_resources`.

- `"Campaign was not started."` → discovery found it. Proven.
- `"Could not find the command"` → discovery never saw the TAOM assembly.

**The durable check:** [`DevConsoleDiscoveryAudit`](../../Main/Features/DevConsole/DevConsoleDiscoveryAudit.cs),
run from `SubModule.OnBeforeInitialModuleScreenSetAsRoot`. `HasFunctionForCommand` is public, so TAOM
asks the engine directly — for every `taom.*` command *and* for a vanilla control command
(`campaign.add_gold_to_hero`). The control is what makes a negative reading decisive:

| Control | All `taom.*` | Reading | Level |
|---|---|---|---|
| found | found | Discovery proven | Info |
| found | any missing | Discovery ran and **dropped** a command — duplicate name or partial abort | **Error** |
| missing | all missing | Collect pass has not run yet; inconclusive, will re-check | Warn |
| missing | any found | The control command was renamed by an engine bump | Warn |

Beyond the one-time proof, this is the only mechanism that surfaces a silent duplicate-name drop on a
player's machine, and it re-proves the assumption after every engine bump. It goes quiet once the
answer is conclusive and fails open in every path.

## Command reference

| Command | Tier | Gate | What it replaces |
|---|---|---|---|
| `taom.add_special_resources [amount]` | B | campaign | — (see [special-resources.md](special-resources.md)) |
| `taom.print_special_resources` | A | campaign | Read-only balance/cap/tier. **Not** `GrantAmount(…, 0f)` — that clamps and writes back |
| `taom.print_momentum [keys]` | A | campaign | ~50 in-game days of play to reach the 32 KB save-corruption threshold |
| `taom.print_party_size` | A | campaign | The #337 weight-deflation chain, invisible in-game. Distinguishes a light party from a degenerate base limit |
| `taom.print_town_economy [town]` | A | campaign | A 4–8 in-game-day observation for #317, plus the vanilla side-by-side that answers "is the buff doing anything" |
| `taom.print_town_ledger [town]` | A | campaign | Where the town's gold actually went, by day and by flow. **No engine code logs a gold movement at all** — the alternative is inferring the drain from a balance that changes once a day. See [economy-diagnostics.md](economy-diagnostics.md) |
| `taom.print_caravans [settlement]` | A | campaign | Which engine gate is holding each parked caravan. Every one of them is a silent early-return, and four of them have different fixes — the gate histogram is the money output |
| `taom.print_patches [filter]` | A | cheat mode | Grepping `taom_debug` for "did this category apply?" |
| `taom.print_races` | A | cheat mode | — (registry + the hero's race, validated before lookup) |
| `taom.print_battle_scene` | A | campaign | Which battle terrain a fight here loads. **Zero candidates is the money output** — the stale-scene-ref class an engine bump introduces silently |
| `taom.audit_settlement_entrances` | A | campaign | Nothing — an unreachable settlement entrance never crashes and never logs, it only makes AI parties fail their path query every tick. Three were caught in the field solely because the testers had written their own pathfinding instrumentation. See below |
| `taom.print_mission_scene` | A | mission | Scene name + player/camera position |
| `taom.print_hud_layout [maxNodes]` | A | cheat mode | Widget-tree dump of the top screen with real on-screen rectangles (`Logs/taom_hud_layout.log`, bounded, collapsed-tree warning). Ported from the career-UX reference module's diagnostic, whose dumps found three UI bugs code reading did not (#384) |
| `taom.print_agent_info [name\|*]` | A | mission | Race, monster, action set, skeleton, mount/rider, spawn equipment. Pairs with `spawn_troops` |
| `taom.spawn_troops <id> <n> [enemy\|ally]` | B | mission | Composing a specific fight. **Vanilla ships no mission spawn at all** |
| `taom.damage_agent <amount> [name]` | B | mission | HP attrition and death thresholds. **Cannot test shrug-off / unstoppable** — a synthetic blow bypasses the hit path those models run on |
| `taom.requeue_settlement <settlement>` | B | campaign | A siege plus a day's wait to re-check #333. Refuses settlements with no existing record, so it verifies a timer rather than arming one |

**Still unbuilt from Phase 1:** `print_town_mercenaries`, `print_banner_bearers`, `print_wotr`.
**Phase 2 remaining:** `convert_settlement`. **Phase 3:** untouched.

> This table has gone stale twice — both times because it was written alongside the code rather than
> re-read against it afterwards. When you add a command, edit this table in the same commit.

### `taom.audit_settlement_entrances`

The one command with no feature doc to link out to — no single feature owns settlement navmesh — so it
is documented here.

Each settlement's entrance (`GatePosition` for towns and castles, `Position` otherwise) is resolved to
a `PathFaceRecord` through `IMapScene.GetFaceIndex`. **`IsValid()` returns true for every face the
field report named**, so an off-mesh check finds nothing: those faces sit on navmesh *islands* the rest
of the map cannot path to. `FaceIslandIndex` is the engine's own connected-component id — two faces
with different island indices have no path between them at any cost — so the main landmass is derived
as the island index the most settlements agree on rather than hardcoded, which keeps the audit correct
across map edits and engine bumps. Every disagreeing settlement is then probed with
`GetAccessiblePointNearPosition` at radii 1, 2, 4, 8, 16, 32, and the first hit on the main island is
printed as a replacement coordinate the engine computed rather than a value to guess at.

**The corrected coordinates do not exist yet.** The auditor ships; producing them takes one in-game
campaign run, and applying them is a separate edit against the LIVE
`TAOM_Map/ModuleData/settlements.xml` — the repo's `Main/_Module/ModuleData/settlements.xml` is a stale
shadow, which the command's own output says. The three destinations reported as wedging AI parties are
`town_MM2` (the only one of the three with a gate position), `hideout_desert_7` and
`castle_village_MM1_2`; all three ids resolve in the live map file.

## Files

| File | Role |
|---|---|
| `Main/Features/DevConsole/TaomConsole.cs` | Dispatch shell: gate → help → body inside a catch-all |
| `Main/Features/DevConsole/DevConsoleGuard.cs` | The three cheat gates |
| `Main/Features/DevConsole/DevConsoleArgs.cs` | Pure argument parsers (invariant culture, finite floats) |
| `Main/Features/DevConsole/DevConsoleDiscoveryAudit.cs` | Startup self-audit with the vanilla control |
| `Main/Features/DevConsole/HarmonyPatchInspector.cs` | Reflection walk: declared categories vs what Harmony applied |
| `Main/Features/DevConsole/PatchReportFormatter.cs` | Pure renderer for `print_patches` |
| `Main/Features/DevConsole/Cheats/DiagnosticCheats.cs` | `print_patches`, `print_races` |
| `Main/Features/DevConsole/Cheats/SettlementEntranceCheats.cs` | `audit_settlement_entrances` — navmesh-island check over every settlement entrance. DevConsole-owned because no feature owns settlement navmesh |
| `Main/Features/<X>/Cheats/` | Feature-owned commands (momentum, party size, town economy, special resources) |
| `Main/SubModule.cs` | Calls the audit from `OnBeforeInitialModuleScreenSetAsRoot`, fail-open |

## Tests

| File | Covers |
|---|---|
| `TAOM.Tests/Features/DevConsole/ConsoleCommandBindingTests.cs` | Assembly-wide: delegate shape, `taom` group, **unique names**, **name convention**, **invoke-with-no-campaign**. Auto-covers every new command — no per-command wiring |
| `TAOM.Tests/Features/DevConsole/DevConsoleArgsTests.cs` | Every parser branch, including `NaN`/`Infinity` rejection, invariant-culture decimals, and float-spellings refused rather than truncated |
| `TAOM.Tests/Features/DevConsole/DevConsoleDiscoveryAuditTests.cs` | All four audit verdicts plus the zero-commands case |

The binding tests live under `Features/DevConsole/` rather than beside any one command because they
are a whole-assembly invariant. All five guards were verified RED by injecting their defects before
acceptance (`docs/reviews/lessons/testing-qa.md`: a guard never seen failing is not a guard).

**Deliberately not tested:** an IL scan asserting every command routes through `TaomConsole`. The
invoke-with-no-campaign test gets ~90% of that enforcement for ~5% of the machinery.

**Not unit-testable, verify in-game:** the gates themselves (need a live `Game`), the audit's
engine-querying half (the *interpretation* is tested), and `TaomConsole`'s help branch — in a test
host the gate always fails first, so the `usage ?? string.Empty` guard on that path is defensive code
with no reachable test. Adding a seam purely to reach it would be ceremony; it is one line.

`audit_settlement_entrances` has **no dedicated test**. Unlike `print_patches` (`PatchReportFormatter`)
and `print_agent_info` (`MissionReportFormatter`), its island derivation and output formatting are
inline in the cheat class with no pure formatter extracted, so the assembly-wide binding tests are its
only coverage.

## Changelog

- **2026-08-03** — Added `taom.audit_settlement_entrances` (Tier A, campaign gate) after field testers
  reported three settlement entrances wedging AI pathfinding. Not part of any phase plan. Admitted
  `audit_` as the second read-only verb, in the naming table and the Tier A definition; the binding
  test's ban list already permitted it.
- **2026-07-31** (#369) — Phase 0: shared contract (`TaomConsole`, `DevConsoleGuard`, `DevConsoleArgs`),
  startup discovery audit, hardened + relocated binding tests, `add_special_resources` migrated onto
  the shell. Naming convention, risk tiers and the localization exemption settled. Reviewed by a
  5-agent `/deep-review`; RCA at `docs/reviews/rca-devconsole-phase0-2026-07-31.md`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/special-resources.md](./special-resources.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/taom-map-settlement-naming.md](../reference/taom-map-settlement-naming.md)

<!-- backlinks-end -->
