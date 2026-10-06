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
  hooks. The first 5b2 grepped the whole of `session-start.sh` and passed on a source comment
  while the printed lines used short names; it now reads only non-comment lines, and the
  banner prints the hook file names. A presence check must read what is shown, not the file
  that shows it. `docs/reviews/rca-changelog-at-release-2026-09-24.md` C1.

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
- **Recurred:** plan 021 (2026-09-24). Its leftover grep searched for `IHookInterface` and "hook
  interface → service", so `→ IHook →` in `submodule-lifecycle-and-harmony.md` survived; it swept
  for two of the amendment's three parts, so two audit prompts still graded every seam as ADR-007;
  and it matched phrases, so procedure steps that build the old shape (`decompiled-code-analysis.md`
  Phase 4, ADR-002 migration step 4) survived beside their amended diagrams. For a rule change, grep
  each part of the rule by its short tokens (`IHook`, `IXxxService`, `mocked adapters`, `sealed`
  plus `service`) and read every procedure that creates the thing the rule governs
  (`rca-architecture-rule-amendments-2026-09-24.md` C3 to C5, C7, Codex 3).
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

### A creature behaviour extended to a second Monster: compare the Monsters' size fields, not only the sets (2026-09-25)
The Brute Force tree scales its trigger range and ring by `AgentScale` only. The engine's reach is `Monster.ArmLength`
times `AgentScale`: the cave troll's `arm_length` 0.9 at scale 1.9, the hill troll's 2.79 at 1.09, three times the reach
per unit of scale, so one set of constants cannot fit both.
- **Why missed:** the extension checked Monster ids, action sets and clips, not the fields the engine sizes a body by.
- **Prevent:** when a tree or service gains a Monster, table `arm_length`, capsules, eye heights and the skin's scale
  for every Monster it serves, and scale distances by what the engine scales reach by.
- **Source:** `docs/reviews/rca-hill-troll-and-loc-sweep-2026-09-25.md` finding 7.

### A new permitted case amends every general rule it is an exception to, in the same change (plan 021, 2026-09-24)

Plan 021 made the hook interface conditional ("only when the patch needs a narrow seam or a test
fake") in AGENTS.md and three rule files, and in a separate step rewrote the always-loaded
`think-before-coding.md` to allow an interface only for an adapter, a fake or a second
implementation. The rule's own example hook has one implementation and no fake, so the general rule
forbade what the specific rules allowed. The same shape recurred four times in one change: ADR-008's
exception allowed "a static read" where ADR-007 allowed engine calls, the Standards lens said "One
exception" beside an ADR that has two, ADR-007 kept `CampaignTime` in its list of sealed classes
needing adapters while its new condition allows it, and the ADR-008 and ADR-002 checklists kept the
absolute wording their amended rules had just qualified.

- **Why missed:** each sentence was prescribed and checked on its own, by a substring gate that proves
  the sentence is present, never that two sentences agree.
- **Prevent:** when a change adds an exception or a permitted case, grep for every rule it qualifies
  (the general rule, sibling ADRs, checklists, severity tables, the section it sits in) and read each
  one against the new case, using the rule's own example as the test input: would a reader following
  that line reject the example? Fix or list each before committing.
- **Source:** `docs/reviews/rca-architecture-rule-amendments-2026-09-24.md` C1, C2, O3, O5, O6;
  Codex P2 1 and P2 2.

### Check a rule's cited exemplars against every condition the rule states (plan 021, 2026-09-24)

ADR-007's new boundary-seam exception names four services as users of the pattern, under four
conditions. The services factor seam bodies into private helpers that return `MobileParty`, `Hero`
and `CharacterObject` (outside the `protected virtual` members the exception covers), and several
seams carry TAOM decision logic (`SupplyOrderService.ChargePlayer` splits and routes the payment,
`RefugeService.FindNearestHostile` filters and picks a target), which condition 2 forbids. Read as
written, the next Standards pass would flag the exemplars the exception was written to legalise.

- **Why missed:** the Step 0 note that offered the looser wording checked one service's seam
  signatures, not the other three services and not what the seams call.
- **Prevent:** before an ADR or rule cites code as its exemplar, apply each condition to every cited
  class, including the private members its qualifying members call, and either narrow the claim to
  what passes or record the rest as known debt in the same text.
- **Source:** `docs/reviews/rca-architecture-rule-amendments-2026-09-24.md` O1, O2 (Agents 1 and 5).

### A plan's RED check names the diagnostics it needs, never the compiler's exact output (plan 026, 2026-09-24)
Plan 026's three RED checks required "exactly two lines" of normalized diagnostics, one of them
`CS0122` (the method under test still `protected`). The retained logs of all three runs contain only
the intended `CS0115` missing-override errors, so an executor following the plan literally could not
confirm RED, although the failures were exactly the ones the step wanted.
- **Why missed:** the plan predicted the compiler's full diagnostic set instead of stating what
  proves the test is RED.
- **Prevent:** a RED check requires a nonzero exit, at least the intended diagnostic codes, and only
  in the named test file; it permits other codes the same edit can cause and rejects any other file.
  This extends the plan 001 lesson above (build the project that holds the test).
- **Source:** `docs/reviews/rca-seam-decision-logic-2026-09-24.md` X1 (Codex P3).

### An absence claim ("no such item exists") needs an id grep of the live data before it is repeated (repeat)
A planning Explore agent searched the Armory for "Glamdring" and reported that no such item exists; the id is
`glamdring_sword`, and twelve more hero weapons and shields were missed the same way. The claim went to Mike and into
the feature doc, and the named-weapon list protected 4 of 17 items.
- **Why missed:** evidence-over-claims A.4 (spot-check a subagent's load-bearing claims before relaying them) was not
  applied to a negative claim from the planning phase.
- **Prevent:** before relaying "X does not exist", grep the live files by the id pattern (`id="[^"]*glamdring`), not by
  the display name, and list what the search did find.
- **Source:** `docs/reviews/rca-armour-acquisition-2026-09-27.md` row 3 (XML lens).

### When a decision reverses an invariant, grep its old words across code comments, config headers, MCM hints and the lessons
The weapon rung began awarding the named hero weapons, but "never change hands" (the MCM master hint) and "never
sold, looted or awarded" (a config comment and the config header) stayed, beside the feature doc's corrected table.
The same happened to the lessons: decision D13 (2026-10-03) reversed the per-frame PatchShield exclusion, and the
2026-09-26 and 2026-09-28 Prevent bullets in `harmony-il.md` still told the next patch author to exclude per-frame
targets, because D13's update went under one lesson only. A pass on the same decision then corrected two comments the
same way, one in `Dependencies/Foundation/PatchShieldPolicy.cs` and one in a test under `TAOM.Tests/`, folders this
lesson did not yet name.
- **Why missed:** only the doc's own table was rewritten; the invariant's other statements were not searched for.
- **Prevent:** grep the old invariant's key words across `Main/`, `Dependencies/`, ModuleData comments,
  `TaomSettings.cs`, the comments in `TAOM.Tests/` and `docs/reviews/lessons/*.md` (a Prevent bullet restates the
  invariant, and it is what the next author reads) before calling a reversed decision done. A repeat of RCA 2026-09-27
  row 19.
- **Source:** `docs/reviews/rca-lords-gear-ladder-2026-09-28.md` row 6 (Data flow A);
  `docs/reviews/rca-campaign-map-frame-profiler-2026-10-02.md` row F4 (D13 and the `harmony-il.md` Prevent bullets) and
  row G2 (the two comments, in `PatchShieldPolicy.cs` and `MissionTickProfilerBindingTests.cs`).

### Every engine claim written into a doc or a data header cites a line read that session, or says UNVERIFIED
The ladder's "Engine facts" and the materials header carried six wrong claims (towns never eat the materials; an
ItemCategory cannot be declared in XML; vanilla goods carry a Trade component; is_merchandise keeps them out of the
hideout pool; only a patch reaches the loot screen; the kill counter's mission list), each plausible, none re-read.
- **Why missed:** the text was written from research summaries, with spot checks on the load-bearing lines only.
- **Prevent:** treat every sentence about engine behaviour as a claim: cite its file and line from a read that
  session, or mark it UNVERIFIED. A repeat of RCA 2026-09-27 rows 3, 6 and 8.
- **Source:** `docs/reviews/rca-lords-gear-ladder-2026-09-28.md` row 16 (Engine, Data flow B).

### Cap a per-event diagnostic per run, and count what the cap drops (2026-10-02)
The plan 028 tick profiler wrote one `[Hitch]` line at INFO, a synchronous flush on the main thread, for every frame
at or above its threshold. The rate was bounded (1000/threshold lines a second) but the count was not: at the MCM floor
of 50 ms, a battle running under 20 fps wrote a per-frame INFO line for its whole length, which D6 rule 5 forbids, and
plan 041 turns the profiler on for every player.
- **Why missed:** the plan priced the stream at the default threshold only and deferred a cap before D6 existed; two of
  six review lenses judged it bounded because nothing was dropped.
- **Prevent:** any line written per event (frame, agent, hit) at INFO gets a per-run cap that keeps the first occurrences
  in full, one line where the cap starts, and a count of every event in the run's summary
  (`MissionTickProfiler.MaxHitchLinesPerMission`). Price a stream at its setting's floor, not its default.
- **Source:** `docs/reviews/rca-mission-tick-profiler-2026-10-02.md` row R1.

### A fault latch records its key before anything that can throw, and a fault keeps what was already measured (2026-10-02)
The plan 039 map profiler stopped measuring on a fault and relied on its session (`_session`, the campaign) to keep
the next frame from retrying. `OpenSession` stored the session after the provider reads, so a throwing read left no
session and every later frame opened it again and threw again. A guard added to log the fault once per session keyed
on a session number that only `BeginSession` advanced, so it muted the repeated throws instead of stopping them, and a
second campaign's identical fault logged nothing. The fault also dropped every frame already closed in the session,
against D6's per-session summary. Separately, a failed install warned that nothing would be measured only at the next
game init, which a one-campaign process never reaches.
- **Why missed:** the fault design was written for faults inside a measuring session; the open path's own failure was
  not walked, and a log guard was chosen where the latch was wrong.
- **Prevent:** a fault handler sets the state that stops the retry itself (here `Fault` takes the campaign and makes it
  the session) rather than relying on code that runs after the throw. Test the fault in the open path with a throwing
  substitute and `Received(1)` on the read (`Step_SessionOpenThrows_FaultsOnceAndDoesNotRetryTheSameCampaign`). A
  stop on fault writes the summary so far with `reason=fault`. A reason line is written at the event that causes it,
  not at the next opportunity to report.
- **Source:** `docs/reviews/rca-campaign-map-frame-profiler-2026-10-02.md` rows R8, R10 and R11.

### When a second measurer joins, re-read every "not measuring" line of the first (2026-10-02)
Plan 041 made Patch98's hitch probe measure every mission by default and left plan 028's tick profiler reason lines as
they were. On the default path, a player turning the profiler on mid-session got "no patches are installed and nothing
is measured" in a mission that then wrote `[TickProfile]`, `[Hitch]` and `[HitchDetail]`; the same held for the failed
install and switched-off lines, and the transpiler fallbacks still said "no mission is measured". The plan limited the
older file to one line, so the executor fixed that line and left its siblings.
- **Why missed:** the change was checked where it was edited; the older lines live in a file the plan froze, and their
  claim became false without any edit to them.
- **Prevent:** when a change adds a second instrument, grep the first one's status lines for "nothing", "not measuring",
  "no mission" and similar consequences, and choose each by what still measures (`HitchProbeLines.ProfilerNotTimingLine`,
  `BuildProfilerOffLine`). A scope limit on a file is not a reason to leave a line in it false; say so to the
  orchestrator instead.
- **Source:** `docs/reviews/rca-profiler-extensions-and-hitch-probe-2026-10-02.md` rows R3, R9 and R10.
- **Repeat (2026-10-04):** in the perf programme's integration merge, plan 028's hook health check, added on plan
  028's branch after plan 041 had branched, wrote "not measuring, required hooks missing" while the probe measured.
  Plan 041's sweep could not see a line its branch did not have, and the integration trial checked compile, tests and
  lost lines, not what a line claims. Rerun this sweep on the merged tree whenever branches that add or change
  instruments merge together. The line now has its probe-aware variant (`HitchProbeLines.BuildHooksMissingLine`),
  chosen unless the missing hooks include the frame boundary both modes need
  (`docs/reviews/rca-perf-integration-fixups-2026-10-04.md` F1).

### A benchmark's baseline arm runs in the state players had before the change, and its figure names what it stubbed (2026-10-02)
Plan 041's overhead benchmark installed the measuring profiler before timing its unpatched arm, so Patch91's agent-tick
pair did its measuring work in both arms and its cost cancelled out, though before plan 041 a default player's profiler
was null and the pair returned at once. The same figure, 0.43 us per frame, was quoted as the probe's cost while the
native clip-loading call was a stub returning false.
- **Why missed:** the baseline was set up in the patched arm's state for convenience, and "the benchmark" was read as
  covering everything a frame pays.
- **Prevent:** set the baseline arm to what players ran before the change (here, no profiler), and write every cost claim
  as "the managed part, X us; the stubbed call's cost is the line that measures it". Corrected figure: 0.477 and
  0.470 us.
- **Source:** `docs/reviews/rca-profiler-extensions-and-hitch-probe-2026-10-02.md` rows R13 and R15.

### A windowed diagnostic flushes its partial window before the summary (2026-10-02)
The `[AnimMem]` session wrote one line per 5 s window and a mission summary. Samples after the last 5 s line reached the log only through the summary, which has no minimum, so a partial window's low point was lost, against D6's "aggregate or sample, never drop".
- **Why missed:** the plan said "a summary at mission end", and nobody compared the summary's fields with the periodic line's to find the fields only the periodic line carries.
- **Prevent:** for any instrument with periodic and summary lines, write the partial window at the end whenever it holds samples, and pin it with a test that ends the session between two periodic lines.
- **Source:** `docs/reviews/rca-anim-memory-probe-2026-10-02.md` C2.

### A staged feature re-checks its inherited claims at every stage: summary rows, "live" settings, "every X"
Plan 040 landed in three stages. The feature-map row kept describing stage A; the toggle was documented
as read live with no restart, though a once-per-process guard in `SubModule.cs` decides the per-category
lines; and "every campaign handler of a loaded save" missed the `OnGameLoadFinished` fan-out that
`SandBoxGameManager`, not `Campaign`, dispatches.
- **Why missed:** each stage followed its own plan steps, which did not list the earlier summary text,
  and the claims were written once from the design rather than re-read against the guards and
  dispatchers that bound them.
- **Prevent:** at each stage's end, grep the feature's name across `feature-map.md`, the registry and
  the MCM hint and re-read every "every", "always" and "live" claim against the code that bounds it
  (the guard, the dispatcher list), not against the plan.
- **Source:** `docs/reviews/rca-load-time-stamps-2026-10-02.md` rows 4, 6 and 7 (Data flow, Completeness).

### A diagnostic's "nothing happened" line is written on the exact complement of its summary's condition (plan 030, 2026-10-02)
Plan 030 added a `no-creatures` INFO line for a mission whose creature diagnostics stayed silent. Its condition was the callbacks' gate (`AnyRegistered`), but a declined creature troop writes `spawn-declined` lines and makes the summary print without registering a creature, so the log ended with the summary and then a line saying no diag line was written.
- **Why missed:** the line was tested on the empty mission it was written for; nobody listed the other states that leave the gate false.
- **Prevent:** derive a skip or reason line's condition from the condition of the line it stands in for (here `WriteSummary`'s), so each mission ends with exactly one of them, and test the boundary case (a decline, an attempt) as well as the empty one. Prefer a count over an absolute claim ("no line was written").
- **Source:** `docs/reviews/rca-mission-diagnostics-diet-2026-10-02.md` R1.

### An aggregate that replaces per-event log lines keeps bad values out of its sums and says when it is written (plan 030, 2026-10-02)
Plan 030 replaced per-hit `[CareerPerks]` DEBUG lines with one line per combination plus a mission-end INFO tally. The tally added every hit's base and result unchecked, so one NaN hit turned the combination's damage totals into NaN, and it is written only at mission teardown, so a crash mid-battle loses the counts the per-hit lines used to leave on disk within 50 ms.
- **Why missed:** "keep the information as an aggregate" was checked for presence on clean, finite input.
- **Prevent:** an aggregate counts a non-finite event apart and keeps it out of every sum and range; its doc says when it is written and what a crash before then loses, and any claim that "nothing is dropped" is checked against that write point.
- **Source:** `docs/reviews/rca-mission-diagnostics-diet-2026-10-02.md` R4, R5.

### A deletion's doc sweep greps the deleted members' targets, categories and counts, not only their names (recurrence of #644, plan 030, 2026-10-02)
Plan 030 deleted Patch23's `EquipItemsFromSpawnEquipment` prefix and Patch35's `Mission.OnTick` postfix and grepped for the class names. A code comment and `battle-load-diagnostics.md` still said Patch23 patches `EquipItemsFromSpawnEquipment` (they named the category and the method), and `companion-tactics.md` still counted 5 FormationPresets hooks.
- **Why missed:** the plan's done-criteria grep matched class names only.
- **Prevent:** for a deleted patch, also grep its target method, its category paired with that method, and every count of its folder or category.
- **Source:** `docs/reviews/rca-mission-diagnostics-diet-2026-10-02.md` R7, R8.

### A mission-scoped write or reset is chosen from every way the engine leaves the mission (plan 030 Codex round, 2026-10-03)
Plan 030 wrote the career hit summary from `CareerPerkMissionBehavior.OnEndMission`. That callback runs only through `Mission.EndMission`; `GameStateManager.CleanStates` finalises a mission state with no `EndMission` first (the application shutting down, if a window close reaches the engine's shutdown callback, unverified; or a mod that loads a save mid-mission), so those exits lost the counts, and the stat service being a singleton, the dead mission's tally rode into the next one. The first wording named "a save loaded mid-battle" as a vanilla exit; it is not one, because only the map's escape menu and the main menu open the load screen.
- **Why missed:** the earlier review framed the limit as "a clean teardown versus a crash" and every lens took `OnEndMission` for every end.
- **Prevent:** before choosing where a mission-scoped write or reset lives, list the engine's exits from the decompile (`docs/reference/engine/mission-and-missionbehavior-lifecycle.md`, "Teardown paths"). Write from `OnEndMission` and `OnRemoveBehavior` when it must survive all of them (the second is a no-op after a normal end), and test the path that skips `EndMission`. Then check each listed exit from the caller side: a `CleanStates` caller on a load path counts only when some screen can open it while a mission is live (`SandBoxViewCreator.CreateSaveLoadScreen` has two callers, the main menu and the map).
- **Source:** `docs/reviews/rca-mission-diagnostics-diet-2026-10-02.md` X1.

### An aggregate's doc names what it keeps and what it drops, with a counterexample (plan 030 Codex round, 2026-10-03)
Plan 030's wording said the per-mission hit summary left nothing the per-hit lines showed lost. It keeps a count, a multiplier range and damage sums; hits of damage 10, 20 and 70 and of 10, 40 and 50 under one multiplier read the same, and the order, each hit's own numbers and a leaderless victim's agent are gone.
- **Why missed:** "aggregate, never drop" was checked as "an aggregate exists".
- **Prevent:** the doc for any aggregate that replaces per-event lines lists the statistics it keeps and the detail it drops, with one pair of inputs it cannot tell apart; "nothing is lost" is never written about one.
- **Source:** `docs/reviews/rca-mission-diagnostics-diet-2026-10-02.md` X2.

### A "no cost" or "no change" claim for a gated path is proven on that path (plan 030 Codex round, 2026-10-03)
Two claims held for the formula and not for the path they named. A refused creature event line "costs no string", but `WriteEvent` built its thread label above the budget check (88 bytes per refused event). A first-troll gate "changes nothing once a troll exists", but it starts the spacing tracker's self-throttled 0.5 s clock at the first troll tick, so a late troll is counted up to 0.5 s sooner and every later scan keeps the shifted phase, which lands a later re-space up to about 0.5 s sooner or later. The first fix wrote "never later", which held for the first scan only.
- **Why missed:** each claim was reasoned from the line or the formula it was written for, not from the consumer's own clock or the code above the guard.
- **Prevent:** prove the claim on that path. For "costs no string", an allocation count (`GC.GetAllocatedBytesForCurrentThread`, reached by reflection on net472) over every event kind on the refused path; for a gate in front of a self-throttled consumer, say where the consumer's clock now starts and bound the shift on every later scan, not only the first (a short simulation of the old and new call patterns does it).
- **Source:** `docs/reviews/rca-mission-diagnostics-diet-2026-10-02.md` X3, X4.

### A feature that schedules into a shared per-frame host scales the host's cost: re-check the host's per-frame path (Race Abilities, 2026-10-04)
`BehaviorTreeMissionLogic.OnMissionTick` snapshots its tree schedule every frame with `List.AddRange`, which on .NET Framework allocates a temporary array the size of the list. With a dozen creature trees that cost nothing; Race Abilities put every profiled soldier on the schedule, about 8 KB of garbage a frame at 1,000 trees. In the same feature, the sensor scanned 30 to 40 m for soldiers whose own state ruled the ability out, the report clock summed the telemetry every frame, and the settings provider resolved MCM on every read.
- **Why missed:** the design weighed the new tree's own once-a-second decision, not the shared host's per-frame work that now ran at a hundred times its old n, nor the per-frame and per-decision calls the feature itself added.
- **Prevent:** when a feature multiplies what a shared host iterates every frame, read the host's per-frame path at the new n for allocations and O(n) work, and pin an allocation-free copy with an IL test. In the feature, gate each per-frame or per-decision cost on whether its answer can matter yet (`RaceAbilityReportClock.IsDue`, `RaceAbilityService.PlanScan`), and give a hot-path settings provider the cached accessor and a row in `HotPathSettingsProvidersTests`.
- **Source:** `docs/reviews/rca-race-abilities-2026-10-04.md` R20, R21, R23, R24 (lens 3).

### A sweep over "every provider of kind X" is only as complete as its list: derive the list, never pick it (#745, #746, 2026-10-06)
Plans 031 and 037 moved the hot-path settings providers onto a cached MCM reference and pinned each in a gate. Both gates list their providers by hand. Five hot readers were never on the list: `RefugeSettingsProvider` (per refuge hit), `SignatureStrikesSettingsProvider` (per signature hit), the static `CreatureBanditTuning` (per creature hit, and it built a whole tuning each time), `SupplyLinesSettingsProvider` (per campaign frame) and `CampSettingsProvider` (per campaign frame while a camp stands). Race Abilities' R23 was the same miss two days earlier.
- **Why missed:** the sweeps found their targets by name and memory, and a gate whose rows are typed in by hand cannot fail for a provider nobody typed. Nothing told the author of a new hot reader the gates existed.
- **Prevent:** a rule line (`csharp-architecture.md` "Config Providers MUST Validate", item 9) now sends every new hot-path provider to its gate in the same commit. The structural fix is still owed: a ratchet test that scans every `TaomSettings.Instance` reader outside a lazy accessor, with the known per-event readers as its baseline. Not yet filed as an issue or plan.
- **Source:** `docs/reviews/rca-refuge-crews-creature-allies-2026-10-06.md` follow-ups; #745, #746 review (lens 4, C7).
