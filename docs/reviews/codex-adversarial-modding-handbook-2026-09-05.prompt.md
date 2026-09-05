# Codex adversarial review: the TAOM modder handbook (docs/modding/)

You are an adversarial technical reviewer. Your job is to find places where this handbook is WRONG,
not to praise it. A wrong statement in this handbook is worse than a missing one, because the reader
is a 3D artist with no programming background who will follow it literally against a live game
install and a shipped mod.

## What was built

`docs/modding/` is a 39-chapter handbook for editing TAOM's XML data without writing C#. Audience:
KEYforce (TAOM's 3D artist), who balances numbers and adds or removes weapons, armour, troops, NPCs,
lords, clans, kingdoms, cultures and settlements; and a second reader building a total conversion
from an empty folder. It was assembled from the v1.4.8 engine deserializers, the shipped XML in
three modules, and TAOM's existing developer docs.

Read `docs/modding/README.md` first for the map, then the chapters.

## Ground truth you must check against, in this order

1. **The engine.** `E:\Decompiled_Bannerlord\_categories_v1.4.8\` is the v1.4.8 decompile. Every
   attribute table in the handbook sits under an `<!-- engine-table type= file= method= inert= -->`
   marker naming the class, the file and the method it claims to describe. Open that method.
2. **The shipped files.** Repo data under `Main/_Module/ModuleData/`. Live modules under
   `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\` (`TAOM_Map`,
   `LOTRLOME_Armory`), plus vanilla `Native`, `SandBoxCore`, `SandBox`.
3. **The developer docs** the chapters cite. These are frequently stale, which is the point of the
   next section.

## Known-stale sources: do NOT treat a developer doc as authority

The research behind the handbook checked 43 claims in TAOM's own docs against the files and found
them wrong. Examples: `docs/features/lord-identity-reconciliation.md` says `heroes.xml` holds 961
heroes (it holds 1001); `docs/features/localization.md` gives string counts that are stale by
thousands; `docs/features/alignment-aware-execution.md` maps `empire` to Rohan and `vlandia` to
Arthedain when `empire` is Dunland and `vlandia` is Rohan; `docs/features/troop-weight-system.md`
says troop_weights.xml has about 80 entries (105 live). If a handbook statement disagrees with a
developer doc, check the FILE before calling the handbook wrong. A handbook that contradicts a stale
doc correctly is doing its job.

## What to look for, in priority order

1. **Fabricated engine behaviour.** An attribute table row whose "What it does" or "Default when
   absent" the cited deserializer contradicts. A stated formula (tier from level, wage, item value,
   party spawn size, merge semantics, load order) that the code computes differently. A claim about
   what throws, what returns null, or what is silently ignored.
2. **Wrong or dangerous recipes.** Walk each `### Add` / `### Modify` / `### Delete` recipe as if
   performing it. Does its `Check:` command actually prove the change? Is its `Takes effect:` line
   right (full game restart vs new campaign only vs next save load vs live)? Is its `Code:` line
   right about whether C# is needed? A recipe that would corrupt a save, break a shipped campaign,
   or edit the stale repo copy of `settlements.xml` instead of the live one is a CRITICAL finding.
3. **Authority confusion between repo and game install.** The repo's
   `Main/_Module/ModuleData/settlements.xml` is a stale shadow; the game reads
   `TAOM_Map/ModuleData/settlements.xml`. `TAOM_Map` and `LOTRLOME_Armory` live only in the game
   install and a module reinstall reverts hand edits. Any chapter that points an edit at the wrong
   copy is a CRITICAL finding.
4. **Ids and paths that do not exist.** Every id named (item, troop, culture, kingdom, clan, hero,
   settlement, party template, body property, race) and every path. Spot-check aggressively.
5. **Numbers.** Counts carry a `<!-- measured: <command> -->` comment. Re-run a sample and compare.
6. **Gaps that matter more than the errors.** A step a non-programmer could not perform from the
   text alone. A term used before it is defined. A command whose flags are unexplained. A place
   where the handbook is confidently silent about a failure mode it should warn about.

## Specific hypotheses to CONFIRM or DISPUTE

State a verdict on each, with the file and line you read.

1. `docs/modding/troops.md` states the level-to-tier ladder and TAOM's raised cap. Check it against
   `DefaultCharacterStatsModel.GetTier` and `Main/Features/TroopProgression/Models/TaomCharacterStatsModel.cs`.
   Are the worked level numbers right at every tier the chapter names?
2. `docs/modding/party-templates.md` claims `max_value` is a spawn ceiling, not a party size, and
   that one ratio is drawn per party and applied to every stack. Confirm against
   `PartyTemplateObject` and the roster-filling call site.
3. `docs/modding/cultures.md` claims the caravan child lists UNION across modules rather than
   replace, so emitting yours without excluding vanilla's leaves both. Confirm against
   `CultureObject.Deserialize`.
4. `docs/modding/items-weapons-and-crafting.md` claims the FIRST `WeaponDescription` covering every
   piece becomes the primary usage, so a polearm absent from `OneHandedPolearm` resolves
   `requires_no_shield` and a shield-carrying troop never draws it. Confirm against the crafting
   code, and check the chapter's remedy is the one the repo actually uses.
5. `docs/modding/lords-and-heroes.md` describes what happens when an `is_hero` NPCCharacter has no
   matching `<Hero>` row. Confirm against `Hero.Deserialize`, including whether the failure is
   confined to that entry or affects the rest of the file.
6. `docs/modding/equipment-rosters.md` claims a non-hero troop's kit is assembled per slot from
   independently chosen sets, so a slot filled in some battle sets and not others can spawn empty.
   Confirm against the equipment selection path.
7. `docs/modding/load-order-and-dependencies.md` claims one malformed entry silently drops every
   LATER entry in the same file. Confirm the exact behaviour and whether the handbook's diagnosis
   advice follows from it.
8. `docs/modding/module-armory.md` claims the Armoury ships no cooked asset packs and the engine
   therefore reads the loose `Assets/**` tree. Verify on disk.
9. `docs/modding/recipe-new-mod-from-zero.md` orders the whole build. Is any ordering constraint
   wrong or missing, such that following the chapter in order produces a crash?
10. Every chapter's `Takes effect:` lines. Pick ten across the handbook and verify each against how
    the engine loads that data. New-campaign-only data presented as save-reachable, or the reverse,
    is a HIGH finding.

## Output format

Group findings by severity: CRITICAL (would corrupt data, break a save, or crash), HIGH (factually
wrong about the engine or the files), MEDIUM (misleading, or a gap that blocks the reader), LOW
(imprecision, style, inconsistency). For each: the chapter and heading, what it says, what the
evidence shows with `file:line`, and the corrected text. End with a short list of what you checked
and found CORRECT, so the coverage is visible.

Do not report the handbook's own "not documented yet" statements as gaps; those are deliberate and
name where to look. Do not report dead links between chapters as findings unless the target chapter
genuinely does not exist in `docs/modding/`.
