# RCA: campaign map frame profiler review findings (plan 039, 2026-10-02)

## Top line

Plan 039's Patch101 map profiler (`a3c219d4`) shipped no engine incompatibility: every target,
signature, field and call site held on the installed v1.5.3, and the listener walk matches vanilla
`MbEvent<T>` statement for statement. The six-lens review found three MEDIUM findings and a LOW tail.
Two MEDIUMs are in the text the maintainer decides FOR-MIKE 16a from: the PatchShield option A
trade-off followed an escaping exception to Patch37's crash capture and stopped, missing the
capture-off branch (PatchShield's own strip on the outer method) and the party AI task a skipped
`PostFrameTick` never starts. The third left `MbEvent<float>.Invoke`, which the walk replaces, unpinned.
The LOW tail is the profiler's fault path (a retry every frame, a dropped summary), two D6 reason-line
gaps and several doc claims. Every finding was fixed before commit, test first where testable.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| R1 | MED | The option A text named Patch37 as the catcher and stopped; with crash capture off Patch37 hands the exception back and PatchShield's finalizer on the outer method swallows it and strips that method's patches. The order was left UNVERIFIED | Exception path not followed to its last catcher | The executor followed the escape to the first finalizer it found and did not read what that finalizer returns on each branch; the order question was marked UNVERIFIED instead of read from Harmony's sorter | Verified order and both branches written; lesson in `harmony-il.md` (repeat of the 2026-10-02 rule) |
| R2 | MED | An escape from `MapScreen.OnFrameTick` skips `OnPostFrameTick`, which starts the party AI task | Skipped call's consequences not traced | The unwound callers were listed by name (`LateUpdate`, `PostFrameTick`) without reading what each starts | Doc, registry, policy comment; same lesson |
| R3 | MED | The walk replaces `Invoke` and `InvokeList`; only `InvokeList` was pinned | Drift guard incomplete | The pin was written for the method the walk copies line for line, not for every method whose call it removes | `Invoke` IL pinned; lesson in `testing-qa.md` |
| R4 | LOW | "No PatchShield finalizer inside the measured phases" was wrong | Unverified cost claim (repeat) | Harmony emits finalizers after postfixes; the claim was not checked against emission order or the frame's other shielded targets, the exact gap the 2026-10-02 harmony-il lesson names | Doc corrected; the lesson's prevention made concrete |
| R5 | LOW | A save was called a gap | Unread engine path | Inferred from "a save is an interruption" without reading `MapState.OnTick` | Corrected from `MapState.cs:144-150` |
| R6 | LOW | "Campaign time stops" overstated | Throw position ignored | One consequence written for every throw site | Per-position text |
| R7 | LOW | "Nothing else patches" true of TAOM only | Scope of a by-name rule | Written from TAOM's registry, while the exclusion applies to every owner | Qualified |
| R8 | LOW | A failed install warned only at a later game init | D6 reason line at the wrong time | The warning was written for the "later init" branch the plan listed | Warning right after the failed install line |
| R9 | LOW | The install fault named the wrong scope | Reused builder | One fault builder shared by session and install faults | `BuildInstallFault` |
| R10 | LOW | A fault before the session was stored retried every frame, silently | Fault latch keyed on state set after the throw | The latch (`_session`) and the log guard (`Session`) were set late in `OpenSession`; the guard was added to stop repeated lines, which hid the repeated throws | `Fault` records the campaign; guard deleted; lesson in `misc.md` |
| R11 | LOW | A fault dropped the closed frames' summary | D6 "never drop" | The plan's fault design predates D6 | `reason=fault` summary; same lesson |
| R12 | LOW | `EndAppTick`, the measuring phase path, the early return, the production patched check and two walker shapes untested | Test helper bypassed the production path | `CloseFrame` called the accumulator directly, and the installer took a fake for its only decision seam | Tests through the real helpers; lesson in `testing-qa.md` |
| R13 | LOW | `taomMs` called TAOM's own share | Metric meaning overstated | Named from what it adds, not from what it leaves out | "A floor" |
| R14 | LOW | `realTickMs` includes the AI-task wait | Unread engine path | `RealTick`'s first statement not read | Field row |
| R15 | LOW | Profiler overhead attribution incomplete | Measurement self-cost | The doc listed the profiler's work under `otherMs` without checking which bracket each part runs in | Field row |
| R16 | LOW | Cost counted three boundary reads, not five | Stale description | Written before deviation 4 and the two time-control reads were final | Cost section |
| R17 | LOW | Bypassed third-party patches and crash attribution unstated | Side effect of a call-site swap | The swap was reviewed for vanilla equivalence only | Two doc lines |
| R18 | LOW | Literal `"Invoke"` for a public member | Rename safety | Copied from a private-member lookup style | `nameof` |
| R19 | LOW | Reflection rows without line numbers | Catalogue convention | New rows written without the column's usual `:line` | Line numbers |

## Root-cause pattern

**Following an exception (R1, R2, R4, R6).** The trade-off text traced the exception from the excluded
method outward, but stopped at the first catcher's name, listed the unwound calls by name only, and
gave one consequence for every throw site. The 2026-10-02 harmony-il lesson from plan 028 already says
to follow an exception to its next catch and read each unwound caller; plan 039 was written by the same
run a day later and repeated it. The rule existed as prose; nothing made the executor produce the
evidence it asks for. The stronger prevention (in the lesson) is a required artifact: a per-target table
with the shield's start, the swallowed effect, the escaping effect per throw position, and the catcher's
finalizer order computed from priorities with every return branch of each finalizer.

**State set after the throw (R10, R11).** The fault path's latch and its log guard were set by code that
runs after the reads that can fail, so the fault path could not see the state it needed. A later guard
then muted the symptom instead of fixing the latch.

**Tests beside the production path (R3, R12).** A drift guard pinned the method the walk copies but not
the one it replaces; a test helper fed the accumulator directly instead of through the hook helper; the
installer's only decision seam was always faked.

## Why each agent missed these

The lenses reviewed the finished change; "missed" means a lens saw the code and did not report it.

- **Standards (L1):** caught R1, R7, R8, R9, R10 and R12. Missed R2 to R6: its lens checks the text's
  shape and the rules, not the engine consequences. Missed R11: it read D6's reason-line rule, not the
  summary rule, for the fault path.
- **Engine (L2):** caught R1 to R8 and R14, R17 to R19. Missed R10 to R12: fault-path state and test
  coverage are outside its lens.
- **Efficiency (L3):** caught R1, R10 and R15. Judged the hot paths clean, which they are; did not
  read the decision text beyond the finalizer order.
- **Completeness (L4):** caught R1, R3, R6, R7, R8, R10 and R12. Missed R2 and R5: it checked the
  trade-off's claims about time, not every skipped call.
- **Data flow (L5):** caught R1, R2, R5, R7, R9, R11, R13, R14 and R17. Missed R3 and R12: it traced
  values, not tests.
- **Design (L6):** caught R1, R2, R4, R5, R7, R10 and R16 and proposed P1 to P7. Missed R11 (judged the
  fault design as planned).

## Feedback memories to codify

None beyond the three lessons: `harmony-il.md` (follow an escaping exception through every return branch
of each finalizer at its next catch, as a per-target table), `misc.md` (a fault latch records its key
before anything that can throw and keeps what was measured), `testing-qa.md` (a copy of engine code pins
every method it replaces, and test helpers drive the production hook).

## Convergence rounds

| Round | Diff read | Finding | Fixed in | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | `a3c219d4..69ed4a36` | C1: the per-throw-position text said a throw after `TickMapTime` still moves parties, and left `SaveHandler.CampaignTick` reading as run | `5714149b` | R6's restatement was written from the method names, not the call order in the v1.5.3 `RealTick` and `MapState.OnTick` | Read the call order from the decompile before stating what a throw skips |
| 1 | same | C2: the docs promised a reason=fault summary after any fault, which cannot hold when the fault is the summary write (its retry runs the same code) | `5714149b` | The retry was judged by its happy path, with a substitute logger that throws where production never does | Ask what a retry re-runs before promising its result |
| 2 | `69ed4a36..5714149b` | R2-1: `5714149b`'s body said party movement is the last step of `RealTick`; `SiegeEventManager.Tick` follows it | the records commit's body (a correction line) | The commit body was written apart from the corrected doc text | Copy an engine fact into a commit body from the doc line it fixes |

This section and the REVIEW-LOG entry's convergence line were written by the orchestrator after round 2.

## Decision (2026-10-03)

FOR-MIKE 16a, the question the option A text above was written for, was answered with **option B** (decision D13):
`MapState.OnTick`, `Campaign.RealTick` and `MapScreen.OnFrameTick` stay under PatchShield, and only `Campaign.Tick` and
`CampaignEvents.Tick` are excluded. The findings above stand as the record of the first build's text; the shipped text
is now the option B text in `docs/features/map-perf-profiler.md`, which carries a per-target table because the lesson
above asks for one with any change that adds or removes a shield. See "Decision" in
`docs/reviews/deep-review-039-campaign-map-frame-profiler-2026-10-02.md`.

## Follow-up pass (2026-10-03)

A Claude review of plan 028's D13 commit and the Codex adversarial review of this branch found no engine
incompatibility. They found four things the first build and the D13 text left open, all fixed in one pass, test first
where testable (issue #721). The detail is under "Review follow-up pass" in the review report.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| F1 | P2 | The speed class read the raw time-control mode, so a waiting main party in a Stoppable mode (no campaign time) was classed `Play` or `FF` | Raw value read where the engine has a simplified accessor | The first review proposed `GetSimplifiedTimeControlMode` and held it as behaviour-changing, so the answer had to wait for the maintainer | A change a review holds for the maintainer goes on the decision list with its proposed default (done: FOR-MIKE 16r) |
| F2 | P2 | The installer checked the six hooks once; PatchShield's owner-wide strip could remove Patch101's pair later and the windows kept printing zeros | Install-time fact trusted for the whole process | The strip was analysed for the callers of the swallowed method, and a silent end of the measurement was accepted as a cost of option B without asking whether the profiler could notice | Lesson `harmony-il.md`: look again where each measurement starts, one aggregated warning, keep what was measured |
| F3 | MED | The D13 Mission text called the `TickAgentsAndTeamsImp` exclusion a fix for the freeze and said an escape reaches the native job thread | Exception path followed for one thread only | The text was written from the asynchronous tick; fast-forward runs the same method inline inside `Mission.OnTick`, whose shield D13 had just restored | An update under the repeat lesson in `harmony-il.md`: follow the throw from every path that can run the method |
| F4 | LOW | The 2026-09-26 and 2026-09-28 rules still told the next patch author to exclude per-frame targets, which D13 reversed | Rule left standing after the decision that narrows it | D13's update went under one lesson, not under every bullet that states the rule | Lesson `misc.md`: when a decision reverses an invariant, grep its old words across the lessons too |

## Convergence round of the follow-up (2026-10-03)

A convergence review of the follow-up commit `7ac9cab7`, and `claude-review-TRUE-039.json` (the Claude review of this
branch's `f86342ca`, which the follow-up pass had not answered), found six things. All were fixed in one pass, test first
where testable (issue #721). The detail is under "Convergence round of the follow-up pass" in the review report.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| G1 | MED | The `tickCompleted` hazard was put on PatchShield alone and sent to a PatchShield plan; Patch37's crash capture on `Module.OnApplicationTick` swallows any exception while capture is on (the default) and nothing between `Mission.OnTick` and it catches | Exception path followed to the nearest shield, not to every catcher | The first follow-up (F3) corrected the thread and stopped at the shield, though the 2026-10-02 lesson already asked for the next catcher's finalizers in order | Lesson `harmony-il.md`, an update under the repeat lesson: name every catcher up to the engine, the default-on crash capture included, before sizing a follow-up. The text now carries plan 028's paragraph from `31c0fd15` |
| G2 | LOW | Two comments still said what the pass had corrected elsewhere (the profiler finds a strip "at its next window or session start", untrue on `MapState.OnTick`; the agent-tick exclusion "is a fix" without the asynchronous qualifier) | One claim restated in several places | The fixes reached the docs and one policy comment, not every code or test comment that restates the claim | Lesson `misc.md` (extended in G4 to the lessons, and in the residual pass, row H2, to `Dependencies/` and test comments): grep a reversed claim's words across code comments and tests too |
| G3 | LOW | The session end wrote its summary without looking at the hooks: a strip on `MapState.OnTick` left the other five hooks and the views measuring with nothing in the output to say so, and a `Campaign.RealTick` or `MapScreen.OnFrameTick` strip after the last window start put zero-phase frames into the summary unflagged | Check placed at the starts only | The check's cadence was chosen from where a measurement starts; the place that writes the summary was not walked | Lesson `harmony-il.md`: the end of a run looks once more before its summary. Built (`MapSessionHooks.WarnIfHooksLost` at `EndSession` and at the `newCampaign` close); seven mutations killed |
| G4 | LOW | Row F4 above named a `misc.md` sentence that did not exist | Preventive action recorded in the RCA only | The pass wrote the preventive action into the RCA and did not touch `misc.md`; a patch author reads the lessons, not the RCA | `misc.md` lesson extended to the lessons, with this RCA's F4 as a source |
| G5 | LOW | The pass answered `claude-review.json` (plan 028's commit, in wt-028) and left `claude-review-TRUE-039.json` (this branch) unread, so three of its LOW findings stood: the "Gained" line paired finalizers with the wrong brackets, `Mission.OnPreTick` was called shared and previously shielded, and the `*_StayUnderPatchShield` walks ignored the namespace exclusions | Wrong review artifact | Two review files with similar names in one folder; the line naming the commit range and the worktree was not read first | A fix pass starts from the review file's own scope line (lesson `testing-qa.md`, "A fix pass starts from the review file's own scope line, not from the file with the nearest name", added in the residual pass, row H2). `PatchShield.IsExcludedTarget` skips on namespace OR method, and both walks now assert both |
| G6 | LOW | The adapter's one engine line behind the speed class had no test: every speed test fakes `ITimeControlAdapter` | Test fakes the line under test | The adapter was listed as not testable offline | IL pin `TimeControlAdapterBindingTests`; lesson `testing-qa.md` updated |

## Residual pass (2026-10-04)

A residual review of the two fix rounds (the branch at `c6940d65`) left four LOW findings. Each was re-read against the
code and the shipped Lib.Harmony 2.4.2 first. Three held and are fixed here, in docs and comments only (issue #721). The
fourth concerns the squash message, decision D13's follow-up record and FOR-MIKE 16m, which live outside the branch and
are redrafted by the orchestrator, so this pass leaves them. The detail is under "Residual pass" in the review report.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| H1 | LOW | G1's rewrite of the `tickCompleted` hazard named two catchers (PatchShield's finalizer on `Mission.OnTick` and Patch37's crash capture) in the feature doc, the policy comment, the registry and the lessons; there are three: Patch37 hands the exception back with capture off, on re-entry and with its service unresolved or throwing, and PatchShield's own finalizer on `Module.OnApplicationTick`, attached from the first game start's pass 2, then swallows a missing-API throw, so a first game freezes with capture off too | Catcher list built from the catchers found first, not from each catcher's hand-back branches | G1 followed the throw to Patch37's capture finalizer and stopped, as F3 had stopped at the shield; the 2026-10-02 lesson already asked for what each finalizer returns on every branch, and row R1 and the map doc already described this hand-back path for the map case | Lesson `harmony-il.md`, the two updates under the repeat lesson: three catchers, and follow each one's hand-back branches to the next finalizer on the same method. The follow-up's regression tests gain a crash-capture-off case |
| H2 | LOW | RCA rows G2 and G5 named lesson text that did not exist: the `misc.md` grep list had no tests and no `Dependencies/`, and no lesson carried G5 (G4's defect, again) | Preventive action recorded in the RCA only | G4 repaired the one row it noticed; the sibling rows of the same table were not grepped against the lessons | `misc.md` extended and its Source names row G2; `testing-qa.md` carries the G5 lesson; both rows point at them. The grep itself is the existing lesson `misc.md`, "A claim found wrong is wrong everywhere it was written" |
| H3 | LOW | The hook check runs on the profiler's own clock: its time and allocation land in the next window's first frame (the new session's, at a campaign change), and the docs said only "off the per-frame path" and named the line write alone | Cost placed on the wrong clock | The check's cadence was chosen from where a measurement starts; where its time lands was not asked, though plan 028 had placed its own check at a mission start for this reason | The `otherMs` row and the Cost section name the check and mark its cost UNVERIFIED; the `harmony-il.md` Prevent bullet says where a look's cost lands. Re-basing the open frame after the window work, for clean windows, is a behaviour change and stays the maintainer's call |
