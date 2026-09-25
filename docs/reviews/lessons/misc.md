# Lessons — Misc

> Category file of the master lessons record — index + house shape: [LESSONS-LEARNED.md](../LESSONS-LEARNED.md). **Append new Misc lessons HERE** (`### rule` → `**Why missed:**` → `**Prevent:**` → `**Source:**`).

### A working reference implementation is the least informative thing in the room
Every wrong turn in the mumakil crew platform traces to one assumption: the elephant's howdah works, so the howdah's mechanism works. It was treated as proven design when it was a hack inside its envelope. The howdah is 3.2 m up, scale 1.0, one deck, two archers. The mumakil is 9 to 13.8 m, scale 3.0, three decks, eight archers. **Every one of those differences produced a defect**, and the clone inherited no rules for any of them because none existed. The previous day's RCA had already named this pattern and written the preventive action, and it still was not applied, because it read as being about geometry when it was equally about mechanism: the howdah's teleport is not a smaller version of what the mumakil needs, it is a different thing that resembles it.
- **Why missed:** a feature that visibly works in game is the strongest evidence available, and it is evidence about its own operating envelope only. Reading the reference's code tells you what it does, never what it would stop doing one variable out.
- **Prevent:** when cloning, write the difference list first (this is the existing rule), and extend it past geometry to MECHANISM: for each difference, ask not only "what rule did the original never need" but "does the original's approach still work at this value". Then ask the engine how it solves the problem natively, before adopting the reference's answer. One grep for the relevant engine field would have settled this architecture on day one instead of day two.
- **Source:** docs/reviews/rca-mumakil-navmesh-2026-09-22.md, root-cause section (#627)

### Cloning a feature onto a bigger instance: write the difference list BEFORE the first edit
TAOM clones features per creature by convention (spider, chariot, mumakil). The recognised risk is that the clone REGRESSES the original's hard-won rules, and the standard mitigation works: reference the original's measured constants from pure helpers rather than copying them, and a method-by-method review confirms nothing was dropped. What that mitigation cannot cover is the opposite direction, which is where the mumakil war tower's two real defects came from: the new instance has properties the original did not, so no rule exists for them yet. The howdah has one deck, scale 1.0 and a harness gate; the tower has three decks, scale 3.0 and no harness. Three decks produced a frame with an archer's head inside the deck above; scale 3.0 produced a native relative-scale trap and a tuning constant scaled by the wrong quantity; the absent harness produced a code comment that read as "not yet" beside a slot that must stay empty forever.
- **Why missed:** the clone's design doc listed the drift risk carefully and in only one direction. Every review lens then checked the clone against the original, which is the same direction again.
- **Prevent:** before the first edit, write one line per property in which the new instance DIFFERS from the original, and for each ask what rule the original never needed. Put the list in the feature doc. Two of three entries carried defects here, which is a high enough hit rate to make the list cheap. Ask the reviewers the same question explicitly: "what does this instance have that the reference did not?"
- **Source:** docs/reviews/rca-mumakil-platform-2026-09-20.md, root-cause pattern (#627 phase 2)

### Confirm a cumulative-isolation suspect with a single-variable control
When isolating a bug by disabling suspects **cumulatively** (off A, test; off A+B, test; off A+B+C, test…), a result only proves the last-disabled thing was **necessary** for the no-bug state — NOT that it is the **sole** or **sufficient** cause. After a cumulative ladder localizes a suspect, run the complementary single-variable control: disable ONLY that suspect (everything else restored) AND/OR enable ONLY that suspect (everything else off); the cause is confirmed only when both the necessity and isolated tests agree. Never say "X is the cause" / "X is the sole cause" from cumulative data alone — assume ≥2 independent contributors until a single-variable test clears the others.
- **Why missed:** Elephant "slide" debug, 2026-06-10 — concluded a root cause from cumulative results twice and was refuted both times by the user's control test. (1) "It's the `as_elephant` data" — refuted: the all-disabled build had no slide, so it was the C# crew, not data. (2) "The crew is the *sole* cause" — refuted: crew-off + everything-else-on **still slid**, revealing **bone-tracking** as a SECOND independent source (its `bo_` physics floor, `SetFrame`'d at the spine bone, overlaps the elephant capsule and the solver shoves it).
- **Prevent:** Honor the user's "disable one at a time" + "keep X off, turn the rest back on" control instinct; don't bundle multiple new disables per test (the rungs 2+3 bundle and skip-to-rung-6 were both corrected). Documentation cadence: update the feature doc each isolation step, not just at the end — losing the per-rung trail forces re-derivation.
- **Source:** memory/feedback_cumulative_isolation_needs_single_variable_control.md (elephant feature; companion to feedback_root_cause_mandatory + feedback_simpler_fix_first; generalized by the always-load evidence-over-claims rule)

### Build the full system, not an MVP or phased port
When porting or building a new feature system, implement the FULL system — not a "minimum viable" or phased-deferral approach. Scope ALL components (UI, battle effects, abilities, save/load, campaign behaviors, etc.) as first-class deliverables together; don't suggest deferring subsystems unless the user explicitly asks for phased delivery.
- **Why missed:** The user explicitly corrected a plan that proposed deferring abilities, mutations, battle effects, and career buttons to "phase 2."
- **Prevent:** When scoping an implementation plan, enumerate every subsystem up front and treat each as a required deliverable; flag any deferral for explicit user sign-off rather than assuming it.
- **Source:** memory/feedback_career_full_product.md (career-system feature)

### Don't add aspirational enum values or state fields
Do NOT add enum values, list fields, or status flags "for future use" unless there is a concrete caller in the SAME PR that produces or consumes them. Every enum value needs ≥1 producer (returns it) AND ≥1 consumer (matches on it); every status/outcome field must be populated by some code path AND read by some consumer. If you're tempted to write `// reserved for X`, delete it instead and add it back when X actually lands. Reserving names for future implementation introduces drift — a future session either builds UI around a value whose count is always zero (false promise to the user) or removes it as dead code and breaks the supposed future-extension promise; both are bad.
- **Why missed:** Deep-review #5 (EquipPresets, 2026-05-06) flagged a data-flow inconsistency: `SlotApplyOutcome.SlotLocked` was declared in the enum, had a `case` arm in the service switch, and `PresetLoadResult.SkippedLockedSlots` was a result field — but across the whole codebase zero places returned `SlotLocked` (the adapter only returned Equipped / ItemMissing / ModifierMissing / HeroNotFound / Failed) and the `SkippedLockedSlots` list was always empty. Caught via Data Flow Tracing (Agent 5 in the deep-review skill) reporting "X is declared but never returned" / "Y is a field but always empty."
- **Prevent:** Tests are the gate — if you can't write a test that exercises the enum value or field, it's not real yet; delete it. Counter-example that's OK: a future-tense enum value with a producer in the same PR even when the consumer is just a `_logger.LogDebug` line — the producer-consumer chain exists. Sibling rules: `feedback_user_facing_promise_must_match_code.md` (MCM hint vs implementation drift) and `feedback_dont_defer_high_review_findings.md` ("we'll fix it later" silent dismissal); this is the design-time member of that trio.
- **Source:** memory/feedback_no_aspirational_enum_values.md (EquipPresets feature; deep-review #5)

### Check the simplest config causes before deep investigation
When CC/rendering breaks for one race, check XML config references before investigating meshes or engine internals. Order: (1) `skins.xml` for missing action-set references (facegen, warrior, villager, etc.), (2) `monsters.xml` for missing/wrong attributes, (3) `action_sets.xml` for missing action definitions — only after ruling out XML config should you investigate mesh/engine-level causes.
- **Why missed:** The elf race broke the CC parent scene; extensive time was spent investigating body meshes, stitching flags, deform keys, and planning Harmony patches. The actual fix was a missing XML reference — `as_elf_facegen` needed to be added to `skins.xml`. The depth of the cause (engine-level mesh incompatibility) was assumed without first checking the simpler possibilities.
- **Prevent:** For any race-specific rendering issue, walk the XML-config checklist above first and rule it out before escalating to mesh/engine analysis.
- **Source:** memory/feedback_simpler_fix_first.md (character-creation / race rendering)

---

### A log line names the field its author intended, not the field its name suggests

`[CultureMarketplace] town_K1 (battania)` was read as "the settlement's culture is battania" and sent the investigation down a wrong branch — it looked like proof that TAOM's culture conversion had already retagged the town, which would have ruled out the actual root cause. The line is fed by `ITownRosterAdapter.GetCurrentCultureId(Settlement)` → `settlement?.OwnerClan?.Culture?.StringId` — the **owner's** culture. Worse, `ICultureConversionAdapter.GetCurrentCultureId(string)` is a same-named method on a sibling adapter that returns the settlement's *own* culture, so reading either one in isolation confirms whichever reading you started with.
- **Why missed:** the diagnostic was trusted at face value because its name (`GetCurrentCultureId`, printed next to a settlement id) matched the hypothesis under test. A same-named sibling with different semantics made the mistake self-confirming.
- **Prevent:** read the getter before reasoning from a diagnostic — especially when that line is the *only* evidence contradicting a hypothesis. When two adapters expose the same method name, check which one the log call site actually holds.
- **Source:** `docs/reviews/rca-landless-culture-spawn-2026-08-04.md` (#374, landless-culture CTD)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/modding/body-properties.md](../../modding/body-properties.md)
- [docs/modding/troubleshooting.md](../../modding/troubleshooting.md)
- [docs/reviews/LESSONS-LEARNED.md](../LESSONS-LEARNED.md)

<!-- backlinks-end -->
### The artifact a reporter attaches may not be the crash they are reporting

**Symptom:** a player reported *"every instance that I attempt to find a female dwarf in a
settlement/battle/tournament, if my camera looks at them it crashes my game, but it will not crash if
I interact with them in dialogue"* and attached a tournament crash bundle from a dwarf campaign. The
bundle and the complaint were **two unrelated defects**:

| | Tournament NRE (#407) | Female dwarves (#403) |
|---|---|---|
| Kind | Managed `NullReferenceException` in a vanilla VM | Native AV `0xC0000005` |
| Evidence | Full crash bundle, clean managed stack | **No bundle at all** |
| Cause | Unguarded `hero.MapFaction.Color` | One unresolved mesh name in `skins.xml` |
| Sex/race relevance | None | Entirely |

**Why missed:** the attachment was treated as evidence *for the stated complaint*. It was evidence for
a different, real bug that happened to be in the same save.

**Prevent:** when a report's symptom and its artifact do not obviously describe the same event, treat
them as two investigations until one is shown to explain the other. And read "no crash bundle" as
positive evidence of a native fault, not as absence of evidence — TAOM's CrashReport finalizers only
see exceptions that cross a managed boundary. Corroborating record:
`investigation-rhun-dwarf-ctd-2026-08-02.md` Established #3.

**Source:** `docs/reviews/rca-patch69-tournament-guard-2026-08-07.md` (#403 / #407).

### Dropping a donor setting drops the behaviour of its DEFAULT value

Porting a donor mod, three separate behaviours were removed as YAGNI because they were expressed as
config knobs nobody wanted to carry: `AllowMultiplePromotions`, a roster precondition, and an
`IsHero` filter on an upgrade walk. In each case the knob was genuinely not worth porting — but the
value it defaulted to WAS the donor's shipped behaviour. Deleting the knob silently deleted the
default. `AllowMultiplePromotions=false` was the only thing capping promotion offers at one per
battle; without it a won battle could raise dozens of consecutive game-pausing modal prompts.

The mirror case, from the same port: FIXING a donor bug can remove an unnamed side effect. The donor
deducted merit when an offer was queued (a real bug — declining destroyed earned merit). Fixing it
correctly also removed the only thing suppressing the re-ask, so the same soldier was proposed after
every won battle forever.

**Prevent:** when dropping a donor setting, write down what its DEFAULT value did and either keep that
behaviour as a constant or record explicitly that you are changing it. When fixing a donor bug,
enumerate what the buggy behaviour was incidentally providing before removing it. "The knob is YAGNI"
and "the behaviour is YAGNI" are different claims and need separate answers.

**Source:** `docs/reviews/rca-field-commission-2026-08-07.md` findings 1, 3, 6, 10.

### Never promote a subagent's factual detail into a durable artifact without running the check yourself
A research subagent reported that Python's `wave` module "misreports IMA ADPCM by roughly 10x". The
surrounding argument was sound and its conclusion was right, so the detail was written verbatim into
a code comment justifying a design choice. It is false: `wave` does not misreport ADPCM, it raises
`wave.Error: unknown format: 17`. The command that settles it is two seconds long. In the same
changeset a docstring asserted "every mp3 in this tree is CBR" (38 carry a Xing/Info VBR frame, and
a VBR clip can measure SHORT, the unsafe direction for a length gate) and a doc table asserted a
`VoiceType` had exactly one call site (a second exists in the character-creation facegen preview).
- **Why missed:** all three claims were specific, checkable, and felt settled, so the seconds were
  not spent. The subagent one is the sharpest: `evidence-over-claims.md` §A.4 already names exactly
  this failure, and a persuasive report made its incidental details feel verified by association.
  A confident subagent report is a claim, not evidence, and its supporting details are separate
  claims from its conclusion: adopting the conclusion does not adopt the reasons.
- **Prevent:** before a subagent-sourced fact goes into a code comment, doc table, CHANGELOG or
  commit message, run the one command that proves it. If the claim is not worth the seconds, it is
  not worth stating: cut it, or attribute it as unverified. Comments that justify a design choice
  are load-bearing precisely because the next reader will not re-derive them.
- **Source:** docs/reviews/rca-dwarf-voices-2026-09-06.md (2026-09-06 deep review)

### A correction is a new claim: measure it before you write it (#617, 2026-09-19)
Fixing a wrong sentence means writing a new one, and the new one carries every risk the old one did.
The #617 second review found the docs calling the arrow's `missile_speed` dead and replaced that with
"it feeds the arrow's tier and price"; `DefaultItemValueModel.CalculateAmmoTier` reads damage and
stack size only. The same round wrote that `characters/lords.xml` carries "empty inline blocks" (all
1,164 templated lords carry 18 rows) and that a troop-versus-troop arrow is "pure engine" (the career
`TroopDamage` passive multiplies troop hits). The first RCA of the same feature carried a fabricated
"why missed" written the same way.
- **Why missed:** a correction feels like the end of a check rather than the start of one. The
  reviewer found the old sentence wrong with evidence, then wrote its replacement from memory in the
  same edit, and nobody re-read the replacement against the source.
- **Prevent:** before writing the replacement for a sentence a review proved wrong, run the one read
  or count that proves the new sentence, as you did for the old one. When the fix itself goes to
  review, point the engine and data lenses at every sentence the fix changed, not only the code.
- **Source:** `docs/reviews/rca-ranged-rebalance-second-review-2026-09-18.md` "The fix-diff review",
  F3; first-RCA audit item 6.
- **Recurred:** 2026-09-23 (#644, Codex review 130). The deep review found the restore-skip comment's
  "nothing changes a wraith's race at runtime" false and replaced it with "the only runtime writer is
  the player's own face editor or character import", naming the writer the lens had found. Codex found a
  third, co-op join reconciliation (`JoinReconciliationService.ApplyRace`). An "only" claim is an
  enumeration: its measurement is a grep of every writer (the `SetHeroRace` callers plus the
  engine's own), run before the replacement is written.

### Triage a crash from logs by reading every line: the deciding fact is rarely an error (#635, 2026-09-22)
A silent-CTD report came with two logs. The first pass read their tails and grepped for
`exception|error|crash|warn`, found nothing past a render census, and built a theory around the tableau
patches nearest the last line. The user asked why the logs had not been read in full. The full read,
with repeated noise collapsed by shape rather than skipped, surfaced the one fact that set this
conversation apart: of six conversations in the session it was the only one started from a menu and
the only one that played a voice line with lip-sync. Every earlier conversation logged
`Voice object for text id is not found` and ended with `Conversation End`; this one logged
`Conversation sound playing` and nothing after. None of those lines is an error.
- **Why missed:** error-keyword grep answers "did something complain", and a native crash complains
  nowhere. What separates the crashing path from the healthy ones is ordinary INFO traffic that looks
  like noise until it is compared with the same event earlier in the session.
- **Prevent:** for a log-only crash report, read both logs in full before stating a cause. Collapse
  high-volume lines with a `sort | uniq -c` pass on the message shape, then read everything else in
  order. Put the crash moment next to the same kind of event earlier in the session that did not
  crash, and list what differs.
- **Source:** issue #635; `docs/features/hero-race.md` "Open: Map-Conversation CTD With a Voiced Elf Speaker".

### A doc that says "this never happens" is a claim about code: check the code before ranking a hypothesis on it (#635, 2026-09-22)
`kingdom-voices.md` said no TAOM culture could match a voice-over file, because `GetAccentClass`
returns `""` for them. The #635 triage used that sentence to rank lip-sync on a custom-race head as
the leading suspect ("a path TAOM almost never exercises") and wrote the argument into the issue, the
plan and memory. The code says otherwise: with an empty accent, `GetSoundPathForCharacter`'s last tier
builds the regex `.+_.+`, which matches any gendered vanilla voice path, so TAOM characters speak
random vanilla lines with lip-sync all the time. The log already in hand contradicted the doc:
`accentClass: ` (empty) followed by `[VOICEOVER]Sound path found`.
- **Why missed:** the doc sentence read as settled research, and it agreed with the theory being
  built, so nobody opened `DefaultVoiceOverModel` to check it. The contradicting log line had been
  read, but it was never tested against the claim.
- **Prevent:** when a hypothesis leans on a documented negative ("never", "dead", "cannot match"), open
  the method the doc summarises before ranking on it, and look in the evidence for a line that would
  be impossible if the doc were right. A negative claim about a lookup with fallbacks is the most
  likely kind to be wrong (`csharp-architecture.md` "Lookup Functions With Fallbacks").
- **Source:** issue #635 (correction comment); `docs/features/kingdom-voices.md` "Dialogue voice-over", corrected 2026-09-22.

### A change that deletes or moves data invalidates numbers and line refs elsewhere: grep for those too (#644, 2026-09-23)

#644 deleted three `characters/lords.xml` rows and inserted nine `lords.xslt` lines. The stale-text
sweep grepped for how docs PHRASE the old facts and fixed those, but the completeness lens still found
row counts (1184, 179 in both files, uruk 59 and 163), a line citation (`lords.xslt:1060`), a
paraphrase the grep missed ("NOT reachable by race") and a how-to that now advised the opposite of the
new rule, across eleven files.

- **Why missed:** the sweep looked for the old facts' wording, not for the measurements a deletion
  changes, and it covered the docs that mention the subject rather than the docs that own the changed
  code and data.
- **Prevent:** after a data change, grep the docs for the old counts and every `file:line` citation
  into the edited files, open the owning feature doc of each changed class, and re-measure a count
  with the command its doc prints rather than subtracting by hand.
- **Source:** `docs/reviews/rca-nazgul-race-2026-09-23.md` finding 5 (2026-09-23).
- **Recurred:** the same day, in the #645 scream swap. Replacing the sound deleted the prompts from
  the doc's Sound provenance section, which the committed #645 CHANGELOG entry still cites for them,
  and left the shipped config comment and the feature map calling the sound ElevenLabs-generated.
  Grep for pointers INTO a rewritten section (its heading text) as well as for its old facts.
  `docs/reviews/rca-nazgul-scream-2026-09-23.md` W3, W8.
- **Recurred:** plan 025 (2026-09-24). Deleting the unwired path-reuse scaffold made the feature
  doc's config-test count wrong (20, then 22 at `032481cc`) and left `NavigationPath` in its
  Dependencies list, though the deleted files were its last users in the feature. The change dropped the doc's
  "103+" total because nothing computes it, yet kept the per-bullet counts beside it. When a
  deletion removes the last user of a type, grep the owning doc's Dependencies list for it, and
  drop hand-kept counts rather than patching one. `docs/reviews/rca-delete-unreachable-scaffolds-2026-09-24.md` F3, F4.
- **Recurred:** plan 020 (2026-09-24). Retiring one python-only gate, the change edited the
  `session-start.sh` degraded banner from "five" to "four" by subtraction; the list had
  been one short since 2026-09-14, so five gates were open and the banner named four. The
  banner now carries no count, and `tools/test_hooks.sh` 5b2 derives the gate list from the
  hooks. `docs/reviews/rca-changelog-at-release-2026-09-24.md` C1.

### A claim found wrong is wrong everywhere it was written: grep for it before fixing the copy in front of you (#644, #645, 2026-09-23)

Twice in one session a correction reached one surface and missed the others. The #648 count was
corrected to 176 in the issue while the #644 CHANGELOG entry kept "the other 173", a hand subtraction
from 179 that was wrong from the start (the #644 lesson above forbids subtracting by hand). And
checking a lesson's wording showed that the spider AV `CustomAttacksUtils` guards against was later
traced to `HandleBlowAux`; the RCA row and the lesson were corrected, but the same claim already sat
in `StrikeSoundPlayer`'s comment and `signature-strikes.md`, both committed.

- **Why missed:** each correction was made where the error was noticed, and the fix felt complete
  because the surface being edited was now right. Nothing prompted a search for the other copies.
- **Prevent:** when a claim turns out wrong, grep the whole change (committed files, the scratch
  drafts of the CHANGELOG, issue and commit text, and memory) for the claim's distinctive words or
  number, and fix every hit in the same edit.
- **Source:** Codex review 130 (the S4 cross-check); `docs/reviews/rca-nazgul-race-2026-09-23.md` and
  `docs/reviews/rca-nazgul-scream-2026-09-23.md` "Codex pass".
- **Recurred:** the same day. Fixing the retracted spider claim in `CustomAttacksUtils` corrected the
  comments' conclusion but kept their sink list (`Mission.OnAgentHit`, which is managed) and their
  NaN example; the same wrong list sat in the test class and in the #645 lesson in
  `lessons/adapters-taleworlds-api.md`, and the retracted cause in
  `rca-spider-directional-attacks-2026-06-15.md`. A correction re-reads every clause of the text it
  keeps, not only the one found wrong.

### A performance claim names the machine that measured it, and the player figure when one exists
Plan 006's CHANGELOG line said crash capture "no longer costs about 30 s of every boot". The 30 s was the maintainer's desktop (about 186 ms per Harmony attach there); the audit committed in the same range measured the same 247-attach sweep at 0 to 1 s on 11 player processes. The CHANGELOG feeds the player-facing release post, so the overclaim would have reached players.
- **Why missed:** the plan quoted desktop log gaps as "every launch", and the comment, test message, feature doc and CHANGELOG each copied the number without its scope.
- **Prevent:** a measured cost or saving in a CHANGELOG, comment or doc says where it was measured; when the machine is known to be atypical (`plans/_audit/2026-09-23-opus/followup-patch-tax.md`), give the player figure next to it or leave the number out of player-facing text.
- **Source:** `docs/reviews/rca-crash-capture-boot-cost-2026-09-24.md` F2.

### Text that promises a switch "takes effect immediately" names every side effect the switch cannot undo
Plan 006 made the crash-capture master toggle live and rewrote the hint and how-to to say the game's handler "(or BUTR)" takes over without a restart. `CrashReportService` calls ButterLib's `Disable()` on every capture while Suspend BUTR is on, and TAOM has no path that re-enables it, so after any capture in the session BUTR stays off until the player re-enables it on ButterLib's page or restarts. The old text ("Restart the game") had been true by accident.
- **Why missed:** the rewrite traced the toggle's own reads, not the state earlier captures had already changed.
- **Prevent:** before writing "live" or "no restart" for a toggle, list what the feature did while the toggle was on (suspended handlers, installed hooks, persisted state) and say which of those turning it off does not reverse.
- **Source:** `docs/reviews/rca-crash-capture-boot-cost-2026-09-24.md` F3.

### A change to a list's membership re-reads every text that describes the list: counts, "left out" examples and player-facing hints
D47 took the crash-capture allowlist from 6 to 16 entries. The same commit left the MCM hint describing only the original six ("character tableau callbacks", no combat), two reference-doc lines saying "six", and an owed in-game probe whose example of a *dropped* callback (`OnAgentRemoved`) was one of the entries just added, so the probe could no longer fail. This repeats the 2026-09-23 lesson above ("A change that deletes or moves data invalidates numbers and line refs elsewhere"), this time for additions.
- **Why missed:** the executor updated the texts that name the list's members and grepped for the class name, but not for the count word, and did not re-check examples chosen to lie outside the list.
- **Prevent:** after changing a list's members, grep the old count spelled both ways ("six", "6 of 6"), every "for example" that points inside or outside the list, and the hint of the toggle that governs it. A player-facing hint is part of that set even when the file is outside the diff.
- **Source:** `docs/reviews/rca-crash-capture-boot-cost-decisions-2026-09-24.md` F3 to F5 (lenses 1, 2, 4, 5, 6 and Codex P3).
### A plan's stale-claim grep is a floor: search the whole repo, tests included, by distinctive words (plan 014, 2026-09-24)

Plan 014 rewrote the claim "ResetSessionCaches is wired to OnGameLoaded only" and its Step 7 gate
grepped `EnlistmentReconciler.cs` for it. The executor ran the gate as written and it passed, while
the same claim sat in `EnlistmentReconcilerTests.cs:835-836`, split across two lines so no phrase
grep could have matched it. Four review lenses caught it; Codex, which checked the same file the
plan named, did not.

- **Why missed:** the gate was scoped to the file the plan changed, and it searched for a phrase. A
  test comment is a copy of the claim too, and a line break splits a phrase.
- **Prevent:** after rewriting a claim, grep the whole repo (`Main`, `TAOM.Tests`, `docs`) for one or
  two of its distinctive words (`OnGameLoaded only`, `wired to`), never for the full sentence, and
  treat a plan's narrower grep as the minimum, not the check. Recurrence of the #644 and #645 lesson
  above.
- **Source:** `docs/reviews/rca-enlistment-session-scope-2026-09-24.md` finding 1.
### A scope word in a claim is checked by a grep over that scope; a behaviour difference is stated as the condition the code tests (plan 015 decisions, 2026-09-24)
The decisions commit said "no node is a service locator" while the same `BuildTree` built three `LogTask`s that resolve per Execute (the first review had already corrected the same `LogTask` overclaim in the feature doc). It also said a skeleton missing during the wind-up "ends the bite when the hit window opens", but the code tests the skeleton at the first in-window tick, so one that is back by then lets the bite go on and hit.
- **Why missed:** both were written from intent (the decision, the scenario pictured) and not from the code: no grep over every node the tree builds, no read of the condition.
- **Prevent:** before writing "no", "every", "all" or "none" about a set, grep the whole set it names and quote the count; scope the claim to what the grep covered. State a behaviour difference as the condition the code evaluates and when it evaluates it, then list the cases it allows, not only the one expected.
- **Source:** `docs/reviews/rca-warg-tick-costs-decisions-2026-09-24.md` F3 and F6 (Agents 1, 4, 5, 6; Codex P3-2).
- **Recurred:** plan 019's maintainer decisions (2026-09-24). Decision 2 closed the no-settlement
  fallback, and the commit recorded that in `siege.md`, but `harmony-patch-registry.md`, the
  crash-triage entry point, still called it "an open decision". The same commit set the Key Files
  test count to 30 while the Tests section of the same file kept "17 tests", although the first
  review had named both lines. A closed decision or a new count is a claim: grep for the old
  wording ("open decision", the plan section's name, the old number) before committing
  (`rca-nullable-ratchet-decisions-2026-09-24.md` #2, #3).

### A pointer to a procedure points at the knowledge base, never at a plan (plan 019, 2026-09-24)
Plan 019's CHANGELOG said the procedure for graduating the next folder was in `code-quality.md`, "How nullable is enforced". That paragraph held the mechanism only; the steps, the fix rules (no `!` on an engine value, `= null!` only with an owner comment) and the hotfix escape lived only in the plan's "Maintenance notes", and both `.editorconfig` comments cited "plan 019". `plans/README.md` calls plans a working backlog, not a knowledge base.
- **Why missed:** the plan's docs step added only the mechanism to `code-quality.md`, and nobody opened the pointer's target to find the promised text. Five of the six review lenses flagged it afterwards.
- **Prevent:** when a plan's maintenance notes hold a procedure later work must follow, the executing change moves it where ADR-011 routes it and points every comment and CHANGELOG line there. Before committing a pointer, open its target and find the promised text.
- **Source:** `docs/reviews/rca-nullable-ratchet-2026-09-24.md` #4.
### A plan's RED step builds the project that holds the test, and names the test filter (plan 001, 2026-09-24)
Plan 001 said building `Main/TAOM.csproj` would fail because a new test calls a missing handler, and
called that compile failure the RED state. The test lives in `TAOM.Tests`, which references `Main`, not
the other way round, so that build cannot see the test at all.
- **Why missed:** the plan reasoned from "the method is missing" without asking which project compiles
  the call.
- **Prevent:** a handoff plan's RED step runs
  `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter FullyQualifiedName~<Class>`
  and states the expected failure (a CS error in the test project, or a named failing assertion).
- **Source:** `docs/reviews/rca-cross-campaign-singleton-resets-2026-09-24.md` F8; Codex P3.
### A handoff plan's prescribed sentence is a draft: re-derive its history and engine claims before committing it (plan 025, 2026-09-24)

Plan 025 prescribed its doc and CHANGELOG text word for word, and the executor copied it. Four of
those claims were wrong: the binding row's recovery commit (`6a80bac6`; `git log -S` finds
`41258657`), "never in the shipped JSON" (the shipped config carried both keys from `6a80bac6` until
`b5cb3018`), Phase 1 paths being reusable in Phase 2 (v1.5.3 keeps the path local, and Phase 2
pathfinds with a different cost multiplier), and a citation said to end "one line further off" that
the deletion made exact. Its "keep the rest of the line unchanged" also left "That alone is a 2-3x
win" pointing at the deleted scaffold, and Codex found its Step 6 sentence contradicted its own done
check.

- **Why missed:** the plan was specific and cited commits, so its text read as verified; the
  executor re-checked code facts but not the history and engine claims inside the prose.
- **Prevent:** when executing a plan, treat each prescribed sentence as a new claim. Run
  `git log -S` for every commit it names as an item's origin, `git log` the file behind any "never"
  about history, check an engine claim against the installed DLL, and re-read the neighbouring
  sentence once the edit lands. A plan author (`/improve`) cites the evidence for each claim it
  prescribes, or marks the sentence as a draft.
- **Source:** `docs/reviews/rca-delete-unreachable-scaffolds-2026-09-24.md` F1, F2, F5, F7.
