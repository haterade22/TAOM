# RCA: Uncapturable Heroes deep review, 2026-08-26

Feature: Sauron and the nine Nazgûl can never be taken prisoner; they escape as fugitives.
Two Harmony patches under `Patch76_UncapturableHeroes`, one registry, one config, one service, one
adapter, one MCM toggle. Suite green at 7587 before review, 7589 after (two binding tests added).

Six review agents ran: standards, API compatibility, efficiency, completeness, cross-system data
flow, plus a focused adversarial audit of the two patches. Standards, efficiency and completeness
returned clean. Every finding below was re-verified against the decompiled v1.4.8 source or the repo
before being accepted, per `.claude/rules/evidence-over-claims.md` A.4; two agent claims did not
survive that check and are recorded as disputed.

## Findings

| # | Sev | Finding | Category | Why missed | Action |
|---|---|---|---|---|---|
| 1 | HIGH (blast radius) | A `Missing*`/`TypeLoad` raised while JITting the postfix fires **before** its own try/catch is entered, so the patch cannot self-guard. It reaches `PatchShield`'s finalizer, which returns `default(bool)` = `false` = **every hero in the game uncapturable**. `PatchShieldPolicy.CompiledProtectedOwnerPrefixes` contains `"TAOM"`, so the shield refuses to unpatch and the state persists for the whole session. `KillCharacterActionDetail.None` was the one engine reference in the postfix body with no binding assertion | Harmony & IL | The patch's own comment reasoned about exceptions thrown *inside* the body and concluded the try/catch made it fail-open. It does, for that class. It cannot cover a JIT-time member-resolution failure in its own IL, which is the case that actually matters after an engine bump | **Fixed:** `KillCharacterActionDetail_StillDeclaresNone` binding test |
| 2 | MEDIUM | The binding test resolved `TakePrisonerAction.Apply` by parameter **type**, not **name**. Harmony binds prefix parameters by name, so a TaleWorlds rename (legal, silent, not part of any API contract) would feed the prefix nulls, the null guard would swallow every call, and the seam would become a no-op that both the type-resolving test and every behavioural test still pass | Harmony & IL | The binding test was written to answer "does the method still exist", which is the question every other TAOM binding test asks. Name-binding is a Harmony-specific coupling that the existing test vocabulary had no idiom for | **Fixed:** `ParameterInfo.Name` assertion |
| 3 | MEDIUM | Neither patch declared a Harmony priority. Another mod's postfix on `CanBecomePrisoner` running after ours could re-grant capture and silently defeat the feature | Harmony & IL | Cross-mod ordering was never considered; the design reasoned only about vanilla callers | **Fixed:** `[HarmonyPriority(Priority.Last)]` on the postfix, so TAOM's denial is last. The reverse stays open by design, since the guard never flips `false` to `true` |
| 4 | MEDIUM | `LordConversationsCampaignBehavior.cs:3072-3076` and `:3145-3149` ("You are my prisoner now.") are not `IsPrisoner`-gated and are reachable, so the player picks the line and the hero escapes anyway | Campaign mechanics | The patch's justification comment named `PrisonerCaptureCampaignBehavior` as "the one that matters in practice" and never examined the conversation behaviour, despite this prefix being exactly the seam that intercepts it | **Documented, not gated.** Both sites pass `MainParty` as captor, so the escape message fires immediately and resolves it. Gating would mean patching vanilla conversation conditions for a line that resolves itself |

## Disputed, and why

**LOW, cross-system agent: "hook-level guard clauses have no direct unit tests, violating
`.claude/rules/tests.md` Skip-Guard Exhaustion."** Read the rule: it is scoped to *service methods*
("When a **service method** has `if (condition) continue/return` guard clauses"), and
`csharp-architecture.md`'s coverage table lists Harmony entry points as "not required, test via
game". The agent conflated TAOM's `IOnXxx` hook layer (80%+ required) with the `Hooks/` folder where
Harmony patches live. The service layer these guards delegate to is fully guard-tested. No action.

**Adversarial agent bonus finding: "`LordWantsRivalCapturedIssueBehavior` could pick a protected
hero and become uncompletable."** That behaviour is already in TAOM's suppression list
(`Main/Features/LotrIssues/LotrIssueSuppression.cs:65`), so the quest never spawns. Moot; the agent
did not know TAOM suppresses it. No action.

## Root-cause pattern

Findings 1, 2 and 3 are one pattern: **the feature reasoned rigorously about vanilla's behaviour and
not at all about the layers wrapped around it.** The design work verified the engine's control flow
exhaustively (the `MapEvent` fall-through was read line by line and pinned with an IL test), but
treated the patch itself as if it executed in isolation. Three of its actual neighbours were never
considered: the JIT, PatchShield, and other mods' patches on the same methods. Each of the three
findings is a different neighbour.

The tell is that all three are invisible to behavioural testing. A green suite proves the feature
works when nothing around it has moved. None of these fires until an engine bump or a second mod,
which is precisely when nobody is looking.

## Why each agent missed what it missed

- **Standards** checks ADR conformance and patch-category registration. Priority declarations and
  JIT semantics are not in its rule set. Correctly returned clean.
- **API compatibility** verified every signature against the installed DLL and found the parameter
  names correct *today*. It deserves credit for going further than asked and flagging that the
  binding test could not see a future rename (finding 2). That is the review catching a gap in the
  review, which is the behaviour to reinforce.
- **Efficiency** decompiled `Hero.AllAliveHeroes` before costing the linear scan rather than
  reflex-flagging LINQ. Correct call, correctly evidenced.
- **Completeness** checks artefacts exist, not whether they are sufficient. A binding test file
  existing satisfies it; whether that file covers every engine reference is out of scope.
- **Cross-system data flow** traced ten flows and confirmed the central premise from source. Its one
  finding over-applied a service-scoped rule to entry points. Its real value here was negative
  evidence: it closed off double-application, fail-open, and toggle-coverage as concerns.
- **The focused adversarial agent found all three of findings 1, 3 and 4**, and was the only agent
  that opened `PatchShield` and the conversation behaviour. It was also the only agent instructed to
  assume the code was wrong and prove it.

The lesson for future reviews is the last bullet. The five standing agents each check a *category*;
the adversarial agent checks a *specific artefact* with a mandate to break it. On a changeset whose
risk is concentrated in two files, the second shape found everything the first shape did not.

## Lessons appended

`docs/reviews/lessons/harmony-il.md`: three entries (JIT-time failure outruns a patch's own
try/catch; Harmony name-binding needs a name assertion; a patch that must not be overridden declares
its priority).

## Codex round (independent adversarial pass)

Dispatched after the six-agent pass and its fixes, with the four already-known findings listed in
the prompt so Codex would not re-report them. Verdict: **1 P1, 4 P2, 3 P3.** Every engine claim was
re-verified against the v1.4.8 decompile before acting.

| # | Sev | Finding | Action |
|---|---|---|---|
| 5 | **P1** | **The two seams contradict each other on a death-marked hero.** The postfix deliberately defers when `DeathMark != None`, so vanilla answers `true`, `MapEvent.cs:1993` calls `TakePrisonerAction.Apply`, and the prefix, which had NO death-mark guard, vetoed the capture the postfix had just decided not to veto. Reachable: a kill applied while the hero is in a map event only stages a mark (`KillCharacterAction.cs:46-49`) and `MapEvent.cs:1977` admits every mark but `DiedInBattle`/`DiedInLabor` | **Fixed:** matching `DeathMark != None` guard on the prefix |
| 6 | P2 | **A throw after the mutation captures a hero who already escaped.** On the direct-capture path the hero is `Fugitive` before the announcement runs; the config read sat OUTSIDE the announce try/catch, and a faulted `Lazy<T>` rethrows forever, so the exception would unwind into the prefix's catch, which returns `true`, and vanilla would capture him anyway | **Fixed:** whole announce body guarded, config read included |
| 7 | P2 | The binding mitigation for finding 1 checked that `Hero.DeathMark` exists and that an enum named `KillCharacterActionDetail` has a member named `None`, but not that the property's TYPE is that enum, nor that `None` is still `0`. C# folds the enum comparison at compile time, so a renumbering would leave both guards comparing against a stale literal | **Fixed:** `PropertyType` identity and `None == 0` assertions |
| 8 | P2 | The premise test asserted only that two calls exist somewhere in the method, so a refactor that kept both while removing the hero from the roster on the gate's false branch would still pass. The test's own claim was stronger than what it checked | **Partly fixed, rest documented:** added the third call plus IL ordering; the control-flow limitation is now stated in the test and the feature doc instead of being implied away |
| 9 | P2 | No test executes either patch body or the real adapter, so specific mutations survive the entire suite: inverting `return !prevented`, or deleting `MakeHeroFugitiveAction.Apply` from the adapter while it still returns `true`. The category test also only asserted the two hooks AGREE, not that they are correct | **Partly fixed:** IL assertions that the adapter calls the engine action and each hook calls its service method, plus the literal category string pinned per hook. The `return !prevented` inversion remains uncovered and is an in-game smoke item |
| 10 | P3 | The battle message said "before your men can bind him" while its relevance test is only "was this the player's battle", which is true when the player LOSES and a protected ally escapes | **Fixed:** captor-neutral wording. Cheap now because the translations had not been run |
| 11 | P3 | **The doc asserted an engine fact that is false.** It said vanilla has no generic AI-prisoner escape. `PrisonerReleaseCampaignBehavior` listens on `DailyTickHeroEvent`, starts from a 4% daily chance and calls `EndCaptivityAction.ApplyByEscape` at three sites | **Fixed** in the feature doc and GitHub #513 |

### What the Codex round says about the first round

Finding 5 is the sharper lesson. The six-agent pass had already found the death-mark hazard and the
postfix guard was written FOR it, with a comment explaining exactly why deferring was correct. What
nobody did was ask the same question of the other seam. A guard added to one of two mirrored code
paths is a half-fix, and it reads as a whole one because the reasoning next to it is sound.

Findings 7, 8 and 9 are one theme: **three of the first round's fixes were weaker than their own
stated claims.** The binding assertion checked a name and was described as closing a JIT hazard. The
premise test checked call presence and was described as pinning a control-flow premise. The suite
was described as covering the feature while specific one-character mutations survived it. In each
case the artefact was real and the confidence attached to it was not earned. Writing a test is not
the same as writing a test that can fail for the reason you care about.

Finding 11 is the one to be least comfortable about. It is a fabricated engine fact: an assertion
about vanilla behaviour written into shipped documentation, a published GitHub issue and an RCA
without ever being checked, in a session that verified far harder claims properly.
`.claude/rules/evidence-over-claims.md` C names this exact failure. The tell was that it sounded
like a fact rather than a finding, so it never got queued for verification.

## The finding that matters most: the lessons record already had two of these

Asked afterwards whether the RCA existed "to ensure our lessons learned are used", the honest
answer turned out to be no. The RCA was written. The lessons were appended. But
`docs/reviews/lessons/harmony-il.md` was never READ before the patches were written, even though
CLAUDE.md says to read the category file before touching a subsystem. It was opened once, at the
end, to append to it.

Two entries that were already in that file describe the two most serious findings of this review.

**Codex P2, finding 6, was lesson "Fall through to vanilla on error is only safe when vanilla is a
safe default at THAT call site"** (landed `b3259e1d`, 2026-08-20, six days before this work, from
#486). Its worked example is a guard prefix whose catch returned `true` on a hero that vanilla had
already de-clanned, de-partied **and made a fugitive**. That is this bug, with the same actor in the
same state. Its Prevent line is: *"before writing a defer-on-error catch, state in one sentence what
vanilla actually DOES at this call site."* That sentence was never written here.

**Codex P1, finding 5, is covered by lesson "When a Prefix returns false, decompile the FULL call
chain and replicate every safety gate"**. The fit is less exact, since the gate we dropped lives in
the caller rather than in a helper the target delegates to, but the discipline it prescribes, trace
the whole chain and replicate every gate before returning `false`, is what would have surfaced the
missing `DeathMark` guard.

So the earlier "why missed" analysis for those two findings is incomplete and is corrected here. It
is not that the questions were hard, or that the review agents lacked a rule. **The rules existed,
in the file the project tells you to read, and the read never happened.** A review pipeline that
only appends is a write-only log; the value is in the read, and nothing enforced it.

### Preventive action

CLAUDE.md already carries the instruction and it did not fire, so restating it would change nothing.
The gap is that the instruction lives in a general always-loaded document while the moment it
matters is specific: opening a file under `Main/**/Hooks/**`. TAOM already has a mechanism for
exactly that, the path-scoped rule `.claude/rules/harmony-patches.md`, which auto-loads on that
glob and previously opened with "Before editing ANY patch: read its registry entry" while never
mentioning the lessons file at all.

That rule now leads with the lessons read. The registry tells you what a patch does; the lessons
tell you how patches in this codebase have gone wrong before, and the second one is what this
session needed and skipped.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
