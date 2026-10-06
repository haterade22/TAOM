# RCA: generated lord kits deep review (2026-10-06)

**Change:** generated lords of TAOM cultures wear culture- and race-appropriate gear (report: a generated Dunland lord, "Ulric", in vanilla Empire armour). Layer 1 is `lord_template_rosters.xslt`, which strips vanilla's lord, teen and ruler template flags for the six renamed cultures. Layer 2 is peer kits: `LordKitSelector`, `LordKitDonorAdapter`, `TaomEquipmentSelectionModel` and the kit half of `TaomHeroCreationModel`. Feature doc: [generated-lord-kits.md](../features/generated-lord-kits.md).

**Review:** `/deep-review`, seven lenses in two waves (Standards, Engine compatibility, Data flow and XML; then Efficiency, Completeness and Design). One HIGH, six MEDIUM and several LOW findings, all fixed in this change except where noted. The HIGH was confirmed against the v1.5.4 decompile before fixing.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | Dead XML lords donated the engine's shared `DeadBattleEquipment` / `DeadCivilianEquipment`. Two deaths made it a "shared" kit, and its untyped civilian set could overwrite a new lord's battle set. | Engine API: override dispatch | I read `BasicCharacterObject.BattleEquipments` (the authored roster) while the call dispatches to `CharacterObject.BattleEquipments`. For a hero, that override returns the live `HeroObject.BattleEquipment`, which falls back to the dead kit once `OnDeath` nulls it (`Hero.cs:221`, `:2038`). | Fixed: donors read each lord's authored roster (`MBObjectManager.GetObject<MBEquipmentRoster>(lordId)`), and a kit needs one living wearer. Lesson appended (adapters-taleworlds-api). |
| 2 | MED | The kept vanilla ancestors `dead_lord_6_*` (khuzait) carried Calradic kits back into Rhûn's pool, undoing layer 1. | Data flow | Same root as 1: the donor list was every original lord character, alive or dead. | Fixed by the "at least one living wearer" rule; `Pick_KitWornOnlyByDeadLords_IsSkipped`. |
| 3 | MED | Minor-faction heroes (Corsair Blades and others on vanilla Rohan-culture templates) were re-kitted as their culture's lords. | Scope of an engine hook | I enumerated the hero-creation callers I knew (companion clans, rebels), not every caller of `InitializeHeroFromSettings`. | Fixed: gate on the clan (`Hero.Clan.IsMinorFaction`, set before equipment; `IsMinorFactionHero` is set only afterwards). |
| 4 | MED | The donor rules and the adult-lord gate lived in the adapter and the model, untested. | Architecture (ADR-002, tests.md skip guards) | I treated "filter the engine list" as wrapping rather than deciding. | Fixed in part: the gate (`LordKitSelector.Wants`) and the living-wearer rule (`IsAlive`) moved into the selector with a test each. The adapter's donor-identity guards (lord, not the player, original, not a template, non-stealth set with a body) stay in the adapter, untested; they read engine facts no unit test can build. |
| 5 | MED | `GetEquipmentsForChangingRuler` was left race-blind: a stepping-down ruler got a template kit. | Completeness | The firing-set enumeration listed the ruler path but I left it out of scope without a decision. | Fixed for the old ruler (Mike's choice); the new ruler keeps the ruler template, recorded as a limit in the feature doc. |
| 6 | MED | Dale's `taom_sturgia_*` templates, now the only fallback pool, were 100% vanilla Sturgian items. | Data drift (#637) | The fallback pool was checked for non-emptiness, not for whose items it holds. | Fixed: hand-ported to Dale's mounted-noble kit (`dale_bat_template_medium_c`), no bow, so no ranged-ceiling question. |
| 7 | LOW | The stylesheet reaches only roster nodes listed before it; nothing pinned its position. | Engine load order | The XSLT was checked on its output, not on where the engine applies it. | Fixed: `SubModule_LordTemplateStylesheet_IsTheLastEquipmentRostersNode`, plus comments in both files. |
| 8 | LOW | Comments understated what is stripped (SandBox noble teen templates) and gave the wrong reason for keeping Khand's child templates. | Docs accuracy | Written from intent, not from the transform's measured output. | Fixed in the stylesheet comment and the feature doc. |
| 9 | LOW | Feature doc, feature-map row, both GameModel catalogues, the RaceAge doc and the `/xslt-check` mapping were missing or stale. | Process | Documentation duty deferred to "after review". | Fixed; `lint_docs.py` reports 0 missing docs and 0 registry drift. |
| 10 | LOW | Each pick built signatures for every lord of every culture, and the gate ran after the scan. | Efficiency | Written for correctness first. | Fixed: prefilter to the request; `Wants` runs before the scan. |
| 11 | LOW | `validate_xml_schemas.py` prints "NOT REGISTERED" for an XSLT-only stylesheet the engine does apply. | Tooling | Pre-existing. | FOLLOW-UP, not fixed here. |

## Root-cause pattern

Findings 1, 2 and 3 share one cause: **I verified the member I expected to be called, not the one the engine dispatches, and the callers I knew, not every caller.** `BattleEquipments` is virtual and `CharacterObject` overrides it; `InitializeHeroFromSettings` has a minor-faction caller I had not listed. The researcher's report named both facts (live gear for heroes; minor-faction spawning), and I did not carry them into the design.

## Why each agent missed these

The lenses ran after the code was written and caught all three; nothing in the build-time workflow did. The first draft's own tests covered the pure selector only, so a wrong input set could not show up in them.

## Limits recorded, not defects

- Existing saves keep the gear their lords already rolled (the engine only picks at creation and role changes).
- A culture, race and sex with no shared kit falls back to the `taom_*` template, which can be another race's armour in a mixed culture.
- `TaomHeroCreationModel` still restates vanilla's offspring rule (Design FOLLOW-UP; deleting it changes the RaceAge doc's stated intent).
- Every new test was written before its fix, but the RED run was masked: another session's uncommitted `CountingMcm.cs` broke the test build at that moment, so the new tests' failure was never observed. They pass now.
