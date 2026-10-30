# RCA: faction screen named cards take over the real hero (#1067), 2026-10-30

## Summary

The review of #1067 ran at tier 1: Codex at `ultra`, Agent 5 Data flow and Agent 2 Engine compatibility.
No reviewer found a CRITICAL, HIGH or MEDIUM defect. Codex refuted all five suspects from the installed
v1.5.6 DLLs and MCM 5.12.3. Agent 5 traced 10 flows with no gap. Agent 2 verified 17 engine facts with no
incompatibility. Seven LOW findings were confirmed. Six were text that still described the rule before the
change, and one is a pre-existing gap outside the diff. Two consequences went to Mike as design questions,
and he kept both.

The text findings repeat a lesson already on file: [misc.md](lessons/misc.md) "Sweep docs for the facts a
change alters, not for the feature's name" (#889, 2026-10-26).

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | `IHeroPickerService.cs:14-17` said `FindTakeover` uses "the same eligibility as the list, and a clan to take over". After the change it uses the hero's own culture and returns a clanless wanderer. | Stale contract text | The builder changed the implementation and its own comments, but not the interface's doc comment in another file. | Fixed. Covered by the #889 lesson: list each fact the change makes false, then grep for it. |
| 2 | LOW | `HeroPickerService.cs:128` said "Opt-in only." beside the lore-locked gate. | Stale comment | The comment is beside the gate, but the change was to the default in another file (`PlayerSwitchPolicy.cs`). | Fixed. Same lesson. |
| 3 | LOW | The new MCM hint said turning the setting off hides the dark lords "here and on the Themed Faction & Hero Picker". On the faction screen they are copied, not hidden: its cards are built without a policy check (`FactionScreenVM.cs:320-336`). | Wrong claim in new text | The hint was written from the Player Switcher panel's behaviour, and the faction screen was not checked. | Fixed: the hint now says the faction screen copies the look. |
| 4 | LOW | `faction-ui.md:88` and `player-switcher.md:504` still said "the faction's culture", and `player-switcher.md:213` said wanderers need the MCM knob. A card now ignores both. | Stale doc | The builder updated the paragraphs it added, but not the older sentences that state the old rule. | Fixed. Same lesson. |
| 5 | LOW | `uncapturable-heroes.md:5` said Sauron and the Nine "can never be taken prisoner". A player-controlled one can be, and #1067 makes that the default start for their cards. | Stale doc in another feature | That doc never names Player Switcher or the setting, so a search for the feature's name cannot find it. This is the exact case of the #889 lesson. | Fixed: one paragraph in the overview. Recurrence added to the lesson. |
| 6 | LOW | After a cross-culture takeover, a career switch is one-way: `CareerRegistry.IsEligible` checks the hero's own culture, so Haldir cannot pick a Lothlorien career again after he leaves one. Codex and Agent 5 found it independently. | Undocumented consequence | The plan asked whether the career breaks. It does not, so the one-way switch was not written down. | Documented in `faction-ui.md`. No code change: nothing breaks. |
| 7 | LOW | `HeroSwitchService.cs:111-117`: the adoption writes the occupation and the clan leader before `committed = true`. A throw in between would read as "failed before any mutation". | Pre-existing, outside the diff | Not this change's code. No caller that throws was found. | No change. `player-switcher.md` "Owed" already records the same class ("A `Failed` outcome can hide a completed swap"). |

Not applied: Agent 5's note that the join offer reads the clan's culture while the gold reads the hero's
culture after an adoption. Both shipped adoption cards (Gimli, Aragorn) are on their own culture's screen,
so no caller reaches it today.

## Design consequences decided by Mike (2026-10-30)

- **The Nazgûl card demotes the Witch-king.** `lord_1_155` is a member of the Witch-king's clan, and the
  #550 rule makes a taken-over member the clan leader. Kept, and documented in `faction-ui.md`.
- **A player Sauron has the NPC tuning** (#321: crush, cleave, knockdown resistance 3.0). Kept, and
  documented in `player-switcher.md` "Lore-locked heroes".

## Why each agent missed these

- **The builder** (implementer) updated the files the plan named and grepped for the old setting name. It
  did not list the facts the change makes false (opt-in, the screen's culture, a clan required, never
  captured) and grep for those. Its brief did not carry the #889 lesson.
- **Agent 5** found findings 1 to 6. **Agent 2** found 2 and 4 again, and 7. **Codex** found 6. None was
  missed by the review; they were missed by the build.

## Feedback to codify

Nothing new. The rule exists as a lesson since #889, and this is its first recurrence. A lesson in
`docs/reviews/lessons/` loads only when someone reads it, so a builder brief does not get it.
FOLLOW-UP no issue: a harness question, not a code defect; raise it at the HARNESS RETRO DUE run, which
is the place to decide whether the fact sweep becomes a line in the builder brief or a completion step.
