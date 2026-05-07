OpenAI Codex v0.128.0 (research preview)
--------
workdir: C:\Users\mikew\source\repos\TAOM
model: gpt-5.5
provider: openai
approval: never
sandbox: workspace-write [workdir, /tmp, C:\Users\mikew\.codex\memories]
reasoning effort: xhigh
reasoning summaries: none
session id: 019dff89-ae7a-75e2-b548-c00ca9aab65b
--------
user
Review the staged, unstaged, and untracked C# / XML / JSON / Markdown changes in this repository against the rules in AGENTS.md and the per-feature rules in `.claude/rules/`. The change under review is the port of LOTRAOM 1.2.12's `StartingEquipmentGold` feature to TAOM 1.3.15: configurable per-culture player starting funds at character-creation finalize, plus persistence of the youth-option's equipment roster onto the player hero.

# Context (do NOT trust me — verify everything against installed DLLs)

- Target Bannerlord version: 1.3.15 (NOT 1.4). The `E:\Decompiled_Bannerlord\` folder is v1.4 and the wrong version. Verify all TaleWorlds API signatures via `ilspycmd` against installed DLLs at `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\`.
- LOTRAOM 1.2.12 source pattern is at `C:\Users\mikew\source\repos\LOTRAOM\Main\Features\StartingEquipmentGold\`. That's a v1.2 reference — do NOT trust its API signatures for v1.3.15.
- Plan file: `C:\Users\mikew\.claude\plans\please-investigate-this-that-lovely-pine.md`.
- 1340/1340 project tests pass. Session-targeted tests: 83/83 green.
- A 5-agent Claude `/deep-review` already ran. It surfaced four fixes, all applied:
  1. Added `<Culture id="empire" playerGold="4000" .../>` to `startup_resources_config.xml`.
  2. Changed `taom_youth_sturgia_1` `title_type` from `"retainer"` to `"guard"` (no vanilla `sturgia_retainer` roster exists).
  3. Routed `CareerMenuService.cs:227` through the new `PlayerEquipmentRosterIds.Build` helper.
  4. Added a `Campaign.Current.DeadBattleEquipment` reference-equality guard in `PlayerEquipmentAdapter.ApplyRosterToPlayer`.
- One pre-existing tech-debt finding (NOT this session): `CharacterCreationContentService.AssignCareer` uses `IoC.Resolve<>` at lines ~218 and ~235. That predates this session — flagged for follow-up, not blocking this commit.

# What the changeset adds

NEW source:
- `Main/Adapters/IPlayerEquipmentAdapter.cs` — interface returning `PlayerEquipmentApplyResult` enum (Success / RosterNotFound / NoSuitableEquipment / HeroNotFound)
- `Main/Adapters/PlayerEquipmentAdapter.cs` — wraps `MBObjectManager.GetObject<MBEquipmentRoster>`, filters `AllEquipments` by `IsBattle`/`IsCivilian`, applies via `Hero.BattleEquipment.FillFrom` / `Hero.CivilianEquipment.FillFrom`. Has the dead-equipment guard.
- `Main/Features/StartupResources/IPlayerStartupGoldService.cs` + impl — looks up `PlayerGold` from config, calls existing `IGoldGiftAdapter.GiveGoldToHero`. Logs warnings on null/empty/missing.
- `Main/Features/CharacterCreation/IPlayerEquipmentService.cs` + impl — builds roster ID via shared helper, dispatches to adapter, switches over the 4 result enum values.
- `Main/Features/CharacterCreation/PlayerEquipmentRosterIds.cs` — shared helper `Build(cultureId, titleType, isFemale)` producing `player_char_creation_{culture}_{titleType}_{m|f}`.

NEW tests (mirror source structure):
- `TAOM.Tests/Features/StartupResources/PlayerStartupGoldServiceTests.cs` — 8 cases
- `TAOM.Tests/Features/CharacterCreation/PlayerEquipmentServiceTests.cs` — 9 cases

MODIFIED:
- `Main/Features/StartupResources/Config/StartupResourcesConfig.cs` — added `int PlayerGold` to `CultureResourceEntry`.
- `Main/Features/StartupResources/StartupResourcesConfigProvider.cs` — added `ParsePlayerGold(string raw, string cultureId)` private method, range `[0, 10_000_000]`. Rejects negative, over-cap, non-numeric. Defaults to 0 when attribute missing (no warning).
- `Main/Features/StartupResources/StartupResourcesIoC.cs` — registered `IPlayerStartupGoldService` as singleton.
- `Main/Features/CharacterCreation/CharacterCreationContentService.cs` — added two constructor params (`IPlayerStartupGoldService`, `IPlayerEquipmentService`). New private method `GrantPlayerStartupResources(string cultureId, CharacterCreationManager manager)` invoked from `OnCharacterCreationFinalize` after `AssignCareer`. Each service call wrapped in try/catch with error log so one failure does not block the other.
- `Main/Features/CharacterCreation/CharacterCreationIoC.cs` — registered `IPlayerEquipmentAdapter` and `IPlayerEquipmentService` as singletons.
- `Main/Features/CharacterCreation/NarrativeMenuBuilder.cs` — `BuildEquipmentRosterId` now delegates to `PlayerEquipmentRosterIds.Build` (was inline format string).
- `Main/Features/CharacterCreation/CareerMenuService.cs:227` — same delegation (deep-review fix).
- `TAOM.Tests/Features/StartupResources/StartupResourcesConfigProviderTests.cs` — 5 new validation tests.
- `TAOM.Tests/Features/CharacterCreation/CharacterCreationContentServiceTests.cs` — constructor signature update.

DATA:
- `Main/_Module/ModuleData/startup_resources/startup_resources_config.xml` — added `playerGold` attribute on 16 cultures (Elven 8000–10000, Dwarf 7500, Dark factions 6000, Human Good 5000, Tribal/Eastern 4000 including the deep-review-added empire/Dunland row).
- `Main/_Module/ModuleData/charactercreation/youth_menu.json` — `taom_youth_sturgia_1` `title_type` retainer→guard.

DOCS:
- `CHANGELOG.md` — comprehensive entry under 2026-05-06.
- `docs/features/startup-resources.md` — extended with new attribute, new services, new "How to" section.

# Adversarial review goals — find what Claude missed

Hit each of these explicitly:

1. **Adapter pattern violations (ADR-007):** Sealed TaleWorlds types crossing service boundaries. Specifically check `PlayerStartupGoldService`, `PlayerEquipmentService`, and `CharacterCreationContentService.GrantPlayerStartupResources`. The boundary class can touch sealed types; services cannot.

2. **Thin entry-point violations (ADR-002):** Any patch / GameModel / behavior over 150 lines OR with inline if/foreach/switch business logic.

3. **v1.3.15 API correctness — independently re-verify EVERY TaleWorlds call** in `PlayerEquipmentAdapter.cs`, `PlayerStartupGoldService.cs` (only via `IGoldGiftAdapter`), and `CharacterCreationContentService.GrantPlayerStartupResources`. Use `ilspycmd` against installed DLLs. Specifically:
   - `MBObjectManager.Instance?.GetObject<MBEquipmentRoster>(string)` — generic type constraint, return type
   - `MBEquipmentRoster.AllEquipments` return type and null behavior
   - `Equipment.IsBattle` / `Equipment.IsCivilian` — bool computed properties
   - `Equipment.FillFrom(Equipment, bool useSourceEquipmentType = true)` — verify default arg and mutation behavior
   - `Hero.FindFirst(Func<Hero, bool>)` — verify exact signature
   - `Hero.BattleEquipment` and `Hero.CivilianEquipment` — getters returning mutable Equipment, with the `Campaign.Current.DeadBattleEquipment` fallback when `_battleEquipment` is null
   - `CharacterCreationContent.SelectedTitleType { get; set; }` — exists and is a plain string
   - `CharacterCreationContent.SelectedCulture { get; private set; }` — read-only from outside

4. **Test coverage gaps:** Services and branches without test coverage. Especially: does `PlayerEquipmentService` have a test for the case where the title_type contains characters that shouldn't appear in a roster ID (e.g., spaces, special chars)? Does `PlayerStartupGoldService` have tests for the lowest culture-ID match precedence when duplicate culture IDs exist?

5. **Config provider validation completeness:** `ParsePlayerGold` handles negative, over-cap, non-numeric. Does it handle: empty string after trim, all-whitespace, scientific notation (`5e3`), leading zeros (`007500`), thousands separators (`5,000`), unicode digits, integer overflow at the parse boundary, `int.MinValue`/`int.MaxValue` literals?

6. **Roster ID convention coupling:** Three callers — `NarrativeMenuBuilder`, `CareerMenuService`, `PlayerEquipmentService`. Grep the entire repository for any other inline `player_char_creation_{` or `"player_char_creation_"` string. Find any fourth caller that still bypasses `PlayerEquipmentRosterIds.Build`.

7. **CC finalize ordering hazards:** In `CharacterCreationContentService.OnCharacterCreationFinalize`:
   - `selectedCulture` captured at top before mutation
   - `Hero.MainHero.Culture` force-set
   - `TeleportToStartingSettlement` → `SetPlayerRace` → `AssignCareer` → `GrantPlayerStartupResources`
   - Any of those four methods able to mutate `manager.CharacterCreationContent.SelectedTitleType`? Any able to null out `Hero.MainHero`?
   - What happens if `CharacterCreationManager.CharacterCreationContent` is null at finalize?

8. **Lifecycle / state matrix:** `OnCharacterCreationFinalize` runs at... when exactly? New Game flow only? In-campaign Test? Save-load? If a player loads a save where finalize already ran, can my new services run again and double-grant gold or re-equip?

9. **Enum exhaustiveness:** `PlayerEquipmentApplyResult` has 4 values today. The switch in `PlayerEquipmentService.ApplyPlayerStartingEquipment` covers all 4 but has no `default` case. If a 5th value ships without a switch update, the player gets silent no-op + no log. Add a `default` arm or a compile-time enforcement?

10. **DeadBattleEquipment guard correctness:** The deep-review added `hero.BattleEquipment != Campaign.Current.DeadBattleEquipment`. Is `DeadBattleEquipment` truly a process-wide singleton in v1.3.15? Verify via decompiled source. Is reference equality the right check? Could `Campaign.Current` be null at CC finalize (campaign not yet started — verify timing)?

11. **youth_menu.json sturgia_1 title_type semantic shift:** Does anything else read `taom_youth_sturgia_1` and depend on `title_type == "retainer"`? Search localization keys, save-game upgrade paths, conditional logic in `CareerMenuService`, `NarrativeDataProvider`, etc. The change from retainer→guard might silently flip behavior elsewhere.

12. **Per-culture tier balance:** The seeded values are tunable but Mordor at 6000 + a high-yield raid economy = potential snowball. Sanity-check the values against TAOM's RaidModel + ClanFinanceModel modifiers for those cultures. Note any culture whose 4000–10000 starting gold combined with culture-specific feats produces a runaway start.

13. **XML schema robustness:** The `playerGold` attribute is additive. What if a user has an old config file from before this session and adds it back via mod manager? Defaults to 0 silently. Is that the right UX? Should there be a migration warning when an entry has `gold` but no `playerGold`?

14. **Concurrency / re-entry:** `StartupResourcesConfigProvider` uses `_cached` field with `_cached != null` guard. Single-threaded at game start so safe today. But if anything in the new pipeline re-loads on hot-reload or save-load, the cache could be stale. Verify singleton lifetime + reload semantics.

# Output format

For each finding:
```
[SEVERITY] file.cs:line — finding — remediation
```

Severities:
- CRITICAL: ships and breaks the game (crash, save corruption, silent data loss)
- HIGH: ships and breaks user-visible behavior (wrong equipment, wrong gold, silent no-op)
- MEDIUM: code-quality issue that will surface as a bug under specific conditions
- LOW: style, missing comment, defensive code suggestion

End with a summary: `CRITICAL: N | HIGH: N | MEDIUM: N | LOW: N | VERDICT: CLEAN / ISSUES FOUND`.

If you find any disagreement with the prior Claude `/deep-review` (e.g., a finding it accepted but you think is wrong, or a finding it dismissed but you think is real), flag the disagreement explicitly with the agent name and your reasoning. Disagreements are valuable signal.
ERROR: The process "13224" not found.
ERROR: The process "78560" not found.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git status --short' in C:\Users\mikew\source\repos\TAOM
 succeeded in 527ms:
 M CHANGELOG.md
 M Main/Features/CharacterCreation/CareerMenuService.cs
 M Main/Features/CharacterCreation/CharacterCreationContentService.cs
 M Main/Features/CharacterCreation/CharacterCreationIoC.cs
 M Main/Features/CharacterCreation/NarrativeMenuBuilder.cs
 M Main/Features/StartupResources/Config/StartupResourcesConfig.cs
 M Main/Features/StartupResources/StartupResourcesConfigProvider.cs
 M Main/Features/StartupResources/StartupResourcesIoC.cs
 M Main/Features/TaomSettings.cs
 M Main/IoC.cs
 M Main/SubModule.cs
 M Main/_Module/ModuleData/charactercreation/youth_menu.json
 M Main/_Module/ModuleData/startup_resources/startup_resources_config.xml
 M TAOM.Tests/Features/CharacterCreation/CharacterCreationContentServiceTests.cs
 M TAOM.Tests/Features/StartupResources/StartupResourcesConfigProviderTests.cs
 M docs/features/startup-resources.md
?? Main/Adapters/IPartyMountInventoryAdapter.cs
?? Main/Adapters/IPlayerEquipmentAdapter.cs
?? Main/Adapters/IPlayerMountAdapter.cs
?? Main/Adapters/PartyMountInventoryAdapter.cs
?? Main/Adapters/PlayerEquipmentAdapter.cs
?? Main/Adapters/PlayerMountAdapter.cs
?? Main/Features/CharacterCreation/IPlayerEquipmentService.cs
?? Main/Features/CharacterCreation/PlayerEquipmentRosterIds.cs
?? Main/Features/CharacterCreation/PlayerEquipmentService.cs
?? Main/Features/Messengers/
?? Main/Features/SiegeDismount/
?? Main/Features/StartupResources/IPlayerStartupGoldService.cs
?? Main/Features/StartupResources/PlayerStartupGoldService.cs
?? TAOM.Tests/Features/CharacterCreation/PlayerEquipmentServiceTests.cs
?? TAOM.Tests/Features/Messengers/
?? TAOM.Tests/Features/SiegeDismount/
?? TAOM.Tests/Features/StartupResources/PlayerStartupGoldServiceTests.cs
?? docs/features/siege-dismount.md
?? docs/reviews/codex-prompt-player-startup.md
?? docs/reviews/codex-result-player-startup.md

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Recurse -File .claude\\rules | Select-Object -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 448ms:
C:\Users\mikew\source\repos\TAOM\.claude\rules\adapters.md
C:\Users\mikew\source\repos\TAOM\.claude\rules\csharp-architecture.md
C:\Users\mikew\source\repos\TAOM\.claude\rules\csharp-patterns.md
C:\Users\mikew\source\repos\TAOM\.claude\rules\environment-failures.md
C:\Users\mikew\source\repos\TAOM\.claude\rules\external-skill-ports.md
C:\Users\mikew\source\repos\TAOM\.claude\rules\gamemodels.md
C:\Users\mikew\source\repos\TAOM\.claude\rules\gui-ui.md
C:\Users\mikew\source\repos\TAOM\.claude\rules\harmony-patches.md
C:\Users\mikew\source\repos\TAOM\.claude\rules\harness-facts.md
C:\Users\mikew\source\repos\TAOM\.claude\rules\tests.md
C:\Users\mikew\source\repos\TAOM\.claude\rules\troops.md
C:\Users\mikew\source\repos\TAOM\.claude\rules\xml-data.md
C:\Users\mikew\source\repos\TAOM\.claude\rules\xslt.md

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content .claude\\rules\\*.md" in C:\Users\mikew\source\repos\TAOM
 succeeded in 481ms:
---
paths:
  - "Main/Adapters/**"
  - "Main/**/I*Adapter.cs"
  - "Main/**/*Adapter.cs"
---

# Adapter Pattern Rules (ADR-007)

## Core Principle
Services NEVER accept sealed TaleWorlds types directly. Always wrap with adapter interfaces.

## Creating New Adapters
1. **Research first** — Decompile the TaleWorlds class with `ilspycmd` before creating the adapter interface
2. **Interface in `Main/Adapters/`** — `I{TypeName}Adapter.cs` with only the properties/methods the feature needs
3. **Implementation in `Main/Adapters/`** — `{TypeName}Adapter.cs` wrapping the sealed type
4. **Recursive wrapping** — If the sealed type exposes other sealed types, wrap those too
5. **Defensive validity** — Check for dead agents, null references in computed properties

## Property Guidelines
- Identify read-only (get-only) vs read-write properties from decompiled source
- Use null-conditional operators (`?.`) for computed properties accessing nested objects
- Cache expensive property lookups where appropriate

## Testing
- Adapters themselves are thin wrappers — test coverage via service tests that mock the adapter interface
- Use `NSubstitute.Substitute.For<IXxxAdapter>()` in tests
---
paths:
  - "Main/**/*.cs"
  - "TAOM.Tests/**/*.cs"
---

# TAOM Architecture Quick Reference

Full guide: `docs/ai-includes/architecture.md`

## Layer Stack

```
HarmonyPatch / GameModel / CampaignBehavior   ← THIN (<150 lines, no logic)
                    │ delegates to
              Service (IXxxService)            ← ALL business logic here
                    │ uses
              Adapter (IXxxAdapter)            ← wraps sealed TaleWorlds types
                    │ wraps
         TaleWorlds Engine (Hero, Agent…)      ← sealed, never cross boundary
```

## Non-Negotiable Rules

| Rule | Detail |
|------|--------|
| Entry points <150 lines | ADR-002: delegate immediately to service |
| No sealed types in services | ADR-007: `IHeroAdapter` not `Hero` |
| Constructor injection only | No service locator in services |
| Convert at boundary | Adapt sealed types in the entry point, not deep in services |
| `?.` for computed properties | TaleWorlds getters crash before your null check — see `adapters.md` |

## IoC Lifetimes

| Lifetime | Use For |
|----------|---------|
| `Reuse.Singleton` | Services, engines, caches |
| `Reuse.Transient` | Hooks, stateless helpers |

## Test Coverage Requirements (ADR-008)

| Component | Required | Notes |
|-----------|----------|-------|
| Services | 100% | Must be mockable via constructor injection |
| Engines | 100% | Pure functions — easy to test |
| Hooks | 80%+ | Use `NSubstitute` mocks for adapters |
| Entry Points | Not required | Harmony/GameModel — test via game |

## Entity State Matrix (MANDATORY for OnGameLoaded behaviors)

Any `CampaignBehaviorBase` that **mutates Hero/Settlement/Clan state on load** must enumerate all possible entity states before writing the mutation code. Build a state matrix:

| State | Key Properties | Should mutate? |
|-------|---------------|----------------|
| (each possible state) | (property values) | Yes/No + why |

**Why:** Review #23 found a HIGH bug where `EnsureCompanionsPlaced()` teleported recruited companions out of the player's party on load because the "skip if already placed" check didn't account for traveling-with-party state. The state matrix would have caught this at design time.

**Rule:** If your OnGameLoaded handler calls `ChangeState`, `EnterSettlementAction`, `SetHeroRace`, or any other state-mutating action on a Hero, enumerate:
- Unrecruited / idle in settlement
- Recruited / in player party (traveling on map)
- Recruited / in player party (visiting settlement)
- Dead / disabled
- Prisoner
- Fugitive

Skip any state where mutation would corrupt the entity.

**Idempotent vs destructive:** Before copying a behavior pattern from another feature, ask: "Is this operation idempotent?" Injecting a banner color twice is harmless. Moving a Hero between locations is destructive. Destructive load-path operations need stricter guards than their new-game counterparts.

## Config Providers MUST Validate (MANDATORY for user-editable JSON/XML)

Any provider that loads `Main/_Module/ModuleData/` JSON or XML the player is expected to edit (retuning knobs, enable/disable flags, tunable thresholds) must validate semantic constraints after deserialization, not just syntax. Parse success is NOT validation success.

**Rule:** If the feature doc tells the user "edit this file to retune," the provider's `LoadConfig` (or equivalent) must:
1. Range-check every numeric field against its engine-valid bounds
2. Enforce ordering invariants between related fields (e.g., warning-threshold ≥ trigger-threshold)
3. Reject sign flips on fields whose meaning is directional (penalties must be ≤ 0; bonuses must be ≥ 0)
4. Log a warning and fall back to the compiled default for any field that fails — never silently apply a bad value
5. Emit a summary warning when any reversion occurred so the user knows to look at prior warnings

**Why:** Review #25 (RevoltTuning) found a HIGH bug where the provider logged "Loaded" success for any parseable file. A plausible user edit like a sign-flipped penalty `1.0` (should be `-1.0`) would silently flip the feature from "soften revolts" to "accelerate revolts" with no warning. Syntax-error tests (missing file, malformed JSON) did not cover this class of failure.

**Test requirement:** Tests must cover semantically-invalid-but-parseable values for every validated field — not just missing-file and malformed-JSON cases. One test per validation rule.

**Doc requirement:** When documenting "edit this file to retune," state the reload scope explicitly. `Reuse.Singleton` providers (the TAOM default) cache for the entire Bannerlord process — changes require a full application restart, not a new campaign or save-load. Never claim "next game load" without cross-checking the DryIoc lifetime.

## Lookup Functions With Fallbacks: Validate Before Lookup (MANDATORY)

When a lookup function MAY return a "default" or "fallback" value for invalid input (with a warning log, sentinel value, or coerced default), the caller MUST validate the input's validity BEFORE the lookup whenever the result is used as a comparison key in a security/correctness decision. The fallback exists for *logging-and-survival*, NOT for *acceptance*.

**The trap:** the fallback masks invalid input as a "valid-looking" value that happens to match the allow-list, causing silent acceptance of state the caller would have rejected if it had known the input was invalid.

**The rule:** if a lookup function can return a fallback, treat that lookup as "best-effort name resolution for diagnostic output" and add an explicit validity gate before any decision logic depends on the result.

```csharp
// ❌ WRONG — invalid IDs silently coerced to "human" sneak past allow-list when culture allows "human"
var raceName = _raceManager.GetRaceNameFromId(faceGenRaceId);  // returns "human" for unknown IDs
bool allowed = cultureData.Races.Any(r => r == raceName);
if (allowed) {
    PreserveValue(faceGenRaceId);  // ← invalid integer preserved
}

// ✅ RIGHT — validate the input BEFORE the lookup, treat invalid as "not allowed"
bool valid = _raceManager.IsValidRaceId(faceGenRaceId);
var raceName = valid ? _raceManager.GetRaceNameFromId(faceGenRaceId) : null;
bool allowed = valid && cultureData.Races.Any(r => r == raceName);
```

**Why this rule exists:** Codex Review #33 (CharacterCreation race-filter, 2026-05-06). `RaceManager.GetRaceNameFromId` (RaceManager.cs:126-131) returns `"human"` as fallback for unknown IDs. `SetPlayerRace` accepted that fallback name, checked it against the culture's allow-list, and for cultures that allow `human` (Mordor, vanilla cultures, Isengard, Gundabad, Dol Guldur — i.e., most cultures) preserved the original junk integer. `Hero.CharacterObject.Race` accepts arbitrary integers; downstream engine calls would silently receive a corrupt race ID for a Mordor save.

**Applies to:** any lookup function whose XML doc, log line, or implementation says "defaults to X for unknown input" (`GetRaceNameFromId`, `GetCultureData` returning a default culture, `GetItemFromId` returning a default item, `MBObjectManager.GetObject<T>` for missing IDs, etc.). When in doubt, read the function body — if it logs a warning and returns a value, that value is fallback, not validation.

**How to apply:** every `GetXxxFromId` / `LookupXxx` style function should be paired with an `IsValidXxxId` / `ContainsXxx` validator on the same interface. If the validator doesn't exist, the lookup function is effectively unsafe for security decisions and the caller must add validation by some other means (e.g., comparing the returned name against a sentinel default).

**Test requirement:** when fixing a finding of this class, add a regression test where the lookup returns the fallback value and assert the caller rejects the input. Example: `SetPlayerRace_InvalidFaceGenRaceId_DoesNotPreserve_FallsBackToCultureDefault` ([CharacterCreationContentServiceTests.cs](../../TAOM.Tests/Features/CharacterCreation/CharacterCreationContentServiceTests.cs)).

**Sibling rule:** see "Config Providers MUST Validate" above for the input-validation rule at the LOADER side; this rule is the input-validation rule at the CONSUMER side. Both are needed because the loader's validation may be downstream of mid-process state mutation (e.g., a save-load that brought in junk race IDs from a prior mod version).

## File Layout

```
Main/Features/MyFeature/
├── IMyFeatureService.cs
├── MyFeatureService.cs
├── MyFeatureIoC.cs          ← Reuse.Singleton registrations
├── Models/
│   └── TaomMyModel.cs       ← GameModel override (if needed)
└── Hooks/
    └── MyPatch.cs           ← Harmony patch (if needed)
Main/Adapters/
├── IMyTypeAdapter.cs
└── MyTypeAdapter.cs
TAOM.Tests/Features/MyFeature/
└── MyFeatureServiceTests.cs
```

## Stale-file re-read

Long sessions edit many files. Cached `Read` content drifts: a teammate-agent may have re-written the same file, a hook or skill may have run `dotnet format`, the user may have edited via the IDE. Editing against stale content produces opaque "no match" failures that look like permission/conflict bugs.

**Rule:** Before editing any C# file you have not Read in the last ~10 tool calls of the current turn, re-Read it.

- Hard signal to re-Read: another agent ran in this turn; `git status` shows changes you didn't make; the Edit tool returns a "string not found" error.
- Soft signal to re-Read: you're about to make >1 edit to the same file, the file is in a hot area (Main/Adapters, GameModels), or it's been more than ~5 minutes wall-clock since you last looked.

The re-Read costs nothing. The Edit failure plus diagnosis costs minutes.
---
paths:
  - "Main/**/*.cs"
  - "TAOM.Tests/**/*.cs"
---

# TAOM C# Design Patterns

Quick reference for the three core patterns. Full details: `docs/ai-includes/patterns.md`

## 1. Hook Pattern (Harmony → Hook Interface → Service)

```
HarmonyPatch (thin)
    └── IOnXxx hook interface
            └── XxxHook implementation
                    └── IXxxService (business logic)
```

- Harmony patch resolves `IOnXxx` hooks via `IoC.ResolveAll<IOnXxx>()`, iterates, delegates
- Hook implementation builds context, calls service
- Service contains all logic — uses adapters, fully testable

```csharp
// Patch — thin, no logic
[HarmonyPatch(typeof(AgentApplyDamageModel), "CalculateDamage")]
public class AgentApplyDamageModel_CalculateDamage_Patch
{
    static void Postfix(ref float __result, Agent attacker, Agent victim)
    {
        foreach (var hook in IoC.ResolveAll<IOnCalculateDamage>())
            hook.OnCalculateDamage(ref __result, attacker, victim);
    }
}
```

## 2. Strategy Pattern

For algorithm families with per-culture or per-faction variants:

```csharp
public interface ICultureStrategy
{
    string CultureId { get; }
    float Calculate(IContextAdapter context);
}
// One class per culture, registered as a collection:
container.RegisterMany<ICultureStrategy>(implementations, Reuse.Singleton);
// Service resolves all and dispatches by CultureId
```

## 3. GameModel Override Pattern

```csharp
public class TaomFooModel : DefaultFooModel
{
    private readonly IFooService _service;
    public TaomFooModel(IFooService service) => _service = service;

    public override float Calculate(SealedType param)
    {
        var adapter = IoC.Resolve<IAdapterFactory>().GetAdapter(param);
        return _service.Calculate(adapter) ?? base.Calculate(param);
    }
}
```

See `.claude/rules/gamemodels.md` for full GameModel rules.

## Transpiler Note

TAOM uses manual `List<CodeInstruction>` iteration. Harmony 2.4.2 (Bannerlord 1.3) has an expanded `CodeMatcher` API — evaluate it for new transpilers before defaulting to manual iteration.

## Anti-Patterns

- Business logic in Harmony patches (must delegate)
- Sealed types crossing service boundaries (use adapters)
- Regular null checks on computed TaleWorlds properties (use `?.` — see adapters.md)
- Multiple responsibilities in one service (split it)
---
description: Environment failures are reported, not fixed. The agent works within infra; the user controls infra.
---

<!--
This rule has NO `paths:` field intentionally. Per Claude Code memory loader:
  - Rules WITHOUT `paths:` load at conversation start (always-on).
  - Rules WITH `paths:` (any glob, including `**/*`) load only when a matching
    file is opened — they are conditional, not unconditional.
This rule is meant to apply universally, so `paths:` is omitted.
-->


# Environment failures: report, don't fix

When something goes wrong that lives outside the project source — missing tools, broken paths, permissions, MCP server down, Bannerlord install drift, GitHub auth, network errors — **report and stop. Do not attempt to fix.**

## Why

Infra fixes are usually irreversible from the agent's view (installed wrong package, edited the wrong PATH entry, changed a global config). The user has context the agent does not — which Bannerlord install is current, which Steam library is preferred, which NuGet feed should host the package, why a particular MCP server is intentionally disabled.

Self-healing infra silently masks the real problem, then breaks differently next session.

## Examples — report, don't fix

| Symptom | What NOT to do | What TO do |
|---------|----------------|------------|
| `ilspycmd: command not found` | `dotnet tool install -g ilspycmd` | "ilspycmd is missing — needed for v1.3.15 signature verification. Install with `dotnet tool install -g ilspycmd`?" |
| `E:\Decompiled_Bannerlord\` empty | `git clone` something into it | "Decompiled folder is empty. Has the v1.4 dump moved? I need a path." |
| `dotnet build` fails: `MSBuild not found` | Edit `.csproj`, change SDK ref | "MSBuild path not picked up — likely a Visual Studio install issue. Investigate from your end." |
| Serena MCP returns no results | Restart MCP, reinstall | "Serena MCP is returning empty. The mcp-health hook may have marked it unhealthy. Check `.claude/logs/`." |
| `gh auth status` fails | `gh auth login` interactively | "gh CLI isn't authenticated. Run `gh auth login` when convenient." |
| Bannerlord DLL paths in `Directory.Build.props` don't resolve | Edit the props file | "Game install path may have moved. Current target: `E:\Steam\...`. Confirm or update?" |
| `BANNERLORD_GAME_DIR` env var unset | Set it for the session | "BANNERLORD_GAME_DIR isn't set. Export it from your shell config and try again." |

## Examples — these are NOT environment failures (fix normally)

- A C# build error in TAOM source — fix per `/build-fix`
- A test assertion fails — investigate per `/investigate`
- An XSLT transform produces wrong XML — fix the XSLT
- A Harmony patch doesn't take effect — debug per `/investigate`
- A skill or hook script throws — fix the script
- `git status` shows unexpected files — investigate (per `CLAUDE.md` guidance, never delete without checking)

The line: anything inside the TAOM repo's tracked files is in scope. Anything outside (tools on PATH, env vars, Steam install, MCP servers, OS config) is the user's domain.

## Tone when reporting

State the facts:
- What you tried
- What failed (exact error)
- What you suspect (one line — not a long diagnosis)
- The minimal next step the user can take

Don't suggest the user "fix their machine" or imply incompetence. Most env failures are just drift — paths move, tools update, auth tokens expire.
---
paths:
  - ".claude/skills/**/SKILL.md"
description: Per-field validation checklist when porting a skill from an external suite (gstack, everything-claude-code, etc.). Prevents port-drift bugs caught in 2026-04-26 reviews.
---

# Porting Skills From External Suites — Validation Checklist

When you copy a skill from another repo (gstack, everything-claude-code, awesome-claude-code-subagents, etc.) into `.claude/skills/`, every frontmatter field, every hook reference, and every behavioral assumption must be validated against current Claude Code semantics. **Other suites target their own runtimes; their conventions don't necessarily transfer.**

We've shipped four port-drift bugs across three review passes (`triggers:` field unsupported, inline-hook activation conflated with state-file presence, `paths: ["**/*"]` treated as always-load, hardcoded MCP tool counts copied without verifying). The pattern is "trusted the upstream because it worked there."

## Frontmatter field check

For every field in the upstream skill's frontmatter, verify it appears in **`.claude/rules/harness-facts.md`** as documented-and-consumed by current Claude Code. As of 2026-04-26 the documented fields are:

`name`, `description`, `allowed-tools`, `hooks`, `argument-hint`, `disable-model-invocation`, `when_to_use`

Anything else is either undocumented (drop it) or might be consumed (verify with a doc URL before keeping).

**Specific killshots from prior reviews:**

| Upstream field | TAOM disposition | Reason |
|---|---|---|
| `triggers:` | DROP | Not in Claude Code skill schema. gstack uses it for its own preamble. Move trigger phrases into `description`. |
| `version:` | OPTIONAL | Not consumed by Claude Code; harmless metadata. Keep if useful for tracking, drop if it's noise. |
| `preamble-tier:` | DROP | gstack-specific. |
| `model:` (skill-level) | VERIFY | Consumed by Claude Code for some configurations; check current docs before using. |

## Hook block check

If the upstream skill declares `hooks:` in frontmatter:

1. **Confirm the lifecycle assumption.** Per `harness-facts.md`: hooks declared in skill frontmatter only fire while the skill is invoked. If the skill (or its prose body) tells the user to "just write the state file" from another context, the hook will NOT fire — the state file alone is inert. Either invoke the skill explicitly, or move the hook to `.claude/settings.json` for global activation.
2. **Verify hook command paths.** The upstream may use a path like `~/.gstack/...` or `${CLAUDE_PLUGIN_DATA}/...` — these don't exist in TAOM. Use `${CLAUDE_PROJECT_DIR}/.claude/skills/<skill>/<script>.sh` for project-local scripts.
3. **Confirm the matcher names.** `Edit`, `Write`, `Bash`, `NotebookEdit` are correct as of 2026-04-26. Don't trust an upstream's matcher casing without checking.

## Hook script check

If the port includes a shell script:

1. **Avoid generic directory names** like `bin/`, `tmp/`, `cache/`, `obj/`, `node_modules/`. These are routinely caught by repo-wide gitignore patterns (`.gitignore:2 bin/` cost us a working `/freeze` skill on the first commit). Prefer descriptive names: keep the script directly in the skill dir, or use `scripts/`.
2. **Run `git check-ignore -v`** against the script after creating it. If it's ignored, RENAME the directory (don't add a gitignore exception — the underlying name choice is the bug).
3. **JSON output safety.** If the script emits JSON to stdout (PreToolUse hook contract), escape backslashes and quotes in any interpolated path. Windows paths routinely contain `\`; raw paths produce invalid JSON and silently fail-open. See `.claude/skills/freeze/check-freeze.sh::_json_escape` for reference.
4. **State file reads.** Use `IFS= read -r VAR < FILE` to preserve internal whitespace. `tr -d '[:space:]'` is a footgun that strips internal spaces too — Steam install paths contain spaces.
5. **Path normalization.** On Windows + Git Bash, paths arrive in three styles: `C:\Users\...` (Windows), `/c/Users/...` (Git Bash), `C:/Users/...` (mixed). Use `cygpath -u` if available; case-insensitive comparison via `shopt -s nocasematch` for boundary checks.

## Hardcoded value check

For every hardcoded constant the upstream uses (tool counts, file size caps, version numbers):

1. **Verify against the actual source** before copying. Don't assume the upstream's count is current.
2. **Tag in comments** as EXACT (counted from source) or HEURISTIC (estimate from upstream docs). Future maintainers need to know which to re-verify.
3. **Add a re-verify trigger.** If the value comes from a downstream dependency (e.g., MCP server tool count), note the source URL in the comment and recheck quarterly or whenever the dependency version changes.

## Process check

After porting:

1. **Run `bash .claude/skills/context-budget/scan.sh --verbose`** — confirm the new skill appears with reasonable eager (frontmatter) and lazy (body) tokens. Description over 30 words gets flagged.
2. **Update CHANGELOG.md** in the same commit. The pre-commit hook `check-changelog-changed.sh` enforces this for `.claude/` changes.
3. **Commit + run `/codex-verify`** for any non-trivial port — Codex catches the lifecycle and load-semantic mistakes Claude tends to make on first port.

## Lessons from the Tier 1 adoption (the canonical port-drift case study)

Three review passes found 19 issues total. The categories that recurred:

- **6 wrong-API-assumption bugs** — `scan.sh` body counting, hook lifecycle, rule paths semantics, frontmatter schema. Now pinned in `harness-facts.md`.
- **3 process violations** — CHANGELOG missed twice; counter math off by one. Now caught by pre-commit hook.
- **1 gitignore blast** (HIGH) — `bin/` swept up `check-freeze.sh`. Now caught by pre-commit hook + naming rule above.
- **3 stale hardcoded values** — MCP filesystem 12→13, ilspy 8→4, descriptions creeping back to 31w. Now tagged EXACT vs HEURISTIC; description bloat lint added.

Each round found fewer (8 → 7 → 4) suggesting the harness improvements work. Don't skip these checks — they exist because we paid for them.
---
paths:
  - "Main/Features/**/Models/*.cs"
  - "Main/Features/**/*Model.cs"
---

# GameModel Override Rules

TAOM has 31 GameModel overrides. All follow the same pattern.

## Pattern

```csharp
public class TaomFooModel : DefaultFooModel
{
    private readonly IFooService _service;

    public TaomFooModel(IFooService service)
    {
        _service = service;
    }

    public override float SomeCalculation(SealedType param)
    {
        var adapter = IoC.Resolve<IAdapterFactory>().GetAdapter(param);
        var taomResult = _service.Calculate(adapter);
        return taomResult ?? base.SomeCalculation(param);
    }
}
```

## Rules

1. **Research first** — Always decompile `DefaultXxxModel` with `/research` before overriding. Never guess which base methods to call.
2. **Inherit from `Default*`** — Never override `GameModel` directly; inherit from the corresponding `Default*` class.
3. **Call `base.Method()`** — Unless deliberately replacing behavior, fall through to base for unhandled cases.
4. **Thin model class** — The model class is an entry point (<150 lines). **All logic goes in a `Service`.** Line count is a ceiling, not the test. The override body may contain ONLY one of: (a) a single constant expression (e.g. `MaxCharacterTier => 10`), (b) perk/adapter conversion at the boundary plus a direct delegate to the service. A body that contains `if`, `foreach`, `switch`, `yield` branching, or any multi-line computation is a violation — extract to a service even if the model is under 20 lines. "It's only a few lines" is not a carve-out; the rule is binary. Counter-example: `TaomCharacterStatsModel` (one constant) is legal; a 6-line `yield return` chain with a conditional is not.
5. **Adapter boundary** — Convert sealed TaleWorlds params to adapters immediately. Never pass `Hero`, `Settlement`, etc. into the service.
6. **JSON/XML config** — Configurable values live in `Main/_Module/ModuleData/configs/` or feature-specific XML, not hardcoded in the model.
7. **Register in SubModule.cs** — GameModel overrides must be returned from `CreateGameModels()` in `SubModule.cs`.
8. **Tests** — Service logic is fully unit-tested. The model class itself is thin enough to not need direct tests.

## Registration Pattern

```csharp
// In SubModule.cs
public override void OnBeforeInitialModuleScreenSetAsRoot()
{
    // Models registered via AddModel in GetGameModels
}

protected override void OnGameStart(Game game, IGameStarter gameStarter)
{
    if (gameStarter is CampaignGameStarter campaignStarter)
    {
        campaignStarter.AddModel(new TaomFooModel(IoC.Resolve<IFooService>()));
    }
}
```

## Existing Overrides (31 total)

| Model | Base | Feature |
|-------|------|---------|
| `TaomCharacterStatsModel` | `DefaultCharacterStatsModel` | `TroopProgression` |
| `TaomPartyWageModel` | `DefaultPartyWageModel` | `CulturalFeats` |
| `TaomVolunteerModel` | `DefaultVolunteerModel` | `TroopProgression` |
| `TaomArmyManagementModel` | `DefaultArmyManagementCalculationModel` | `CulturalFeats` |
| `TaomPartySpeedModel` | `DefaultPartySpeedCalculatingModel` | `CulturalFeats` |
| `TaomSettlementProsperityModel` | `DefaultSettlementProsperityModel` | `CulturalFeats` |
| `TaomSettlementMilitiaModel` | `DefaultSettlementMilitiaModel` | `CulturalFeats` |
| `TaomBuildingConstructionModel` | `DefaultBuildingConstructionModel` | `CulturalFeats` |
| `TaomVillageProductionModel` | `DefaultVillageProductionCalculatorModel` | `CulturalFeats` |
| `TaomCaravanModel` | `DefaultCaravanModel` | `CulturalFeats` |
| `TaomBattleRewardModel` | `DefaultBattleRewardModel` | `CulturalFeats` |
| `TaomPartyTroopUpgradeModel` | `DefaultPartyTroopUpgradeModel` | `CulturalFeats` |
| `TaomPartySizeModel` | `DefaultPartySizeLimitModel` | `CulturalFeats` |
| `TaomFoodConsumptionModel` | `DefaultMobilePartyFoodConsumptionModel` | `CulturalFeats` |
| `TaomSettlementLoyaltyModel` | `DefaultSettlementLoyaltyModel` | `CulturalFeats` |
| `TaomPartyMoraleModel` | `DefaultPartyMoraleModel` | `CulturalFeats` |
| `TaomSmithingModel` | `DefaultSmithingModel` | `CulturalFeats` |
| `TaomClanFinanceModel` | `DefaultClanFinanceModel` | `CulturalFeats` |
| `TaomRaidModel` | `DefaultRaidModel` | `CulturalFeats` |
| `TaomMilitaryPowerModel` | `DefaultMilitaryPowerModel` | `BattleBalance` |
| `TaomCombatSimulationModel` | `DefaultCombatSimulationModel` | `BattleBalance` |
| `TaomPartyHealingModel` | `DefaultPartyHealingModel` | `Arena` |
| `TaomTournamentModel` | `DefaultTournamentModel` | `Arena` |
| `TaomAgeModel` | `DefaultAgeModel` | `RaceAge` |
| `TaomPregnancyModel` | `DefaultPregnancyModel` | `RaceAge` |
| `TaomHeroCreationModel` | `DefaultHeroCreationModel` | `RaceAge` |
| `TaomAllianceModel` | `DefaultAllianceModel` | `Diplomacy` |
| `TaomKingdomDecisionPermissionModel` | `DefaultKingdomDecisionPermissionModel` | `Diplomacy` |
| `TaomDiplomacyModel` | `DefaultDiplomacyModel` | `Diplomacy` |
| `TaomExecutionRelationModel` | `DefaultExecutionRelationModel` | `Execution` |
| `TaomInformationRestrictionModel` | `DefaultInformationRestrictionModel` | `Encyclopedia` |
---
paths:
  - "Main/**/*Mixin*.cs"
  - "Main/**/*Prefab*.cs"
  - "Main/**/*Widget*.cs"
  - "Main/**/*VM.cs"
  - "Main/**/*ViewModel*.cs"
  - "Main/_Module/GUI/**"
---

# GUI / UI / Sprite Rules

## Sprite References (MANDATORY)

Before writing ANY `Sprite="X"` in XML or `GetSprite("X")` in C#:

1. **Read `Main/_Module/GUI/TAOMSpriteData.xml`** — find the `<Name>` entry for your sprite
2. **Use the EXACT registered name** — sprite ID = filename without extension, prefixed by subfolder path using backslashes
3. **Do NOT add module prefixes** — a PNG at `SpriteParts/ui_taom/CareerSystem/foo.png` is `CareerSystem\foo`, NOT `TAOM\CareerSystem\foo`
4. **Verify the PNG exists** — check `GUI/SpriteParts/ui_taom/<subfolder>/` before referencing

**Why:** Sprite="TAOM\CareerSystem\career_button_placeholder" failed silently (blank button, no crash, no log) because the registered name was "CareerSystem\career_button_placeholder". This class of bug is invisible without in-game testing.

## UIExtenderEx PrefabExtension Safety (MANDATORY)

Before injecting into ANY vanilla prefab container:

1. **Research the target container** — decompile vanilla code that accesses the container's children
2. **Check for hardcoded indexing** — does vanilla code do `children[i]` with a fixed index?
3. **Check for typed iteration** — does vanilla code cast all children to a specific type?
4. **Check for count assumptions** — does vanilla code assume `children.Count == N`?

If ANY of these are true: **do NOT inject children into that container**. Use bound `[DataSourceProperty]` on a ViewModel mixin + inject into a DIFFERENT container that's safe.

**Why:** Adding to `SecondaryInfoItems` caused `IndexOutOfRangeException` in vanilla's `HandlePanelSwitchingInput` (hardcoded positional indexing). This pattern applies to any data-bound `ListPanel` where vanilla code indexes by position.

**Safe pattern:**
```
[ViewModelMixin] → [DataSourceProperty] bindings
[PrefabExtension] → inject widget into a NON-data-bound container, bind to mixin properties
```

**Unsafe pattern:**
```
mapInfo.SecondaryInfoItems.Add(new MapInfoItemVM(...))  // NEVER DO THIS
```

## TaleWorlds VM property setters: verify no-op early returns (MANDATORY)

Before writing `vm.X = value` on any TaleWorlds-owned ViewModel property (especially anything ending in `Index`, `SelectedItem`, `Selected*`, or `Current*`), decompile the setter to confirm whether it returns early when `value == _backingField`. Many setters are guarded:

```csharp
set
{
    if (value != _backingField)  // ← early return when no change
    {
        _backingField = value;
        // ... real state updates: SelectedItem, IsSelected, _onChange.Invoke ...
    }
}
```

If the setter is guarded, **mutating the underlying collection then re-setting the property to the same value is a no-op** — the dependent state (`SelectedItem`, downstream `_onChange` callbacks, child-VM `IsSelected` flags) does not refresh, leaving stale references to objects that are no longer in the collection.

**Concrete pattern caught in the wild (Codex Review #30, 2026-05-04):** TAOM's CustomBattles filter cleared `CharacterSelectionGroup.ItemList`, populated 3 new `CharacterItemVM`s, then set `SelectedIndex = 0`. Because vanilla left `_selectedIndex` at `0` already, the setter's `if (value != _selectedIndex)` guard short-circuited, leaving `SelectedItem` pointing at a `CharacterItemVM` that had just been removed. The Custom Battle launched with the previously-selected commander instead of the filtered first one — visible faction picker disconnected from the actual battle commander.

**Correct patterns (in order of preference):**
1. **Use the type's own `Refresh()` method** when one exists (e.g., `SelectorVM<T>.Refresh(IEnumerable<T>, int, Action<SelectorVM<T>>)`). Vanilla `Refresh` resets the private backing field directly before re-setting, which is the documented escape hatch.
2. **Mirror `Refresh()`'s reset trick via reflection** when the public API doesn't fit. Cache the `FieldInfo` once at `Initialize()`, then `field.SetValue(vm, -1)` (or whatever sentinel the type uses) before assigning the real value. See `Main/Features/CustomBattles/Hooks/CommanderSelectorRebuilder.cs` for the canonical implementation.
3. **Avoid setting the same value back** — if you can detect the no-op case (read the current value first, only assign if different), you sidestep the trap entirely. But this only helps when there's no dependent state that needs refreshing.

**Do NOT** use the indirection of `prop = -1; prop = 0;` — many setters fire downstream callbacks (`_onChange.Invoke(this)`) that crash on the intermediate sentinel value (e.g., `OnCharacterSelection` dereferences `selector.SelectedItem.Character`, which is null at index -1).

**When this rule applies:** Any C# file that mutates TaleWorlds VM properties post-construction (filter patches, refresh hooks, mid-mission UI updates). Decompile the setter via `ilspycmd` against the installed v1.3.15 DLL before writing the assignment. The guard on `_setter` is invisible at the call site.

## TaleWorlds VM property notification: prefer public setter over reflected field+notify (MANDATORY)

When you need to REPLACE an entire VM property's value (not just mutate the existing object), and the field is private but a public property wraps it, **always use the public property setter**. Do NOT reflect on the backing field and then try to fire the change notification yourself.

```csharp
// ❌ WRONG — silently breaks UI rebinding
_raceSelectorField.SetValue(faceGenVM, newSelector);
_onPropertyChangedWithValueMethod?.Invoke(faceGenVM, new object[] { newSelector, "RaceSelector" });

// ✅ RIGHT — vanilla setter handles both field assignment and notification
faceGenVM.RaceSelector = newSelector;
```

The `OnPropertyChangedWithValue` method on `ViewModel` is **generic** (`OnPropertyChangedWithValue<T>(T value, string propertyName) where T : class`). `AccessTools.Method` looking up by `(typeof(object), typeof(string))` returns `null` because the open generic's signature is `(T, string)`, not `(object, string)`. The reflected invoke would fail at runtime — but with a `?.` null-conditional the failure is silent. Result: the field is replaced internally, but Gauntlet's `GauntletView.OnViewModelPropertyChangedWithValue` is never called, `RefreshBindingWithChildren` never fires, and the UI stays bound to the previous value forever.

Initial construction can mask this — `LoadMovie("...", DataSource)` reads the field directly after construction. Subsequent changes are where the bug manifests: any `Refresh(true)` in vanilla VM code that re-creates the property's value will rebind to the vanilla version, NOT your replacement.

**Rule:** before reflecting on a private field, search for a public property that wraps it (`grep -n "public.*get { return _fieldName }\|return _fieldName;"`). If the property exists, use its setter. The setter handles both the field assignment AND the correctly-typed change notification. Only reflect when no such property exists (e.g., the field is `private` with no wrapper).

**Concrete pattern caught in the wild (Codex Review #33, 2026-05-06):** `FaceGenRaceSelectorRebuilder.Apply` mutated `_raceSelector` via reflection, then attempted `OnPropertyChangedWithValue(object, string)` invocation. The lookup returned `null`. Field was replaced; UI dropdown stayed bound to vanilla's unfiltered selector. First Refresh appeared correct (initial construction reads field), but every race-change rebound to vanilla. Fixed by replacing both lines with `faceGenVM.RaceSelector = newSelector`.

**Sister rule:** the setter-guard rule above (no-op early returns) covers the case where you assign the SAME value. This rule covers the case where you assign a DIFFERENT value but bypass the setter. Both must be respected.

## ViewModel Binding Rules

- `@PropertyName` in XML must EXACTLY match `[DataSourceProperty]` name (case-sensitive)
- `Command.Click="ExecuteX"` requires a public `void ExecuteX()` method
- `{CollectionName}` requires `MBBindingList<T>`, NOT `List<T>`
- Every `[DataSourceProperty]` that is set must have a corresponding XML binding — unused properties are dead code

## File Conventions

| File Type | Location | Naming |
|---|---|---|
| Prefab XML | `Main/_Module/GUI/PreFabs/<Feature>/` | `<ScreenName>.xml` |
| ViewModel | `Main/Features/<Feature>/UI/` | `<Name>VM.cs` |
| Mixin | `Main/Features/<Feature>/UI/` | `<Target>Mixin.cs` |
| Prefab extension | `Main/Features/<Feature>/UI/` | `<Feature>Prefab.cs` |
| Custom widget | `Main/Features/<Feature>/UI/` | `<Name>Widget.cs` |
| Source sprites | `GUI/SpriteParts/ui_taom/<Feature>/` | `<sprite_name>.png` |
---
paths:
  - "Main/**/Hooks/**"
  - "Main/**/Patches/**"
  - "Main/**/*Patch.cs"
---

# Harmony Patch Rules

## Research First (MANDATORY)
ALWAYS decompile the target method with `ilspycmd` before writing a patch. Verify:
- Exact method signature (parameters, return types, access modifiers)
- Whether the method is virtual, sealed, or static
- Correct namespace and class hierarchy
- Method existence in Bannerlord v1.3.12

## Patch Types
- **Prefix** — Runs before original method. Return `false` to skip original.
- **Postfix** — Runs after original method. Can modify `__result`.
- **Transpiler** — Modifies IL instructions. Most fragile — use sparingly.

## Architecture Requirements
- Patches are **thin entry points** — delegate ALL logic to services via `IHookInterface`
- Entry point files MUST be <150 lines (ADR-002)
- Resolve services from IoC container, never instantiate directly
- Use thread-local state pattern for multi-patch coordination

## Patch Organization
- Place in `Main/Features/{FeatureName}/Hooks/` directory
- Name: `{TargetClass}{TargetMethod}Patch.cs`
- Register in `SubModule.cs` patch categories (Patch0 through Patch6)

## Common Pitfalls
- Collection modification during iteration — use `.ToList()` copy
- Null handling — TaleWorlds often expects `TextObject.Empty` not `null`
- Event timing — verify when events fire vs when state changes
- Static state — avoid unless using thread-local pattern
- **Reflection in hot paths** — `AccessTools.Method` / `AccessTools.Field` lookups MUST be cached in a static field during `Initialize()`, never resolved inside `Prefix()`/`Postfix()`. Guard spawning calls the patch ~20x per settlement visit; uncached reflection means ~20 redundant lookups per entry.

## Static State Machines: Sentinel-Collision Check (MANDATORY)

When a patch holds static state across frames AND drives that state from polling external values (engine counts, file sizes, MBObjectManager queries, vanilla VM properties), enumerate the four boundary states BEFORE writing the change-detection logic:

| # | State | Typical value |
|---|-------|---------------|
| 1 | Sentinel / uninitialized (set by `Reset...()` / `Initialize()`) | `-1`, `null`, `default(T)`, empty |
| 2 | First real observation (poll returns this BEFORE work has begun) | `0`, `false`, empty collection |
| 3 | In-progress values | the range during normal operation |
| 4 | Terminal value (completion) | often the same encoding as state 2 |

**The trap:** state 2 and state 4 frequently share the same encoding (e.g. `0`). The change-detection comparison sees `_lastValue = -1`, observes `0`, and concludes "value changed, terminal state reached" — even though the polled subsystem simply hadn't started yet.

**The rule:** if your patch acts on a "sentinel → terminal" transition (cleanup, latch reset, `EndGame()` call, anything irreversible-for-this-cycle), require an additional `_hasObservedWork` boolean flag set the first time you observe a state-3 value. Only fire the terminal-state action when `current == terminal && _hasObservedWork`.

**Why this rule exists:** RCA `docs/reviews/rca-shader-precompilation-initial-zero-latch-2026-05-04.md`. The shader-precompilation patch's `_lastShaderCount = -1` collided with `Utilities.GetNumberOfShaderCompilationsInProgress() == 0` on the first frame after a warm-cache load. The patch fired its completion branch, killed its own latch, and produced an entire battle of blank loading screens that looked like the feature was completely broken.

**Sibling rule:** see `.claude/rules/csharp-architecture.md` "Entity State Matrix" for the lifecycle equivalent (*when does this entity die?*). Observation matrix and lifecycle matrix are different reviews — both are needed for static-state machines that observe external state.
---
description: Verified Claude Code load semantics, hook lifecycle, and frontmatter schema. Pinned source-of-truth so future skill/rule/agent edits don't recreate already-fixed bugs.
---

<!--
This rule has NO `paths:` field intentionally — see scoped-rules convention
in CLAUDE.md. It is loaded at every conversation start.

Each fact below is sourced from official Claude Code docs (URLs provided)
or from a specific TAOM bug we shipped and fixed. When you see "verified
2026-04-26" that means a Codex review pass cited the upstream doc by URL
and the assertion held up against current behavior. If Claude Code changes,
update this file as the FIRST step — never let other harness files drift
ahead of this one.
-->

# Claude Code Harness Facts (verified)

## Skill load semantics

| Fact | Source | Why we care |
|------|--------|-------------|
| Skill **descriptions** load eagerly at conversation start, EXCEPT when the skill has `disable-model-invocation: true` — those descriptions are NOT in context. Skill **bodies** load only when the skill is invoked, regardless of model-invocation setting. | https://code.claude.com/docs/en/skills (verified 2026-04-26) | If you're auditing context overhead, count frontmatter only for the eager total — and skip the eager charge for skills with `disable-model-invocation: true`. The pre-fix `scan.sh` got the body-counting wrong and inflated the baseline 25× for skills. |
| Frontmatter fields documented as consumed: `name`, `description`, `allowed-tools`, `hooks`, `argument-hint`, `disable-model-invocation`, `when_to_use`. | docs above | `triggers:` is NOT documented. Other suites (gstack) use it for their own preamble; in Claude Code it's dead weight. Move trigger phrases into `description` or `when_to_use`. |
| Skill description should be ≤30 words. It loads on every Task spawn. | empirical / scan.sh flag | We've trimmed `/freeze` and `/investigate` twice for description creep. The bloat comes back when phrases get pasted in during edits — keep an eye on word count. |
| Skills with `disable-model-invocation: true` are user-only (no proactive invoke). | docs above | Use this for skills that cost money or create public artifacts. We currently apply it implicitly via routing-table "Never auto-invoke" tier rather than via frontmatter. |

## Agent (subagent) load semantics

| Fact | Source | Why we care |
|------|--------|-------------|
| Agent **descriptions** load into the Task tool's tool-definition context for every Task spawn. Agent **bodies** load only when that specific agent is spawned. | docs (skills + Task tool) | Same eager/lazy split as skills. `scan_agents` had the same body-counting bug as `scan_skills` — caught in RCA. |
| Agent description should be ≤30 words. Loaded into every Task tool spawn. | empirical | Bloated agent descriptions tax every Task call, not just when that agent is used. |

## Hook lifecycle

| Fact | Source | Why we care |
|------|--------|-------------|
| Hooks declared in `.claude/settings.json` are **global** — they fire for every tool call matching the matcher, regardless of which skill is active. | https://code.claude.com/docs/en/hooks (verified 2026-04-26) | Use settings.json hooks for unconditional safety nets (build check, push validation). |
| Hooks declared inline in a skill's `SKILL.md` `hooks:` frontmatter are **scoped to that skill's lifecycle** — they fire only while the skill is invoked. | docs above (verified 2026-04-26) | This is what `/freeze` does. Crucial corollary: writing the `freeze-dir.txt` state file from a non-`/freeze`, non-`/investigate` context does NOT activate the hook. The state file alone is inert. |
| `/investigate` re-declares `/freeze`'s PreToolUse hook in its own SKILL.md frontmatter. This is intentional — it lets `/investigate` write the state file and have the hook fire under its own activation. | this repo's design | Don't extend this pattern blindly. Copy the inline hook block to another skill ONLY when that skill genuinely needs the same behavior, with explicit reasoning. |
| Hook scripts read tool-input JSON from stdin and emit JSON to stdout: `{}` to allow, `{"permissionDecision":"deny","message":"..."}` to block, `{"permissionDecision":"ask",...}` to prompt. Malformed JSON typically results in fail-open (allow). | docs above + check-freeze.sh test cycle | Always escape backslashes (`\` → `\\`) and quotes (`"` → `\"`) in any path interpolated into the JSON message. Windows paths routinely contain backslashes; unescaped output crashes the parser silently. |

## Rule loader (memory) semantics

| Fact | Source | Why we care |
|------|--------|-------------|
| Rules WITHOUT a `paths:` field load at conversation start (always-on). | https://code.claude.com/docs/en/memory (verified 2026-04-26) | This is how `harness-facts.md`, `environment-failures.md`, and `csharp-architecture.md` etc. behave. |
| Rules WITH any `paths:` field (any glob, including `paths: ["**/*"]`) load **conditionally** — only when a file matching the glob is opened. | docs above (verified 2026-04-26) | `paths: ["**/*"]` is NOT a synonym for "always-load". To make a rule unconditional, omit `paths:` entirely. The pre-fix `environment-failures.md` had this wrong. |

## Memory file (MEMORY.md) semantics

| Fact | Source | Why we care |
|------|--------|-------------|
| MEMORY.md is loaded at the start of every conversation. Cap is whichever binds first: first ~200 lines OR first ~25KB. | https://code.claude.com/docs/en/memory (verified 2026-04-26) | Counts toward the eager startup baseline. `scan_memory()` should enforce both caps in the token estimate. |
| MEMORY.md lives at `~/.claude/projects/<project-slug>/memory/MEMORY.md`. The Claude Code memory docs only say `<project>` "is derived from the git repository". The exact derivation (drive letter lowercased + `--` + path with `/` and `\` replaced by `-`) is **empirical, not doc-backed** — observed on Windows + Git Bash on 2026-04-26. The format may differ on other platforms or change in future Claude Code versions. | https://code.claude.com/docs/en/memory + empirical | When auditing memory across projects, derive the candidate slug from `cygpath -w "$REPO_ROOT"` then transform — and fall back to substring matching if the derived slug doesn't match an actual directory. Substring matching alone on basename is ambiguous when multiple project slugs share a substring (TAOM, TAOM-Online, taommod), so prefer derived-then-fallback over fallback-only. |

## Git invocation forms hooks must handle

When writing a PreToolUse(Bash) hook that filters on git subcommands, enumerate explicitly which invocation forms it must catch — substring matching `*"git commit"*` MISSES the following real-world forms (Codex review 2026-04-26 found this gap):

| Form | Purpose | Handled by `*"git commit"*` substring? |
|------|---------|----------------------------------------|
| `git commit` | Bare commit | YES |
| `git commit -m "msg"` | Commit with message | YES |
| `git commit --amend` | Amend (must NOT blanket-skip — see "amend exemptions" below) | YES |
| `git commit -F file.txt` | Commit with message file | YES |
| `git -C /path commit` | Run as if from /path (no leading `cd`) | NO — needs `*"git -"*" commit"*` |
| `git -c key=val commit` | One-time config override | NO — same |
| `git --git-dir=/path commit` | Operate on a specific git-dir | NO — would need a separate pattern |
| `git commit-tree` | Plumbing — DIFFERENT command, must NOT match | YES (false positive) — needs explicit `*"git commit-"*` rejection |
| `git commit-graph` | Plumbing — same | YES (false positive) — same |

**Reference pattern** (used by `check-changelog-changed.sh`, `check-claude-files-tracked.sh`, and `suggest-compact.sh`):

```bash
case "$COMMAND" in
    *"git commit-"*) echo '{}'; exit 0 ;;       # commit-tree etc — different command
esac
case "$COMMAND" in
    *"git commit"* | *"git -"*" commit"* ) ;;   # bare or with leading flags
    *) echo '{}'; exit 0 ;;
esac
```

**MANDATORY for any new hook that detects git commits.** Codex review #29 caught `suggest-compact.sh` shipping in `79350f2` with a bare `*"git commit"*` substring matcher — the same recursion-risk class codified after review #28. The prevention rule existed but wasn't applied to its own first user.

When you write a NEW hook (or add commit detection to an existing one), grep for `git commit` substring matches in the diff before commit. If you find one that's NOT using the two-stage pattern above, that's a regression — fix before shipping. The `/skill-stocktake` checklist now includes this check.

## Amend exemptions in pre-commit hooks (recursion-risk pattern)

Do NOT blanket-skip `git commit --amend` in pre-commit hooks. `amend` is commonly used as a workflow ("oops, forgot a file, amend it in") — that's exactly the case the hook needs to catch. Codex review 2026-04-26 caught this as prevention theater: both `check-changelog-changed.sh` and `check-claude-files-tracked.sh` originally exempted `--amend`, defeating the very gates they were supposed to enforce.

Two correct patterns depending on what the hook checks:

| Hook checks | Correct amend handling |
|-------------|------------------------|
| Files in the commit's diff (e.g., is CHANGELOG.md staged?) | Compute the **post-amend file set** as `staged ∪ HEAD` and apply the same gate. If CHANGELOG was already in HEAD's diff, it's still in the post-amend commit — the gate correctly allows. |
| Working-tree state (e.g., is a file gitignored?) | Don't exempt amend at all. Working-tree state is amend-independent — a gitignored file on disk is just as broken in an amended commit as in a fresh one. |

## Gitignore blast radius

| Fact | Source | Why we care |
|------|--------|-------------|
| `git check-ignore -v <path>` is the authoritative check for "is this file gitignored". Reading `.gitignore` and grepping is unreliable (multiple files, negation rules, parent dir patterns). | git docs + 2026-04-26 deep-review | The pre-fix `check-freeze.sh` was excluded by `.gitignore`'s `bin/` line (intended for `Main/bin/` .NET output) and shipped as a non-functional skill. Always run `git check-ignore` against any new file under `.claude/` before assuming it'll commit. |
| Generic patterns in `.gitignore` (`bin/`, `obj/`, `*.cache`, `tmp/`, `node_modules/`) match anywhere in the tree, not just at the repo root. | git docs | When introducing a new directory under `.claude/`, prefer descriptive names (`scripts/`, `state/`) over generic ones (`bin/`, `tmp/`, `cache/`). |

## What this rule changes about how you work

When you write or modify any skill, agent, rule, or hook in `.claude/`:

1. **If the change relies on Claude Code load behavior** (eager vs lazy, hook lifecycle, rule loader scoping, frontmatter consumption) — verify against this file's facts. If this file disagrees with what you intended, update this file FIRST (with a doc citation) before changing the harness.
2. **If you're porting a skill from an external suite** (gstack, everything-claude-code, etc.) — see `.claude/rules/external-skill-ports.md` for the per-field validation checklist.
3. **If you're committing changes touching `.claude/`** — the pre-commit hook `check-changelog-changed.sh` will **hard-block** the commit if CHANGELOG.md isn't in the post-commit file set (staged for new commits, staged + HEAD for amends). The hook `check-claude-files-tracked.sh` will **hard-block** if any file under `.claude/{skills,agents,rules,hooks}/` exists on disk but is gitignored or untracked. Both hooks fire on amends too — there is no blanket `--amend` exemption (a Codex review on 2026-04-26 caught this as a recursion-risk; amend is commonly used as "oops forgot a file" workflow, exactly the case the gate must catch). NOTE: these hooks fire only when Claude Code invokes Bash via the tool dispatch — they do NOT fire when a user types `git commit` directly in a shell outside Claude. They are prevention for Claude-driven commits, not a global git pre-commit hook.

4. **When running `/review-codex` or any review skill** — Phase 3e (Root Cause Analysis) applies to **EVERY confirmed bug**, not just HIGH ones. Conflating severity with importance for RCA means we patch LOW symptoms but never extract the systemic lesson — and the same category of bug ships again in the next commit. The skill's literal text is: *"Do NOT skip this step. The point is not just to fix bugs — it's to make the same category of bug impossible in future features."* Review #28 caught us shortcutting this — we ran RCA only for the HIGH+MED bypass, not for the 4 LOWs and 2 MEDs that also had real "why missed" stories.

5. **When writing facts in this file** (or any rule that asserts behavior) — every fact must explicitly cite either a doc URL (DOC-BACKED) or an observation context (EMPIRICAL: where, when, by whom). Vague "verified" claims without source attribution age into wrong assumptions. Example: the project-slug derivation rule was originally presented as fact; Codex caught that the Claude Code memory docs only say `<project>` "is derived from the git repository" — the exact format is empirical-on-Windows, not doc-backed.

## Last verified: 2026-04-26

This file is the source of truth for harness behavior in TAOM. Update the "Last verified" date and add new facts whenever a Codex review or experiment confirms something not yet captured here.
---
paths:
  - "TAOM.Tests/**"
  - "**/*Tests.cs"
  - "**/*Test.cs"
---

# Testing Rules (TDD Mandatory)

## Workflow: RED -> GREEN -> REFACTOR
1. Write a failing test FIRST (verify RED state)
2. Write minimum production code to pass (GREEN)
3. Refactor while keeping tests green

## Naming Convention
`MethodName_StateUnderTest_ExpectedBehavior`

Examples:
- `LoadRegions_ValidJson_ReturnsRegionList`
- `GetWage_Tier10_ReturnsExtendedWage`
- `LoadRegions_MissingFile_ReturnsEmptyAndLogs`

## Structure: AAA Pattern
```csharp
[TestMethod]
public void MethodName_State_Expected()
{
    // Arrange
    var mock = Substitute.For<IMyAdapter>();

    // Act
    var result = _sut.DoSomething();

    // Assert
    Assert.AreEqual(expected, result);
}
```

## Framework
- **MSTest** — `[TestClass]`, `[TestMethod]`, `[TestInitialize]`, `[TestCleanup]`
- **NSubstitute** — `Substitute.For<T>()`, `.Returns()`, `.Received()`
- **No Moq** — Project uses NSubstitute exclusively

## Coverage Requirements
| Layer | Required |
|-------|----------|
| Services/Engines | 100% |
| Hooks | 80%+ |
| Entry Points | N/A (thin delegation) |
| Adapters | Via service tests |

## Skip-Guard Exhaustion (MANDATORY)

When a service method has `if (condition) continue/return` guard clauses, write a test for **every entity state that should be skipped**, not just the obvious ones.

**Why:** Review #23 found a HIGH bug where `EnsureCompanionsPlaced()` had guards for dead heroes, disabled entries, and already-placed companions -- but missed the recruited-and-traveling state. The most important negative case (companion in player's party) was untested.

**Rule:** For any method that iterates entities and conditionally skips:
1. List every possible entity state (use the state matrix from `csharp-architecture.md`)
2. Write one test per skip condition
3. The test name must identify the specific state: `Method_RecruitedCompanion_SkipsPlacement`
4. Prioritize the most common real-world states first -- a companion traveling with the player is more common than a dead companion

**Pattern to apply:**
```
// For each guard clause:  if (!X) continue;
// Write: Method_XisFalse_Skips()
// AND:   Method_XisTrue_Proceeds()
```

## Test Organization
Mirror source structure: `TAOM.Tests/Features/{FeatureName}/{ServiceName}Tests.cs`
---
paths:
  - "Main/_Module/ModuleData/troops/**"
  - "Main/_Module/ModuleData/taom_partyTemplates.xml"
  - "Main/Features/TroopProgression/**"
---

# Troop Management Rules

## When Adding or Restructuring Troops

Update ALL of the following (checklist):

| Step | File(s) | What to do |
|------|---------|------------|
| 1. Define troops | `Main/_Module/ModuleData/troops/troops_{culture}.xml` | Add NPCCharacter with skills, equipment, upgrade_targets, race, culture |
| 2. Party templates | `Main/_Module/ModuleData/taom_partyTemplates.xml` | Add to ALL relevant templates for the culture (hero, patrol L1/L2/L3, outlaw, rebels, mercenary, vassal_reward) |
| 3. Culture config | `Main/_Module/ModuleData/taom_spcultures.xml` | Update `basic_troop` / `elite_basic_troop` if entry point changed |
| 4. Recruitment code | `Main/Features/TroopProgression/VolunteerRecruitmentService.cs` | Add/update settlement, clan, and culture fallback pools |
| 5. Recruitment tests | `TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs` | TDD: write tests FIRST, then implement |
| 6. NPC references | `Main/_Module/ModuleData/characters/npcs_{culture}.xml` | Check villager upgrade_targets, caravan guard references |
| 7. CHANGELOG | `CHANGELOG.md` | Document the changes |

## Troop ID Naming Convention

`{culture_prefix}_{origin}_{role}` — Examples:
- `dg_goblin_slave` — Dol Guldur, goblin race, slave role
- `dg_khamul_shadow_initiate` — Dol Guldur, Khamul's line, shadow initiate
- `gondor_ano_peasant` — Gondor, Anórien origin, peasant role

## Race Attributes by Culture

| Culture | Race Lines | Race Attribute |
|---------|-----------|---------------|
| Dol Guldur | Goblin | `race="goblin"` |
| Dol Guldur | Orc | `race="orc"` |
| Dol Guldur | Uruk | `race="dg_uruk"` |
| Dol Guldur | Khamul (human) | no `race` attribute |
| Gondor | Human | no `race` attribute |
| Gundabad | Goblin/Orc | `race="goblin"` / `race="orc"` |

## Party Template Types

Each culture typically has these templates in `taom_partyTemplates.xml`:

| Template | Purpose | Typical Composition |
|----------|---------|-------------------|
| `kingdom_hero_party_{culture}_template` | Lord armies | Full range T1-T9 |
| `kingdom_hero_party_mercenary_{culture}_template` | Mercenary bands | Mid-tier professional |
| `kingdom_hero_party_outlaw_{culture}_template` | Outlaw parties | Low-tier rabble |
| `patrol_party_{culture}_template_level_1` | Weak patrols | Low-mid tier |
| `patrol_party_{culture}_template_level_2` | Medium patrols | Mid tier |
| `patrol_party_{culture}_template_level_3` | Elite patrols | High tier |
| `rebels_{culture}_template` | Rebel uprisings | Low tier masses |
| `vassal_reward_troops_{culture}` | Vassal rewards | Elite troops |
| `militia_{culture}_template` | Town garrison | Militia troops |

## Save Compatibility

- **Never change troop IDs** — rename display names only (keep `id` attribute)
- **Never delete troops** — orphan them (remove from upgrade_targets) but keep in file
- **is_basic_troop** — marks a troop as a standalone recruitment entry point
---
paths:
  - "Main/_Module/ModuleData/**/*.xml"
  - "Main/_Module/ModuleData/characters/**"
  - "Main/_Module/ModuleData/factionmap/**"
---

# XML Data File Rules

## File Types
- **XSLT transforms** (`*.xslt`) — Modify vanilla XML at load time (see xslt.md rule)
- **New entity XML** (`characters/*.xml`, `taom_*.xml`) — Entities not in vanilla
- **JSON config** (`factionmap/*.json`) — Feature-specific data

## Culture NPC Naming Convention
Each culture has 26 notable NPCs in `characters/npcs_{culture}.xml`:
- `spc_notable_{culture}_0` through `_4b` — Merchants (10)
- `spc_notable_{culture}_5/_6/_7` — Preachers (3)
- `spc_notable_{culture}_8/_9` — Artisans (2)
- `spc_notable_{culture}_gl1/_10/_11/_gl4/_12/_13` — Gang Leaders (6)
- `spc_notable_{culture}_21/_22` — Rural Notables (2)
- `spc_{culture}_headman_1/_2/_3` — Headmen (3)

## Culture Attribute References
Culture XML attributes (`merchant_notary`, `artisan_notary`, etc.) must reference the FIRST NPC of each occupation type.

## Region Codes
EN=Rohan, ES=Mordor, EW=Gondor, A=Harad, B=Dunland, V=Vlandia, K=Easterlings, S=Dale/North, DG=Dol Guldur, E=Erebor, G=Gundabad, I=Isengard, L=Lothlorien, M=Mirkwood, R=Rivendell, RU=Rhun, U=Umbar

## Config ID Cross-Reference (MANDATORY)

After writing ANY XML/JSON config containing culture, kingdom, or settlement IDs, cross-reference EVERY ID against this table before moving on.

### Culture StringIds (runtime values)

| Type | StringIds | Note |
|------|-----------|------|
| **Custom cultures** | `gondor`, `mordor`, `erebor`, `rivendell`, `lothlorien`, `mirkwood`, `isengard`, `gundabad`, `dolguldur`, `umbar` | Use LOTR names |
| **XSLT cultures** | `vlandia` (Rohan), `empire` (Dunland), `aserai` (Harad), `khuzait` (Easterlings), `sturgia` (Dale), `battania` (Khand) | Use vanilla engine IDs |

**Common mistake:** Writing lore names for XSLT cultures. `rohan` is WRONG — use `vlandia`. `dunland` is WRONG — use `empire`. `harad`/`rhun`/`dale`/`khand` are WRONG — use `aserai`/`khuzait`/`sturgia`/`battania`.

### Checklist

| Step | What to check |
|------|---------------|
| 1 | Every `culture=` attribute uses a StringId from the table above |
| 2 | Every `kingdom=` attribute uses a kingdom ID from CLAUDE.md cheatsheet |
| 3 | Every `settlement=` attribute exists in `settlements.xml` |
| 4 | Every `troop=` attribute exists in `troops/troops_{culture}.xml` |

### Why this matters

This exact bug pattern has been caught in 5+ Codex reviews. Custom cultures happen to use LOTR names as StringIds, which makes it easy to assume ALL cultures do — but XSLT cultures inherit vanilla engine IDs.

## Formatting
- 2-space indentation (per .editorconfig)
- UTF-8 encoding
- CRLF line endings
---
paths:
  - "**/*.xslt"
  - "Main/_Module/ModuleData/*.xslt"
---

# XSLT Transformation Rules

## Authoritative Source
- **SandBoxCore/ModuleData/** is the authoritative reference for vanilla XML structure
- NEVER use SandBox/ModuleData/ — it has different element names the engine ignores
- Example: SandBoxCore uses `<notable_templates>` (engine reads), SandBox uses `<notable_and_wanderer_templates>` (engine ignores)

## Passthrough Requirements (CRITICAL)
- Always pass through ALL vanilla attributes: `<xsl:apply-templates select="@*"/>`
- Always pass through unmodified child elements: `<xsl:apply-templates select="*[not(...)]"/>`
- Never filter out vanilla attributes — critical ones like `is_main_culture`, `can_have_settlement`, `faction_banner_key` will be silently dropped
- Only override the specific attributes/elements you intend to change

## Identity Transform
Every XSLT file must include the identity transform template to copy unmatched nodes:
```xml
<xsl:template match="@*|node()">
  <xsl:copy>
    <xsl:apply-templates select="@*|node()"/>
  </xsl:copy>
</xsl:template>
```

## Common Mistakes to Avoid
- Overly broad `xsl:template match` that catches unintended elements
- Hardcoding attribute values that should be passed through from vanilla
- Missing `xsl:output` declaration
- Forgetting to handle child elements when overriding a parent

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git diff --stat; git diff --cached --stat' in C:\Users\mikew\source\repos\TAOM
 succeeded in 349ms:
 CHANGELOG.md                                       | 86 ++++++++++++++++++++++
 .../CharacterCreation/CareerMenuService.cs         |  2 +-
 .../CharacterCreationContentService.cs             | 38 ++++++++++
 .../CharacterCreation/CharacterCreationIoC.cs      |  2 +
 .../CharacterCreation/NarrativeMenuBuilder.cs      |  4 +-
 .../Config/StartupResourcesConfig.cs               |  1 +
 .../StartupResourcesConfigProvider.cs              | 25 ++++++-
 .../StartupResources/StartupResourcesIoC.cs        |  1 +
 Main/Features/TaomSettings.cs                      | 39 ++++++++++
 Main/IoC.cs                                        |  2 +
 Main/SubModule.cs                                  |  2 +
 .../ModuleData/charactercreation/youth_menu.json   |  2 +-
 .../startup_resources/startup_resources_config.xml | 43 +++++++----
 .../CharacterCreationContentServiceTests.cs        |  7 ++
 .../StartupResourcesConfigProviderTests.cs         | 70 ++++++++++++++++++
 docs/features/startup-resources.md                 | 65 ++++++++++------
 16 files changed, 344 insertions(+), 45 deletions(-)
warning: in the working copy of 'Main/Features/StartupResources/Config/StartupResourcesConfig.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Main/Features/StartupResources/StartupResourcesConfigProvider.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Main/Features/StartupResources/StartupResourcesIoC.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Main/_Module/ModuleData/charactercreation/youth_menu.json', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Main/_Module/ModuleData/startup_resources/startup_resources_config.xml', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'TAOM.Tests/Features/StartupResources/StartupResourcesConfigProviderTests.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'docs/features/startup-resources.md', LF will be replaced by CRLF the next time Git touches it

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git diff -- Main/Features/CharacterCreation/CharacterCreationContentService.cs Main/Features/CharacterCreation/CareerMenuService.cs Main/Features/CharacterCreation/NarrativeMenuBuilder.cs Main/Features/CharacterCreation/CharacterCreationIoC.cs Main/Features/StartupResources/Config/StartupResourcesConfig.cs Main/Features/StartupResources/StartupResourcesConfigProvider.cs Main/Features/StartupResources/StartupResourcesIoC.cs Main/Features/TaomSettings.cs Main/IoC.cs Main/SubModule.cs' in C:\Users\mikew\source\repos\TAOM
 succeeded in 350ms:
diff --git a/Main/Features/CharacterCreation/CareerMenuService.cs b/Main/Features/CharacterCreation/CareerMenuService.cs
index e68be27..fd663f1 100644
--- a/Main/Features/CharacterCreation/CareerMenuService.cs
+++ b/Main/Features/CharacterCreation/CareerMenuService.cs
@@ -224,7 +224,7 @@ public class CareerMenuService : ICareerMenuService
         var cultureId = culture?.StringId ?? "gondor";
         var isFemale = Hero.MainHero?.IsFemale ?? false;
         var titleType = manager.CharacterCreationContent?.SelectedTitleType ?? "guard";
-        var equipmentId = $"player_char_creation_{cultureId}_{titleType}_{(isFemale ? "f" : "m")}";
+        var equipmentId = PlayerEquipmentRosterIds.Build(cultureId, titleType, isFemale);
 
         return new List<NarrativeMenuCharacterArgs>
         {
diff --git a/Main/Features/CharacterCreation/CharacterCreationContentService.cs b/Main/Features/CharacterCreation/CharacterCreationContentService.cs
index 8ccc424..d3af99d 100644
--- a/Main/Features/CharacterCreation/CharacterCreationContentService.cs
+++ b/Main/Features/CharacterCreation/CharacterCreationContentService.cs
@@ -10,6 +10,7 @@ using TAOM.Adapters;
 using TAOM.Core.Domain;
 using TAOM.Core.Logging;
 using TAOM.Features.CharacterCreation.Models;
+using TAOM.Features.StartupResources;
 
 namespace TAOM.Features.CharacterCreation;
 
@@ -27,6 +28,8 @@ public class CharacterCreationContentService : ICharacterCreationContentService
     private readonly IHeroRosterAdapter _heroRosterAdapter;
     private readonly IEquipmentRosterProvider _equipmentRosterProvider;
     private readonly ICareerMenuService _careerMenuService;
+    private readonly IPlayerStartupGoldService _playerStartupGoldService;
+    private readonly IPlayerEquipmentService _playerEquipmentService;
     private readonly IModLogger _logger;
 
     // Vanilla cultures already registered by SandBox handler — skip these
@@ -42,6 +45,8 @@ public class CharacterCreationContentService : ICharacterCreationContentService
         IHeroRosterAdapter heroRosterAdapter,
         IEquipmentRosterProvider equipmentRosterProvider,
         ICareerMenuService careerMenuService,
+        IPlayerStartupGoldService playerStartupGoldService,
+        IPlayerEquipmentService playerEquipmentService,
         IModLogger logger)
     {
         _dataProvider = dataProvider;
@@ -50,6 +55,8 @@ public class CharacterCreationContentService : ICharacterCreationContentService
         _heroRosterAdapter = heroRosterAdapter;
         _equipmentRosterProvider = equipmentRosterProvider;
         _careerMenuService = careerMenuService;
+        _playerStartupGoldService = playerStartupGoldService;
+        _playerEquipmentService = playerEquipmentService;
         _logger = logger;
     }
 
@@ -168,6 +175,37 @@ public class CharacterCreationContentService : ICharacterCreationContentService
         TeleportToStartingSettlement(cultureData);
         SetPlayerRace(cultureData, Hero.MainHero?.StringId);
         AssignCareer(selectedCulture.StringId, Hero.MainHero?.StringId);
+        GrantPlayerStartupResources(selectedCulture.StringId, manager);
+    }
+
+    private void GrantPlayerStartupResources(string cultureId, CharacterCreationManager manager)
+    {
+        var heroId = Hero.MainHero?.StringId;
+        if (string.IsNullOrEmpty(heroId))
+        {
+            _logger.LogWarning("CC Finalize: Hero.MainHero is null — skipping player startup gold + equipment");
+            return;
+        }
+
+        try
+        {
+            _playerStartupGoldService.GrantPlayerStartupGold(cultureId, heroId);
+        }
+        catch (Exception ex)
+        {
+            _logger.LogError($"CC Finalize: player startup gold failed: {ex.Message}");
+        }
+
+        try
+        {
+            var titleType = manager.CharacterCreationContent?.SelectedTitleType;
+            var isFemale = Hero.MainHero?.IsFemale ?? false;
+            _playerEquipmentService.ApplyPlayerStartingEquipment(cultureId, titleType, isFemale, heroId);
+        }
+        catch (Exception ex)
+        {
+            _logger.LogError($"CC Finalize: player starting equipment failed: {ex.Message}");
+        }
     }
 
     private void AssignCareer(string cultureId, string heroStringId)
diff --git a/Main/Features/CharacterCreation/CharacterCreationIoC.cs b/Main/Features/CharacterCreation/CharacterCreationIoC.cs
index 0344b19..12b2b87 100644
--- a/Main/Features/CharacterCreation/CharacterCreationIoC.cs
+++ b/Main/Features/CharacterCreation/CharacterCreationIoC.cs
@@ -18,5 +18,7 @@ public static class CharacterCreationIoC
         container.Register<ICCBodyPropertiesProvider, CCBodyPropertiesProvider>(Reuse.Singleton);
         container.Register<IPlayerBodyPropertiesAdapter, PlayerBodyPropertiesAdapter>(Reuse.Singleton);
         container.Register<ICCBodyPropertiesService, CCBodyPropertiesService>(Reuse.Singleton);
+        container.Register<IPlayerEquipmentAdapter, PlayerEquipmentAdapter>(Reuse.Singleton);
+        container.Register<IPlayerEquipmentService, PlayerEquipmentService>(Reuse.Singleton);
     }
 }
diff --git a/Main/Features/CharacterCreation/NarrativeMenuBuilder.cs b/Main/Features/CharacterCreation/NarrativeMenuBuilder.cs
index 74d451c..24da874 100644
--- a/Main/Features/CharacterCreation/NarrativeMenuBuilder.cs
+++ b/Main/Features/CharacterCreation/NarrativeMenuBuilder.cs
@@ -56,9 +56,7 @@ public class NarrativeMenuBuilder
     }
 
     internal static string BuildEquipmentRosterId(string cultureId, string titleType, bool isFemale)
-    {
-        return $"player_char_creation_{cultureId}_{titleType}_{(isFemale ? "f" : "m")}";
-    }
+        => PlayerEquipmentRosterIds.Build(cultureId, titleType, isFemale);
 
     public NarrativeMenuOption BuildOption(NarrativeOptionDefinition definition)
     {
diff --git a/Main/Features/StartupResources/Config/StartupResourcesConfig.cs b/Main/Features/StartupResources/Config/StartupResourcesConfig.cs
index be5bd13..9595c43 100644
--- a/Main/Features/StartupResources/Config/StartupResourcesConfig.cs
+++ b/Main/Features/StartupResources/Config/StartupResourcesConfig.cs
@@ -12,4 +12,5 @@ public class CultureResourceEntry
     public string CultureId { get; set; }
     public int Gold { get; set; }
     public float Influence { get; set; }
+    public int PlayerGold { get; set; }
 }
diff --git a/Main/Features/StartupResources/StartupResourcesConfigProvider.cs b/Main/Features/StartupResources/StartupResourcesConfigProvider.cs
index f3c3925..e0c2305 100644
--- a/Main/Features/StartupResources/StartupResourcesConfigProvider.cs
+++ b/Main/Features/StartupResources/StartupResourcesConfigProvider.cs
@@ -11,6 +11,8 @@ namespace TAOM.Features.StartupResources;
 
 public class StartupResourcesConfigProvider : IStartupResourcesConfigProvider
 {
+    private const int PlayerGoldMaxValue = 10_000_000;
+
     private readonly IPathService _pathService;
     private readonly IModLogger _logger;
     private StartupResourcesConfig _cached;
@@ -50,7 +52,8 @@ public class StartupResourcesConfigProvider : IStartupResourcesConfigProvider
                 {
                     CultureId = id,
                     Gold = int.Parse(el.Attribute("gold")?.Value ?? "0", CultureInfo.InvariantCulture),
-                    Influence = float.Parse(el.Attribute("influence")?.Value ?? "0", CultureInfo.InvariantCulture)
+                    Influence = float.Parse(el.Attribute("influence")?.Value ?? "0", CultureInfo.InvariantCulture),
+                    PlayerGold = ParsePlayerGold(el.Attribute("playerGold")?.Value, id)
                 });
             }
 
@@ -64,4 +67,24 @@ public class StartupResourcesConfigProvider : IStartupResourcesConfigProvider
             return _cached;
         }
     }
+
+    private int ParsePlayerGold(string raw, string cultureId)
+    {
+        if (string.IsNullOrEmpty(raw))
+            return 0;
+
+        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
+        {
+            _logger.LogWarning($"StartupResourcesConfigProvider: invalid playerGold='{raw}' for culture '{cultureId}' — reverting to 0");
+            return 0;
+        }
+
+        if (value < 0 || value > PlayerGoldMaxValue)
+        {
+            _logger.LogWarning($"StartupResourcesConfigProvider: playerGold={value} for culture '{cultureId}' out of range [0, {PlayerGoldMaxValue}] — reverting to 0");
+            return 0;
+        }
+
+        return value;
+    }
 }
diff --git a/Main/Features/StartupResources/StartupResourcesIoC.cs b/Main/Features/StartupResources/StartupResourcesIoC.cs
index b029a28..b940dd9 100644
--- a/Main/Features/StartupResources/StartupResourcesIoC.cs
+++ b/Main/Features/StartupResources/StartupResourcesIoC.cs
@@ -13,5 +13,6 @@ public static class StartupResourcesIoC
         container.Register<IStartupResourcesConfigProvider, StartupResourcesConfigProvider>(Reuse.Singleton);
         container.Register<IStartupGoldService, StartupGoldService>(Reuse.Singleton);
         container.Register<IStartupInfluenceService, StartupInfluenceService>(Reuse.Singleton);
+        container.Register<IPlayerStartupGoldService, PlayerStartupGoldService>(Reuse.Singleton);
     }
 }
diff --git a/Main/Features/TaomSettings.cs b/Main/Features/TaomSettings.cs
index 569fdd5..2e48d08 100644
--- a/Main/Features/TaomSettings.cs
+++ b/Main/Features/TaomSettings.cs
@@ -171,4 +171,43 @@ public class TaomSettings : AttributeGlobalSettings<TaomSettings>
     [SettingPropertyInteger("Turbo Multiplier (Ctrl+Space)", 1, 128, Order = 2,
         HintText = "Speed multiplier while holding Ctrl+Space. Releases back to prior speed on key-up. Default: 16.")]
     public int CtrlSpaceMultiplier { get; set; } = 16;
+
+    // --- Battle Tactics / Siege Dismount ---
+
+    [SettingPropertyGroup("Battle Tactics/Siege Dismount", GroupOrder = 20)]
+    [SettingPropertyBool("Enable Siege Dismount", Order = 0,
+        HintText = "Master toggle for the siege auto-dismount feature. When off, sieges behave vanilla (mount stays equipped).")]
+    public bool EnableSiegeDismount { get; set; } = true;
+
+    [SettingPropertyGroup("Battle Tactics/Siege Dismount")]
+    [SettingPropertyInteger("Siege Mount Behavior (0=Vanilla, 1=KeepOnMap, 2=ToInventory, 3=AutoRemount)", 0, 3, Order = 1,
+        HintText = "0 = Vanilla (no change). 1 = Mount spawns nearby on the map but player is on foot. 2 = Mount moves to inventory for siege duration. 3 = Mount moves to inventory and is auto-restored after siege ends.")]
+    public int SiegeMountBehavior { get; set; } = 3;
+
+    [SettingPropertyGroup("Battle Tactics/Siege Dismount")]
+    [SettingPropertyBool("Siege Dismount Debug Mode", Order = 2,
+        HintText = "Show diagnostic [SiegeDismount] messages on the in-game HUD. Off = file log only.")]
+    public bool SiegeDismountDebug { get; set; } = false;
+
+    // --- Messengers ---
+
+    [SettingPropertyGroup("Messengers", GroupOrder = 25)]
+    [SettingPropertyBool("Enable Messengers", Order = 0,
+        HintText = "Send paid messengers to heroes you have already met. They travel for several days and trigger a conversation on arrival. Disable to remove the encyclopedia button and dialog hook.")]
+    public bool EnableMessengers { get; set; } = true;
+
+    [SettingPropertyGroup("Messengers")]
+    [SettingPropertyInteger("Gold Cost", 10, 500, Order = 1,
+        HintText = "Denar cost to dispatch one messenger.")]
+    public int MessengerGoldCost { get; set; } = 50;
+
+    [SettingPropertyGroup("Messengers")]
+    [SettingPropertyInteger("Travel Days", 1, 10, Order = 2,
+        HintText = "In-game days a messenger spends in transit before arriving at the target. Speed scales to map size.")]
+    public int MessengerTravelDays { get; set; } = 3;
+
+    [SettingPropertyGroup("Messengers")]
+    [SettingPropertyBool("Enable Accidents", Order = 3,
+        HintText = "Random ambush chance during travel. The base hourly probability lives in messenger_config.json (default 0.2%).")]
+    public bool MessengerAccidents { get; set; } = true;
 }
diff --git a/Main/IoC.cs b/Main/IoC.cs
index 0913d30..41987fd 100644
--- a/Main/IoC.cs
+++ b/Main/IoC.cs
@@ -31,6 +31,7 @@ using TAOM.Features.CareerSystem;
 using TAOM.Features.SettlementGuards;
 using TAOM.Features.NamedCompanions;
 using TAOM.Features.RevoltTuning;
+using TAOM.Features.SiegeDismount;
 
 namespace TAOM;
 
@@ -74,6 +75,7 @@ public static class IoC
         SettlementGuardsIoC.RegisterSettlementGuardsFeature(container);
         NamedCompanionIoC.RegisterNamedCompanionsFeature(container);
         RevoltTuningIoC.RegisterRevoltTuningFeature(container);
+        SiegeDismountIoC.RegisterSiegeDismountFeature(container);
 
         _container = container;
 
diff --git a/Main/SubModule.cs b/Main/SubModule.cs
index 7cd4440..f4d3fd5 100644
--- a/Main/SubModule.cs
+++ b/Main/SubModule.cs
@@ -59,6 +59,7 @@ using TAOM.Features.CareerSystem.Models;
 using TAOM.Features.SettlementGuards;
 using TAOM.Features.SettlementGuards.Hooks;
 using TAOM.Features.RevoltTuning;
+using TAOM.Features.SiegeDismount.Hooks;
 using BehaviorTreeWrapper;
 using TaleWorlds.CampaignSystem.CampaignBehaviors;
 
@@ -426,6 +427,7 @@ public class SubModule : MBSubModuleBase
         mission.AddMissionBehavior(new AutonomousMovementPlayerController());
         mission.AddMissionBehavior(new WargMissionBehavior());
         mission.AddMissionBehavior(new SpiderMissionBehavior());
+        mission.AddMissionBehavior(new SiegeDismountMissionBehavior());
 
         var colorStore = IoC.Resolve<IAgentColorStore>();
         if (colorStore != null)
warning: in the working copy of 'Main/Features/StartupResources/Config/StartupResourcesConfig.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Main/Features/StartupResources/StartupResourcesConfigProvider.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Main/Features/StartupResources/StartupResourcesIoC.cs', LF will be replaced by CRLF the next time Git touches it

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content Main\\Adapters\\IPlayerEquipmentAdapter.cs,Main\\Adapters\\PlayerEquipmentAdapter.cs,Main\\Features\\CharacterCreation\\IPlayerEquipmentService.cs,Main\\Features\\CharacterCreation\\PlayerEquipmentRosterIds.cs,Main\\Features\\CharacterCreation\\PlayerEquipmentService.cs,Main\\Features\\StartupResources\\IPlayerStartupGoldService.cs,Main\\Features\\StartupResources\\PlayerStartupGoldService.cs" in C:\Users\mikew\source\repos\TAOM
 succeeded in 450ms:
namespace TAOM.Adapters;

public enum PlayerEquipmentApplyResult
{
    Success,
    RosterNotFound,
    NoSuitableEquipment,
    HeroNotFound
}

public interface IPlayerEquipmentAdapter
{
    PlayerEquipmentApplyResult ApplyRosterToPlayer(string rosterId, string playerHeroId);
}
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace TAOM.Adapters;

public sealed class PlayerEquipmentAdapter : IPlayerEquipmentAdapter
{
    public PlayerEquipmentApplyResult ApplyRosterToPlayer(string rosterId, string playerHeroId)
    {
        var roster = MBObjectManager.Instance?.GetObject<MBEquipmentRoster>(rosterId);
        if (roster == null)
            return PlayerEquipmentApplyResult.RosterNotFound;

        var battle = roster.AllEquipments.FirstOrDefault(e => e.IsBattle);
        var civilian = roster.AllEquipments.FirstOrDefault(e => e.IsCivilian);
        if (battle == null && civilian == null)
            return PlayerEquipmentApplyResult.NoSuitableEquipment;

        var hero = Hero.FindFirst(h => h.StringId == playerHeroId);
        if (hero == null)
            return PlayerEquipmentApplyResult.HeroNotFound;

        // Hero.BattleEquipment / CivilianEquipment fall through to Campaign.Current.DeadBattleEquipment
        // (a process-wide shared singleton) when the hero's _battleEquipment is null. Calling FillFrom
        // on that fallback would corrupt equipment for every dead/uninitialized hero. MainHero at CC
        // finalize is always initialized, but the adapter takes any heroId — guard defensively.
        var dead = Campaign.Current?.DeadBattleEquipment;
        if (battle != null && hero.BattleEquipment != null && hero.BattleEquipment != dead)
            hero.BattleEquipment.FillFrom(battle);
        if (civilian != null && hero.CivilianEquipment != null && hero.CivilianEquipment != dead)
            hero.CivilianEquipment.FillFrom(civilian);

        return PlayerEquipmentApplyResult.Success;
    }
}
namespace TAOM.Features.CharacterCreation;

public interface IPlayerEquipmentService
{
    void ApplyPlayerStartingEquipment(string cultureId, string titleType, bool isFemale, string playerHeroId);
}
namespace TAOM.Features.CharacterCreation;

internal static class PlayerEquipmentRosterIds
{
    public static string Build(string cultureId, string titleType, bool isFemale)
    {
        return $"player_char_creation_{cultureId}_{titleType}_{(isFemale ? "f" : "m")}";
    }
}
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.CharacterCreation;

public class PlayerEquipmentService : IPlayerEquipmentService
{
    private readonly IPlayerEquipmentAdapter _adapter;
    private readonly IModLogger _logger;

    public PlayerEquipmentService(IPlayerEquipmentAdapter adapter, IModLogger logger)
    {
        _adapter = adapter;
        _logger = logger;
    }

    public void ApplyPlayerStartingEquipment(string cultureId, string titleType, bool isFemale, string playerHeroId)
    {
        if (string.IsNullOrEmpty(cultureId))
        {
            _logger.LogWarning("PlayerEquipmentService: cultureId is null or empty — skipping equipment apply");
            return;
        }

        if (string.IsNullOrEmpty(titleType))
        {
            _logger.LogWarning($"PlayerEquipmentService: titleType is null or empty for culture '{cultureId}' — skipping equipment apply");
            return;
        }

        if (string.IsNullOrEmpty(playerHeroId))
        {
            _logger.LogWarning($"PlayerEquipmentService: playerHeroId is null or empty for culture '{cultureId}' — skipping equipment apply");
            return;
        }

        var rosterId = PlayerEquipmentRosterIds.Build(cultureId, titleType, isFemale);
        var result = _adapter.ApplyRosterToPlayer(rosterId, playerHeroId);

        switch (result)
        {
            case PlayerEquipmentApplyResult.Success:
                _logger.LogInfo($"PlayerEquipmentService: applied roster '{rosterId}' to player ({cultureId}/{titleType})");
                break;
            case PlayerEquipmentApplyResult.RosterNotFound:
                _logger.LogWarning($"PlayerEquipmentService: equipment roster '{rosterId}' not found — no equipment applied");
                break;
            case PlayerEquipmentApplyResult.NoSuitableEquipment:
                _logger.LogWarning($"PlayerEquipmentService: roster '{rosterId}' has no battle or civilian equipment — no equipment applied");
                break;
            case PlayerEquipmentApplyResult.HeroNotFound:
                _logger.LogError($"PlayerEquipmentService: hero '{playerHeroId}' not found — equipment apply failed");
                break;
        }
    }
}
namespace TAOM.Features.StartupResources;

public interface IPlayerStartupGoldService
{
    void GrantPlayerStartupGold(string cultureId, string playerHeroId);
}
using System;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.StartupResources;

public class PlayerStartupGoldService : IPlayerStartupGoldService
{
    private readonly IGoldGiftAdapter _goldGiftAdapter;
    private readonly IStartupResourcesConfigProvider _configProvider;
    private readonly IModLogger _logger;

    public PlayerStartupGoldService(
        IGoldGiftAdapter goldGiftAdapter,
        IStartupResourcesConfigProvider configProvider,
        IModLogger logger)
    {
        _goldGiftAdapter = goldGiftAdapter;
        _configProvider = configProvider;
        _logger = logger;
    }

    public void GrantPlayerStartupGold(string cultureId, string playerHeroId)
    {
        if (string.IsNullOrEmpty(cultureId))
        {
            _logger.LogWarning("PlayerStartupGoldService: cultureId is null or empty — skipping grant");
            return;
        }

        if (string.IsNullOrEmpty(playerHeroId))
        {
            _logger.LogWarning($"PlayerStartupGoldService: playerHeroId is null or empty for culture '{cultureId}' — skipping grant");
            return;
        }

        var config = _configProvider.LoadConfig();
        var entry = config.CultureEntries.FirstOrDefault(
            e => string.Equals(e.CultureId, cultureId, StringComparison.OrdinalIgnoreCase));

        if (entry == null)
        {
            _logger.LogWarning($"PlayerStartupGoldService: no startup-resources entry for culture '{cultureId}' — no gold granted");
            return;
        }

        if (entry.PlayerGold <= 0)
            return;

        _goldGiftAdapter.GiveGoldToHero(playerHeroId, entry.PlayerGold);
        _logger.LogInfo($"PlayerStartupGoldService: granted {entry.PlayerGold} gold to player ({cultureId})");
    }
}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content Main\\Adapters\\IGoldGiftAdapter.cs,Main\\Adapters\\GoldGiftAdapter.cs,Main\\Features\\StartupResources\\StartupGoldService.cs,Main\\Features\\StartupResources\\StartupInfluenceService.cs,Main\\Features\\StartupResources\\IStartupResourcesConfigProvider.cs,Main\\Features\\StartupResources\\Config\\StartupResourcesConfig.cs" in C:\Users\mikew\source\repos\TAOM
 succeeded in 420ms:
namespace TAOM.Adapters;

public interface IGoldGiftAdapter
{
    void GiveGoldToHero(string heroId, int amount);
}
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace TAOM.Adapters;

public class GoldGiftAdapter : IGoldGiftAdapter
{
    public void GiveGoldToHero(string heroId, int amount)
    {
        var hero = Hero.FindFirst(h => h.StringId == heroId);
        if (hero == null)
            return;

        GiveGoldAction.ApplyBetweenCharacters(null, hero, amount, disableNotification: true);
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.StartupResources.Config;

namespace TAOM.Features.StartupResources;

public class StartupGoldService : IStartupGoldService
{
    private readonly IStartupHeroAdapter _heroAdapter;
    private readonly IGoldGiftAdapter _goldGiftAdapter;
    private readonly IStartupResourcesConfigProvider _configProvider;
    private readonly IModLogger _logger;

    public StartupGoldService(
        IStartupHeroAdapter heroAdapter,
        IGoldGiftAdapter goldGiftAdapter,
        IStartupResourcesConfigProvider configProvider,
        IModLogger logger)
    {
        _heroAdapter = heroAdapter;
        _goldGiftAdapter = goldGiftAdapter;
        _configProvider = configProvider;
        _logger = logger;
    }

    public void DistributeStartupGold()
    {
        var config = _configProvider.LoadConfig();
        var lookup = BuildCultureLookup(config);
        var heroes = _heroAdapter.GetAliveLordHeroes();

        int totalGold = 0;
        int lordCount = 0;

        foreach (var hero in heroes)
        {
            if (hero.IsPlayerClan)
                continue;

            if (lookup.TryGetValue(hero.CultureId.ToLowerInvariant(), out var entry) && entry.Gold > 0)
            {
                _goldGiftAdapter.GiveGoldToHero(hero.HeroId, entry.Gold);
                totalGold += entry.Gold;
                lordCount++;
            }
        }

        _logger.LogInfo($"StartupGold: distributed {totalGold} gold to {lordCount} lords");
    }

    private static Dictionary<string, CultureResourceEntry> BuildCultureLookup(StartupResourcesConfig config)
    {
        var lookup = new Dictionary<string, CultureResourceEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in config.CultureEntries)
        {
            if (!string.IsNullOrEmpty(entry.CultureId))
                lookup[entry.CultureId.ToLowerInvariant()] = entry;
        }
        return lookup;
    }
}
using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.StartupResources.Config;

namespace TAOM.Features.StartupResources;

public class StartupInfluenceService : IStartupInfluenceService
{
    private readonly IClanStartupAdapter _clanAdapter;
    private readonly IStartupResourcesConfigProvider _configProvider;
    private readonly IModLogger _logger;

    public StartupInfluenceService(
        IClanStartupAdapter clanAdapter,
        IStartupResourcesConfigProvider configProvider,
        IModLogger logger)
    {
        _clanAdapter = clanAdapter;
        _configProvider = configProvider;
        _logger = logger;
    }

    public void DistributeStartupInfluence()
    {
        var config = _configProvider.LoadConfig();
        var lookup = BuildCultureLookup(config);
        var clans = _clanAdapter.GetEligibleClans();

        float totalInfluence = 0f;
        int clanCount = 0;

        foreach (var clan in clans)
        {
            if (lookup.TryGetValue(clan.CultureId.ToLowerInvariant(), out var entry) && entry.Influence > 0f)
            {
                _clanAdapter.AddInfluence(clan.ClanId, entry.Influence);
                totalInfluence += entry.Influence;
                clanCount++;
            }
        }

        _logger.LogInfo($"StartupInfluence: added {totalInfluence} influence to {clanCount} clans");
    }

    private static Dictionary<string, CultureResourceEntry> BuildCultureLookup(StartupResourcesConfig config)
    {
        var lookup = new Dictionary<string, CultureResourceEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in config.CultureEntries)
        {
            if (!string.IsNullOrEmpty(entry.CultureId))
                lookup[entry.CultureId.ToLowerInvariant()] = entry;
        }
        return lookup;
    }
}
using TAOM.Features.StartupResources.Config;

namespace TAOM.Features.StartupResources;

public interface IStartupResourcesConfigProvider
{
    StartupResourcesConfig LoadConfig();
}
using System.Collections.Generic;

namespace TAOM.Features.StartupResources.Config;

public class StartupResourcesConfig
{
    public List<CultureResourceEntry> CultureEntries { get; set; } = new List<CultureResourceEntry>();
}

public class CultureResourceEntry
{
    public string CultureId { get; set; }
    public int Gold { get; set; }
    public float Influence { get; set; }
    public int PlayerGold { get; set; }
}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content Main'"\\Features\\CharacterCreation\\CharacterCreationContentService.cs | ForEach-Object { '{0,4}: {1}' -f "'$i, $_; $i++ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 448ms:
   1: using System;
   2: using System.Collections.Generic;
   3: using System.Linq;
   4: using TaleWorlds.CampaignSystem;
   5: using TaleWorlds.CampaignSystem.CharacterCreationContent;
   6: using TaleWorlds.CampaignSystem.Party;
   7: using TaleWorlds.CampaignSystem.Settlements;
   8: using TaleWorlds.ObjectSystem;
   9: using TAOM.Adapters;
  10: using TAOM.Core.Domain;
  11: using TAOM.Core.Logging;
  12: using TAOM.Features.CharacterCreation.Models;
  13: using TAOM.Features.StartupResources;
  14: 
  15: namespace TAOM.Features.CharacterCreation;
  16: 
  17: public class CharacterCreationContentService : ICharacterCreationContentService
  18: {
  19:     private const string ParentMenuId = "narrative_parent_menu";
  20:     private const string ChildhoodMenuId = "narrative_childhood_menu";
  21:     private const string EducationMenuId = "narrative_education_menu";
  22:     private const string YouthMenuId = "narrative_youth_menu";
  23:     private const string AdulthoodMenuId = "narrative_adulthood_menu";
  24: 
  25:     private readonly ICultureCreationDataProvider _dataProvider;
  26:     private readonly INarrativeDataProvider _narrativeDataProvider;
  27:     private readonly IRaceManager _raceManager;
  28:     private readonly IHeroRosterAdapter _heroRosterAdapter;
  29:     private readonly IEquipmentRosterProvider _equipmentRosterProvider;
  30:     private readonly ICareerMenuService _careerMenuService;
  31:     private readonly IPlayerStartupGoldService _playerStartupGoldService;
  32:     private readonly IPlayerEquipmentService _playerEquipmentService;
  33:     private readonly IModLogger _logger;
  34: 
  35:     // Vanilla cultures already registered by SandBox handler — skip these
  36:     private static readonly HashSet<string> VanillaCultureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
  37:     {
  38:         "empire", "vlandia", "sturgia", "aserai", "battania", "khuzait"
  39:     };
  40: 
  41:     public CharacterCreationContentService(
  42:         ICultureCreationDataProvider dataProvider,
  43:         INarrativeDataProvider narrativeDataProvider,
  44:         IRaceManager raceManager,
  45:         IHeroRosterAdapter heroRosterAdapter,
  46:         IEquipmentRosterProvider equipmentRosterProvider,
  47:         ICareerMenuService careerMenuService,
  48:         IPlayerStartupGoldService playerStartupGoldService,
  49:         IPlayerEquipmentService playerEquipmentService,
  50:         IModLogger logger)
  51:     {
  52:         _dataProvider = dataProvider;
  53:         _narrativeDataProvider = narrativeDataProvider;
  54:         _raceManager = raceManager;
  55:         _heroRosterAdapter = heroRosterAdapter;
  56:         _equipmentRosterProvider = equipmentRosterProvider;
  57:         _careerMenuService = careerMenuService;
  58:         _playerStartupGoldService = playerStartupGoldService;
  59:         _playerEquipmentService = playerEquipmentService;
  60:         _logger = logger;
  61:     }
  62: 
  63:     public void RegisterCustomCultures(CharacterCreationManager manager)
  64:     {
  65:         var cultures = _dataProvider.LoadCultures();
  66:         int registered = 0;
  67: 
  68:         foreach (var cultureData in cultures)
  69:         {
  70:             if (VanillaCultureIds.Contains(cultureData.CultureId))
  71:                 continue;
  72: 
  73:             var cultureObject = GetCultureObject(cultureData.CultureId);
  74:             if (cultureObject == null)
  75:             {
  76:                 _logger.LogWarning($"Culture '{cultureData.CultureId}' not found in MBObjectManager — skipping");
  77:                 continue;
  78:             }
  79: 
  80:             try
  81:             {
  82:                 manager.CharacterCreationContent.AddCharacterCreationCulture(
  83:                     cultureObject,
  84:                     cultureData.FocusToAdd,
  85:                     cultureData.SkillLevelToAdd);
  86:                 registered++;
  87:             }
  88:             catch (Exception ex)
  89:             {
  90:                 _logger.LogError($"Failed to register culture '{cultureData.CultureId}': {ex.Message}");
  91:             }
  92:         }
  93: 
  94:         _logger.LogInfo($"Registered {registered} custom cultures for character creation");
  95:     }
  96: 
  97:     public void RegisterNarrativeMenus(CharacterCreationManager manager)
  98:     {
  99:         var builder = new NarrativeMenuBuilder(_logger, _equipmentRosterProvider);
 100: 
 101:         ReplaceMenuOptions(manager, builder, ParentMenuId,    "parents");
 102:         ReplaceMenuOptions(manager, builder, ChildhoodMenuId, "childhood");
 103:         ReplaceMenuOptions(manager, builder, EducationMenuId, "education");
 104:         ReplaceMenuOptions(manager, builder, YouthMenuId,     "youth");
 105:         ReplaceMenuOptions(manager, builder, AdulthoodMenuId, "adulthood");
 106:     }
 107: 
 108:     public void RegisterCareerMenu(CharacterCreationManager manager)
 109:     {
 110:         _careerMenuService.RegisterCareerMenu(manager);
 111:     }
 112: 
 113:     private void ReplaceMenuOptions(
 114:         CharacterCreationManager manager,
 115:         NarrativeMenuBuilder builder,
 116:         string menuId,
 117:         string dataFileName)
 118:     {
 119:         var menu = manager.GetNarrativeMenuWithId(menuId);
 120:         if (menu == null)
 121:         {
 122:             _logger.LogError($"Narrative menu '{menuId}' not found — SandBox handler may not have run");
 123:             return;
 124:         }
 125: 
 126:         RemoveVanillaOptions(menu, menuId);
 127: 
 128:         var options = _narrativeDataProvider.LoadMenuOptions(dataFileName);
 129:         int added = builder.AddOptionsToMenu(menu, options);
 130: 
 131:         _logger.LogInfo($"[{menuId}] Added {added} TAOM narrative options");
 132:     }
 133: 
 134:     private void RemoveVanillaOptions(NarrativeMenu menu, string menuId)
 135:     {
 136:         var vanillaOptions = menu.CharacterCreationMenuOptions
 137:             .Where(o => !o.StringId.StartsWith("taom_", StringComparison.OrdinalIgnoreCase))
 138:             .ToList();
 139: 
 140:         foreach (var option in vanillaOptions)
 141:         {
 142:             menu.RemoveNarrativeMenuOption(option);
 143:         }
 144: 
 145:         _logger.LogInfo($"[{menuId}] Removed {vanillaOptions.Count} vanilla narrative options");
 146:     }
 147: 
 148:     public void OnCharacterCreationFinalize(CharacterCreationManager manager)
 149:     {
 150:         var selectedCulture = manager.CharacterCreationContent.SelectedCulture;
 151:         if (selectedCulture == null)
 152:         {
 153:             _logger.LogWarning("No culture selected at finalization");
 154:             return;
 155:         }
 156: 
 157:         var cultureData = _dataProvider.GetCultureData(selectedCulture.StringId);
 158:         if (cultureData == null)
 159:         {
 160:             _logger.LogWarning($"No culture data found for '{selectedCulture.StringId}' — using defaults");
 161:             return;
 162:         }
 163: 
 164:         // BL's ApplyCulture() should have set Hero.Culture = SelectedCulture already.
 165:         // Log and force-set as safety net for custom cultures.
 166:         var heroCultureBefore = Hero.MainHero?.Culture?.StringId ?? "null";
 167:         _logger.LogInfo($"CC Finalize: SelectedCulture='{selectedCulture.StringId}', Hero.Culture before='{heroCultureBefore}'");
 168: 
 169:         if (Hero.MainHero != null && Hero.MainHero.Culture?.StringId != selectedCulture.StringId)
 170:         {
 171:             Hero.MainHero.Culture = selectedCulture;
 172:             _logger.LogInfo($"CC Finalize: Force-set Hero.Culture to '{selectedCulture.StringId}' (was '{heroCultureBefore}')");
 173:         }
 174: 
 175:         TeleportToStartingSettlement(cultureData);
 176:         SetPlayerRace(cultureData, Hero.MainHero?.StringId);
 177:         AssignCareer(selectedCulture.StringId, Hero.MainHero?.StringId);
 178:         GrantPlayerStartupResources(selectedCulture.StringId, manager);
 179:     }
 180: 
 181:     private void GrantPlayerStartupResources(string cultureId, CharacterCreationManager manager)
 182:     {
 183:         var heroId = Hero.MainHero?.StringId;
 184:         if (string.IsNullOrEmpty(heroId))
 185:         {
 186:             _logger.LogWarning("CC Finalize: Hero.MainHero is null — skipping player startup gold + equipment");
 187:             return;
 188:         }
 189: 
 190:         try
 191:         {
 192:             _playerStartupGoldService.GrantPlayerStartupGold(cultureId, heroId);
 193:         }
 194:         catch (Exception ex)
 195:         {
 196:             _logger.LogError($"CC Finalize: player startup gold failed: {ex.Message}");
 197:         }
 198: 
 199:         try
 200:         {
 201:             var titleType = manager.CharacterCreationContent?.SelectedTitleType;
 202:             var isFemale = Hero.MainHero?.IsFemale ?? false;
 203:             _playerEquipmentService.ApplyPlayerStartingEquipment(cultureId, titleType, isFemale, heroId);
 204:         }
 205:         catch (Exception ex)
 206:         {
 207:             _logger.LogError($"CC Finalize: player starting equipment failed: {ex.Message}");
 208:         }
 209:     }
 210: 
 211:     private void AssignCareer(string cultureId, string heroStringId)
 212:     {
 213:         if (string.IsNullOrEmpty(heroStringId) || string.IsNullOrEmpty(cultureId))
 214:             return;
 215: 
 216:         try
 217:         {
 218:             var handler = IoC.Resolve<CareerSystem.ICareerCreationHandler>();
 219:             if (handler == null)
 220:             {
 221:                 _logger.LogWarning("CareerSystem: Cannot assign career at CC — handler not resolved");
 222:                 return;
 223:             }
 224: 
 225:             // Use player's career menu selection if available
 226:             var selectedCareerId = _careerMenuService?.SelectedCareerStringId;
 227:             if (!string.IsNullOrEmpty(selectedCareerId))
 228:             {
 229:                 handler.OnCareerSelected(heroStringId, selectedCareerId);
 230:                 _logger.LogInfo($"CareerSystem: Assigned player-selected career '{selectedCareerId}' during CC");
 231:                 return;
 232:             }
 233: 
 234:             // Fallback: auto-assign first eligible career (legacy/backward compat)
 235:             var registry = IoC.Resolve<CareerSystem.ICareerRegistry>();
 236:             if (registry == null)
 237:             {
 238:                 _logger.LogWarning("CareerSystem: Cannot assign career at CC — registry not resolved");
 239:                 return;
 240:             }
 241: 
 242:             foreach (var career in registry.GetAllCareers())
 243:             {
 244:                 foreach (var eligibleCulture in career.EligibleCultureIds)
 245:                 {
 246:                     if (string.Equals(eligibleCulture, cultureId, StringComparison.OrdinalIgnoreCase))
 247:                     {
 248:                         handler.OnCareerSelected(heroStringId, career.Id);
 249:                         _logger.LogInfo($"CareerSystem: Auto-assigned career '{career.Id}' during CC for culture '{cultureId}' (no player selection)");
 250:                         return;
 251:                     }
 252:                 }
 253:             }
 254: 
 255:             _logger.LogInfo($"CareerSystem: No eligible career found for culture '{cultureId}' during CC");
 256:         }
 257:         catch (Exception ex)
 258:         {
 259:             _logger.LogError($"CareerSystem: Failed to assign career during CC: {ex.Message}");
 260:         }
 261:     }
 262: 
 263:     internal void SetPlayerRace(CultureCreationData cultureData, string heroStringId)
 264:     {
 265:         if (string.IsNullOrEmpty(heroStringId))
 266:         {
 267:             _logger.LogWarning("Cannot set player race — hero string ID is null");
 268:             return;
 269:         }
 270: 
 271:         var fallbackRaceName = cultureData.Races != null && cultureData.Races.Length > 0
 272:             ? cultureData.Races[0]
 273:             : "human";
 274: 
 275:         try
 276:         {
 277:             // Bannerlord assigns Hero.CharacterObject.Race from FaceGen output before finalize runs.
 278:             // Preserve the player's actual choice when it's in the culture's allowed list; otherwise
 279:             // fall back to the culture default. Without this check, a Mordor player who picks "human"
 280:             // in the FaceGen dropdown would be overridden to "uruk" (Races[0]) at finalize.
 281:             //
 282:             // Codex review #N (2026-05-06) caught: GetRaceNameFromId silently returns "human" for
 283:             // unknown IDs (RaceManager.cs:126-130). Without IsValidRaceId gating, an invalid ID
 284:             // would be coerced to "human", and if the culture allows "human", we would preserve
 285:             // a value the player never picked. Validate the ID before accepting the FaceGen choice.
 286:             var faceGenRaceId = _heroRosterAdapter.GetHeroRace(heroStringId);
 287:             var faceGenRaceIdValid = _raceManager.IsValidRaceId(faceGenRaceId);
 288:             var faceGenRaceName = faceGenRaceIdValid ? _raceManager.GetRaceNameFromId(faceGenRaceId) : null;
 289: 
 290:             bool faceGenChoiceAllowed = faceGenRaceIdValid
 291:                 && cultureData.Races != null
 292:                 && cultureData.Races.Length > 0
 293:                 && cultureData.Races.Any(r => string.Equals(r, faceGenRaceName, StringComparison.OrdinalIgnoreCase));
 294: 
 295:             string raceName;
 296:             int raceId;
 297:             if (faceGenChoiceAllowed)
 298:             {
 299:                 raceName = faceGenRaceName;
 300:                 raceId = faceGenRaceId;
 301:             }
 302:             else
 303:             {
 304:                 raceName = fallbackRaceName;
 305:                 raceId = _raceManager.GetRaceIdFromName(raceName);
 306:             }
 307: 
 308:             _heroRosterAdapter.SetHeroRace(heroStringId, raceId);
 309:             if (faceGenChoiceAllowed)
 310:                 _logger.LogInfo($"Set player race to '{raceName}' (id: {raceId}) — preserved FaceGen selection");
 311:             else
 312:                 _logger.LogInfo($"Set player race to '{raceName}' (id: {raceId}) — fell back to culture default (FaceGen was '{faceGenRaceName}')");
 313:         }
 314:         catch (Exception ex)
 315:         {
 316:             _logger.LogError($"Failed to set player race: {ex.Message}");
 317:         }
 318:     }
 319: 
 320:     private void TeleportToStartingSettlement(CultureCreationData cultureData)
 321:     {
 322:         if (string.IsNullOrEmpty(cultureData.StartingSettlement))
 323:             return;
 324: 
 325:         try
 326:         {
 327:             var settlement = Settlement.Find(cultureData.StartingSettlement);
 328:             if (settlement != null)
 329:             {
 330:                 var position = settlement.GatePosition;
 331:                 MobileParty.MainParty.Position = position.IsNonZero() ? position : settlement.Position;
 332:                 _logger.LogInfo($"Teleported to starting settlement: {cultureData.StartingSettlement}");
 333:             }
 334:             else
 335:             {
 336:                 _logger.LogWarning($"Starting settlement not found: {cultureData.StartingSettlement}");
 337:             }
 338:         }
 339:         catch (Exception ex)
 340:         {
 341:             _logger.LogError($"Failed to teleport to starting settlement: {ex.Message}");
 342:         }
 343:     }
 344: 
 345:     private static CultureObject GetCultureObject(string cultureId)
 346:     {
 347:         return MBObjectManager.Instance?.GetObject<CultureObject>(cultureId);
 348:     }
 349: }

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content Main\\Features\\CharacterCreation\\ICharacterCreationContentService.cs; Get-ChildItem Main\\Features\\CharacterCreation -Recurse -File | Select-String -Pattern \"OnCharacterCreationFinalize|RegisterCustomCultures\" -Context 2" in C:\Users\mikew\source\repos\TAOM
 succeeded in 485ms:
using TaleWorlds.CampaignSystem.CharacterCreationContent;

namespace TAOM.Features.CharacterCreation;

public interface ICharacterCreationContentService
{
    void RegisterCustomCultures(CharacterCreationManager manager);
    void RegisterNarrativeMenus(CharacterCreationManager manager);
    void RegisterCareerMenu(CharacterCreationManager manager);
    void OnCharacterCreationFinalize(CharacterCreationManager manager);
}

  Main\Features\CharacterCreation\CharacterCreationContentService.cs:61:    }
  Main\Features\CharacterCreation\CharacterCreationContentService.cs:62:
> Main\Features\CharacterCreation\CharacterCreationContentService.cs:63:    public void 
[7mRegisterCustomCultures[0m(CharacterCreationManager [0m[7m[0mmanager)[0m
[7m[0m  Main\Features\CharacterCreation\CharacterCreationContentService.cs:64:    {[0m
[7m [0m[7m [0m[7m[0mMain\Features\CharacterCreation\CharacterCreationContentService.cs:65: [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m[0mvar [0m[7m[0mcultures [0m[7m[0m= [0m
[7m[0m_dataProvider.LoadCultures();[0m
  Main\Features\CharacterCreation\CharacterCreationContentService.cs:146:    }
  Main\Features\CharacterCreation\CharacterCreationContentService.cs:147:
> Main\Features\CharacterCreation\CharacterCreationContentService.cs:148:    public void 
[7mOnCharacterCreationFinalize[0m(CharacterCreationManager [0m[7m[0mmanager)[0m
[7m[0m  Main\Features\CharacterCreation\CharacterCreationContentService.cs:149:    {[0m
[7m [0m[7m [0m[7m[0mMain\Features\CharacterCreation\CharacterCreationContentService.cs:150: [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m[0mvar [0m[7m[0mselectedCulture [0m[7m[0m= [0m
[7m[0mmanager.CharacterCreationContent.SelectedCulture;[0m
  Main\Features\CharacterCreation\ICharacterCreationContentService.cs:5:public interface 
ICharacterCreationContentService
  Main\Features\CharacterCreation\ICharacterCreationContentService.cs:6:{
> Main\Features\CharacterCreation\ICharacterCreationContentService.cs:7:    void 
[7mRegisterCustomCultures[0m(CharacterCreationManager [0m[7m[0mmanager);[0m
[7m [0m[7m [0m[7m[0mMain\Features\CharacterCreation\ICharacterCreationContentService.cs:8: [0m[7m [0m[7m [0m[7m [0m[7m[0mvoid [0m
[7m[0mRegisterNarrativeMenus(CharacterCreationManager [0m[7m[0mmanager);[0m
[7m [0m[7m [0m[7m[0mMain\Features\CharacterCreation\ICharacterCreationContentService.cs:9: [0m[7m [0m[7m [0m[7m [0m[7m[0mvoid [0m
[7m[0mRegisterCareerMenu(CharacterCreationManager [0m[7m[0mmanager);[0m
> Main\Features\CharacterCreation\ICharacterCreationContentService.cs:10:    void 
[7mOnCharacterCreationFinalize[0m(CharacterCreationManager [0m[7m[0mmanager);[0m
[7m[0m  Main\Features\CharacterCreation\ICharacterCreationContentService.cs:11:}[0m
  Main\Features\CharacterCreation\TaomCharacterCreationContentHandler.cs:25:    public void 
AfterInitializeContent(CharacterCreationManager characterCreationManager)
  Main\Features\CharacterCreation\TaomCharacterCreationContentHandler.cs:26:    {
> Main\Features\CharacterCreation\TaomCharacterCreationContentHandler.cs:27:        
_contentService.[7mRegisterCustomCultures[0m(characterCreationManager);[0m
[7m [0m[7m [0m[7m[0mMain\Features\CharacterCreation\TaomCharacterCreationContentHandler.cs:28: [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m
[7m[0m_contentService.RegisterNarrativeMenus(characterCreationManager);[0m
[7m [0m[7m [0m[7m[0mMain\Features\CharacterCreation\TaomCharacterCreationContentHandler.cs:29: [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m
[7m[0m_contentService.RegisterCareerMenu(characterCreationManager);[0m
  Main\Features\CharacterCreation\TaomCharacterCreationContentHandler.cs:36:    }
  Main\Features\CharacterCreation\TaomCharacterCreationContentHandler.cs:37:
> Main\Features\CharacterCreation\TaomCharacterCreationContentHandler.cs:38:    public void 
[7mOnCharacterCreationFinalize[0m(CharacterCreationManager [0m[7m[0mcharacterCreationManager)[0m
[7m[0m  Main\Features\CharacterCreation\TaomCharacterCreationContentHandler.cs:39:    {[0m
> Main\Features\CharacterCreation\TaomCharacterCreationContentHandler.cs:40:        
_contentService.[7mOnCharacterCreationFinalize[0m(characterCreationManager);[0m
[7m[0m  Main\Features\CharacterCreation\TaomCharacterCreationContentHandler.cs:41:    }[0m
[7m[0m  Main\Features\CharacterCreation\TaomCharacterCreationContentHandler.cs:42:}[0m


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content TAOM.Tests\\Features\\StartupResources\\PlayerStartupGoldServiceTests.cs,TAOM.Tests\\Features\\CharacterCreation\\PlayerEquipmentServiceTests.cs,TAOM.Tests\\Features\\StartupResources\\StartupResourcesConfigProviderTests.cs" in C:\Users\mikew\source\repos\TAOM
 succeeded in 456ms:
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.StartupResources;
using TAOM.Features.StartupResources.Config;

namespace TAOM.Tests.Features.StartupResources;

[TestClass]
public class PlayerStartupGoldServiceTests
{
    private IGoldGiftAdapter _goldGiftAdapter;
    private IStartupResourcesConfigProvider _configProvider;
    private IModLogger _logger;
    private PlayerStartupGoldService _sut;

    [TestInitialize]
    public void Setup()
    {
        _goldGiftAdapter = Substitute.For<IGoldGiftAdapter>();
        _configProvider = Substitute.For<IStartupResourcesConfigProvider>();
        _logger = Substitute.For<IModLogger>();
        _configProvider.LoadConfig().Returns(new StartupResourcesConfig());

        _sut = new PlayerStartupGoldService(_goldGiftAdapter, _configProvider, _logger);
    }

    [TestMethod]
    public void GrantPlayerStartupGold_ConfiguredCulture_GivesGold()
    {
        var config = new StartupResourcesConfig
        {
            CultureEntries = new List<CultureResourceEntry>
            {
                new CultureResourceEntry { CultureId = "gondor", PlayerGold = 5000 }
            }
        };
        _configProvider.LoadConfig().Returns(config);

        _sut.GrantPlayerStartupGold("gondor", "player_hero_id");

        _goldGiftAdapter.Received(1).GiveGoldToHero("player_hero_id", 5000);
    }

    [TestMethod]
    public void GrantPlayerStartupGold_CaseInsensitiveCulture_MatchesCorrectly()
    {
        var config = new StartupResourcesConfig
        {
            CultureEntries = new List<CultureResourceEntry>
            {
                new CultureResourceEntry { CultureId = "gondor", PlayerGold = 5000 }
            }
        };
        _configProvider.LoadConfig().Returns(config);

        _sut.GrantPlayerStartupGold("Gondor", "player_hero_id");

        _goldGiftAdapter.Received(1).GiveGoldToHero("player_hero_id", 5000);
    }

    [TestMethod]
    public void GrantPlayerStartupGold_CultureNotInConfig_NoGoldGivenAndWarns()
    {
        var config = new StartupResourcesConfig
        {
            CultureEntries = new List<CultureResourceEntry>
            {
                new CultureResourceEntry { CultureId = "gondor", PlayerGold = 5000 }
            }
        };
        _configProvider.LoadConfig().Returns(config);

        _sut.GrantPlayerStartupGold("unknown_culture", "player_hero_id");

        _goldGiftAdapter.DidNotReceiveWithAnyArgs().GiveGoldToHero(default, default);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("unknown_culture")));
    }

    [TestMethod]
    public void GrantPlayerStartupGold_ZeroPlayerGold_SkipsGrant()
    {
        var config = new StartupResourcesConfig
        {
            CultureEntries = new List<CultureResourceEntry>
            {
                new CultureResourceEntry { CultureId = "gondor", PlayerGold = 0 }
            }
        };
        _configProvider.LoadConfig().Returns(config);

        _sut.GrantPlayerStartupGold("gondor", "player_hero_id");

        _goldGiftAdapter.DidNotReceiveWithAnyArgs().GiveGoldToHero(default, default);
    }

    [TestMethod]
    public void GrantPlayerStartupGold_NullCultureId_NoOpAndWarns()
    {
        _sut.GrantPlayerStartupGold(null, "player_hero_id");

        _goldGiftAdapter.DidNotReceiveWithAnyArgs().GiveGoldToHero(default, default);
        _logger.Received().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GrantPlayerStartupGold_EmptyCultureId_NoOpAndWarns()
    {
        _sut.GrantPlayerStartupGold(string.Empty, "player_hero_id");

        _goldGiftAdapter.DidNotReceiveWithAnyArgs().GiveGoldToHero(default, default);
        _logger.Received().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GrantPlayerStartupGold_NullHeroId_NoOpAndWarns()
    {
        var config = new StartupResourcesConfig
        {
            CultureEntries = new List<CultureResourceEntry>
            {
                new CultureResourceEntry { CultureId = "gondor", PlayerGold = 5000 }
            }
        };
        _configProvider.LoadConfig().Returns(config);

        _sut.GrantPlayerStartupGold("gondor", null);

        _goldGiftAdapter.DidNotReceiveWithAnyArgs().GiveGoldToHero(default, default);
        _logger.Received().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GrantPlayerStartupGold_HappyPath_LogsAmount()
    {
        var config = new StartupResourcesConfig
        {
            CultureEntries = new List<CultureResourceEntry>
            {
                new CultureResourceEntry { CultureId = "mordor", PlayerGold = 7500 }
            }
        };
        _configProvider.LoadConfig().Returns(config);

        _sut.GrantPlayerStartupGold("mordor", "player_hero_id");

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("7500") && s.Contains("mordor")));
    }
}
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;

namespace TAOM.Tests.Features.CharacterCreation;

[TestClass]
public class PlayerEquipmentServiceTests
{
    private IPlayerEquipmentAdapter _adapter;
    private IModLogger _logger;
    private PlayerEquipmentService _sut;

    [TestInitialize]
    public void Setup()
    {
        _adapter = Substitute.For<IPlayerEquipmentAdapter>();
        _logger = Substitute.For<IModLogger>();
        _sut = new PlayerEquipmentService(_adapter, _logger);
    }

    [TestMethod]
    public void ApplyPlayerStartingEquipment_HappyPathMale_BuildsRosterIdAndCallsAdapter()
    {
        _adapter
            .ApplyRosterToPlayer("player_char_creation_gondor_retainer_m", "player_hero_id")
            .Returns(PlayerEquipmentApplyResult.Success);

        _sut.ApplyPlayerStartingEquipment("gondor", "retainer", isFemale: false, "player_hero_id");

        _adapter.Received(1).ApplyRosterToPlayer("player_char_creation_gondor_retainer_m", "player_hero_id");
    }

    [TestMethod]
    public void ApplyPlayerStartingEquipment_HappyPathFemale_BuildsFemaleSuffixedRosterId()
    {
        _adapter
            .ApplyRosterToPlayer("player_char_creation_rivendell_warrior_f", "player_hero_id")
            .Returns(PlayerEquipmentApplyResult.Success);

        _sut.ApplyPlayerStartingEquipment("rivendell", "warrior", isFemale: true, "player_hero_id");

        _adapter.Received(1).ApplyRosterToPlayer("player_char_creation_rivendell_warrior_f", "player_hero_id");
    }

    [TestMethod]
    public void ApplyPlayerStartingEquipment_NullCulture_NoOpAndWarns()
    {
        _sut.ApplyPlayerStartingEquipment(null, "retainer", false, "player_hero_id");

        _adapter.DidNotReceiveWithAnyArgs().ApplyRosterToPlayer(default, default);
        _logger.Received().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ApplyPlayerStartingEquipment_EmptyTitleType_NoOpAndWarns()
    {
        _sut.ApplyPlayerStartingEquipment("gondor", string.Empty, false, "player_hero_id");

        _adapter.DidNotReceiveWithAnyArgs().ApplyRosterToPlayer(default, default);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("titleType")));
    }

    [TestMethod]
    public void ApplyPlayerStartingEquipment_NullHeroId_NoOpAndWarns()
    {
        _sut.ApplyPlayerStartingEquipment("gondor", "retainer", false, null);

        _adapter.DidNotReceiveWithAnyArgs().ApplyRosterToPlayer(default, default);
        _logger.Received().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ApplyPlayerStartingEquipment_RosterNotFound_LogsWarning()
    {
        _adapter
            .ApplyRosterToPlayer(Arg.Any<string>(), Arg.Any<string>())
            .Returns(PlayerEquipmentApplyResult.RosterNotFound);

        _sut.ApplyPlayerStartingEquipment("gondor", "retainer", false, "player_hero_id");

        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("player_char_creation_gondor_retainer_m")));
    }

    [TestMethod]
    public void ApplyPlayerStartingEquipment_NoSuitableEquipment_LogsWarning()
    {
        _adapter
            .ApplyRosterToPlayer(Arg.Any<string>(), Arg.Any<string>())
            .Returns(PlayerEquipmentApplyResult.NoSuitableEquipment);

        _sut.ApplyPlayerStartingEquipment("gondor", "retainer", false, "player_hero_id");

        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("no battle or civilian")));
    }

    [TestMethod]
    public void ApplyPlayerStartingEquipment_HeroNotFound_LogsError()
    {
        _adapter
            .ApplyRosterToPlayer(Arg.Any<string>(), Arg.Any<string>())
            .Returns(PlayerEquipmentApplyResult.HeroNotFound);

        _sut.ApplyPlayerStartingEquipment("gondor", "retainer", false, "missing_hero");

        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("missing_hero")));
    }

    [TestMethod]
    public void ApplyPlayerStartingEquipment_Success_LogsInfo()
    {
        _adapter
            .ApplyRosterToPlayer(Arg.Any<string>(), Arg.Any<string>())
            .Returns(PlayerEquipmentApplyResult.Success);

        _sut.ApplyPlayerStartingEquipment("gondor", "retainer", false, "player_hero_id");

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("gondor") && s.Contains("retainer")));
    }
}
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.StartupResources;

namespace TAOM.Tests.Features.StartupResources;

[TestClass]
public class StartupResourcesConfigProviderTests
{
    private string _tempDir;
    private IPathService _pathService;
    private IModLogger _logger;
    private StartupResourcesConfigProvider _sut;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_Tests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_tempDir, "startup_resources"));

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new StartupResourcesConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [TestMethod]
    public void LoadConfig_ValidXml_ParsesAllCultures()
    {
        WriteConfig(@"<?xml version=""1.0"" encoding=""utf-8""?>
<StartupResources>
  <Culture id=""rivendell"" gold=""6000000"" influence=""2000"" />
  <Culture id=""gondor"" gold=""500000"" influence=""100"" />
</StartupResources>");

        var config = _sut.LoadConfig();

        Assert.AreEqual(2, config.CultureEntries.Count);
        Assert.AreEqual("rivendell", config.CultureEntries[0].CultureId);
        Assert.AreEqual(6000000, config.CultureEntries[0].Gold);
        Assert.AreEqual(2000f, config.CultureEntries[0].Influence);
        Assert.AreEqual("gondor", config.CultureEntries[1].CultureId);
        Assert.AreEqual(500000, config.CultureEntries[1].Gold);
        Assert.AreEqual(100f, config.CultureEntries[1].Influence);
    }

    [TestMethod]
    public void LoadConfig_MissingFile_ReturnsEmptyConfigAndLogs()
    {
        var config = _sut.LoadConfig();

        Assert.AreEqual(0, config.CultureEntries.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void LoadConfig_MalformedXml_ReturnsEmptyConfigAndLogs()
    {
        WriteConfig("not valid xml {{{");

        var config = _sut.LoadConfig();

        Assert.AreEqual(0, config.CultureEntries.Count);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void LoadConfig_CachesResult()
    {
        WriteConfig(@"<?xml version=""1.0"" encoding=""utf-8""?>
<StartupResources>
  <Culture id=""gondor"" gold=""500000"" influence=""100"" />
</StartupResources>");

        var first = _sut.LoadConfig();
        var second = _sut.LoadConfig();

        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void LoadConfig_DecimalInfluence_ParsesCorrectly()
    {
        WriteConfig(@"<?xml version=""1.0"" encoding=""utf-8""?>
<StartupResources>
  <Culture id=""erebor"" gold=""1000000"" influence=""1500.5"" />
</StartupResources>");

        var config = _sut.LoadConfig();

        Assert.AreEqual(1500.5f, config.CultureEntries[0].Influence, 0.01f);
    }

    [TestMethod]
    public void LoadConfig_MissingAttributes_DefaultsToZero()
    {
        WriteConfig(@"<?xml version=""1.0"" encoding=""utf-8""?>
<StartupResources>
  <Culture id=""gondor"" />
</StartupResources>");

        var config = _sut.LoadConfig();

        Assert.AreEqual(1, config.CultureEntries.Count);
        Assert.AreEqual("gondor", config.CultureEntries[0].CultureId);
        Assert.AreEqual(0, config.CultureEntries[0].Gold);
        Assert.AreEqual(0f, config.CultureEntries[0].Influence);
        Assert.AreEqual(0, config.CultureEntries[0].PlayerGold);
    }

    [TestMethod]
    public void LoadConfig_PlayerGoldAttribute_Parsed()
    {
        WriteConfig(@"<?xml version=""1.0"" encoding=""utf-8""?>
<StartupResources>
  <Culture id=""gondor"" gold=""500000"" influence=""100"" playerGold=""5000"" />
</StartupResources>");

        var config = _sut.LoadConfig();

        Assert.AreEqual(5000, config.CultureEntries[0].PlayerGold);
    }

    [TestMethod]
    public void LoadConfig_NegativePlayerGold_RevertsToZeroAndWarns()
    {
        WriteConfig(@"<?xml version=""1.0"" encoding=""utf-8""?>
<StartupResources>
  <Culture id=""gondor"" gold=""500000"" playerGold=""-500"" />
</StartupResources>");

        var config = _sut.LoadConfig();

        Assert.AreEqual(0, config.CultureEntries[0].PlayerGold);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("playerGold") && s.Contains("gondor")));
    }

    [TestMethod]
    public void LoadConfig_PlayerGoldExceedsUpperBound_RevertsToZeroAndWarns()
    {
        WriteConfig(@"<?xml version=""1.0"" encoding=""utf-8""?>
<StartupResources>
  <Culture id=""gondor"" gold=""500000"" playerGold=""99999999"" />
</StartupResources>");

        var config = _sut.LoadConfig();

        Assert.AreEqual(0, config.CultureEntries[0].PlayerGold);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("playerGold") && s.Contains("gondor")));
    }

    [TestMethod]
    public void LoadConfig_NonNumericPlayerGold_RevertsToZeroAndWarns()
    {
        WriteConfig(@"<?xml version=""1.0"" encoding=""utf-8""?>
<StartupResources>
  <Culture id=""gondor"" gold=""500000"" playerGold=""abc"" />
</StartupResources>");

        var config = _sut.LoadConfig();

        Assert.AreEqual(0, config.CultureEntries[0].PlayerGold);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("playerGold") && s.Contains("gondor")));
    }

    [TestMethod]
    public void LoadConfig_MissingPlayerGoldAttribute_DefaultsToZeroNoWarning()
    {
        WriteConfig(@"<?xml version=""1.0"" encoding=""utf-8""?>
<StartupResources>
  <Culture id=""gondor"" gold=""500000"" influence=""100"" />
</StartupResources>");

        var config = _sut.LoadConfig();

        Assert.AreEqual(0, config.CultureEntries[0].PlayerGold);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("playerGold")));
    }

    private void WriteConfig(string content)
    {
        File.WriteAllText(
            Path.Combine(_tempDir, "startup_resources", "startup_resources_config.xml"),
            content);
    }
}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Recurse -File Main\\Features\\Messengers,Main\\Features\\SiegeDismount,TAOM.Tests\\Features\\Messengers,TAOM.Tests\\Features\\SiegeDismount | Select-Object FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 439ms:

[32;1mFullName[0m
[32;1m--------[0m
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\IMessengerConfigProvider.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\IMessengerRandomSource.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\IMessengerService.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\IMessengerSettingsProvider.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\IMessengerStateStore.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerCampaignBehavior.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerConfig.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerConfigProvider.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerRandomSource.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerService.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerSettingsProvider.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerStateStore.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\Domain\HeroSnapshot.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\Domain\MessengerValidationResult.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\Domain\PendingMessenger.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\Domain\PositionUpdate.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\ISiegeDismountService.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\ISiegeDismountSettingsProvider.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\SiegeDismountIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\SiegeDismountService.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\SiegeDismountSettingsProvider.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\Hooks\SiegeDismountMissionBehavior.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\Models\IMountSnapshot.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\Models\MountSnapshot.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\Models\SiegeMountBehaviorType.cs
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\Messengers\MessengerConfigProviderTests.cs
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\Messengers\MessengerServiceTests.cs
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\Messengers\MessengerStateStoreTests.cs
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SiegeDismount\SiegeDismountServiceTests.cs


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content Main\\Features\\SiegeDismount\\*.cs Main\\Features\\SiegeDismount\\Hooks\\*.cs Main\\Features\\SiegeDismount\\Models\\*.cs Main\\Adapters\\IPartyMountInventoryAdapter.cs Main\\Adapters\\IPlayerMountAdapter.cs Main\\Adapters\\PartyMountInventoryAdapter.cs Main\\Adapters\\PlayerMountAdapter.cs" in C:\Users\mikew\source\repos\TAOM
2026-05-06T23:06:04.370521Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.5 seconds
Output:
[31;1mGet-Content: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-Content Main\Features\SiegeDismount\*.cs Main\Features\SiegeDismo[0m …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mA positional parameter cannot be found that accepts argument 'Main\Features\SiegeDismount\Hooks\*.cs'.[0m

 exited 1 in 489ms:
[31;1mGet-Content: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-Content Main\Features\SiegeDismount\*.cs Main\Features\SiegeDismo[0m …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mA positional parameter cannot be found that accepts argument 'Main\Features\SiegeDismount\Hooks\*.cs'.[0m

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$paths = @('"'Main\\Features\\SiegeDismount\\*.cs','Main\\Features\\SiegeDismount\\Hooks\\*.cs','Main\\Features\\SiegeDismount\\Models\\*.cs','Main\\Adapters\\IPartyMountInventoryAdapter.cs','Main\\Adapters\\IPlayerMountAdapter.cs','Main\\Adapters\\PartyMountInventoryAdapter.cs','Main\\Adapters\\PlayerMountAdapter.cs'); foreach ("'$p in $paths) { Get-ChildItem $p | ForEach-Object { "===== $($_.FullName)"; Get-Content $_.FullName } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 472ms:
===== C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\ISiegeDismountService.cs
namespace TAOM.Features.SiegeDismount;

public interface ISiegeDismountService
{
    /// <summary>
    /// Called when a mission begins. The behavior reads mission state and passes primitives in
    /// so the service is testable without a live <c>Mission</c>.
    /// </summary>
    void OnMissionStart(bool isSiegeBattle, string? sceneName);

    /// <summary>
    /// Called when a mission ends. Restores the mount if auto-remount was previously elected.
    /// </summary>
    void OnMissionEnd();
}
===== C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\ISiegeDismountSettingsProvider.cs
using TAOM.Features.SiegeDismount.Models;

namespace TAOM.Features.SiegeDismount;

public interface ISiegeDismountSettingsProvider
{
    bool IsEnabled { get; }
    SiegeMountBehaviorType MountBehavior { get; }
    bool IsDebugMode { get; }
}
===== C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\SiegeDismountIoC.cs
using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.SiegeDismount;

public static class SiegeDismountIoC
{
    public static void RegisterSiegeDismountFeature(IContainer container)
    {
        container.Register<ISiegeDismountSettingsProvider, SiegeDismountSettingsProvider>(Reuse.Singleton);
        container.Register<IPlayerMountAdapter, PlayerMountAdapter>(Reuse.Singleton);
        container.Register<IPartyMountInventoryAdapter, PartyMountInventoryAdapter>(Reuse.Singleton);
        container.Register<ISiegeDismountService, SiegeDismountService>(Reuse.Singleton);
    }
}
===== C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\SiegeDismountService.cs
using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.SiegeDismount.Models;

namespace TAOM.Features.SiegeDismount;

public class SiegeDismountService : ISiegeDismountService
{
    // Keywords for non-IsSiegeBattle siege detection. Intentionally NARROW — earlier
    // versions included "gate" and "wall", which falsely matched real TAOM castle scenes
    // (castle_orthanc_gate, castle_gundabad_wall) and clobbered the player's mount during
    // normal castle visits. Real sieges hit Mission.IsSiegeBattle=true; this list is only
    // a fallback for modded/custom siege scenes that fail to set that flag.
    private static readonly string[] SceneSiegeKeywords = { "siege", "assault", "breach" };

    private readonly ISiegeDismountSettingsProvider _settings;
    private readonly IPlayerMountAdapter _mount;
    private readonly IPartyMountInventoryAdapter _inventory;
    private readonly IModLogger _logger;

    private IMountSnapshot? _capturedSnapshot;
    private bool _pendingRemount;

    public SiegeDismountService(
        ISiegeDismountSettingsProvider settings,
        IPlayerMountAdapter mount,
        IPartyMountInventoryAdapter inventory,
        IModLogger logger)
    {
        _settings = settings;
        _mount = mount;
        _inventory = inventory;
        _logger = logger;
    }

    public void OnMissionStart(bool isSiegeBattle, string? sceneName)
    {
        if (!_settings.IsEnabled)
        {
            _logger.LogInfo("[SiegeDismount] disabled via MCM — patches inert");
            return;
        }

        if (!IsSiegeMission(isSiegeBattle, sceneName))
            return;

        var behavior = _settings.MountBehavior;
        if (behavior == SiegeMountBehaviorType.Vanilla)
            return;

        _logger.LogInfo($"[SiegeDismount] siege detected — scene='{sceneName}' behavior={behavior}");

        if (!_mount.HasMount())
        {
            if (_settings.IsDebugMode)
                _logger.LogDebug("[SiegeDismount] player has no mount equipped — no action");
            return;
        }

        try
        {
            var snapshot = _mount.Capture();
            if (!snapshot.HasMount)
            {
                _logger.LogWarning("[SiegeDismount] HasMount returned true but capture was empty — skipping dismount");
                _capturedSnapshot = null;
                _pendingRemount = false;
                return;
            }
            _capturedSnapshot = snapshot;

            switch (behavior)
            {
                case SiegeMountBehaviorType.DismountKeepOnMap:
                    if (_settings.IsDebugMode)
                        _logger.LogDebug("[SiegeDismount] DismountKeepOnMap — captured but leaving on map");
                    _pendingRemount = false;
                    break;

                case SiegeMountBehaviorType.DismountToInventory:
                    _mount.Clear();
                    _inventory.Deposit(_capturedSnapshot);
                    _pendingRemount = false;
                    if (_settings.IsDebugMode)
                        _logger.LogDebug("[SiegeDismount] DismountToInventory — moved to inventory");
                    break;

                case SiegeMountBehaviorType.AutoRemountAfter:
                    _mount.Clear();
                    _inventory.Deposit(_capturedSnapshot);
                    _pendingRemount = true;
                    if (_settings.IsDebugMode)
                        _logger.LogDebug("[SiegeDismount] AutoRemountAfter — moved to inventory, will restore on mission end");
                    break;

                default:
                    _logger.LogWarning($"[SiegeDismount] unknown SiegeMountBehavior value {(int)behavior} — treating as Vanilla (no-op). Check MCM JSON for an out-of-range setting.");
                    _capturedSnapshot = null;
                    _pendingRemount = false;
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"[SiegeDismount] dismount failed: {ex.Message}");
            _pendingRemount = false;
            _capturedSnapshot = null;
        }
    }

    public void OnMissionEnd()
    {
        if (!_pendingRemount || _capturedSnapshot == null)
        {
            // Even when no auto-remount is pending, clear any stale snapshot so the
            // singleton doesn't carry mount-id strings between missions (state hygiene).
            _capturedSnapshot = null;
            return;
        }

        try
        {
            _mount.Restore(_capturedSnapshot);
            _inventory.Withdraw(_capturedSnapshot);
            _logger.LogInfo("[SiegeDismount] mount restored after siege");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[SiegeDismount] remount failed: {ex.Message}");
        }
        finally
        {
            _pendingRemount = false;
            _capturedSnapshot = null;
        }
    }

    private static bool IsSiegeMission(bool isSiegeBattle, string? sceneName)
    {
        if (isSiegeBattle) return true;
        if (string.IsNullOrEmpty(sceneName)) return false;

        var lower = sceneName!.ToLowerInvariant();
        foreach (var keyword in SceneSiegeKeywords)
        {
            if (lower.Contains(keyword))
                return true;
        }
        return false;
    }
}
===== C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\SiegeDismountSettingsProvider.cs
using TAOM.Features.SiegeDismount.Models;

namespace TAOM.Features.SiegeDismount;

public class SiegeDismountSettingsProvider : ISiegeDismountSettingsProvider
{
    public bool IsEnabled => TaomSettings.Instance?.EnableSiegeDismount ?? true;

    public SiegeMountBehaviorType MountBehavior =>
        (SiegeMountBehaviorType)(TaomSettings.Instance?.SiegeMountBehavior ?? (int)SiegeMountBehaviorType.AutoRemountAfter);

    public bool IsDebugMode => TaomSettings.Instance?.SiegeDismountDebug ?? false;
}
===== C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\Hooks\SiegeDismountMissionBehavior.cs
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;

namespace TAOM.Features.SiegeDismount.Hooks;

/// <summary>
/// Thin <see cref="MissionBehavior"/> that bridges Mission lifecycle into <see cref="ISiegeDismountService"/>.
/// Resolves the service via IoC at construction and forwards primitive mission state.
/// </summary>
public class SiegeDismountMissionBehavior : MissionBehavior
{
    private readonly ISiegeDismountService _service;
    private readonly IModLogger _logger;

    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Logic;

    public SiegeDismountMissionBehavior()
    {
        _service = IoC.Resolve<ISiegeDismountService>();
        _logger = IoC.Resolve<IModLogger>();
    }

    public override void OnBehaviorInitialize()
    {
        base.OnBehaviorInitialize();

        var mission = Mission.Current;
        var isSiegeBattle = mission?.IsSiegeBattle ?? false;
        var sceneName = mission?.SceneName;

        _service.OnMissionStart(isSiegeBattle, sceneName);
    }

    protected override void OnEndMission()
    {
        base.OnEndMission();
        _service.OnMissionEnd();
    }
}
===== C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\Models\IMountSnapshot.cs
namespace TAOM.Features.SiegeDismount.Models;

public interface IMountSnapshot
{
    bool HasMount { get; }
    bool HasHarness { get; }
    string? MountItemId { get; }
    string? HarnessItemId { get; }
}
===== C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\Models\MountSnapshot.cs
namespace TAOM.Features.SiegeDismount.Models;

internal sealed class MountSnapshot : IMountSnapshot
{
    public static readonly MountSnapshot Empty = new(null, null);

    public MountSnapshot(string? mountItemId, string? harnessItemId)
    {
        MountItemId = mountItemId;
        HarnessItemId = harnessItemId;
    }

    public bool HasMount => !string.IsNullOrEmpty(MountItemId);
    public bool HasHarness => !string.IsNullOrEmpty(HarnessItemId);
    public string? MountItemId { get; }
    public string? HarnessItemId { get; }
}
===== C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\Models\SiegeMountBehaviorType.cs
namespace TAOM.Features.SiegeDismount.Models;

public enum SiegeMountBehaviorType
{
    Vanilla,
    DismountKeepOnMap,
    DismountToInventory,
    AutoRemountAfter
}
===== C:\Users\mikew\source\repos\TAOM\Main\Adapters\IPartyMountInventoryAdapter.cs
using TAOM.Features.SiegeDismount.Models;

namespace TAOM.Adapters;

/// <summary>
/// Adds and removes mount items from the player main party's <c>ItemRoster</c>.
/// Operates on opaque <see cref="IMountSnapshot"/> tokens so services never see <c>ItemObject</c> (ADR-007).
/// </summary>
public interface IPartyMountInventoryAdapter
{
    void Deposit(IMountSnapshot snapshot);
    void Withdraw(IMountSnapshot snapshot);
}
===== C:\Users\mikew\source\repos\TAOM\Main\Adapters\IPlayerMountAdapter.cs
using TAOM.Features.SiegeDismount.Models;

namespace TAOM.Adapters;

/// <summary>
/// Reads and mutates the player main hero's mount + harness equipment slots.
/// Returns opaque <see cref="IMountSnapshot"/> tokens so services never see <c>EquipmentElement</c> directly (ADR-007).
/// </summary>
public interface IPlayerMountAdapter
{
    bool HasMount();
    IMountSnapshot Capture();
    void Clear();
    void Restore(IMountSnapshot snapshot);
}
===== C:\Users\mikew\source\repos\TAOM\Main\Adapters\PartyMountInventoryAdapter.cs
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TAOM.Features.SiegeDismount.Models;

namespace TAOM.Adapters;

/// <summary>
/// Concrete <see cref="IPartyMountInventoryAdapter"/> backed by <c>MobileParty.MainParty.ItemRoster</c>.
/// </summary>
public class PartyMountInventoryAdapter : IPartyMountInventoryAdapter
{
    public void Deposit(IMountSnapshot snapshot)
    {
        var roster = MobileParty.MainParty?.ItemRoster;
        if (roster == null || snapshot == null) return;

        if (snapshot.HasMount)
        {
            var mount = MBObjectManager.Instance?.GetObject<ItemObject>(snapshot.MountItemId);
            if (mount != null) roster.AddToCounts(mount, 1);
        }
        if (snapshot.HasHarness)
        {
            var harness = MBObjectManager.Instance?.GetObject<ItemObject>(snapshot.HarnessItemId);
            if (harness != null) roster.AddToCounts(harness, 1);
        }
    }

    public void Withdraw(IMountSnapshot snapshot)
    {
        var roster = MobileParty.MainParty?.ItemRoster;
        if (roster == null || snapshot == null) return;

        if (snapshot.HasMount)
        {
            var mount = MBObjectManager.Instance?.GetObject<ItemObject>(snapshot.MountItemId);
            if (mount != null) roster.AddToCounts(mount, -1);
        }
        if (snapshot.HasHarness)
        {
            var harness = MBObjectManager.Instance?.GetObject<ItemObject>(snapshot.HarnessItemId);
            if (harness != null) roster.AddToCounts(harness, -1);
        }
    }
}
===== C:\Users\mikew\source\repos\TAOM\Main\Adapters\PlayerMountAdapter.cs
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TAOM.Features.SiegeDismount.Models;

namespace TAOM.Adapters;

/// <summary>
/// Concrete <see cref="IPlayerMountAdapter"/> backed by <c>Hero.MainHero.BattleEquipment</c>.
/// All TaleWorlds types stay inside this class — services see only <see cref="IMountSnapshot"/>.
/// </summary>
public class PlayerMountAdapter : IPlayerMountAdapter
{
    public bool HasMount()
    {
        var equipment = Hero.MainHero?.BattleEquipment;
        if (equipment == null) return false;
        return !equipment[EquipmentIndex.Horse].IsEmpty;
    }

    public IMountSnapshot Capture()
    {
        var equipment = Hero.MainHero?.BattleEquipment;
        if (equipment == null) return MountSnapshot.Empty;

        var mountId = equipment[EquipmentIndex.Horse].Item?.StringId;
        var harnessId = equipment[EquipmentIndex.HorseHarness].Item?.StringId;
        return new MountSnapshot(mountId, harnessId);
    }

    public void Clear()
    {
        var equipment = Hero.MainHero?.BattleEquipment;
        if (equipment == null) return;

        equipment[EquipmentIndex.Horse] = EquipmentElement.Invalid;
        equipment[EquipmentIndex.HorseHarness] = EquipmentElement.Invalid;
    }

    public void Restore(IMountSnapshot snapshot)
    {
        var equipment = Hero.MainHero?.BattleEquipment;
        if (equipment == null || snapshot == null) return;

        if (snapshot.HasMount)
        {
            var mount = MBObjectManager.Instance?.GetObject<ItemObject>(snapshot.MountItemId);
            if (mount != null)
                equipment[EquipmentIndex.Horse] = new EquipmentElement(mount);
        }
        if (snapshot.HasHarness)
        {
            var harness = MBObjectManager.Instance?.GetObject<ItemObject>(snapshot.HarnessItemId);
            if (harness != null)
                equipment[EquipmentIndex.HorseHarness] = new EquipmentElement(harness);
        }
    }
}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Main\\**\\*.cs -Pattern 'string\\?' -SimpleMatch | Select-Object -First 20" in C:\Users\mikew\source\repos\TAOM
 succeeded in 454ms:
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Recurse -Filter *.cs Main | Select-String -Pattern 'string\\?' | Select-Object -First 20 Path,LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
 succeeded in 499ms:

[32;1mPath                                                                                 [0m[32;1m LineNumber[0m[32;1m Line[0m
[32;1m----                                                                                 [0m [32;1m----------[0m [32;1m----[0m
C:\Users\mikew\source\repos\TAOM\Main\Features\FactionMap\FactionSelectionService.cs          30     public string? Ge…
C:\Users\mikew\source\repos\TAOM\Main\Features\FactionMap\IFactionSelectionService.cs          8     string? GetCultur…
C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\ISiegeDismountService.cs          9     void OnMissionSta…
C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\SiegeDismountService.cs          37     public void OnMis…
C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\SiegeDismountService.cs         139     private static bo…
C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\Models\IMountSnapshot.cs          7     string? MountItem…
C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\Models\IMountSnapshot.cs          8     string? HarnessIt…
C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\Models\MountSnapshot.cs           7     public MountSnaps…
C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\Models\MountSnapshot.cs          15     public string? Mo…
C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\Models\MountSnapshot.cs          16     public string? Ha…


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'foreach ($f in Get-ChildItem -Recurse -File Main'"\\Features\\Messengers\\*.cs) { \"===== "'$($f.FullName)"; Get-Content $f.FullName }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 481ms:
===== C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\Domain\HeroSnapshot.cs
namespace TAOM.Features.Messengers.Domain;

public sealed class HeroSnapshot
{
    public string HeroId { get; }
    public bool IsAlive { get; }
    public bool IsPrisoner { get; }
    public bool IsChild { get; }
    public bool IsFugitive { get; }
    public bool IsActive { get; }
    public bool IsWanderer { get; }
    public bool IsHumanPlayerCharacter { get; }
    public bool IsInPlayerParty { get; }
    public bool IsInMapEvent { get; }

    public HeroSnapshot(
        string heroId,
        bool isAlive,
        bool isPrisoner,
        bool isChild,
        bool isFugitive,
        bool isActive,
        bool isWanderer,
        bool isHumanPlayerCharacter,
        bool isInPlayerParty,
        bool isInMapEvent)
    {
        HeroId = heroId;
        IsAlive = isAlive;
        IsPrisoner = isPrisoner;
        IsChild = isChild;
        IsFugitive = isFugitive;
        IsActive = isActive;
        IsWanderer = isWanderer;
        IsHumanPlayerCharacter = isHumanPlayerCharacter;
        IsInPlayerParty = isInPlayerParty;
        IsInMapEvent = isInMapEvent;
    }
}
===== C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\Domain\MessengerValidationResult.cs
namespace TAOM.Features.Messengers.Domain;

public enum MessengerValidationResult
{
    Ok,
    NullTarget,
    HumanPlayerCharacter,
    HeroDead,
    HeroPrisoner,
    HeroChild,
    HeroFugitive,
    TargetUnavailable,
    TargetInPlayerParty,
    InsufficientGold,
    AlreadyPending,
}
===== C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\Domain\PendingMessenger.cs
using System.Globalization;
using TaleWorlds.Library;

namespace TAOM.Features.Messengers.Domain;

public sealed class PendingMessenger
{
    public string TargetHeroId { get; }
    public double DispatchTimeDays { get; }
    public Vec2 Position { get; set; }
    public bool Arrived { get; set; }

    public PendingMessenger(string targetHeroId, double dispatchTimeDays, Vec2 position, bool arrived)
    {
        TargetHeroId = targetHeroId;
        DispatchTimeDays = dispatchTimeDays;
        Position = position;
        Arrived = arrived;
    }

    public string Serialize()
    {
        var inv = CultureInfo.InvariantCulture;
        return $"{DispatchTimeDays.ToString("R", inv)}|{Position.X.ToString("R", inv)}|{Position.Y.ToString("R", inv)}|{(Arrived ? 1 : 0)}";
    }

    public static bool TryDeserialize(string heroId, string serialized, out PendingMessenger messenger)
    {
        messenger = null;
        if (string.IsNullOrEmpty(heroId) || string.IsNullOrEmpty(serialized))
            return false;

        var parts = serialized.Split('|');
        if (parts.Length != 4)
            return false;

        var inv = CultureInfo.InvariantCulture;
        if (!double.TryParse(parts[0], NumberStyles.Float, inv, out var days)) return false;
        if (!float.TryParse(parts[1], NumberStyles.Float, inv, out var x)) return false;
        if (!float.TryParse(parts[2], NumberStyles.Float, inv, out var y)) return false;
        if (parts[3] != "0" && parts[3] != "1") return false;

        messenger = new PendingMessenger(heroId, days, new Vec2(x, y), parts[3] == "1");
        return true;
    }
}
===== C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\Domain\PositionUpdate.cs
using TaleWorlds.Library;

namespace TAOM.Features.Messengers.Domain;

public readonly struct PositionUpdate
{
    public Vec2 NewPosition { get; }
    public bool Arrived { get; }

    public PositionUpdate(Vec2 newPosition, bool arrived)
    {
        NewPosition = newPosition;
        Arrived = arrived;
    }
}
===== C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\IMessengerConfigProvider.cs
namespace TAOM.Features.Messengers;

public interface IMessengerConfigProvider
{
    MessengerConfig GetConfig();
}
===== C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\IMessengerRandomSource.cs
namespace TAOM.Features.Messengers;

public interface IMessengerRandomSource
{
    float NextFloat();
}
===== C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\IMessengerService.cs
using TaleWorlds.Library;
using TAOM.Features.Messengers.Domain;

namespace TAOM.Features.Messengers;

public interface IMessengerService
{
    MessengerValidationResult CanSendMessenger(HeroSnapshot target, int playerGold);
    bool RollAccident();
    PositionUpdate AdvancePosition(Vec2 currentPosition, Vec2 targetPosition, float speed);
    float CalculateMessengerSpeed(float mapDiagonal, int travelDays);
}
===== C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\IMessengerSettingsProvider.cs
namespace TAOM.Features.Messengers;

public interface IMessengerSettingsProvider
{
    bool EnableMessengers { get; }
    int MessengerGoldCost { get; }
    int MessengerTravelDays { get; }
    bool MessengerAccidents { get; }
}
===== C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\IMessengerStateStore.cs
using System.Collections.Generic;
using TAOM.Features.Messengers.Domain;

namespace TAOM.Features.Messengers;

public interface IMessengerStateStore
{
    void Add(PendingMessenger messenger);
    bool Remove(string heroId);
    PendingMessenger Get(string heroId);
    bool Contains(string heroId);
    IReadOnlyList<PendingMessenger> GetAll();
    int Count { get; }
    void Clear();

    Dictionary<string, string> Serialize();
    void Deserialize(IReadOnlyDictionary<string, string> data);
}
===== C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerCampaignBehavior.cs
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.Messengers.Domain;

namespace TAOM.Features.Messengers;

public class MessengerCampaignBehavior : CampaignBehaviorBase, IMissionListener
{
    private readonly IMessengerService _service;
    private readonly IMessengerStateStore _store;
    private readonly IMessengerSettingsProvider _settings;
    private readonly IModLogger _logger;

    private static readonly MissionMode[] AllowedMissionModes = { MissionMode.Conversation, MissionMode.Barter };

    private bool _dialogsRegistered;
    private bool _processingArrivedMessenger;
    private PendingMessenger _activeMessenger;
    private Mission _currentMission;
    private Vec2 _originalPosition = Vec2.Invalid;

    public MessengerCampaignBehavior(
        IMessengerService service,
        IMessengerStateStore store,
        IMessengerSettingsProvider settings,
        IModLogger logger)
    {
        _service = service;
        _store = store;
        _settings = settings;
        _logger = logger;
    }

    // --- CampaignBehaviorBase ---

    public override void RegisterEvents()
    {
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
    }

    public override void SyncData(IDataStore dataStore)
    {
        if (dataStore.IsSaving)
        {
            var snapshot = _store.Serialize();
            dataStore.SyncData("_taom_messengers", ref snapshot);
        }
        else
        {
            Dictionary<string, string> snapshot = null;
            dataStore.SyncData("_taom_messengers", ref snapshot);
            _store.Deserialize(snapshot);
            _processingArrivedMessenger = false;
            _activeMessenger = null;
            _currentMission = null;
            _originalPosition = Vec2.Invalid;
        }
    }

    // --- Public API (callable by other features) ---

    public void SendMessenger(Hero targetHero)
    {
        var snapshot = SnapshotHero(targetHero);
        var playerGold = Hero.MainHero?.Gold ?? 0;
        var validation = _service.CanSendMessenger(snapshot, playerGold);
        if (validation != MessengerValidationResult.Ok)
        {
            ShowInquiry(
                new TextObject("{=taom_messenger_cannot_send}Cannot Send Messenger").ToString(),
                BuildValidationReason(validation, targetHero).ToString(),
                affirmative: GameTexts.FindText("str_ok").ToString());
            return;
        }

        GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, _settings.MessengerGoldCost, false);

        var startPosition = Hero.MainHero?.GetMapPoint()?.Position2D ?? Vec2.Zero;
        var messenger = new PendingMessenger(
            targetHeroId: targetHero.StringId,
            dispatchTimeDays: CampaignTime.Now.ToDays,
            position: startPosition,
            arrived: false);
        _store.Add(messenger);

        var sentText = new TextObject("{=taom_messenger_sent}A messenger has been dispatched to {HERO_NAME} and will arrive within {DAYS} days.");
        sentText.SetTextVariable("HERO_NAME", targetHero.Name);
        sentText.SetTextVariable("DAYS", _settings.MessengerTravelDays);
        ShowInquiry(
            new TextObject("{=taom_messenger_sent_title}Messenger Sent").ToString(),
            sentText.ToString(),
            affirmative: GameTexts.FindText("str_ok").ToString());
    }

    public bool CanSendMessenger(Hero targetHero, out TextObject reason)
    {
        var validation = _service.CanSendMessenger(SnapshotHero(targetHero), Hero.MainHero?.Gold ?? 0);
        if (validation == MessengerValidationResult.Ok)
        {
            reason = TextObject.GetEmpty();
            return true;
        }
        reason = BuildValidationReason(validation, targetHero);
        return false;
    }

    // --- Lifecycle ---

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        if (!_dialogsRegistered)
        {
            AddDialogOptions(starter);
            _dialogsRegistered = true;
        }
    }

    private void OnHourlyTick()
    {
        if (_store.Count == 0)
            return;

        var mapDiagonal = Campaign.MapDiagonal;
        var travelDays = _settings.MessengerTravelDays;
        var speed = _service.CalculateMessengerSpeed(mapDiagonal, travelDays);

        var toRemove = new List<string>();

        foreach (var messenger in _store.GetAll())
        {
            if (messenger.Arrived)
            {
                if (!_processingArrivedMessenger)
                {
                    var outcome = HandleArrivedMessenger(messenger);
                    if (outcome == ArrivedHandleOutcome.RemoveFromList)
                        toRemove.Add(messenger.TargetHeroId);
                }
                continue;
            }

            UpdateMessengerPosition(messenger, travelDays, speed);

            if (!messenger.Arrived && _service.RollAccident())
            {
                NotifyAccident(messenger);
                toRemove.Add(messenger.TargetHeroId);
            }
        }

        foreach (var heroId in toRemove)
            _store.Remove(heroId);
    }

    private void UpdateMessengerPosition(PendingMessenger messenger, int travelDays, float speed)
    {
        var elapsedDays = CampaignTime.Now.ToDays - messenger.DispatchTimeDays;
        if (elapsedDays >= travelDays)
        {
            messenger.Arrived = true;
            return;
        }

        var target = ResolveHero(messenger.TargetHeroId);
        var targetPosition = target?.GetMapPoint()?.Position2D ?? Vec2.Invalid;
        if (!targetPosition.IsValid)
            return;

        var update = _service.AdvancePosition(messenger.Position, targetPosition, speed);
        messenger.Position = update.NewPosition;
        if (update.Arrived)
            messenger.Arrived = true;
    }

    private enum ArrivedHandleOutcome
    {
        WaitForNextTick,
        AwaitingUserInput,
        RemoveFromList,
    }

    private ArrivedHandleOutcome HandleArrivedMessenger(PendingMessenger messenger)
    {
        var target = ResolveHero(messenger.TargetHeroId);
        if (target == null)
            return ArrivedHandleOutcome.RemoveFromList;

        if (target.PartyBelongedTo == Hero.MainHero?.PartyBelongedTo)
        {
            var text = new TextObject("{=taom_messenger_confused}The messenger seems confused — they were trying to reach someone in your own party!");
            text.SetTextVariable("HERO_NAME", target.Name);
            ShowInquiry(
                new TextObject("{=taom_messenger_error}Messenger Error").ToString(),
                text.ToString(),
                affirmative: GameTexts.FindText("str_ok").ToString());
            return ArrivedHandleOutcome.RemoveFromList;
        }

        if (!IsPlayerAvailable() || !IsTargetAvailableNow(target))
            return ArrivedHandleOutcome.WaitForNextTick;

        _processingArrivedMessenger = true;

        var arrivalText = new TextObject("{=taom_messenger_arrived}A messenger from {HERO_NAME} has arrived. Do you wish to speak with them?");
        arrivalText.SetTextVariable("HERO_NAME", target.Name);

        InformationManager.ShowInquiry(new InquiryData(
                new TextObject("{=taom_messenger_arrived_title}Messenger Arrived").ToString(),
                arrivalText.ToString(),
                isAffirmativeOptionShown: true,
                isNegativeOptionShown: true,
                affirmativeText: new TextObject("{=taom_messenger_speak}Speak").ToString(),
                negativeText: new TextObject("{=taom_messenger_dismiss}Dismiss").ToString(),
                affirmativeAction: () => StartMessengerConversation(messenger),
                negativeAction: () => DismissMessenger(messenger)),
            pauseGameActiveState: true,
            prioritize: false);

        return ArrivedHandleOutcome.AwaitingUserInput;
    }

    private void DismissMessenger(PendingMessenger messenger)
    {
        _store.Remove(messenger.TargetHeroId);
        _processingArrivedMessenger = false;
    }

    private void NotifyAccident(PendingMessenger messenger)
    {
        var target = ResolveHero(messenger.TargetHeroId);
        var targetName = target?.Name ?? new TextObject(messenger.TargetHeroId);

        var text = new TextObject("{=taom_messenger_lost}Your messenger to {HERO_NAME} was ambushed by bandits and never arrived.");
        text.SetTextVariable("HERO_NAME", targetName);
        ShowInquiry(
            new TextObject("{=taom_messenger_lost_title}Messenger Lost").ToString(),
            text.ToString(),
            affirmative: GameTexts.FindText("str_ok").ToString());
    }

    private void StartMessengerConversation(PendingMessenger messenger)
    {
        if (_activeMessenger != null || _currentMission != null)
            return;

        var target = ResolveHero(messenger.TargetHeroId);
        if (target == null)
        {
            _store.Remove(messenger.TargetHeroId);
            _processingArrivedMessenger = false;
            return;
        }

        _activeMessenger = messenger;
        var mainParty = PartyBase.MainParty;

        // Spawn unspawned wanderer at born settlement
        if (target.IsWanderer && target.HeroState == Hero.CharacterStates.NotSpawned && target.BornSettlement != null)
        {
            target.ChangeState(Hero.CharacterStates.Active);
            EnterSettlementAction.ApplyForCharacterOnly(target, target.BornSettlement);
        }

        // Resolve target party — settlement → travelling party → born settlement → main (last-resort)
        var currentSettlement = target.CurrentSettlement;
        PartyBase targetParty;
        if (currentSettlement != null)
            targetParty = currentSettlement.Party ?? target.BornSettlement?.Party;
        else
            targetParty = target.PartyBelongedTo?.Party ?? target.BornSettlement?.Party;

        PlayerEncounter.Start();
        PlayerEncounter.Current.SetupFields(mainParty, targetParty ?? mainParty);
        Campaign.Current.CurrentConversationContext = ConversationContext.Default;

        if (currentSettlement != null)
        {
            _originalPosition = Hero.MainHero.GetMapPoint().Position2D;
            PlayerEncounter.EnterSettlement();

            var targetLocation = LocationComplex.Current.GetLocationOfCharacter(target);
            var playerLocation = LocationComplex.Current.GetLocationOfCharacter(Hero.MainHero);

            CampaignEventDispatcher.Instance.OnPlayerStartTalkFromMenu(target);
            _currentMission = (Mission)PlayerEncounter.LocationEncounter.CreateAndOpenMissionController(
                targetLocation, playerLocation, target.CharacterObject, null);
        }
        else
        {
            _originalPosition = Vec2.Invalid;
            _currentMission = (Mission)Campaign.Current.CampaignMissionManager.OpenConversationMission(
                new ConversationCharacterData(Hero.MainHero.CharacterObject, mainParty, true, false, false, false, false, false),
                new ConversationCharacterData(target.CharacterObject, targetParty, true, false, false, false, false, false),
                "", "");
        }

        _currentMission?.AddListener(this);
    }

    // --- IMissionListener ---

    public void OnEndMission()
    {
        if (_activeMessenger != null)
        {
            _store.Remove(_activeMessenger.TargetHeroId);
            _activeMessenger = null;
        }

        _currentMission?.RemoveListener(this);
        _currentMission = null;
        _processingArrivedMessenger = false;

        // Defer settlement cleanup by one tick — PlayerEncounter.Finish must not run inside OnEndMission
        CampaignEvents.TickEvent.AddNonSerializedListener(this, CleanUpSettlementEncounter);
    }

    public void OnMissionModeChange(MissionMode oldMissionMode, bool atStart)
    {
        if (_currentMission == null) return;
        var allowedNow = false;
        for (var i = 0; i < AllowedMissionModes.Length; i++)
            if (AllowedMissionModes[i] == _currentMission.Mode) { allowedNow = true; break; }
        var allowedBefore = false;
        for (var i = 0; i < AllowedMissionModes.Length; i++)
            if (AllowedMissionModes[i] == oldMissionMode) { allowedBefore = true; break; }
        if (!allowedNow && allowedBefore)
            _currentMission.EndMission();
    }

    public void OnEquipItemsFromSpawnEquipmentBegin(Agent agent, Agent.CreationType creationType) { }
    public void OnEquipItemsFromSpawnEquipment(Agent agent, Agent.CreationType creationType) { }
    public void OnConversationCharacterChanged() { }
    public void OnResetMission() { }
    public void OnDeploymentPlanMade(Team team, bool isFirstPlan) { }

    private void CleanUpSettlementEncounter(float dt)
    {
        try
        {
            PlayerEncounter.Finish(true);

            if (_originalPosition.IsValid && MobileParty.MainParty != null)
                MobileParty.MainParty.Position = new CampaignVec2(_originalPosition, isOnLand: true);
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning($"Messengers: settlement-cleanup tick handler threw: {ex.Message}");
        }
        finally
        {
            _originalPosition = Vec2.Invalid;
            CampaignEvents.TickEvent.ClearListeners(this);
        }
    }

    // --- Dialog tree ---

    private void AddDialogOptions(CampaignGameStarter starter)
    {
        starter.AddPlayerLine(
            "taom_messenger_send_init",
            "hero_main_options",
            "taom_messenger_send_confirm",
            "{=taom_messenger_dialog_init}I need to send you a message later. Can I dispatch a messenger to you?",
            DialogCondition_CanSend,
            null);

        starter.AddDialogLine(
            "taom_messenger_send_npc_accept",
            "taom_messenger_send_confirm",
            "taom_messenger_send_choice",
            "{=taom_messenger_dialog_npc_accept}Of course. I'll make sure to receive your messenger when they arrive.",
            null, null);

        starter.AddPlayerLine(
            "taom_messenger_send_choice_yes",
            "taom_messenger_send_choice",
            "taom_messenger_dialog_sent",
            "{=taom_messenger_dialog_send}Send the messenger ({COST} denars)",
            DialogCondition_HasGold,
            DialogConsequence_DispatchMessenger);

        starter.AddPlayerLine(
            "taom_messenger_send_choice_no",
            "taom_messenger_send_choice",
            "taom_messenger_dialog_decline_ack",
            "{=taom_messenger_dialog_decline}On second thought, never mind.",
            null, null);

        starter.AddDialogLine(
            "taom_messenger_dialog_sent",
            "taom_messenger_dialog_sent",
            "close_window",
            "{=taom_messenger_dialog_sent}I'll be expecting your messenger then.",
            null, null);

        starter.AddDialogLine(
            "taom_messenger_dialog_decline_ack",
            "taom_messenger_dialog_decline_ack",
            "close_window",
            "{=taom_messenger_dialog_decline_ack}As you wish.",
            null, null);
    }

    private bool DialogCondition_CanSend()
    {
        var hero = Hero.OneToOneConversationHero;
        if (hero == null) return false;
        return _service.CanSendMessenger(SnapshotHero(hero), Hero.MainHero?.Gold ?? 0) == MessengerValidationResult.Ok;
    }

    private bool DialogCondition_HasGold()
    {
        MBTextManager.SetTextVariable("COST", _settings.MessengerGoldCost.ToString());
        return (Hero.MainHero?.Gold ?? 0) >= _settings.MessengerGoldCost;
    }

    private void DialogConsequence_DispatchMessenger()
    {
        var hero = Hero.OneToOneConversationHero;
        if (hero == null) return;
        SendMessenger(hero);
    }

    // --- Helpers ---

    private static Hero ResolveHero(string heroId)
    {
        if (string.IsNullOrEmpty(heroId)) return null;
        return Hero.FindFirst(h => h != null && h.StringId == heroId);
    }

    private static HeroSnapshot SnapshotHero(Hero hero)
    {
        if (hero == null) return null;
        var inPlayerParty = hero.PartyBelongedTo != null
                            && Hero.MainHero?.PartyBelongedTo != null
                            && hero.PartyBelongedTo == Hero.MainHero.PartyBelongedTo;
        var inMapEvent = hero.PartyBelongedTo?.MapEvent != null;
        return new HeroSnapshot(
            heroId: hero.StringId,
            isAlive: hero.IsAlive,
            isPrisoner: hero.IsPrisoner,
            isChild: hero.IsChild,
            isFugitive: hero.IsFugitive,
            isActive: hero.IsActive,
            isWanderer: hero.IsWanderer,
            isHumanPlayerCharacter: hero.IsHumanPlayerCharacter,
            isInPlayerParty: inPlayerParty,
            isInMapEvent: inMapEvent);
    }

    private static bool IsTargetAvailableNow(Hero hero)
    {
        if (hero == null || !hero.IsAlive || hero.IsPrisoner || hero.IsChild) return false;
        return hero.PartyBelongedTo?.MapEvent == null;
    }

    private static bool IsPlayerAvailable()
    {
        if (PartyBase.MainParty == null) return false;
        if (PlayerEncounter.Current != null) return false;
        if (GameStateManager.Current?.ActiveState is MapState mapState && !mapState.AtMenu) return true;
        return false;
    }

    private TextObject BuildValidationReason(MessengerValidationResult result, Hero target)
    {
        TextObject text;
        switch (result)
        {
            case MessengerValidationResult.NullTarget:
            case MessengerValidationResult.HumanPlayerCharacter:
                return new TextObject("{=taom_messenger_invalid_target}Invalid target.");
            case MessengerValidationResult.InsufficientGold:
                text = new TextObject("{=taom_messenger_no_gold}Not enough gold! You need {COST} denars.");
                text.SetTextVariable("COST", _settings.MessengerGoldCost);
                return text;
            case MessengerValidationResult.AlreadyPending:
                return new TextObject("{=taom_messenger_already_sent}A messenger has already been dispatched to this person.");
            case MessengerValidationResult.HeroDead:
                text = new TextObject("{=taom_messenger_hero_dead}{HERO_NAME} is dead.");
                text.SetTextVariable("HERO_NAME", target?.Name ?? new TextObject(""));
                return text;
            case MessengerValidationResult.HeroPrisoner:
                text = new TextObject("{=taom_messenger_hero_prisoner}{HERO_NAME} is imprisoned and cannot receive messengers.");
                text.SetTextVariable("HERO_NAME", target?.Name ?? new TextObject(""));
                return text;
            case MessengerValidationResult.HeroFugitive:
                text = new TextObject("{=taom_messenger_hero_fugitive}{HERO_NAME} is fugitive and cannot be found.");
                text.SetTextVariable("HERO_NAME", target?.Name ?? new TextObject(""));
                return text;
            case MessengerValidationResult.HeroChild:
                text = new TextObject("{=taom_messenger_hero_child}{HERO_NAME} is too young to receive messengers.");
                text.SetTextVariable("HERO_NAME", target?.Name ?? new TextObject(""));
                return text;
            case MessengerValidationResult.TargetInPlayerParty:
                text = new TextObject("{=taom_messenger_confused}The messenger seems confused — they were trying to reach someone in your own party!");
                text.SetTextVariable("HERO_NAME", target?.Name ?? new TextObject(""));
                return text;
            default:
                return new TextObject("{=taom_messenger_target_unavailable}This person cannot be reached by messenger at this time.");
        }
    }

    private static void ShowInquiry(string title, string body, string affirmative)
    {
        InformationManager.ShowInquiry(new InquiryData(
            title, body,
            isAffirmativeOptionShown: true,
            isNegativeOptionShown: false,
            affirmativeText: affirmative,
            negativeText: string.Empty,
            affirmativeAction: null,
            negativeAction: null),
            pauseGameActiveState: false,
            prioritize: false);
    }
}
===== C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerConfig.cs
namespace TAOM.Features.Messengers;

public class MessengerConfig
{
    public float AccidentChancePerHour { get; set; } = 0.002f;
    public float TravelSpeedMultiplier { get; set; } = 1.0f;
}
===== C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerConfigProvider.cs
using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;

namespace TAOM.Features.Messengers;

public class MessengerConfigProvider : IMessengerConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<MessengerConfig> _config;

    public MessengerConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<MessengerConfig>(LoadConfig);
    }

    public MessengerConfig GetConfig() => _config.Value;

    private MessengerConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "messengers", "messenger_config.json");

        if (!File.Exists(path))
        {
            _logger.LogInfo($"MessengerConfigProvider: messenger_config.json not found at {path}, using defaults");
            return new MessengerConfig();
        }

        MessengerConfig parsed;
        try
        {
            var json = File.ReadAllText(path);
            parsed = JsonConvert.DeserializeObject<MessengerConfig>(json) ?? new MessengerConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"MessengerConfigProvider: Failed to parse messenger_config.json: {ex.Message}");
            return new MessengerConfig();
        }

        return Validate(parsed);
    }

    private MessengerConfig Validate(MessengerConfig parsed)
    {
        var sanitized = new MessengerConfig
        {
            AccidentChancePerHour = parsed.AccidentChancePerHour,
            TravelSpeedMultiplier = parsed.TravelSpeedMultiplier,
        };

        var defaults = new MessengerConfig();
        var rejected = false;

        if (sanitized.AccidentChancePerHour < 0f || sanitized.AccidentChancePerHour > 1f)
        {
            _logger.LogWarning($"MessengerConfigProvider: accidentChancePerHour={sanitized.AccidentChancePerHour} outside [0,1], reverting to default {defaults.AccidentChancePerHour}");
            sanitized.AccidentChancePerHour = defaults.AccidentChancePerHour;
            rejected = true;
        }

        if (sanitized.TravelSpeedMultiplier < 0.1f || sanitized.TravelSpeedMultiplier > 10f)
        {
            _logger.LogWarning($"MessengerConfigProvider: travelSpeedMultiplier={sanitized.TravelSpeedMultiplier} outside [0.1,10], reverting to default {defaults.TravelSpeedMultiplier}");
            sanitized.TravelSpeedMultiplier = defaults.TravelSpeedMultiplier;
            rejected = true;
        }

        if (rejected)
            _logger.LogWarning("MessengerConfigProvider: messenger_config.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo("MessengerConfigProvider: Loaded messenger_config.json");

        return sanitized;
    }
}
===== C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerIoC.cs
using DryIoc;

namespace TAOM.Features.Messengers;

public static class MessengerIoC
{
    public static void RegisterMessengerFeature(IContainer container)
    {
        container.Register<IMessengerSettingsProvider, MessengerSettingsProvider>(Reuse.Singleton);
        container.Register<IMessengerConfigProvider, MessengerConfigProvider>(Reuse.Singleton);
        container.Register<IMessengerStateStore, MessengerStateStore>(Reuse.Singleton);
        container.Register<IMessengerRandomSource, MessengerRandomSource>(Reuse.Singleton);
        container.Register<IMessengerService, MessengerService>(Reuse.Singleton);
    }
}
===== C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerRandomSource.cs
using TaleWorlds.Core;

namespace TAOM.Features.Messengers;

public class MessengerRandomSource : IMessengerRandomSource
{
    public float NextFloat() => MBRandom.RandomFloat;
}
===== C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerService.cs
using System;
using TaleWorlds.Library;
using TAOM.Features.Messengers.Domain;

namespace TAOM.Features.Messengers;

public class MessengerService : IMessengerService
{
    private readonly IMessengerSettingsProvider _settings;
    private readonly IMessengerConfigProvider _config;
    private readonly IMessengerStateStore _store;
    private readonly IMessengerRandomSource _random;

    public MessengerService(
        IMessengerSettingsProvider settings,
        IMessengerConfigProvider config,
        IMessengerStateStore store,
        IMessengerRandomSource random)
    {
        _settings = settings;
        _config = config;
        _store = store;
        _random = random;
    }

    public MessengerValidationResult CanSendMessenger(HeroSnapshot target, int playerGold)
    {
        if (target == null)
            return MessengerValidationResult.NullTarget;

        if (target.IsHumanPlayerCharacter)
            return MessengerValidationResult.HumanPlayerCharacter;

        if (!target.IsAlive)
            return MessengerValidationResult.HeroDead;

        if (target.IsPrisoner)
            return MessengerValidationResult.HeroPrisoner;

        if (target.IsChild)
            return MessengerValidationResult.HeroChild;

        if (target.IsFugitive)
            return MessengerValidationResult.HeroFugitive;

        var canBeReached = target.IsActive
                           || (target.IsWanderer && !target.IsActive);
        if (!canBeReached)
            return MessengerValidationResult.TargetUnavailable;

        if (target.IsInPlayerParty)
            return MessengerValidationResult.TargetInPlayerParty;

        if (playerGold < _settings.MessengerGoldCost)
            return MessengerValidationResult.InsufficientGold;

        if (_store.Contains(target.HeroId))
            return MessengerValidationResult.AlreadyPending;

        return MessengerValidationResult.Ok;
    }

    public bool RollAccident()
    {
        if (!_settings.MessengerAccidents)
            return false;

        var chance = _config.GetConfig().AccidentChancePerHour;
        if (chance <= 0f)
            return false;
        if (chance >= 1f)
            return true;

        return _random.NextFloat() < chance;
    }

    public PositionUpdate AdvancePosition(Vec2 currentPosition, Vec2 targetPosition, float speed)
    {
        if (!targetPosition.IsValid)
            return new PositionUpdate(currentPosition, false);

        var direction = targetPosition - currentPosition;
        var distance = direction.Length;

        if (distance <= speed)
            return new PositionUpdate(targetPosition, true);

        var stepped = currentPosition + (direction.Normalized() * speed);
        return new PositionUpdate(stepped, false);
    }

    public float CalculateMessengerSpeed(float mapDiagonal, int travelDays)
    {
        var clampedDays = Math.Max(travelDays, 1);
        var baseSpeed = mapDiagonal / (24f * clampedDays);
        var multiplier = _config.GetConfig().TravelSpeedMultiplier;
        return baseSpeed * multiplier;
    }
}
===== C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerSettingsProvider.cs
namespace TAOM.Features.Messengers;

public class MessengerSettingsProvider : IMessengerSettingsProvider
{
    public bool EnableMessengers => TaomSettings.Instance?.EnableMessengers ?? true;
    public int MessengerGoldCost => TaomSettings.Instance?.MessengerGoldCost ?? 50;
    public int MessengerTravelDays => TaomSettings.Instance?.MessengerTravelDays ?? 3;
    public bool MessengerAccidents => TaomSettings.Instance?.MessengerAccidents ?? true;
}
===== C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerStateStore.cs
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.Messengers.Domain;

namespace TAOM.Features.Messengers;

public class MessengerStateStore : IMessengerStateStore
{
    private readonly Dictionary<string, PendingMessenger> _messengers = new Dictionary<string, PendingMessenger>();
    private readonly IModLogger _logger;

    public MessengerStateStore(IModLogger logger)
    {
        _logger = logger;
    }

    public int Count => _messengers.Count;

    public void Add(PendingMessenger messenger)
    {
        if (messenger == null || string.IsNullOrEmpty(messenger.TargetHeroId))
            return;
        _messengers[messenger.TargetHeroId] = messenger;
    }

    public bool Remove(string heroId)
    {
        if (string.IsNullOrEmpty(heroId))
            return false;
        return _messengers.Remove(heroId);
    }

    public PendingMessenger Get(string heroId)
    {
        if (string.IsNullOrEmpty(heroId))
            return null;
        _messengers.TryGetValue(heroId, out var m);
        return m;
    }

    public bool Contains(string heroId) => !string.IsNullOrEmpty(heroId) && _messengers.ContainsKey(heroId);

    public IReadOnlyList<PendingMessenger> GetAll()
    {
        var list = new List<PendingMessenger>(_messengers.Count);
        foreach (var kvp in _messengers)
            list.Add(kvp.Value);
        return list;
    }

    public void Clear() => _messengers.Clear();

    public Dictionary<string, string> Serialize()
    {
        var dict = new Dictionary<string, string>(_messengers.Count);
        foreach (var kvp in _messengers)
            dict[kvp.Key] = kvp.Value.Serialize();
        return dict;
    }

    public void Deserialize(IReadOnlyDictionary<string, string> data)
    {
        _messengers.Clear();
        if (data == null)
            return;

        foreach (var kvp in data)
        {
            if (PendingMessenger.TryDeserialize(kvp.Key, kvp.Value, out var messenger))
            {
                _messengers[kvp.Key] = messenger;
            }
            else
            {
                _logger?.LogWarning($"MessengerStateStore: dropped malformed entry for heroId={kvp.Key}");
            }
        }
    }
}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Recurse -File Main,TAOM.Tests | Select-String -Pattern \"MessengerIoC|MessengerCampaignBehavior|IMessengerService|SendMessenger\\(\" | Select-Object Path,LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
 succeeded in 42596ms:

[32;1mPath                                                                                         [0m[32;1m LineNumber[0m[32;1m Line[0m
[32;1m----                                                                                         [0m [32;1m----------[0m [32;1m----[0m
C:\Users\mikew\source\repos\TAOM\Main\bin\Debug\net472\TAOM.dll                                    14702  e �' e( …
C:\Users\mikew\source\repos\TAOM\Main\bin\Debug\net472\TAOM.pdb                                     1232 ��C�ֱ�2[�}��…
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\IMessengerService.cs                         6 public interf…
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\IMessengerService.cs                         8     Messenger…
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerCampaignBehavior.cs                19 public class …
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerCampaignBehavior.cs                21     private r…
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerCampaignBehavior.cs                34     public Me…
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerCampaignBehavior.cs                35         IMess…
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerCampaignBehavior.cs                75     public vo…
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerCampaignBehavior.cs                79         var v…
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerCampaignBehavior.cs               108     public bo…
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerCampaignBehavior.cs               110         var v…
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerCampaignBehavior.cs               423         retur…
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerCampaignBehavior.cs               436         SendM…
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerIoC.cs                              5 public static…
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerIoC.cs                             13         conta…
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerService.cs                          7 public class …
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerService.cs                         26     public Me…
C:\Users\mikew\source\repos\TAOM\Main\obj\Debug\net472\TAOM.dll                                    14702  e �' e( …
C:\Users\mikew\source\repos\TAOM\Main\obj\Debug\net472\TAOM.pdb                                     1232 ��C�ֱ�2[�}��…
C:\Users\mikew\source\repos\TAOM\Main\_Module\bin\Gaming.Desktop.x64_Shipping_Client\TAOM.dll      14702  e �' e( …
C:\Users\mikew\source\repos\TAOM\Main\_Module\bin\Gaming.Desktop.x64_Shipping_Client\TAOM.pdb       1232 ��C�ֱ�2[�}��…
C:\Users\mikew\source\repos\TAOM\Main\_Module\bin\Win64_Shipping_Client\TAOM.dll                   14702  e �' e( …
C:\Users\mikew\source\repos\TAOM\Main\_Module\bin\Win64_Shipping_Client\TAOM.pdb                    1232 ��C�ֱ�2[�}��…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\bin\Debug\net472\TAOM.dll                              14702  e �' e( …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\bin\Debug\net472\TAOM.pdb                               1232 ��C�ֱ�2[�}��…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\Messengers\MessengerServiceTests.cs              50         var r…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\Messengers\MessengerServiceTests.cs              62         var r…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\Messengers\MessengerServiceTests.cs              75         var r…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\Messengers\MessengerServiceTests.cs              88         var r…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\Messengers\MessengerServiceTests.cs             101         var r…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\Messengers\MessengerServiceTests.cs             114         var r…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\Messengers\MessengerServiceTests.cs             127         var r…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\Messengers\MessengerServiceTests.cs             140         var r…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\Messengers\MessengerServiceTests.cs             150         var r…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\Messengers\MessengerServiceTests.cs             160         var r…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\Messengers\MessengerServiceTests.cs             173         var r…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\Messengers\MessengerServiceTests.cs             181         var r…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\Messengers\MessengerServiceTests.cs             191         var r…


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'foreach ($f in Get-ChildItem -Recurse -File TAOM.Tests'"\\Features\\Messengers\\*.cs,TAOM.Tests\\Features\\SiegeDismount\\*.cs) { \"===== "'$($f.FullName)"; Get-Content $f.FullName }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 501ms:
===== C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\Messengers\MessengerConfigProviderTests.cs
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Messengers;

namespace TAOM.Tests.Features.Messengers;

[TestClass]
public class MessengerConfigProviderTests
{
    private string _tempDir = null!;
    private string _messengersDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;
    private MessengerConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_Messengers_" + Path.GetRandomFileName());
        _messengersDir = Path.Combine(_tempDir, "messengers");
        Directory.CreateDirectory(_messengersDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new MessengerConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_messengersDir, "messenger_config.json"), json);

    [TestMethod]
    public void GetConfig_ValidJson_ParsesAllFields()
    {
        WriteConfig(@"{
  ""accidentChancePerHour"": 0.005,
  ""travelSpeedMultiplier"": 1.5
}");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.005f, config.AccidentChancePerHour, 0.00001f);
        Assert.AreEqual(1.5f, config.TravelSpeedMultiplier, 0.0001f);
    }

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsDefaultsAndLogsInfo()
    {
        var config = _sut.GetConfig();

        Assert.AreEqual(0.002f, config.AccidentChancePerHour, 0.00001f);
        Assert.AreEqual(1.0f, config.TravelSpeedMultiplier, 0.0001f);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_ReturnsDefaultsAndLogsError()
    {
        WriteConfig("not valid {{{");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.002f, config.AccidentChancePerHour, 0.00001f);
        Assert.AreEqual(1.0f, config.TravelSpeedMultiplier, 0.0001f);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetConfig_NegativeAccidentChance_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""accidentChancePerHour"": -0.1 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.002f, config.AccidentChancePerHour, 0.00001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("accidentChancePerHour=-0.1")));
    }

    [TestMethod]
    public void GetConfig_AccidentChanceAboveOne_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""accidentChancePerHour"": 1.5 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.002f, config.AccidentChancePerHour, 0.00001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("accidentChancePerHour=1.5")));
    }

    [TestMethod]
    public void GetConfig_ZeroSpeedMultiplier_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""travelSpeedMultiplier"": 0.0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1.0f, config.TravelSpeedMultiplier, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("travelSpeedMultiplier=0")));
    }

    [TestMethod]
    public void GetConfig_NegativeSpeedMultiplier_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""travelSpeedMultiplier"": -1.0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1.0f, config.TravelSpeedMultiplier, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("travelSpeedMultiplier=-1")));
    }

    [TestMethod]
    public void GetConfig_AbsurdSpeedMultiplier_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""travelSpeedMultiplier"": 100.0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1.0f, config.TravelSpeedMultiplier, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("travelSpeedMultiplier=100")));
    }

    [TestMethod]
    public void GetConfig_BoundaryAccidentChanceZero_Accepted()
    {
        WriteConfig(@"{ ""accidentChancePerHour"": 0.0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.0f, config.AccidentChancePerHour, 0.00001f);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("accidentChancePerHour")));
    }

    [TestMethod]
    public void GetConfig_BoundaryAccidentChanceOne_Accepted()
    {
        WriteConfig(@"{ ""accidentChancePerHour"": 1.0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1.0f, config.AccidentChancePerHour, 0.00001f);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("accidentChancePerHour")));
    }

    [TestMethod]
    public void GetConfig_PartialJson_MergesWithDefaults()
    {
        WriteConfig(@"{ ""accidentChancePerHour"": 0.01 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.01f, config.AccidentChancePerHour, 0.00001f);
        Assert.AreEqual(1.0f, config.TravelSpeedMultiplier, 0.0001f);
    }

    [TestMethod]
    public void GetConfig_CalledTwice_ReturnsSameCachedInstance()
    {
        WriteConfig(@"{ ""accidentChancePerHour"": 0.005 }");

        var first = _sut.GetConfig();
        var second = _sut.GetConfig();

        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void GetConfig_ValidValues_LogsInfoNotWarning()
    {
        WriteConfig(@"{ ""accidentChancePerHour"": 0.003, ""travelSpeedMultiplier"": 1.2 }");

        _sut.GetConfig();

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("outside")));
    }

    [TestMethod]
    public void DefaultConfig_MatchesLOTRAOMSpec()
    {
        var config = new MessengerConfig();

        Assert.AreEqual(0.002f, config.AccidentChancePerHour, 0.00001f,
            "Default accident chance should be 0.2%/hr to match LOTRAOM");
        Assert.AreEqual(1.0f, config.TravelSpeedMultiplier, 0.0001f,
            "Default speed multiplier should be 1.0 (no scaling)");
    }
}
===== C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\Messengers\MessengerServiceTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Library;
using TAOM.Features.Messengers;
using TAOM.Features.Messengers.Domain;

namespace TAOM.Tests.Features.Messengers;

[TestClass]
public class MessengerServiceTests
{
    private IMessengerSettingsProvider _settings = null!;
    private IMessengerConfigProvider _config = null!;
    private IMessengerStateStore _store = null!;
    private IMessengerRandomSource _random = null!;
    private MessengerService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IMessengerSettingsProvider>();
        _settings.EnableMessengers.Returns(true);
        _settings.MessengerGoldCost.Returns(50);
        _settings.MessengerTravelDays.Returns(3);
        _settings.MessengerAccidents.Returns(true);

        _config = Substitute.For<IMessengerConfigProvider>();
        _config.GetConfig().Returns(new MessengerConfig());

        _store = Substitute.For<IMessengerStateStore>();
        _store.Contains(Arg.Any<string>()).Returns(false);

        _random = Substitute.For<IMessengerRandomSource>();
        _random.NextFloat().Returns(0.5f);

        _sut = new MessengerService(_settings, _config, _store, _random);
    }

    private static HeroSnapshot ValidActiveHero(string id = "lord_1") =>
        new HeroSnapshot(id,
            isAlive: true, isPrisoner: false, isChild: false, isFugitive: false,
            isActive: true, isWanderer: false, isHumanPlayerCharacter: false,
            isInPlayerParty: false, isInMapEvent: false);

    // --- Validation: skip-guard exhaustion ---

    [TestMethod]
    public void CanSendMessenger_NullTarget_ReturnsNullTarget()
    {
        var result = _sut.CanSendMessenger(null, playerGold: 1000);
        Assert.AreEqual(MessengerValidationResult.NullTarget, result);
    }

    [TestMethod]
    public void CanSendMessenger_PlayerCharacter_ReturnsHumanPlayerCharacter()
    {
        var hero = new HeroSnapshot("player",
            isAlive: true, isPrisoner: false, isChild: false, isFugitive: false,
            isActive: true, isWanderer: false, isHumanPlayerCharacter: true,
            isInPlayerParty: true, isInMapEvent: false);

        var result = _sut.CanSendMessenger(hero, playerGold: 1000);

        Assert.AreEqual(MessengerValidationResult.HumanPlayerCharacter, result);
    }

    [TestMethod]
    public void CanSendMessenger_DeadHero_ReturnsHeroDead()
    {
        var hero = new HeroSnapshot("lord_dead",
            isAlive: false, isPrisoner: false, isChild: false, isFugitive: false,
            isActive: false, isWanderer: false, isHumanPlayerCharacter: false,
            isInPlayerParty: false, isInMapEvent: false);

        var result = _sut.CanSendMessenger(hero, playerGold: 1000);

        Assert.AreEqual(MessengerValidationResult.HeroDead, result);
    }

    [TestMethod]
    public void CanSendMessenger_PrisonerHero_ReturnsHeroPrisoner()
    {
        var hero = new HeroSnapshot("lord_prisoner",
            isAlive: true, isPrisoner: true, isChild: false, isFugitive: false,
            isActive: false, isWanderer: false, isHumanPlayerCharacter: false,
            isInPlayerParty: false, isInMapEvent: false);

        var result = _sut.CanSendMessenger(hero, playerGold: 1000);

        Assert.AreEqual(MessengerValidationResult.HeroPrisoner, result);
    }

    [TestMethod]
    public void CanSendMessenger_ChildHero_ReturnsHeroChild()
    {
        var hero = new HeroSnapshot("lord_child",
            isAlive: true, isPrisoner: false, isChild: true, isFugitive: false,
            isActive: true, isWanderer: false, isHumanPlayerCharacter: false,
            isInPlayerParty: false, isInMapEvent: false);

        var result = _sut.CanSendMessenger(hero, playerGold: 1000);

        Assert.AreEqual(MessengerValidationResult.HeroChild, result);
    }

    [TestMethod]
    public void CanSendMessenger_FugitiveHero_ReturnsHeroFugitive()
    {
        var hero = new HeroSnapshot("lord_fugitive",
            isAlive: true, isPrisoner: false, isChild: false, isFugitive: true,
            isActive: false, isWanderer: false, isHumanPlayerCharacter: false,
            isInPlayerParty: false, isInMapEvent: false);

        var result = _sut.CanSendMessenger(hero, playerGold: 1000);

        Assert.AreEqual(MessengerValidationResult.HeroFugitive, result);
    }

    [TestMethod]
    public void CanSendMessenger_HeroNotActiveAndNotWanderer_ReturnsTargetUnavailable()
    {
        var hero = new HeroSnapshot("lord_idle",
            isAlive: true, isPrisoner: false, isChild: false, isFugitive: false,
            isActive: false, isWanderer: false, isHumanPlayerCharacter: false,
            isInPlayerParty: false, isInMapEvent: false);

        var result = _sut.CanSendMessenger(hero, playerGold: 1000);

        Assert.AreEqual(MessengerValidationResult.TargetUnavailable, result);
    }

    [TestMethod]
    public void CanSendMessenger_TargetInPlayerParty_ReturnsTargetInPlayerParty()
    {
        var hero = new HeroSnapshot("companion",
            isAlive: true, isPrisoner: false, isChild: false, isFugitive: false,
            isActive: true, isWanderer: true, isHumanPlayerCharacter: false,
            isInPlayerParty: true, isInMapEvent: false);

        var result = _sut.CanSendMessenger(hero, playerGold: 1000);

        Assert.AreEqual(MessengerValidationResult.TargetInPlayerParty, result);
    }

    [TestMethod]
    public void CanSendMessenger_InsufficientGold_ReturnsInsufficientGold()
    {
        _settings.MessengerGoldCost.Returns(50);

        var result = _sut.CanSendMessenger(ValidActiveHero(), playerGold: 49);

        Assert.AreEqual(MessengerValidationResult.InsufficientGold, result);
    }

    [TestMethod]
    public void CanSendMessenger_AlreadyPending_ReturnsAlreadyPending()
    {
        _store.Contains("lord_1").Returns(true);

        var result = _sut.CanSendMessenger(ValidActiveHero(), playerGold: 1000);

        Assert.AreEqual(MessengerValidationResult.AlreadyPending, result);
    }

    [TestMethod]
    public void CanSendMessenger_NotSpawnedWanderer_AllowedAsTarget()
    {
        var wanderer = new HeroSnapshot("wanderer_1",
            isAlive: true, isPrisoner: false, isChild: false, isFugitive: false,
            isActive: false, isWanderer: true, isHumanPlayerCharacter: false,
            isInPlayerParty: false, isInMapEvent: false);

        var result = _sut.CanSendMessenger(wanderer, playerGold: 1000);

        Assert.AreEqual(MessengerValidationResult.Ok, result);
    }

    [TestMethod]
    public void CanSendMessenger_ValidActiveHero_ReturnsOk()
    {
        var result = _sut.CanSendMessenger(ValidActiveHero(), playerGold: 1000);

        Assert.AreEqual(MessengerValidationResult.Ok, result);
    }

    [TestMethod]
    public void CanSendMessenger_ExactlyEnoughGold_ReturnsOk()
    {
        _settings.MessengerGoldCost.Returns(50);

        var result = _sut.CanSendMessenger(ValidActiveHero(), playerGold: 50);

        Assert.AreEqual(MessengerValidationResult.Ok, result);
    }

    // --- RollAccident ---

    [TestMethod]
    public void RollAccident_AccidentsDisabledInSettings_ReturnsFalse()
    {
        _settings.MessengerAccidents.Returns(false);
        _random.NextFloat().Returns(0.0f);

        Assert.IsFalse(_sut.RollAccident());
    }

    [TestMethod]
    public void RollAccident_ChanceZero_ReturnsFalse()
    {
        _config.GetConfig().Returns(new MessengerConfig { AccidentChancePerHour = 0f });
        _random.NextFloat().Returns(0f);

        Assert.IsFalse(_sut.RollAccident());
    }

    [TestMethod]
    public void RollAccident_ChanceOne_ReturnsTrue()
    {
        _config.GetConfig().Returns(new MessengerConfig { AccidentChancePerHour = 1f });
        _random.NextFloat().Returns(0.999f);

        Assert.IsTrue(_sut.RollAccident());
    }

    [TestMethod]
    public void RollAccident_RandomBelowChance_ReturnsTrue()
    {
        _config.GetConfig().Returns(new MessengerConfig { AccidentChancePerHour = 0.1f });
        _random.NextFloat().Returns(0.05f);

        Assert.IsTrue(_sut.RollAccident());
    }

    [TestMethod]
    public void RollAccident_RandomAboveChance_ReturnsFalse()
    {
        _config.GetConfig().Returns(new MessengerConfig { AccidentChancePerHour = 0.1f });
        _random.NextFloat().Returns(0.5f);

        Assert.IsFalse(_sut.RollAccident());
    }

    // --- AdvancePosition ---

    [TestMethod]
    public void AdvancePosition_DistanceLessThanSpeed_SnapsAndArrives()
    {
        var current = new Vec2(0f, 0f);
        var target = new Vec2(3f, 4f);   // distance = 5
        var speed = 10f;

        var update = _sut.AdvancePosition(current, target, speed);

        Assert.IsTrue(update.Arrived);
        Assert.AreEqual(target.X, update.NewPosition.X, 0.0001f);
        Assert.AreEqual(target.Y, update.NewPosition.Y, 0.0001f);
    }

    [TestMethod]
    public void AdvancePosition_DistanceEqualToSpeed_SnapsAndArrives()
    {
        var current = new Vec2(0f, 0f);
        var target = new Vec2(3f, 4f);   // distance = 5
        var speed = 5f;

        var update = _sut.AdvancePosition(current, target, speed);

        Assert.IsTrue(update.Arrived);
    }

    [TestMethod]
    public void AdvancePosition_DistanceGreaterThanSpeed_StepsTowardTarget()
    {
        var current = new Vec2(0f, 0f);
        var target = new Vec2(10f, 0f);
        var speed = 2f;

        var update = _sut.AdvancePosition(current, target, speed);

        Assert.IsFalse(update.Arrived);
        Assert.AreEqual(2f, update.NewPosition.X, 0.0001f);
        Assert.AreEqual(0f, update.NewPosition.Y, 0.0001f);
    }

    [TestMethod]
    public void AdvancePosition_InvalidTarget_ReturnsCurrentNotArrived()
    {
        var current = new Vec2(5f, 5f);
        var update = _sut.AdvancePosition(current, Vec2.Invalid, speed: 10f);

        Assert.IsFalse(update.Arrived);
        Assert.AreEqual(current.X, update.NewPosition.X);
        Assert.AreEqual(current.Y, update.NewPosition.Y);
    }

    // --- CalculateMessengerSpeed ---

    [TestMethod]
    public void CalculateMessengerSpeed_StandardInputs_DivBy24TimesDays()
    {
        var speed = _sut.CalculateMessengerSpeed(mapDiagonal: 240f, travelDays: 5);

        // 240 / (24 * 5) = 2.0; multiplier = 1.0
        Assert.AreEqual(2.0f, speed, 0.0001f);
    }

    [TestMethod]
    public void CalculateMessengerSpeed_ZeroDays_ClampsToOne()
    {
        var speed = _sut.CalculateMessengerSpeed(mapDiagonal: 240f, travelDays: 0);

        // 240 / (24 * 1) = 10.0
        Assert.AreEqual(10.0f, speed, 0.0001f);
    }

    [TestMethod]
    public void CalculateMessengerSpeed_NegativeDays_ClampsToOne()
    {
        var speed = _sut.CalculateMessengerSpeed(mapDiagonal: 240f, travelDays: -3);

        Assert.AreEqual(10.0f, speed, 0.0001f);
    }

    [TestMethod]
    public void CalculateMessengerSpeed_DoubleMultiplier_DoublesSpeed()
    {
        _config.GetConfig().Returns(new MessengerConfig { TravelSpeedMultiplier = 2.0f });

        var speed = _sut.CalculateMessengerSpeed(mapDiagonal: 240f, travelDays: 5);

        Assert.AreEqual(4.0f, speed, 0.0001f);
    }
}
===== C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\Messengers\MessengerStateStoreTests.cs
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Library;
using TAOM.Core.Logging;
using TAOM.Features.Messengers;
using TAOM.Features.Messengers.Domain;

namespace TAOM.Tests.Features.Messengers;

[TestClass]
public class MessengerStateStoreTests
{
    private IModLogger _logger = null!;
    private MessengerStateStore _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _sut = new MessengerStateStore(_logger);
    }

    private static PendingMessenger Make(string id, double dispatchDays = 1.0, float x = 1.5f, float y = 2.5f, bool arrived = false) =>
        new PendingMessenger(id, dispatchDays, new Vec2(x, y), arrived);

    [TestMethod]
    public void Add_NewMessenger_StoredAndCountIncreases()
    {
        _sut.Add(Make("h1"));
        Assert.AreEqual(1, _sut.Count);
        Assert.IsTrue(_sut.Contains("h1"));
    }

    [TestMethod]
    public void Add_DuplicateHeroId_ReplacesExisting()
    {
        _sut.Add(Make("h1", dispatchDays: 1.0));
        _sut.Add(Make("h1", dispatchDays: 2.0));

        Assert.AreEqual(1, _sut.Count);
        Assert.AreEqual(2.0, _sut.Get("h1").DispatchTimeDays, 0.0001);
    }

    [TestMethod]
    public void Add_NullMessenger_NoOp()
    {
        _sut.Add(null);
        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void Add_EmptyHeroId_NoOp()
    {
        _sut.Add(new PendingMessenger("", 0.0, Vec2.Zero, false));
        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void Remove_ExistingHero_ReturnsTrueAndRemoves()
    {
        _sut.Add(Make("h1"));

        var removed = _sut.Remove("h1");

        Assert.IsTrue(removed);
        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void Remove_NonexistentHero_ReturnsFalse()
    {
        var removed = _sut.Remove("nope");
        Assert.IsFalse(removed);
    }

    [TestMethod]
    public void Get_ExistingHero_ReturnsMessenger()
    {
        var m = Make("h1");
        _sut.Add(m);

        var fetched = _sut.Get("h1");

        Assert.AreSame(m, fetched);
    }

    [TestMethod]
    public void Get_NonexistentHero_ReturnsNull()
    {
        Assert.IsNull(_sut.Get("nope"));
    }

    [TestMethod]
    public void Contains_EmptyId_ReturnsFalse()
    {
        Assert.IsFalse(_sut.Contains(""));
        Assert.IsFalse(_sut.Contains(null));
    }

    [TestMethod]
    public void GetAll_MultipleEntries_ReturnsAll()
    {
        _sut.Add(Make("h1"));
        _sut.Add(Make("h2"));
        _sut.Add(Make("h3"));

        var all = _sut.GetAll();

        Assert.AreEqual(3, all.Count);
    }

    [TestMethod]
    public void Clear_RemovesAll()
    {
        _sut.Add(Make("h1"));
        _sut.Add(Make("h2"));

        _sut.Clear();

        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void Serialize_Deserialize_RoundtripPreservesAllFields()
    {
        var original = Make("h1", dispatchDays: 12.345, x: 7.5f, y: 11.25f, arrived: true);
        _sut.Add(original);
        _sut.Add(Make("h2", dispatchDays: 0.99, x: -3.5f, y: 0.5f, arrived: false));

        var serialized = _sut.Serialize();

        var restored = new MessengerStateStore(_logger);
        restored.Deserialize(serialized);

        Assert.AreEqual(2, restored.Count);
        var roundtrip = restored.Get("h1");
        Assert.AreEqual(12.345, roundtrip.DispatchTimeDays, 0.0001);
        Assert.AreEqual(7.5f, roundtrip.Position.X, 0.0001f);
        Assert.AreEqual(11.25f, roundtrip.Position.Y, 0.0001f);
        Assert.IsTrue(roundtrip.Arrived);
    }

    [TestMethod]
    public void Deserialize_NullData_ClearsExisting()
    {
        _sut.Add(Make("h1"));

        _sut.Deserialize(null);

        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void Deserialize_MalformedEntry_DropsAndLogs()
    {
        var data = new Dictionary<string, string>
        {
            { "h_good", "1.5|1.5|2.5|0" },
            { "h_bad", "garbage" },
        };

        _sut.Deserialize(data);

        Assert.AreEqual(1, _sut.Count);
        Assert.IsTrue(_sut.Contains("h_good"));
        Assert.IsFalse(_sut.Contains("h_bad"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("h_bad")));
    }

    [TestMethod]
    public void Deserialize_ReplacesExistingState()
    {
        _sut.Add(Make("old"));

        var data = new Dictionary<string, string> { { "h_new", "1.5|1.5|2.5|0" } };
        _sut.Deserialize(data);

        Assert.IsFalse(_sut.Contains("old"));
        Assert.IsTrue(_sut.Contains("h_new"));
    }
}
===== C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SiegeDismount\SiegeDismountServiceTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.SiegeDismount;
using TAOM.Features.SiegeDismount.Models;

namespace TAOM.Tests.Features.SiegeDismount;

[TestClass]
public class SiegeDismountServiceTests
{
    private ISiegeDismountSettingsProvider _settings = null!;
    private IPlayerMountAdapter _mount = null!;
    private IPartyMountInventoryAdapter _inventory = null!;
    private IModLogger _logger = null!;
    private SiegeDismountService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<ISiegeDismountSettingsProvider>();
        _mount = Substitute.For<IPlayerMountAdapter>();
        _inventory = Substitute.For<IPartyMountInventoryAdapter>();
        _logger = Substitute.For<IModLogger>();

        _settings.IsEnabled.Returns(true);
        _settings.IsDebugMode.Returns(false);
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.AutoRemountAfter);
        _mount.HasMount().Returns(true);
        _mount.Capture().Returns(new MountSnapshot("horse_charger_west", "harness_chain"));

        _sut = new SiegeDismountService(_settings, _mount, _inventory, _logger);
    }

    // -------- Disable / inert paths --------

    [TestMethod]
    public void OnMissionStart_FeatureDisabled_DoesNothing()
    {
        _settings.IsEnabled.Returns(false);

        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_swadia_castle");

        _mount.DidNotReceive().Capture();
        _mount.DidNotReceive().Clear();
        _inventory.DidNotReceiveWithAnyArgs().Deposit(default!);
    }

    [TestMethod]
    public void OnMissionStart_NotASiegeMission_DoesNothing()
    {
        _sut.OnMissionStart(isSiegeBattle: false, sceneName: "field_battle_aserai");

        _mount.DidNotReceive().Capture();
        _mount.DidNotReceive().Clear();
    }

    [TestMethod]
    public void OnMissionStart_VanillaBehavior_DoesNotMutateEquipment()
    {
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.Vanilla);

        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _mount.DidNotReceive().Clear();
        _inventory.DidNotReceiveWithAnyArgs().Deposit(default!);
    }

    [TestMethod]
    public void OnMissionStart_PlayerOnFoot_DoesNotMutateAndLogsDebug()
    {
        _mount.HasMount().Returns(false);

        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _mount.DidNotReceive().Capture();
        _mount.DidNotReceive().Clear();
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    // -------- Siege detection: scene-name fallback --------

    [DataTestMethod]
    [DataRow("scene_with_siege_in_name")]
    [DataRow("settlement_wall_assault")]
    [DataRow("city_gate_breach")]
    [DataRow("breach_arena")]
    [DataRow("ASSAULT_CASTLE_C")]
    public void OnMissionStart_SceneNameContainsSiegeKeyword_TriggersDismount(string sceneName)
    {
        _sut.OnMissionStart(isSiegeBattle: false, sceneName: sceneName);

        _mount.Received(1).Capture();
    }

    [TestMethod]
    public void OnMissionStart_NullSceneName_NotASiege_DoesNothing()
    {
        _sut.OnMissionStart(isSiegeBattle: false, sceneName: null);

        _mount.DidNotReceive().Capture();
    }

    [TestMethod]
    public void OnMissionStart_EmptySceneName_NotASiege_DoesNothing()
    {
        _sut.OnMissionStart(isSiegeBattle: false, sceneName: "");

        _mount.DidNotReceive().Capture();
    }

    // -------- Regression: TAOM castle false-positives (deep-review GAP 2) --------
    // castle_orthanc_gate (Isengard) and castle_gundabad_wall (Gundabad) are real TAOM
    // settlement center scenes. Visiting those castles in non-siege missions would
    // previously match the "gate"/"wall" substring and clobber the player's mount.

    [DataTestMethod]
    [DataRow("castle_orthanc_gate")]
    [DataRow("castle_gundabad_wall")]
    [DataRow("castle_some_other_gate_settlement")]
    [DataRow("settlement_with_wall_in_name")]
    public void OnMissionStart_TaomCastleSceneWithGateOrWallSubstring_DoesNotTriggerDismount(string sceneName)
    {
        _sut.OnMissionStart(isSiegeBattle: false, sceneName: sceneName);

        _mount.DidNotReceive().Capture();
    }

    // -------- KeepOnMap: capture but don't move to inventory --------

    [TestMethod]
    public void OnMissionStart_DismountKeepOnMap_CapturesButDoesNotMoveToInventory()
    {
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.DismountKeepOnMap);

        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _mount.Received(1).Capture();
        _mount.DidNotReceive().Clear();
        _inventory.DidNotReceiveWithAnyArgs().Deposit(default!);
    }

    [TestMethod]
    public void OnMissionStart_DismountKeepOnMap_DoesNotMarkPendingRemount()
    {
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.DismountKeepOnMap);
        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _sut.OnMissionEnd();

        _mount.DidNotReceiveWithAnyArgs().Restore(default!);
        _inventory.DidNotReceiveWithAnyArgs().Withdraw(default!);
    }

    // -------- DismountToInventory: move + no auto-remount --------

    [TestMethod]
    public void OnMissionStart_DismountToInventory_ClearsAndDeposits()
    {
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.DismountToInventory);

        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _mount.Received(1).Clear();
        _inventory.Received(1).Deposit(Arg.Any<IMountSnapshot>());
    }

    [TestMethod]
    public void OnMissionStart_DismountToInventory_DoesNotRemountOnEnd()
    {
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.DismountToInventory);
        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _sut.OnMissionEnd();

        _mount.DidNotReceiveWithAnyArgs().Restore(default!);
        _inventory.DidNotReceiveWithAnyArgs().Withdraw(default!);
    }

    // -------- AutoRemountAfter: full round-trip --------

    [TestMethod]
    public void OnMissionStart_AutoRemount_ClearsAndDeposits()
    {
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.AutoRemountAfter);

        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _mount.Received(1).Clear();
        _inventory.Received(1).Deposit(Arg.Any<IMountSnapshot>());
    }

    [TestMethod]
    public void OnMissionEnd_AutoRemount_RestoresEquipmentAndWithdrawsFromInventory()
    {
        var captured = new MountSnapshot("horse_charger_west", "harness_chain");
        _mount.Capture().Returns(captured);
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.AutoRemountAfter);
        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _sut.OnMissionEnd();

        _mount.Received(1).Restore(captured);
        _inventory.Received(1).Withdraw(captured);
    }

    [TestMethod]
    public void OnMissionEnd_AutoRemountTriggeredTwice_OnlyRemountsOnce()
    {
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.AutoRemountAfter);
        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _sut.OnMissionEnd();
        _sut.OnMissionEnd();

        _mount.Received(1).Restore(Arg.Any<IMountSnapshot>());
        _inventory.Received(1).Withdraw(Arg.Any<IMountSnapshot>());
    }

    [TestMethod]
    public void OnMissionEnd_NoPriorMissionStart_DoesNothing()
    {
        _sut.OnMissionEnd();

        _mount.DidNotReceiveWithAnyArgs().Restore(default!);
        _inventory.DidNotReceiveWithAnyArgs().Withdraw(default!);
    }

    [TestMethod]
    public void OnMissionEnd_AfterDismountToInventory_DoesNotRemount()
    {
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.DismountToInventory);
        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _sut.OnMissionEnd();

        _mount.DidNotReceiveWithAnyArgs().Restore(default!);
    }

    // -------- Logging contracts (per CLAUDE.md mandatory logging rule) --------

    [TestMethod]
    public void OnMissionStart_FeatureDisabled_LogsInertOnce()
    {
        _settings.IsEnabled.Returns(false);

        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("disabled") && s.Contains("[SiegeDismount]")));
    }

    [TestMethod]
    public void OnMissionStart_SiegeDetected_LogsInfoWithSceneAndBehavior()
    {
        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_swadia_castle");

        _logger.Received().LogInfo(Arg.Is<string>(s =>
            s.Contains("[SiegeDismount]") && s.Contains("siege") && s.Contains("AutoRemountAfter")));
    }

    [TestMethod]
    public void OnMissionStart_AdapterThrows_LogsErrorAndDoesNotPropagate()
    {
        _mount.When(m => m.Clear()).Do(_ => throw new System.InvalidOperationException("equipment slot null"));
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.AutoRemountAfter);

        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _logger.Received().LogError(Arg.Is<string>(s =>
            s.Contains("[SiegeDismount]") && s.Contains("equipment slot null")));
    }

    [TestMethod]
    public void OnMissionEnd_RestoreThrows_LogsErrorAndDoesNotPropagate()
    {
        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");
        _mount.When(m => m.Restore(Arg.Any<IMountSnapshot>()))
              .Do(_ => throw new System.InvalidOperationException("hero is null"));

        _sut.OnMissionEnd();

        _logger.Received().LogError(Arg.Is<string>(s =>
            s.Contains("[SiegeDismount]") && s.Contains("hero is null")));
    }

    // -------- Regression: out-of-range MountBehavior enum (deep-review GAP 1) --------
    // If a user manually edits TAOM.json and sets SiegeMountBehavior to 99, the cast
    // produces an undefined enum value. Switch must have a default: case that logs
    // a warning and treats as a no-op (Vanilla equivalent), not silently capture
    // mount data without acting.

    [TestMethod]
    public void OnMissionStart_OutOfRangeMountBehavior_LogsWarningAndNoOps()
    {
        _settings.MountBehavior.Returns((SiegeMountBehaviorType)99);

        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _mount.DidNotReceive().Clear();
        _inventory.DidNotReceiveWithAnyArgs().Deposit(default!);
        _logger.Received().LogWarning(Arg.Is<string>(s =>
            s.Contains("[SiegeDismount]") && s.Contains("99")));
    }

    [TestMethod]
    public void OnMissionStart_OutOfRangeMountBehavior_DoesNotMarkPendingRemount()
    {
        _settings.MountBehavior.Returns((SiegeMountBehaviorType)99);
        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _sut.OnMissionEnd();

        _mount.DidNotReceiveWithAnyArgs().Restore(default!);
        _inventory.DidNotReceiveWithAnyArgs().Withdraw(default!);
    }

    // -------- Regression: state hygiene — snapshot cleanup (deep-review KL 1) --------

    [TestMethod]
    public void OnMissionStart_SecondSiegeAfterDismountToInventory_OverwritesStaleSnapshot()
    {
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.DismountToInventory);
        var firstSnapshot = new MountSnapshot("horse_charger_west", null);
        var secondSnapshot = new MountSnapshot("horse_destrier_east", "harness_chain");
        _mount.Capture().Returns(firstSnapshot, secondSnapshot);

        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");
        _sut.OnMissionEnd();
        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");
        _inventory.ClearReceivedCalls();
        _sut.OnMissionEnd();

        // After two cycles ending without auto-remount, no Restore/Withdraw should fire on either OnMissionEnd.
        _mount.DidNotReceiveWithAnyArgs().Restore(default!);
        _inventory.DidNotReceiveWithAnyArgs().Withdraw(default!);
    }

    // -------- Regression: HasMount guard after Capture (deep-review KL 3) --------

    [TestMethod]
    public void OnMissionStart_CaptureReturnsEmptySnapshot_DoesNotClearOrDeposit()
    {
        _mount.HasMount().Returns(true); // adapter says yes
        _mount.Capture().Returns(MountSnapshot.Empty); // but capture returns empty
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.AutoRemountAfter);

        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _mount.DidNotReceive().Clear();
        _inventory.DidNotReceiveWithAnyArgs().Deposit(default!);
    }

    [TestMethod]
    public void OnMissionStart_CaptureReturnsEmptySnapshot_LogsWarning()
    {
        _mount.HasMount().Returns(true);
        _mount.Capture().Returns(MountSnapshot.Empty);
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.AutoRemountAfter);

        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _logger.Received().LogWarning(Arg.Is<string>(s =>
            s.Contains("[SiegeDismount]") && s.Contains("HasMount returned true but capture was empty")));
    }
}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Recurse -Filter AssemblyInfo.cs Main | Select-String -Pattern 'InternalsVisibleTo' -Context 0; Select-String -Path Main\\*.csproj,TAOM.Tests\\*.csproj -Pattern 'InternalsVisibleTo|LangVersion'" in C:\Users\mikew\source\repos\TAOM
 succeeded in 493ms:

Main\TAOM.csproj:88:	<!-- [7mInternalsVisibleTo[0m for unit testing and TAOM_Online multiplayer module -->
Main\TAOM.csproj:90:		<AssemblyAttribute Include="System.Runtime.CompilerServices.[7mInternalsVisibleTo[0m">
Main\TAOM.csproj:93:		<AssemblyAttribute Include="System.Runtime.CompilerServices.[7mInternalsVisibleTo[0m">


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content Main\\TAOM.csproj | Select-Object -Skip 75 -First 25" in C:\Users\mikew\source\repos\TAOM
 succeeded in 455ms:
			<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
		</PackageReference>
		<PackageReference Include="Bannerlord.MCM" Version="5.11.3" IncludeAssets="compile" />
		<PackageReference Include="DryIoc.dll" Version="4.8.8" />
		<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
		<PackageReference Include="Lib.Harmony" Version="2.4.2" IncludeAssets="compile" />
		<PackageReference Include="Harmony.Extensions" Version="3.2.0.77" PrivateAssets="all" />
		<PackageReference Include="BUTR.Harmony.Analyzer" Version="1.0.1.50" PrivateAssets="all" />
		<PackageReference Include="Nullable" Version="1.3.1" PrivateAssets="all" IncludeAssets="runtime; build; native; contentfiles; analyzers; buildtransitive" />
		<PackageReference Include="IsExternalInit" Version="1.0.3" PrivateAssets="all" IncludeAssets="runtime; build; native; contentfiles; analyzers; buildtransitive" />
	</ItemGroup>

	<!-- InternalsVisibleTo for unit testing and TAOM_Online multiplayer module -->
	<ItemGroup>
		<AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
			<_Parameter1>TAOM.Tests</_Parameter1>
		</AssemblyAttribute>
		<AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
			<_Parameter1>TAOM_Online</_Parameter1>
		</AssemblyAttribute>
	</ItemGroup>
	<ItemGroup>
	  <Content Include="_Module\GUI\.vs\GUI\v17\DocumentLayout.json" />
	  <Content Include="_Module\GUI\.vs\VSWorkspaceState.json" />
	</ItemGroup>

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Get-Command ilspycmd -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source' in C:\Users\mikew\source\repos\TAOM
 succeeded in 447ms:
C:\Users\mikew\.dotnet\tools\ilspycmd.exe

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'ilspycmd --help | Select-Object -First 40' in C:\Users\mikew\source\repos\TAOM
 succeeded in 534ms:
ilspycmd: 9.1.0.7988
ICSharpCode.Decompiler: 9.1.0.7988

dotnet tool for decompiling .NET assemblies and generating portable PDBs

Usage: ilspycmd [options] <Assembly file name(s)>

Arguments:
  Assembly file name(s)                   The list of assemblies that is being
                                          decompiled. This argument is
                                          mandatory.

Options:
  -v|--version                            Show version of ICSharpCode.Decompiler
                                          used.
  -h|--help                               Show help information.
  -o|--outputdir <directory>              The output directory, if omitted
                                          decompiler output is written to
                                          standard out.
  -p|--project                            Decompile assembly as compilable
                                          project. This requires the output
                                          directory option.
  -t|--type <type-name>                   The fully qualified name of the type
                                          to decompile.
  -il|--ilcode                            Show IL code.
  --il-sequence-points                    Show IL with sequence points. Implies
                                          -il.
  -genpdb|--generate-pdb                  Generate PDB.
  -usepdb|--use-varnames-from-pdb         Use variable names from PDB.
  -l|--list <entity-type(s)>              Lists all entities of the specified
                                          type(s). Valid types: c(lass),
                                          i(nterface), s(truct), d(elegate),
                                          e(num)
  -lv|--languageversion <version>         C# Language version: CSharp1, CSharp2,
                                          CSharp3, CSharp4, CSharp5, CSharp6,
                                          CSharp7, CSharp7_1, CSharp7_2,
                                          CSharp7_3, CSharp8_0, CSharp9_0,
                                          CSharp10_0, Preview or Latest
                                          Allowed values are: CSharp1, CSharp2,
                                          CSharp3, CSharp4, CSharp5, CSharp6,

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client' -Filter 'TaleWorlds.ObjectSystem.dll' | Select-Object FullName; Get-ChildItem 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client' -Filter 'TaleWorlds.Core.dll' | Select-Object FullName; Get-ChildItem 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client' -Filter 'TaleWorlds.CampaignSystem.dll' | Select-Object FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 474ms:

[32;1mFullName[0m
[32;1m--------[0m
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.ObjectSystem.dll
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.Core.dll
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.CampaignSystem.dll


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.ObjectSystem.dll'; ilspycmd -t TaleWorlds.ObjectSystem.MBObjectManager "'$dll | Select-String -Pattern '"'GetObject|class MBObjectManager|Instance' -Context 2" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1293ms:

  namespace TaleWorlds.ObjectSystem;
  
> public sealed [7mclass MBObjectManager[0m
[7m[0m  {[0m
[7m[0m  	internal interface IObjectTypeRecord : IEnumerable[0m
  		MBObjectBase IObjectTypeRecord.GetMBObject(string objId)
  		{
> 			return [7mGetObject[0m(objId);[0m
[7m[0m  		}[0m
[7m[0m  [0m
  		}
  
> 		internal T [7mGetObject[0m(string objId)[0m
[7m[0m  		{[0m
[7m[0m  			_registeredObjects.TryGetValue(objId, out var value);[0m
  		}
  
> 		internal MBReadOnlyList<T> [7mGetObject[0msList()[0m
[7m[0m  		{[0m
[7m[0m  			return RegisteredObjectsList;[0m
  	private List<IObjectManagerHandler> _handlers;
  
> 	public static MBObjectManager [7mInstance[0m { get; private set; }[0m
[7m[0m  [0m
[7m[0m  	public int NumRegisteredTypes[0m
  	public static MBObjectManager Init()
  	{
> 		_ = [7mInstance[0m;[0m
> 		[7mInstance[0m = new MBObjectManager();
> 		return [7mInstance[0m;[0m
[7m[0m  	}[0m
[7m[0m  [0m
  	{
  		ClearAllObjects();
> 		[7mInstance[0m = null;[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	public void RegisterType<T>(string classPrefix, string classListPrefix, uint typeId, bool autoCreate[7mInstance [0m[7m[0m= [0m
[7m[0mtrue, [0m[7m[0mbool [0m[7m[0misTemporary [0m[7m[0m= [0m[7m[0mfalse) [0m[7m[0mwhere [0m[7m[0mT [0m[7m[0m: [0m[7m[0mMBObjectBase[0m
[7m[0m  	{[0m
[7m[0m  		if (NumRegisteredTypes > MaxRegisteredTypes)[0m
  			Debug.FailedAssert(new MBTooManyRegisteredTypesException().ToString(), 
"C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.ObjectSystem\\MBObjectManager.cs", "RegisterType", 64);
  		}
> 		ObjectTypeRecords.Add(new ObjectTypeRecord<T>(typeId, classPrefix, classListPrefix, autoCreate[7mInstance[0m, [0m
[7m[0misTemporary));[0m
[7m[0m  	}[0m
[7m[0m  [0m
  	}
  
> 	public T [7mGetObject[0m<T>(Func<T, bool> predicate) where T : MBObjectBase[0m
[7m[0m  	{[0m
[7m[0m  		Type typeFromHandle = typeof(T);[0m
  	}
  
> 	public MBReadOnlyList<T> [7mGetObject[0ms<T>(Func<T, bool> predicate) where T : MBObjectBase[0m
[7m[0m  	{[0m
[7m[0m  		MBList<T> mBList = new MBList<T>();[0m
  	}
  
> 	public T [7mGetObject[0m<T>(string objectName) where T : MBObjectBase[0m
[7m[0m  	{[0m
[7m[0m  		Type typeFromHandle = typeof(T);[0m
  				if (objectTypeRecord.ObjectClass == typeFromHandle)
  				{
> 					return ((ObjectTypeRecord<T>)objectTypeRecord).[7mGetObject[0m(objectName);[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
  	}
  
> 	public MBObjectBase [7mGetObject[0m(MBGUID objectId)[0m
[7m[0m  	{[0m
[7m[0m  		foreach (IObjectTypeRecord objectTypeRecord in ObjectTypeRecords)[0m
  			}
  		}
> 		Debug.FailedAssert(objectId.GetTypeIndex() + " could not be found in MBObjectManager objectTypeRecords!", 
"C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.ObjectSystem\\MBObjectManager.cs", "[7mGetObject[0m", [0m[7m[0m424);[0m
[7m[0m  		return null;[0m
[7m[0m  	}[0m
  
> 	public MBObjectBase [7mGetObject[0m(string typeName, string objectName)[0m
[7m[0m  	{[0m
[7m[0m  		foreach (IObjectTypeRecord objectTypeRecord in ObjectTypeRecords)[0m
  			}
  		}
> 		Debug.FailedAssert(typeName + " could not be found in MBObjectManager objectTypeRecords!", 
"C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.ObjectSystem\\MBObjectManager.cs", "[7mGetObject[0m", [0m[7m[0m439);[0m
[7m[0m  		return null;[0m
[7m[0m  	}[0m
  	}
  
> 	public MBReadOnlyList<T> [7mGetObject[0mTypeList<T>() where T : MBObjectBase[0m
[7m[0m  	{[0m
[7m[0m  		Type typeFromHandle = typeof(T);[0m
  				if (objectTypeRecord.ObjectClass == typeFromHandle)
  				{
> 					return ((ObjectTypeRecord<T>)objectTypeRecord).[7mGetObject[0msList();[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
> 			Debug.FailedAssert(typeof(T).Name + " could not be found in MBObjectManager objectTypeRecords!", 
"C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.ObjectSystem\\MBObjectManager.cs", "[7mGetObject[0mTypeList", [0m
[7m[0m504);[0m
[7m[0m  			return null;[0m
[7m[0m  		}[0m
  	}
  
> 	public string [7mGetObject[0mTypeIds()[0m
[7m[0m  	{[0m
[7m[0m  		string text = "";[0m


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.ObjectSystem.dll'; "'$out=ilspycmd -t TaleWorlds.ObjectSystem.MBObjectManager $dll; $out | Select-String -Pattern '"'public T GetObject<T>\\(string objectName\\)' -Context 0,25" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1299ms:

> 	[7mpublic T GetObject<T>(string objectName)[0m where T : MBObjectBase[0m
[7m[0m  	{[0m
[7m[0m  		Type typeFromHandle = typeof(T);[0m
[7m[0m  		if (typeFromHandle.IsSealed)[0m
[7m[0m  		{[0m
[7m[0m  			foreach (IObjectTypeRecord objectTypeRecord in ObjectTypeRecords)[0m
[7m[0m  			{[0m
[7m[0m  				if (objectTypeRecord.ObjectClass == typeFromHandle)[0m
[7m[0m  				{[0m
[7m[0m  					return ((ObjectTypeRecord<T>)objectTypeRecord).GetObject(objectName);[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  		else[0m
[7m[0m  		{[0m
[7m[0m  			foreach (IObjectTypeRecord objectTypeRecord2 in ObjectTypeRecords)[0m
[7m[0m  			{[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m	[0m[7m	[0m[7m[0mif [0m[7m[0m(typeFromHandle.IsAssignableFrom(objectTypeRecord2.ObjectClass) [0m[7m[0m&& [0m[7m[0mobjectTypeRecord2.GetMBObject(objectName) [0m
[7m[0mis [0m[7m[0mT [0m[7m[0mresult)[0m
[7m[0m  				{[0m
[7m[0m  					return result;[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  		return null;[0m
[7m[0m  	}[0m
[7m[0m  [0m


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.Core.dll'; ilspycmd -t TaleWorlds.Core.MBEquipmentRoster "'$dll | Select-String -Pattern '"'class MBEquipmentRoster|AllEquipments|MBReadOnlyList|Equipment' -Context 2" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1031ms:

  namespace TaleWorlds.Core;
  
> public [7mclass MBEquipmentRoster[0m : MBObjectBase[0m
[7m[0m  {[0m
> 	public static readonly [7mEquipment[0m EmptyEquipment = new Equipment(Equipment.EquipmentType.Civilian);[0m
[7m[0m  [0m
> 	private MBList<[7mEquipment[0m> _equipments = new MBList<Equipment>();[0m
[7m[0m  [0m
> 	public BasicCultureObject [7mEquipment[0mCulture;[0m
[7m[0m  [0m
> 	public [7mEquipment[0mFlags EquipmentFlags { get; private set; }[0m
[7m[0m  [0m
> 	public [7mMBReadOnlyList[0m<Equipment> AllEquipments[0m
[7m[0m  	{[0m
[7m[0m  		get[0m
  		{
> 			if (_[7mequipment[0ms.IsEmpty())[0m
[7m[0m  			{[0m
> 				return new MBList<[7mEquipment[0m>(1) { EmptyEquipment };[0m
[7m[0m  			}[0m
> 			return _[7mequipment[0ms;[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
  
> 	public [7mEquipment[0m DefaultEquipment[0m
[7m[0m  	{[0m
[7m[0m  		get[0m
  		{
> 			if (!_[7mequipment[0ms.IsEmpty())[0m
[7m[0m  			{[0m
> 				return _[7mequipment[0ms.FirstOrDefault();[0m
[7m[0m  			}[0m
> 			return Empty[7mEquipment[0m;[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
  
> 	public bool Has[7mEquipment[0mFlags(EquipmentFlags flags)[0m
[7m[0m  	{[0m
> 		return ([7mEquipment[0mFlags & flags) == flags;[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	public bool Is[7mEquipment[0mTemplate()[0m
[7m[0m  	{[0m
> 		return [7mEquipment[0mFlags != EquipmentFlags.None;[0m
[7m[0m  	}[0m
[7m[0m  [0m
  	public void Init(MBObjectManager objectManager, XmlNode node)
  	{
> 		if (node.Name == "[7mEquipment[0mRoster")[0m
[7m[0m  		{[0m
> 			Init[7mEquipment[0m(objectManager, node);[0m
[7m[0m  		}[0m
[7m[0m  		else[0m
  		{
> 			Debug.FailedAssert("false", 
"C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.Core\\MB[7mEquipment[0mRoster.cs", [0m[7m[0m"Init", [0m[7m[0m81);[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
  		if (node.Attributes["culture"] != null)
  		{
> 			[7mEquipment[0mCulture = MBObjectManager.Instance.ReadObjectReferenceFromXml<BasicCultureObject>("culture", node);[0m
[7m[0m  		}[0m
[7m[0m  		foreach (XmlNode childNode in node.ChildNodes)[0m
  		{
> 			if (childNode.Name == "[7mEquipment[0mSet")[0m
[7m[0m  			{[0m
> 				Init[7mEquipment[0m(objectManager, childNode);[0m
[7m[0m  			}[0m
[7m[0m  			if (!(childNode.Name == "Flags"))[0m
  			foreach (XmlAttribute attribute in childNode.Attributes)
  			{
> 				[7mEquipment[0mFlags equipmentFlags = (EquipmentFlags)Enum.Parse(typeof(EquipmentFlags), attribute.Name);[0m
[7m[0m  				if (bool.Parse(attribute.InnerText))[0m
[7m[0m  				{[0m
> 					[7mEquipment[0mFlags |= equipmentFlags;[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
  	}
  
> 	private void Init[7mEquipment[0m(MBObjectManager objectManager, XmlNode node)[0m
[7m[0m  	{[0m
[7m[0m  		base.Initialize();[0m
> 		[7mEquipment[0m.EquipmentType result = Equipment.EquipmentType.Battle;
> 		if (node.Attributes["[7mequipment[0mType"] != null)[0m
[7m[0m  		{[0m
> 			if (!Enum.TryParse<[7mEquipment[0m.EquipmentType>(node.Attributes["equipmentType"].Value, out result))[0m
[7m[0m  			{[0m
> 				Debug.FailedAssert("This [7mequipment [0m[7m[0mdefinition [0m[7m[0mis [0m[7m[0mwrong", [0m
[7m[0m"C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.Core\\MBEquipmentRoster.cs", [0m[7m[0m"InitEquipment", [0m[7m[0m127);[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
  		else if (node.Attributes["civilian"] != null && bool.Parse(node.Attributes["civilian"].Value))
  		{
> 			result = [7mEquipment[0m.EquipmentType.Civilian;[0m
[7m[0m  		}[0m
> 		[7mEquipment[0m equipment = new Equipment(result);
> 		[7mequipment[0m.Deserialize(objectManager, node);
> 		_[7mequipment[0ms.Add(equipment);[0m
[7m[0m  		AfterInitialized();[0m
[7m[0m  	}[0m
  
> 	public void Add[7mEquipment[0mRoster(MBEquipmentRoster equipmentRoster, Equipment.EquipmentType equipmentType)[0m
[7m[0m  	{[0m
> 		foreach ([7mEquipment[0m item in equipmentRoster._equipments.ToList())[0m
[7m[0m  		{[0m
> 			if (([7mequipment[0mType [0m[7m[0m== [0m[7m[0mEquipment.EquipmentType.Stealth [0m[7m[0m&& [0m[7m[0mitem.IsStealth) [0m[7m[0m|| [0m[7m[0m(equipmentType [0m[7m[0m== [0m
[7m[0mEquipment.EquipmentType.Civilian [0m[7m[0m&& [0m[7m[0mitem.IsCivilian) [0m[7m[0m|| [0m[7m[0m(equipmentType [0m[7m[0m== [0m[7m[0mEquipment.EquipmentType.Battle [0m[7m[0m&& [0m
[7m[0mitem.IsBattle))[0m
[7m[0m  			{[0m
> 				_[7mequipment[0ms.Add(item);[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
> 		[7mEquipment[0mFlags = equipmentRoster.EquipmentFlags;[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	public void AddOverriden[7mEquipment[0ms(MBObjectManager objectManager, List<XmlNode> overridenEquipmentSlots)[0m
[7m[0m  	{[0m
> 		List<[7mEquipment[0m> list = _equipments.ToList();
> 		_[7mequipment[0ms.Clear();
> 		foreach ([7mEquipment[0m item in list)[0m
[7m[0m  		{[0m
> 			_[7mequipment[0ms.Add(item.Clone());[0m
[7m[0m  		}[0m
> 		foreach (XmlNode overriden[7mEquipment[0mSlot in overridenEquipmentSlots)[0m
[7m[0m  		{[0m
> 			foreach ([7mEquipment[0m equipment in _equipments)[0m
[7m[0m  			{[0m
> 				[7mequipment[0m.DeserializeNode(objectManager, overridenEquipmentSlot);[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
  	}
  
> 	public void Order[7mEquipment[0ms()[0m
[7m[0m  	{[0m
> 		_[7mequipment[0ms [0m[7m[0m= [0m[7m[0mnew [0m[7m[0mMBList<Equipment>(_equipments.OrderByDescending((Equipment [0m[7m[0meq) [0m[7m[0m=> [0m[7m[0m!eq.IsCivilian [0m[7m[0m&& [0m
[7m[0m!eq.IsStealth));[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	public void InitializeDefault[7mEquipment[0m(string equipmentName)[0m
[7m[0m  	{[0m
> 		if (_[7mequipment[0ms[0] == null)[0m
[7m[0m  		{[0m
> 			_[7mequipment[0ms[0] = new Equipment(Equipment.EquipmentType.Battle);[0m
[7m[0m  		}[0m
> 		_[7mequipment[0ms[0].FillFrom(Game.Current.GetDefaultEquipmentWithName(equipmentName));[0m
[7m[0m  	}[0m
[7m[0m  }[0m


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.Core.dll'; "'$out=ilspycmd -t TaleWorlds.Core.Equipment $dll; $out | Select-String -Pattern '"'public bool IsBattle|public bool IsCivilian|FillFrom|EquipmentType|DeadBattleEquipment' -Context 2" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1132ms:

  public class Equipment
  {
> 	public enum [7mEquipmentType[0m
[7m[0m  	{[0m
[7m[0m  		Invalid = -1,[0m
  
  	[SaveableField(1)]
> 	private [7mEquipmentType[0m _equipmentType;[0m
[7m[0m  [0m
[7m[0m  	public const int EquipmentSlotLength = 12;[0m
  	public const string NullCode = "@null";
  
> 	[7mpublic bool IsCivilian[0m => _equipmentType == EquipmentType.Civilian;[0m
[7m[0m  [0m
> 	[7mpublic bool IsBattle[0m => _equipmentType == EquipmentType.Battle;[0m
[7m[0m  [0m
> 	public bool IsStealth => _[7mequipmentType[0m == EquipmentType.Stealth;[0m
[7m[0m  [0m
[7m[0m  	public EquipmentElement this[int index][0m
  	}
  
> 	internal static object AutoGeneratedGetMemberValue_[7mequipmentType[0m(object o)[0m
[7m[0m  	{[0m
> 		return ((Equipment)o)._[7mequipmentType[0m;[0m
[7m[0m  	}[0m
[7m[0m  [0m
  	{
  		_itemSlots = new EquipmentElement[12];
> 		_[7mequipmentType[0m = EquipmentType.Invalid;[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	public Equipment([7mEquipmentType[0m equipmentType)[0m
[7m[0m  		: this()[0m
[7m[0m  	{[0m
  		_itemSlots = new EquipmentElement[12];
> 		_[7mequipmentType[0m = equipmentType;[0m
[7m[0m  	}[0m
[7m[0m  [0m
  			_itemSlots[i] = new EquipmentElement(equipment[i]);
  		}
> 		_[7mequipmentType[0m = equipment._equipmentType;[0m
[7m[0m  	}[0m
[7m[0m  [0m
  	public Equipment Clone(bool cloneWithoutWeapons = false)
  	{
> 		Equipment equipment = new Equipment(_[7mequipmentType[0m);[0m
[7m[0m  		for (int i = 0; i < 12; i++)[0m
[7m[0m  		{[0m
  	}
  
> 	public void [7mFillFrom[0m(Equipment sourceEquipment, bool useSourceEquipmentType = true)[0m
[7m[0m  	{[0m
> 		if (useSource[7mEquipmentType[0m)[0m
[7m[0m  		{[0m
> 			_[7mequipmentType[0m = sourceEquipment._equipmentType;[0m
[7m[0m  		}[0m
[7m[0m  		for (int i = 0; i < 12; i++)[0m
  	}
  
> 	public static Equipment GetRandomEquipmentElements(BasicCharacterObject character, bool randomEquipmentModifier, 
[7mEquipmentType [0m[7m[0mequipmentType, [0m[7m[0mint [0m[7m[0mseed [0m[7m[0m= [0m[7m[0m-1)[0m
[7m[0m  	{[0m
> 		Equipment equipment = new Equipment([7mequipmentType[0m);[0m
[7m[0m  		List<Equipment> list = new List<Equipment>();[0m
> 		switch ([7mequipmentType[0m)[0m
[7m[0m  		{[0m
> 		case [7mEquipmentType[0m.Battle:[0m
[7m[0m  			list = character.BattleEquipments.ToList();[0m
[7m[0m  			break;[0m
> 		case [7mEquipmentType[0m.Civilian:[0m
[7m[0m  			list = character.CivilianEquipments.ToList();[0m
[7m[0m  			break;[0m


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.Core.dll'; "'$out=ilspycmd -t TaleWorlds.Core.Equipment $dll; $out | Select-String -Pattern '"'public void FillFrom' -Context 0,18" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1122ms:

> 	[7mpublic void FillFrom[0m(Equipment sourceEquipment, bool useSourceEquipmentType = true)[0m
[7m[0m  	{[0m
[7m[0m  		if (useSourceEquipmentType)[0m
[7m[0m  		{[0m
[7m[0m  			_equipmentType = sourceEquipment._equipmentType;[0m
[7m[0m  		}[0m
[7m[0m  		for (int i = 0; i < 12; i++)[0m
[7m[0m  		{[0m
[7m[0m  			this[i] = sourceEquipment[i];[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public void Deserialize(MBObjectManager objectManager, XmlNode node)[0m
[7m[0m  	{[0m
[7m[0m  		foreach (XmlNode childNode in node.ChildNodes)[0m
[7m[0m  		{[0m
[7m[0m  			DeserializeNode(objectManager, childNode);[0m
[7m[0m  		}[0m
[7m[0m  	}[0m


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; "'$out=ilspycmd -t TaleWorlds.CampaignSystem.Hero $dll; $out | Select-String -Pattern '"'FindFirst|BattleEquipment|CivilianEquipment|_battleEquipment|DeadBattleEquipment|public CultureObject Culture|public static Hero MainHero|IsFemale' -Context 2" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1423ms:

  
  	[SaveableField(551)]
> 	[7mpublic CultureObject Culture[0m;[0m
[7m[0m  [0m
[7m[0m  	[XmlIgnore][0m
  
  	[SaveableProperty(200)]
> 	public bool [7mIsFemale[0m { get; set; }[0m
[7m[0m  [0m
[7m[0m  	[SaveableProperty(210)][0m
> 	private Equipment [7m_battleEquipment[0m { get; set; }[0m
[7m[0m  [0m
[7m[0m  	[SaveableProperty(220)][0m
> 	private Equipment _[7mcivilianEquipment[0m { get; set; }[0m
[7m[0m  [0m
[7m[0m  	[SaveableProperty(881)][0m
  	private Equipment _stealthEquipment { get; set; }
  
> 	public Equipment [7mBattleEquipment[0m => _battleEquipment ?? Campaign.Current.DeadBattleEquipment;[0m
[7m[0m  [0m
> 	public Equipment [7mCivilianEquipment[0m => _civilianEquipment ?? Campaign.Current.DeadCivilianEquipment;[0m
[7m[0m  [0m
[7m[0m  	public Equipment StealthEquipment => _stealthEquipment ?? Campaign.Current.DefaultStealthEquipment;[0m
  		get
  		{
> 			return [7mBattleEquipment[0m[EquipmentIndex.ExtraWeaponSlot];[0m
[7m[0m  		}[0m
[7m[0m  		set[0m
  		{
> 			[7mBattleEquipment[0m[EquipmentIndex.ExtraWeaponSlot] = value;[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
  	public static MBReadOnlyList<Hero> DeadOrDisabledHeroes => Campaign.Current.DeadOrDisabledHeroes;
  
> 	[7mpublic static Hero MainHero[0m => CharacterObject.PlayerCharacter.HeroObject;[0m
[7m[0m  [0m
[7m[0m  	public static Hero OneToOneConversationHero => Campaign.Current.ConversationManager.OneToOneConversationHero;[0m
  		StaticBodyProperties.AutoGeneratedStaticCollectObjectsStaticBodyProperties(StaticBodyProperties, collectedObjects);
  		collectedObjects.Add(EncyclopediaText);
> 		collectedObjects.Add([7m_battleEquipment[0m);[0m
> 		collectedObjects.Add(_[7mcivilianEquipment[0m);[0m
[7m[0m  		collectedObjects.Add(_stealthEquipment);[0m
[7m[0m  		CampaignTime.AutoGeneratedStaticCollectObjectsCampaignTime(CaptivityStartTime, collectedObjects);[0m
  	}
  
> 	internal static object AutoGeneratedGetMemberValue[7mIsFemale[0m(object o)[0m
[7m[0m  	{[0m
> 		return ((Hero)o).[7mIsFemale[0m;[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	internal static object AutoGeneratedGetMemberValue[7m_battleEquipment[0m(object o)[0m
[7m[0m  	{[0m
> 		return ((Hero)o).[7m_battleEquipment[0m;[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	internal static object AutoGeneratedGetMemberValue_[7mcivilianEquipment[0m(object o)[0m
[7m[0m  	{[0m
> 		return ((Hero)o)._[7mcivilianEquipment[0m;[0m
[7m[0m  	}[0m
[7m[0m  [0m
  	public Hero()
  	{
> 		[7m_battleEquipment[0m = null;[0m
> 		_[7mcivilianEquipment[0m = null;[0m
[7m[0m  		_stealthEquipment = null;[0m
[7m[0m  		_gold = 0;[0m
  			if (Name.Attributes == null || !Name.Attributes.ContainsKey("FEMALE"))
  			{
> 				Name.SetTextVariable("FEMALE", [7mIsFemale[0m ? 1 : 0);[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
  		if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("v1.3.0") 
&& IsNoncombatant)
  		{
> 			if ([7mBattleEquipment[0m.IsEmpty())[0m
[7m[0m  			{[0m
[7m[0m  				if (Template != null)[0m
  				{
> 					[7m_battleEquipment[0m = Template.FirstBattleEquipment.Clone();[0m
[7m[0m  				}[0m
[7m[0m  				else[0m
  				{
> 					[7m_battleEquipment [0m[7m[0m= [0m[7m[0mMBEquipmentRosterExtensions.All.Find((MBEquipmentRoster [0m[7m[0mx) [0m[7m[0m=> [0m[7m[0mx.StringId [0m[7m[0m== [0m
[7m[0m"generic_bat_dummy").AllEquipments.GetRandomElement();[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
> 			if ([7mCivilianEquipment[0m.IsEmpty())[0m
[7m[0m  			{[0m
[7m[0m  				if (Template != null)[0m
  				{
> 					_[7mcivilianEquipment[0m = Template.FirstCivilianEquipment.Clone();[0m
[7m[0m  				}[0m
[7m[0m  				else[0m
  				{
> 					_[7mcivilianEquipment [0m[7m[0m= [0m[7m[0mMBEquipmentRosterExtensions.All.Find((MBEquipmentRoster [0m[7m[0mx) [0m[7m[0m=> [0m[7m[0mx.StringId [0m[7m[0m== [0m
[7m[0m"generic_civ_dummy").AllEquipments.GetRandomElement();[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
  		_heroDeveloper = null;
  		VolunteerTypes = null;
> 		[7m_battleEquipment[0m = null;[0m
> 		_[7mcivilianEquipment[0m = null;[0m
[7m[0m  		_stealthEquipment = null;[0m
[7m[0m  		SupporterOf = null;[0m
  	public void SetTextVariables()
  	{
> 		MBTextManager.SetTextVariable("SALUTATION_BY_PLAYER", (!CharacterObject.OneToOneConversationCharacter.[7mIsFemale[0m) [0m[7m[0m? [0m
[7m[0mGameTexts.FindText("str_my_lord") [0m[7m[0m: [0m[7m[0mGameTexts.FindText("str_my_lady"));[0m
[7m[0m  		if (!TextObject.IsNullOrEmpty(FirstName))[0m
[7m[0m  		{[0m
  			MBTextManager.SetTextVariable("FIRST_NAME", Name);
  		}
> 		MBTextManager.SetTextVariable("GENDER", [7mIsFemale[0m ? 1 : 0);[0m
[7m[0m  	}[0m
[7m[0m  [0m
  	}
  
> 	public static Hero [7mFindFirst[0m(Func<Hero, bool> predicate)[0m
[7m[0m  	{[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m[0mreturn [0m[7m[0mCampaign.Current.Characters.FirstOrDefault((CharacterObject [0m[7m[0mx) [0m[7m[0m=> [0m[7m[0mx.IsHero [0m[7m[0m&& [0m
[7m[0mpredicate(x.HeroObject))?.HeroObject;[0m
  		}
  		HitPoints = characterObject.MaxHitPoints();
> 		[7mIsFemale[0m = characterObject.IsFemale;[0m
[7m[0m  		Occupation = CharacterObject.GetDefaultOccupation();[0m
> 		List<Equipment> list = characterObject.[7mBattleEquipment[0ms.Where((Equipment x) => !x.IsEmpty()).ToList();
> 		List<Equipment> list2 = characterObject.[7mCivilianEquipment[0ms.Where((Equipment x) => !x.IsEmpty()).ToList();[0m
[7m[0m  		List<Equipment> list3 = characterObject.StealthEquipments.Where((Equipment x) => !x.IsEmpty()).ToList();[0m
[7m[0m  		if (list.IsEmpty())[0m
  		{
  			CultureObject cultureObject = Game.Current.ObjectManager.GetObject<CultureObject>("neutral_culture");
> 			list.AddRange(cultureObject.Default[7mBattleEquipment[0mRoster.AllEquipments);[0m
[7m[0m  		}[0m
[7m[0m  		if (list2.IsEmpty())[0m
  		{
  			CultureObject cultureObject2 = Game.Current.ObjectManager.GetObject<CultureObject>("neutral_culture");
> 			list2.AddRange(cultureObject2.Default[7mCivilianEquipment[0mRoster.AllEquipments);[0m
[7m[0m  		}[0m
[7m[0m  		if (list3.IsEmpty())[0m
  		if (!list.IsEmpty())
  		{
> 			[7m_battleEquipment[0m = list.GetRandomElement().Clone();[0m
[7m[0m  		}[0m
[7m[0m  		if (!list2.IsEmpty())[0m
  		{
> 			_[7mcivilianEquipment[0m = list2.GetRandomElement().Clone();[0m
[7m[0m  		}[0m
[7m[0m  		if (!list3.IsEmpty())[0m
  	public void ResetEquipments()
  	{
> 		[7m_battleEquipment[0m = Template.FirstBattleEquipment.Clone();[0m
> 		_[7mcivilianEquipment[0m = Template.FirstCivilianEquipment.Clone();[0m
[7m[0m  		_stealthEquipment = Template.FirstStealthEquipment.Clone();[0m
[7m[0m  	}[0m
  		for (int i = 0; i < 12; i++)
  		{
> 			if ([7mBattleEquipment[0m?[i].Item == DefaultItems.Trash)[0m
[7m[0m  			{[0m
> 				HandleInvalidItem(is[7mBattleEquipment[0m: true, i);[0m
[7m[0m  			}[0m
> 			else if ([7mBattleEquipment[0m?[i].Item != null)[0m
[7m[0m  			{[0m
> 				if (![7mBattleEquipment[0m[i].Item.IsReady)[0m
[7m[0m  				{[0m
> 					if (MBObjectManager.Instance.GetObject([7mBattleEquipment[0m[i].Item.Id) == BattleEquipment[i].Item)[0m
[7m[0m  					{[0m
> 						MBObjectManager.Instance.UnregisterObject([7mBattleEquipment[0m[i].Item);[0m
[7m[0m  					}[0m
> 					HandleInvalidItem(is[7mBattleEquipment[0m: true, i);[0m
[7m[0m  					PartyBelongedTo?.ItemRoster.AddToCounts(DefaultItems.Trash, 1);[0m
[7m[0m  				}[0m
> 				ItemModifier itemModifier = [7mBattleEquipment[0m[i].ItemModifier;[0m
[7m[0m  				if (itemModifier != null && !itemModifier.IsReady)[0m
[7m[0m  				{[0m
> 					HandleInvalidModifier(is[7mBattleEquipment[0m: true, i);[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
> 			if ([7mCivilianEquipment[0m?[i].Item == DefaultItems.Trash)[0m
[7m[0m  			{[0m
> 				HandleInvalidItem(is[7mBattleEquipment[0m: false, i);[0m
[7m[0m  			}[0m
[7m[0m  			else[0m
  			{
> 				if ([7mCivilianEquipment[0m?[i].Item == null)[0m
[7m[0m  				{[0m
[7m[0m  					continue;[0m
  				}
> 				if (![7mCivilianEquipment[0m[i].Item.IsReady)[0m
[7m[0m  				{[0m
> 					if (MBObjectManager.Instance.GetObject([7mCivilianEquipment[0m[i].Item.Id) == CivilianEquipment[i].Item)[0m
[7m[0m  					{[0m
> 						MBObjectManager.Instance.UnregisterObject([7mCivilianEquipment[0m[i].Item);[0m
[7m[0m  					}[0m
> 					HandleInvalidItem(is[7mBattleEquipment[0m: false, i);[0m
[7m[0m  					PartyBelongedTo?.ItemRoster.AddToCounts(DefaultItems.Trash, 1);[0m
[7m[0m  				}[0m
> 				ItemModifier itemModifier2 = [7mCivilianEquipment[0m[i].ItemModifier;[0m
[7m[0m  				if (itemModifier2 != null && !itemModifier2.IsReady)[0m
[7m[0m  				{[0m
> 					HandleInvalidModifier(is[7mBattleEquipment[0m: false, i);[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
  	}
  
> 	private void HandleInvalidItem(bool is[7mBattleEquipment[0m, int i)[0m
[7m[0m  	{[0m
[7m[0m  		if (IsHumanPlayerCharacter)[0m
  		{
> 			if (is[7mBattleEquipment[0m)[0m
[7m[0m  			{[0m
> 				[7mBattleEquipment[0m[i] = EquipmentElement.Invalid;[0m
[7m[0m  			}[0m
[7m[0m  			else[0m
  			{
> 				[7mCivilianEquipment[0m[i] = EquipmentElement.Invalid;[0m
[7m[0m  			}[0m
[7m[0m  			return;[0m
  		}
> 		List<Equipment> list = (is[7mBattleEquipment [0m[7m[0m? [0m[7m[0mCharacterObject.BattleEquipments.Where((Equipment [0m[7m[0mt) [0m[7m[0m=> [0m
[7m[0m!t.IsEmpty()).ToList() [0m[7m[0m: [0m[7m[0mCharacterObject.CivilianEquipments.Where((Equipment [0m[7m[0mt) [0m[7m[0m=> [0m[7m[0m!t.IsEmpty()).ToList());[0m
[7m[0m  		EquipmentElement value = list[this.RandomInt(list.Count)][i];[0m
[7m[0m  		if (value.IsEmpty || !value.Item.IsReady)[0m
  			value = EquipmentElement.Invalid;
  		}
> 		if (!is[7mBattleEquipment[0m)[0m
[7m[0m  		{[0m
> 			_ = [7mCivilianEquipment[0m[i];[0m
[7m[0m  		}[0m
[7m[0m  		else[0m
  		{
> 			_ = [7mBattleEquipment[0m[i];[0m
[7m[0m  		}[0m
> 		if (is[7mBattleEquipment[0m)[0m
[7m[0m  		{[0m
> 			[7mBattleEquipment[0m[i] = value;[0m
[7m[0m  		}[0m
[7m[0m  		else[0m
  		{
> 			[7mCivilianEquipment[0m[i] = value;[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
  
> 	private void HandleInvalidModifier(bool is[7mBattleEquipment[0m, int i)[0m
[7m[0m  	{[0m
> 		if (is[7mBattleEquipment[0m)[0m
[7m[0m  		{[0m
> 			[7mBattleEquipment[0m[i] = new EquipmentElement(BattleEquipment[i].Item);[0m
[7m[0m  		}[0m
[7m[0m  		else[0m
  		{
> 			[7mCivilianEquipment[0m[i] = new EquipmentElement(CivilianEquipment[i].Item);[0m
[7m[0m  		}[0m
[7m[0m  	}[0m


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; "'$out=ilspycmd -t TaleWorlds.CampaignSystem.Campaign $dll; $out | Select-String -Pattern '"'DeadBattleEquipment|DeadCivilianEquipment|new Equipment|public static Campaign Current' -Context 2" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1455ms:

  	public bool TrueSight { get; set; }
  
> 	[7mpublic static Campaign Current[0m { get; private set; }[0m
[7m[0m  [0m
[7m[0m  	[SaveableProperty(37)][0m
  	public bool IsMainHeroDisguised { get; set; }
  
> 	public Equipment [7mDeadBattleEquipment[0m { get; set; }[0m
[7m[0m  [0m
> 	public Equipment [7mDeadCivilianEquipment[0m { get; set; }[0m
[7m[0m  [0m
[7m[0m  	public Equipment DefaultStealthEquipment { get; private set; }[0m
  	private void InitializeDefaultEquipments()
  	{
> 		[7mDeadBattleEquipment [0m[7m[0m= [0m
[7m[0mGame.Current.ObjectManager.GetObject<MBEquipmentRoster>("default_battle_equipment_roster_neutral").DefaultEquipment;[0m
> 		[7mDeadCivilianEquipment [0m[7m[0m= [0m
[7m[0mGame.Current.ObjectManager.GetObject<MBEquipmentRoster>("default_civilian_equipment_roster_neutral").DefaultEquipment;[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m[0mDefaultStealthEquipment [0m[7m[0m= [0m
[7m[0mGame.Current.ObjectManager.GetObject<MBEquipmentRoster>("default_stealth_equipment_roster").DefaultEquipment;[0m
[7m[0m  	}[0m


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content Main'"\\Adapters\\PlayerEquipmentAdapter.cs | ForEach-Object { '{0,4}: {1}' -f "'$i, $_; $i++ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 432ms:
   1: using System.Linq;
   2: using TaleWorlds.CampaignSystem;
   3: using TaleWorlds.Core;
   4: using TaleWorlds.ObjectSystem;
   5: 
   6: namespace TAOM.Adapters;
   7: 
   8: public sealed class PlayerEquipmentAdapter : IPlayerEquipmentAdapter
   9: {
  10:     public PlayerEquipmentApplyResult ApplyRosterToPlayer(string rosterId, string playerHeroId)
  11:     {
  12:         var roster = MBObjectManager.Instance?.GetObject<MBEquipmentRoster>(rosterId);
  13:         if (roster == null)
  14:             return PlayerEquipmentApplyResult.RosterNotFound;
  15: 
  16:         var battle = roster.AllEquipments.FirstOrDefault(e => e.IsBattle);
  17:         var civilian = roster.AllEquipments.FirstOrDefault(e => e.IsCivilian);
  18:         if (battle == null && civilian == null)
  19:             return PlayerEquipmentApplyResult.NoSuitableEquipment;
  20: 
  21:         var hero = Hero.FindFirst(h => h.StringId == playerHeroId);
  22:         if (hero == null)
  23:             return PlayerEquipmentApplyResult.HeroNotFound;
  24: 
  25:         // Hero.BattleEquipment / CivilianEquipment fall through to Campaign.Current.DeadBattleEquipment
  26:         // (a process-wide shared singleton) when the hero's _battleEquipment is null. Calling FillFrom
  27:         // on that fallback would corrupt equipment for every dead/uninitialized hero. MainHero at CC
  28:         // finalize is always initialized, but the adapter takes any heroId — guard defensively.
  29:         var dead = Campaign.Current?.DeadBattleEquipment;
  30:         if (battle != null && hero.BattleEquipment != null && hero.BattleEquipment != dead)
  31:             hero.BattleEquipment.FillFrom(battle);
  32:         if (civilian != null && hero.CivilianEquipment != null && hero.CivilianEquipment != dead)
  33:             hero.CivilianEquipment.FillFrom(civilian);
  34: 
  35:         return PlayerEquipmentApplyResult.Success;
  36:     }
  37: }

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git diff -- Main/_Module/ModuleData/startup_resources/startup_resources_config.xml Main/_Module/ModuleData/charactercreation/youth_menu.json' in C:\Users\mikew\source\repos\TAOM
 succeeded in 319ms:
diff --git a/Main/_Module/ModuleData/charactercreation/youth_menu.json b/Main/_Module/ModuleData/charactercreation/youth_menu.json
index f89ae90..cd57f35 100644
--- a/Main/_Module/ModuleData/charactercreation/youth_menu.json
+++ b/Main/_Module/ModuleData/charactercreation/youth_menu.json
@@ -818,7 +818,7 @@
     {
         "string_id": "taom_youth_sturgia_1",
         "culture_id": "sturgia",
-        "title_type": "retainer",
+        "title_type": "guard",
         "text": "Served in the Royal Guard of Dale.",
         "description": "You pledged your loyalty to the King of Dale, serving in the Royal Guard and learning the martial traditions of the Men of the North.",
         "skills": ["Riding", "Polearm"],
diff --git a/Main/_Module/ModuleData/startup_resources/startup_resources_config.xml b/Main/_Module/ModuleData/startup_resources/startup_resources_config.xml
index a10f9c2..80404d5 100644
--- a/Main/_Module/ModuleData/startup_resources/startup_resources_config.xml
+++ b/Main/_Module/ModuleData/startup_resources/startup_resources_config.xml
@@ -1,27 +1,40 @@
 <?xml version="1.0" encoding="utf-8"?>
+<!--
+  StartupResources config — values applied at new-game start.
+
+  Attributes:
+    gold        = denars granted to NPC lords of this culture (NOT player). Default 0.
+    influence   = influence granted to NPC lords of this culture. Default 0.
+    playerGold  = denars granted to the PLAYER hero at CC finalize, by selected culture.
+                  Range [0, 10_000_000]. Out-of-range or non-numeric values revert to 0
+                  with a logged warning. Default 0 (no warning when missing).
+
+  Edits to this file take effect on the next Bannerlord process restart, not save reload.
+-->
 <StartupResources>
   <!-- Elven cultures -->
-  <Culture id="rivendell"   gold="600000" influence="1000" />
-  <Culture id="lothlorien"  gold="600000" influence="1000" />
-  <Culture id="mirkwood"    gold="600000" influence="1000"   />
+  <Culture id="rivendell"   gold="600000" influence="1000" playerGold="10000" />
+  <Culture id="lothlorien"  gold="600000" influence="1000" playerGold="10000" />
+  <Culture id="mirkwood"    gold="600000" influence="1000" playerGold="8000"  />
 
   <!-- Dwarven cultures -->
-  <Culture id="erebor"      gold="50000" influence="150"   />
+  <Culture id="erebor"      gold="50000"  influence="150"  playerGold="7500"  />
 
   <!-- Human Good cultures -->
-  <Culture id="gondor"      gold="50000"  influence="500"  />
-  <Culture id="vlandia"     gold="50000"  influence="50"   />
-  <Culture id="sturgia"     gold="50000"  influence="50"   />
+  <Culture id="gondor"      gold="50000"  influence="500"  playerGold="5000"  />
+  <Culture id="vlandia"     gold="50000"  influence="50"   playerGold="5000"  />
+  <Culture id="sturgia"     gold="50000"  influence="50"   playerGold="5000"  />
 
   <!-- Human Evil / Eastern cultures -->
-  <Culture id="battania"    gold="50000"  influence="100"  />
-  <Culture id="aserai"      gold="50000"  influence="100"  />
-  <Culture id="khuzait"     gold="50000"  influence="1000" />
+  <Culture id="empire"      gold="50000"  influence="100"  playerGold="4000"  />
+  <Culture id="battania"    gold="50000"  influence="100"  playerGold="4000"  />
+  <Culture id="aserai"      gold="50000"  influence="100"  playerGold="4000"  />
+  <Culture id="khuzait"     gold="50000"  influence="1000" playerGold="4000"  />
 
   <!-- Orcish / Dark cultures -->
-  <Culture id="mordor"      gold="50000"  influence="500"  />
-  <Culture id="isengard"    gold="200000" influence="2000" />
-  <Culture id="gundabad"    gold="200000" influence="2000" />
-  <Culture id="dolguldur"   gold="200000" influence="2000" />
-  <Culture id="umbar"       gold="200000" influence="500"  />
+  <Culture id="mordor"      gold="50000"  influence="500"  playerGold="6000"  />
+  <Culture id="isengard"    gold="200000" influence="2000" playerGold="6000"  />
+  <Culture id="gundabad"    gold="200000" influence="2000" playerGold="6000"  />
+  <Culture id="dolguldur"   gold="200000" influence="2000" playerGold="6000"  />
+  <Culture id="umbar"       gold="200000" influence="500"  playerGold="6000"  />
 </StartupResources>
warning: in the working copy of 'Main/_Module/ModuleData/charactercreation/youth_menu.json', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Main/_Module/ModuleData/startup_resources/startup_resources_config.xml', LF will be replaced by CRLF the next time Git touches it

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Main\\_Module\\ModuleData\\taom_spcultures.xml,Main\\_Module\\ModuleData\\TAOM_spkingdoms.xml -Pattern 'id=\"(rivendell|lothlorien|mirkwood|erebor|gondor|vlandia|sturgia|empire|battania|aserai|khuzait|mordor|isengard|gundabad|dolguldur|umbar)\"' | Select-Object Path,LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
 succeeded in 442ms:

[32;1mPath                                                                        [0m[32;1m LineNumber[0m[32;1m Line[0m
[32;1m----                                                                        [0m [32;1m----------[0m [32;1m----[0m
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml          7     id="erebor"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml        370     id="rivendell"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml        739     id="mirkwood"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml       1099     id="lothlorien"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml       1460     id="isengard"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml       1821     id="gundabad"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml       2181     id="umbar"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml       2540     id="dolguldur"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml       2900     id="gondor"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml       3269     id="mordor"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TAOM_spkingdoms.xml          5         id="erebor"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TAOM_spkingdoms.xml         92         id="rivendell"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TAOM_spkingdoms.xml        178         id="mirkwood"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TAOM_spkingdoms.xml        265         id="lothlorien"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TAOM_spkingdoms.xml        352         id="isengard"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TAOM_spkingdoms.xml        443         id="gundabad"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TAOM_spkingdoms.xml        534         id="umbar"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TAOM_spkingdoms.xml        625         id="dolguldur"


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Get-ChildItem -Recurse -File Main | Where-Object { $_.Extension -in '"'.cs','.xml','.json','.md' } | Select-String -Pattern 'player_char_creation_' | Select-Object Path,LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
 succeeded in 596ms:

[32;1mPath                                                                                                   [0m[32;1m LineNumber[0m[32;1m Line[0m
[32;1m----                                                                                                   [0m [32;1m----------[0m [32;1m----[0m
C:\Users\mikew\source\repos\TAOM\Main\Features\CharacterCreation\PlayerEquipmentRosterIds.cs                     7    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        210 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        222 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        234 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        246 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        258 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        270 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        282 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        294 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        306 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        318 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        330 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        342 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        354 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        366 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        378 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        390 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        402 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        414 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        426 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        438 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        450 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        462 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        474 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        486 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        499 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        541 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        583 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        625 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        667 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        712 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        757 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        799 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        841 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        880 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        919 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        961 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1003 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1045 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1087 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1126 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1166 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1382 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1394 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1406 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1418 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1430 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1442 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1454 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1466 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1478 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1490 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1502 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1514 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1526 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1538 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1550 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1562 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1574 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1586 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1598 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1610 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1622 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1634 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1646 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1658 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1671 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1710 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1749 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1788 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1827 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1869 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1911 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1950 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1989 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2025 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2061 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2100 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2139 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2178 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2217 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2253 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2290 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2518 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2530 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2542 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2554 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2566 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2578 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2590 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2602 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2614 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2626 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2638 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2650 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2662 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2674 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2686 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2698 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2710 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2722 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2734 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2746 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2758 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2770 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2782 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2794 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2807 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2849 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2891 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2933 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2975 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3020 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3065 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3107 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3149 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3188 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3227 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3269 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3311 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3353 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3395 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3434 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3474 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3702 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3714 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3726 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3738 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3750 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3762 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3774 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3786 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3798 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3810 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3822 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3834 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3846 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3858 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3870 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3882 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3894 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3906 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3918 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3930 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3942 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3954 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3966 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3978 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3991 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4033 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4075 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4117 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4159 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4204 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4249 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4291 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4333 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4372 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4411 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4453 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4495 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4537 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4579 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4618 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4658 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4886 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4898 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4910 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4922 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4934 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4946 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4958 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4970 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4982 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4994 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5006 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5018 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5030 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5042 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5054 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5066 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5078 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5090 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5102 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5114 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5126 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5138 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5150 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5162 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5175 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5217 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5259 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5301 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5343 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5388 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5433 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5475 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5517 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5556 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5595 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5637 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5679 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5721 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5763 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5802 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5842 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6070 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6082 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6094 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6106 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6118 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6130 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6142 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6154 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6166 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6178 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6190 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6202 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6214 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6226 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6238 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6250 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6262 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6274 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6286 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6298 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6310 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6322 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6334 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6346 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6359 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6401 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6443 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6485 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6527 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6572 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6617 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6659 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6701 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6740 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6779 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6821 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6863 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6905 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6947 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6986 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7026 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7254 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7266 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7278 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7290 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7302 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7314 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7326 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7338 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7350 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7362 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7374 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7386 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7398 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7410 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7422 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7434 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7446 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7458 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7470 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7482 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7494 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7506 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7518 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7530 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7543 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7585 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7627 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7669 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7711 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7753 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7795 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7834 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7873 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7912 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7951 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       7993 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8035 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8074 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8113 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8152 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8192 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8420 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8432 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8444 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8456 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8468 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8480 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8492 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8504 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8516 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8528 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8540 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8552 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8564 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8576 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8588 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8600 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8612 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8624 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8636 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8648 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8660 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8672 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8684 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8696 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8709 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8751 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8793 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8835 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8877 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8919 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       8961 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9000 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9039 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9078 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9117 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9156 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9195 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9231 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9267 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9306 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9346 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9574 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9586 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9598 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9610 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9622 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9634 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9646 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9658 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9670 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9682 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9694 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9706 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9718 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9730 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9742 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9754 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9766 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9778 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9790 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9802 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9814 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9826 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9838 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9850 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9863 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9905 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9947 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       9989 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10031 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10076 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10121 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10163 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10205 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10244 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10283 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10325 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10367 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10409 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10451 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10490 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10530 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10758 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10770 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10782 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10794 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10806 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10818 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10830 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10842 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10854 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10866 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10878 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10890 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10902 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10914 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10926 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10938 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10950 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10962 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10974 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10986 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      10998 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      11010 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      11022 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      11034 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      11047 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      11089 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      11131 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      11173 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      11215 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      11260 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      11305 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      11347 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      11389 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      11428 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      11467 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      11509 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      11551 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      11590 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      11629 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      11668 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      11708 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      11936 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      11948 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      11960 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      11972 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      11984 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      11996 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12008 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12020 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12032 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12044 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12056 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12068 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12080 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12092 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12104 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12116 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12128 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12140 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12152 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12164 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12176 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12188 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12200 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12212 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12225 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12267 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12309 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12351 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12393 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12438 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12483 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12525 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12567 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12606 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12645 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12687 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12729 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12771 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12813 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12852 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      12892 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13120 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13132 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13144 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13156 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13168 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13180 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13192 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13204 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13216 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13228 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13240 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13252 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13264 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13276 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13288 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13300 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13312 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13324 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13336 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13348 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13360 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13372 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13384 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13396 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13409 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13451 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13493 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13535 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13577 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13622 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13667 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13709 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13751 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13790 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13829 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13871 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13913 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13955 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      13997 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      14036 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml      14076 		i…


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Recurse -File Main -Include *.cs | Select-String -Pattern 'player_char_creation_' | Select-Object Path,LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
 succeeded in 665ms:

[32;1mPath                                                                                        [0m[32;1m LineNumber[0m[32;1m Line[0m
[32;1m----                                                                                        [0m [32;1m----------[0m [32;1m----[0m
C:\Users\mikew\source\repos\TAOM\Main\Features\CharacterCreation\PlayerEquipmentRosterIds.cs          7         return…


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content Main'"\\Features\\CharacterCreation\\NarrativeMenuBuilder.cs | ForEach-Object { '{0,4}: {1}' -f "'$i, $_; $i++ }; $i=1; Get-Content Main'"\\Features\\CharacterCreation\\CareerMenuService.cs | ForEach-Object { if ("'$i -ge 190 -and $i -le 245) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 463ms:
   1: using System;
   2: using System.Collections.Generic;
   3: using System.Linq;
   4: using TaleWorlds.CampaignSystem;
   5: using TaleWorlds.CampaignSystem.CharacterCreationContent;
   6: using TaleWorlds.Core;
   7: using TaleWorlds.Localization;
   8: using TAOM.Core.Logging;
   9: using TAOM.Features.CharacterCreation.Models;
  10: 
  11: namespace TAOM.Features.CharacterCreation;
  12: 
  13: public class NarrativeMenuBuilder
  14: {
  15:     private readonly IModLogger _logger;
  16:     private readonly IEquipmentRosterProvider _equipmentRosterProvider;
  17: 
  18:     private static readonly Dictionary<string, Func<SkillObject>> SkillMap =
  19:         new Dictionary<string, Func<SkillObject>>(StringComparer.OrdinalIgnoreCase)
  20:         {
  21:             ["OneHanded"] = () => DefaultSkills.OneHanded,
  22:             ["TwoHanded"] = () => DefaultSkills.TwoHanded,
  23:             ["Polearm"] = () => DefaultSkills.Polearm,
  24:             ["Bow"] = () => DefaultSkills.Bow,
  25:             ["Crossbow"] = () => DefaultSkills.Crossbow,
  26:             ["Throwing"] = () => DefaultSkills.Throwing,
  27:             ["Riding"] = () => DefaultSkills.Riding,
  28:             ["Athletics"] = () => DefaultSkills.Athletics,
  29:             ["Crafting"] = () => DefaultSkills.Crafting,
  30:             ["Scouting"] = () => DefaultSkills.Scouting,
  31:             ["Tactics"] = () => DefaultSkills.Tactics,
  32:             ["Roguery"] = () => DefaultSkills.Roguery,
  33:             ["Charm"] = () => DefaultSkills.Charm,
  34:             ["Leadership"] = () => DefaultSkills.Leadership,
  35:             ["Trade"] = () => DefaultSkills.Trade,
  36:             ["Steward"] = () => DefaultSkills.Steward,
  37:             ["Medicine"] = () => DefaultSkills.Medicine,
  38:             ["Engineering"] = () => DefaultSkills.Engineering,
  39:         };
  40: 
  41:     private static readonly Dictionary<string, Func<CharacterAttribute>> AttributeMap =
  42:         new Dictionary<string, Func<CharacterAttribute>>(StringComparer.OrdinalIgnoreCase)
  43:         {
  44:             ["Vigor"] = () => DefaultCharacterAttributes.Vigor,
  45:             ["Control"] = () => DefaultCharacterAttributes.Control,
  46:             ["Endurance"] = () => DefaultCharacterAttributes.Endurance,
  47:             ["Cunning"] = () => DefaultCharacterAttributes.Cunning,
  48:             ["Social"] = () => DefaultCharacterAttributes.Social,
  49:             ["Intelligence"] = () => DefaultCharacterAttributes.Intelligence,
  50:         };
  51: 
  52:     public NarrativeMenuBuilder(IModLogger logger, IEquipmentRosterProvider equipmentRosterProvider)
  53:     {
  54:         _logger = logger;
  55:         _equipmentRosterProvider = equipmentRosterProvider;
  56:     }
  57: 
  58:     internal static string BuildEquipmentRosterId(string cultureId, string titleType, bool isFemale)
  59:         => PlayerEquipmentRosterIds.Build(cultureId, titleType, isFemale);
  60: 
  61:     public NarrativeMenuOption BuildOption(NarrativeOptionDefinition definition)
  62:     {
  63:         var skills = ResolveSkills(definition.Skills);
  64:         var attribute = ResolveAttribute(definition.Attribute);
  65:         var cultureId = definition.CultureId;
  66:         var occupationType = definition.OccupationType;
  67:         var titleType = definition.TitleType;
  68:         var focusToAdd = definition.FocusToAdd;
  69:         var skillLevelToAdd = definition.SkillLevelToAdd;
  70:         var attributeLevelToAdd = definition.AttributeLevelToAdd;
  71: 
  72:         return new NarrativeMenuOption(
  73:             definition.StringId,
  74:             new TextObject($"{{=taom_cc_{definition.StringId}_text}}{definition.Text}"),
  75:             new TextObject($"{{=taom_cc_{definition.StringId}_desc}}{definition.Description}"),
  76:             getNarrativeMenuOptionArgs: (NarrativeMenuOptionArgs args) =>
  77:             {
  78:                 if (skills.Length > 0)
  79:                 {
  80:                     args.SetAffectedSkills(skills);
  81:                     args.SetFocusToSkills(focusToAdd);
  82:                     args.SetLevelToSkills(skillLevelToAdd);
  83:                 }
  84: 
  85:                 if (attribute != null)
  86:                 {
  87:                     args.SetLevelToAttribute(attribute, attributeLevelToAdd);
  88:                 }
  89:             },
  90:             onCondition: string.IsNullOrEmpty(cultureId)
  91:                 ? (NarrativeMenuOptionOnConditionDelegate)null
  92:                 : (CharacterCreationManager manager) =>
  93:                 {
  94:                     var selectedCulture = manager.CharacterCreationContent?.SelectedCulture;
  95:                     return selectedCulture != null &&
  96:                            string.Equals(selectedCulture.StringId, cultureId, StringComparison.OrdinalIgnoreCase);
  97:                 },
  98:             onSelect: (string.IsNullOrEmpty(occupationType) && string.IsNullOrEmpty(titleType))
  99:                 ? (NarrativeMenuOptionOnSelectDelegate)null
 100:                 : (CharacterCreationManager manager) =>
 101:                 {
 102:                     if (!string.IsNullOrEmpty(occupationType))
 103:                         manager.CharacterCreationContent.SetParentOccupation(occupationType);
 104:                     if (!string.IsNullOrEmpty(titleType))
 105:                     {
 106:                         manager.CharacterCreationContent.SelectedTitleType = titleType;
 107:                         UpdateYouthEquipment(manager, titleType);
 108:                     }
 109:                 },
 110:             onConsequence: null
 111:         );
 112:     }
 113: 
 114:     public int AddOptionsToMenu(NarrativeMenu menu, IReadOnlyList<NarrativeOptionDefinition> definitions)
 115:     {
 116:         int added = 0;
 117:         foreach (var definition in definitions)
 118:         {
 119:             try
 120:             {
 121:                 var option = BuildOption(definition);
 122:                 menu.AddNarrativeMenuOption(option);
 123:                 added++;
 124:             }
 125:             catch (Exception ex)
 126:             {
 127:                 _logger.LogError($"Failed to build narrative option '{definition.StringId}': {ex.Message}");
 128:             }
 129:         }
 130:         return added;
 131:     }
 132: 
 133:     private SkillObject[] ResolveSkills(string[] skillNames)
 134:     {
 135:         if (skillNames == null || skillNames.Length == 0)
 136:             return Array.Empty<SkillObject>();
 137: 
 138:         var resolved = new List<SkillObject>();
 139:         foreach (var name in skillNames)
 140:         {
 141:             if (SkillMap.TryGetValue(name, out var factory))
 142:             {
 143:                 var skill = factory();
 144:                 if (skill != null)
 145:                     resolved.Add(skill);
 146:                 else
 147:                     _logger.LogWarning($"Skill '{name}' resolved to null — game may not be initialized");
 148:             }
 149:             else
 150:             {
 151:                 _logger.LogWarning($"Unknown skill name: '{name}'");
 152:             }
 153:         }
 154:         return resolved.ToArray();
 155:     }
 156: 
 157:     private CharacterAttribute ResolveAttribute(string attributeName)
 158:     {
 159:         if (string.IsNullOrEmpty(attributeName))
 160:             return null;
 161: 
 162:         if (AttributeMap.TryGetValue(attributeName, out var factory))
 163:         {
 164:             var attr = factory();
 165:             if (attr == null)
 166:                 _logger.LogWarning($"Attribute '{attributeName}' resolved to null — game may not be initialized");
 167:             return attr;
 168:         }
 169: 
 170:         _logger.LogWarning($"Unknown attribute name: '{attributeName}'");
 171:         return null;
 172:     }
 173: 
 174:     private void UpdateYouthEquipment(CharacterCreationManager manager, string titleType)
 175:     {
 176:         var cultureId = manager.CharacterCreationContent.SelectedCulture?.StringId;
 177:         if (string.IsNullOrEmpty(cultureId))
 178:             return;
 179: 
 180:         var isFemale = Hero.MainHero?.IsFemale ?? false;
 181:         var rosterId = BuildEquipmentRosterId(cultureId, titleType, isFemale);
 182:         var roster = _equipmentRosterProvider.GetRoster(rosterId);
 183:         if (roster == null)
 184:         {
 185:             _logger.LogWarning($"Equipment roster '{rosterId}' not found");
 186:             return;
 187:         }
 188: 
 189:         foreach (var character in manager.CurrentMenu.Characters)
 190:         {
 191:             if (character.StringId == "player_youth_character")
 192:             {
 193:                 character.SetEquipment(roster);
 194:                 break;
 195:             }
 196:         }
 197:     }
 198: }
 190:                 {
 191:                     args.SetAffectedSkills(skills);
 192:                     args.SetFocusToSkills(focusToAdd);
 193:                     args.SetLevelToSkills(skillLevelToAdd);
 194:                 }
 195:                 if (attribute != null)
 196:                 {
 197:                     args.SetLevelToAttribute(attribute, attributeLevelToAdd);
 198:                 }
 199:             },
 200:             (CharacterCreationManager manager) =>
 201:             {
 202:                 var selectedCulture = manager.CharacterCreationContent?.SelectedCulture;
 203:                 if (selectedCulture == null)
 204:                     return false;
 205: 
 206:                 foreach (var cultureId in eligibleCultures)
 207:                 {
 208:                     if (string.Equals(selectedCulture.StringId, cultureId, StringComparison.OrdinalIgnoreCase))
 209:                         return true;
 210:                 }
 211:                 return false;
 212:             },
 213:             (CharacterCreationManager manager) =>
 214:             {
 215:                 OnCareerOptionSelected(careerId);
 216:                 _logger.LogInfo($"Player selected career: {careerId}");
 217:             },
 218:             null);
 219:     }
 220: 
 221:     private static List<NarrativeMenuCharacterArgs> GetCareerMenuCharacterArgs(
 222:         CultureObject culture, string occupationType, CharacterCreationManager manager)
 223:     {
 224:         var cultureId = culture?.StringId ?? "gondor";
 225:         var isFemale = Hero.MainHero?.IsFemale ?? false;
 226:         var titleType = manager.CharacterCreationContent?.SelectedTitleType ?? "guard";
 227:         var equipmentId = PlayerEquipmentRosterIds.Build(cultureId, titleType, isFemale);
 228: 
 229:         return new List<NarrativeMenuCharacterArgs>
 230:         {
 231:             new NarrativeMenuCharacterArgs(
 232:                 "player_career_character",
 233:                 25,
 234:                 equipmentId,
 235:                 "act_character_creation_male_default_standing",
 236:                 "spawnpoint_player_1",
 237:                 "", "", null, true, isFemale)
 238:         };
 239:     }
 240: 
 241:     private SkillObject[] ResolveSkills(string[] skillNames)
 242:     {
 243:         if (skillNames == null || skillNames.Length == 0)
 244:             return Array.Empty<SkillObject>();
 245: 

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content Main\\Features\\CharacterCreation\\CareerMenuService.cs" in C:\Users\mikew\source\repos\TAOM
 succeeded in 438ms:
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CharacterCreation.Models;

namespace TAOM.Features.CharacterCreation;

public class CareerMenuService : ICareerMenuService
{
    private const string CareerMenuId = "narrative_career_menu";
    private const string AgeSelectionMenuId = "narrative_age_selection_menu";

    private readonly ICareerRegistry _registry;
    private readonly ICareerMenuDataProvider _dataProvider;
    private readonly IModLogger _logger;

    public string SelectedCareerStringId { get; private set; }

    private static readonly Dictionary<string, Func<SkillObject>> SkillMap =
        new Dictionary<string, Func<SkillObject>>(StringComparer.OrdinalIgnoreCase)
        {
            ["OneHanded"] = () => DefaultSkills.OneHanded,
            ["TwoHanded"] = () => DefaultSkills.TwoHanded,
            ["Polearm"] = () => DefaultSkills.Polearm,
            ["Bow"] = () => DefaultSkills.Bow,
            ["Crossbow"] = () => DefaultSkills.Crossbow,
            ["Throwing"] = () => DefaultSkills.Throwing,
            ["Riding"] = () => DefaultSkills.Riding,
            ["Athletics"] = () => DefaultSkills.Athletics,
            ["Crafting"] = () => DefaultSkills.Crafting,
            ["Scouting"] = () => DefaultSkills.Scouting,
            ["Tactics"] = () => DefaultSkills.Tactics,
            ["Roguery"] = () => DefaultSkills.Roguery,
            ["Charm"] = () => DefaultSkills.Charm,
            ["Leadership"] = () => DefaultSkills.Leadership,
            ["Trade"] = () => DefaultSkills.Trade,
            ["Steward"] = () => DefaultSkills.Steward,
            ["Medicine"] = () => DefaultSkills.Medicine,
            ["Engineering"] = () => DefaultSkills.Engineering,
        };

    private static readonly Dictionary<string, Func<CharacterAttribute>> AttributeMap =
        new Dictionary<string, Func<CharacterAttribute>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Vigor"] = () => DefaultCharacterAttributes.Vigor,
            ["Control"] = () => DefaultCharacterAttributes.Control,
            ["Endurance"] = () => DefaultCharacterAttributes.Endurance,
            ["Cunning"] = () => DefaultCharacterAttributes.Cunning,
            ["Social"] = () => DefaultCharacterAttributes.Social,
            ["Intelligence"] = () => DefaultCharacterAttributes.Intelligence,
        };

    public CareerMenuService(ICareerRegistry registry, ICareerMenuDataProvider dataProvider, IModLogger logger)
    {
        _registry = registry;
        _dataProvider = dataProvider;
        _logger = logger;
    }

    public void RegisterCareerMenu(CharacterCreationManager manager)
    {
        SelectedCareerStringId = null;

        var options = BuildCareerMenuOptions();
        if (options.Count == 0)
        {
            _logger.LogWarning("No career menu options built — skipping career menu registration");
            return;
        }

        var characters = new List<NarrativeMenuCharacter>();
        var playerBody = CharacterObject.PlayerCharacter?.GetBodyProperties(null) ?? BodyProperties.Default;
        var playerRace = CharacterObject.PlayerCharacter?.Race ?? 0;
        var isFemale = Hero.MainHero?.IsFemale ?? false;
        characters.Add(new NarrativeMenuCharacter("player_career_character", playerBody, playerRace, isFemale));

        // Chain after age selection: adulthood → age_selection (vanilla) → career → finalize
        var careerMenu = new NarrativeMenu(
            CareerMenuId,
            AgeSelectionMenuId,
            "",
            new TextObject("{=taom_cc_career_title}Career"),
            new TextObject("{=taom_cc_career_desc}Your experiences have set you on a path. Choose the career that will define your legend."),
            characters,
            GetCareerMenuCharacterArgs);

        foreach (var option in options)
        {
            careerMenu.AddNarrativeMenuOption(option);
        }

        manager.AddNewMenu(careerMenu);
        _logger.LogInfo($"Registered career menu with {options.Count} options");
    }

    public List<NarrativeMenuOption> BuildCareerMenuOptions()
    {
        var careers = _registry.GetAllCareers();
        var options = new List<NarrativeMenuOption>();

        foreach (var career in careers)
        {
            var ccData = _dataProvider.GetOptionForCareer(career.Id);
            if (ccData == null)
            {
                _logger.LogWarning($"No CC data for career '{career.Id}' — skipping");
                continue;
            }

            var option = BuildOptionForCareer(career, ccData);
            options.Add(option);
        }

        // Fallback option for cultures with no eligible careers (e.g., shaghana, abanissa).
        // Without this, an empty menu causes KeyNotFoundException in vanilla
        // TrySwitchToNextMenu when SelectedOptions has no entry for the career menu.
        options.Add(BuildFallbackOption());

        return options;
    }

    public void OnCareerOptionSelected(string careerStringId)
    {
        SelectedCareerStringId = careerStringId;
    }

    public IReadOnlyList<string> GetEligibleCultureIds(CareerDefinition career)
    {
        return career.EligibleCultureIds;
    }

    private NarrativeMenuOption BuildFallbackOption()
    {
        // Collect all culture IDs that have at least one career option.
        // The fallback is visible only for cultures NOT in this set.
        var coveredCultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var career in _registry.GetAllCareers())
        {
            foreach (var cultureId in career.EligibleCultureIds)
                coveredCultures.Add(cultureId);
        }

        return new NarrativeMenuOption(
            "taom_career_none",
            new TextObject("{=taom_cc_career_none}No specialization"),
            new TextObject("{=taom_cc_career_none_desc}You have not yet committed to a particular path. Your career will be determined by your actions in the world."),
            (NarrativeMenuOptionArgs args) => { },
            (CharacterCreationManager manager) =>
            {
                var selectedCulture = manager.CharacterCreationContent?.SelectedCulture;
                if (selectedCulture == null)
                    return true;
                // Show only when the player's culture has no career options
                return !coveredCultures.Contains(selectedCulture.StringId);
            },
            (CharacterCreationManager manager) =>
            {
                SelectedCareerStringId = null;
                _logger.LogInfo("Player selected no career specialization (fallback)");
            },
            null);
    }

    private NarrativeMenuOption BuildOptionForCareer(CareerDefinition career, CareerMenuOptionDefinition ccData)
    {
        var careerId = career.Id;
        var eligibleCultures = career.EligibleCultureIds;
        var skillNames = ccData.Skills;
        var attributeName = ccData.Attribute;
        var focusToAdd = ccData.FocusToAdd;
        var skillLevelToAdd = ccData.SkillLevelToAdd;
        var attributeLevelToAdd = ccData.AttributeLevelToAdd;

        return new NarrativeMenuOption(
            $"taom_career_{careerId}",
            new TextObject(career.DisplayName),
            new TextObject(career.Description),
            (NarrativeMenuOptionArgs args) =>
            {
                // Resolve at runtime when game is initialized
                var skills = ResolveSkills(skillNames);
                var attribute = ResolveAttribute(attributeName);
                if (skills.Length > 0)
                {
                    args.SetAffectedSkills(skills);
                    args.SetFocusToSkills(focusToAdd);
                    args.SetLevelToSkills(skillLevelToAdd);
                }
                if (attribute != null)
                {
                    args.SetLevelToAttribute(attribute, attributeLevelToAdd);
                }
            },
            (CharacterCreationManager manager) =>
            {
                var selectedCulture = manager.CharacterCreationContent?.SelectedCulture;
                if (selectedCulture == null)
                    return false;

                foreach (var cultureId in eligibleCultures)
                {
                    if (string.Equals(selectedCulture.StringId, cultureId, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            },
            (CharacterCreationManager manager) =>
            {
                OnCareerOptionSelected(careerId);
                _logger.LogInfo($"Player selected career: {careerId}");
            },
            null);
    }

    private static List<NarrativeMenuCharacterArgs> GetCareerMenuCharacterArgs(
        CultureObject culture, string occupationType, CharacterCreationManager manager)
    {
        var cultureId = culture?.StringId ?? "gondor";
        var isFemale = Hero.MainHero?.IsFemale ?? false;
        var titleType = manager.CharacterCreationContent?.SelectedTitleType ?? "guard";
        var equipmentId = PlayerEquipmentRosterIds.Build(cultureId, titleType, isFemale);

        return new List<NarrativeMenuCharacterArgs>
        {
            new NarrativeMenuCharacterArgs(
                "player_career_character",
                25,
                equipmentId,
                "act_character_creation_male_default_standing",
                "spawnpoint_player_1",
                "", "", null, true, isFemale)
        };
    }

    private SkillObject[] ResolveSkills(string[] skillNames)
    {
        if (skillNames == null || skillNames.Length == 0)
            return Array.Empty<SkillObject>();

        var resolved = new List<SkillObject>();
        foreach (var name in skillNames)
        {
            if (SkillMap.TryGetValue(name, out var factory))
            {
                var skill = factory();
                if (skill != null)
                    resolved.Add(skill);
            }
            else
            {
                _logger.LogWarning($"Unknown skill name: '{name}'");
            }
        }
        return resolved.ToArray();
    }

    private CharacterAttribute ResolveAttribute(string attributeName)
    {
        if (string.IsNullOrEmpty(attributeName))
            return null;

        if (AttributeMap.TryGetValue(attributeName, out var factory))
            return factory();

        _logger.LogWarning($"Unknown attribute name: '{attributeName}'");
        return null;
    }
}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; ilspycmd -l c "'$dll | Select-String -Pattern '"'CharacterCreationContent' | Select-Object -First 50" in C:\Users\mikew\source\repos\TAOM
 succeeded in 645ms:

Class TaleWorlds.CampaignSystem.[7mCharacterCreationContent[0m.CharacterCreationBannerEditorStage
Class TaleWorlds.CampaignSystem.[7mCharacterCreationContent[0m.CharacterCreationClanNamingStage
Class TaleWorlds.CampaignSystem.[7mCharacterCreationContent[0m.CharacterCreationContent
Class TaleWorlds.CampaignSystem.[7mCharacterCreationContent[0m.CharacterCreationCultureStage
Class TaleWorlds.CampaignSystem.[7mCharacterCreationContent[0m.CharacterCreationFaceGeneratorStage
Class TaleWorlds.CampaignSystem.[7mCharacterCreationContent[0m.CharacterCreationManager
Class TaleWorlds.CampaignSystem.[7mCharacterCreationContent[0m.CharacterCreationNarrativeStage
Class TaleWorlds.CampaignSystem.[7mCharacterCreationContent[0m.CharacterCreationOptionsStage
Class TaleWorlds.CampaignSystem.[7mCharacterCreationContent[0m.CharacterCreationReviewStage
Class TaleWorlds.CampaignSystem.[7mCharacterCreationContent[0m.CharacterCreationStageBase
Class TaleWorlds.CampaignSystem.[7mCharacterCreationContent[0m.CharacterCreationState
Class TaleWorlds.CampaignSystem.[7mCharacterCreationContent[0m.NarrativeMenu
Class TaleWorlds.CampaignSystem.[7mCharacterCreationContent[0m.NarrativeMenuCharacter
Class TaleWorlds.CampaignSystem.[7mCharacterCreationContent[0m.NarrativeMenuOption
Class TaleWorlds.CampaignSystem.[7mCharacterCreationContent[0m.NarrativeMenuOptionArgs
Class TaleWorlds.CampaignSystem.[7mCharacterCreationContent[0m.CharacterCreationContent.<GetCultures>d__46
Class TaleWorlds.CampaignSystem.[7mCharacterCreationContent[0m.CharacterCreationManager.<>c__DisplayClass40_0
Class TaleWorlds.CampaignSystem.[7mCharacterCreationContent[0m.CharacterCreationManager.<>c


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; "'$out=ilspycmd -t TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationContent $dll; $out | Select-String -Pattern '"'SelectedTitleType|SelectedCulture|public string|CultureObject SelectedCulture' -Context 2" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1039ms:

  	private readonly List<TryGetEquipmentIdDelegate> _tryGetEquipmentIdDelegates = new 
List<TryGetEquipmentIdDelegate>();
  
> 	[7mpublic string[0m SelectedTitleType { get; set; }[0m
[7m[0m  [0m
> 	[7mpublic string[0m SelectedParentOccupation { get; private set; }[0m
[7m[0m  [0m
> 	[7mpublic string[0m DefaultSelectedTitleType { get; set; }[0m
[7m[0m  [0m
[7m[0m  	public TextObject ReviewPageDescription { get; private set; }[0m
  
> 	[7mpublic string[0m MainCharacterName { get; private set; }[0m
[7m[0m  [0m
> 	public [7mCultureObject SelectedCulture[0m { get; private set; }[0m
[7m[0m  [0m
[7m[0m  	public Banner SelectedBanner { get; private set; }[0m
  	}
  
> 	public void Set[7mSelectedCulture[0m(CultureObject culture, CharacterCreationManager characterCreationManager)[0m
[7m[0m  	{[0m
> 		[7mSelectedCulture[0m = culture;[0m
[7m[0m  		characterCreationManager.ResetMenuOptions();[0m
> 		[7mSelectedTitleType[0m = DefaultSelectedTitleType;[0m
[7m[0m  		TextObject textObject = FactionHelper.GenerateClanNameforPlayer();[0m
[7m[0m  		Clan.PlayerClan.ChangeClanName(textObject, textObject);[0m
  	public void ApplyCulture(CharacterCreationManager characterCreationManager)
  	{
> 		Hero.MainHero.Culture = [7mSelectedCulture[0m;[0m
> 		Clan.PlayerClan.Culture = [7mSelectedCulture[0m;[0m
[7m[0m  		Clan.PlayerClan.ResetPlayerHomeAndFactionMidSettlement();[0m
[7m[0m  		Hero.MainHero.BornSettlement = Clan.PlayerClan.HomeSettlement;[0m


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; "'$out=ilspycmd -t TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationManager $dll; $out | Select-String -Pattern '"'Finalize|CharacterCreationContent|ApplyCulture|OnCharacterCreation' -Context 2" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1071ms:

  using TaleWorlds.Library;
  
> namespace TaleWorlds.CampaignSystem.[7mCharacterCreationContent[0m;[0m
[7m[0m  [0m
[7m[0m  public class CharacterCreationManager[0m
  	public readonly Dictionary<NarrativeMenu, NarrativeMenuOption> SelectedOptions;
  
> 	private SortedList<int, I[7mCharacterCreationContent[0mHandler> [0m[7m[0m_handlers [0m[7m[0m= [0m[7m[0mnew [0m[7m[0mSortedList<int, [0m
[7m[0mICharacterCreationContentHandler>();[0m
[7m[0m  [0m
[7m[0m  	private readonly CharacterCreationState _state;[0m
  	public MBReadOnlyList<NarrativeMenu> NarrativeMenus => _narrativeMenus;
  
> 	public [7mCharacterCreationContent[0m CharacterCreationContent { get; private set; }[0m
[7m[0m  [0m
[7m[0m  	public NarrativeMenu CurrentMenu { get; private set; }[0m
  		_narrativeMenus = new MBList<NarrativeMenu>();
  		SelectedOptions = new Dictionary<NarrativeMenu, NarrativeMenuOption>();
> 		[7mCharacterCreationContent[0m = new CharacterCreationContent();[0m
> 		CampaignEventDispatcher.Instance.[7mOnCharacterCreation[0mInitialized(this);
> 		foreach (KeyValuePair<int, I[7mCharacterCreationContent[0mHandler> handler in _handlers)[0m
[7m[0m  		{[0m
[7m[0m  			handler.Value.InitializeContent(this);[0m
  		}
> 		foreach (KeyValuePair<int, I[7mCharacterCreationContent[0mHandler> handler2 in _handlers)[0m
[7m[0m  		{[0m
[7m[0m  			handler2.Value.AfterInitializeContent(this);[0m
  	}
  
> 	public void Register[7mCharacterCreationContent[0mHandler(ICharacterCreationContentHandler [0m
[7m[0mcharacterCreationContentHandler, [0m[7m[0mint [0m[7m[0mpriority)[0m
[7m[0m  	{[0m
> 		_handlers.Add(priority, [7mcharacterCreationContent[0mHandler);[0m
[7m[0m  	}[0m
[7m[0m  [0m
  		if (CurrentStage != null)
  		{
> 			CurrentStage?.On[7mFinalize[0m();[0m
> 			foreach (KeyValuePair<int, I[7mCharacterCreationContent[0mHandler> handler in _handlers)[0m
[7m[0m  			{[0m
[7m[0m  				handler.Value.OnStageCompleted(CurrentStage);[0m
  		{
  			ApplyFinalEffects();
> 			_state.[7mFinalize[0mCharacterCreationState();[0m
[7m[0m  		}[0m
[7m[0m  		else[0m
  	public void PreviousStage()
  	{
> 		CurrentStage?.On[7mFinalize[0m();[0m
[7m[0m  		_stageIndex--;[0m
[7m[0m  		ActivateStage(_stages[_stageIndex]);[0m
  		if (stageIndex >= 0 && stageIndex < _stages.Count && stageIndex != _stageIndex && stageIndex <= 
_furthestStageIndex)
  		{
> 			CurrentStage?.On[7mFinalize[0m();[0m
[7m[0m  			_stageIndex = stageIndex;[0m
[7m[0m  			ActivateStage(_stages[_stageIndex]);[0m
  	{
  		List<NarrativeMenuCharacter> characters = CurrentMenu.Characters;
> 		foreach (NarrativeMenuCharacterArgs item in 
CurrentMenu.GetNarrativeMenuCharacterArgs([7mCharacterCreationContent[0m.SelectedCulture, [0m
[7m[0mCharacterCreationContent.SelectedTitleType, [0m[7m[0mthis))[0m
[7m[0m  		{[0m
[7m[0m  			foreach (NarrativeMenuCharacter item2 in characters)[0m
  					if (mBEquipmentRoster == null)
  					{
> 						Debug.FailedAssert("character creation menu character equipment should not be null! Equipment id: " + 
item.EquipmentId, "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\[7mCharacterCreationContent[0m\\[0m
[7m[0mCharacterCreationManager.cs", [0m[7m[0m"ModifyMenuCharacters", [0m[7m[0m305);[0m
[7m[0m  						mBEquipmentRoster = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>("player_char_creation_default");[0m
[7m[0m  					}[0m
  	{
  		Clan.PlayerClan.Renown = 0f;
> 		[7mCharacterCreationContent[0m.ApplyCulture(this);[0m
[7m[0m  		foreach (KeyValuePair<NarrativeMenu, NarrativeMenuOption> selectedOption in SelectedOptions)[0m
[7m[0m  		{[0m
> 			selectedOption.Value.ApplyFinalEffects([7mCharacterCreationContent[0m);[0m
[7m[0m  		}[0m
[7m[0m  		TraitLevelingHelper.UpdateTraitXPAccordingToTraitLevels();[0m
  			else
  			{
> 				Debug.FailedAssert("Selected culture start pos is invalid!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\Tale
Worlds.CampaignSystem\\[7mCharacterCreationContent[0m\\CharacterCreationManager.cs", [0m[7m[0m"ApplyFinalEffects", [0m[7m[0m382);[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m	[0m[7m	[0m[7m[0mCampaignVec2 [0m[7m[0mclosestNavMeshFaceCenterPositionForPosition [0m[7m[0m= [0m
[7m[0mNavigationHelper.GetClosestNavMeshFaceCenterPositionForPosition(culture.StartingPoint, [0m[7m[0mCampaign.Current.Models.PartyNav[0m
[7m[0migationModel.GetInvalidTerrainTypesForNavigationType(MobileParty.MainParty.NavigationCapability));[0m
[7m[0m  				MobileParty.MainParty.Position = closestNavMeshFaceCenterPositionForPosition;[0m
  			mapState.Handler.TeleportCameraToMainParty();
  		}
> 		foreach (KeyValuePair<int, I[7mCharacterCreationContent[0mHandler> handler in _handlers)[0m
[7m[0m  		{[0m
> 			handler.Value.[7mOnCharacterCreation[0mFinalize(this);[0m
[7m[0m  		}[0m
[7m[0m  	}[0m


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; "'$out=ilspycmd -t TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationManager $dll; $out | Select-String -Pattern '"'private void ApplyFinalEffects' -Context 0,80" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1030ms:
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; "'$out=ilspycmd -t TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationManager $dll; $idx=($out | Select-String -Pattern '"'void ApplyFinalEffects' | Select-Object -First 1).LineNumber; "'$out | Select-Object -Skip ($idx-3) -First 90' in C:\Users\mikew\source\repos\TAOM
 succeeded in 1045ms:
	}

	public void ApplyFinalEffects()
	{
		Clan.PlayerClan.Renown = 0f;
		CharacterCreationContent.ApplyCulture(this);
		foreach (KeyValuePair<NarrativeMenu, NarrativeMenuOption> selectedOption in SelectedOptions)
		{
			selectedOption.Value.ApplyFinalEffects(CharacterCreationContent);
		}
		TraitLevelingHelper.UpdateTraitXPAccordingToTraitLevels();
		CultureObject culture = CharacterObject.PlayerCharacter.Culture;
		if (culture.StartingPoint.IsNonZero())
		{
			if (NavigationHelper.IsPositionValidForNavigationType(culture.StartingPoint, MobileParty.MainParty.NavigationCapability))
			{
				MobileParty.MainParty.Position = culture.StartingPoint;
			}
			else
			{
				Debug.FailedAssert("Selected culture start pos is invalid!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\CharacterCreationContent\\CharacterCreationManager.cs", "ApplyFinalEffects", 382);
				CampaignVec2 closestNavMeshFaceCenterPositionForPosition = NavigationHelper.GetClosestNavMeshFaceCenterPositionForPosition(culture.StartingPoint, Campaign.Current.Models.PartyNavigationModel.GetInvalidTerrainTypesForNavigationType(MobileParty.MainParty.NavigationCapability));
				MobileParty.MainParty.Position = closestNavMeshFaceCenterPositionForPosition;
			}
		}
		if (GameStateManager.Current.ActiveState is MapState mapState)
		{
			mapState.Handler.ResetCamera(resetDistance: true, teleportToMainParty: true);
			mapState.Handler.TeleportCameraToMainParty();
		}
		foreach (KeyValuePair<int, ICharacterCreationContentHandler> handler in _handlers)
		{
			handler.Value.OnCharacterCreationFinalize(this);
		}
	}
}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Main\\SubModule.cs -Pattern 'AddBehavior|CampaignBehavior|Messenger|StartupResources|Register.*Feature' -Context 1" in C:\Users\mikew\source\repos\TAOM
 succeeded in 482ms:

  Main\SubModule.cs:26:using TAOM.Features.RaceAge.Models;
> Main\SubModule.cs:27:using TAOM.Features.[7mStartupResources[0m;[0m
[7m[0m  Main\SubModule.cs:28:using TAOM.Features.NamedCompanions;[0m
  Main\SubModule.cs:63:using BehaviorTreeWrapper;
> Main\SubModule.cs:64:using TaleWorlds.CampaignSystem.[7mCampaignBehavior[0ms;[0m
[7m[0m  Main\SubModule.cs:65:[0m
  Main\SubModule.cs:120:        DiplomacyIoC.InitializeHooks(allianceHook, peaceHook);
> Main\SubModule.cs:121:        Alliance[7mCampaignBehavior[0m_EndAlliance_Patch.Initialize(logger);[0m
[7m[0m  Main\SubModule.cs:122:        DeclareWarAction_ApplyInternal_Patch.Initialize(logger);[0m
  Main\SubModule.cs:215:            var racePersistenceService = IoC.Resolve<IRacePersistenceService>();
> Main\SubModule.cs:216:            campaignStarter.[7mAddBehavior[0m(new RacePersistenceBehavior(racePersistenceService));[0m
[7m[0m  Main\SubModule.cs:217:[0m
  Main\SubModule.cs:218:            var bannerInjectionService = IoC.Resolve<IBannerInjectionService>();
> Main\SubModule.cs:219:            campaignStarter.[7mAddBehavior[0m(new BannerInjectionBehavior(bannerInjectionService));[0m
[7m[0m  Main\SubModule.cs:220:[0m
  Main\SubModule.cs:222:            var ccLogger = IoC.Resolve<IModLogger>();
> Main\SubModule.cs:223:            campaignStarter.[7mAddBehavior[0m(new [0m
[7m[0mCharacterCreationRegistrationBehavior(ccContentService, [0m[7m[0mccLogger));[0m
[7m[0m  Main\SubModule.cs:224:[0m
> Main\SubModule.cs:225:            campaignStarter.RemoveBehaviors<InitialChildGeneration[7mCampaignBehavior[0m>();[0m
[7m[0m  Main\SubModule.cs:226:            var childGenService = IoC.Resolve<IInitialChildGenerationService>();[0m
> Main\SubModule.cs:227:            campaignStarter.[7mAddBehavior[0m(new [0m
[7m[0mTaomInitialChildGenerationBehavior(childGenService));[0m
[7m[0m  Main\SubModule.cs:228:[0m
  Main\SubModule.cs:239:            var raceAgeLogger = IoC.Resolve<IModLogger>();
> Main\SubModule.cs:240:            campaignStarter.[7mAddBehavior[0m(new [0m[7m[0mRaceAgeBehavior(raceAgeService, [0m[7m[0mheroAgeAdapter, [0m
[7m[0mraceAgeLogger));[0m
[7m[0m  Main\SubModule.cs:241:            campaignStarter.AddModel(new TaomAgeModel(raceAgeService));[0m
  Main\SubModule.cs:247:            var diplomacyLogger = IoC.Resolve<IModLogger>();
> Main\SubModule.cs:248:            campaignStarter.[7mAddBehavior[0m(new [0m[7m[0mDiplomacyBehavior(diplomacyService, [0m
[7m[0mdiplomacyLogger));[0m
[7m[0m  Main\SubModule.cs:249:            campaignStarter.AddModel(new TaomAllianceModel(diplomacyService));[0m
  Main\SubModule.cs:253:            var wotrLogger = IoC.Resolve<IModLogger>();
> Main\SubModule.cs:254:            campaignStarter.[7mAddBehavior[0m(new WarOfTheRingBehavior(wotrService, wotrLogger));[0m
[7m[0m  Main\SubModule.cs:255:[0m
  Main\SubModule.cs:257:            var siegeDefenseLogger = IoC.Resolve<IModLogger>();
> Main\SubModule.cs:258:            campaignStarter.[7mAddBehavior[0m(new [0m[7m[0mSiegeDefenseBehavior(siegeDefenseService, [0m
[7m[0msiegeDefenseLogger));[0m
[7m [0m[7m [0m[7m[0mMain\SubModule.cs:259: [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m[0mcampaignStarter.AddModel(new [0m
[7m[0mTaomSiegeEventModel(IoC.Resolve<ISiegeEngineAvailabilityService>()));[0m
  Main\SubModule.cs:300:                specialResourceService, specialResourceStorage, specialResourceConfig, 
specialResourceLogger);
> Main\SubModule.cs:301:            campaignStarter.[7mAddBehavior[0m(specialResourceBehavior);[0m
[7m[0m  Main\SubModule.cs:302:            PartyScreenLogic_AddCommand_Patch.SetBehavior(specialResourceBehavior);[0m
  Main\SubModule.cs:307:            var careerLogger = IoC.Resolve<IModLogger>();
> Main\SubModule.cs:308:            campaignStarter.[7mAddBehavior[0m(new [0m[7m[0mCareerPersistenceBehavior(careerDataService, [0m
[7m[0mcareerLogger));[0m
[7m[0m  Main\SubModule.cs:309:            var careerCreationHandler = IoC.Resolve<ICareerCreationHandler>();[0m
> Main\SubModule.cs:310:            campaignStarter.[7mAddBehavior[0m(new CareerCampaignBehavior([0m
[7m [0m[7m [0m[7m[0mMain\SubModule.cs:311: [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m[0mcareerDataService, [0m[7m[0mcareerRegistry, [0m[7m[0mcareerPassiveService, [0m
[7m[0mcareerCreationHandler, [0m[7m[0mcareerLogger));[0m
  Main\SubModule.cs:314:            var careerAdapterFactory = IoC.Resolve<ICareerHeroAdapterFactory>();
> Main\SubModule.cs:315:            campaignStarter.[7mAddBehavior[0m(new CareerSwitchDialogueBehavior([0m
[7m [0m[7m [0m[7m[0mMain\SubModule.cs:316: [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m[0mcareerDataService, [0m[7m[0mcareerRegistry, [0m[7m[0mcareerSwitchService, [0m[7m[0mcareerAdapterFactory, [0m
[7m[0mcareerLogger));[0m
  Main\SubModule.cs:327:            var startupLogger = IoC.Resolve<IModLogger>();
> Main\SubModule.cs:328:            campaignStarter.[7mAddBehavior[0m(new [0m[7m[0mStartupResourcesBehavior(goldService, [0m
[7m[0minfluenceService, [0m[7m[0mstartupLogger));[0m
[7m[0m  Main\SubModule.cs:329:[0m
  Main\SubModule.cs:330:            var namedCompanionService = IoC.Resolve<INamedCompanionService>();
> Main\SubModule.cs:331:            campaignStarter.[7mAddBehavior[0m(new NamedCompanionBehavior(namedCompanionService));[0m
[7m[0m  Main\SubModule.cs:332:        }[0m
  Main\SubModule.cs:371:        var settlementGuardService = IoC.Resolve<ISettlementGuardService>();
> Main\SubModule.cs:372:        Guards[7mCampaignBehavior[0m_TakeGuardAgentData_Patch.Initialize(settlementGuardService);[0m
> Main\SubModule.cs:373:        Guards[7mCampaignBehavior[0m_GetSuitableSpear_Patch.Initialize(settlementGuardService);[0m
[7m[0m  Main\SubModule.cs:374:[0m
> Main\SubModule.cs:375:        // Manual patches for private Guards[7mCampaignBehavior[0m methods (SandBox.dll)
> Main\SubModule.cs:376:        var takeGuardTarget = Guards[7mCampaignBehavior[0m_TakeGuardAgentData_Patch.TargetMethod();[0m
[7m[0m  Main\SubModule.cs:377:        if (takeGuardTarget != null)[0m
  Main\SubModule.cs:378:            _harmony.Patch(takeGuardTarget, prefix: new HarmonyMethod(
> Main\SubModule.cs:379:                typeof(Guards[7mCampaignBehavior[0m_TakeGuardAgentData_Patch),[0m
> Main\SubModule.cs:380:                nameof(Guards[7mCampaignBehavior[0m_TakeGuardAgentData_Patch.Prefix)));[0m
[7m[0m  Main\SubModule.cs:381:        else[0m
  Main\SubModule.cs:383:
> Main\SubModule.cs:384:        var spearTarget = Guards[7mCampaignBehavior[0m_GetSuitableSpear_Patch.TargetMethod();[0m
[7m[0m  Main\SubModule.cs:385:        if (spearTarget != null)[0m
  Main\SubModule.cs:386:            _harmony.Patch(spearTarget, prefix: new HarmonyMethod(
> Main\SubModule.cs:387:                typeof(Guards[7mCampaignBehavior[0m_GetSuitableSpear_Patch),[0m
> Main\SubModule.cs:388:                nameof(Guards[7mCampaignBehavior[0m_GetSuitableSpear_Patch.Prefix)));[0m
[7m[0m  Main\SubModule.cs:389:        else[0m


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content Main'"\\SubModule.cs | ForEach-Object { if ("'$i -ge 205 -and $i -le 335) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 460ms:
 205:                 isHidden:            null));
 206:         }
 207:     }
 208: 
 209:     protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
 210:     {
 211:         base.OnGameStart(game, gameStarterObject);
 212: 
 213:         if (gameStarterObject is CampaignGameStarter campaignStarter)
 214:         {
 215:             var racePersistenceService = IoC.Resolve<IRacePersistenceService>();
 216:             campaignStarter.AddBehavior(new RacePersistenceBehavior(racePersistenceService));
 217: 
 218:             var bannerInjectionService = IoC.Resolve<IBannerInjectionService>();
 219:             campaignStarter.AddBehavior(new BannerInjectionBehavior(bannerInjectionService));
 220: 
 221:             var ccContentService = IoC.Resolve<ICharacterCreationContentService>();
 222:             var ccLogger = IoC.Resolve<IModLogger>();
 223:             campaignStarter.AddBehavior(new CharacterCreationRegistrationBehavior(ccContentService, ccLogger));
 224: 
 225:             campaignStarter.RemoveBehaviors<InitialChildGenerationCampaignBehavior>();
 226:             var childGenService = IoC.Resolve<IInitialChildGenerationService>();
 227:             campaignStarter.AddBehavior(new TaomInitialChildGenerationBehavior(childGenService));
 228: 
 229:             var costService = IoC.Resolve<ITroopCostService>();
 230:             var volunteerService = IoC.Resolve<IVolunteerTierService>();
 231:             var recruitmentService = IoC.Resolve<IVolunteerRecruitmentService>();
 232:             var volunteerContextAdapter = IoC.Resolve<IVolunteerContextAdapter>();
 233:             campaignStarter.AddModel(new TaomCharacterStatsModel());
 234:             campaignStarter.AddModel(new TaomPartyWageModel(costService));
 235:             campaignStarter.AddModel(new TaomVolunteerModel(volunteerService, recruitmentService, volunteerContextAdapter));
 236: 
 237:             var raceAgeService = IoC.Resolve<IRaceAgeService>();
 238:             var heroAgeAdapter = IoC.Resolve<IHeroAgeAdapter>();
 239:             var raceAgeLogger = IoC.Resolve<IModLogger>();
 240:             campaignStarter.AddBehavior(new RaceAgeBehavior(raceAgeService, heroAgeAdapter, raceAgeLogger));
 241:             campaignStarter.AddModel(new TaomAgeModel(raceAgeService));
 242:             campaignStarter.AddModel(new TaomPregnancyModel(raceAgeService));
 243:             campaignStarter.AddModel(new TaomHeroCreationModel());
 244: 
 245:             var diplomacyService = IoC.Resolve<IDiplomacyService>();
 246:             var wotrService = IoC.Resolve<IWarOfTheRingService>();
 247:             var diplomacyLogger = IoC.Resolve<IModLogger>();
 248:             campaignStarter.AddBehavior(new DiplomacyBehavior(diplomacyService, diplomacyLogger));
 249:             campaignStarter.AddModel(new TaomAllianceModel(diplomacyService));
 250:             campaignStarter.AddModel(new TaomKingdomDecisionPermissionModel(diplomacyService, wotrService));
 251:             campaignStarter.AddModel(new TaomDiplomacyModel(wotrService));
 252: 
 253:             var wotrLogger = IoC.Resolve<IModLogger>();
 254:             campaignStarter.AddBehavior(new WarOfTheRingBehavior(wotrService, wotrLogger));
 255: 
 256:             var siegeDefenseService = IoC.Resolve<ISiegeDefenseService>();
 257:             var siegeDefenseLogger = IoC.Resolve<IModLogger>();
 258:             campaignStarter.AddBehavior(new SiegeDefenseBehavior(siegeDefenseService, siegeDefenseLogger));
 259:             campaignStarter.AddModel(new TaomSiegeEventModel(IoC.Resolve<ISiegeEngineAvailabilityService>()));
 260: 
 261:             var executionAction = IoC.Resolve<IOnExecutionAction>();
 262:             campaignStarter.AddModel(new TaomExecutionRelationModel(executionAction));
 263: 
 264:             // Cultural feat models
 265:             campaignStarter.AddModel(new TaomArmyManagementModel());
 266:             campaignStarter.AddModel(new TaomPartySpeedModel());
 267:             campaignStarter.AddModel(new TaomSettlementProsperityModel());
 268:             campaignStarter.AddModel(new TaomSettlementMilitiaModel());
 269:             campaignStarter.AddModel(new TaomBuildingConstructionModel());
 270:             campaignStarter.AddModel(new TaomVillageProductionModel());
 271:             campaignStarter.AddModel(new TaomCaravanModel());
 272:             campaignStarter.AddModel(new TaomBattleRewardModel());
 273:             campaignStarter.AddModel(new TaomTournamentModel());
 274:             campaignStarter.AddModel(new TaomPartyTroopUpgradeModel());
 275:             campaignStarter.AddModel(new TaomPartySizeModel());
 276:             campaignStarter.AddModel(new TaomFoodConsumptionModel());
 277:             campaignStarter.AddModel(new TaomSettlementLoyaltyModel(IoC.Resolve<IRevoltTuningConfigProvider>()));
 278:             campaignStarter.AddModel(new TaomPartyMoraleModel());
 279:             campaignStarter.AddModel(new TaomSmithingModel());
 280:             campaignStarter.AddModel(new TaomClanFinanceModel());
 281:             campaignStarter.AddModel(new TaomRaidModel());
 282: 
 283:             // Battle balance models
 284:             var battleBalanceSettings = IoC.Resolve<IBattleBalanceSettingsProvider>();
 285:             var battleBalanceConfig = IoC.Resolve<IBattleBalanceConfigProvider>();
 286:             campaignStarter.AddModel(new TaomMilitaryPowerModel(battleBalanceSettings, battleBalanceConfig));
 287:             campaignStarter.AddModel(new TaomCombatSimulationModel(battleBalanceSettings));
 288:             campaignStarter.AddModel(new TaomPartyHealingModel(battleBalanceSettings, battleBalanceConfig));
 289: 
 290:             campaignStarter.AddModel(new TaomInformationRestrictionModel());
 291: 
 292:             var armyTargetingService = IoC.Resolve<IArmyTargetingService>();
 293:             campaignStarter.AddModel(new TaomTargetScoreModel(armyTargetingService));
 294: 
 295:             var specialResourceService = IoC.Resolve<ISpecialResourceService>();
 296:             var specialResourceStorage = IoC.Resolve<ISpecialResourceStorageService>();
 297:             var specialResourceConfig = IoC.Resolve<ISpecialResourceConfigProvider>();
 298:             var specialResourceLogger = IoC.Resolve<IModLogger>();
 299:             var specialResourceBehavior = new SpecialResourcesBehavior(
 300:                 specialResourceService, specialResourceStorage, specialResourceConfig, specialResourceLogger);
 301:             campaignStarter.AddBehavior(specialResourceBehavior);
 302:             PartyScreenLogic_AddCommand_Patch.SetBehavior(specialResourceBehavior);
 303: 
 304:             var careerDataService = IoC.Resolve<ICareerDataService>();
 305:             var careerRegistry = IoC.Resolve<ICareerRegistry>();
 306:             var careerPassiveService = IoC.Resolve<ICareerPassiveService>();
 307:             var careerLogger = IoC.Resolve<IModLogger>();
 308:             campaignStarter.AddBehavior(new CareerPersistenceBehavior(careerDataService, careerLogger));
 309:             var careerCreationHandler = IoC.Resolve<ICareerCreationHandler>();
 310:             campaignStarter.AddBehavior(new CareerCampaignBehavior(
 311:                 careerDataService, careerRegistry, careerPassiveService, careerCreationHandler, careerLogger));
 312: 
 313:             var careerSwitchService = IoC.Resolve<ICareerSwitchService>();
 314:             var careerAdapterFactory = IoC.Resolve<ICareerHeroAdapterFactory>();
 315:             campaignStarter.AddBehavior(new CareerSwitchDialogueBehavior(
 316:                 careerDataService, careerRegistry, careerSwitchService, careerAdapterFactory, careerLogger));
 317: 
 318:             // Career system GameModels — reuse careerPassiveService resolved above (line 300).
 319:             campaignStarter.AddModel(new TaomMapVisibilityModel());
 320:             campaignStarter.AddModel(new TaomInventoryCapacityModel());
 321:             campaignStarter.AddModel<AgentStatCalculateModel>(new TaomAgentStatCalculateModel(careerPassiveService));
 322:             campaignStarter.AddModel<AgentApplyDamageModel>(new TaomAgentApplyDamageModel(careerPassiveService));
 323:             campaignStarter.AddModel(new TaomClanTierModel(careerPassiveService));
 324: 
 325:             var goldService = IoC.Resolve<IStartupGoldService>();
 326:             var influenceService = IoC.Resolve<IStartupInfluenceService>();
 327:             var startupLogger = IoC.Resolve<IModLogger>();
 328:             campaignStarter.AddBehavior(new StartupResourcesBehavior(goldService, influenceService, startupLogger));
 329: 
 330:             var namedCompanionService = IoC.Resolve<INamedCompanionService>();
 331:             campaignStarter.AddBehavior(new NamedCompanionBehavior(namedCompanionService));
 332:         }
 333:     }
 334: 
 335:     public override void OnGameInitializationFinished(Game game)

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content Main'"\\Features\\StartupResources\\PlayerStartupGoldService.cs | ForEach-Object { '{0,4}: {1}' -f "'$i, $_; $i++ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 432ms:
   1: using System;
   2: using System.Linq;
   3: using TAOM.Adapters;
   4: using TAOM.Core.Logging;
   5: 
   6: namespace TAOM.Features.StartupResources;
   7: 
   8: public class PlayerStartupGoldService : IPlayerStartupGoldService
   9: {
  10:     private readonly IGoldGiftAdapter _goldGiftAdapter;
  11:     private readonly IStartupResourcesConfigProvider _configProvider;
  12:     private readonly IModLogger _logger;
  13: 
  14:     public PlayerStartupGoldService(
  15:         IGoldGiftAdapter goldGiftAdapter,
  16:         IStartupResourcesConfigProvider configProvider,
  17:         IModLogger logger)
  18:     {
  19:         _goldGiftAdapter = goldGiftAdapter;
  20:         _configProvider = configProvider;
  21:         _logger = logger;
  22:     }
  23: 
  24:     public void GrantPlayerStartupGold(string cultureId, string playerHeroId)
  25:     {
  26:         if (string.IsNullOrEmpty(cultureId))
  27:         {
  28:             _logger.LogWarning("PlayerStartupGoldService: cultureId is null or empty — skipping grant");
  29:             return;
  30:         }
  31: 
  32:         if (string.IsNullOrEmpty(playerHeroId))
  33:         {
  34:             _logger.LogWarning($"PlayerStartupGoldService: playerHeroId is null or empty for culture '{cultureId}' — skipping grant");
  35:             return;
  36:         }
  37: 
  38:         var config = _configProvider.LoadConfig();
  39:         var entry = config.CultureEntries.FirstOrDefault(
  40:             e => string.Equals(e.CultureId, cultureId, StringComparison.OrdinalIgnoreCase));
  41: 
  42:         if (entry == null)
  43:         {
  44:             _logger.LogWarning($"PlayerStartupGoldService: no startup-resources entry for culture '{cultureId}' — no gold granted");
  45:             return;
  46:         }
  47: 
  48:         if (entry.PlayerGold <= 0)
  49:             return;
  50: 
  51:         _goldGiftAdapter.GiveGoldToHero(playerHeroId, entry.PlayerGold);
  52:         _logger.LogInfo($"PlayerStartupGoldService: granted {entry.PlayerGold} gold to player ({cultureId})");
  53:     }
  54: }

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content Main\\_Module\\ModuleData\\equipmentsets\\taom_char_creation_equipment.xml | Select-Object -Skip 200 -First 70" in C:\Users\mikew\source\repos\TAOM
 succeeded in 444ms:
				slot="Body"
				id="Item.gondor_noble_coat_a" />
			<Equipment
				slot="Leg"
				id="Item.ithilien_boots" />
		</EquipmentSet>
	</EquipmentRoster>
	<!-- Childhood & Education age -->
	<EquipmentRoster
		id="player_char_creation_childhood_age_gondor_retainer_m"
		culture="Culture.gondor">
		<EquipmentSet>
			<Equipment
				slot="Body"
				id="Item.gondor_noble_coat_b" />
			<Equipment
				slot="Leg"
				id="Item.sk_gd_ano_boots_a" />
		</EquipmentSet>
	</EquipmentRoster>
	<EquipmentRoster
		id="player_char_creation_education_age_gondor_retainer_m"
		culture="Culture.gondor">
		<EquipmentSet>
			<Equipment
				slot="Body"
				id="Item.gondor_noble_coat_b" />
			<Equipment
				slot="Leg"
				id="Item.sk_gd_ano_boots_a" />
		</EquipmentSet>
	</EquipmentRoster>
	<EquipmentRoster
		id="player_char_creation_childhood_age_gondor_retainer_f"
		culture="Culture.gondor">
		<EquipmentSet>
			<Equipment
				slot="Body"
				id="Item.gondor_noble_coat_a" />
			<Equipment
				slot="Leg"
				id="Item.sk_gd_ano_boots_a" />
		</EquipmentSet>
	</EquipmentRoster>
	<EquipmentRoster
		id="player_char_creation_education_age_gondor_retainer_f"
		culture="Culture.gondor">
		<EquipmentSet>
			<Equipment
				slot="Body"
				id="Item.gondor_noble_coat_a" />
			<Equipment
				slot="Leg"
				id="Item.sk_gd_ano_boots_a" />
		</EquipmentSet>
	</EquipmentRoster>
	<EquipmentRoster
		id="player_char_creation_childhood_age_gondor_merchant_m"
		culture="Culture.gondor">
		<EquipmentSet>
			<Equipment
				slot="Body"
				id="Item.gondor_noble_coat_b" />
			<Equipment
				slot="Leg"
				id="Item.sk_gd_ano_boots_a" />
		</EquipmentSet>
	</EquipmentRoster>
	<EquipmentRoster
		id="player_char_creation_education_age_gondor_merchant_m"

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Main\\_Module\\ModuleData\\equipmentsets\\taom_char_creation_equipment.xml -Pattern 'player_char_creation_sturgia_(retainer|guard)_[mf]' -Context 0,2" in C:\Users\mikew\source\repos\TAOM
 succeeded in 432ms:
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Main\\_Module\\ModuleData\\equipmentsets\\taom_char_creation_equipment.xml -Pattern 'player_char_creation_.*sturgia' | Select-Object -First 50 LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
 succeeded in 425ms:
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Recurse -File Main\\_Module\\ModuleData | Select-String -Pattern 'player_char_creation_sturgia|sturgia_guard|retainer' | Select-Object -First 100 Path,LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
 succeeded in 561ms:

[32;1mPath                                                                                                   [0m[32;1m LineNumber[0m[32;1m Line[0m
[32;1m----                                                                                                   [0m [32;1m----------[0m [32;1m----[0m
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml                                   273 	<s…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml                                  1353 	<s…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\adulthood_menu.json                 593    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\education_menu.json                   5    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\education_menu.json                  95    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\education_menu.json                 185    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\education_menu.json                 275    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\education_menu.json                 365    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\education_menu.json                 455    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\education_menu.json                 545    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\education_menu.json                 635    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\education_menu.json                 725    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\education_menu.json                 815    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\education_menu.json                 905    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\education_menu.json                 995    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\education_menu.json                1085    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\education_menu.json                1175    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\education_menu.json                1265    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\education_menu.json                1355    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\parents_menu.json                    12    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\parents_menu.json                    85    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\parents_menu.json                   158    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\parents_menu.json                   231    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\parents_menu.json                   304    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\parents_menu.json                   377    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\parents_menu.json                   450    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\parents_menu.json                   523    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\parents_menu.json                   596    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\parents_menu.json                   669    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\parents_menu.json                   742    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\parents_menu.json                   808    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\parents_menu.json                   815    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\parents_menu.json                   888    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\parents_menu.json                   961    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\parents_menu.json                  1034    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\parents_menu.json                  1107    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\youth_menu.json                       5    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\youth_menu.json                      65    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\youth_menu.json                     209    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\youth_menu.json                     329    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\youth_menu.json                     389    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\youth_menu.json                     461    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\youth_menu.json                     533    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\youth_menu.json                     605    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\youth_menu.json                     677    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\youth_menu.json                     749    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\youth_menu.json                     893    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\youth_menu.json                     965    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\youth_menu.json                    1037    …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml         37 	<!…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml         39 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml         57 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        210 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        222 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        234 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        246 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        499 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml        541 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1218 	<!…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1220 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1235 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1382 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1394 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1406 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1418 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1671 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       1710 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2345 	<!…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2347 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2365 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2518 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2530 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2542 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2554 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2807 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       2849 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3529 	<!…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3531 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3549 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3702 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3714 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3726 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3738 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       3991 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4033 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4713 	<!…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4715 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4733 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4886 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4898 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4910 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       4922 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5175 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5217 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5897 	<!…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5899 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       5917 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6070 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6082 		i…
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml       6094 		i…


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "@'
import re, pathlib
p=pathlib.Path('Main/_Module/ModuleData/equipmentsets/taom_char_creation_equipment.xml')
ids=re.findall(r'id=\"(["'^"]*player_char_creation[''^"]*)"'"', p.read_text(encoding='utf-8'))
for s in sorted(set(i.split('_')[3] if i.startswith('player_char_creation_') else '' for i in ids)):
    print(s)
print('count', len(ids), len(set(ids)))
print([i for i in ids if 'sturgia' in i][:10])
'@ | python -" in C:\Users\mikew\source\repos\TAOM
 succeeded in 469ms:
abanissa
childhood
dolguldur
education
erebor
gondor
gundabad
isengard
lothlorien
mirkwood
mordor
rivendell
shaghana
show
umbar
count 492 492
[]

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules' -Recurse -File -Include *.xml | Select-String -Pattern 'player_char_creation_sturgia_(guard|retainer)_[mf]' | Select-Object Path,LineNumber,Line -First 20" in C:\Users\mikew\source\repos\TAOM
 succeeded in 8593ms:

[32;1mPath                                                                                                       [0m[32;1m LineNumber[0m
[32;1m----                                                                                                       [0m [32;1m----------[0m
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\sandbox_equipment_sets.xml      13328
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\sandbox_equipment_sets.xml      13382


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\SandBox\\ModuleData\\sandbox_equipment_sets.xml' -Pattern 'player_char_creation_sturgia_(guard|retainer)_[mf]' -Context 0,1 | Format-List" in C:\Users\mikew\source\repos\TAOM
 succeeded in 446ms:

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m13328
[32;1mLine       : [0m		id="player_char_creation_sturgia_guard_m"
[32;1mFilename   : [0msandbox_equipment_sets.xml
[32;1mPath       : [0mE:\Steam\steamapps\common\Mount & Blade II 
             Bannerlord\Modules\SandBox\ModuleData\sandbox_equipment_sets.xml
[32;1mPattern    : [0mplayer_char_creation_sturgia_(guard|retainer)_[mf]
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m13382
[32;1mLine       : [0m		id="player_char_creation_sturgia_guard_f"
[32;1mFilename   : [0msandbox_equipment_sets.xml
[32;1mPath       : [0mE:\Steam\steamapps\common\Mount & Blade II 
             Bannerlord\Modules\SandBox\ModuleData\sandbox_equipment_sets.xml
[32;1mPattern    : [0mplayer_char_creation_sturgia_(guard|retainer)_[mf]
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "@'
import json,re,pathlib
# collect youth entries culture_id/title_type
entries=json.loads(pathlib.Path('Main/_Module/ModuleData/charactercreation/youth_menu.json').read_text())
# collect roster ids in TAOM + vanilla modules
ids=set()
paths=[pathlib.Path('Main/_Module/ModuleData/equipmentsets/taom_char_creation_equipment.xml'), pathlib.Path(r'E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/SandBox/ModuleData/sandbox_equipment_sets.xml')]
for p in paths:
    text=p.read_text(encoding='utf-8-sig', errors='ignore')
    ids.update(re.findall(r'id=\"(["'^"]*player_char_creation[''^"]*)"'"', text))
missing=[]
for e in entries:
    c=e.get('culture_id'); t=e.get('title_type')
    if c and t:
        for g in 'mf':
            rid=f'player_char_creation_{c}_{t}_{g}'
            if rid not in ids:
                missing.append((e.get('string_id'), rid))
print('missing', len(missing))
for x in missing[:100]: print(x)
'@ | python -" in C:\Users\mikew\source\repos\TAOM
 succeeded in 472ms:
missing 0

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Recurse -File C:\\Users\\mikew\\source\\repos\\LOTRAOM\\Main\\Features\\StartingEquipmentGold | Select-Object FullName; foreach ("'$f in Get-ChildItem -Recurse -File C:'"\\Users\\mikew\\source\\repos\\LOTRAOM\\Main\\Features\\StartingEquipmentGold) { \"===== "'$($f.FullName)"; Get-Content $f.FullName }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 472ms:

[32;1mFullName[0m
[32;1m--------[0m
C:\Users\mikew\source\repos\LOTRAOM\Main\Features\StartingEquipmentGold\StartEquipmentConfig.cs
C:\Users\mikew\source\repos\LOTRAOM\Main\Features\StartingEquipmentGold\StartEquipmentHook.cs
C:\Users\mikew\source\repos\LOTRAOM\Main\Features\StartingEquipmentGold\StartingEquipmentIoC.cs
C:\Users\mikew\source\repos\LOTRAOM\Main\Features\StartingEquipmentGold\StartItemConfig.cs
C:\Users\mikew\source\repos\LOTRAOM\Main\Features\StartingEquipmentGold\StartItemsHook.cs
C:\Users\mikew\source\repos\LOTRAOM\Main\Features\StartingEquipmentGold\YouthType.cs
C:\Users\mikew\source\repos\LOTRAOM\Main\Features\StartingEquipmentGold\Hooks\IStartEquipment.cs
C:\Users\mikew\source\repos\LOTRAOM\Main\Features\StartingEquipmentGold\Hooks\IStartItems.cs
===== C:\Users\mikew\source\repos\LOTRAOM\Main\Features\StartingEquipmentGold\StartEquipmentConfig.cs
using System.Collections.Generic;
using LOTRAOM.Core.Domain.Constants;

namespace LOTRAOM.Features.StartingEquipmentGold;

public class StartEquipmentConfig
{
    public static readonly string DefaultStartEquipment = "spc_wanderer_vlandia_0";

    public static readonly Dictionary<string, Dictionary<YouthType, string>> MainHeroStartingEquipment = new()
    {
        [CultureConstants.EreborCulture] = new Dictionary<YouthType, string>
        {
            [YouthType.Default] = "erebor_cc_template_default",
            [YouthType.Cavalry] = "erebor_cc_template_polearm",
            [YouthType.Garrison] = "erebor_cc_template_crossbow",
            [YouthType.Skirmisher] = "erebor_cc_template_bow",
            [YouthType.OtherOutrider] = "erebor_cc_template_polearm",
            [YouthType.Infantry] = "erebor_cc_template_2h",
            [YouthType.Camper] = "erebor_cc_template_default",
            [YouthType.YouthCommander] = "erebor_cc_template_default",
            [YouthType.Groom] = "erebor_cc_template_default",
            [YouthType.HearthGuard] = "erebor_cc_template_default",
            [YouthType.Chieftain] = "erebor_cc_template_default",
            [YouthType.Outrider] = "erebor_cc_template_default"
        },
        [CultureConstants.IsengardCulture] = new Dictionary<YouthType, string>
        {
            [YouthType.Default] = "isengard_cc_template_default",
            [YouthType.Cavalry] = "isengard_cc_template_default",
            [YouthType.Garrison] = "isengard_cc_template_default",
            [YouthType.Skirmisher] = "isengard_cc_template_default",
            [YouthType.OtherOutrider] = "isengard_cc_template_default",
            [YouthType.Infantry] = "isengard_cc_template_default",
            [YouthType.Camper] = "isengard_cc_template_default",
            [YouthType.YouthCommander] = "isengard_cc_template_default",
            [YouthType.Groom] = "isengard_cc_template_default",
            [YouthType.HearthGuard] = "isengard_cc_template_default",
            [YouthType.Chieftain] = "isengard_cc_template_default",
            [YouthType.Outrider] = "isengard_cc_template_default"
        },
        [CultureConstants.MirkwoodCulture] = new Dictionary<YouthType, string>
        {
            [YouthType.Default] = "mirkwood_cc_template_default",
            [YouthType.Cavalry] = "mirkwood_cc_template_default",
            [YouthType.Garrison] = "mirkwood_cc_template_default_bow",
            [YouthType.Skirmisher] = "mirkwood_cc_template_default_bow",
            [YouthType.OtherOutrider] = "mirkwood_cc_template_default_bow",
            [YouthType.Infantry] = "mirkwood_cc_template_default",
            [YouthType.Camper] = "mirkwood_cc_template_default",
            [YouthType.YouthCommander] = "mirkwood_cc_template_default",
            [YouthType.Groom] = "mirkwood_cc_template_default",
            [YouthType.HearthGuard] = "mirkwood_cc_template_default",
            [YouthType.Chieftain] = "mirkwood_cc_template_default",
            [YouthType.Outrider] = "mirkwood_cc_template_default"
        },
        [CultureConstants.LothlorienCulture] = new Dictionary<YouthType, string>
        {
            [YouthType.Default] = "lothlorien_cc_template_default",
            [YouthType.Cavalry] = "lothlorien_cc_template_default",
            [YouthType.Garrison] = "rivendell_cc_template_default_bow",
            [YouthType.Skirmisher] = "rivendell_cc_template_default_bow",
            [YouthType.OtherOutrider] = "rivendell_cc_template_default_bow",
            [YouthType.Infantry] = "lothlorien_cc_template_default",
            [YouthType.Camper] = "lothlorien_cc_template_default",
            [YouthType.YouthCommander] = "lothlorien_cc_template_default",
            [YouthType.Groom] = "lothlorien_cc_template_default",
            [YouthType.HearthGuard] = "lothlorien_cc_template_default",
            [YouthType.Chieftain] = "lothlorien_cc_template_default",
            [YouthType.Outrider] = "lothlorien_cc_template_default"
        },
        [CultureConstants.RivendellCulture] = new Dictionary<YouthType, string>
        {
            [YouthType.Default] = "rivendell_cc_template_default",
            [YouthType.Cavalry] = "rivendell_cc_template_default",
            [YouthType.Garrison] = "rivendell_cc_template_default_bow",
            [YouthType.Skirmisher] = "rivendell_cc_template_default_bow",
            [YouthType.OtherOutrider] = "rivendell_cc_template_default_bow",
            [YouthType.Infantry] = "rivendell_cc_template_default",
            [YouthType.Camper] = "rivendell_cc_template_default",
            [YouthType.YouthCommander] = "rivendell_cc_template_default",
            [YouthType.Groom] = "rivendell_cc_template_default",
            [YouthType.HearthGuard] = "rivendell_cc_template_default",
            [YouthType.Chieftain] = "rivendell_cc_template_default",
            [YouthType.Outrider] = "rivendell_cc_template_default"
        },

        [CultureConstants.MordorCulture] = new Dictionary<YouthType, string>
        {
            [YouthType.Default] = "mordor_cc_template_default",
            [YouthType.Cavalry] = "mordor_cc_template_default",
            [YouthType.Garrison] = "mordor_cc_template_default",
            [YouthType.Skirmisher] = "mordor_cc_template_default",
            [YouthType.OtherOutrider] = "mordor_cc_template_default",
            [YouthType.Infantry] = "mordor_cc_template_default",
            [YouthType.Camper] = "mordor_cc_template_default",
            [YouthType.YouthCommander] = "mordor_cc_template_default",
            [YouthType.Groom] = "mordor_cc_template_default",
            [YouthType.HearthGuard] = "mordor_cc_template_default",
            [YouthType.Chieftain] = "mordor_cc_template_default",
            [YouthType.Outrider] = "mordor_cc_template_default"
        },

        [CultureConstants.DolguldurCulture] = new Dictionary<YouthType, string>
        {
            [YouthType.Default] = "dolguldur_cc_template_default",
            [YouthType.Cavalry] = "dolguldur_cc_template_default_b",
            [YouthType.Garrison] = "dolguldur_cc_template_default_c",
            [YouthType.Skirmisher] = "dolguldur_cc_template_default",
            [YouthType.OtherOutrider] = "dolguldur_cc_template_default_c",
            [YouthType.Infantry] = "dolguldur_cc_template_default",
            [YouthType.Camper] = "dolguldur_cc_template_default",
            [YouthType.YouthCommander] = "dolguldur_cc_template_default",
            [YouthType.Groom] = "dolguldur_cc_template_default",
            [YouthType.HearthGuard] = "dolguldur_cc_template_default",
            [YouthType.Chieftain] = "dolguldur_cc_template_default",
            [YouthType.Outrider] = "dolguldur_cc_template_default_b"
        },
        [CultureConstants.GundabadCulture] = new Dictionary<YouthType, string>
        {
            [YouthType.Default] = "gundabad_cc_template_default",
            [YouthType.Cavalry] = "gundabad_cc_template_default_c",
            [YouthType.Garrison] = "gundabad_cc_template_default_e",
            [YouthType.Skirmisher] = "gundabad_cc_template_default_d",
            [YouthType.OtherOutrider] = "gundabad_cc_template_default_e",
            [YouthType.Infantry] = "gundabad_cc_template_default_b",
            [YouthType.Camper] = "gundabad_cc_template_default_c",
            [YouthType.YouthCommander] = "gundabad_cc_template_default_d",
            [YouthType.Groom] = "gundabad_cc_template_default_d",
            [YouthType.HearthGuard] = "gundabad_cc_template_default_d",
            [YouthType.Chieftain] = "gundabad_cc_template_default_d",
            [YouthType.Outrider] = "gundabad_cc_template_default_c"
        },
    };
    public static string GetEquipmentConfig(string cultureId, YouthType type)
    {
        if (MainHeroStartingEquipment.TryGetValue(cultureId, out var equipmentDict) && equipmentDict.TryGetValue(type, out var equipment))
            return equipment;

        return DefaultStartEquipment;
    }
}
===== C:\Users\mikew\source\repos\LOTRAOM\Main\Features\StartingEquipmentGold\StartEquipmentHook.cs
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TaleWorlds.CampaignSystem.Extensions;
using LOTRAOM.Features.Logging;
using LOTRAOM.Features.StartingEquipmentGold.Hooks;

namespace LOTRAOM.Features.StartingEquipmentGold;

public class StartEquipmentHook : IStartEquipment
{
    private readonly IModLogger _logger;

    public StartEquipmentHook(IModLogger logger)
    {
        _logger = logger;
    }
    public void ChooseCharacterEquipment(CharacterCreation characterCreation, YouthType startType)
    {
        characterCreation.ClearFaceGenMounts();
        string equipmentId = default;
        MBEquipmentRoster equipmentRoster;
        try
        {
            equipmentId = StartEquipmentConfig.GetEquipmentConfig(Hero.MainHero.Culture.StringId, startType);
            equipmentRoster = MBObjectManager.Instance.GetObject<MBEquipmentRoster>(equipmentId);
            IEnumerable<Equipment> battleEquipments = equipmentRoster.GetBattleEquipments();
            IEnumerable<Equipment> civilianEquipments = equipmentRoster.GetCivilianEquipments();
            Equipment battleEquipment = CharacterObject.PlayerCharacter.IsFemale ? GetFemaleEquipment(battleEquipments) : GetMaleEquipment(battleEquipments);
            Equipment civilianEquipment = CharacterObject.PlayerCharacter.IsFemale ? GetFemaleEquipment(civilianEquipments) : GetMaleEquipment(civilianEquipments);
            if (battleEquipment != null)
            {
                characterCreation.ChangeCharactersEquipment(new List<Equipment> { battleEquipment });
                CharacterObject.PlayerCharacter.FirstBattleEquipment.FillFrom(battleEquipment);
            }
            if (civilianEquipment != null) CharacterObject.PlayerCharacter.FirstCivilianEquipment.FillFrom(civilianEquipment);
        }
        catch
        {
            _logger.LogError($"[StartEquipmentHook] Equipment roster with id '{equipmentId}' not found or invalid.");
        }
    }
    private static Equipment GetMaleEquipment(IEnumerable<Equipment> eq) { return eq.FirstOrDefault(); }
    private static Equipment GetFemaleEquipment(IEnumerable<Equipment> eq) { return eq.LastOrDefault(); }
}
===== C:\Users\mikew\source\repos\LOTRAOM\Main\Features\StartingEquipmentGold\StartingEquipmentIoC.cs
using LOTRAOM.Features.StartingEquipmentGold.Hooks;
﻿using DryIoc;
using LOTRAOM.Core.Hooks;

namespace LOTRAOM.Features.StartingEquipmentGold;

public static class StartingEquipmentIoC
{
    public static void RegisterStartEquipmentFeature(IContainer container)
    {
        container.Register<IStartItems, StartItemsHook>(Reuse.Singleton);
        container.Register<IStartEquipment, StartEquipmentHook>(Reuse.Singleton);
    }
}
===== C:\Users\mikew\source\repos\LOTRAOM\Main\Features\StartingEquipmentGold\StartItemConfig.cs
using System;
using System.Collections.Generic;
using LOTRAOM.Core.Domain.Constants;

namespace LOTRAOM.Features.StartingEquipmentGold;

public class StartItemData
{
    public StartItemData(int gold, List<Tuple<string, int>>? items = null, List<Tuple<string, int>>? troops = null)
    {
        Gold = gold;
        Items = items ?? new();
        Troops = troops ?? new();
    }

    public int Gold { get; set; }
    public List<Tuple<string, int>> Items { get; set; } = new ();
    public List<Tuple<string, int>> Troops { get; set; } = new();
}
public class StartItemConfig
{
    public static readonly StartItemData DefaultStartItems = new(gold: 3000);

    public static readonly Dictionary<string, Dictionary<YouthType, StartItemData>> StartItems = new()
    {
        [CultureConstants.GondorCulture] = new Dictionary<YouthType, StartItemData>
        {
            [YouthType.Default] = new StartItemData(10),
            [YouthType.Garrison] = new StartItemData(3000, new() { }, new() { new("gondor_levyman", 1) })
        },
        [CultureConstants.RohanCulture] = new Dictionary<YouthType, StartItemData>
        {
            [YouthType.Default] = new StartItemData(10, new() { }, new() { new("rohan_edoras_recruit", 1) })
        },
        [CultureConstants.IsengardCulture] = new Dictionary<YouthType, StartItemData>
        {
            [YouthType.Default] = new StartItemData(3000),
            [YouthType.Cavalry] = new StartItemData(3000),
            [YouthType.Garrison] = new StartItemData(3000),
            [YouthType.Skirmisher] = new StartItemData(3000),
            [YouthType.OtherOutrider] = new StartItemData(3000),
            [YouthType.Infantry] = new StartItemData(3000),
            [YouthType.Camper] = new StartItemData(3000),
            [YouthType.YouthCommander] = new StartItemData(3000),
            [YouthType.Groom] = new StartItemData(3000),
            [YouthType.HearthGuard] = new StartItemData(3000),
            [YouthType.Chieftain] = new StartItemData(3000),
            [YouthType.Outrider] = new StartItemData(3000)
        },
        [CultureConstants.MordorCulture] = new Dictionary<YouthType, StartItemData>
        {
            [YouthType.Default] = new StartItemData(3000),
            [YouthType.Cavalry] = new StartItemData(3000),
            [YouthType.Garrison] = new StartItemData(3000),
            [YouthType.Skirmisher] = new StartItemData(3000),
            [YouthType.OtherOutrider] = new StartItemData(3000),
            [YouthType.Infantry] = new StartItemData(3000),
            [YouthType.Camper] = new StartItemData(3000),
            [YouthType.YouthCommander] = new StartItemData(3000),
            [YouthType.Groom] = new StartItemData(3000),
            [YouthType.HearthGuard] = new StartItemData(3000),
            [YouthType.Chieftain] = new StartItemData(3000),
            [YouthType.Outrider] = new StartItemData(3000)
        },
        [CultureConstants.RivendellCulture] = new Dictionary<YouthType, StartItemData>
        {
            [YouthType.Default] = new StartItemData(20000),
            [YouthType.Cavalry] = new StartItemData(20000),
            [YouthType.Garrison] = new StartItemData(20000),
            [YouthType.Skirmisher] = new StartItemData(20000),
            [YouthType.OtherOutrider] = new StartItemData(20000),
            [YouthType.Infantry] = new StartItemData(20000),
            [YouthType.Camper] = new StartItemData(20000),
            [YouthType.YouthCommander] = new StartItemData(20000),
            [YouthType.Groom] = new StartItemData(20000),
            [YouthType.HearthGuard] = new StartItemData(20000),
            [YouthType.Chieftain] = new StartItemData(20000),
            [YouthType.Outrider] = new StartItemData(20000)
        },
        [CultureConstants.LothlorienCulture] = new Dictionary<YouthType, StartItemData>
        {
            [YouthType.Default] = new StartItemData(20000),
            [YouthType.Cavalry] = new StartItemData(20000),
            [YouthType.Garrison] = new StartItemData(20000),
            [YouthType.Skirmisher] = new StartItemData(20000),
            [YouthType.OtherOutrider] = new StartItemData(20000),
            [YouthType.Infantry] = new StartItemData(20000),
            [YouthType.Camper] = new StartItemData(20000),
            [YouthType.YouthCommander] = new StartItemData(20000),
            [YouthType.Groom] = new StartItemData(20000),
            [YouthType.HearthGuard] = new StartItemData(20000),
            [YouthType.Chieftain] = new StartItemData(20000),
            [YouthType.Outrider] = new StartItemData(20000)
        },
        [CultureConstants.MirkwoodCulture] = new Dictionary<YouthType, StartItemData>
        {
            [YouthType.Default] = new StartItemData(25000),
            [YouthType.Cavalry] = new StartItemData(25000),
            [YouthType.Garrison] = new StartItemData(25000),
            [YouthType.Skirmisher] = new StartItemData(25000),
            [YouthType.OtherOutrider] = new StartItemData(25000),
            [YouthType.Infantry] = new StartItemData(25000),
            [YouthType.Camper] = new StartItemData(25000),
            [YouthType.YouthCommander] = new StartItemData(25000),
            [YouthType.Groom] = new StartItemData(25000),
            [YouthType.HearthGuard] = new StartItemData(25000),
            [YouthType.Chieftain] = new StartItemData(25000),
            [YouthType.Outrider] = new StartItemData(25000)
        },
        [CultureConstants.EreborCulture] = new Dictionary<YouthType, StartItemData>
        {
            [YouthType.Default] = new StartItemData(5000),
            [YouthType.Cavalry] = new StartItemData(5000),
            [YouthType.Garrison] = new StartItemData(5000),
            [YouthType.Skirmisher] = new StartItemData(5000),
            [YouthType.OtherOutrider] = new StartItemData(5000),
            [YouthType.Infantry] = new StartItemData(5000),
            [YouthType.Camper] = new StartItemData(5000),
            [YouthType.YouthCommander] = new StartItemData(5000),
            [YouthType.Groom] = new StartItemData(5000),
            [YouthType.HearthGuard] = new StartItemData(5000),
            [YouthType.Chieftain] = new StartItemData(5000),
            [YouthType.Outrider] = new StartItemData(5000)
        },
        [CultureConstants.DolguldurCulture] = new Dictionary<YouthType, StartItemData>
        {
            [YouthType.Default] = new StartItemData(3000),
            [YouthType.Cavalry] = new StartItemData(3000),
            [YouthType.Garrison] = new StartItemData(3000),
            [YouthType.Skirmisher] = new StartItemData(3000),
            [YouthType.OtherOutrider] = new StartItemData(3000),
            [YouthType.Infantry] = new StartItemData(3000),
            [YouthType.Camper] = new StartItemData(3000),
            [YouthType.YouthCommander] = new StartItemData(3000),
            [YouthType.Groom] = new StartItemData(3000),
            [YouthType.HearthGuard] = new StartItemData(3000),
            [YouthType.Chieftain] = new StartItemData(3000),
            [YouthType.Outrider] = new StartItemData(3000)
        },
        [CultureConstants.GundabadCulture] = new Dictionary<YouthType, StartItemData>
        {
            [YouthType.Default] = new StartItemData(3000),
            [YouthType.Cavalry] = new StartItemData(3000),
            [YouthType.Garrison] = new StartItemData(3000),
            [YouthType.Skirmisher] = new StartItemData(3000),
            [YouthType.OtherOutrider] = new StartItemData(3000),
            [YouthType.Infantry] = new StartItemData(3000),
            [YouthType.Camper] = new StartItemData(3000),
            [YouthType.YouthCommander] = new StartItemData(3000),
            [YouthType.Groom] = new StartItemData(3000),
            [YouthType.HearthGuard] = new StartItemData(3000),
            [YouthType.Chieftain] = new StartItemData(3000),
            [YouthType.Outrider] = new StartItemData(3000)
        }
    };
    public static StartItemData GetEquipmentConfig(string cultureId, YouthType type)
    {
        if (StartItems.TryGetValue(cultureId, out var equipmentDict) && equipmentDict.TryGetValue(type, out var equipment))
            return equipment;
        return DefaultStartItems;
    }
}
===== C:\Users\mikew\source\repos\LOTRAOM\Main\Features\StartingEquipmentGold\StartItemsHook.cs
using LOTRAOM.Features.Logging;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using System;
using LOTRAOM.Features.StartingEquipmentGold.Hooks;

namespace LOTRAOM.Features.StartingEquipmentGold;

public class StartItemsHook : IStartItems
{
    private readonly IModLogger _logger;

    public StartItemsHook(IModLogger logger)
    {
        _logger = logger;
    }

    public void GivePlayerStartItems(string cultureId, YouthType youthType)
    {
        StartItemData config = StartItemConfig.GetEquipmentConfig(cultureId, youthType);
        if (config == null)
        {
            _logger.LogError($"[StartItemsHook] Starting items config for culture '{cultureId}' not found.");
            return;
        }
        Hero mainHero = Hero.MainHero;
        GiveGoldAction.ApplyBetweenCharacters(null, mainHero, config.Gold, true);
        foreach (var item in config.Items)
        {
            try
            {
                ItemObject nextItem = MBObjectManager.Instance.GetObject<ItemObject>(item.Item1);
                mainHero.PartyBelongedTo.ItemRoster.AddToCounts(nextItem, item.Item2);

            }
            catch (Exception)
            {
                _logger.LogError($"[StartItemsHook] Item '{item.Item1}' not found in MBObjectManager. Skipping item addition.");
            }
        }
        foreach (var item in config.Troops)
        {
            try
            {
                CharacterObject nextTroop = MBObjectManager.Instance.GetObject<CharacterObject>(item.Item1);
                mainHero.PartyBelongedTo.MemberRoster.AddToCounts(nextTroop, item.Item2);

            }
            catch (Exception)
            {
                _logger.LogError($"[StartItemsHook] Troop '{item.Item1}' not found in MBObjectManager. Skipping troop addition.");
            }
        }
    }
}
===== C:\Users\mikew\source\repos\LOTRAOM\Main\Features\StartingEquipmentGold\YouthType.cs
namespace LOTRAOM.Features.StartingEquipmentGold;

public enum YouthType
{
    Default,
    Cavalry,
    YouthCommander,
    Groom,
    HearthGuard,
    Chieftain,
    Outrider,
    OtherOutrider,
    Infantry,
    Skirmisher,
    Garrison,
    Camper
}
===== C:\Users\mikew\source\repos\LOTRAOM\Main\Features\StartingEquipmentGold\Hooks\IStartEquipment.cs
using LOTRAOM.Features.StartingEquipmentGold;
using TaleWorlds.CampaignSystem.CharacterCreationContent;

namespace LOTRAOM.Features.StartingEquipmentGold.Hooks;

public interface IStartEquipment
{
    void ChooseCharacterEquipment(CharacterCreation characterCreation, YouthType startType);
}
===== C:\Users\mikew\source\repos\LOTRAOM\Main\Features\StartingEquipmentGold\Hooks\IStartItems.cs
using LOTRAOM.Features.StartingEquipmentGold;

namespace LOTRAOM.Features.StartingEquipmentGold.Hooks;

/// <summary>
/// Hook interface for giving player starting items, troops, gold
/// </summary>
public interface IStartItems
{
    void GivePlayerStartItems(string cultureId, YouthType youthType);
}


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Main\\_Module\\ModuleData\\equipmentsets\\taom_char_creation_equipment.xml -Pattern 'id=\"player_char_creation_gondor_retainer_m\"' -Context 0,60" in C:\Users\mikew\source\repos\TAOM
 succeeded in 460ms:

> Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:499:		
[7mid="player_char_creation_gondor_retainer_m"[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:500:		culture="Culture.gondor">[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:501:		<EquipmentSet>[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:502:			<Equipment[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:503:				slot="Item0"[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:504:				id="Item.wm_gondor_sword_a01" />[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:505:			<Equipment[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:506:				slot="Item1"[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:507:				id="Item.gond_shld2" />[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:508:			<Equipment[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:509:				slot="Item2"[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:510:				id="Item.wm_gondor_spear" />[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:511:			<Equipment[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:512:				slot="Body"[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:513:				id="Item.gondor_noble_jerkin_a" />[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:514:			<Equipment[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:515:				slot="Leg"[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:516:				id="Item.sk_gd_ano_boots_a" />[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:517:			<Equipment[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:518:				slot="Horse"[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:519:				id="Item.sumpter_horse" />[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:520:			<Equipment[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:521:				slot="HorseHarness"[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:522:				id="Item.light_harness" />[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:523:		</EquipmentSet>[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:524:		<EquipmentSet[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:525:			civilian="true">[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:526:			<Equipment[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:527:				slot="Item0"[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:528:				id="Item.wm_gondor_sword_a01" />[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:529:			<Equipment[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:530:				slot="Body"[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:531:				id="Item.gondor_noble_coat_b" />[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:532:			<Equipment[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:533:				slot="Cape"[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:534:				id="Item.ithilien_cloak" />[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:535:			<Equipment[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:536:				slot="Leg"[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:537:				id="Item.sk_gd_ano_boots_a" />[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:538:		</EquipmentSet>[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:539:	</EquipmentRoster>[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:540:	<EquipmentRoster[0m
[7m [0m[7m [0m[7m[0mMain\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:541:	[0m[7m	[0m
[7m[0mid="player_char_creation_gondor_retainer_f"[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:542:		culture="Culture.gondor">[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:543:		<EquipmentSet>[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:544:			<Equipment[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:545:				slot="Item0"[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:546:				id="Item.wm_gondor_sword_a01" />[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:547:			<Equipment[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:548:				slot="Item1"[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:549:				id="Item.gond_shld2" />[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:550:			<Equipment[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:551:				slot="Item2"[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:552:				id="Item.wm_gondor_spear" />[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:553:			<Equipment[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:554:				slot="Body"[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:555:				id="Item.gondor_noble_jerkin_a" />[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:556:			<Equipment[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:557:				slot="Leg"[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:558:				id="Item.sk_gd_ano_boots_a" />[0m
[7m[0m  Main\_Module\ModuleData\equipmentsets\taom_char_creation_equipment.xml:559:			<Equipment[0m


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content Main\\Features\\CharacterCreation\\CharacterCreationRegistrationBehavior.cs,Main\\Features\\CharacterCreation\\TaomCharacterCreationContentHandler.cs" in C:\Users\mikew\source\repos\TAOM
 succeeded in 440ms:
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TAOM.Core.Logging;

namespace TAOM.Features.CharacterCreation;

public class CharacterCreationRegistrationBehavior : CampaignBehaviorBase
{
    private const int HandlerPriority = 1050;

    private readonly ICharacterCreationContentService _contentService;
    private readonly IModLogger _logger;

    public CharacterCreationRegistrationBehavior(
        ICharacterCreationContentService contentService,
        IModLogger logger)
    {
        _contentService = contentService;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnCharacterCreationInitializedEvent.AddNonSerializedListener(
            this,
            OnCharacterCreationInitialized);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // No persistent data to sync
    }

    private void OnCharacterCreationInitialized(CharacterCreationManager manager)
    {
        var handler = new TaomCharacterCreationContentHandler(_contentService, _logger);
        manager.RegisterCharacterCreationContentHandler(handler, HandlerPriority);
        _logger.LogInfo($"Registered TAOM character creation handler at priority {HandlerPriority}");
    }
}
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TAOM.Core.Logging;

namespace TAOM.Features.CharacterCreation;

public class TaomCharacterCreationContentHandler : ICharacterCreationContentHandler
{
    private readonly ICharacterCreationContentService _contentService;
    private readonly IModLogger _logger;

    public TaomCharacterCreationContentHandler(
        ICharacterCreationContentService contentService,
        IModLogger logger)
    {
        _contentService = contentService;
        _logger = logger;
    }

    public void InitializeContent(CharacterCreationManager characterCreationManager)
    {
        // SandBox handler (priority 800) runs first and registers vanilla cultures + stages.
        // We do nothing here — all TAOM work happens in AfterInitializeContent.
    }

    public void AfterInitializeContent(CharacterCreationManager characterCreationManager)
    {
        _contentService.RegisterCustomCultures(characterCreationManager);
        _contentService.RegisterNarrativeMenus(characterCreationManager);
        _contentService.RegisterCareerMenu(characterCreationManager);
        _logger.LogInfo("TAOM character creation content initialized");
    }

    public void OnStageCompleted(CharacterCreationStageBase stage)
    {
        // Future: track stage completions if needed
    }

    public void OnCharacterCreationFinalize(CharacterCreationManager characterCreationManager)
    {
        _contentService.OnCharacterCreationFinalize(characterCreationManager);
    }
}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content Main\\Features\\StartupResources\\StartupResourcesBehavior.cs" in C:\Users\mikew\source\repos\TAOM
 succeeded in 417ms:
using System;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.StartupResources;

public class StartupResourcesBehavior : CampaignBehaviorBase
{
    private readonly IStartupGoldService _goldService;
    private readonly IStartupInfluenceService _influenceService;
    private readonly IModLogger _logger;
    private bool _goldDistributed;
    private bool _influenceDistributed;

    public StartupResourcesBehavior(IStartupGoldService goldService, IStartupInfluenceService influenceService, IModLogger logger)
    {
        _goldService = goldService;
        _influenceService = influenceService;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnNewGameCreatedPartialFollowUpEvent.AddNonSerializedListener(
            this, OnNewGameCreatedPartialFollowUp);
    }

    public void OnNewGameCreatedPartialFollowUp(CampaignGameStarter starter, int index)
    {
        if (index != 1) return;

        if (!_goldDistributed)
        {
            try
            {
                _goldService.DistributeStartupGold();
                _goldDistributed = true;
                _logger.LogInfo("[StartupResources] Gold distribution complete");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[StartupResources] Gold distribution failed: {ex.Message}");
            }
        }

        if (!_influenceDistributed)
        {
            try
            {
                _influenceService.DistributeStartupInfluence();
                _influenceDistributed = true;
                _logger.LogInfo("[StartupResources] Influence distribution complete");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[StartupResources] Influence distribution failed: {ex.Message}");
            }
        }
    }

    public override void SyncData(IDataStore dataStore) { }
}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'dotnet test TAOM.Tests --no-restore' in C:\Users\mikew\source\repos\TAOM
2026-05-06T23:11:48.110204Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.4 seconds
Output:
Failed to add 'C:\Users\CodexSandboxOffline\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
System.UnauthorizedAccessException: Access to the path 'C:\Users\CodexSandboxOffline\.dotnet\10.0.107.toolpath.sentinel' is denied.
   at Microsoft.Win32.SafeHandles.SafeFileHandle.CreateFile(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options)
   at Microsoft.Win32.SafeHandles.SafeFileHandle.Open(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.Strategies.OSFileStreamStrategy..ctor(String path, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.Strategies.FileStreamHelpers.ChooseStrategyCore(String path, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.File.Create(String path)
   at Microsoft.Extensions.EnvironmentAbstractions.FileWrapper.CreateEmptyFile(String path)
   at Microsoft.DotNet.Cli.Utils.FileAccessRetrier.RetryOnIOException(Action action)
   at Microsoft.DotNet.Configurer.DotnetFirstTimeUseConfigurer.Configure()
   at Microsoft.DotNet.Cli.Program.ConfigureDotNetForFirstTimeUse(IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel, IAspNetCertificateSentinel aspNetCertificateSentinel, IFileSentinel toolPathSentinel, Boolean isDotnetBeingInvokedFromNativeInstaller, DotnetFirstRunConfiguration dotnetFirstRunConfiguration, IEnvironmentProvider environmentProvider, Dictionary`2 performanceMeasurements, Boolean skipFirstTimeUseCheck)
   at Microsoft.DotNet.Cli.Program.ProcessArgs(String[] args, TimeSpan startupTime)
   at Microsoft.DotNet.Cli.Program.Main(String[] args)

 exited 1 in 446ms:
Failed to add 'C:\Users\CodexSandboxOffline\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
System.UnauthorizedAccessException: Access to the path 'C:\Users\CodexSandboxOffline\.dotnet\10.0.107.toolpath.sentinel' is denied.
   at Microsoft.Win32.SafeHandles.SafeFileHandle.CreateFile(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options)
   at Microsoft.Win32.SafeHandles.SafeFileHandle.Open(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.Strategies.OSFileStreamStrategy..ctor(String path, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.Strategies.FileStreamHelpers.ChooseStrategyCore(String path, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.File.Create(String path)
   at Microsoft.Extensions.EnvironmentAbstractions.FileWrapper.CreateEmptyFile(String path)
   at Microsoft.DotNet.Cli.Utils.FileAccessRetrier.RetryOnIOException(Action action)
   at Microsoft.DotNet.Configurer.DotnetFirstTimeUseConfigurer.Configure()
   at Microsoft.DotNet.Cli.Program.ConfigureDotNetForFirstTimeUse(IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel, IAspNetCertificateSentinel aspNetCertificateSentinel, IFileSentinel toolPathSentinel, Boolean isDotnetBeingInvokedFromNativeInstaller, DotnetFirstRunConfiguration dotnetFirstRunConfiguration, IEnvironmentProvider environmentProvider, Dictionary`2 performanceMeasurements, Boolean skipFirstTimeUseCheck)
   at Microsoft.DotNet.Cli.Program.ProcessArgs(String[] args, TimeSpan startupTime)
   at Microsoft.DotNet.Cli.Program.Main(String[] args)

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$env:DOTNET_CLI_HOME='"'C:\\tmp\\dotnet-cli-home'; "'$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='"'1'; "'$env:DOTNET_NOLOGO='"'1'; dotnet test TAOM.Tests --no-restore" in C:\Users\mikew\source\repos\TAOM
2026-05-06T23:11:52.519739Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.4 seconds
Output:
Failed to add 'C:\tmp\dotnet-cli-home\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
System.UnauthorizedAccessException: Access to the path 'C:\tmp\dotnet-cli-home\.dotnet' is denied.
   at System.IO.FileSystem.CreateDirectory(String fullPath, Byte[] securityDescriptor)
   at System.IO.Directory.CreateDirectory(String path)
   at Microsoft.Extensions.EnvironmentAbstractions.DirectoryWrapper.CreateDirectory(String path)
   at Microsoft.DotNet.Configurer.FileSystemExtensions.<>c__DisplayClass0_0.<CreateIfNotExists>b__0()
   at Microsoft.DotNet.Cli.Utils.FileAccessRetrier.RetryOnIOException(Action action)
   at Microsoft.DotNet.Configurer.DotnetFirstTimeUseConfigurer.Configure()
   at Microsoft.DotNet.Cli.Program.ConfigureDotNetForFirstTimeUse(IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel, IAspNetCertificateSentinel aspNetCertificateSentinel, IFileSentinel toolPathSentinel, Boolean isDotnetBeingInvokedFromNativeInstaller, DotnetFirstRunConfiguration dotnetFirstRunConfiguration, IEnvironmentProvider environmentProvider, Dictionary`2 performanceMeasurements, Boolean skipFirstTimeUseCheck)
   at Microsoft.DotNet.Cli.Program.ProcessArgs(String[] args, TimeSpan startupTime)
   at Microsoft.DotNet.Cli.Program.Main(String[] args)

 exited 1 in 381ms:
Failed to add 'C:\tmp\dotnet-cli-home\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
System.UnauthorizedAccessException: Access to the path 'C:\tmp\dotnet-cli-home\.dotnet' is denied.
   at System.IO.FileSystem.CreateDirectory(String fullPath, Byte[] securityDescriptor)
   at System.IO.Directory.CreateDirectory(String path)
   at Microsoft.Extensions.EnvironmentAbstractions.DirectoryWrapper.CreateDirectory(String path)
   at Microsoft.DotNet.Configurer.FileSystemExtensions.<>c__DisplayClass0_0.<CreateIfNotExists>b__0()
   at Microsoft.DotNet.Cli.Utils.FileAccessRetrier.RetryOnIOException(Action action)
   at Microsoft.DotNet.Configurer.DotnetFirstTimeUseConfigurer.Configure()
   at Microsoft.DotNet.Cli.Program.ConfigureDotNetForFirstTimeUse(IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel, IAspNetCertificateSentinel aspNetCertificateSentinel, IFileSentinel toolPathSentinel, Boolean isDotnetBeingInvokedFromNativeInstaller, DotnetFirstRunConfiguration dotnetFirstRunConfiguration, IEnvironmentProvider environmentProvider, Dictionary`2 performanceMeasurements, Boolean skipFirstTimeUseCheck)
   at Microsoft.DotNet.Cli.Program.ProcessArgs(String[] args, TimeSpan startupTime)
   at Microsoft.DotNet.Cli.Program.Main(String[] args)

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "New-Item -ItemType Directory -Force -Path C:\\tmp\\dotnet-cli-home | Out-Null; Get-Item C:\\tmp\\dotnet-cli-home | Format-List FullName,Attributes" in C:\Users\mikew\source\repos\TAOM
2026-05-06T23:11:56.828288Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.5 seconds
Output:
[31;1mNew-Item: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mNew-Item -ItemType Directory -Force -Path C:\tmp\dotnet-cli-home[0m | Ou …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\tmp\dotnet-cli-home' is denied.[0m
[31;1mGet-Item: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … p\dotnet-cli-home | Out-Null; [36;1mGet-Item C:\tmp\dotnet-cli-home[0m | Forma …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot find path 'C:\tmp\dotnet-cli-home' because it does not exist.[0m

 exited 1 in 518ms:
[31;1mNew-Item: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mNew-Item -ItemType Directory -Force -Path C:\tmp\dotnet-cli-home[0m | Ou …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\tmp\dotnet-cli-home' is denied.[0m
[31;1mGet-Item: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … p\dotnet-cli-home | Out-Null; [36;1mGet-Item C:\tmp\dotnet-cli-home[0m | Forma …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot find path 'C:\tmp\dotnet-cli-home' because it does not exist.[0m

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "New-Item -ItemType Directory -Force -Path .\\.dotnet-cli-home | Out-Null; "'$env:DOTNET_CLI_HOME=(Resolve-Path .'"\\.dotnet-cli-home).Path; "'$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='"'1'; "'$env:DOTNET_NOLOGO='"'1'; dotnet test TAOM.Tests --no-restore" in C:\Users\mikew\source\repos\TAOM
2026-05-06T23:12:01.518451Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.8 seconds
Output:
Failed to add 'C:\Users\mikew\source\repos\TAOM\.dotnet-cli-home\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
C:\Program Files\dotnet\sdk\10.0.107\Microsoft.Common.CurrentVersion.targets(93,5): error MSB4184: The expression "[Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKLocation(Windows, 7.0)" cannot be evaluated. Access to the path 'C:\Users\mikew\AppData\Local\Microsoft SDKs' is denied. [C:\Users\mikew\source\repos\TAOM\TAOM.Tests\TAOM.Tests.csproj]

 exited 1 in 803ms:
Failed to add 'C:\Users\mikew\source\repos\TAOM\.dotnet-cli-home\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
C:\Program Files\dotnet\sdk\10.0.107\Microsoft.Common.CurrentVersion.targets(93,5): error MSB4184: The expression "[Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKLocation(Windows, 7.0)" cannot be evaluated. Access to the path 'C:\Users\mikew\AppData\Local\Microsoft SDKs' is denied. [C:\Users\mikew\source\repos\TAOM\TAOM.Tests\TAOM.Tests.csproj]

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content Main\\Features\\CharacterCreation\\CharacterCreationIoC.cs,Main\\Features\\StartupResources\\StartupResourcesIoC.cs" in C:\Users\mikew\source\repos\TAOM
 succeeded in 425ms:
using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.CharacterCreation;

public static class CharacterCreationIoC
{
    public static void RegisterCharacterCreationFeature(IContainer container)
    {
        container.Register<ICultureCreationDataProvider, CultureCreationDataProvider>(Reuse.Singleton);
        container.Register<ICultureRaceFilterService, CultureRaceFilterService>(Reuse.Singleton);
        container.Register<INarrativeDataProvider, NarrativeDataProvider>(Reuse.Singleton);
        container.Register<IEquipmentRosterProvider, EquipmentRosterProvider>(Reuse.Singleton);
        container.Register<ICareerMenuDataProvider, CareerMenuDataProvider>(Reuse.Singleton);
        container.Register<ICareerMenuService, CareerMenuService>(Reuse.Singleton);
        container.Register<ICharacterCreationContentService, CharacterCreationContentService>(Reuse.Singleton);
        container.Register<INarrativeHorseGuardService, NarrativeHorseGuardService>(Reuse.Singleton);
        container.Register<ICCBodyPropertiesProvider, CCBodyPropertiesProvider>(Reuse.Singleton);
        container.Register<IPlayerBodyPropertiesAdapter, PlayerBodyPropertiesAdapter>(Reuse.Singleton);
        container.Register<ICCBodyPropertiesService, CCBodyPropertiesService>(Reuse.Singleton);
        container.Register<IPlayerEquipmentAdapter, PlayerEquipmentAdapter>(Reuse.Singleton);
        container.Register<IPlayerEquipmentService, PlayerEquipmentService>(Reuse.Singleton);
    }
}
using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.StartupResources;

public static class StartupResourcesIoC
{
    public static void RegisterStartupResourcesFeature(IContainer container)
    {
        container.Register<IStartupHeroAdapter, StartupHeroAdapter>(Reuse.Singleton);
        container.Register<IGoldGiftAdapter, GoldGiftAdapter>(Reuse.Singleton);
        container.Register<IClanStartupAdapter, ClanStartupAdapter>(Reuse.Singleton);
        container.Register<IStartupResourcesConfigProvider, StartupResourcesConfigProvider>(Reuse.Singleton);
        container.Register<IStartupGoldService, StartupGoldService>(Reuse.Singleton);
        container.Register<IStartupInfluenceService, StartupInfluenceService>(Reuse.Singleton);
        container.Register<IPlayerStartupGoldService, PlayerStartupGoldService>(Reuse.Singleton);
    }
}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content Main'"\\IoC.cs | ForEach-Object { if ("'$i -ge 40 -and $i -le 90) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 430ms:
  40: {
  41:     private static IContainer _container;
  42: 
  43:     public static void Configure()
  44:     {
  45:         var container = new Container();
  46: 
  47:         container.RegisterInstance<IContainer>(container);
  48: 
  49:         RegisterCoreServices(container);
  50:         RegisterLoggingServices(container);
  51: 
  52:         HeroRaceIoC.RegisterHeroRaceFeature(container);
  53:         BannerInjectionIoC.RegisterBannerInjectionFeature(container);
  54:         BannerColorPersistenceIoC.RegisterBannerColorPersistenceFeature(container);
  55:         TroopProgressionIoC.RegisterTroopProgressionFeature(container);
  56:         FactionMapIoC.RegisterFactionMapFeature(container);
  57:         CharacterCreationIoC.RegisterCharacterCreationFeature(container);
  58:         InitialChildGenerationIoC.RegisterInitialChildGenerationFeature(container);
  59:         DiplomacyIoC.RegisterDiplomacyFeature(container);
  60:         RaceAgeIoC.RegisterRaceAgeFeature(container);
  61:         ExecutionIoC.RegisterExecutionFeature(container);
  62:         StartupResourcesIoC.RegisterStartupResourcesFeature(container);
  63:         TroopWeightIoC.RegisterTroopWeightFeature(container);
  64:         AdvancedCombatIoC.RegisterAdvancedCombatFeature(container);
  65:         WargIoC.RegisterWargFeature(container);
  66:         SpiderIoC.RegisterSpiderFeature(container);
  67:         CustomBattlesIoC.RegisterCustomBattlesFeature(container);
  68:         BattleBalanceIoC.RegisterBattleBalanceFeature(container);
  69:         MainMenuCustomizerIoC.RegisterMainMenuCustomizerFeature(container);
  70:         ShaderPrecompilationIoC.RegisterShaderPrecompilationFeature(container);
  71:         SiegeDefenseIoC.RegisterSiegeDefenseFeature(container);
  72:         ArmyTargetingIoC.RegisterArmyTargetingFeature(container);
  73:         TimeAccelerationIoC.RegisterTimeAccelerationFeature(container);
  74:         SpecialResourcesIoC.RegisterSpecialResourcesFeature(container);
  75:         CareerSystemIoC.RegisterCareerSystemFeature(container);
  76:         SettlementGuardsIoC.RegisterSettlementGuardsFeature(container);
  77:         NamedCompanionIoC.RegisterNamedCompanionsFeature(container);
  78:         RevoltTuningIoC.RegisterRevoltTuningFeature(container);
  79:         SiegeDismountIoC.RegisterSiegeDismountFeature(container);
  80:         MessengerIoC.RegisterMessengerFeature(container);
  81: 
  82:         _container = container;
  83: 
  84:         // Post-registration initialization
  85:         CareerSystemIoC.InitializeCalculators(container.Resolve<Features.CareerSystem.Mutations.IMutationCalculatorRegistry>());
  86:     }
  87: 
  88:     private static void RegisterCoreServices(IContainer container)
  89:     {
  90:         container.Register<IModulePathAdapter, ModulePathAdapter>(Reuse.Singleton);

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content Main\\IoC.cs | Select-Object -First 40; git diff -- Main/IoC.cs" in C:\Users\mikew\source\repos\TAOM
 succeeded in 469ms:
using DryIoc;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.BannerColorPersistence;
using TAOM.Features.BannerInjection;
using TAOM.Features.HeroRace;
using TAOM.Features.CharacterCreation;
using TAOM.Features.FactionMap;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.Diplomacy;
using TAOM.Features.RaceAge;
using TAOM.Features.Execution;
using TAOM.Features.StartupResources;
using TAOM.Features.TroopProgression;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.CustomBattles;
using TAOM.Features.TroopWeight;
using TAOM.Features.Warg;
using TAOM.Features.Spider;
using TAOM.Features.BattleBalance;
using TAOM.Features.MainMenuCustomizer;
using TAOM.Features.ShaderPrecompilation;
using TAOM.Features.Siege;
using TAOM.Features.ArmyTargeting;
using TAOM.Features.TimeAcceleration;
using TAOM.Features.SpecialResources;
using TAOM.Features.CareerSystem;
using TAOM.Features.SettlementGuards;
using TAOM.Features.NamedCompanions;
using TAOM.Features.RevoltTuning;
using TAOM.Features.SiegeDismount;
using TAOM.Features.Messengers;

namespace TAOM;

public static class IoC
{
diff --git a/Main/IoC.cs b/Main/IoC.cs
index 0913d30..2738e4e 100644
--- a/Main/IoC.cs
+++ b/Main/IoC.cs
@@ -31,6 +31,8 @@ using TAOM.Features.CareerSystem;
 using TAOM.Features.SettlementGuards;
 using TAOM.Features.NamedCompanions;
 using TAOM.Features.RevoltTuning;
+using TAOM.Features.SiegeDismount;
+using TAOM.Features.Messengers;
 
 namespace TAOM;
 
@@ -74,6 +76,8 @@ public static class IoC
         SettlementGuardsIoC.RegisterSettlementGuardsFeature(container);
         NamedCompanionIoC.RegisterNamedCompanionsFeature(container);
         RevoltTuningIoC.RegisterRevoltTuningFeature(container);
+        SiegeDismountIoC.RegisterSiegeDismountFeature(container);
+        MessengerIoC.RegisterMessengerFeature(container);
 
         _container = container;
 

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "git diff -- Main/SubModule.cs | Select-String -Pattern 'Messenger|SiegeDismount' -Context 2" in C:\Users\mikew\source\repos\TAOM
 succeeded in 466ms:

   using TAOM.Features.SettlementGuards.Hooks;
   using TAOM.Features.RevoltTuning;
> +using TAOM.Features.[7mSiegeDismount[0m.Hooks;[0m
[7m[0m   using BehaviorTreeWrapper;[0m
[7m[0m   using TaleWorlds.CampaignSystem.CampaignBehaviors;[0m
           mission.AddMissionBehavior(new WargMissionBehavior());
           mission.AddMissionBehavior(new SpiderMissionBehavior());
> +        mission.AddMissionBehavior(new [7mSiegeDismount[0mMissionBehavior());[0m
[7m[0m   [0m
[7m[0m           var colorStore = IoC.Resolve<IAgentColorStore>();[0m


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content docs\\features\\siege-dismount.md -Head 80; if (Test-Path Main\\_Module\\ModuleData\\messengers) { Get-ChildItem -Recurse Main\\_Module\\ModuleData\\messengers } else { 'no messengers dir' }" in C:\Users\mikew\source\repos\TAOM
 succeeded in 446ms:
# Siege Dismount

## Overview

Auto-handles the player's mount when entering siege missions: removes the horse so the player fights on foot, optionally moves it to inventory, and (in the default mode) restores it automatically when the siege ends.

## Why This Exists

In LOTR-themed sieges (Helm's Deep, Minas Tirith, Erebor's gates) it is jarring for the player to fight on horseback inside a fortress courtyard or up a wall ladder. Vanilla Bannerlord makes you remember to manually un-equip your mount and re-equip after — an immersion break and a chore.

- **Vanilla behavior:** Player keeps mount equipped going into siege; mount may glitch through doors and stairs; player must manually un-equip / re-equip via the inventory screen.
- **TAOM requirement:** Optional auto-dismount with four operating modes, configurable per-player via MCM. Default is "auto-restore on siege end" so the only player-visible change is "no horse during siege."
- **Without this feature:** Players who forget to dismount get the buggy on-horseback siege experience or have to interrupt their battle prep to swap equipment.

## Architecture

### Design Challenge

Three constraints:

1. **Detect a siege accurately** — `Mission.IsSiegeBattle` is the authoritative engine flag, but some custom siege scenes flag inconsistently. Fall back to scene-name keyword match (`siege`, `wall`, `gate`, `assault`, `breach`).
2. **Don't lose modifiers / quality on round-trip** — vanilla `EquipmentElement` carries `ItemModifier` (durability, quality bonus). Phase 1 of this port stores only the item `StringId`, which means modifiers are dropped on auto-remount. Documented limitation; see [Performance](#performance) for the upgrade path.
3. **Preserve toggle parity with original** — the developer's tested module had four modes (Vanilla / KeepOnMap / ToInventory / AutoRemount). Keep the modes verbatim; do not "improve."

### Solution Approach

`SiegeDismountMissionBehavior` is a thin `MissionBehavior` that bridges the engine lifecycle into `ISiegeDismountService`. Mission state is read at the boundary (`Mission.Current.IsSiegeBattle`, `Mission.Current.SceneName`) and passed into the service as primitives — the service is fully unit-testable without a live `Mission`.

The service owns the state machine: capture the player's mount/harness via `IPlayerMountAdapter`, optionally move to inventory via `IPartyMountInventoryAdapter`, then on mission end restore if AutoRemount was elected.

### Component Diagram

```
TaomSettings.cs (MCM groups)
       │
SiegeDismountSettingsProvider (reads MCM)
       │
   SiegeDismountMissionBehavior (engine hook)
       │ delegates to
   SiegeDismountService (core state machine)
       │
       ├── IPlayerMountAdapter ── Hero.MainHero.BattleEquipment
       └── IPartyMountInventoryAdapter ── MobileParty.MainParty.ItemRoster
```

`IMountSnapshot` is an opaque token the service stores between mission start and mission end. The service never sees `EquipmentElement` or `ItemObject` (ADR-007).

## Configuration

### MCM Group: `Battle Tactics / Siege Dismount`

| Setting | Type | Default | Description |
|---|---|---|---|
| `Enable Siege Dismount` | bool | `true` | Master toggle. When off, sieges behave vanilla (mount stays equipped). |
| `Siege Mount Behavior` | int 0–3 | `3` (AutoRemount) | 0=Vanilla, 1=KeepOnMap, 2=ToInventory, 3=AutoRemount |
| `Siege Dismount Debug Mode` | bool | `false` | Show diagnostic `[SiegeDismount]` messages on the in-game HUD. Off = file log only. |

### Behavior Modes

| Mode | What happens | When player wants this |
|---|---|---|
| `Vanilla` | Feature inert. | They like the on-horseback siege experience or are testing collisions. |
| `DismountKeepOnMap` | Mount/harness state captured but equipment is NOT cleared. The horse can spawn nearby. | They want to use the horse on the post-siege map but accept it appearing during the siege. |
| `DismountToInventory` | Mount + harness moved to inventory. NOT restored automatically — player must re-equip manually. | They want fine-grained control. |
| `AutoRemountAfter` (default) | Mount + harness moved to inventory at mission start, restored to slots 10/11 at mission end. | Set-and-forget. Recommended. |

## Key Files

| File | Purpose |
|------|---------|
| [Main/Features/SiegeDismount/SiegeDismountService.cs](../../Main/Features/SiegeDismount/SiegeDismountService.cs) | State-machine logic; owns `_capturedSnapshot` + `_pendingRemount` |
| [Main/Features/SiegeDismount/ISiegeDismountService.cs](../../Main/Features/SiegeDismount/ISiegeDismountService.cs) | Service interface |
| [Main/Features/SiegeDismount/SiegeDismountSettingsProvider.cs](../../Main/Features/SiegeDismount/SiegeDismountSettingsProvider.cs) | Wraps `TaomSettings.Instance` for testability |
| [Main/Features/SiegeDismount/Models/SiegeMountBehaviorType.cs](../../Main/Features/SiegeDismount/Models/SiegeMountBehaviorType.cs) | Enum of the four modes |
| [Main/Features/SiegeDismount/Models/IMountSnapshot.cs](../../Main/Features/SiegeDismount/Models/IMountSnapshot.cs) | Opaque token across the service/adapter boundary |
| [Main/Features/SiegeDismount/Hooks/SiegeDismountMissionBehavior.cs](../../Main/Features/SiegeDismount/Hooks/SiegeDismountMissionBehavior.cs) | Thin MissionBehavior; reads `Mission.Current` and delegates |
| [Main/Features/SiegeDismount/SiegeDismountIoC.cs](../../Main/Features/SiegeDismount/SiegeDismountIoC.cs) | DryIoc registrations |
| [Main/Adapters/IPlayerMountAdapter.cs](../../Main/Adapters/IPlayerMountAdapter.cs) | Reads/writes `Hero.MainHero.BattleEquipment[Horse|HorseHarness]` |
| [Main/Adapters/PlayerMountAdapter.cs](../../Main/Adapters/PlayerMountAdapter.cs) | TaleWorlds-side implementation |
| [Main/Adapters/IPartyMountInventoryAdapter.cs](../../Main/Adapters/IPartyMountInventoryAdapter.cs) | Adds/removes items from `MobileParty.MainParty.ItemRoster` |

    Directory: C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\messengers

[32;1mMode   [0m[32;1m              LastWriteTime[0m[32;1m         Length[0m[32;1m Name[0m
[32;1m----   [0m [32;1m             -------------[0m [32;1m        ------[0m [32;1m----[0m
-a---            5/6/2026  6:10 PM             69 messenger_config.json


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "git status --short --untracked-files=all | Select-String -Pattern 'messenger|Siege|Mount|PlayerEquipment|StartupGold|ModuleData'" in C:\Users\mikew\source\repos\TAOM
 succeeded in 489ms:

 M Main/_Module/[7mModuleData[0m/Languages/PL/language_data.xml
 M Main/_Module/[7mModuleData[0m/Languages/SP/language_data.xml
 M Main/_Module/[7mModuleData[0m/charactercreation/youth_menu.json
 M Main/_Module/[7mModuleData[0m/startup_resources/startup_resources_config.xml
?? Main/Adapters/IParty[7mMount[0mInventoryAdapter.cs
?? Main/Adapters/I[7mPlayerEquipment[0mAdapter.cs
?? Main/Adapters/IPlayer[7mMount[0mAdapter.cs
?? Main/Adapters/Party[7mMount[0mInventoryAdapter.cs
?? Main/Adapters/[7mPlayerEquipment[0mAdapter.cs
?? Main/Adapters/Player[7mMount[0mAdapter.cs
?? Main/Features/CharacterCreation/I[7mPlayerEquipment[0mService.cs
?? Main/Features/CharacterCreation/[7mPlayerEquipment[0mRosterIds.cs
?? Main/Features/CharacterCreation/[7mPlayerEquipment[0mService.cs
?? Main/Features/[7mMessenger[0ms/Domain/HeroSnapshot.cs
?? Main/Features/[7mMessenger[0ms/Domain/MessengerValidationResult.cs
?? Main/Features/[7mMessenger[0ms/Domain/PendingMessenger.cs
?? Main/Features/[7mMessenger[0ms/Domain/PositionUpdate.cs
?? Main/Features/[7mMessenger[0ms/IMessengerConfigProvider.cs
?? Main/Features/[7mMessenger[0ms/IMessengerRandomSource.cs
?? Main/Features/[7mMessenger[0ms/IMessengerService.cs
?? Main/Features/[7mMessenger[0ms/IMessengerSettingsProvider.cs
?? Main/Features/[7mMessenger[0ms/IMessengerStateStore.cs
?? Main/Features/[7mMessenger[0ms/MessengerCampaignBehavior.cs
?? Main/Features/[7mMessenger[0ms/MessengerConfig.cs
?? Main/Features/[7mMessenger[0ms/MessengerConfigProvider.cs
?? Main/Features/[7mMessenger[0ms/MessengerIoC.cs
?? Main/Features/[7mMessenger[0ms/MessengerRandomSource.cs
?? Main/Features/[7mMessenger[0ms/MessengerService.cs
?? Main/Features/[7mMessenger[0ms/MessengerSettingsProvider.cs
?? Main/Features/[7mMessenger[0ms/MessengerStateStore.cs
?? Main/Features/[7mMessenger[0ms/UI/MessengerEncyclopediaMixin.cs
?? Main/Features/[7mMessenger[0ms/UI/MessengerEncyclopediaPrefabExtension.cs
?? Main/Features/[7mSiege[0mDismount/Hooks/SiegeDismountMissionBehavior.cs
?? Main/Features/[7mSiege[0mDismount/ISiegeDismountService.cs
?? Main/Features/[7mSiege[0mDismount/ISiegeDismountSettingsProvider.cs
?? Main/Features/[7mSiege[0mDismount/Models/IMountSnapshot.cs
?? Main/Features/[7mSiege[0mDismount/Models/MountSnapshot.cs
?? Main/Features/[7mSiege[0mDismount/Models/SiegeMountBehaviorType.cs
?? Main/Features/[7mSiege[0mDismount/SiegeDismountIoC.cs
?? Main/Features/[7mSiege[0mDismount/SiegeDismountService.cs
?? Main/Features/[7mSiege[0mDismount/SiegeDismountSettingsProvider.cs
?? Main/Features/StartupResources/IPlayer[7mStartupGold[0mService.cs
?? Main/Features/StartupResources/Player[7mStartupGold[0mService.cs
?? Main/_Module/[7mModuleData[0m/Languages/PL/std_taom_messenger_strings_pol-PL.xml
?? Main/_Module/[7mModuleData[0m/Languages/SP/std_taom_messenger_strings_spa-LA.xml
?? Main/_Module/[7mModuleData[0m/messengers/messenger_config.json
?? Main/_Module/[7mModuleData[0m/taom_messenger_strings.xml
?? TAOM.Tests/Features/CharacterCreation/[7mPlayerEquipment[0mServiceTests.cs
?? TAOM.Tests/Features/[7mMessenger[0ms/MessengerConfigProviderTests.cs
?? TAOM.Tests/Features/[7mMessenger[0ms/MessengerServiceTests.cs
?? TAOM.Tests/Features/[7mMessenger[0ms/MessengerStateStoreTests.cs
?? TAOM.Tests/Features/[7mSiege[0mDismount/SiegeDismountServiceTests.cs
?? TAOM.Tests/Features/StartupResources/Player[7mStartupGold[0mServiceTests.cs
?? docs/features/[7msiege[0m-dismount.md
?? docs/reviews/codex-prompt-[7msiege[0mdismount-2026-05-06.md


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git status --short --untracked-files=all' in C:\Users\mikew\source\repos\TAOM
 succeeded in 330ms:
 M CHANGELOG.md
 M Main/Features/CharacterCreation/CareerMenuService.cs
 M Main/Features/CharacterCreation/CharacterCreationContentService.cs
 M Main/Features/CharacterCreation/CharacterCreationIoC.cs
 M Main/Features/CharacterCreation/NarrativeMenuBuilder.cs
 M Main/Features/StartupResources/Config/StartupResourcesConfig.cs
 M Main/Features/StartupResources/StartupResourcesConfigProvider.cs
 M Main/Features/StartupResources/StartupResourcesIoC.cs
 M Main/Features/TaomSettings.cs
 M Main/IoC.cs
 M Main/SubModule.cs
 M Main/_Module/ModuleData/Languages/PL/language_data.xml
 M Main/_Module/ModuleData/Languages/SP/language_data.xml
 M Main/_Module/ModuleData/charactercreation/youth_menu.json
 M Main/_Module/ModuleData/startup_resources/startup_resources_config.xml
 M Main/_Module/SubModule.xml
 M TAOM.Tests/Features/CharacterCreation/CharacterCreationContentServiceTests.cs
 M TAOM.Tests/Features/StartupResources/StartupResourcesConfigProviderTests.cs
 M docs/features/startup-resources.md
?? .dotnet-cli-home/.dotnet/.workloadAdvertisingManifestSentinel10.0.100
?? .dotnet-cli-home/.dotnet/10.0.107.aspNetCertificateSentinel
?? .dotnet-cli-home/.dotnet/10.0.107.dotnetFirstUseSentinel
?? .dotnet-cli-home/.dotnet/10.0.107.toolpath.sentinel
?? Main/Adapters/IPartyMountInventoryAdapter.cs
?? Main/Adapters/IPlayerEquipmentAdapter.cs
?? Main/Adapters/IPlayerMountAdapter.cs
?? Main/Adapters/PartyMountInventoryAdapter.cs
?? Main/Adapters/PlayerEquipmentAdapter.cs
?? Main/Adapters/PlayerMountAdapter.cs
?? Main/Features/CharacterCreation/IPlayerEquipmentService.cs
?? Main/Features/CharacterCreation/PlayerEquipmentRosterIds.cs
?? Main/Features/CharacterCreation/PlayerEquipmentService.cs
?? Main/Features/Messengers/Domain/HeroSnapshot.cs
?? Main/Features/Messengers/Domain/MessengerValidationResult.cs
?? Main/Features/Messengers/Domain/PendingMessenger.cs
?? Main/Features/Messengers/Domain/PositionUpdate.cs
?? Main/Features/Messengers/IMessengerConfigProvider.cs
?? Main/Features/Messengers/IMessengerRandomSource.cs
?? Main/Features/Messengers/IMessengerService.cs
?? Main/Features/Messengers/IMessengerSettingsProvider.cs
?? Main/Features/Messengers/IMessengerStateStore.cs
?? Main/Features/Messengers/MessengerCampaignBehavior.cs
?? Main/Features/Messengers/MessengerConfig.cs
?? Main/Features/Messengers/MessengerConfigProvider.cs
?? Main/Features/Messengers/MessengerIoC.cs
?? Main/Features/Messengers/MessengerRandomSource.cs
?? Main/Features/Messengers/MessengerService.cs
?? Main/Features/Messengers/MessengerSettingsProvider.cs
?? Main/Features/Messengers/MessengerStateStore.cs
?? Main/Features/Messengers/UI/MessengerEncyclopediaMixin.cs
?? Main/Features/Messengers/UI/MessengerEncyclopediaPrefabExtension.cs
?? Main/Features/SiegeDismount/Hooks/SiegeDismountMissionBehavior.cs
?? Main/Features/SiegeDismount/ISiegeDismountService.cs
?? Main/Features/SiegeDismount/ISiegeDismountSettingsProvider.cs
?? Main/Features/SiegeDismount/Models/IMountSnapshot.cs
?? Main/Features/SiegeDismount/Models/MountSnapshot.cs
?? Main/Features/SiegeDismount/Models/SiegeMountBehaviorType.cs
?? Main/Features/SiegeDismount/SiegeDismountIoC.cs
?? Main/Features/SiegeDismount/SiegeDismountService.cs
?? Main/Features/SiegeDismount/SiegeDismountSettingsProvider.cs
?? Main/Features/StartupResources/IPlayerStartupGoldService.cs
?? Main/Features/StartupResources/PlayerStartupGoldService.cs
?? Main/_Module/ModuleData/Languages/PL/std_taom_messenger_strings_pol-PL.xml
?? Main/_Module/ModuleData/Languages/SP/std_taom_messenger_strings_spa-LA.xml
?? Main/_Module/ModuleData/messengers/messenger_config.json
?? Main/_Module/ModuleData/taom_messenger_strings.xml
?? TAOM.Tests/Features/CharacterCreation/PlayerEquipmentServiceTests.cs
?? TAOM.Tests/Features/Messengers/MessengerConfigProviderTests.cs
?? TAOM.Tests/Features/Messengers/MessengerServiceTests.cs
?? TAOM.Tests/Features/Messengers/MessengerStateStoreTests.cs
?? TAOM.Tests/Features/SiegeDismount/SiegeDismountServiceTests.cs
?? TAOM.Tests/Features/StartupResources/PlayerStartupGoldServiceTests.cs
?? docs/features/siege-dismount.md
?? docs/reviews/codex-prompt-player-startup.md
?? docs/reviews/codex-prompt-siegedismount-2026-05-06.md
?? docs/reviews/codex-result-player-startup.md

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content Main\\Features\\Messengers\\UI\\MessengerEncyclopediaMixin.cs,Main\\Features\\Messengers\\UI\\MessengerEncyclopediaPrefabExtension.cs; git diff -- Main/_Module/SubModule.xml Main/_Module/ModuleData/Languages/PL/language_data.xml Main/_Module/ModuleData/Languages/SP/language_data.xml; Get-Content Main\\_Module\\ModuleData\\messengers\\messenger_config.json; Get-Content Main\\_Module\\ModuleData\\taom_messenger_strings.xml -Head 120" in C:\Users\mikew\source\repos\TAOM
 succeeded in 470ms:
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.Messengers.UI;

[ViewModelMixin("RefreshValues")]
internal sealed class MessengerEncyclopediaMixin : BaseViewModelMixin<EncyclopediaHeroPageVM>
{
    private readonly IMessengerService _service;
    private readonly IMessengerSettingsProvider _settings;

    private bool _isMessengerAvailable;
    private HintViewModel _sendMessengerHint;
    private string _sendMessengerActionName = "";

    public MessengerEncyclopediaMixin(EncyclopediaHeroPageVM viewModel) : base(viewModel)
    {
        _service = TAOM.IoC.Resolve<IMessengerService>();
        _settings = TAOM.IoC.Resolve<IMessengerSettingsProvider>();
        _sendMessengerHint = new HintViewModel();
        _sendMessengerActionName = new TextObject("{=taom_send_messenger}Send Messenger").ToString();
    }

    public override void OnRefresh()
    {
        try
        {
            UpdateAvailability();
        }
        catch
        {
            // never let UI refresh throw
            IsMessengerAvailable = false;
        }
    }

    private void UpdateAvailability()
    {
        var hero = ResolveHero();
        if (hero == null)
        {
            IsMessengerAvailable = false;
            SendMessengerHint = new HintViewModel(new TextObject("{=taom_messenger_target_unavailable}This person cannot be reached by messenger at this time."));
            return;
        }

        if (!_settings.EnableMessengers)
        {
            IsMessengerAvailable = false;
            SendMessengerHint = new HintViewModel(new TextObject("{=taom_messenger_disabled}The messenger system is disabled in settings."));
            return;
        }

        var behavior = Campaign.Current?.GetCampaignBehavior<MessengerCampaignBehavior>();
        if (behavior == null)
        {
            IsMessengerAvailable = false;
            SendMessengerHint = new HintViewModel(new TextObject("{=taom_messenger_not_available}Messenger system not available."));
            return;
        }

        var canSend = behavior.CanSendMessenger(hero, out var reason);
        IsMessengerAvailable = canSend;
        SendMessengerHint = canSend
            ? new HintViewModel()
            : new HintViewModel(reason);
    }

    [DataSourceMethod]
    public void ExecuteSendMessenger()
    {
        var hero = ResolveHero();
        if (hero == null) return;

        var behavior = Campaign.Current?.GetCampaignBehavior<MessengerCampaignBehavior>();
        behavior?.SendMessenger(hero);
        OnRefresh();
    }

    [DataSourceProperty]
    public string SendMessengerActionName
    {
        get => _sendMessengerActionName;
        set
        {
            if (_sendMessengerActionName != value)
            {
                _sendMessengerActionName = value;
                ViewModel?.OnPropertyChangedWithValue(value, nameof(SendMessengerActionName));
            }
        }
    }

    [DataSourceProperty]
    public int SendMessengerCost => _settings.MessengerGoldCost;

    [DataSourceProperty]
    public bool IsMessengerAvailable
    {
        get => _isMessengerAvailable;
        set
        {
            if (_isMessengerAvailable != value)
            {
                _isMessengerAvailable = value;
                ViewModel?.OnPropertyChangedWithValue(value, nameof(IsMessengerAvailable));
            }
        }
    }

    [DataSourceProperty]
    public HintViewModel SendMessengerHint
    {
        get => _sendMessengerHint;
        set
        {
            if (_sendMessengerHint != value)
            {
                _sendMessengerHint = value;
                ViewModel?.OnPropertyChangedWithValue(value, nameof(SendMessengerHint));
            }
        }
    }

    private Hero ResolveHero()
    {
        if (ViewModel == null) return null;
        return ViewModel.Obj as Hero;
    }
}
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace TAOM.Features.Messengers.UI;

[PrefabExtension("EncyclopediaHeroPage",
    "descendant::RichTextWidget[@Text='@InformationText']")]
internal class MessengerEncyclopediaPrefabExtension : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Append;

    [PrefabExtensionXmlDocument]
    public XmlDocument GetDocument()
    {
        var doc = new XmlDocument();
        doc.LoadXml(
            "<ListPanel" +
            " WidthSizePolicy=\"CoverChildren\"" +
            " HeightSizePolicy=\"CoverChildren\"" +
            " HorizontalAlignment=\"Center\"" +
            " VerticalAlignment=\"Center\"" +
            " MarginTop=\"10\"" +
            " LayoutImp.LayoutMethod=\"VerticalBottomToTop\">" +
            "<Children>" +
            "<ButtonWidget" +
            " WidthSizePolicy=\"Fixed\"" +
            " HeightSizePolicy=\"Fixed\"" +
            " SuggestedWidth=\"227\"" +
            " SuggestedHeight=\"40\"" +
            " HorizontalAlignment=\"Center\"" +
            " VerticalAlignment=\"Center\"" +
            " IsEnabled=\"@IsMessengerAvailable\"" +
            " Command.Click=\"ExecuteSendMessenger\"" +
            " DoNotPassEventsToChildren=\"true\"" +
            " Brush=\"Popup.PartySelection.Confirm\">" +
            "<Children>" +
            "<TextWidget" +
            " WidthSizePolicy=\"StretchToParent\"" +
            " HeightSizePolicy=\"StretchToParent\"" +
            " Text=\"@SendMessengerActionName\"" +
            " Brush=\"Popup.PartySelection.Button.Text\"" +
            " VerticalAlignment=\"Center\"" +
            " HorizontalAlignment=\"Center\" />" +
            "<HintWidget" +
            " WidthSizePolicy=\"StretchToParent\"" +
            " HeightSizePolicy=\"StretchToParent\"" +
            " DataSource=\"{SendMessengerHint}\"" +
            " Command.HoverBegin=\"ExecuteBeginHint\"" +
            " Command.HoverEnd=\"ExecuteEndHint\"" +
            " IsDisabled=\"true\" />" +
            "</Children>" +
            "</ButtonWidget>" +
            "</Children>" +
            "</ListPanel>");
        return doc;
    }
}
diff --git a/Main/_Module/ModuleData/Languages/PL/language_data.xml b/Main/_Module/ModuleData/Languages/PL/language_data.xml
index 02d7975..5e7a0df 100644
--- a/Main/_Module/ModuleData/Languages/PL/language_data.xml
+++ b/Main/_Module/ModuleData/Languages/PL/language_data.xml
@@ -5,4 +5,5 @@
   <LanguageFile xml_path="PL/std_taom_named_companion_strings_pol-PL.xml" />
   <LanguageFile xml_path="PL/std_taom_cc_strings_pol-PL.xml" />
   <LanguageFile xml_path="PL/std_taom_career_strings_pol-PL.xml" />
+  <LanguageFile xml_path="PL/std_taom_messenger_strings_pol-PL.xml" />
 </LanguageData>
diff --git a/Main/_Module/ModuleData/Languages/SP/language_data.xml b/Main/_Module/ModuleData/Languages/SP/language_data.xml
index dbfceae..5acac02 100644
--- a/Main/_Module/ModuleData/Languages/SP/language_data.xml
+++ b/Main/_Module/ModuleData/Languages/SP/language_data.xml
@@ -5,4 +5,5 @@
   <LanguageFile xml_path="SP/std_taom_named_companion_strings_spa-LA.xml" />
   <LanguageFile xml_path="SP/std_taom_cc_strings_spa-LA.xml" />
   <LanguageFile xml_path="SP/std_taom_career_strings_spa-LA.xml" />
+  <LanguageFile xml_path="SP/std_taom_messenger_strings_spa-LA.xml" />
 </LanguageData>
diff --git a/Main/_Module/SubModule.xml b/Main/_Module/SubModule.xml
index de9d93b..fe792cd 100644
--- a/Main/_Module/SubModule.xml
+++ b/Main/_Module/SubModule.xml
@@ -711,6 +711,15 @@
 				<GameType value="EditorGame"/>
 			</IncludedGameTypes>
 		</XmlNode>
+		<XmlNode>
+			<XmlName id="GameText" path="taom_messenger_strings"/>
+			<IncludedGameTypes>
+				<GameType value="Campaign"/>
+				<GameType value="CampaignStoryMode"/>
+				<GameType value="CustomGame"/>
+				<GameType value="EditorGame"/>
+			</IncludedGameTypes>
+		</XmlNode>
 		<XmlNode>
 			<XmlName id="BannerIcons" path="banner_icons"/>
 		</XmlNode>
{
  "accidentChancePerHour": 0.002,
  "travelSpeedMultiplier": 1.0
}
<?xml version="1.0" encoding="utf-8"?>
<strings>

	<!-- Encyclopedia button + hints -->
	<string id="taom_send_messenger" text="{=taom_send_messenger}Send Messenger" />
	<string id="taom_messenger_target_unavailable" text="{=taom_messenger_target_unavailable}This person cannot be reached by messenger at this time." />
	<string id="taom_messenger_disabled" text="{=taom_messenger_disabled}The messenger system is disabled in settings." />
	<string id="taom_messenger_not_available" text="{=taom_messenger_not_available}Messenger system not available." />
	<string id="taom_messenger_invalid_target" text="{=taom_messenger_invalid_target}Invalid target." />

	<!-- Send-side notifications -->
	<string id="taom_messenger_cannot_send" text="{=taom_messenger_cannot_send}Cannot Send Messenger" />
	<string id="taom_messenger_no_gold" text="{=taom_messenger_no_gold}Not enough gold! You need {COST} denars." />
	<string id="taom_messenger_already_sent" text="{=taom_messenger_already_sent}A messenger has already been dispatched to this person." />
	<string id="taom_messenger_hero_dead" text="{=taom_messenger_hero_dead}{HERO_NAME} is dead." />
	<string id="taom_messenger_hero_prisoner" text="{=taom_messenger_hero_prisoner}{HERO_NAME} is imprisoned and cannot receive messengers." />
	<string id="taom_messenger_hero_fugitive" text="{=taom_messenger_hero_fugitive}{HERO_NAME} is a fugitive and cannot be found." />
	<string id="taom_messenger_hero_child" text="{=taom_messenger_hero_child}{HERO_NAME} is too young to receive messengers." />
	<string id="taom_messenger_confused" text="{=taom_messenger_confused}The messenger seems confused — they were trying to reach someone in your own party!" />
	<string id="taom_messenger_sent" text="{=taom_messenger_sent}A messenger has been dispatched to {HERO_NAME} and will arrive within {DAYS} days." />
	<string id="taom_messenger_sent_title" text="{=taom_messenger_sent_title}Messenger Sent" />

	<!-- Arrival -->
	<string id="taom_messenger_arrived" text="{=taom_messenger_arrived}A messenger from {HERO_NAME} has arrived. Do you wish to speak with them?" />
	<string id="taom_messenger_arrived_title" text="{=taom_messenger_arrived_title}Messenger Arrived" />
	<string id="taom_messenger_speak" text="{=taom_messenger_speak}Speak" />
	<string id="taom_messenger_dismiss" text="{=taom_messenger_dismiss}Dismiss" />

	<!-- Accident / error -->
	<string id="taom_messenger_lost" text="{=taom_messenger_lost}Your messenger to {HERO_NAME} was ambushed by bandits and never arrived." />
	<string id="taom_messenger_lost_title" text="{=taom_messenger_lost_title}Messenger Lost" />
	<string id="taom_messenger_error" text="{=taom_messenger_error}Messenger Error" />

	<!-- In-conversation dialog hook -->
	<string id="taom_messenger_dialog_init" text="{=taom_messenger_dialog_init}I may need to send you a message later. Can I dispatch a messenger to you?" />
	<string id="taom_messenger_dialog_npc_accept" text="{=taom_messenger_dialog_npc_accept}Of course. I'll make sure to receive your messenger when they arrive." />
	<string id="taom_messenger_dialog_send" text="{=taom_messenger_dialog_send}Send the messenger ({COST} denars)" />
	<string id="taom_messenger_dialog_decline" text="{=taom_messenger_dialog_decline}On second thought, never mind." />
	<string id="taom_messenger_dialog_decline_ack" text="{=taom_messenger_dialog_decline_ack}As you wish." />
	<string id="taom_messenger_dialog_sent" text="{=taom_messenger_dialog_sent}I'll be expecting your messenger then." />

</strings>
warning: in the working copy of 'Main/_Module/ModuleData/Languages/PL/language_data.xml', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Main/_Module/ModuleData/Languages/SP/language_data.xml', LF will be replaced by CRLF the next time Git touches it

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Main\\TAOM.csproj,Main\\SubModule.cs -Pattern 'UIExtender|Register.*Prefab|ViewModelMixin|UIPrefab|UIExtenderEx' -Context 2" in C:\Users\mikew\source\repos\TAOM
 succeeded in 463ms:

  Main\TAOM.csproj:40:			<Private>False</Private>
  Main\TAOM.csproj:41:		</Reference>
> Main\TAOM.csproj:42:		<Reference 
Include="$(GameFolder)\Modules\Bannerlord.[7mUIExtender[0mEx\bin\$(GameBinariesFolder)\Bannerlord.UIExtenderEx.dll">[0m
[7m[0m  Main\TAOM.csproj:43:			<HintPath>%(Identity)</HintPath>[0m
[7m[0m  Main\TAOM.csproj:44:			<Private>False</Private>[0m
> Main\SubModule.cs:1:using Bannerlord.[7mUIExtender[0mEx;[0m
[7m[0m  Main\SubModule.cs:2:using HarmonyLib;[0m
[7m[0m  Main\SubModule.cs:3:using TaleWorlds.CampaignSystem;[0m
  Main\SubModule.cs:70:{
  Main\SubModule.cs:71:    private Harmony _harmony;
> Main\SubModule.cs:72:    private [7mUIExtender[0m? _uiExtender;[0m
[7m[0m  Main\SubModule.cs:73:    private ITimeAccelerationService? _timeAccelerationService;[0m
[7m[0m  Main\SubModule.cs:74:    private static float _shaderTickAccumulator;[0m
  Main\SubModule.cs:81:        IoC.Configure();
  Main\SubModule.cs:82:
> Main\SubModule.cs:83:        _[7muiExtender[0m = UIExtender.Create("TAOM");[0m
> Main\SubModule.cs:84:        _[7muiExtender[0m.Register(typeof(SubModule).Assembly);
> Main\SubModule.cs:85:        _[7muiExtender[0m.Enable();[0m
[7m[0m  Main\SubModule.cs:86:[0m
[7m[0m  Main\SubModule.cs:87:        _timeAccelerationService = IoC.Resolve<ITimeAccelerationService>();[0m


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.ViewModelCollection.dll'; if (Test-Path "'$dll) { ilspycmd -t TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages.EncyclopediaHeroPageVM $dll | Select-String -Pattern '"'Obj|class EncyclopediaHeroPageVM|Hero' -Context 2 } else { Get-ChildItem 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client' -Filter '*ViewModel*.dll' | Select FullName }" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1181ms:

  namespace TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
  
> [EncyclopediaViewModel(typeof([7mHero[0m))][0m
> public [7mclass EncyclopediaHeroPageVM[0m : EncyclopediaContentPageVM[0m
[7m[0m  {[0m
> 	private readonly [7mHero[0m _hero;[0m
[7m[0m  [0m
> 	private readonly Text[7mObj[0mect _infoHiddenReasonText;[0m
[7m[0m  [0m
> 	private List<[7mHero[0m> _allRelatedHeroes;[0m
[7m[0m  [0m
> 	private readonly [7mHero[0mRelationComparer _relationAscendingComparer;[0m
[7m[0m  [0m
> 	private readonly [7mHero[0mRelationComparer _relationDescendingComparer;[0m
[7m[0m  [0m
[7m[0m  	private const int _alliesEnemiesCapacity = 13;[0m
  
> 	private MBBindingList<[7mHero[0mVM> _enemies;[0m
[7m[0m  [0m
> 	private MBBindingList<[7mHero[0mVM> _allies;[0m
[7m[0m  [0m
[7m[0m  	private MBBindingList<EncyclopediaFamilyMemberVM> _family;[0m
  
> 	private MBBindingList<[7mHero[0mVM> _companions;[0m
[7m[0m  [0m
[7m[0m  	private MBBindingList<EncyclopediaSettlementVM> _settlements;[0m
  	private string _familyText;
  
> 	private [7mHero[0mViewModel _heroCharacter;[0m
[7m[0m  [0m
[7m[0m  	private bool _isCompanion;[0m
  	private bool _isInformationHidden;
  
> 	private [7mHero[0mVM _master;[0m
[7m[0m  [0m
[7m[0m  	private EncyclopediaFactionVM _faction;[0m
  	private bool _hasAnySkills;
  
> 	private MBBindingList<[7mHero[0mVM> _additionalAllies;[0m
[7m[0m  [0m
> 	private MBBindingList<[7mHero[0mVM> _additionalEnemies;[0m
[7m[0m  [0m
[7m[0m  	private bool _anyAdditionalAllies;[0m
  
  	[DataSourceProperty]
> 	public [7mHero[0mVM Master[0m
[7m[0m  	{[0m
[7m[0m  		get[0m
  
  	[DataSourceProperty]
> 	public [7mHero[0mViewModel HeroCharacter[0m
[7m[0m  	{[0m
[7m[0m  		get[0m
  		{
> 			return _[7mhero[0mCharacter;[0m
[7m[0m  		}[0m
[7m[0m  		set[0m
  		{
> 			if (value != _[7mhero[0mCharacter)[0m
[7m[0m  			{[0m
> 				_[7mhero[0mCharacter = value;
> 				OnPropertyChangedWithValue(value, "[7mHero[0mCharacter");[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
  
  	[DataSourceProperty]
> 	public MBBindingList<[7mHero[0mVM> Companions[0m
[7m[0m  	{[0m
[7m[0m  		get[0m
  
  	[DataSourceProperty]
> 	public MBBindingList<[7mHero[0mVM> Enemies[0m
[7m[0m  	{[0m
[7m[0m  		get[0m
  
  	[DataSourceProperty]
> 	public MBBindingList<[7mHero[0mVM> Allies[0m
[7m[0m  	{[0m
[7m[0m  		get[0m
  
  	[DataSourceProperty]
> 	public MBBindingList<[7mHero[0mVM> AdditionalEnemies[0m
[7m[0m  	{[0m
[7m[0m  		get[0m
  
  	[DataSourceProperty]
> 	public MBBindingList<[7mHero[0mVM> AdditionalAllies[0m
[7m[0m  	{[0m
[7m[0m  		get[0m
  	}
  
> 	public Encyclopedia[7mHero[0mPageVM(EncyclopediaPageArgs args)[0m
[7m[0m  		: base(args)[0m
[7m[0m  	{[0m
> 		_[7mhero[0m = base.Obj as Hero;
> 		_relationAscendingComparer = new [7mHero[0mRelationComparer(_hero, isAscending: true, showLeadersFirst: true);
> 		_relationDescendingComparer = new [7mHero[0mRelationComparer(_hero, isAscending: false, showLeadersFirst: true);
> 		IsInformationHidden = CampaignUIHelper.Is[7mHero[0mInformationHidden(_hero, out var disableReason);[0m
[7m[0m  		_infoHiddenReasonText = disableReason;[0m
> 		_allRelated[7mHero[0mes = new List<Hero> { _hero.Father, _hero.Mother, _hero.Spouse };
> 		_allRelated[7mHero[0mes.AddRange(_hero.Siblings);
> 		_allRelated[7mHero[0mes.AddRange(_hero.ExSpouses);
> 		_allRelated[7mHero[0mes.AddRange(CampaignUIHelper.GetChildrenAndGrandchildrenOfHero(_hero));
> 		StringHelpers.SetCharacterProperties("NPC", _[7mhero[0m.CharacterObject);[0m
[7m[0m  		Settlements = new MBBindingList<EncyclopediaSettlementVM>();[0m
[7m[0m  		Dwellings = new MBBindingList<EncyclopediaDwellingVM>();[0m
> 		Allies = new MBBindingList<[7mHero[0mVM>();
> 		AdditionalAllies = new MBBindingList<[7mHero[0mVM>();
> 		Enemies = new MBBindingList<[7mHero[0mVM>();
> 		AdditionalEnemies = new MBBindingList<[7mHero[0mVM>();[0m
[7m[0m  		Family = new MBBindingList<EncyclopediaFamilyMemberVM>();[0m
> 		Companions = new MBBindingList<[7mHero[0mVM>();[0m
[7m[0m  		History = new MBBindingList<EncyclopediaHistoryEventVM>();[0m
[7m[0m  		Skills = new MBBindingList<EncyclopediaSkillVM>();[0m
  		Stats = new MBBindingList<StringPairItemVM>();
  		Traits = new MBBindingList<EncyclopediaTraitItemVM>();
> 		[7mHero[0mCharacter = new HeroViewModel(CharacterViewModel.StanceTypes.EmphasizeFace);[0m
> 		base.IsBookmarked = Campaign.Current.EncyclopediaManager.ViewDataTracker.IsEncyclopediaBookmarked(_[7mhero[0m);
> 		Faction = new EncyclopediaFactionVM(_[7mhero[0m.Clan);[0m
[7m[0m  		RefreshValues();[0m
[7m[0m  	}[0m
  		Stats.Clear();
  		Traits.Clear();
> 		NameText = _[7mhero[0m.Name.ToString();[0m
[7m[0m  		string text = GameTexts.FindText("str_missing_info_indicator").ToString();[0m
> 		EncyclopediaPage pageOf = Campaign.Current.EncyclopediaManager.GetPageOf(typeof([7mHero[0m));
> 		HasNeutralClan = _[7mhero[0m.Clan == null;[0m
[7m[0m  		if (!IsInformationHidden)[0m
[7m[0m  		{[0m
> 			List<Skill[7mObj[0mect> list = TaleWorlds.CampaignSystem.Extensions.Skills.All.ToList();
> 			list.Sort(CampaignUIHelper.Skill[7mObj[0mectComparerInstance);
> 			foreach (Skill[7mObj[0mect item3 in list)[0m
[7m[0m  			{[0m
> 				if (_[7mhero[0m.GetSkillValue(item3) >= 50)[0m
[7m[0m  				{[0m
> 					Skills.Add(new EncyclopediaSkillVM(item3, _[7mhero[0m.GetSkillValue(item3)));[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
> 			foreach (Trait[7mObj[0mect heroTrait in CampaignUIHelper.GetHeroTraits())[0m
[7m[0m  			{[0m
> 				if (_[7mhero[0m.GetTraitLevel(heroTrait) != 0)[0m
[7m[0m  				{[0m
> 					Traits.Add(new EncyclopediaTraitItemVM([7mhero[0mTrait, _hero));[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
> 			if (_[7mhero[0m.Age >= (float)Campaign.Current.Models.AgeModel.HeroComesOfAge)[0m
[7m[0m  			{[0m
> 				for (int i = 0; i < [7mHero[0m.AllAliveHeroes.Count; i++)[0m
[7m[0m  				{[0m
> 					Add[7mHero[0mToRelatedVMList(Hero.AllAliveHeroes[i]);[0m
[7m[0m  				}[0m
> 				for (int j = 0; j < [7mHero[0m.DeadOrDisabledHeroes.Count; j++)[0m
[7m[0m  				{[0m
> 					Add[7mHero[0mToRelatedVMList(Hero.DeadOrDisabledHeroes[j]);[0m
[7m[0m  				}[0m
[7m[0m  				Allies.Sort(_relationDescendingComparer);[0m
  				while (Allies.Count > 13)
  				{
> 					[7mHero[0mVM item = Allies[13];[0m
[7m[0m  					Allies.Remove(item);[0m
[7m[0m  					AdditionalAllies.Add(item);[0m
  				while (Enemies.Count > 13)
  				{
> 					[7mHero[0mVM item2 = Enemies[13];[0m
[7m[0m  					Enemies.Remove(item2);[0m
[7m[0m  					AdditionalEnemies.Add(item2);[0m
  				OnAdditionalListsUpdated();
  			}
> 			if (_[7mhero[0m.Clan != null && _hero == _hero.Clan.Leader)[0m
[7m[0m  			{[0m
> 				for (int k = 0; k < _[7mhero[0m.Clan.Companions.Count; k++)[0m
[7m[0m  				{[0m
> 					[7mHero[0m hero = _hero.Clan.Companions[k];
> 					Companions.Add(new [7mHero[0mVM(hero));[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
> 			for (int l = 0; l < _allRelated[7mHero[0mes.Count; l++)[0m
[7m[0m  			{[0m
> 				[7mHero[0m hero2 = _allRelatedHeroes[l];
> 				if ([7mhero[0m2 != null && pageOf.IsValidEncyclopediaItem(hero2))[0m
[7m[0m  				{[0m
> 					Family.Add(new EncyclopediaFamilyMemberVM([7mhero[0m2, _hero));[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
> 			for (int m = 0; m < _[7mhero[0m.OwnedWorkshops.Count; m++)[0m
[7m[0m  			{[0m
> 				Dwellings.Add(new EncyclopediaDwellingVM(_[7mhero[0m.OwnedWorkshops[m].WorkshopType));[0m
[7m[0m  			}[0m
[7m[0m  			EncyclopediaPage pageOf2 = Campaign.Current.EncyclopediaManager.GetPageOf(typeof(Settlement));[0m
  			{
  				Settlement settlement = Settlement.All[n];
> 				if (settlement.OwnerClan != null && settlement.OwnerClan.Leader == _[7mhero [0m[7m[0m&& [0m
[7m[0mpageOf2.IsValidEncyclopediaItem(settlement))[0m
[7m[0m  				{[0m
[7m[0m  					Settlements.Add(new EncyclopediaSettlementVM(settlement));[0m
  		}
  		HasAnySkills = Skills.Count > 0;
> 		if (_[7mhero[0m.Culture != null)[0m
[7m[0m  		{[0m
[7m[0m  			string definition = GameTexts.FindText("str_enc_sf_culture").ToString();[0m
> 			Stats.Add(new StringPairItemVM(definition, _[7mhero[0m.Culture.Name.ToString()));[0m
[7m[0m  		}[0m
[7m[0m  		string definition2 = GameTexts.FindText("str_enc_sf_age").ToString();[0m
> 		Stats.Add(new StringPairItemVM(definition2, IsInformationHidden ? text : ((int)_[7mhero[0m.Age).ToString()));[0m
[7m[0m  		for (int num = Campaign.Current.LogEntryHistory.GameActionLogs.Count - 1; num >= 0; num--)[0m
[7m[0m  		{[0m
> 			if (Campaign.Current.LogEntryHistory.GameActionLogs[num] is IEncyclopediaLog encyclopediaLog && 
encyclopediaLog.IsVisibleInEncyclopediaPageOf(_[7mhero[0m))[0m
[7m[0m  			{[0m
[7m[0m  				History.Add(new EncyclopediaHistoryEventVM(encyclopediaLog));[0m
  			}
  		}
> 		if (!_[7mhero[0m.IsNotable && !_hero.IsWanderer && _hero.Clan?.Kingdom != null)[0m
[7m[0m  		{[0m
> 			KingdomRankText = CampaignUIHelper.Get[7mHero[0mKingdomRank(_hero);[0m
[7m[0m  		}[0m
> 		string [7mhero[0mOccupationName = CampaignUIHelper.GetHeroOccupationName(_hero);
> 		if (!string.IsNullOrEmpty([7mhero[0mOccupationName))[0m
[7m[0m  		{[0m
[7m[0m  			string definition3 = GameTexts.FindText("str_enc_sf_occupation").ToString();[0m
> 			Stats.Add(new StringPairItemVM(definition3, IsInformationHidden ? text : [7mhero[0mOccupationName));[0m
[7m[0m  		}[0m
> 		if (_[7mhero[0m != Hero.MainHero)[0m
[7m[0m  		{[0m
[7m[0m  			string definition4 = GameTexts.FindText("str_enc_sf_relation").ToString();[0m
> 			Stats.Add(new StringPairItemVM(definition4, IsInformationHidden ? text : 
_[7mhero[0m.GetRelationWithPlayer().ToString()));[0m
[7m[0m  		}[0m
> 		LastSeenText = ((_[7mhero[0m == Hero.MainHero) ? "" : HeroHelper.GetLastSeenText(_hero).ToString());
> 		[7mHero[0mCharacter.FillFrom(_hero, -1, _hero.IsNotable, useCharacteristicIdleAction: true);
> 		[7mHero[0mCharacter.SetEquipment(EquipmentIndex.ArmorItemEndSlot, default(EquipmentElement));
> 		[7mHero[0mCharacter.SetEquipment(EquipmentIndex.HorseHarness, default(EquipmentElement));
> 		[7mHero[0mCharacter.SetEquipment(EquipmentIndex.NumAllWeaponSlots, default(EquipmentElement));
> 		IsCompanion = _[7mhero[0m.CompanionOf != null;[0m
[7m[0m  		if (IsCompanion)[0m
[7m[0m  		{[0m
  			MasterText = GameTexts.FindText("str_companion_of").ToString();
> 			Master = new [7mHero[0mVM(_hero.CompanionOf?.Leader);[0m
[7m[0m  		}[0m
> 		IsPregnant = _[7mhero[0m.IsPregnant;
> 		IsDead = !_[7mhero[0m.IsAlive;[0m
[7m[0m  		base.IsLoadingOver = true;[0m
[7m[0m  	}[0m
  
> 	private void Add[7mHero[0mToRelatedVMList(Hero hero)[0m
[7m[0m  	{[0m
> 		if (Campaign.Current.EncyclopediaManager.GetPageOf(typeof([7mHero[0m)).IsValidEncyclopediaItem(hero) [0m[7m[0m&& [0m[7m[0m!hero.IsNotable [0m
[7m[0m&& [0m[7m[0mhero [0m[7m[0m!= [0m[7m[0m_hero [0m[7m[0m&& [0m[7m[0mhero.IsAlive [0m[7m[0m&& [0m[7m[0mhero.Age [0m[7m[0m>= [0m[7m[0m(float)Campaign.Current.Models.AgeModel.HeroComesOfAge [0m[7m[0m&& [0m
[7m[0m!_allRelatedHeroes.Contains(hero))[0m
[7m[0m  		{[0m
> 			if (_[7mhero[0m.IsFriend(hero))[0m
[7m[0m  			{[0m
> 				Allies.Add(new [7mHero[0mVM(hero));[0m
[7m[0m  			}[0m
> 			else if (_[7mhero[0m.IsEnemy(hero))[0m
[7m[0m  			{[0m
> 				Enemies.Add(new [7mHero[0mVM(hero));[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
  	public override string GetName()
  	{
> 		return _[7mhero[0m.Name.ToString();[0m
[7m[0m  	}[0m
[7m[0m  [0m
  	public override string GetNavigationBarURL()
  	{
> 		return string.Concat(string.Concat(string.Concat(HyperlinkTexts.GetGenericHyperlinkText("Home", 
GameTexts.FindText("str_encyclopedia_home").ToString()) + " \\ ", 
HyperlinkTexts.GetGenericHyperlinkText("ListPage-[7mHero[0mes", [0m[7m[0mGameTexts.FindText("str_encyclopedia_heroes").ToString())), [0m
[7m[0m" [0m[7m[0m\\ [0m[7m[0m"), [0m[7m[0mGetName());[0m
[7m[0m  	}[0m
[7m[0m  [0m
  		if (base.IsBookmarked)
  		{
> 			Campaign.Current.EncyclopediaManager.ViewDataTracker.AddEncyclopediaBookmarkToItem(_[7mhero[0m);[0m
[7m[0m  		}[0m
[7m[0m  		else[0m
  		{
> 			Campaign.Current.EncyclopediaManager.ViewDataTracker.RemoveEncyclopediaBookmarkFromItem(_[7mhero[0m);[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
  	{
  		base.OnFinalize();
> 		[7mHero[0mCharacter.OnFinalize();[0m
[7m[0m  	}[0m
[7m[0m  [0m
  	{
  		InformationText = "";
> 		if (!Text[7mObj[0mect.IsNullOrEmpty(_hero.EncyclopediaText))[0m
[7m[0m  		{[0m
> 			InformationText = _[7mhero[0m.EncyclopediaText.ToString();[0m
[7m[0m  		}[0m
> 		else if (_[7mhero[0m.CharacterObject.Occupation == Occupation.Lord)[0m
[7m[0m  		{[0m
> 			InformationText = [7mHero[0m.SetHeroEncyclopediaTextAndLinks(_hero).ToString();[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
  		AnyAdditionalAllies = AdditionalAllies.Count > 0;
  		AnyAdditionalEnemies = AdditionalEnemies.Count > 0;
> 		AdditionalAlliesString = (AnyAdditionalAllies ? new Text[7mObj[0mect("{=!}+{REMAINING}").SetTextVariable("REMAINING", [0m
[7m[0mAdditionalAllies.Count).ToString() [0m[7m[0m: [0m[7m[0mstring.Empty);[0m
> 		AdditionalEnemiesString = (AnyAdditionalEnemies ? new Text[7mObj[0mect("{=!}+{REMAINING}").SetTextVariable("REMAINING", [0m
[7m[0mAdditionalEnemies.Count).ToString() [0m[7m[0m: [0m[7m[0mstring.Empty);[0m
[7m[0m  		AdditionalAlliesHint = new BasicTooltipViewModel(() => GetOverflowTooltip(AdditionalAllies));[0m
[7m[0m  		AdditionalEnemiesHint = new BasicTooltipViewModel(() => GetOverflowTooltip(AdditionalEnemies));[0m
  	}
  
> 	private List<TooltipProperty> GetOverflowTooltip(MBBindingList<[7mHero[0mVM> overflowList)[0m
[7m[0m  	{[0m
[7m[0m  		List<TooltipProperty> list = new List<TooltipProperty>();[0m
> 		foreach ([7mHero[0mVM overflow in overflowList)[0m
[7m[0m  		{[0m
[7m[0m  			list.Add(new TooltipProperty(string.Empty, overflow.NameText, 0));[0m


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Recurse -File Main | Select-String -Pattern 'new MessengerCampaignBehavior|MessengerCampaignBehavior\\(' | Select-Object Path,LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
 succeeded in 31462ms:

[32;1mPath                                                                                  [0m[32;1m LineNumber[0m[32;1m Line[0m
[32;1m----                                                                                  [0m [32;1m----------[0m [32;1m----[0m
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerCampaignBehavior.cs         34     public Messenger…


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content Main'"\\Features\\Messengers\\UI\\MessengerEncyclopediaMixin.cs | ForEach-Object { '{0,4}: {1}' -f "'$i, $_; $i++ } | Select-Object -First 130' in C:\Users\mikew\source\repos\TAOM
 succeeded in 488ms:
   1: using Bannerlord.UIExtenderEx.Attributes;
   2: using Bannerlord.UIExtenderEx.ViewModels;
   3: using TaleWorlds.CampaignSystem;
   4: using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
   5: using TaleWorlds.Core.ViewModelCollection.Information;
   6: using TaleWorlds.Library;
   7: using TaleWorlds.Localization;
   8: 
   9: namespace TAOM.Features.Messengers.UI;
  10: 
  11: [ViewModelMixin("RefreshValues")]
  12: internal sealed class MessengerEncyclopediaMixin : BaseViewModelMixin<EncyclopediaHeroPageVM>
  13: {
  14:     private readonly IMessengerService _service;
  15:     private readonly IMessengerSettingsProvider _settings;
  16: 
  17:     private bool _isMessengerAvailable;
  18:     private HintViewModel _sendMessengerHint;
  19:     private string _sendMessengerActionName = "";
  20: 
  21:     public MessengerEncyclopediaMixin(EncyclopediaHeroPageVM viewModel) : base(viewModel)
  22:     {
  23:         _service = TAOM.IoC.Resolve<IMessengerService>();
  24:         _settings = TAOM.IoC.Resolve<IMessengerSettingsProvider>();
  25:         _sendMessengerHint = new HintViewModel();
  26:         _sendMessengerActionName = new TextObject("{=taom_send_messenger}Send Messenger").ToString();
  27:     }
  28: 
  29:     public override void OnRefresh()
  30:     {
  31:         try
  32:         {
  33:             UpdateAvailability();
  34:         }
  35:         catch
  36:         {
  37:             // never let UI refresh throw
  38:             IsMessengerAvailable = false;
  39:         }
  40:     }
  41: 
  42:     private void UpdateAvailability()
  43:     {
  44:         var hero = ResolveHero();
  45:         if (hero == null)
  46:         {
  47:             IsMessengerAvailable = false;
  48:             SendMessengerHint = new HintViewModel(new TextObject("{=taom_messenger_target_unavailable}This person cannot be reached by messenger at this time."));
  49:             return;
  50:         }
  51: 
  52:         if (!_settings.EnableMessengers)
  53:         {
  54:             IsMessengerAvailable = false;
  55:             SendMessengerHint = new HintViewModel(new TextObject("{=taom_messenger_disabled}The messenger system is disabled in settings."));
  56:             return;
  57:         }
  58: 
  59:         var behavior = Campaign.Current?.GetCampaignBehavior<MessengerCampaignBehavior>();
  60:         if (behavior == null)
  61:         {
  62:             IsMessengerAvailable = false;
  63:             SendMessengerHint = new HintViewModel(new TextObject("{=taom_messenger_not_available}Messenger system not available."));
  64:             return;
  65:         }
  66: 
  67:         var canSend = behavior.CanSendMessenger(hero, out var reason);
  68:         IsMessengerAvailable = canSend;
  69:         SendMessengerHint = canSend
  70:             ? new HintViewModel()
  71:             : new HintViewModel(reason);
  72:     }
  73: 
  74:     [DataSourceMethod]
  75:     public void ExecuteSendMessenger()
  76:     {
  77:         var hero = ResolveHero();
  78:         if (hero == null) return;
  79: 
  80:         var behavior = Campaign.Current?.GetCampaignBehavior<MessengerCampaignBehavior>();
  81:         behavior?.SendMessenger(hero);
  82:         OnRefresh();
  83:     }
  84: 
  85:     [DataSourceProperty]
  86:     public string SendMessengerActionName
  87:     {
  88:         get => _sendMessengerActionName;
  89:         set
  90:         {
  91:             if (_sendMessengerActionName != value)
  92:             {
  93:                 _sendMessengerActionName = value;
  94:                 ViewModel?.OnPropertyChangedWithValue(value, nameof(SendMessengerActionName));
  95:             }
  96:         }
  97:     }
  98: 
  99:     [DataSourceProperty]
 100:     public int SendMessengerCost => _settings.MessengerGoldCost;
 101: 
 102:     [DataSourceProperty]
 103:     public bool IsMessengerAvailable
 104:     {
 105:         get => _isMessengerAvailable;
 106:         set
 107:         {
 108:             if (_isMessengerAvailable != value)
 109:             {
 110:                 _isMessengerAvailable = value;
 111:                 ViewModel?.OnPropertyChangedWithValue(value, nameof(IsMessengerAvailable));
 112:             }
 113:         }
 114:     }
 115: 
 116:     [DataSourceProperty]
 117:     public HintViewModel SendMessengerHint
 118:     {
 119:         get => _sendMessengerHint;
 120:         set
 121:         {
 122:             if (_sendMessengerHint != value)
 123:             {
 124:                 _sendMessengerHint = value;
 125:                 ViewModel?.OnPropertyChangedWithValue(value, nameof(SendMessengerHint));
 126:             }
 127:         }
 128:     }
 129: 
 130:     private Hero ResolveHero()

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content Main'"\\Features\\StartupResources\\StartupResourcesConfigProvider.cs | ForEach-Object { '{0,4}: {1}' -f "'$i, $_; $i++ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 426ms:
   1: using System;
   2: using System.Collections.Generic;
   3: using System.Globalization;
   4: using System.IO;
   5: using System.Xml.Linq;
   6: using TAOM.Core.Infrastructure;
   7: using TAOM.Core.Logging;
   8: using TAOM.Features.StartupResources.Config;
   9: 
  10: namespace TAOM.Features.StartupResources;
  11: 
  12: public class StartupResourcesConfigProvider : IStartupResourcesConfigProvider
  13: {
  14:     private const int PlayerGoldMaxValue = 10_000_000;
  15: 
  16:     private readonly IPathService _pathService;
  17:     private readonly IModLogger _logger;
  18:     private StartupResourcesConfig _cached;
  19: 
  20:     public StartupResourcesConfigProvider(IPathService pathService, IModLogger logger)
  21:     {
  22:         _pathService = pathService;
  23:         _logger = logger;
  24:     }
  25: 
  26:     public StartupResourcesConfig LoadConfig()
  27:     {
  28:         if (_cached != null)
  29:             return _cached;
  30: 
  31:         var path = Path.Combine(_pathService.ModuleDataPath, "startup_resources", "startup_resources_config.xml");
  32: 
  33:         if (!File.Exists(path))
  34:         {
  35:             _logger.LogWarning($"StartupResourcesConfigProvider: Config file not found: {path}");
  36:             _cached = new StartupResourcesConfig();
  37:             return _cached;
  38:         }
  39: 
  40:         try
  41:         {
  42:             var doc = XDocument.Load(path);
  43:             var config = new StartupResourcesConfig();
  44: 
  45:             foreach (var el in doc.Root.Elements("Culture"))
  46:             {
  47:                 var id = el.Attribute("id")?.Value;
  48:                 if (string.IsNullOrEmpty(id))
  49:                     continue;
  50: 
  51:                 config.CultureEntries.Add(new CultureResourceEntry
  52:                 {
  53:                     CultureId = id,
  54:                     Gold = int.Parse(el.Attribute("gold")?.Value ?? "0", CultureInfo.InvariantCulture),
  55:                     Influence = float.Parse(el.Attribute("influence")?.Value ?? "0", CultureInfo.InvariantCulture),
  56:                     PlayerGold = ParsePlayerGold(el.Attribute("playerGold")?.Value, id)
  57:                 });
  58:             }
  59: 
  60:             _cached = config;
  61:             return _cached;
  62:         }
  63:         catch (Exception ex)
  64:         {
  65:             _logger.LogError($"StartupResourcesConfigProvider: Failed to parse {path}: {ex.Message}");
  66:             _cached = new StartupResourcesConfig();
  67:             return _cached;
  68:         }
  69:     }
  70: 
  71:     private int ParsePlayerGold(string raw, string cultureId)
  72:     {
  73:         if (string.IsNullOrEmpty(raw))
  74:             return 0;
  75: 
  76:         if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
  77:         {
  78:             _logger.LogWarning($"StartupResourcesConfigProvider: invalid playerGold='{raw}' for culture '{cultureId}' — reverting to 0");
  79:             return 0;
  80:         }
  81: 
  82:         if (value < 0 || value > PlayerGoldMaxValue)
  83:         {
  84:             _logger.LogWarning($"StartupResourcesConfigProvider: playerGold={value} for culture '{cultureId}' out of range [0, {PlayerGoldMaxValue}] — reverting to 0");
  85:             return 0;
  86:         }
  87: 
  88:         return value;
  89:     }
  90: }

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.MountAndBlade.dll'; "'$out=ilspycmd -t TaleWorlds.MountAndBlade.MissionBehavior $dll; $out | Select-String -Pattern '"'OnEndMission|OnBehaviorInitialize|BehaviorType' -Context 2" in C:\Users\mikew\source\repos\TAOM
 succeeded in 849ms:

  	public IInputContext DebugInput => Input.DebugInput;
  
> 	public abstract Mission[7mBehaviorType[0m BehaviorType { get; }[0m
[7m[0m  [0m
[7m[0m  	public virtual void OnAfterMissionCreated()[0m
  	}
  
> 	public virtual void [7mOnBehaviorInitialize[0m()[0m
[7m[0m  	{[0m
[7m[0m  	}[0m
  	}
  
> 	public virtual void [7mOnEndMission[0mInternal()[0m
[7m[0m  	{[0m
> 		[7mOnEndMission[0m();[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	protected virtual void [7mOnEndMission[0m()[0m
[7m[0m  	{[0m
[7m[0m  	}[0m


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.MountAndBlade.dll'; ilspycmd -t TaleWorlds.MountAndBlade.IMissionListener "'$dll' in C:\Users\mikew\source\repos\TAOM
 succeeded in 550ms:
using TaleWorlds.Core;

namespace TaleWorlds.MountAndBlade;

public interface IMissionListener
{
	void OnEquipItemsFromSpawnEquipmentBegin(Agent agent, Agent.CreationType creationType);

	void OnEquipItemsFromSpawnEquipment(Agent agent, Agent.CreationType creationType);

	void OnEndMission();

	void OnMissionModeChange(MissionMode oldMissionMode, bool atStart);

	void OnConversationCharacterChanged();

	void OnResetMission();

	void OnDeploymentPlanMade(Team team, bool isFirstPlan);
}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; ilspycmd -t TaleWorlds.CampaignSystem.CampaignVec2 "'$dll | Select-String -Pattern '"'CampaignVec2\\(' -Context 1" in C:\Users\mikew\source\repos\TAOM
 succeeded in 932ms:

  
> 	public static CampaignVec2 Invalid => new [7mCampaignVec2([0mVec2.Invalid, isOnLand: true);[0m
[7m[0m  [0m
> 	public static CampaignVec2 Zero => new [7mCampaignVec2([0mVec2.Zero, isOnLand: true);[0m
[7m[0m  [0m
  
> 	public static void AutoGeneratedStaticCollectObjects[7mCampaignVec2([0mobject o, List<object> collectedObjects)[0m
[7m[0m  	{[0m
  
> 	public [7mCampaignVec2([0mVec2 pos, bool isOnLand)[0m
[7m[0m  	{[0m
  	{
> 		return new [7mCampaignVec2([0mv1._position + v2._position, v1.IsOnLand);[0m
[7m[0m  	}[0m
  	{
> 		return new [7mCampaignVec2([0mv1._position - v2._position, v1.IsOnLand);[0m
[7m[0m  	}[0m
  	{
> 		return new [7mCampaignVec2([0mv1._position + v2, v1.IsOnLand);[0m
[7m[0m  	}[0m
  	{
> 		return new [7mCampaignVec2([0mv1._position - v2, v1.IsOnLand);[0m
[7m[0m  	}[0m
  	{
> 		return new [7mCampaignVec2([0mnew Vec2(v.X * f, v.Y * f), v.IsOnLand);[0m
[7m[0m  	}[0m
  	{
> 		return new [7mCampaignVec2([0mnew Vec2(v.X * f, v.Y * f), v.IsOnLand);[0m
[7m[0m  	}[0m
  	{
> 		return new [7mCampaignVec2([0mnew Vec2(v.X / f, v.Y / f), v.IsOnLand);[0m
[7m[0m  	}[0m
  	{
> 		return new [7mCampaignVec2([0mnew Vec2(Y, 0f - X), IsOnLand);[0m
[7m[0m  	}[0m
  	{
> 		return new [7mCampaignVec2([0mnew Vec2(0f - Y, X), IsOnLand);[0m
[7m[0m  	}[0m


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content Main\\_Module\\ModuleData\\taom_cc_strings.xml -Head 10; Get-Content Main\\_Module\\ModuleData\\taom_named_companion_strings.xml -Head 5" in C:\Users\mikew\source\repos\TAOM
2026-05-06T23:14:12.405210Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.5 seconds
Output:
<?xml version="1.0" encoding="utf-8"?>

<strings>



	<!-- PARENTS -->

	<string id="taom_cc_taom_parent_gondor_1_text" text="{=taom_cc_taom_parent_gondor_1_text}Noble Houses of Gondor" />

[31;1mGet-Content: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … l -Head 10; [36;1mGet-Content Main\_Module\ModuleData\taom_named_companion_[0m …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m               ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot find path 'C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_named_companion_strings.xml' because it does not exist.[0m

 exited 1 in 496ms:
<?xml version="1.0" encoding="utf-8"?>

<strings>



	<!-- PARENTS -->

	<string id="taom_cc_taom_parent_gondor_1_text" text="{=taom_cc_taom_parent_gondor_1_text}Noble Houses of Gondor" />

[31;1mGet-Content: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … l -Head 10; [36;1mGet-Content Main\_Module\ModuleData\taom_named_companion_[0m …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m               ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot find path 'C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_named_companion_strings.xml' because it does not exist.[0m

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; ilspycmd -t TaleWorlds.CampaignSystem.Actions.GiveGoldAction "'$dll | Select-String -Pattern '"'ApplyBetweenCharacters|ChangeGold' -Context 0,50" in C:\Users\mikew\source\repos\TAOM
 succeeded in 868ms:

> 			settlementComponent.[7mChangeGold[0m(-goldAmount);[0m
[7m[0m  		}[0m
[7m[0m  		if (recipientHero != null)[0m
[7m[0m  		{[0m
[7m[0m  			recipientHero.ChangeHeroGold(goldAmount);[0m
[7m[0m  		}[0m
[7m[0m  		else if (recipientParty != null && recipientParty.IsMobile)[0m
[7m[0m  		{[0m
[7m[0m  			recipientParty.MobileParty.PartyTradeGold += goldAmount;[0m
[7m[0m  		}[0m
[7m[0m  		else if (recipientParty != null && recipientParty.IsSettlement)[0m
[7m[0m  		{[0m
> 			recipientParty.Settlement.SettlementComponent.[7mChangeGold[0m(goldAmount);[0m
[7m[0m  		}[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m[0mCampaignEventDispatcher.Instance.OnHeroOrPartyTradedGold((giverHero, [0m[7m[0mgiverParty), [0m[7m[0m(recipientHero, [0m[7m[0mrecipientParty), [0m
[7m[0m(goldAmount, [0m[7m[0mtransactionStringId), [0m[7m[0mshowQuickInformation);[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	public static void [7mApplyBetweenCharacters[0m(Hero [0m[7m[0mgiverHero, [0m[7m[0mHero [0m[7m[0mrecipientHero, [0m[7m[0mint [0m[7m[0mamount, [0m[7m[0mbool [0m[7m[0mdisableNotification [0m
[7m[0m= [0m[7m[0mfalse)[0m
[7m[0m  	{[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m[0mApplyInternal(giverHero, [0m[7m[0mnull, [0m[7m[0mrecipientHero, [0m[7m[0mnull, [0m[7m[0mamount, [0m[7m[0m!disableNotification [0m[7m[0m&& [0m[7m[0m(giverHero [0m[7m[0m== [0m[7m[0mHero.MainHero [0m[7m[0m|| [0m
[7m[0mrecipientHero [0m[7m[0m== [0m[7m[0mHero.MainHero));[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m [0m[7m [0m[7m	[0m[7m[0mpublic [0m[7m[0mstatic [0m[7m[0mvoid [0m[7m[0mApplyForCharacterToSettlement(Hero [0m[7m[0mgiverHero, [0m[7m[0mSettlement [0m[7m[0msettlement, [0m[7m[0mint [0m[7m[0mamount, [0m[7m[0mbool [0m
[7m[0mdisableNotification [0m[7m[0m= [0m[7m[0mfalse)[0m
[7m[0m  	{[0m
[7m[0m  		ApplyInternal(giverHero, null, null, settlement.Party, amount, !disableNotification && giverHero == Hero.MainHero);[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m [0m[7m [0m[7m	[0m[7m[0mpublic [0m[7m[0mstatic [0m[7m[0mvoid [0m[7m[0mApplyForSettlementToCharacter(Settlement [0m[7m[0mgiverSettlement, [0m[7m[0mHero [0m[7m[0mrecipientHero, [0m[7m[0mint [0m[7m[0mamount, [0m[7m[0mbool [0m
[7m[0mdisableNotification [0m[7m[0m= [0m[7m[0mfalse)[0m
[7m[0m  	{[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m[0mApplyInternal(recipientHero, [0m[7m[0mnull, [0m[7m[0mnull, [0m[7m[0mgiverSettlement.Party, [0m[7m[0m-amount, [0m[7m[0m!disableNotification [0m[7m[0m&& [0m[7m[0mrecipientHero [0m[7m[0m== [0m
[7m[0mHero.MainHero);[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m [0m[7m [0m[7m	[0m[7m[0mpublic [0m[7m[0mstatic [0m[7m[0mvoid [0m[7m[0mApplyForSettlementToParty(Settlement [0m[7m[0mgiverSettlement, [0m[7m[0mPartyBase [0m[7m[0mrecipientParty, [0m[7m[0mint [0m[7m[0mamount, [0m[7m[0mbool [0m
[7m[0mdisableNotification [0m[7m[0m= [0m[7m[0mfalse)[0m
[7m[0m  	{[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m[0mApplyInternal(null, [0m[7m[0mgiverSettlement.Party, [0m[7m[0mnull, [0m[7m[0mrecipientParty, [0m[7m[0mamount, [0m[7m[0m!disableNotification [0m[7m[0m&& [0m
[7m[0mrecipientParty.LeaderHero [0m[7m[0m== [0m[7m[0mHero.MainHero);[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m [0m[7m [0m[7m	[0m[7m[0mpublic [0m[7m[0mstatic [0m[7m[0mvoid [0m[7m[0mApplyForPartyToSettlement(PartyBase [0m[7m[0mgiverParty, [0m[7m[0mSettlement [0m[7m[0msettlement, [0m[7m[0mint [0m[7m[0mamount, [0m[7m[0mbool [0m
[7m[0mdisableNotification [0m[7m[0m= [0m[7m[0mfalse)[0m
[7m[0m  	{[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m[0mApplyInternal(null, [0m[7m[0mgiverParty, [0m[7m[0mnull, [0m[7m[0msettlement.Party, [0m[7m[0mamount, [0m[7m[0m!disableNotification [0m[7m[0m&& [0m[7m[0mgiverParty?.LeaderHero [0m[7m[0m== [0m
[7m[0mHero.MainHero);[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m [0m[7m [0m[7m	[0m[7m[0mpublic [0m[7m[0mstatic [0m[7m[0mvoid [0m[7m[0mApplyForPartyToCharacter(PartyBase [0m[7m[0mgiverParty, [0m[7m[0mHero [0m[7m[0mrecipientHero, [0m[7m[0mint [0m[7m[0mamount, [0m[7m[0mbool [0m
[7m[0mdisableNotification [0m[7m[0m= [0m[7m[0mfalse)[0m
[7m[0m  	{[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m[0mApplyInternal(null, [0m[7m[0mgiverParty, [0m[7m[0mrecipientHero, [0m[7m[0mnull, [0m[7m[0mamount, [0m[7m[0m!disableNotification [0m[7m[0m&& [0m[7m[0mgiverParty [0m[7m[0m!= [0m[7m[0mnull [0m[7m[0m&& [0m
[7m[0m(giverParty.LeaderHero [0m[7m[0m== [0m[7m[0mHero.MainHero [0m[7m[0m|| [0m[7m[0mrecipientHero [0m[7m[0m== [0m[7m[0mHero.MainHero));[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m [0m[7m [0m[7m	[0m[7m[0mpublic [0m[7m[0mstatic [0m[7m[0mvoid [0m[7m[0mApplyForCharacterToParty(Hero [0m[7m[0mgiverHero, [0m[7m[0mPartyBase [0m[7m[0mreceipentParty, [0m[7m[0mint [0m[7m[0mamount, [0m[7m[0mbool [0m
[7m[0mdisableNotification [0m[7m[0m= [0m[7m[0mfalse)[0m
[7m[0m  	{[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m[0mApplyInternal(giverHero, [0m[7m[0mnull, [0m[7m[0mnull, [0m[7m[0mreceipentParty, [0m[7m[0mamount, [0m[7m[0m!disableNotification [0m[7m[0m&& [0m[7m[0m(giverHero [0m[7m[0m== [0m[7m[0mHero.MainHero [0m
[7m[0m|| [0m[7m[0mreceipentParty.LeaderHero [0m[7m[0m== [0m[7m[0mHero.MainHero));[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m [0m[7m [0m[7m	[0m[7m[0mpublic [0m[7m[0mstatic [0m[7m[0mvoid [0m[7m[0mApplyForPartyToParty(PartyBase [0m[7m[0mgiverParty, [0m[7m[0mPartyBase [0m[7m[0mreceipentParty, [0m[7m[0mint [0m[7m[0mamount, [0m[7m[0mbool [0m
[7m[0mdisableNotification [0m[7m[0m= [0m[7m[0mfalse)[0m
[7m[0m  	{[0m
[7m [0m[7m [0m[7m	[0m[7m	[0m[7m[0mApplyInternal(null, [0m[7m[0mgiverParty, [0m[7m[0mnull, [0m[7m[0mreceipentParty, [0m[7m[0mamount, [0m[7m[0m!disableNotification [0m[7m[0m&& [0m[7m[0m(giverParty.LeaderHero [0m[7m[0m== [0m
[7m[0mHero.MainHero [0m[7m[0m|| [0m[7m[0mreceipentParty.LeaderHero [0m[7m[0m== [0m[7m[0mHero.MainHero));[0m
[7m[0m  	}[0m
[7m[0m  }[0m


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; "'$out=ilspycmd -t TaleWorlds.CampaignSystem.Actions.GiveGoldAction $dll; $idx=($out|Select-String -Pattern '"'private static void ApplyInternal'|Select-Object -First 1).LineNumber; "'$out|Select-Object -Skip ($idx-1) -First 40' in C:\Users\mikew\source\repos\TAOM
 succeeded in 850ms:
	private static void ApplyInternal(Hero giverHero, PartyBase giverParty, Hero recipientHero, PartyBase recipientParty, int goldAmount, bool showQuickInformation, string transactionStringId = "")
	{
		if (giverHero != null)
		{
			goldAmount = MathF.Min(giverHero.Gold, goldAmount);
			giverHero.ChangeHeroGold(-goldAmount);
		}
		else if (giverParty != null && giverParty.IsMobile)
		{
			goldAmount = MathF.Min(giverParty.MobileParty.PartyTradeGold, goldAmount);
			giverParty.MobileParty.PartyTradeGold -= goldAmount;
		}
		else if (giverParty != null && giverParty.IsSettlement)
		{
			SettlementComponent settlementComponent = giverParty.Settlement.SettlementComponent;
			goldAmount = MathF.Min(settlementComponent.Gold, goldAmount);
			settlementComponent.ChangeGold(-goldAmount);
		}
		if (recipientHero != null)
		{
			recipientHero.ChangeHeroGold(goldAmount);
		}
		else if (recipientParty != null && recipientParty.IsMobile)
		{
			recipientParty.MobileParty.PartyTradeGold += goldAmount;
		}
		else if (recipientParty != null && recipientParty.IsSettlement)
		{
			recipientParty.Settlement.SettlementComponent.ChangeGold(goldAmount);
		}
		CampaignEventDispatcher.Instance.OnHeroOrPartyTradedGold((giverHero, giverParty), (recipientHero, recipientParty), (goldAmount, transactionStringId), showQuickInformation);
	}

	public static void ApplyBetweenCharacters(Hero giverHero, Hero recipientHero, int amount, bool disableNotification = false)
	{
		ApplyInternal(giverHero, null, recipientHero, null, amount, !disableNotification && (giverHero == Hero.MainHero || recipientHero == Hero.MainHero));
	}

	public static void ApplyForCharacterToSettlement(Hero giverHero, Settlement settlement, int amount, bool disableNotification = false)
	{

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git diff -- TAOM.Tests/Features/CharacterCreation/CharacterCreationContentServiceTests.cs | Select-Object -First 120' in C:\Users\mikew\source\repos\TAOM
 succeeded in 431ms:
diff --git a/TAOM.Tests/Features/CharacterCreation/CharacterCreationContentServiceTests.cs b/TAOM.Tests/Features/CharacterCreation/CharacterCreationContentServiceTests.cs
index 302528d..042f65f 100644
--- a/TAOM.Tests/Features/CharacterCreation/CharacterCreationContentServiceTests.cs
+++ b/TAOM.Tests/Features/CharacterCreation/CharacterCreationContentServiceTests.cs
@@ -5,6 +5,7 @@ using TAOM.Core.Domain;
 using TAOM.Core.Logging;
 using TAOM.Features.CharacterCreation;
 using TAOM.Features.CharacterCreation.Models;
+using TAOM.Features.StartupResources;
 
 namespace TAOM.Tests.Features.CharacterCreation;
 
@@ -17,6 +18,8 @@ public class CharacterCreationContentServiceTests
     private IHeroRosterAdapter _heroRosterAdapter;
     private IEquipmentRosterProvider _equipmentRosterProvider;
     private ICareerMenuService _careerMenuService;
+    private IPlayerStartupGoldService _playerStartupGoldService;
+    private IPlayerEquipmentService _playerEquipmentService;
     private IModLogger _logger;
     private CharacterCreationContentService _sut;
 
@@ -29,6 +32,8 @@ public class CharacterCreationContentServiceTests
         _heroRosterAdapter = Substitute.For<IHeroRosterAdapter>();
         _equipmentRosterProvider = Substitute.For<IEquipmentRosterProvider>();
         _careerMenuService = Substitute.For<ICareerMenuService>();
+        _playerStartupGoldService = Substitute.For<IPlayerStartupGoldService>();
+        _playerEquipmentService = Substitute.For<IPlayerEquipmentService>();
         _logger = Substitute.For<IModLogger>();
 
         _sut = new CharacterCreationContentService(
@@ -38,6 +43,8 @@ public class CharacterCreationContentServiceTests
             _heroRosterAdapter,
             _equipmentRosterProvider,
             _careerMenuService,
+            _playerStartupGoldService,
+            _playerEquipmentService,
             _logger);
     }
 

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git diff -- CHANGELOG.md | Select-Object -First 120' in C:\Users\mikew\source\repos\TAOM
 succeeded in 444ms:
diff --git a/CHANGELOG.md b/CHANGELOG.md
index 364bab2..8ca911f 100644
--- a/CHANGELOG.md
+++ b/CHANGELOG.md
@@ -2,6 +2,92 @@
 
 ## 2026-05-06
 
+### Feat: Player starting gold + CC equipment persistence (port from LOTRAOM `StartingEquipmentGold`)
+
+Adds two adjacent capabilities the LOTRAOM 1.2.12 `StartingEquipmentGold/` module provided that TAOM had only half-built: configurable per-culture **player starting funds** at character-creation finalize, and **persistence** of the youth option's equipment roster onto `Hero.MainHero.BattleEquipment` / `CivilianEquipment` (previously the CC preview was visual-only — the player exited CC with vanilla default equipment regardless of the option chosen).
+
+**Why this exists:** The existing `StartupResources` feature explicitly skipped the player clan (`StartupGoldService.cs:40 if (hero.IsPlayerClan) continue;`) — only NPC lords got gold. And `NarrativeMenuBuilder.UpdateYouthEquipment` mutated the CC preview character but never wrote to the player's persistent equipment slots. New campaigns started with vanilla default 1000 denars and vanilla default starting equipment regardless of culture or youth option.
+
+**Architecture (XML/JSON-driven, not LOTRAOM's hard-coded C# dictionary):**
+
+- **Gold:** new `playerGold="…"` attribute on `<Culture>` rows in [`startup_resources_config.xml`](Main/_Module/ModuleData/startup_resources/startup_resources_config.xml). Per-culture only (per the user's scope choice this session). Range-validated `[0, 10_000_000]` per the "Config Providers MUST Validate" rule — out-of-range, non-numeric, or sign-flipped values revert to 0 with a logged warning. Missing attribute defaults to 0 silently. New service [`PlayerStartupGoldService`](Main/Features/StartupResources/PlayerStartupGoldService.cs) reuses the existing `IGoldGiftAdapter` (which already wraps `GiveGoldAction.ApplyBetweenCharacters(null, hero, amount, true)`).
+- **Equipment:** new ADR-007 adapter [`IPlayerEquipmentAdapter`](Main/Adapters/IPlayerEquipmentAdapter.cs) wraps `MBEquipmentRoster.AllEquipments` filter by `IsBattle`/`IsCivilian` and `Equipment.FillFrom` mutate-in-place. Service [`PlayerEquipmentService`](Main/Features/CharacterCreation/PlayerEquipmentService.cs) builds the roster ID via the existing TAOM convention `player_char_creation_{culture}_{titleType}_{m|f}` (promoted from `NarrativeMenuBuilder.BuildEquipmentRosterId` to a shared helper [`PlayerEquipmentRosterIds`](Main/Features/CharacterCreation/PlayerEquipmentRosterIds.cs)). Adapter returns an enum `PlayerEquipmentApplyResult` so the service surface stays free of sealed TaleWorlds types.
+- **Wiring:** both services injected into `CharacterCreationContentService` and called from `OnCharacterCreationFinalize` after `AssignCareer`. Reads `selectedCulture.StringId` and `manager.CharacterCreationContent.SelectedTitleType` directly (not via `Hero.MainHero.Culture` — see plan risk note about the in-flight finalize-order culture override).
+
+**API verification (v1.3.15 vs v1.2.12 LOTRAOM source):**
+
+Run `ilspycmd` on installed v1.3.15 DLLs before writing the adapter. Two drifts caught:
+1. `MBEquipmentRoster.GetBattleEquipments()` / `GetCivilianEquipments()` (LOTRAOM 1.2 surface) **don't exist** in v1.3.15 — the public surface is `AllEquipments` + filter by `Equipment.IsBattle` / `IsCivilian` properties.
+2. LOTRAOM wrote to `CharacterObject.PlayerCharacter.FirstBattleEquipment.FillFrom(...)`. In v1.3.15 the same backing object is exposed cleaner via `Hero.MainHero.BattleEquipment.FillFrom(...)` (the `CharacterObject.FirstBattleEquipment` getter on a Hero just delegates to `HeroObject.BattleEquipment` — same Equipment instance, cleaner v1.3 surface).
+
+The `GiveGoldAction.ApplyBetweenCharacters(Hero giverHero, Hero recipientHero, int amount, bool disableNotification = false)` signature matches LOTRAOM's call exactly — already in production use via the existing `GoldGiftAdapter`.
+
+**Tests:** 28 new + extended unit tests, all green. 1340/1340 total tests pass.
+- 5 new `StartupResourcesConfigProviderTests` cases — `playerGold` parsed, negative rejected, over-cap rejected, non-numeric rejected, missing attribute silent
+- 8 new `PlayerStartupGoldServiceTests` — culture match (case-insensitive), unknown culture warn, zero-gold skip, null/empty culture/hero no-ops, info-log content
+- 9 new `PlayerEquipmentServiceTests` — male/female roster suffix, null/empty input no-ops, all four `PlayerEquipmentApplyResult` branches mapped to correct log levels
+- 6 existing `CharacterCreationContentServiceTests` — updated for the new constructor signature (added `IPlayerStartupGoldService` and `IPlayerEquipmentService` dependencies)
+
+**Initial culture seeds for `playerGold`:** Elven 8,000–10,000 (Rivendell/Lothlorien wealthiest), Dwarf 7,500, Dark factions 6,000, Human Good kingdoms 5,000, Tribal/Eastern 4,000. Tunable in [`startup_resources_config.xml`](Main/_Module/ModuleData/startup_resources/startup_resources_config.xml) — edits require Bannerlord process restart (singleton config cache), not save-load.
+
+**Deep-review fixes (Agent 5 data-flow trace, 2026-05-06):**
+- Added `<Culture id="empire" .../>` with `playerGold="4000"` to startup config — Dunland (CC-selectable per `cultures.json`) was missing from the seed XML and would have silently granted 0 gold.
+- Changed `taom_youth_sturgia_1` (Royal Guard of Dale) `title_type` from `"retainer"` to `"guard"` — vanilla SandBox `sandbox_equipment_sets.xml` has no `sturgia_retainer` roster pair, so the first sturgia youth option would have shipped with no equipment applied. `guard` matches both the option's text ("Royal Guard of Dale") and an existing roster.
+- Routed `CareerMenuService.GetCareerMenuCharacterArgs` (the career-screen visual preview) through the new shared `PlayerEquipmentRosterIds.Build` helper instead of inlining the roster-ID format string. Eliminates the third independent construction of the `player_char_creation_*` convention.
+- Added `Campaign.Current.DeadBattleEquipment` guard to `PlayerEquipmentAdapter.ApplyRosterToPlayer`. `Hero.BattleEquipment` falls through to a process-wide shared `DeadBattleEquipment` singleton when the hero's `_battleEquipment` is null; calling `FillFrom` on that singleton would corrupt equipment for every dead/uninitialized hero in the session. MainHero at CC finalize is always initialized so this is defensive — but the adapter accepts any `heroId` and shouldn't expose the foot-gun to future callers.
+
+**Out of scope (deliberate):** per-youth-option gold (per-culture only this session), starting items / starting troops (LOTRAOM had this; CareerSystem covers troop starts in TAOM), MCM live retuning. The visual `UpdateYouthEquipment` preview is preserved unchanged — it's orthogonal to persistence.
+
+**Pre-existing tech debt noted by deep-review (NOT fixed this session, separate cleanup):** `CharacterCreationContentService.AssignCareer` resolves `ICareerCreationHandler` and `ICareerRegistry` via `IoC.Resolve<>` (lines ~218, 235) — service-locator anti-pattern flagged by Standards agent. Pre-dates this session. Should be lifted to constructor injection in a follow-up.
+
+Plan: [`C:\Users\mikew\.claude\plans\please-investigate-this-that-lovely-pine.md`](../../.claude/plans/please-investigate-this-that-lovely-pine.md)
+
+Constraint: youth-option title_type strings (`retainer`, `warrior`, etc.) must match between `youth_menu.json` and the equipment XML roster IDs — typos surface as a "roster not found" warning at finalize and the player gets vanilla equipment. No crash.
+
+Research: `GiveGoldAction.ApplyBetweenCharacters` (TaleWorlds.CampaignSystem.Actions), `MBEquipmentRoster.AllEquipments` (TaleWorlds.Core), `Equipment.FillFrom` (TaleWorlds.Core), `Hero.BattleEquipment` / `CivilianEquipment` (TaleWorlds.CampaignSystem), `CharacterCreationContent.SelectedTitleType` (TaleWorlds.CampaignSystem.CharacterCreationContent).
+
+Save-compat: Player gold + equipment writes happen at CC finalize on new-game start only — no save-format changes, no impact on existing saves.
+
+
+
+### Fix: SiegeDismount — deep-review HIGH findings (false-positive dismount + config validation)
+
+Two HIGH findings from `/deep-review` Agent 5 (Data Flow), fixed in the same session per the "no silent deferrals" rule:
+
+**GAP 1 — out-of-range MountBehavior int silently captured mount with no action.** A user manually editing `ModuleData/MCM/Global/TAOM.json` to set `SiegeMountBehavior` outside `[0, 3]` produced an undefined enum value. The switch had no `default:` case, so `_capturedSnapshot` got set but no clear/deposit/restore fired — the player's mount data was read but no effect occurred. Fix: added `default:` case to the switch in [`SiegeDismountService.OnMissionStart`](Main/Features/SiegeDismount/SiegeDismountService.cs) that logs `LogWarning` and treats unknown values as a full no-op. Two regression tests cover the path. Per `csharp-architecture.md` "Config Providers MUST Validate" rule.
+
+**GAP 2 — false-positive siege detection on real TAOM castle scenes.** The keyword fallback `IsSiegeMission` matched substrings `gate` and `wall`, falsely firing for [`castle_orthanc_gate`](Main/_Module/ModuleData/custom_settlements.xml#L74) (Isengard's Orthanc Gate castle) and [`castle_gundabad_wall`](Main/_Module/ModuleData/custom_settlements.xml#L344) (Gundabad Wall castle) — both real TAOM `Location id="center"` scenes used during normal castle visits. With `DismountKeepOnMap` or `DismountToInventory` modes, the player's mount would have been incorrectly removed during a non-siege visit. Fix: narrowed `SceneSiegeKeywords` to `siege`, `assault`, `breach` only — removed `gate` and `wall`. Real sieges hit `Mission.IsSiegeBattle = true` directly; the keyword fallback is only for modded/custom siege scenes that fail to set that flag. Four data-row regression tests cover the false-positive scenes.
+
+**KL 1, KL 3 — state hygiene.** `OnMissionEnd`'s early-return path now clears the stale `_capturedSnapshot` so the singleton doesn't carry mount-id strings between missions. Added a guard in `OnMissionStart` for the theoretical case where `HasMount()` returns true but `Capture()` returns an empty snapshot. Three regression tests.
+
+Net: 33 SiegeDismount tests pass (+9 from this fix). 1404/1404 total tests green. Saving the deep-review findings cost less than 30 minutes; in-game discovery would have cost a player having their mount silently disappear when visiting Orthanc Gate.
+
+### Feat: SiegeDismount — port external sibling module into Main/Features/
+
+Refactored the developer-built `SiegeDismount` module (one of seven dropped at `Downloads/Features_fixed/`) into TAOM's adapter / service / IoC pattern. The original was a standalone Bannerlord module with its own `SubModule.xml`, `MissionBehavior`, and MCM settings; this commit replaces it with `Main/Features/SiegeDismount/` so it ships as part of the TAOM DLL with the same MCM, logging, and toggle conventions as the rest of TAOM.
+
+**What it does:** when a siege mission begins, the player's mount + harness are auto-handled per the user's MCM choice — Vanilla (no change), KeepOnMap, ToInventory, or AutoRemount-after-siege (default). Eliminates the on-horseback-in-fortress-courtyard immersion break for LOTR sieges (Helm's Deep, Minas Tirith, Erebor's gates).
+
+**Architecture:**
+- [`SiegeDismountService`](Main/Features/SiegeDismount/SiegeDismountService.cs) — pure state machine, fully unit-testable
+- [`SiegeDismountMissionBehavior`](Main/Features/SiegeDismount/Hooks/SiegeDismountMissionBehavior.cs) — thin engine bridge; reads `Mission.Current.IsSiegeBattle` + `SceneName` and delegates
+- [`IPlayerMountAdapter`](Main/Adapters/IPlayerMountAdapter.cs) + [`IPartyMountInventoryAdapter`](Main/Adapters/IPartyMountInventoryAdapter.cs) — ADR-007 wrappers over `Hero.MainHero.BattleEquipment` and `MobileParty.MainParty.ItemRoster`. Service never sees `EquipmentElement` or `ItemObject`
+- [`IMountSnapshot`](Main/Features/SiegeDismount/Models/IMountSnapshot.cs) — opaque token between adapter and service
+- MCM settings folded into [`TaomSettings.cs`](Main/Features/TaomSettings.cs) under group `Battle Tactics / Siege Dismount` (3 settings: Enable, Behavior dropdown 0-3, Debug)
+- No Harmony patches — pure `MissionBehavior` integration
+
+**Logging:** every lifecycle event hits `IModLogger` per the mandatory cross-cutting logging contract from the integration plan. `LogInfo` on enable/disable + siege detection + restore. `LogDebug` (gated by `SiegeDismountDebug` MCM toggle) for per-mode decisions. `LogError` for all caught exceptions on adapter calls — never silent.
+
+**Tests:** 24 unit tests in [`SiegeDismountServiceTests`](TAOM.Tests/Features/SiegeDismount/SiegeDismountServiceTests.cs) covering disable paths, all four behavior modes, scene-name siege detection (5 keyword variants), idempotent end, and four logging contracts. Build green, 1340/1340 tests pass.
+
+Source material: [`Downloads/Features_fixed/_decompiled/SiegeDismount/SiegeDismount.decompiled.cs`](Downloads/Features_fixed/_decompiled/SiegeDismount/SiegeDismount.decompiled.cs). Original developer's behavior preserved verbatim — same modes, same defaults, same scene-name keywords.
+
+Not-tested: `PlayerMountAdapter` and `PartyMountInventoryAdapter` (require live `Hero.MainHero` and `MobileParty.MainParty`); covered by in-game golden-path verification per [docs/features/siege-dismount.md](docs/features/siege-dismount.md#verification).
+
+Constraint: mount/harness `ItemModifier` (durability/quality bonus) is dropped on auto-remount because Phase 1 stores only `StringId`. Documented as known limitation — upgrade to a modifier-preserving snapshot is a follow-up if any player reports it.
+
+
+
 ### Docs: CCBodyProperties — feature doc rewrite + seed config + memory entry (in-game verified)
 
 User confirmed the OnCultureSelection postfix made the configured culture body visible in-game (issue #108 closed). Documentation updated to reflect the final 3-patch architecture and the call-chain lessons learned.

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path CHANGELOG.md -Pattern 'Messenger|SiegeDismount|Starting' -Context 1" in C:\Users\mikew\source\repos\TAOM
 succeeded in 459ms:

  CHANGELOG.md:4:
> CHANGELOG.md:5:### Feat: Player [7mstarting[0m gold + CC equipment persistence (port from LOTRAOM `StartingEquipmentGold`)[0m
[7m[0m  CHANGELOG.md:6:[0m
> CHANGELOG.md:7:Adds two adjacent capabilities the LOTRAOM 1.2.12 `[7mStarting[0mEquipmentGold/` [0m[7m[0mmodule [0m[7m[0mprovided [0m[7m[0mthat [0m[7m[0mTAOM [0m
[7m[0mhad [0m[7m[0monly [0m[7m[0mhalf-built: [0m[7m[0mconfigurable [0m[7m[0mper-culture [0m[7m[0m**player [0m[7m[0mstarting [0m[7m[0mfunds** [0m[7m[0mat [0m[7m[0mcharacter-creation [0m[7m[0mfinalize, [0m[7m[0mand [0m
[7m[0m**persistence** [0m[7m[0mof [0m[7m[0mthe [0m[7m[0myouth [0m[7m[0moption's [0m[7m[0mequipment [0m[7m[0mroster [0m[7m[0monto [0m[7m[0m`Hero.MainHero.BattleEquipment` [0m[7m[0m/ [0m[7m[0m`CivilianEquipment` [0m
[7m[0m(previously [0m[7m[0mthe [0m[7m[0mCC [0m[7m[0mpreview [0m[7m[0mwas [0m[7m[0mvisual-only [0m[7m[0m— [0m[7m[0mthe [0m[7m[0mplayer [0m[7m[0mexited [0m[7m[0mCC [0m[7m[0mwith [0m[7m[0mvanilla [0m[7m[0mdefault [0m[7m[0mequipment [0m[7m[0mregardless [0m[7m[0mof [0m[7m[0mthe [0m
[7m[0moption [0m[7m[0mchosen).[0m
[7m[0m  CHANGELOG.md:8:[0m
> CHANGELOG.md:9:**Why this exists:** The existing `StartupResources` feature explicitly skipped the player clan 
(`StartupGoldService.cs:40 if (hero.IsPlayerClan) continue;`) — only NPC lords got gold. And 
`NarrativeMenuBuilder.UpdateYouthEquipment` mutated the CC preview character but never wrote to the player's 
persistent equipment slots. New campaigns started with vanilla default 1000 denars and vanilla default [7mstarting [0m
[7m[0mequipment [0m[7m[0mregardless [0m[7m[0mof [0m[7m[0mculture [0m[7m[0mor [0m[7m[0myouth [0m[7m[0moption.[0m
[7m[0m  CHANGELOG.md:10:[0m
  CHANGELOG.md:38:
> CHANGELOG.md:39:**Out of scope (deliberate):** per-youth-option gold (per-culture only this session), [7mstarting [0m[7m[0mitems [0m
[7m[0m/ [0m[7m[0mstarting [0m[7m[0mtroops [0m[7m[0m(LOTRAOM [0m[7m[0mhad [0m[7m[0mthis; [0m[7m[0mCareerSystem [0m[7m[0mcovers [0m[7m[0mtroop [0m[7m[0mstarts [0m[7m[0min [0m[7m[0mTAOM), [0m[7m[0mMCM [0m[7m[0mlive [0m[7m[0mretuning. [0m[7m[0mThe [0m[7m[0mvisual [0m
[7m[0m`UpdateYouthEquipment` [0m[7m[0mpreview [0m[7m[0mis [0m[7m[0mpreserved [0m[7m[0munchanged [0m[7m[0m— [0m[7m[0mit's [0m[7m[0morthogonal [0m[7m[0mto [0m[7m[0mpersistence.[0m
[7m[0m  CHANGELOG.md:40:[0m
  CHANGELOG.md:52:
> CHANGELOG.md:53:### Fix: [7mSiegeDismount[0m — deep-review HIGH findings (false-positive dismount + config validation)[0m
[7m[0m  CHANGELOG.md:54:[0m
  CHANGELOG.md:56:
> CHANGELOG.md:57:**GAP 1 — out-of-range MountBehavior int silently captured mount with no action.** A user manually 
editing `ModuleData/MCM/Global/TAOM.json` to set `SiegeMountBehavior` outside `[0, 3]` produced an undefined enum 
value. The switch had no `default:` case, so `_capturedSnapshot` got set but no clear/deposit/restore fired — the 
player's mount data was read but no effect occurred. Fix: added `default:` case to the switch in 
[`[7mSiegeDismount[0mService.OnMissionStart`](Main/Features/SiegeDismount/SiegeDismountService.cs) [0m[7m[0mthat [0m[7m[0mlogs [0m[7m[0m`LogWarning` [0m
[7m[0mand [0m[7m[0mtreats [0m[7m[0munknown [0m[7m[0mvalues [0m[7m[0mas [0m[7m[0ma [0m[7m[0mfull [0m[7m[0mno-op. [0m[7m[0mTwo [0m[7m[0mregression [0m[7m[0mtests [0m[7m[0mcover [0m[7m[0mthe [0m[7m[0mpath. [0m[7m[0mPer [0m[7m[0m`csharp-architecture.md` [0m[7m[0m"Config [0m
[7m[0mProviders [0m[7m[0mMUST [0m[7m[0mValidate" [0m[7m[0mrule.[0m
[7m[0m  CHANGELOG.md:58:[0m
  CHANGELOG.md:62:
> CHANGELOG.md:63:Net: 33 [7mSiegeDismount [0m[7m[0mtests [0m[7m[0mpass [0m[7m[0m(+9 [0m[7m[0mfrom [0m[7m[0mthis [0m[7m[0mfix). [0m[7m[0m1404/1404 [0m[7m[0mtotal [0m[7m[0mtests [0m[7m[0mgreen. [0m[7m[0mSaving [0m[7m[0mthe [0m
[7m[0mdeep-review [0m[7m[0mfindings [0m[7m[0mcost [0m[7m[0mless [0m[7m[0mthan [0m[7m[0m30 [0m[7m[0mminutes; [0m[7m[0min-game [0m[7m[0mdiscovery [0m[7m[0mwould [0m[7m[0mhave [0m[7m[0mcost [0m[7m[0ma [0m[7m[0mplayer [0m[7m[0mhaving [0m[7m[0mtheir [0m[7m[0mmount [0m[7m[0msilently [0m
[7m[0mdisappear [0m[7m[0mwhen [0m[7m[0mvisiting [0m[7m[0mOrthanc [0m[7m[0mGate.[0m
[7m[0m  CHANGELOG.md:64:[0m
> CHANGELOG.md:65:### Feat: [7mSiegeDismount[0m — port external sibling module into Main/Features/[0m
[7m[0m  CHANGELOG.md:66:[0m
> CHANGELOG.md:67:Refactored the developer-built `[7mSiegeDismount[0m` [0m[7m[0mmodule [0m[7m[0m(one [0m[7m[0mof [0m[7m[0mseven [0m[7m[0mdropped [0m[7m[0mat [0m
[7m[0m`Downloads/Features_fixed/`) [0m[7m[0minto [0m[7m[0mTAOM's [0m[7m[0madapter [0m[7m[0m/ [0m[7m[0mservice [0m[7m[0m/ [0m[7m[0mIoC [0m[7m[0mpattern. [0m[7m[0mThe [0m[7m[0moriginal [0m[7m[0mwas [0m[7m[0ma [0m[7m[0mstandalone [0m[7m[0mBannerlord [0m
[7m[0mmodule [0m[7m[0mwith [0m[7m[0mits [0m[7m[0mown [0m[7m[0m`SubModule.xml`, [0m[7m[0m`MissionBehavior`, [0m[7m[0mand [0m[7m[0mMCM [0m[7m[0msettings; [0m[7m[0mthis [0m[7m[0mcommit [0m[7m[0mreplaces [0m[7m[0mit [0m[7m[0mwith [0m
[7m[0m`Main/Features/SiegeDismount/` [0m[7m[0mso [0m[7m[0mit [0m[7m[0mships [0m[7m[0mas [0m[7m[0mpart [0m[7m[0mof [0m[7m[0mthe [0m[7m[0mTAOM [0m[7m[0mDLL [0m[7m[0mwith [0m[7m[0mthe [0m[7m[0msame [0m[7m[0mMCM, [0m[7m[0mlogging, [0m[7m[0mand [0m[7m[0mtoggle [0m[7m[0mconventions [0m
[7m[0mas [0m[7m[0mthe [0m[7m[0mrest [0m[7m[0mof [0m[7m[0mTAOM.[0m
[7m[0m  CHANGELOG.md:68:[0m
  CHANGELOG.md:71:**Architecture:**
> CHANGELOG.md:72:- [`[7mSiegeDismount[0mService`](Main/Features/SiegeDismount/SiegeDismountService.cs) [0m[7m[0m— [0m[7m[0mpure [0m[7m[0mstate [0m
[7m[0mmachine, [0m[7m[0mfully [0m[7m[0munit-testable[0m
> CHANGELOG.md:73:- 
[`[7mSiegeDismount[0mMissionBehavior`](Main/Features/SiegeDismount/Hooks/SiegeDismountMissionBehavior.cs) [0m[7m[0m— [0m[7m[0mthin [0m[7m[0mengine [0m
[7m[0mbridge; [0m[7m[0mreads [0m[7m[0m`Mission.Current.IsSiegeBattle` [0m[7m[0m+ [0m[7m[0m`SceneName` [0m[7m[0mand [0m[7m[0mdelegates[0m
[7m [0m[7m [0m[7m[0mCHANGELOG.md:74:- [0m[7m[0m[`IPlayerMountAdapter`](Main/Adapters/IPlayerMountAdapter.cs) [0m[7m[0m+ [0m
[7m[0m[`IPartyMountInventoryAdapter`](Main/Adapters/IPartyMountInventoryAdapter.cs) [0m[7m[0m— [0m[7m[0mADR-007 [0m[7m[0mwrappers [0m[7m[0mover [0m
[7m[0m`Hero.MainHero.BattleEquipment` [0m[7m[0mand [0m[7m[0m`MobileParty.MainParty.ItemRoster`. [0m[7m[0mService [0m[7m[0mnever [0m[7m[0msees [0m[7m[0m`EquipmentElement` [0m[7m[0mor [0m
[7m[0m`ItemObject`[0m
> CHANGELOG.md:75:- [`IMountSnapshot`](Main/Features/[7mSiegeDismount[0m/Models/IMountSnapshot.cs) [0m[7m[0m— [0m[7m[0mopaque [0m[7m[0mtoken [0m[7m[0mbetween [0m
[7m[0madapter [0m[7m[0mand [0m[7m[0mservice[0m
[7m [0m[7m [0m[7m[0mCHANGELOG.md:76:- [0m[7m[0mMCM [0m[7m[0msettings [0m[7m[0mfolded [0m[7m[0minto [0m[7m[0m[`TaomSettings.cs`](Main/Features/TaomSettings.cs) [0m[7m[0munder [0m[7m[0mgroup [0m[7m[0m`Battle [0m
[7m[0mTactics [0m[7m[0m/ [0m[7m[0mSiege [0m[7m[0mDismount` [0m[7m[0m(3 [0m[7m[0msettings: [0m[7m[0mEnable, [0m[7m[0mBehavior [0m[7m[0mdropdown [0m[7m[0m0-3, [0m[7m[0mDebug)[0m
  CHANGELOG.md:78:
> CHANGELOG.md:79:**Logging:** every lifecycle event hits `IModLogger` per the mandatory cross-cutting logging 
contract from the integration plan. `LogInfo` on enable/disable + siege detection + restore. `LogDebug` (gated by 
`[7mSiegeDismount[0mDebug` [0m[7m[0mMCM [0m[7m[0mtoggle) [0m[7m[0mfor [0m[7m[0mper-mode [0m[7m[0mdecisions. [0m[7m[0m`LogError` [0m[7m[0mfor [0m[7m[0mall [0m[7m[0mcaught [0m[7m[0mexceptions [0m[7m[0mon [0m[7m[0madapter [0m[7m[0mcalls [0m[7m[0m— [0m[7m[0mnever [0m
[7m[0msilent.[0m
[7m[0m  CHANGELOG.md:80:[0m
> CHANGELOG.md:81:**Tests:** 24 unit tests in 
[`[7mSiegeDismount[0mServiceTests`](TAOM.Tests/Features/SiegeDismount/SiegeDismountServiceTests.cs) [0m[7m[0mcovering [0m[7m[0mdisable [0m[7m[0mpaths, [0m
[7m[0mall [0m[7m[0mfour [0m[7m[0mbehavior [0m[7m[0mmodes, [0m[7m[0mscene-name [0m[7m[0msiege [0m[7m[0mdetection [0m[7m[0m(5 [0m[7m[0mkeyword [0m[7m[0mvariants), [0m[7m[0midempotent [0m[7m[0mend, [0m[7m[0mand [0m[7m[0mfour [0m[7m[0mlogging [0m[7m[0mcontracts. [0m
[7m[0mBuild [0m[7m[0mgreen, [0m[7m[0m1340/1340 [0m[7m[0mtests [0m[7m[0mpass.[0m
[7m[0m  CHANGELOG.md:82:[0m
> CHANGELOG.md:83:Source material: [`Downloads/Features_fixed/_decompiled/[7mSiegeDismount[0m/SiegeDismount.decompiled.cs`](D[0m
[7m[0mownloads/Features_fixed/_decompiled/SiegeDismount/SiegeDismount.decompiled.cs). [0m[7m[0mOriginal [0m[7m[0mdeveloper's [0m[7m[0mbehavior [0m
[7m[0mpreserved [0m[7m[0mverbatim [0m[7m[0m— [0m[7m[0msame [0m[7m[0mmodes, [0m[7m[0msame [0m[7m[0mdefaults, [0m[7m[0msame [0m[7m[0mscene-name [0m[7m[0mkeywords.[0m
[7m[0m  CHANGELOG.md:84:[0m
  CHANGELOG.md:160:
> CHANGELOG.md:161:In-game testing showed the configured culture body never reached the FaceGen preview — the player 
saw the vanilla [7mstarting [0m[7m[0msilhouette [0m[7m[0mregardless [0m[7m[0mof [0m[7m[0mwhich [0m[7m[0mculture [0m[7m[0mthey [0m[7m[0mselected. [0m[7m[0mLogs [0m[7m[0mconfirmed [0m[7m[0mthe [0m[7m[0mpatch [0m[7m[0mfired [0m
[7m[0mcorrectly [0m[7m[0m(`Faction [0m[7m[0mconfirmed: [0m[7m[0mKingdom [0m[7m[0mof [0m[7m[0mRohan [0m[7m[0m-> [0m[7m[0mRohirrim` [0m[7m[0mfollowed [0m[7m[0mimmediately [0m[7m[0mby [0m[7m[0m`CCBodyPropertiesProvider: [0m[7m[0mLoaded [0m
[7m[0m1 [0m[7m[0mculture [0m[7m[0mbody-property [0m[7m[0mentries` [0m[7m[0mand [0m[7m[0m`CCBodyPropertiesService: [0m[7m[0mapplied [0m[7m[0mculture [0m[7m[0mbody [0m[7m[0mfor [0m[7m[0m'vlandia'`), [0m[7m[0mso [0m[7m[0mthe [0m[7m[0mchain [0m
[7m[0mProvider [0m[7m[0m→ [0m[7m[0mService [0m[7m[0m→ [0m[7m[0mAdapter [0m[7m[0mwas [0m[7m[0mintact. [0m[7m[0mThe [0m[7m[0mbreak [0m[7m[0mwas [0m[7m[0mat [0m[7m[0mthe [0m[7m[0mengine [0m[7m[0mboundary: [0m
[7m[0m`CharacterObject.UpdatePlayerCharacterBodyProperties` [0m[7m[0mis [0m[7m[0mfully [0m[7m[0mno-op'd [0m[7m[0mwhen [0m[7m[0mits [0m[7m[0minternal [0m[7m[0mguard [0m[7m[0m(`if [0m[7m[0m(IsPlayerCharacter [0m
[7m[0m&& [0m[7m[0mIsHero)`) [0m[7m[0mdoes [0m[7m[0mnot [0m[7m[0mpass.[0m
[7m[0m  CHANGELOG.md:162:[0m
  CHANGELOG.md:294:
> CHANGELOG.md:295:The visible-progress fix shipped one commit ago corrected a bug that should have been caught by 
`/deep-review` Agent 5 (Data Flow Tracing) and the prior Codex 2026-04-14 review — both walked happy-path examples 
[7mstarting [0m[7m[0mfrom [0m[7m[0m`count=100` [0m[7m[0mand [0m[7m[0mnever [0m[7m[0menumerated [0m[7m[0mthe [0m[7m[0m`count=0` [0m[7m[0mfirst-frame [0m[7m[0mstate [0m[7m[0mwhere [0m[7m[0mthe [0m[7m[0mbug [0m[7m[0mfires. [0m[7m[0mThe [0m[7m[0mpattern [0m[7m[0mis [0m[7m[0ma [0m
[7m[0m**state-machine [0m[7m[0msentinel [0m[7m[0mcollision** [0m[7m[0m— [0m[7m[0mthe [0m[7m[0m"uninitialized" [0m[7m[0msentinel [0m[7m[0mvalue [0m[7m[0m(`_lastShaderCount [0m[7m[0m= [0m[7m[0m-1`) [0m[7m[0mwas [0m
[7m[0mindistinguishable [0m[7m[0mfrom [0m[7m[0mthe [0m[7m[0mreal [0m[7m[0mterminal [0m[7m[0mvalue [0m[7m[0m(`0`) [0m[7m[0mwhen [0m[7m[0mcompared [0m[7m[0magainst [0m[7m[0mthe [0m[7m[0mfirst [0m[7m[0mpoll [0m[7m[0mobservation.[0m
[7m[0m  CHANGELOG.md:296:[0m
  CHANGELOG.md:1149:
> CHANGELOG.md:1150:- `TaomAllianceModel.GetScoreOf[7mStarting[0mAlliance` [0m[7m[0m— [0m[7m[0mremoved [0m[7m[0m`IFaction [0m[7m[0mevaluatingFaction` [0m[7m[0mparameter [0m
[7m[0m(dropped [0m[7m[0min [0m[7m[0mv1.4.0 [0m[7m[0mbase [0m[7m[0mclass)[0m
[7m [0m[7m [0m[7m[0mCHANGELOG.md:1151:- [0m[7m[0m`TaomBattleRewardModel.CalculateRenownGain` [0m[7m[0m— [0m[7m[0madded [0m[7m[0m`float [0m[7m[0mrenownMultiplierForWinnerSide` [0m[7m[0mand [0m
[7m[0m`bool [0m[7m[0mincludeDescriptions` [0m[7m[0mparameters [0m[7m[0m(added [0m[7m[0min [0m[7m[0mv1.4.0 [0m[7m[0mbase [0m[7m[0mclass)[0m
  CHANGELOG.md:1458:- Added all module strings: 17 lord names, 17 clan names, 52 NPC display names, kingdom/culture 
descriptors to `taom_module_strings.xml`
> CHANGELOG.md:1459:- Added `shaghana` and `abanissa` entries to `charactercreation/cultures.json` ([7mstarting [0m
[7m[0msettlements: [0m[7m[0mtown_A6 [0m[7m[0mZajâna [0m[7m[0m/ [0m[7m[0mtown_A14 [0m[7m[0mDamudûr)[0m
[7m[0m  CHANGELOG.md:1460:[0m
  CHANGELOG.md:1471:- **Banner keys**: All 17 new clan entries (clan_shaghana_1–9, clan_abanissa_1–8) had placeholder 
banner keys. Restored original keys copied from their source clans (clan_aserai_10–26) which held the real designed 
banners
> CHANGELOG.md:1472:- **Education templates**: Added 6 
`child_education_templates_stage_2_page_0_branch_{0-5}_{culture}` entries each for `Culture.shaghana` and 
`Culture.abanissa` to `taom_education_character_templates.xml` — without these the character creation education stage 
crashes for players [7mstarting [0m[7m[0mas [0m[7m[0mthese [0m[7m[0mcultures[0m
[7m [0m[7m [0m[7m[0mCHANGELOG.md:1473:- [0m[7m[0m**Removed [0m[7m[0mduplicate [0m[7m[0mclans**: [0m[7m[0mDeleted [0m[7m[0m`clan_aserai_10–26` [0m[7m[0mfrom [0m[7m[0m`clans.xml`, [0m[7m[0m`lord_A10_1–A26_1` [0m
[7m[0mfrom [0m[7m[0m`heroes.xml` [0m[7m[0mand [0m[7m[0m`lords.xml`. [0m[7m[0mThese [0m[7m[0mold [0m[7m[0maserai [0m[7m[0mentries [0m[7m[0mwere [0m[7m[0mnever [0m[7m[0mremoved [0m[7m[0mwhen [0m[7m[0mthe [0m[7m[0mnew [0m[7m[0m`clan_shaghana_*` [0m[7m[0m/ [0m
[7m[0m`clan_abanissa_*` [0m[7m[0mentries [0m[7m[0mwere [0m[7m[0mcreated, [0m[7m[0mcausing [0m[7m[0mall [0m[7m[0m26 [0m[7m[0mclans [0m[7m[0mto [0m[7m[0mappear [0m[7m[0munder [0m[7m[0mHarwan [0m[7m[0minstead [0m[7m[0mof [0m[7m[0m9[0m
  CHANGELOG.md:1609:- **Adult stage** (`GetAdultMenuNarrativeMenuCharacterArgs` line 2819): added Prefix returning 
`"player_adulthood_character"` (age 20)
> CHANGELOG.md:1610:- **Age selection stage** (`GetAgeSelectionMenuNarrativeMenuCharacterArgs` line 3298): added 
Prefix returning `"player_age_selection_character"` (age = `[7mStarting[0mAge`)[0m
[7m [0m[7m [0m[7m[0mCHANGELOG.md:1611:- [0m[7m[0m`Patch20_NarrativeHorseGuard` [0m[7m[0mnow [0m[7m[0mhas [0m[7m[0m4 [0m[7m[0mpatches [0m[7m[0m(3 [0m[7m[0mPrefixes [0m[7m[0m+ [0m[7m[0m1 [0m[7m[0mFinalizer) [0m[7m[0mcovering [0m[7m[0mall [0m[7m[0mcrash [0m
[7m[0msites [0m[7m[0m— [0m[7m[0mdecompilation [0m[7m[0mconfirmed [0m[7m[0mno [0m[7m[0mfurther [0m[7m[0mhorse-reading [0m[7m[0mmethods [0m[7m[0mexist [0m[7m[0min [0m[7m[0mthe [0m[7m[0mclass[0m
  CHANGELOG.md:1735:- **Tests:** 22 new tests covering config parsing, gold distribution, influence distribution, and 
behavior trigger logic
> CHANGELOG.md:1736:- **Ported from:** LOTRAOM's `StartupFunds` and `[7mStarting[0mInfluence` features[0m
[7m[0m  CHANGELOG.md:1737:[0m
  CHANGELOG.md:2103:- `SetPlayerRace()` uses first race from `CultureCreationData.Races[]` (defaults to "human" if 
empty/null)
> CHANGELOG.md:2104:- Called from `OnCharacterCreationFinalize()` after teleport to [7mstarting[0m settlement[0m
[7m [0m[7m [0m[7m[0mCHANGELOG.md:2105:- [0m[7m[0m`CharacterCreationContentServiceTests.cs` [0m[7m[0m— [0m[7m[0m5 [0m[7m[0mtests [0m[7m[0m(first [0m[7m[0mrace, [0m[7m[0msingle [0m[7m[0mrace, [0m[7m[0mempty/null [0m[7m[0mraces, [0m
[7m[0mlogging)[0m
  CHANGELOG.md:2609:
> CHANGELOG.md:2610:- Updated settlement data and clan/kingdom [7mstarting[0m positions[0m
[7m[0m  CHANGELOG.md:2611:- Updated `spclans.xslt` and `spkingdoms.xslt` with settlement references[0m
  CHANGELOG.md:2719:- Updated banner keys and kingdom color attributes
> CHANGELOG.md:2720:- Updated [7mstarting[0m positions for cultures and fixed Dol Guldur owner[0m
[7m [0m[7m [0m[7m[0mCHANGELOG.md:2721:- [0m[7m[0mCreated [0m[7m[0m`scripts/replace_equipment_templates.py` [0m[7m[0m— [0m[7m[0mreplaces [0m[7m[0mcustom [0m[7m[0mLOTRAOM [0m[7m[0mequipment [0m[7m[0mtemplates [0m
[7m[0mwith [0m[7m[0mvanilla [0m[7m[0mequivalents[0m


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Recurse -File Main\\Features\\CareerSystem | Select-String -Pattern 'GiveGold|Gold|Equipment|SelectedTitleType|BattleEquipment|ItemRoster|MemberRoster' | Select-Object Path,LineNumber,Line -First 100" in C:\Users\mikew\source\repos\TAOM
 succeeded in 454ms:

[32;1mPath                                                                                   [0m[32;1m LineNumber[0m[32;1m Line[0m
[32;1m----                                                                                   [0m [32;1m----------[0m [32;1m----[0m
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Domain\PassiveEffectType.cs         27     EquipmentWeight…


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content Main\\_Module\\ModuleData\\charactercreation\\cultures.json | Select-Object -First 120" in C:\Users\mikew\source\repos\TAOM
 succeeded in 437ms:
[
  {
    "culture_id": "gondor",
    "races": ["human"],
    "starting_settlement": "town_EW1",
    "default_age": 20.0,
    "default_weight": 0.0,
    "default_build": 0.7222,
    "focus_to_add": 1,
    "skill_level_to_add": 10
  },
  {
    "culture_id": "mordor",
    "races": ["uruk", "orc", "human"],
    "starting_settlement": "town_ES1",
    "default_age": 20.0,
    "default_weight": 0.5417,
    "default_build": 0.5231,
    "focus_to_add": 1,
    "skill_level_to_add": 10
  },
  {
    "culture_id": "erebor",
    "races": ["dwarf"],
    "starting_settlement": "town_E1",
    "default_age": 20.0,
    "default_weight": 0.5648,
    "default_build": 0.5347,
    "focus_to_add": 1,
    "skill_level_to_add": 10
  },
  {
    "culture_id": "rivendell",
    "races": ["elf", "human"],
    "starting_settlement": "town_R1",
    "default_age": 20.0,
    "default_weight": 0.0232,
    "default_build": 0.5347,
    "focus_to_add": 1,
    "skill_level_to_add": 10
  },
  {
    "culture_id": "mirkwood",
    "races": ["elf", "human"],
    "starting_settlement": "town_M1",
    "default_age": 20.0,
    "default_weight": 0.0232,
    "default_build": 0.5347,
    "focus_to_add": 1,
    "skill_level_to_add": 10
  },
  {
    "culture_id": "lothlorien",
    "races": ["elf", "human"],
    "starting_settlement": "town_L1",
    "default_age": 20.0,
    "default_weight": 0.0232,
    "default_build": 0.5347,
    "focus_to_add": 1,
    "skill_level_to_add": 10
  },
  {
    "culture_id": "isengard",
    "races": ["uruk_hai", "berserker", "human"],
    "starting_settlement": "town_isengard",
    "default_age": 20.0,
    "default_weight": 0.5417,
    "default_build": 0.5231,
    "focus_to_add": 1,
    "skill_level_to_add": 10
  },
  {
    "culture_id": "gundabad",
    "races": ["pale_uruk", "goblin", "orc", "human"],
    "starting_settlement": "town_G1",
    "default_age": 30.0,
    "default_weight": 0.5417,
    "default_build": 0.543,
    "focus_to_add": 1,
    "skill_level_to_add": 10
  },
  {
    "culture_id": "dolguldur",
    "races": ["dg_uruk", "goblin", "orc", "human"],
    "starting_settlement": "town_DG1",
    "default_age": 20.0,
    "default_weight": 0.5417,
    "default_build": 0.3125,
    "focus_to_add": 1,
    "skill_level_to_add": 10
  },
  {
    "culture_id": "umbar",
    "races": ["human"],
    "starting_settlement": "town_U1",
    "default_age": 20.0,
    "default_weight": 0.5417,
    "default_build": 0.5231,
    "focus_to_add": 1,
    "skill_level_to_add": 10
  },
  {
    "culture_id": "empire",
    "races": ["human"],
    "starting_settlement": "town_EN2",
    "default_age": 20.0,
    "default_weight": 0.0,
    "default_build": 0.7222,
    "focus_to_add": 1,
    "skill_level_to_add": 10
  },
  {
    "culture_id": "vlandia",
    "races": ["human"],
    "starting_settlement": "town_V1",
    "default_age": 20.0,
    "default_weight": 0.0,
    "default_build": 0.7222,
    "focus_to_add": 1,
    "skill_level_to_add": 10

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content Main\\_Module\\ModuleData\\charactercreation\\cultures.json | Select-Object -Skip 120 -First 120" in C:\Users\mikew\source\repos\TAOM
 succeeded in 426ms:
  },
  {
    "culture_id": "sturgia",
    "races": ["human"],
    "starting_settlement": "town_S1",
    "default_age": 20.0,
    "default_weight": 0.0,
    "default_build": 0.7222,
    "focus_to_add": 1,
    "skill_level_to_add": 10
  },
  {
    "culture_id": "aserai",
    "races": ["human"],
    "starting_settlement": "town_A1",
    "default_age": 20.0,
    "default_weight": 0.0,
    "default_build": 0.7222,
    "focus_to_add": 1,
    "skill_level_to_add": 10
  },
  {
    "culture_id": "shaghana",
    "races": ["human"],
    "starting_settlement": "town_A6",
    "default_age": 20.0,
    "default_weight": 0.5417,
    "default_build": 0.5231,
    "focus_to_add": 1,
    "skill_level_to_add": 10
  },
  {
    "culture_id": "abanissa",
    "races": ["human"],
    "starting_settlement": "town_A14",
    "default_age": 20.0,
    "default_weight": 0.5417,
    "default_build": 0.5231,
    "focus_to_add": 1,
    "skill_level_to_add": 10
  },
  {
    "culture_id": "battania",
    "races": ["human"],
    "starting_settlement": "town_K1",
    "default_age": 20.0,
    "default_weight": 0.0,
    "default_build": 0.7222,
    "focus_to_add": 1,
    "skill_level_to_add": 10
  },
  {
    "culture_id": "khuzait",
    "races": ["human"],
    "starting_settlement": "town_RU1",
    "default_age": 20.0,
    "default_weight": 0.0,
    "default_build": 0.7222,
    "focus_to_add": 1,
    "skill_level_to_add": 10
  }
]

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Main\\_Module\\ModuleData\\charactercreation\\youth_menu.json -Pattern '\"culture_id\": \"(shaghana|abanissa)\"' | Measure-Object; Select-String -Path Main\\_Module\\ModuleData\\equipmentsets\\taom_char_creation_equipment.xml -Pattern 'player_char_creation_(shaghana|abanissa)_' | Select-Object -First 5 LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
 succeeded in 561ms:

[32;1mCount             : [0m0
[32;1mAverage           : [0m
[32;1mSum               : [0m
[32;1mMaximum           : [0m
[32;1mMinimum           : [0m
[32;1mStandardDeviation : [0m
[32;1mProperty          : [0m

[32;1mLineNumber : [0m11047
[32;1mLine       : [0m		id="player_char_creation_shaghana_retainer_m"

[32;1mLineNumber : [0m11089
[32;1mLine       : [0m		id="player_char_creation_shaghana_retainer_f"

[32;1mLineNumber : [0m11131
[32;1mLine       : [0m		id="player_char_creation_shaghana_mercenary_m"

[32;1mLineNumber : [0m11173
[32;1mLine       : [0m		id="player_char_creation_shaghana_mercenary_f"

[32;1mLineNumber : [0m11215
[32;1mLine       : [0m		id="player_char_creation_shaghana_guard_m"


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem Main\\_Module\\ModuleData\\charactercreation\\*.json | Select-String -Pattern '\"culture_id\": \"(shaghana|abanissa)\"' | Select-Object Path,LineNumber,Line -First 20" in C:\Users\mikew\source\repos\TAOM
 succeeded in 460ms:

[32;1mPath                                                                                    [0m[32;1m LineNumber[0m[32;1m Line[0m
[32;1m----                                                                                    [0m [32;1m----------[0m [32;1m----[0m
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\cultures.json        143     "culture_id": …
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\charactercreation\cultures.json        153     "culture_id": …


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "@'
import json,pathlib,collections
for fname in ['parents','childhood','education','youth','adulthood']:
 p=pathlib.Path(f'Main/_Module/ModuleData/charactercreation/{fname}_menu.json')
 data=json.loads(p.read_text())
 counts=collections.Counter(e.get('culture_id') or '<generic>' for e in data)
 print(fname, counts)
'@ | python -" in C:\Users\mikew\source\repos\TAOM
 succeeded in 462ms:
parents Counter({'gondor': 6, 'mordor': 6, 'erebor': 6, 'rivendell': 6, 'mirkwood': 6, 'lothlorien': 6, 'isengard': 6, 'gundabad': 6, 'dolguldur': 6, 'umbar': 6, 'empire': 6, 'vlandia': 6, 'sturgia': 6, 'aserai': 6, 'battania': 6, 'khuzait': 6})
childhood Counter({'<generic>': 6})
education Counter({'gondor': 6, 'mordor': 6, 'erebor': 6, 'rivendell': 6, 'mirkwood': 6, 'lothlorien': 6, 'isengard': 6, 'gundabad': 6, 'dolguldur': 6, 'umbar': 6, 'empire': 6, 'vlandia': 6, 'sturgia': 6, 'aserai': 6, 'battania': 6, 'khuzait': 6})
youth Counter({'mordor': 6, 'erebor': 6, 'isengard': 6, 'gundabad': 6, 'dolguldur': 6, 'umbar': 6, 'empire': 6, 'vlandia': 6, 'sturgia': 6, 'aserai': 6, 'battania': 6, 'khuzait': 6, 'gondor': 5, 'rivendell': 5, 'mirkwood': 5, 'lothlorien': 5})
adulthood Counter({'gondor': 6, 'mordor': 6, 'erebor': 6, 'rivendell': 6, 'mirkwood': 6, 'lothlorien': 6, 'isengard': 6, 'gundabad': 6, 'dolguldur': 6, 'umbar': 6, 'empire': 6, 'vlandia': 6, 'sturgia': 6, 'aserai': 6, 'battania': 6, 'khuzait': 6})

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Main\\_Module\\ModuleData\\taom_spcultures.xml -Pattern 'id=\"(shaghana|abanissa)\"' -Context 0,1; Get-ChildItem Main\\_Module\\ModuleData -Recurse -File -Include *.xml,*.xslt | Select-String -Pattern 'id=\"(shaghana|abanissa)\"' | Select-Object Path,LineNumber,Line -First 20" in C:\Users\mikew\source\repos\TAOM
 succeeded in 536ms:

> Main\_Module\ModuleData\taom_spcultures.xml:3639:    [7mid="shaghana"[0m
[7m[0m  Main\_Module\ModuleData\taom_spcultures.xml:3640:    name="{=aom_shaghana_name}Shaghâna"[0m
> Main\_Module\ModuleData\taom_spcultures.xml:3903:    [7mid="abanissa"[0m
[7m[0m  Main\_Module\ModuleData\taom_spcultures.xml:3904:    name="{=aom_abanissa_name}Âbanissa"[0m

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\special_resources\special_resources_config.xml
[32;1mLineNumber : [0m89
[32;1mLine       : [0m    <Kingdom id="shaghana" />


[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\special_resources\special_resources_config.xml
[32;1mLineNumber : [0m90
[32;1mLine       : [0m    <Kingdom id="abanissa" />


[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\special_resources\special_resources_config.xml
[32;1mLineNumber : [0m92
[32;1mLine       : [0m    <Culture id="shaghana" />


[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\special_resources\special_resources_config.xml
[32;1mLineNumber : [0m93
[32;1mLine       : [0m    <Culture id="abanissa" />


[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml
[32;1mLineNumber : [0m3639
[32;1mLine       : [0m    id="shaghana"


[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spcultures.xml
[32;1mLineNumber : [0m3903
[32;1mLine       : [0m    id="abanissa"


[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spkingdoms.xml
[32;1mLineNumber : [0m716
[32;1mLine       : [0m        id="shaghana"


[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_spkingdoms.xml
[32;1mLineNumber : [0m811
[32;1mLine       : [0m        id="abanissa"



exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content Main'"\\_Module\\ModuleData\\startup_resources\\startup_resources_config.xml | ForEach-Object { '{0,4}: {1}' -f "'$i, $_; $i++ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 428ms:
   1: <?xml version="1.0" encoding="utf-8"?>
   2: <!--
   3:   StartupResources config — values applied at new-game start.
   4: 
   5:   Attributes:
   6:     gold        = denars granted to NPC lords of this culture (NOT player). Default 0.
   7:     influence   = influence granted to NPC lords of this culture. Default 0.
   8:     playerGold  = denars granted to the PLAYER hero at CC finalize, by selected culture.
   9:                   Range [0, 10_000_000]. Out-of-range or non-numeric values revert to 0
  10:                   with a logged warning. Default 0 (no warning when missing).
  11: 
  12:   Edits to this file take effect on the next Bannerlord process restart, not save reload.
  13: -->
  14: <StartupResources>
  15:   <!-- Elven cultures -->
  16:   <Culture id="rivendell"   gold="600000" influence="1000" playerGold="10000" />
  17:   <Culture id="lothlorien"  gold="600000" influence="1000" playerGold="10000" />
  18:   <Culture id="mirkwood"    gold="600000" influence="1000" playerGold="8000"  />
  19: 
  20:   <!-- Dwarven cultures -->
  21:   <Culture id="erebor"      gold="50000"  influence="150"  playerGold="7500"  />
  22: 
  23:   <!-- Human Good cultures -->
  24:   <Culture id="gondor"      gold="50000"  influence="500"  playerGold="5000"  />
  25:   <Culture id="vlandia"     gold="50000"  influence="50"   playerGold="5000"  />
  26:   <Culture id="sturgia"     gold="50000"  influence="50"   playerGold="5000"  />
  27: 
  28:   <!-- Human Evil / Eastern cultures -->
  29:   <Culture id="empire"      gold="50000"  influence="100"  playerGold="4000"  />
  30:   <Culture id="battania"    gold="50000"  influence="100"  playerGold="4000"  />
  31:   <Culture id="aserai"      gold="50000"  influence="100"  playerGold="4000"  />
  32:   <Culture id="khuzait"     gold="50000"  influence="1000" playerGold="4000"  />
  33: 
  34:   <!-- Orcish / Dark cultures -->
  35:   <Culture id="mordor"      gold="50000"  influence="500"  playerGold="6000"  />
  36:   <Culture id="isengard"    gold="200000" influence="2000" playerGold="6000"  />
  37:   <Culture id="gundabad"    gold="200000" influence="2000" playerGold="6000"  />
  38:   <Culture id="dolguldur"   gold="200000" influence="2000" playerGold="6000"  />
  39:   <Culture id="umbar"       gold="200000" influence="500"  playerGold="6000"  />
  40: </StartupResources>

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "@'
import json, pathlib, xml.etree.ElementTree as ET
cult=[e['culture_id'] for e in json.loads(pathlib.Path('Main/_Module/ModuleData/charactercreation/cultures.json').read_text())]
root=ET.parse('Main/_Module/ModuleData/startup_resources/startup_resources_config.xml').getroot()
conf=[e.attrib['id'] for e in root.findall('Culture')]
print('cc', cult)
print('conf', conf)
print('missing', sorted(set(cult)-set(conf)))
print('extra', sorted(set(conf)-set(cult)))
'@ | python -" in C:\Users\mikew\source\repos\TAOM
 succeeded in 507ms:
cc ['gondor', 'mordor', 'erebor', 'rivendell', 'mirkwood', 'lothlorien', 'isengard', 'gundabad', 'dolguldur', 'umbar', 'empire', 'vlandia', 'sturgia', 'aserai', 'shaghana', 'abanissa', 'battania', 'khuzait']
conf ['rivendell', 'lothlorien', 'mirkwood', 'erebor', 'gondor', 'vlandia', 'sturgia', 'empire', 'battania', 'aserai', 'khuzait', 'mordor', 'isengard', 'gundabad', 'dolguldur', 'umbar']
missing ['abanissa', 'shaghana']
extra []

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content Main'"\\Features\\Messengers\\MessengerCampaignBehavior.cs | ForEach-Object { if ("'$i -le 120 -or ($i -ge 410 -and $i -le 440)) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 434ms:
   1: using System.Collections.Generic;
   2: using TaleWorlds.CampaignSystem;
   3: using TaleWorlds.CampaignSystem.Actions;
   4: using TaleWorlds.CampaignSystem.Conversation;
   5: using TaleWorlds.CampaignSystem.Encounters;
   6: using TaleWorlds.CampaignSystem.GameState;
   7: using TaleWorlds.CampaignSystem.Party;
   8: using TaleWorlds.CampaignSystem.Settlements;
   9: using TaleWorlds.CampaignSystem.Settlements.Locations;
  10: using TaleWorlds.Core;
  11: using TaleWorlds.Library;
  12: using TaleWorlds.Localization;
  13: using TaleWorlds.MountAndBlade;
  14: using TAOM.Core.Logging;
  15: using TAOM.Features.Messengers.Domain;
  16: 
  17: namespace TAOM.Features.Messengers;
  18: 
  19: public class MessengerCampaignBehavior : CampaignBehaviorBase, IMissionListener
  20: {
  21:     private readonly IMessengerService _service;
  22:     private readonly IMessengerStateStore _store;
  23:     private readonly IMessengerSettingsProvider _settings;
  24:     private readonly IModLogger _logger;
  25: 
  26:     private static readonly MissionMode[] AllowedMissionModes = { MissionMode.Conversation, MissionMode.Barter };
  27: 
  28:     private bool _dialogsRegistered;
  29:     private bool _processingArrivedMessenger;
  30:     private PendingMessenger _activeMessenger;
  31:     private Mission _currentMission;
  32:     private Vec2 _originalPosition = Vec2.Invalid;
  33: 
  34:     public MessengerCampaignBehavior(
  35:         IMessengerService service,
  36:         IMessengerStateStore store,
  37:         IMessengerSettingsProvider settings,
  38:         IModLogger logger)
  39:     {
  40:         _service = service;
  41:         _store = store;
  42:         _settings = settings;
  43:         _logger = logger;
  44:     }
  45: 
  46:     // --- CampaignBehaviorBase ---
  47: 
  48:     public override void RegisterEvents()
  49:     {
  50:         CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
  51:         CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
  52:     }
  53: 
  54:     public override void SyncData(IDataStore dataStore)
  55:     {
  56:         if (dataStore.IsSaving)
  57:         {
  58:             var snapshot = _store.Serialize();
  59:             dataStore.SyncData("_taom_messengers", ref snapshot);
  60:         }
  61:         else
  62:         {
  63:             Dictionary<string, string> snapshot = null;
  64:             dataStore.SyncData("_taom_messengers", ref snapshot);
  65:             _store.Deserialize(snapshot);
  66:             _processingArrivedMessenger = false;
  67:             _activeMessenger = null;
  68:             _currentMission = null;
  69:             _originalPosition = Vec2.Invalid;
  70:         }
  71:     }
  72: 
  73:     // --- Public API (callable by other features) ---
  74: 
  75:     public void SendMessenger(Hero targetHero)
  76:     {
  77:         var snapshot = SnapshotHero(targetHero);
  78:         var playerGold = Hero.MainHero?.Gold ?? 0;
  79:         var validation = _service.CanSendMessenger(snapshot, playerGold);
  80:         if (validation != MessengerValidationResult.Ok)
  81:         {
  82:             ShowInquiry(
  83:                 new TextObject("{=taom_messenger_cannot_send}Cannot Send Messenger").ToString(),
  84:                 BuildValidationReason(validation, targetHero).ToString(),
  85:                 affirmative: GameTexts.FindText("str_ok").ToString());
  86:             return;
  87:         }
  88: 
  89:         GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, _settings.MessengerGoldCost, false);
  90: 
  91:         var startPosition = Hero.MainHero?.GetMapPoint()?.Position.ToVec2() ?? Vec2.Zero;
  92:         var messenger = new PendingMessenger(
  93:             targetHeroId: targetHero.StringId,
  94:             dispatchTimeDays: CampaignTime.Now.ToDays,
  95:             position: startPosition,
  96:             arrived: false);
  97:         _store.Add(messenger);
  98: 
  99:         var sentText = new TextObject("{=taom_messenger_sent}A messenger has been dispatched to {HERO_NAME} and will arrive within {DAYS} days.");
 100:         sentText.SetTextVariable("HERO_NAME", targetHero.Name);
 101:         sentText.SetTextVariable("DAYS", _settings.MessengerTravelDays);
 102:         ShowInquiry(
 103:             new TextObject("{=taom_messenger_sent_title}Messenger Sent").ToString(),
 104:             sentText.ToString(),
 105:             affirmative: GameTexts.FindText("str_ok").ToString());
 106:     }
 107: 
 108:     public bool CanSendMessenger(Hero targetHero, out TextObject reason)
 109:     {
 110:         var validation = _service.CanSendMessenger(SnapshotHero(targetHero), Hero.MainHero?.Gold ?? 0);
 111:         if (validation == MessengerValidationResult.Ok)
 112:         {
 113:             reason = TextObject.GetEmpty();
 114:             return true;
 115:         }
 116:         reason = BuildValidationReason(validation, targetHero);
 117:         return false;
 118:     }
 119: 
 120:     // --- Lifecycle ---
 410: 
 411:         starter.AddDialogLine(
 412:             "taom_messenger_dialog_decline_ack",
 413:             "taom_messenger_dialog_decline_ack",
 414:             "close_window",
 415:             "{=taom_messenger_dialog_decline_ack}As you wish.",
 416:             null, null);
 417:     }
 418: 
 419:     private bool DialogCondition_CanSend()
 420:     {
 421:         var hero = Hero.OneToOneConversationHero;
 422:         if (hero == null) return false;
 423:         return _service.CanSendMessenger(SnapshotHero(hero), Hero.MainHero?.Gold ?? 0) == MessengerValidationResult.Ok;
 424:     }
 425: 
 426:     private bool DialogCondition_HasGold()
 427:     {
 428:         MBTextManager.SetTextVariable("COST", _settings.MessengerGoldCost.ToString());
 429:         return (Hero.MainHero?.Gold ?? 0) >= _settings.MessengerGoldCost;
 430:     }
 431: 
 432:     private void DialogConsequence_DispatchMessenger()
 433:     {
 434:         var hero = Hero.OneToOneConversationHero;
 435:         if (hero == null) return;
 436:         SendMessenger(hero);
 437:     }
 438: 
 439:     // --- Helpers ---
 440: 

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; ilspycmd -t TaleWorlds.CampaignSystem.Campaign "'$dll | Select-String -Pattern '"'GetCampaignBehavior' -Context 1" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1356ms:

  
> 	public T [7mGetCampaignBehavior[0m<T>()[0m
[7m[0m  	{[0m
  
> 	public IEnumerable<T> [7mGetCampaignBehavior[0ms<T>()[0m
[7m[0m  	{[0m
  		}
> 		foreach (INonReadyObjectHandler campaignBehavior in [7mGetCampaignBehavior[0ms<INonReadyObjectHandler>())[0m
[7m[0m  		{[0m


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content Main'"\\IoC.cs | ForEach-Object { if ("'$i -ge 74 -and $i -le 82) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 444ms:
  74:         SpecialResourcesIoC.RegisterSpecialResourcesFeature(container);
  75:         CareerSystemIoC.RegisterCareerSystemFeature(container);
  76:         SettlementGuardsIoC.RegisterSettlementGuardsFeature(container);
  77:         NamedCompanionIoC.RegisterNamedCompanionsFeature(container);
  78:         RevoltTuningIoC.RegisterRevoltTuningFeature(container);
  79:         SiegeDismountIoC.RegisterSiegeDismountFeature(container);
  80:         MessengerIoC.RegisterMessengerFeature(container);
  81: 
  82:         _container = container;

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content Main'"\\Features\\Messengers\\MessengerConfigProvider.cs | ForEach-Object { '{0,4}: {1}' -f "'$i, $_; $i++ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 437ms:
   1: using System;
   2: using System.IO;
   3: using Newtonsoft.Json;
   4: using TAOM.Core.Infrastructure;
   5: using TAOM.Core.Logging;
   6: 
   7: namespace TAOM.Features.Messengers;
   8: 
   9: public class MessengerConfigProvider : IMessengerConfigProvider
  10: {
  11:     private readonly IPathService _pathService;
  12:     private readonly IModLogger _logger;
  13:     private readonly Lazy<MessengerConfig> _config;
  14: 
  15:     public MessengerConfigProvider(IPathService pathService, IModLogger logger)
  16:     {
  17:         _pathService = pathService;
  18:         _logger = logger;
  19:         _config = new Lazy<MessengerConfig>(LoadConfig);
  20:     }
  21: 
  22:     public MessengerConfig GetConfig() => _config.Value;
  23: 
  24:     private MessengerConfig LoadConfig()
  25:     {
  26:         var path = Path.Combine(_pathService.ModuleDataPath, "messengers", "messenger_config.json");
  27: 
  28:         if (!File.Exists(path))
  29:         {
  30:             _logger.LogInfo($"MessengerConfigProvider: messenger_config.json not found at {path}, using defaults");
  31:             return new MessengerConfig();
  32:         }
  33: 
  34:         MessengerConfig parsed;
  35:         try
  36:         {
  37:             var json = File.ReadAllText(path);
  38:             parsed = JsonConvert.DeserializeObject<MessengerConfig>(json) ?? new MessengerConfig();
  39:         }
  40:         catch (Exception ex)
  41:         {
  42:             _logger.LogError($"MessengerConfigProvider: Failed to parse messenger_config.json: {ex.Message}");
  43:             return new MessengerConfig();
  44:         }
  45: 
  46:         return Validate(parsed);
  47:     }
  48: 
  49:     private MessengerConfig Validate(MessengerConfig parsed)
  50:     {
  51:         var sanitized = new MessengerConfig
  52:         {
  53:             AccidentChancePerHour = parsed.AccidentChancePerHour,
  54:             TravelSpeedMultiplier = parsed.TravelSpeedMultiplier,
  55:         };
  56: 
  57:         var defaults = new MessengerConfig();
  58:         var rejected = false;
  59: 
  60:         if (sanitized.AccidentChancePerHour < 0f || sanitized.AccidentChancePerHour > 1f)
  61:         {
  62:             _logger.LogWarning($"MessengerConfigProvider: accidentChancePerHour={sanitized.AccidentChancePerHour} outside [0,1], reverting to default {defaults.AccidentChancePerHour}");
  63:             sanitized.AccidentChancePerHour = defaults.AccidentChancePerHour;
  64:             rejected = true;
  65:         }
  66: 
  67:         if (sanitized.TravelSpeedMultiplier < 0.1f || sanitized.TravelSpeedMultiplier > 10f)
  68:         {
  69:             _logger.LogWarning($"MessengerConfigProvider: travelSpeedMultiplier={sanitized.TravelSpeedMultiplier} outside [0.1,10], reverting to default {defaults.TravelSpeedMultiplier}");
  70:             sanitized.TravelSpeedMultiplier = defaults.TravelSpeedMultiplier;
  71:             rejected = true;
  72:         }
  73: 
  74:         if (rejected)
  75:             _logger.LogWarning("MessengerConfigProvider: messenger_config.json contained invalid values. See prior warnings for details.");
  76:         else
  77:             _logger.LogInfo("MessengerConfigProvider: Loaded messenger_config.json");
  78: 
  79:         return sanitized;
  80:     }
  81: }

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Recurse -File Main\\Features | Select-String -Pattern 'Vec2' | Select-Object Path,LineNumber,Line -First 20" in C:\Users\mikew\source\repos\TAOM
 succeeded in 437ms:

[32;1mPath                                                                                               [0m[32;1m LineNumber[0m[32;1m Line[0m
[32;1m----                                                                                               [0m [32;1m----------[0m [32;1m----[0m
C:\Users\mikew\source\repos\TAOM\Main\Features\AdvancedCombat\AutonomousMovementPlayerController.cs         24     pri…
C:\Users\mikew\source\repos\TAOM\Main\Features\AdvancedCombat\AutonomousMovementPlayerController.cs         26        …
C:\Users\mikew\source\repos\TAOM\Main\Features\AdvancedCombat\AutonomousMovementPlayerController.cs         27        …
C:\Users\mikew\source\repos\TAOM\Main\Features\AdvancedCombat\AutonomousMovementPlayerController.cs         28        …
C:\Users\mikew\source\repos\TAOM\Main\Features\AdvancedCombat\AutonomousMovementPlayerController.cs         31        …
C:\Users\mikew\source\repos\TAOM\Main\Features\AdvancedCombat\AutonomousMovementPlayerController.cs         32        …
C:\Users\mikew\source\repos\TAOM\Main\Features\AdvancedCombat\AutonomousMovementPlayerController.cs         33        …
C:\Users\mikew\source\repos\TAOM\Main\Features\AdvancedCombat\AutonomousMovementPlayerController.cs         35        …
C:\Users\mikew\source\repos\TAOM\Main\Features\AdvancedCombat\AutonomousMovementPlayerController.cs         37        …
C:\Users\mikew\source\repos\TAOM\Main\Features\AdvancedCombat\AutonomousMovementPlayerController.cs         38        …
C:\Users\mikew\source\repos\TAOM\Main\Features\AdvancedCombat\AutonomousMovementPlayerController.cs         42        …
C:\Users\mikew\source\repos\TAOM\Main\Features\AdvancedCombat\AutonomousMovementPlayerController.cs        325        …
C:\Users\mikew\source\repos\TAOM\Main\Features\AdvancedCombat\AutonomousMovementPlayerController.cs        326        …
C:\Users\mikew\source\repos\TAOM\Main\Features\AdvancedCombat\AutonomousMovementPlayerController.cs        329        …
C:\Users\mikew\source\repos\TAOM\Main\Features\AdvancedCombat\AutonomousMovementPlayerController.cs        330        …
C:\Users\mikew\source\repos\TAOM\Main\Features\AdvancedCombat\AutonomousMovementPlayerController.cs        395        …
C:\Users\mikew\source\repos\TAOM\Main\Features\AdvancedCombat\AutonomousMovementPlayerController.cs        403        …
C:\Users\mikew\source\repos\TAOM\Main\Features\AdvancedCombat\AutonomousMovementPlayerController.cs        410        …
C:\Users\mikew\source\repos\TAOM\Main\Features\AdvancedCombat\AutonomousMovementPlayerController.cs        415     pri…
C:\Users\mikew\source\repos\TAOM\Main\Features\AdvancedCombat\AutonomousMovementPlayerController.cs        429     pri…


codex
The StartingEquipmentGold path still has a wrong v1.3.15 equipment fallback guard and misses two selectable cultures in the new playerGold config. The current workspace also includes Messenger changes that register services/UI without installing the campaign behavior and violate TAOM entry-point/config-validation rules.

Full review comments:

- [P1] Guard civilian equipment against the civilian dead singleton — C:\Users\mikew\source\repos\TAOM\Main\Adapters\PlayerEquipmentAdapter.cs:32-33
  In v1.3.15 `Hero.CivilianEquipment` decompiles to `_civilianEquipment ?? Campaign.Current.DeadCivilianEquipment`, but this branch compares it to the `DeadBattleEquipment` value stored in `dead`. If this adapter is called for a hero whose civilian equipment is uninitialized, the condition passes and `FillFrom` mutates the shared `DeadCivilianEquipment`, so later dead/uninitialized heroes can inherit the player's civilian kit; compare against `Campaign.Current.DeadCivilianEquipment` before filling the civilian slot.

- [P2] Seed player gold for all selectable cultures — C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\startup_resources\startup_resources_config.xml:31-32
  `charactercreation/cultures.json` registers `shaghana` and `abanissa` as selectable cultures, but the new `playerGold` table only covers the 16 rows here. Selecting either missing culture makes `PlayerStartupGoldService` find no entry and grant 0 starting gold, unlike every other character-creation culture; add rows or intentionally remove/block those cultures from this feature.

- [P1] Add the messenger campaign behavior to game start — C:\Users\mikew\source\repos\TAOM\Main\IoC.cs:80-80
  This registers the Messenger services, and the encyclopedia mixin later calls `Campaign.Current.GetCampaignBehavior<MessengerCampaignBehavior>()`, but no code adds `MessengerCampaignBehavior` to `CampaignGameStarter` in `SubModule.OnGameStart`. In a campaign the lookup is therefore always null, so the new UI stays disabled and the dialog/hourly tick hooks never register; add the behavior alongside the other campaign behaviors.

- [P2] Keep MessengerCampaignBehavior as a thin entry point — C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerCampaignBehavior.cs:19-19
  When this behavior is added, it is a 450+ line `CampaignBehavior` that owns validation, gold transfer, travel simulation, conversation startup, dialog construction, UI inquiries, and persistence cleanup. ADR-002 requires `CampaignBehavior` entry points to stay thin (<150 lines) and delegate business logic to services, so this should be split into services/hooks before shipping.

- [P2] Reject NaN messenger config floats — C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerConfigProvider.cs:60-68
  `JsonConvert` can populate these floats with `NaN`, and comparisons like `< 0f` or `> 10f` are false for `NaN`, so a user-edited `messenger_config.json` can pass validation with a non-finite `TravelSpeedMultiplier` or accident chance. That poisons speed/position math or silently disables accident rolls; check `float.IsNaN`/`IsInfinity` before the range gates.
SUCCESS: The process with PID 72004 (child process of PID 75060) has been terminated.
SUCCESS: The process with PID 75060 (child process of PID 78948) has been terminated.
SUCCESS: The process with PID 78948 (child process of PID 49156) has been terminated.
SUCCESS: The process with PID 49156 (child process of PID 60192) has been terminated.
SUCCESS: The process with PID 60192 (child process of PID 68664) has been terminated.
SUCCESS: The process with PID 36404 (child process of PID 79764) has been terminated.
SUCCESS: The process with PID 79764 (child process of PID 27692) has been terminated.
SUCCESS: The process with PID 27692 (child process of PID 78620) has been terminated.
SUCCESS: The process with PID 78620 (child process of PID 68664) has been terminated.
SUCCESS: The process with PID 75508 (child process of PID 35788) has been terminated.
SUCCESS: The process with PID 35788 (child process of PID 59876) has been terminated.
SUCCESS: The process with PID 59876 (child process of PID 44528) has been terminated.
SUCCESS: The process with PID 44528 (child process of PID 68664) has been terminated.
SUCCESS: The process with PID 67452 (child process of PID 27696) has been terminated.
SUCCESS: The process with PID 27696 (child process of PID 73448) has been terminated.
SUCCESS: The process with PID 73448 (child process of PID 77856) has been terminated.
SUCCESS: The process with PID 77856 (child process of PID 59896) has been terminated.
SUCCESS: The process with PID 59896 (child process of PID 68664) has been terminated.
The StartingEquipmentGold path still has a wrong v1.3.15 equipment fallback guard and misses two selectable cultures in the new playerGold config. The current workspace also includes Messenger changes that register services/UI without installing the campaign behavior and violate TAOM entry-point/config-validation rules.

Full review comments:

- [P1] Guard civilian equipment against the civilian dead singleton — C:\Users\mikew\source\repos\TAOM\Main\Adapters\PlayerEquipmentAdapter.cs:32-33
  In v1.3.15 `Hero.CivilianEquipment` decompiles to `_civilianEquipment ?? Campaign.Current.DeadCivilianEquipment`, but this branch compares it to the `DeadBattleEquipment` value stored in `dead`. If this adapter is called for a hero whose civilian equipment is uninitialized, the condition passes and `FillFrom` mutates the shared `DeadCivilianEquipment`, so later dead/uninitialized heroes can inherit the player's civilian kit; compare against `Campaign.Current.DeadCivilianEquipment` before filling the civilian slot.

- [P2] Seed player gold for all selectable cultures — C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\startup_resources\startup_resources_config.xml:31-32
  `charactercreation/cultures.json` registers `shaghana` and `abanissa` as selectable cultures, but the new `playerGold` table only covers the 16 rows here. Selecting either missing culture makes `PlayerStartupGoldService` find no entry and grant 0 starting gold, unlike every other character-creation culture; add rows or intentionally remove/block those cultures from this feature.

- [P1] Add the messenger campaign behavior to game start — C:\Users\mikew\source\repos\TAOM\Main\IoC.cs:80-80
  This registers the Messenger services, and the encyclopedia mixin later calls `Campaign.Current.GetCampaignBehavior<MessengerCampaignBehavior>()`, but no code adds `MessengerCampaignBehavior` to `CampaignGameStarter` in `SubModule.OnGameStart`. In a campaign the lookup is therefore always null, so the new UI stays disabled and the dialog/hourly tick hooks never register; add the behavior alongside the other campaign behaviors.

- [P2] Keep MessengerCampaignBehavior as a thin entry point — C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerCampaignBehavior.cs:19-19
  When this behavior is added, it is a 450+ line `CampaignBehavior` that owns validation, gold transfer, travel simulation, conversation startup, dialog construction, UI inquiries, and persistence cleanup. ADR-002 requires `CampaignBehavior` entry points to stay thin (<150 lines) and delegate business logic to services, so this should be split into services/hooks before shipping.

- [P2] Reject NaN messenger config floats — C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerConfigProvider.cs:60-68
  `JsonConvert` can populate these floats with `NaN`, and comparisons like `< 0f` or `> 10f` are false for `NaN`, so a user-edited `messenger_config.json` can pass validation with a non-finite `TravelSpeedMultiplier` or accident chance. That poisons speed/position math or silently disables accident rolls; check `float.IsNaN`/`IsInfinity` before the range gates.
