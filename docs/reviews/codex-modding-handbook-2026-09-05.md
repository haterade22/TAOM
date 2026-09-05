# Codex adversarial review: the modder handbook (docs/modding/)

Run 2026-09-05 against HEAD `60d9761` with `gpt-5.6-sol` at max reasoning effort, 1.7M tokens.
Prompt: [codex-adversarial-modding-handbook-2026-09-05.prompt.md](codex-adversarial-modding-handbook-2026-09-05.prompt.md).

**Verdict: 3 CRITICAL, 11 HIGH, 4 MEDIUM, 0 LOW.** Codex made no changes; every finding below was
re-verified against the engine or the files before anything was edited, per
`.claude/rules/evidence-over-claims.md`. The five load-bearing ones were confirmed by hand first:
managed `Monster` registration (`Game.cs:307,437`), `AlwaysPreferMerge` on party-template stacks
(`partyTemplates.xsd:14`), TAOM's `TaomMilitaryPowerModel` registration (`Main/SubModule.cs:968`),
the vanilla volunteer fallback (`TaomVolunteerModel.GetBasicVolunteer`), and `AlwaysPreferMerge`
throughout `Items.xsd`.

**Why the gates missed all of it, and that is the useful part.** Both handbook gates pass on every
one of these chapters. `check_handbook_attributes.py` proves an attribute table matches the
deserializer that produced it, and `lint_handbook.py` proves the contract holds. Neither can judge a
sentence of prose about how the engine behaves, which is where all 18 findings live. Codex says so
itself in its closing section. A mechanical gate and an adversarial reader catch disjoint classes of
error, and this run is the evidence.

The raw transcript, including every command Codex ran, is at
`docs/reviews/raw/codex-adversarial-modding-handbook-2026-09-05.md` (gitignored, 2.4 MB).

---

Engine citations below are relative to `E:\Decompiled_Bannerlord\_categories_v1.4.8\`; schema citations are under the game-root `XmlSchemas\`. Review was against HEAD `60d9761`. No files were changed.

## CRITICAL

[CRITICAL] `docs/modding/recipe-new-mod-from-zero.md:143,161,218-364` — Validation scope — The recipe promises that each `Check:` proves the reader’s new module, but the commands mostly inspect TAOM’s existing files. `validate_moduledata.py:49-50,93-106` defaults to `Main/_Module/ModuleData`; `taom_schema.py:1588-1597,1704-1733` hardcodes TAOM/vanilla module names and filenames; `check_external_xslt.py:42-63` checks only repo TAOM, TAOM_Map and Armory; `validate_mesh_refs.py:82-93` defaults to Armory. Worst, Stage 16 warns that a missing `game_entity` crashes map loading, but `audit_scene_names.py:25-35,84-100` only checks four hardcoded settlement files’ interior `scene_name` values against `SceneObj` folders. It never examines `<YourModule>` or settlement entities in `Main_map`. This can produce a green check immediately before the documented crash. — Corrected text: “These are TAOM-only gates. They do not validate `<YourModule>`. Stage 16 requires a checker that accepts the new settlement XML and verifies every settlement ID against `Main_map` game entities; run the separate `scene_name` audit for interior scenes.”

[CRITICAL] `docs/modding/recipe-new-mod-from-zero.md:181-200` — Monster registration — Stage 6 states that monsters are not read through `<Xmls>` and belong only in `project.mbproj`. Managed monsters are explicitly registered as `RegisterType<Monster>("Monster", "Monsters", 2u)` and loaded through `LoadXML("Monsters")` at `Core/TaleWorlds.Core/TaleWorlds.Core/Game.cs:304-320,435-445`. `MountAndBlade/.../FaceGen.cs:34-59` then resolves them from `Game.Current.ObjectManager`. The live Armory correctly registers them through `<XmlName id="Monsters">` at `LOTRLOME_Armory/SubModule.xml:215-295`; its own `project.mbproj:17-23` says the managed registration is what loads the spider Monster. Following the chapter from an empty module leaves custom races without their managed Monster objects and exposes null dereferences during character/agent construction. — Corrected text: “Register every managed `<Monster>` file under `<Xmls>` with `id="Monsters"`. `project.mbproj` separately registers native skin/action/usage data; some creature integrations need both paths.”

[CRITICAL] `docs/modding/recipe-new-mod-from-zero.md:147-159`; `docs/modding/module-armory.md:82-86` — XSLT passthrough order — “Keep `<xsl:apply-templates select="@*|node()"/>` last in every template” is unsafe. If children have already been emitted, applying `@*` attempts to add attributes after children; if replacement attributes were emitted first, copying the original attributes afterward can restore the vanilla value. TAOM’s working stylesheet does the opposite: identity transform at `Main/_Module/ModuleData/spcultures.xslt:6-10`, attribute passthrough before overrides at `:14-23`, and selective passthrough of only untouched children at `:304-317`. The live `weapon_descriptions.xslt:672-683` also proves child order is feature-specific rather than universally “passthrough last.” — Corrected text: “Use a global identity transform. In a specialized template, copy `@*` before emitting children, then emit replacement attributes, and pass through only unmodified children in the position their schema requires.”

## HIGH

[HIGH] `docs/modding/party-templates.md:19-31` — Same-ID merge — The chapter says a later same-ID template replaces the earlier stack list and XML cannot append one stack. `partyTemplates.xsd:8-34` marks `<stacks>` `AlwaysPreferMerge`; `<PartyTemplateStack>` has no unique key; `MBObjectManager.cs:820-874` consequently appends later stack children. `PartyTemplateObject.Deserialize` resets its list at `PartyTemplateObject.cs:26-30`, but only after the engine has created one merged XML node containing both modules’ stacks. This can silently double or inflate a spawn roster. — Corrected text: “A later same-ID template appends its stacks to the earlier template. Put `_replaceWhileMerging="true"` on the later `<MBPartyTemplate>` when a full replacement is intended.”

[HIGH] `docs/modding/party-templates.md:73-74,89-93` — `max_value` ceiling — `max_value` is not an unconditional spawn ceiling. The shared ratio and interpolation are correct at `DefaultPartySizeLimitModel.cs:427-448`, but villager parties are subsequently multiplied by the governor’s Village Network perk at `:449-455`; that perk is +10% at `DefaultPerks.cs:2149`. — Corrected text: “`max_value` is the stack’s pre-modifier interpolation ceiling, not the party’s later size limit. Village Network can raise a villager stack above it.”

[HIGH] `docs/modding/kingdoms.md:197-203` — Kingdom merge and schema — The chapter says omitted attributes on a second same-ID kingdom revert to defaults and that Kingdoms has no schema. The install contains `XmlSchemas/Kingdoms.xsd`; Kingdom IDs are unique at `:138-142`, while relationships and policies are merge containers at `:11-73`. `MBObjectManager.MergeElementAttributes` changes only attributes present on the later row (`MBObjectManager.cs:799-817`), so omitted values survive from the earlier module. — Corrected text: “Same-ID kingdom rows schema-merge before one deserialization. Later attributes overwrite individually; omitted attributes remain inherited. `Kingdoms.xsd` lives at the game root, not under `Modules`.”

[HIGH] `docs/modding/items-armor.md:42,253-256`; `docs/modding/module-armory.md:318-323` — Vanilla item overrides — The handbook says a vanilla item can only be changed by editing `SandBoxCore` and describes duplicate item definitions as one silently shadowing the other. `Items.xsd:15-24,239-280,499-509` gives `Item@id` a unique key and marks item components, Armor and Weapon for merge. `MBObjectManager.cs:799-874` layers the later module’s attributes and children onto the earlier item. Directly editing the base-game module is unnecessary and is vulnerable to Steam verification or game updates. — Corrected text: “Do not edit SandBoxCore. Define the same `Item@id` in a later module; it produces a hybrid schema merge. Use a complete row or `_replaceWhileMerging="true"` when inheritance is not intended.”

[HIGH] `docs/modding/banners-and-heraldry.md:73-85,218-222`; `docs/modding/load-order-and-dependencies.md:165` — Banner collision precedence — The blanket “earlier module wins; an icon/background cannot be overridden” rule ignores the XML merge that occurs before `BannerManager`. `BannerIcons.xsd:6-11,39-47,77-81` keys groups and their children. A later icon or background with the same ID in the same group is merged and its attributes win through `MBObjectManager.cs:799-817`; only duplicate IDs that survive in different groups reach `BannerIconGroup.Deserialize`’s global first-wins guard (`BannerIconGroup.cs:47-72`). The chapter already recognizes this pre-parser collapse for `<Color>` but incorrectly excludes icons and backgrounds. — Corrected text: “Same group plus same child ID: later attributes win during XML merge. Same icon/background ID in different groups: the first parsed group wins.”

[HIGH] `docs/modding/balance-levers.md:20-30,50,180-186,268` — Auto-resolve formula and model — The chapter labels `CharacterObject.GetPower`’s `(2+t)*(8+t)*.02` as auto-resolve and says TAOM does not override that model. Actual simulated hits call `MilitaryPowerModel.GetTroopPower` at `DefaultCombatSimulationModel.cs:18-22`; vanilla base power is `(2+t)*(10+t)*.02` at `DefaultMilitaryPowerModel.cs:244-252`. TAOM overrides it in `Main/Features/BattleBalance/Models/TaomMilitaryPowerModel.cs:19-55`, registers that model at `Main/SubModule.cs:968`, and ships custom power enabled at `Main/Features/TaomSettings.cs:243-280`. For ordinary troops, mounted status comes from `default_group` (`BasicCharacterObject.cs:489-496`); only heroes derive it from their Horse slot (`CharacterObject.cs:318-327`). The worked T4 and mounted T5 values are therefore 1.68 and 2.52 under default settings, not 1.44 and 2.184. — Corrected text: replace the formula, examples, mount source, and “not overridden” statement with the MilitaryPowerModel path and TAOM’s T7–T10/MCM behavior.

[HIGH] `docs/modding/equipment-rosters.md:370-376` — Equipment-set sampling — It says three roster picks are reused for slots 0–1, 2–3 and 4–11. That is only the `seed == -1` path. With a real seed, all three indices are rerolled inside every slot iteration at `Equipment.cs:571-578`; the relevant one is then used for that slot at `:579-591`. Mission spawning passes `AgentEquipmentSeed` at `Mission.cs:4202-4206`. Thus the chapter’s headline consequence is right—each slot can come from a different set and can be empty—but its three-bucket mechanism is wrong. — Corrected text: “Normal campaign agents make a fresh deterministic source-set draw for every slot. The three grouped picks are reused only by callers passing `seed=-1`.”

[HIGH] `docs/modding/recipe-new-mod-from-zero.md:26-37,269-283` — Engine load order and reference prerequisite — The recipe puts `SPCultures` first and says references resolve only against objects already loaded. On both new and saved campaigns, `InitializeDefaultCampaignObjects` loads Monsters through SkillSets, Items, EquipmentRosters and partyTemplates before `InitializeBasicObjectXmls` loads SPCultures (`Campaign.cs:1396-1410,1460-1473,1520-1525`). Most dotted references also call `GetPresumedObject`, which auto-creates registered placeholder objects (`MBObjectManager.cs:713-730,1497-1534`). The recipe later relies on precisely this behavior at `:460-464`. — Corrected text: show the real runtime sequence and distinguish forward-safe `ReadObjectReferenceFromXml` from direct `GetObject` lookups that genuinely require an earlier object.

[HIGH] `docs/modding/recipe-add-a-culture.md:50-64,202-213,223-251`; `docs/modding/recipe-new-mod-from-zero.md:243-254` — Recruitment pool requirement — The handbook says a culture without a TAOM C# pool gets empty recruitment slots and that no data equivalent exists. `TaomVolunteerModel.GetBasicVolunteer` explicitly falls through to vanilla when the service returns no custom ID (`TaomVolunteerModel.cs:55-67`). Vanilla returns `sellerHero.Culture.BasicTroop` or `EliteBasicTroop` at `DefaultVolunteerModel.cs:111-118`; AI minor parties use `ActualClan.BasicTroop` at `RecruitmentCampaignBehavior.cs:316-335`. — Corrected text: “A valid XML `basic_troop`/`elite_basic_troop` and clan basic troop are sufficient for ordinary recruitment. C# is needed only when the culture needs TAOM’s weighted settlement/clan/culture pool behavior.”

[HIGH] Multiple recipe chapters — Lifecycle labels — Six of ten sampled labels contradict the handbook’s own correct lifecycle table. Engine basis: `Campaign.cs:1396-1415,1460-1473`, `SandBoxManager.cs:360-380`, `Settlement.cs:944-1044`, and process-level registry construction at `Module.cs:246-267,1026-1033`.

| Handbook line | Claimed | Verified result |
|---|---|---|
| `recipe-add-a-culture.md:248-251` | new campaign only | Full process restart is also required when adding its new `<XmlNode>`; then a new campaign for new clans/heroes/world entities |
| `items-armor.md:280` | full restart | Existing registered item file: next campaign/save load |
| `equipment-rosters.md:330` | new campaign only | Next campaign/save load for nonhero/future consumers; already-created heroes keep saved equipment |
| `party-templates.md:188` | full restart | Next campaign/save load; only subsequently created parties change |
| `cultures.md:323` | full restart | Next campaign/save load for an existing culture binding |
| `npcs-notables-and-townsfolk.md:181` | new campaign only | Next campaign/save load makes the template available for future notable creation |
| `clans.md:142` | new campaign only | Correct: `Factions` is skipped for saved campaigns |
| `lords-and-heroes.md:296` | new campaign only | Correct: `Heroes` is skipped for saved campaigns |
| `settlements.md:335` | next save load | Correct: `Settlement.Culture` is deserialized every load |
| `module-armory.md:352` | new campaign only | Race/native registry itself requires a full process restart; whether a new campaign is needed depends on the consuming object |

Corrected text should use the four lifecycle categories already accurately defined at `load-order-and-dependencies.md:167-174`, with consumer-specific caveats.

[HIGH] `docs/modding/npcs-notables-and-townsfolk.md:44,225`; `docs/modding/troops.md:42,67,261`; `docs/modding/wanderers-and-named-companions.md:37` — Exception scope — These chapters say a bad entry makes “nothing” or the “whole file” load. For managed MBObjects, `LoadXml` deserializes entries sequentially (`MBObjectManager.cs:1387-1395`) and `LoadXML` catches around the whole walk (`:786-796`). Entries before the exception survive; the broken entry is partial, and every later entry in the merged document is skipped. This is not necessarily the same physical file because the document contains all contributing modules. — Corrected text: “A deserialization exception silently truncates the merged object list at that entry; earlier entries remain.”

## MEDIUM

[MEDIUM] `docs/modding/troops.md:53,324-338` — `is_obsolete` retirement — The table admits the only managed consumer is `Hero.cs:1537`, then calls it the supported way to retire an ordinary troop. A complete v1.4.8 search finds only that Hero consumer; the flag has no effect on nonhero line troops. The rest of the deletion recipe—retaining the resolvable row while removing upgrade edges, templates and recruitment pools—is what actually retires them. — Corrected text: “`is_obsolete` affects Hero reinsertion only. For ordinary troops, keep the ID definition for save compatibility and remove every creation path.”

[MEDIUM] `docs/modding/npcs-notables-and-townsfolk.md:163-176,227` — Civilian assert — Inline `<EquipmentRoster civilian="true">` does not take the deprecated-assert path. It is handled by `MBEquipmentRoster.InitEquipment` at `MBEquipmentRoster.cs:88-106`, which accepts it. The assert at `BasicCharacterObject.cs:395-401` applies only to an `<EquipmentSet id="...">` reference carrying `civilian=`, exactly as the more accurate table at `equipment-rosters.md:187-198` says. — Corrected text: “Inline `EquipmentRoster civilian=true` remains accepted without an assert; use `equipmentType=Civilian` on referenced/standalone EquipmentSets.”

[MEDIUM] `docs/modding/recipe-new-mod-from-zero.md:222-228` — Impossible editor instruction — “Import … in the Kit, with the editor closed” is self-contradictory. `module-armory.md:330-335` correctly says to import with the editor open/idle. Closing the editor applies when an external process overwrites TPACs so the next editor startup rescans them. — Corrected text: “Open the Modding Kit to import textures and meshes. Close it only before externally replacing generated files.”

[MEDIUM] `docs/modding/kingdoms.md:134-145` — Ineffective recipe check — The relationship recipe warns that a missing dotted prefix and a comment inside `<relationships>` both break engine deserialization, but its check only calls `ElementTree.parse`. Both mistakes are well-formed XML and pass that command. — Corrected text: “This command checks syntax only. Also assert that every relationship/policy child has the expected tag and attributes and that every referenced kingdom/clan ID resolves.”

## Required hypothesis verdicts

1. **CONFIRM.** `troops.md:77-90` matches `DefaultCharacterStatsModel.cs:11,18-25` and `TaomCharacterStatsModel.cs:14-23`. Every listed interval T0–T10 is correct.

2. **QUALIFIED / partly dispute.** The one-ratio-per-party claim is correct at `DefaultPartySizeLimitModel.cs:430-442`, and it is not the later party-size limit. Calling `max_value` an absolute spawn ceiling misses the post-interpolation villager perk at `:449-455`.

3. **CONFIRM.** `cultures.md:111-117` matches `SPCultures.xsd:11-60` and `CultureObject.cs:485-497`: caravan children union unless the old container is explicitly replaced or filtered out.

4. **CONFIRM within the managed evidence boundary.** `Crafting.cs:566-608` processes descriptions in order and adds every full-piece match; `WeaponComponent.cs:14,26-29` makes the first added weapon primary. Native’s `crafting_templates.xml:3598-3627` places `OneHandedPolearm` first, while `item_usage_sets.xml:10124-10129` gives the fallback usage `requires_no_shield`. The live Armory remedy is present at `weapon_descriptions.xslt:547-683` under the `TAOM-1H-POLEARM` markers and is maintained by `tools/register_one_handed_polearms.py:85-86,237`. The final AI wield refusal is native-side rather than visible in the managed decompile.

5. **CONFIRM.** `lords-and-heroes.md:373-383` gets both cases right. A Hero row without a Character throws at `Hero.cs:1803-1808` and truncates later merged Hero entries. An `is_hero` NPCCharacter without a Hero row never invokes `Hero.Deserialize`; it remains a nonhero CharacterObject.

6. **CONFIRM the stated consequence; dispute the chapter’s three-bucket explanation.** Normal nonhero mission equipment is chosen per slot, so a slot absent from some source sets can spawn empty. Evidence: `Equipment.cs:549-615`, `Mission.cs:4202-4206`.

7. **CONFIRM.** `load-order-and-dependencies.md:124` precisely describes the behavior: prior entries survive, every later entry in the merged ID document is skipped. Its diagnostic advice follows from the loop and catch placement.

8. **CONFIRM.** The live Armory has no `AssetPackages` directory and contains 4,364 TPACs under `Assets`: 2,573 texture, 932 material, 663 geometry and 196 animation packs, matching `module-armory.md:102-104`.

9. **DISPUTE.** The “only order that works” premise is wrong, the runtime order is misstated, forward-safe references are ignored, managed Monster registration is missing, and its checks do not inspect the new module.

10. **Six of ten sampled `Takes effect:` lines are wrong.** The exact ten and corrections are in the lifecycle finding above.

## Checked and found correct

- The handbook consistently identifies `TAOM_Map/ModuleData/settlements.xml` as live and `Main/_Module/ModuleData/settlements.xml` as the stale shadow. Current counts are 988 live versus 863 shadow.
- Sampled counts matched disk: 1,001 Hero rows; 383 party templates with 3,295 stacks; 4,364 Armory TPACs.
- The tier ladder, culture caravan union, crafted-polearm registration remedy, Hero/Character asymmetry, and master merged-document truncation rule are correct.
- `python tools/lint_handbook.py` and `python tools/check_handbook_attributes.py` both pass, demonstrating that the findings above are outside their present semantic coverage.

CRITICAL: 3 | HIGH: 11 | MEDIUM: 4 | LOW: 0  
VERDICT: ISSUES FOUND
