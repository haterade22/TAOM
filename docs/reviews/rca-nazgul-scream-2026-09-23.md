# RCA: the Nine's SCREAM, SignatureStrikes with N signatures (#645), 2026-09-23

## Top-line

#645 turned SignatureStrikes from one hard-wired signature into a `signatures` list and gave the
Nine a SCREAM with a generated sound. The seven-lens `/deep-review` found no engine incompatibility
(27 members verified), no data-flow gap in the signature-index contract or the cooldown stamps, no
performance cost worth a change, and one design simplification. It found one MED in new code (an
engine float into a native call with no finiteness gate), one MED in docs (a sentence the change
made false), a set of LOW doc, comment and placement misses, and a REPEAT of a lesson written for
this very test file one week earlier. A convergence pass over the fixes found one more LOW, a
citation the finding-3 fix left half-updated. All were fixed in the same session.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED (data flow) | `StrikeSoundPlayer` passed `attacker.GetEyeGlobalPosition()` straight into native `Mission.MakeSound` with no finiteness check, ahead of the runner's own centre gate. `CustomAttacksUtils.IsBlowGeometrySafe` keeps the same values out of `MakeSound`. What a non-finite position does inside native is unproven: the spider AV that guard was written for was later traced to `HandleBlowAux` (`rca-spider-dismount-on-hit-2026-06-15.md`), so this gate is a defence, not a crash fix. | Engine float into a native sink | The runner's existing centre gate was kept, but the NEW native call was written as a plain call: the NaN rule in `csharp-architecture.md` names decision gates, casts and new inputs to existing gates, not a float handed to native. | Gated (`none (non-finite position)`); lesson in `adapters-taleworlds-api.md`; see "Feedback memories" for the rule. |
| 2 | MED (completeness) | `combat-mechanics.md:40` said a signature overhead always floors the struck agent; the Nine's overhead is a Scream with `knockDown: false`. The model's own comment said a sweep was the only knock-back true; two domain comments said "hero id or race" and "from the impact". | Doc drift inside the changed semantics | The sweep updated the knock-back half of the same line and missed its knock-down parenthetical; no grep for code comments describing signature semantics. | Fixed; the misc lesson from #644 (grep for what a change invalidates, not only its wording) applies. |
| 3 | LOW (engine) | The doc's morale sentence gave `0.5 * tier + 1` for everyone. A campaign hero uses `1.5 * (0.5 * (level / 4 + 1) + 1)`, and a Custom Battle character is a `BasicCharacterObject` whose resistance is 1 (`BasicCharacterObject.cs:268-271`), so there the full 25 applies. | Unverified engine claim | Carried from the plan's research of the campaign-troop path without reading `GetMoraleResistance` on both classes. | Corrected with citations; `evidence-over-claims.md` §C. |
| 4 | LOW (engine) | The doc said Native supports `.ogg` and `.wav` only; Native's header lists them, and TAOM ships `.mp3` variations elsewhere. | Overstated claim | Paraphrase hardened into "only". | Reworded to what the header says. |
| 5 | LOW (data flow) | The MCM hint promised the struck-foe stagger everywhere; `TaomCombatMechanicsModel` is campaign-only. | Player-facing text drift | The #605 hint had the same limit for the knockdown and the new text copied its shape. | Hint states the Custom Battle caveat. |
| 6 | LOW (XML) | The scream's `module_sound` sat inside the Troll section of `module_sounds.xml`. | Placement | Inserted after the entry used as the shape reference instead of reading the file's section layout. | Own `LOTR/Mordor/Nazgul` section. |
| 7 | LOW (completeness) | The regex in `ShippedSignatureStrikesConfigTests` became two literal dash characters when the class was rewritten with the Write tool, which decodes `\uXXXX`. | Tool gotcha, REPEAT | The lesson exists (`build-tooling-workflow.md`, from `rca-signature-strikes-2026-09-16.md` finding 8, the same file). The dash scan this session covered markdown and JSON, not the rewritten `.cs`. | Escapes restored by a byte-level script; the lesson gains the wholesale-rewrite trigger. |
| 8 | LOW (completeness) | No cross-links from `nazgul-family.md` / `dread-aura.md`; the issue's file table named a helper the plan proposed and the build dropped; `StrikeSoundPlayer` had no direct test. | Completeness | The plan's file list went into the issue before implementation and was not re-read after the design changed. | Links added, issue corrected, two engine-free tests. |
| 9 | Design (applied) | The service parsed direction and kind fail-closed but defaulted an unparseable origin to `Impact`. | Silent default route | Written for convenience in the service; the provider already reverts, so no shipped config could reach it. | Origin parsed fail-closed; RED test `Evaluate_ProfileWithUnknownOrigin_ReturnsNull`. |
| 10 | LOW (engine, convergence pass) | The morale sentence corrected for finding 3 still cited only `SandboxBattleMoraleModel.cs:95-98` for the division; a Custom Battle runs `CustomBattleMoraleModel` (`:72-75`, through `TaomCustomBattleMoraleModel`, `SubModule.cs:1264`), and both floor the divisor at 1. No number changed. | Incomplete fix | The fix split the resistance values by game mode but kept the one-mode citation beside them: finding 2's shape again, half of a sentence updated. | Both models cited and the floor stated. |

Unverified and owed to the smoke, not findings: that native answers -1 for an unregistered sound
name (the engine's own null id; the smoke misspells a name on purpose), the first `.ogg` a TAOM
module sound has ever played, and whether the nazghul voice set carries the `Yell` the fallback
uses.

## Root-cause pattern

Findings 1, 2, 5 and 10 share a shape: **the change's NEW surface inherited nothing from the rules
its OLD surface already obeyed.** The ring centre was gated, the new sound call was not; the
knock-back sentence was updated, the knock-down half of the same line was not; the effect was
described, the campaign-only caveat was not; the resistance values were split by game mode, the
citation beside them was not. Each time the fix was to apply to the new line what the adjacent old
line already did.

## Why each lens missed or caught these

- Standards: passed; its rules are structural.
- Engine: caught 3 and 4 by reading the engine behind each doc claim; verified every new member.
- Data flow: caught 1 and 5, tracing the eye position to its native sink.
- XML: caught 6 and ran every gate on the new sound.
- Efficiency: found no cost worth a change (the per-hit path is one reference-keyed probe).
- Completeness: caught 2, 7 and 8.
- Design: proposed 9 and confirmed the rest optimal (the sound path, the double name parse, the
  hero-set idiom, the test growth).
- Convergence (the standards lens over the fix hunks only): caught 10 by re-reading the engine
  behind each corrected sentence, and confirmed the origin change cannot drop a row the provider
  emits.

## Feedback memories to codify

- `docs/reviews/lessons/adapters-taleworlds-api.md`: an engine float handed straight into a native
  call is a gate too.
- `docs/reviews/lessons/build-tooling-workflow.md`: the existing `\uXXXX` lesson now records its
  second occurrence and the wholesale-rewrite trigger.
- Recommended, not done here: `csharp-architecture.md` "Engine-Float Decision Gates" names four
  categories and says to widen the scope when a new one appears. A float into a native sink is a
  fifth; it was caught in review rather than shipped, so the rule change is left for the next
  harness pass rather than made inside a feature commit.

## Codex pass (Review 130, 2026-09-23)

Codex (gpt-6-astra, ultra) reviewed `b90fd3a4` together with #644's `9804f67b`: P1 0, P2 0. It
disputed the Custom Battle, cooldown, geometry and NaN suspects with decompiled lines, matched every
shipped config value to the compiled defaults, measured the three takes (mono Vorbis at 44.1 kHz;
3.00 s, 3.48 s and 3.00 s, under `mission_voice_shout`'s 8 s), and left the native sound lookup,
playback and the `Yell` fallback UNVERIFIED. It noted that `entry.Times = entry.Times.With(...)` is
not a synchronised transaction: overlapping writers would lose an update only if native delivers
melee hits concurrently, which nothing shows. Two #645 findings, both fixed in the follow-up commit:

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| C1 | P3 (Codex O1) | `"heroIds": [null]` or `[""]`, other axes empty, passed the "matches nobody" check, which reads `Count`; the registry then skipped the entry silently, so the signature loaded as valid and matched nobody. | Missing entry validation | The tests covered the list's `null` and `[]` states, not its entries, and the registry's skip hid the gap downstream. | `ValidateList` removes a null or blank entry with a warning; two RED tests; new lesson in `lessons/gamemodels-services.md`. |
| C2 | LOW (found verifying the lesson) | `StrikeSoundPlayer`'s comment and `signature-strikes.md` said a NaN into `MakeSound` is what `CustomAttacksUtils` has guarded since the spider auto-bite AV; that AV was traced to `HandleBlowAux` (`rca-spider-dismount-on-hit-2026-06-15.md`), so the gate is a defence, not a known crash fix. Row 1 and its lesson were corrected before `b90fd3a4`; the two copies in that commit were not. | Correction not propagated | Corrected where it was noticed, with no search for the other copies. | Both corrected; new lesson in `lessons/misc.md` "A claim found wrong is wrong everywhere it was written". The guard's own comments in `CustomAttacksUtils.cs` gave the retracted cause too; that copy was fixed in the Review 130 follow-up (below). |

The fix diff got its own six-lens deep review. Its #645 findings, both fixed:

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| R1 | LOW (Standards, Engine, Data flow) | `ValidateList` removed a blank entry but kept a padded one, and the registry matches hero ids exactly and skips without a word, so `" lord_1_17 "` matched nobody silently: C1's class one step over. | Missing entry normalisation | The fix answered the inputs Codex sent (`[null]`, `[""]`), not the class. | A kept entry is trimmed, as the id and the sound are; `GetConfig_PaddedIdentityEntry_IsTrimmedWithoutAWarning`. |
| R2 | LOW (Standards, Data flow) | The provider's class summary listed every drop case but the new one, and the warning read "holds 1 null or blank entries". | Completeness | The method was edited, not its summary. | Both corrected. |

## Follow-ups to Review 130 (2026-09-23)

Mike asked for the four owed follow-ups to be reviewed and fixed where they were a problem. Two
were: the entry gap in three sibling providers, and the retracted spider claim in
`CustomAttacksUtils`. The Load Game thumbnail (nazghul in `BasicTableauRaceGuard`) needs an in-game
render test and stays owed. Sharing one compiled `lords.xslt` would save under a second: only one
test transforms the full file, and the two Nazgul data tests take 0.5 s and 0.3 s, each compiling
the stylesheet. The fix diff's six-lens deep review found an older bug beside the
first, fixed in the same change.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | LOW | `DreadAuraConfigProvider`, `UncapturableHeroesConfigProvider` and `BannerBearerConfigProvider` kept blank list entries and never trimmed, so a padded name matched nobody; a padded `excludeHeroIds` entry left its hero uncapturable. | Missing entry validation (C1's class) | Review 130 fixed the instance Codex sent and listed the siblings by the method name `ValidateList`. | The same filter and two or three tests per provider; a recurrence on the `lessons/gamemodels-services.md` entry lesson. `CombatMechanicsConfigProvider.CleanIdList` and `FieldCommissionConfigProvider.SanitizeRaceNames` have the shape under other names and are left for Mike. |
| F2 | MED (Data flow, older than this change) | `BannerBearerConfigProvider.ValidateFormationGroups` kept any string `Enum.TryParse` accepted, including `" Infantry "`, `"2"` and `"Infantry, Ranged"`. `BannerBearerService` compares with `FormationClass.ToString()`, so such an entry loaded clean and, as the only entry, switched every bearer off without a warning. | `Enum.TryParse` taken for a name check (REPEAT of `rca-signature-strikes-2026-09-16.md` C2, Codex review 114 F2) | The only test was the typo `Infntry`, and the #605 fix lived as a private helper in `StrikeNames` that no other provider could find. | The helper moved to `TAOM.Core.Validation.EnumNames`, used by both; entries are stored as the enum prints them (it declares three values twice); a new lesson in `lessons/gamemodels-services.md`. Other enum parsers with the leak are left for Mike. |
| F3 | LOW | C2's owed copy, fixed: `CustomAttacksUtils`'s comments and its test class gave the retracted cause. The first rewrite then named `Mission.OnAgentHit` (managed) as a native sink and blamed `Vec3.Normalize()` for NaN; v1.5.3 maps a near-zero or NaN vector to (0, 1, 0), and only an infinite component comes out NaN. | Engine claim carried forward | The rewrite corrected the old comment's conclusion and kept its sink list and example without re-reading them. | The rationale lives once, on `IsBlowGeometrySafe`, naming `HandleBlowAux`, `Die` and `MakeSound`; the test class points there; `rca-spider-directional-attacks-2026-06-15.md` has a superseded note; a recurrence on the `lessons/misc.md` propagation lesson. |
| F4 | LOW | The docs' test figures had drifted: BannerBearers 74 and 42, and Dread Aura's "6,615-test suite" (its own 161 was right until this change added two). | Stale count | Counted by hand when written. | Re-measured with `dotnet test --filter` (163, 107, 61); the suite size is no longer quoted. |

## The scream swap (2026-09-23)

Mike replaced the three ElevenLabs takes with a clip he supplied (`TAOM_Nazgul Scream 2 .mp3`), as
one variation. The swap's `/deep-review` (the XML, data flow, completeness, design and engine lenses,
then a convergence pass) found no runtime defect: the name, the registration and the file connect.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| W1 | HIGH (XML lens; the record, no runtime effect) | The first draft called the clip Mike's own (the doc: "Mike's own take"; the XML comment: "Mike's take"); its metadata tags named a third-party website. | Unverified claim | Inferred from "use that for the nazgul scream" and the `TAOM_` file name without reading the file's tags. | Now "the clip Mike supplied". Mike chose not to pursue provenance and had the tags dropped (`-map_metadata -1`). `evidence-over-claims.md` C already covers the cause: read the file (`ffprobe -show_entries format_tags`) before describing where it came from. |
| W2 | MED (data flow, XML) | The deleted takes stay in the game install, because the module copy (`CopyModule`, `Clean="false"`) never removes a file, and the release zip is built from the install, so they would ship unused. | Additive deploy | The deletion was checked in the repo only. | The doc's Source bullet and the CHANGELOG entry say to delete them by hand after the next deploy and before packaging. |
| W3 | LOW (completeness, design) | Live lines still called the sound ElevenLabs-generated (the shipped config comment, the feature map) or three takes (the Key Files row), and the smoke step neither required a deploy nor checked that one clip repeating does not sound like a loop. | Stale text | The swap rewrote the section it replaced, not the other descriptions of the sound. | All updated; the smoke step covers the deploy and repeats. |
| W4 | LOW (engine) | The smoke step's remedy for a looping repeat (widen the pitch range) assumed native draws a new pitch per play; nothing readable in `TaleWorlds.Native.dll` shows how the multipliers are used at play time. | Unverified engine claim | The W3 fix dropped "per play" from the Sound provenance line but not from the remedy that depends on it. | The step now calls it a try, names the listen as the test, and falls back to more takes. |
| W5 | LOW (engine) | W2's first wording, "a deploy never removes files", was too broad (the binary copy runs `Clean="true"` on `bin/Win64_Shipping_Client`, `Basic.targets:56`) and did not say the deletion must follow the deploy, while the installed XML still names the old files. | Engine claim overgeneralised | Written from the module copy alone. | Worded as the module copy and ordered after the next deploy, in the doc, the CHANGELOG entry and W2. |
| W6 | LOW (convergence) | W1 first quoted "Mike's own take" for both the doc and the XML comment; the comment read "Mike's take". | Misquote | Quoted from the doc draft for both. | W1 quotes each. |
| W7 | LOW (convergence) | The CHANGELOG draft gave the peak as -1 dBFS as if of the shipped file, which measures -1.6 dB; -1 dBFS is the level before encoding. | Figure without its condition | Copied from the processing step without "before encoding". | Added, as the doc already had it. |
| W8 | LOW (convergence) | The committed #645 CHANGELOG entry sends the reader to the doc's Sound provenance section for the prompts, which the swap deleted. | Dangling pointer | The swap rewrote the section without searching for references into it. | The section says the prompts are in its `b90fd3a4` copy; recurrence on the `lessons/misc.md` data-change lesson. |
| W9 | LOW (convergence) | The Review 130 paragraph (`437d5911`, 12:44) said "Nothing is pushed or deployed"; the install already held #645's three takes and XML, in a folder created at 12:41. | Status claim from the plan | Written from the plan (deploy on Mike's word) without reading the install. | Now "Nothing is pushed", with the install state. `evidence-over-claims.md` C covers it. |
