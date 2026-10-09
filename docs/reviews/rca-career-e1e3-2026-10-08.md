# RCA: career fixes #766, #767, #768 and the bugs their review found (#770, #771, #774), 2026-10-08

## Top-line

Three bug fixes open the career redesign (#769): career choices never linked to their group (#766),
the career special-resource gain reached only daily town income (#767), and the party-size limit did
not refresh after a career pick (#768). `/deep-review` ran in two waves. Wave one used the standards,
engine, completeness and data-flow lenses. Wave two used efficiency, design and a wiring lens the
maintainer asked for, because past career work shipped values the engine never read; one or two
adversarial verifiers then tried to refute every defect claim. Nothing was HIGH; four findings were
MEDIUM. The review also found two older bugs, fixed here as #770 (raids paid the battle credit; a
resisted raid paid `per_raid` twice) and #771 (screen handlers stacked once per save load), and one
filed for its owners as #774 (War of the Ring momentum counts a resisted raid twice).

The first #770 fix was itself wrong: four lenses of the fix review found that keying the battle credit
on the event type alone also removed it from real fights against lord parties inside raid events. Round
two keyed it on whether a field party was beaten. A final convergence pass found only documentation and
comment defects, all fixed. Full suite at the end: 15,139 passed, 6 skipped, 2 failed, both unrelated
(missing race-ability translation rows, and the game updating to v1.5.5 during the session).

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| #766 | bug | `ParseChoice` read a choice's group only from a `group_id` attribute no nested choice carries, so the click tier gate, `KeystoneExclusivityRule` and the foreign-choice repair were inert for all 2,100 group choices since they shipped | Parsed field never populated | Every rule test built `CareerChoiceDefinition` by hand with `groupId` set; nothing parsed the shipped XML and checked the link. Second occurrence: the 2026-09-02 review saw `group_id=""` on roots and fixed roots only | Shipped-data test that every grouped choice carries its group; click tests over parsed XML; lesson in `testing-qa.md` |
| W1-01 | MED | The click-path test #766 promised was not written | Test plan item skipped | The E1 brief said "if an existing test covers it, leave it", and the existing test drove the unbound `ExecuteSelectChoice` | `ExecuteToggleChoice` tests over a real provider and registry; the lesson names the bound command |
| W1-02 | MED | Group, root and choice ownership was read three ways (registry first-wins, integrity test last-wins, screen shows all); #766 let the repair pass delete on that data error | Destructive path switched on without its invariant | The fix enabled deletion for group choices without re-reading what the pass assumes about the data | Shipped-data single-owner gate; ambiguous ownership means no owner, so the choice is kept; refund message (WIRE-05) |
| W1-03 | MED, latent | Two upkeep computations with different NaN rules, where #767 promised one | Duplicate formula | A second helper was added beside the inline block | One `BuildUpkeepLines` for the breakdown and desertion |
| W1-04 | MED | The map-bar per-event rows printed raw config after credits were scaled | Display left behind by a formula change | Nobody searched for the surfaces that display a changed formula's inputs | Rows render through the public `ScaleEarned`; parity test |
| W1-05 | LOW | Leader-only `PartySize` also removes the bonus from garrisons, militia, villagers, patrols, supply caravans and refuges, not only caravans | Reach of a resolver change | The issue reasoned from caravans | Commit body and docs state the full reach; Rohan's "garrison" text reworded |
| W1-06 | LOW | Nothing pinned the leader-only key, and the docs still taught owner-first | Unpinned exception to a shared rule | | Source pin; `CareerPassiveHero` summary names the exception |
| WIRE-02 | LOW, pre-existing | The career flat party size was added before the AI-lord factor, which then multiplied it | Flat term before a factor | `ApplyFlat`'s comment promised it worked "whatever the call order" | Call moved after the last `AddFactor`; ordering test; contract comment fixed; lesson in `gamemodels-services.md` |
| W1-14 | LOW | Every career refresh invalidated every mobile party's caches | Over-broad cache bust | | `InvalidateLedBy` for the leaders whose passives changed |
| W1-09, WIRE-03 | LOW | A shared adapter was registered by one feature, and the special-resource service's optional career dependency would fail silently | Silent dependency | | Registration in `IoC.RegisterCoreServices`; container and source-pin tests |
| WIRE-06, CONV-768-01 | LOW | `taom.career_perks` printed the model value, then compared the cache with the display path | Diagnostic blind to the bug it should show | | Probe prints the tooltip value, the enforcement value and the engine cache |
| WIRE-04 | LOW | The keystone tier-3 exemption can be banked by refunding | Rule design | | Accepted and documented: old trees only; the redesign retires the rule |
| #770 | MED, pre-existing | Raids and extortion paid the battle credit, and a resisted raid paid `per_raid` twice | A map event with a winner is not always a battle | The battle handler never filtered the event; the raid handler trusted `RaidCompleted`, which also fires for a won militia fight | Battle credit unless no field party was beaten in a raid or extortion event; `per_raid` only on the engine's `Looted` state and only for the winner; lesson in `campaign-mechanics.md` |
| #770 round 1 | MED | The first #770 gate keyed on the event type alone and zeroed real lord fights inside raid events | Fix narrower than its own rule | The issue considered only lords who arrive later, which the engine splits into a separate battle | Gate keyed on the beaten side; every event type tested with both flag values |
| #771 | LOW, pre-existing | Static `ScreenManager.OnPushScreen` handlers stacked once per save load | Process-lifetime event, campaign-lifetime subscriber | The code comment and a standing lesson claimed the orphan was collectable | One static handler per process; the lesson in `state-lifecycle-save.md` rewritten |
| #774 | MED, pre-existing | War of the Ring momentum counts a resisted raid twice (same engine fact as #770) | Second consumer of one engine fact | Found while scoping #770 | Filed; not fixed on this branch because another session owns that feature |

## Root-cause patterns

1. **Tests that encode the author's belief about parsed data** (#766, W1-01). Second occurrence of
   "A test that stubs the collaborator it is pinning behaviour against pins nothing"
   (`lessons/state-lifecycle-save.md`). A rule that reads a parsed field needs one test over the
   shipped data that the field is populated, and the behaviour test must drive the command the UI binds.
2. **Switching on a dormant destructive path** (W1-02, WIRE-05). Re-check the invariant the pass relies
   on, gate it on shipped data, make ambiguity keep, and tell the player.
3. **A changed formula leaves its displays behind** (W1-04, WIRE-06, the console text). The wiring lens's
   reverse direction, every surface that displays the value, is what found them.
4. **One engine fact, several consumers** (#770, #774). When a review finds that an engine event means
   more than its name, grep every listener of that event.

## Why each lens caught or missed what it did

- **Standards, Engine:** caught the unpinned exception, the wrong cache comment and the full reach of the
  leader-only change from the installed DLL; neither looks for duplicate formulas or display surfaces.
- **Completeness, Data flow:** caught the missing click test, the ownership ambiguity and the tooltip;
  data flow also confirmed every path connects. Neither looked past the diff at raid events.
- **Wiring (new):** found #770 by tracing the raid event end to end, and the probe that could not show
  the #768 bug. The maintainer's request for this lens paid for itself.
- **Efficiency:** found #771 while checking subscription lifetimes.
- **Fix review:** four lenses independently found the round-1 #770 regression; the verifiers' engine
  traces settled which lord fights the engine folds into a raid event.

## Lessons appended

- `lessons/testing-qa.md`: a rule that reads a parsed field needs a shipped-data test, and a click test
  drives the bound command.
- `lessons/campaign-mechanics.md`: `RaidCompleted` and `OnMapEventEnded` fire for more than their names
  say; key payouts on the engine's own verdict and on who was beaten.
- `lessons/gamemodels-services.md`: a flat `ApplyFlat` term goes after the last `AddFactor`.
- `lessons/state-lifecycle-save.md`: the static-event entry rewritten (#771).

## Owed

In-game checks per issue (the commit bodies list them), and the hideout lead from the final pass: on
v1.5.5 the "send troops" hideout result may raise the hideout-completed event twice, which would pay
`per_hideout_clear` twice. Unverified; to be checked in game before the hideout doc line is trusted.
