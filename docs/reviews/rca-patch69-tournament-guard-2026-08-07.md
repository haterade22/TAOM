# RCA — Patch69 tournament winner-panel guard + female-dwarf mesh CTD (2026-08-07)

**Issues:** [#407](https://github.com/haterade22/TAOM/issues/407) (tournament NRE),
[#403](https://github.com/haterade22/TAOM/pull/403) (female dwarf mesh, found by @Sternab).
**Review:** `/deep-review`, 6 agents (5 core + a tooling-correctness agent), plus a Codex
adversarial pass. Prompt: `codex-adversarial-patch69-tournament-guard-2026-08-07.prompt.md`.

## Top-line

Two unrelated crashes arrived as one report. The reporter said *"female dwarves crash my game"* and
attached a **tournament** bundle from a dwarf campaign. Triaging the attachment found a real,
separate defect; the complaint itself was a different defect entirely, in data, with no managed
signal at all. **Neither would have been found by investigating the other.**

The review produced **eight findings: two CRITICAL, three MEDIUM, three LOW** — and the two
CRITICALs came from the independent Codex pass after all six Claude agents had cleared the same
code. No finding invalidated the design; every one was fixed before commit. Nothing was deferred.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | MED | The roster-guard postfix emitted a durable, synchronously-flushed INFO line on **every** call to `GetParticipantCharacters`, not once per tournament. Four call sites exist; `GetMenuText` + `GetTournamentPrize` both run from the arena join menu's `on_init`, so every menu open wrote to disk. | Logging / call-frequency | I established that `GetParticipantCharacters` was "not per-frame" and stopped there. Not-per-frame is not the same as once-per-event, and the doc comment I wrote asserted "once per tournament" without my having enumerated the call sites. | Clean roster now logs DEBUG (async); substitutions stay WARNING. **Rule extracted:** before writing a durable log in a postfix, enumerate the target's call sites — do not infer frequency from the method's name or its "expensive" shape. |
| 2 | LOW | Doc comment claimed `TournamentMatch.AddParticipant` "reads `participant.Team` with no null check". `.Team` **is** null-checked; `participant` is not. | Doc accuracy | Ambiguous phrasing on my part. The mechanism was right, the sentence was not — and it misled a review agent into reporting the design premise as refuted. | Reworded to name the actual unchecked reference, and the full chain (fixed 16-slot array → `FillParticipants` → null slot). A design justification has to survive being read by someone who will check it. |
| 3 | LOW | A `Safe` verdict does not guarantee `Hero.ClanBanner` is non-null: a hero can pass on a `MapFaction` resolved from `HomeSettlement` while `Clan` is null. It does not crash only because `BannerImageIdentifier`'s ctor null-checks independently. | Cross-system data flow | `Classify` was designed to mirror the winner panel's two colour branches exactly. It does that correctly — but "mirrors the branches I looked at" is not "covers every dereference in the method". I enumerated the two I was fixing, not all ten. | Documented in `ITournamentRosterGuardService` as safe-by-coincidence with the engine property it depends on named, so an engine change reopens it loudly rather than silently. |
| 4 | MED | The `skins.xml` fix existed only as an inline one-off invocation. The snapshot README itself says an Armory update silently reverts these edits — nothing would have re-applied it. | Tooling / process | I followed the README's *documented edit procedure* (patch live + snapshot, `.bak-` backup) and treated that as complete. The README also documents that these edits get reverted, and every comparable fix (`fix_uruk_hai_hands_teamcolor.py`, `fix_orphaned_tavern_conversation_actions.py`) ships a re-runnable script — I matched the procedure but not the pattern around it. | Authored `tools/oneoff/fix_dwarf_female_underwear_mesh.py` (dry-run default, `--apply`, `--revert`, idempotent, non-`.xml` backups) + a `tools/README.md` row naming the re-run condition. |
| 5 | LOW | I rewrote `tools/README.md` with `newline='\n'`; the file is committed CRLF, so a 1-row change became a 297-line whole-file diff. | XML/text I/O convention | `.claude/rules/moduledata-validation.md` scopes its I/O convention to *ModuleData XML*. This was a `.md`, so I did not apply it — the identical failure mode, one file type outside the rule's stated scope. | Restored from HEAD and re-applied byte-preserving. **Rule extracted:** the read-binary/write-binary discipline is about *any* tracked file whose line endings or BOM you did not author, not about the `.xml` extension. |
| 6 | **CRITICAL** (Codex P1) | Both Patch69 files exceeded ADR-002's hard `<150` entry-point limit — 175 and 161 lines. | Architecture / ADR-002 | Two compounding causes. The deep-review Standards agent measured 154/153 and classified it as an "acceptable margin case" on the reasoning that the *methods* were thin — a carve-out that is not in the rule. Then my own review-response doc-comment additions pushed both further over. **A hard numeric limit has no margin; that is what makes it a hard limit.** | Extracted `TournamentBracketFormatter` (the dump) and `TournamentEntrantMapper` (sealed-type conversion + filler resolution) as boundary classes, following the `SpawnOriginFormatter` precedent. Now 86 and 133 lines. |
| 7 | **CRITICAL** (Codex P1) | — as above, second file. | Architecture / ADR-002 | Same. | Same. |
| 8 | MED (Codex P3) | The finalizer swallowed **every** exception class, not just the null-dereference it exists for. | Harmony finalizer semantics | I wrote it as "contain the crash" without enumerating what else can throw there. `OnTournamentEnd` opens with `Round4.Matches.Last(m => m.IsValid)`, and `Last(predicate)` throws **InvalidOperationException** when nothing qualifies — I had *already identified* that in triage and still let the finalizer eat it, turning a real bracket bug into a silent half-drawn screen with no bundle. | Swallows `NullReferenceException` only; everything else is rethrown after the dump so it still reaches CrashReport. Same rule PatchShield already follows (eat only the engine-drift trinity, rethrow the rest). |

## The agent-vs-Codex disagreement, which is the most useful result here

Findings 6/7 are the headline. The deep-review **Standards agent measured the violation and then
excused it** — "file size slightly exceeds 150 lines but patch methods are thin… acceptable margin
case." Codex, given the same files, rated the identical fact **P1 CRITICAL**. Codex was right:
`CLAUDE.md` states the limit as `<150 lines` with no method-thinness carve-out, and ADR-002 exists
precisely so entry points do not accumulate exactly the kind of helper code that had accumulated
here.

Two lessons, both about how to read a review rather than about tournaments:

1. **An agent that reports a measured violation and then supplies its own exemption is the most
   dangerous review output there is** — more dangerous than a miss, because it launders the finding
   as considered-and-cleared. Treat "technically over, but acceptable because…" as an unresolved
   finding unless the exemption is written in the rule.
2. **This is what the independent adversarial pass buys.** Five Claude agents and I all saw the line
   counts; only the reviewer with no stake in the code called it. That is the documented purpose of
   `/review-codex` and it earned its cost on this changeset.

## Root-cause pattern: scope-one-narrower

Findings 1, 3 and 5 are the same shape — **the check I ran was one category narrower than the
defect.** Not-per-frame vs not-per-event (1). The branches I was fixing vs every dereference in the
method (3). ModuleData XML vs any file with line endings I did not author (5).

This is the same shape the repo has recorded five times for NaN gates
(`.claude/rules/csharp-architecture.md` "Engine-Float Decision Gates" — each recurrence happened
because the rule's scope was one category narrower than the bug). It is worth noting that the
pattern is not specific to NaN: it is what happens whenever a correct-but-narrow check is treated as
a complete one. The generalisable habit is to state the category you checked *out loud* — "I checked
per-frame" — because the narrower phrasing makes the gap visible in a way "I checked the frequency"
does not.

## Why each agent missed what it missed

- **Standards** reported two ADR-007 violations in `MissionDiagnosticService` that are **pre-existing
  and untouched by this change** (`git diff` on that interface is one line). The agent reviewed the
  file, not the diff. Not a defect in the agent's rules — a scoping instruction I should have made
  explicit in the prompt.
- **Efficiency** correctly refused to rate finding 1 without call-frequency evidence and returned it
  as needing verification rather than inventing a severity. That is the prompt's
  "unverified-is-not-HIGH" rule working as intended; the resolution required decompiling the callers,
  which the orchestrator did.
- **Completeness** found the missing GitHub issue (now #407) and miscounted the test methods as 12
  (actual: 13). A count is checkable in one command; taking it on faith would have put a wrong
  number in the registry.
- **Data flow** found findings 1 and 3, and independently killed the highest-value hypothesis I had
  flagged — that a tournament surviving a save/load would restore unguarded participants. It does
  not: `TournamentBehavior` is a `MissionLogic` with zero `Saveable*` attributes anywhere in its
  object graph, so the bracket cannot survive a save/load by construction.
- **API compatibility** verified all 9 API claims and both design premises against the installed
  DLLs, and independently reached finding 2's wording correction.
- **Tooling correctness** found findings 4 and 5. Neither was reachable by the five C#-centric core
  agents — this is the second time the dedicated tooling agent has been the only one to catch a
  data-mutation issue (first: `rca-scene-tooling-2026-05-28.md`).

## The triage lesson, which is the durable one

A reporter's theory names the symptom they noticed, not the defect — and **the artifact they attach
may not be the crash they are complaining about.** Here the bundle and the complaint were two
different bugs:

| | Tournament NRE (#407) | Female dwarves (#403) |
|---|---|---|
| Kind | Managed `NullReferenceException` | Native AV `0xC0000005` |
| Evidence | Full crash bundle, clean stack | **No bundle at all** — never crosses a managed boundary |
| Cause | Vanilla VM dereference | One wrong mesh name in `skins.xml` |
| Relation to sex/race | None | Entirely |

The data sweep that preceded the real fix is worth not repeating: all 5,166 `<NPCCharacter>` entries
in the load order carry `culture=` except two multiplayer-only rows; `as_dwarf_*` action sets are at
90/90 parity with `as_human_*`; all 13 TAOM female dwarf lords carry a `faction=`. Every one of those
was a plausible cause and none was the cause.

**"No crash bundle" is itself evidence**, and it points away from managed code rather than meaning
"nothing to go on" — the same signal recorded in
`investigation-rhun-dwarf-ctd-2026-08-02.md` Established #3.

## Preventive actions taken

1. `tools/validate_mesh_refs.py` extended to cover `skins.xml` body meshes — the file was **always**
   inside its scan root; only the eight attribute names were missing. Red on the pre-fix backup,
   green after. 3 regression tests, one pinning the prefix-vs-exact-token distinction.
2. `tools/oneoff/fix_dwarf_female_underwear_mesh.py` — re-runnable, idempotent, verified on a copy of
   the pre-fix backup (dry-run writes nothing; apply changes exactly +2 bytes; re-run no-ops).
3. Call-frequency and both accepted side effects recorded in the patch registry.
4. The `ClanBanner` coincidence and the uncovered `EndCurrentMatch` site documented as residuals in
   the source, not left implicit.

## Lessons to append to `docs/reviews/lessons/`

- **Build/Tooling/Workflow** — "Before writing a durable log in a Harmony postfix, enumerate the
  target's call sites." (finding 1)
- **Build/Tooling/Workflow** — "Read-binary/write-binary applies to any tracked file whose line
  endings you did not author, not just ModuleData XML." (finding 5)
- **Misc / triage** — "A reporter's attached artifact may not be the crash they are reporting; a
  missing crash bundle is evidence of a native fault, not absence of evidence." (the triage lesson)
- **Testing & QA** — "A review agent that measures a violation and then grants its own exemption is
  worse than a miss — it launders the finding. Re-verify any 'technically over but acceptable'
  verdict against the rule text." (findings 6/7)
- **Harmony & IL** — "A containment finalizer must swallow only the exception class it exists for.
  Enumerate what else the target can throw — `Last(predicate)` throws InvalidOperationException, not
  NRE — and rethrow the rest so it still reaches the crash reporter." (finding 8)

## Verification after the review fixes

- `dotnet build` — 0 errors; full C# suite **5,712 pass / 0 fail**.
- ADR-002: `Patch69_TournamentRosterGuard` 175 → **133** lines, `Patch69_TournamentEndGuard`
  161 → **86**. Both under the hard limit.
- `python tools/validate_mesh_refs.py --no-rgl-log` — 0 errors across the Armory ModuleData;
  red on the pre-fix backup, green after.
- `tools/oneoff/fix_dwarf_female_underwear_mesh.py` exercised on a copy of the pre-fix backup:
  dry-run writes nothing, apply changes exactly +2 bytes with line endings preserved, re-run no-ops,
  file still parses.
- **Still owed: in-game verification of both fixes.** Nothing here has been confirmed against a
  running engine.
