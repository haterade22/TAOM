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
