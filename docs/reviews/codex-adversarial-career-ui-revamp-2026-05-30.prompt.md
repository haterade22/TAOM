# Adversarial review -- TAOM CareerSystem screen revamp (2026-05-30)

You are an adversarial reviewer. TAOM is a Lord of the Rings total-conversion mod of Mount & Blade II: Bannerlord v1.4.5. This session revamped the Career screen UI and added per-tier rank titles. Find real bugs. Confirm or DISPUTE each Known Suspect with evidence from the actual files. Do not flag vanilla-matching code as bugs. Use `--` not em-dash.

## TAOM ID CHEATSHEET
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar.
"rohan" is NOT valid (use vlandia). "dol_guldur" is NOT valid (use dolguldur).

## READ FIRST
- docs/features/career-system.md (feature overview)
- Main/_Module/GUI/Prefabs/CareerSystem/CareerScreen.xml (the rewritten prefab)

## What changed this session
C#:
- Main/Features/CareerSystem/UI/CareerScreenVM.cs -- removed Tier2GateBottomHalf/Tier3GateTopHalf/Tier3GateFull/Tier1Locked props; added Tier2RequirementText/Tier3RequirementText; tier labels Tier1/2/3Label now set from career.Rank1/2/3Name with a "{=taom_career_tierN}Tier N" fallback.
- Main/Features/CareerSystem/UI/CareerChoiceObjectVM.cs -- new computed `IsUnavailable => !_isTaken && !_isFreeToTake`; IsTaken/IsFreeToTake setters now also call OnPropertyChanged(nameof(IsUnavailable)).
- Main/Features/CareerSystem/UI/CareerChoiceGroupObjectVM.cs -- GroupName now returns DisplayName (localized) or a HumanizeId(id) fallback ("ranger_of_ithilien_t1_a" -> "Path A").
- Main/Features/CareerSystem/Domain/CareerChoiceGroupDefinition.cs -- new optional `displayName` ctor param + DisplayName prop.
- Main/Features/CareerSystem/Domain/CareerDefinition.cs -- new optional `rank1Name`/`rank2Name`/`rank3Name` ctor params + Rank1/2/3Name props.
- Main/Features/CareerSystem/CareerConfigProvider.cs -- parse `display_name` on <ChoiceGroup> and `rank1_name`/`rank2_name`/`rank3_name` on <Career>.
- Main/Features/CareerSystem/CareerRegistry.cs + ICareerRegistry.cs -- new `int GetTierUnlockLevel(int tier)` (1/10/20, int.MaxValue otherwise); `IsTierAvailable` refactored to `tier in 1..3 && heroLevel >= GetTierUnlockLevel(tier)`.
Data:
- Main/_Module/ModuleData/career_system/taom_career_choices.xml -- 294 `display_name="{=taom_career_grp_<groupId>}<name>"` on <ChoiceGroup> (288 active groups + 6 inside a pre-existing DISABLED comment block for cave_troll_master).
- Main/_Module/ModuleData/career_system/taom_careers.xml -- 147 `rank1/2/3_name="{=taom_career_rank{N}_<careerId>}<title>"` on all 49 <Career>.
- Main/_Module/ModuleData/taom_module_strings.xml -- +441 `<string id="taom_career_grp_*"/>` and `taom_career_rank{1,2,3}_*` keys (and taom_career_tier_requirement).
- Main/_Module/GUI/TAOMSpriteData.xml -- ui_taom_career_system category SpriteSheetCount 1 -> 2, added `<SpriteSheetSize ID="2" Width="256" Height="256"/>`, added a <SpritePart> (Name=CareerSystem\career_point_pip, SheetID=2, SheetX=0, SheetY=0, Width=256, Height=256) and a <GenericSprite> for it. New PNG at Main/_Module/GUI/SpriteParts/ui_taom_career_system/CareerSystem/career_point_pip.png (256x256, alpha).
Tooling (review for data-corruption risk only): tools/apply_career_group_names.py, tools/apply_career_rank_names.py, tools/career_group_names.json, tools/career_rank_names.json.

## KNOWN SUSPECTS -- confirm or DISPUTE each with evidence
1. SPRITE-ATLAS SHEET-2 EDIT (highest risk). TAOM ships loose PNGs under GUI/SpriteParts/<category>/ with NO pre-baked sheet textures in the repo; TAOMSpriteData.xml carries per-sprite SheetID/SheetX/SheetY rectangles. The edit adds a SECOND sheet to the `ui_taom_career_system` category sized 256x256 (sheet 1 is 4096x4096) and puts career_point_pip alone on it at (0,0). QUESTION: in Bannerlord v1.4.5, is SheetID scoped per-category (so sheet "2" is valid within this category), and does the engine's runtime atlas assembly accept multiple sheets of DIFFERENT sizes within one category? Decompile/inspect TaleWorlds.TwoDimension / GauntletUI SpriteData + SpriteCategory loading (e.g. SpriteData.Deserialize, SpriteCategory, Sprite/SpritePart) to confirm the manifest shape is valid and will not break loading of the WHOLE category (which would also break plus_sign_icon/minus_sign_icon/portraits/abilities already on sheet 1). If multi-size-per-category is NOT supported, this is HIGH. If it is, DISPUTE.
2. VM<->PREFAB BINDING COMPLETENESS. For every `@Prop`, `{Collection}`, and `Command.X` in CareerScreen.xml, confirm a matching public [DataSourceProperty] / public method exists on the bound VM (screen-level on CareerScreenVM; node-level on CareerChoiceGroupObjectVM; choice-level on CareerChoiceObjectVM). Flag any dangling binding (renders blank) AND any removed gate property still referenced. Confirm NO `@Tier2GateBottomHalf`/`@Tier3GateTopHalf`/`@Tier3GateFull`/`@Tier1Locked` remain in the prefab now that the VM props are gone.
3. LOCALIZATION KEY CONSISTENCY. The `{=key}` prefixes in the display_name (taom_career_choices.xml) and rank_name (taom_careers.xml) attributes must EXACTLY match the `<string id=...>`/`{=...}` entries harvested into taom_module_strings.xml. Spot-check several. Also: are there any DUPLICATE `<string id>` entries introduced in taom_module_strings.xml? Is the file still well-formed? Are all 441 new keys unique?
4. ISTIERAVAILABLE REFACTOR EQUIVALENCE. Old: switch(tier){1:true; 2:>=10; 3:>=20; default:false}. New: `tier<1||tier>3 ? false : heroLevel>=GetTierUnlockLevel(tier)` with GetTierUnlockLevel 1/10/20. Confirm behavior is identical for all (heroLevel, tier) the game passes. Note GetTierUnlockLevel(1)=1 means IsTierAvailable(0,1)=false where the old code returned true at tier 1 regardless of level -- is heroLevel ever 0 or negative in practice (heroes start at level 1)? Confirm this is benign or flag.
5. OPTIONAL-PARAM CTOR BACK-COMPAT. CareerDefinition and CareerChoiceGroupDefinition gained trailing optional params (default ""). Confirm all existing call sites (CareerConfigProvider, tests) still compile/behave, and that the parser passes the new attrs. Confirm no positional-arg call site silently shifts.
6. IsUnavailable NOTIFICATION. CareerChoiceObjectVM.IsUnavailable is computed from _isTaken/_isFreeToTake; the setters call OnPropertyChanged(nameof(IsUnavailable)). The VMs are also fully rebuilt on RefreshValues. Confirm there is no stale-binding path where a pip shows the wrong state, and that OnPropertyChanged(string) is the correct v1.4.5 ViewModel API.
7. COMMENTED-BLOCK INJECTION. The 6 cave_troll_master `<ChoiceGroup>` display_name attrs were injected INSIDE a pre-existing `<!-- DISABLED ... -->` comment in taom_career_choices.xml. Confirm the file is still well-formed XML (the injected attrs are inside the comment, inert) and that this does not break parsing.

## REQUIRED SECTIONS in your output
- SPRITE ATLAS: paste the relevant TaleWorlds SpriteData/SpriteCategory deserialization code you inspected; state whether multi-sheet, mixed-size-per-category is valid in v1.4.5. Verdict on Suspect 1.
- BINDING TABLE: every prefab binding -> backing member -> CONNECTED/GAP.
- LOC CONSISTENCY: spot-check results + duplicate-id check + well-formedness.
- FINDINGS: numbered, each with file:line, severity (HIGH/MED/LOW), evidence, and fix. If none, say so explicitly per section.

## QUALITY GATES
- Decompile/inspect the actual installed v1.4.5 TaleWorlds DLLs for Suspect 1 (sprite atlas) -- do not guess. Path: E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/.
- Read the actual TAOM files before claiming a binding is missing -- grep, don't assume.
- Tests are green (2698 passed). A prior Claude deep-review found no HIGH; verify independently and say where you agree/disagree.

## PRIOR REVIEW LESSONS
SUCCESSES: config ID cross-ref caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland); Codex flagged vanilla-matching code as bugs; Codex skipped hard sections. Do NOT repeat these.
