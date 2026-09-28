# RCA: Saruman the White, the Sauron and Saruman faces, and two tools built on false premises (2026-09-28)

**Scope:** the work committed as `6df36909` (Mike, 16:00, pushed; subject without a version label) and the
follow-up that fixes its review findings. Content: a new lord `lord_I1_0` Saruman the White (race `saruman`) who
leads `clan_isengard_1` and rules the `isengard` kingdom, with Uglúk (`lord_I1_1`) kept in the clan with his
title; Saruman's and Sauron's faces from Mike's in-game exports; `sauron` briefly offered by Mordor in character
creation. Code and tools: the console command `taom.print_face`, the hair and beard morph fitter
`tools/blender/fit_hair_morphs.py` with `hair_follow.py`, and `tools/oneoff/add_sauron_eye_colours.py`. Live
Armory: gold and red eye stops on the `sauron` race in `skins.xml`, and fitted hair and beard channels in
`AssetSources/Race Test/Saruman/saruman_delivery_2026.03.16.fbx` (Kit re-import 15:00). Reviewed by `/deep-review`
wave 1 (Standards, Engine compatibility, Data flow, XML), which reported after the commit had landed.

## Summary

Two tools were built on premises nobody had checked against vanilla. `taom.print_face` existed because "the face
editor has no copy or export action"; the editor copies the face on Ctrl+C (`BodyGeneratorView.TickInput`). The
hair fitter existed because Saruman's floating hair looked like the eye bug of 2026-09-27; vanilla's beards and hair
carry no morph channels at all, and the engine moves them with the head's channels through a per-vertex table
(0x56EBA0). Both were removed, on Mike's decisions ("Remove it"; "Vanilla should be the correct way to do it").
The one HIGH was a session convenience left in the data: `sauron` in Mordor's character-creation races, which hands
a player every system keyed on that race. It was removed once Sauron's face was captured.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | `cultures.json` offered the NPC-only `sauron` race to Mordor players: dread aura, signature strikes, combat modifiers and immortality with no heirs, no `_facegen` sets (human fallback), male body on the female skins | data, character creation | Added as an authoring aid, per Mike's request, and never reverted; the snapshot README's NPC-only rule was not read; no gate limits which races a culture offers | Removed. Lesson in `data-content-cultures.md`; the morphs doc's export recipe says to take a session race out before committing |
| 2 | MED | `taom.print_face`'s reason for existing was false: the face editor copies `CurrentBodyProperties.ToString()` on Ctrl+C | research | Only `FaceGenVM` was searched; the input handling lives in the View and its hotkey category | Command, tests and doc rows removed; the doc's export recipe leads with Ctrl+C. Lesson in `localization-ui.md` |
| 3 | MED | `fit_hair_morphs.py` fitted channels on hair and beard that vanilla does not have; nothing decompiled reads them | engine, assets | The fix copied the previous day's eye fix instead of the engine's own assets; vanilla's packages were opened only after the commit | Tool and tests removed; the doc records the vanilla rule (no channels, rest shape fitted to the exact head). Lesson in `animation-skeleton.md` |
| 4 | MED | The `sauron` eye stops lived only in the unversioned live Armory, with no gate and no snapshot | unversioned modules | The one-off exited 0 whether or not the stops existed | Snapshot refreshed, APPLIED EDIT entry added, `--check` exits 1 when a skin lacks them |
| 5 | LOW | The eye script could push a gradient past the engine's 32-stop array, which the parser does not clamp | engine limits | The array size was unknown when the script was written | The script refuses more than 32 stops; tested |
| 6 | LOW | The export doc named the wrong log folder and target file, and skipped cheat mode | docs | Written from the logger's intent, not from the disk | Section rewritten around Ctrl+C; moot with the command gone |
| 7 | LOW | `print_face` searched living heroes only; test names described states the formatter lacks | code | | Moot: the command is gone |
| 8 | LOW | The eye script's docstring stated the gradient "flat band" as fact | evidence | The sampler is not decompiled | Reworded as intent |

Process, not a code defect: `6df36909` went out without the `v2.0.32` subject label and swept in another session's
`.claude/skills/release/SKILL.md`, and it landed while the review was running, so the review's findings arrive as a
second commit.

## Root-cause pattern

Findings 2 and 3 share one cause: a claim about what vanilla does or lacks was made from the nearest code or the
last bug, not from vanilla itself. In each case the refuting evidence took one read (the View's `TickInput`; the
vanilla beard metameshes in `pack3.tpac`). Finding 1 is the other recurring shape: a reversible convenience made
for testing ships because nothing forces the revert.

A half-step in the middle shows how easily the wrong reference wins. After the review, the LOTRLOME dwarf beards
were measured: they carry 101 channels that copy the dwarf head's motion (channel 46: head 5.58 mm, beard 5.61 mm),
exactly what the removed tool wrote. That reopened the tool for a moment; Mike settled it on vanilla, because the
dwarf channels were an experiment and vanilla works without them. Whether the engine reads such channels at all is
still open.

## Why each lens caught or missed what it did

- **Standards (1):** its checklist passed; it found finding 2 by reading the vanilla View beyond its checklist.
- **Engine compatibility (2):** found findings 2 and 3 and the 32-stop limit (5) from the decompile.
- **Data flow (5):** found finding 1 by tracing the race name through every race-keyed config, and finding 4.
- **XML (7):** found finding 1 independently from the snapshot README and the missing facegen sets, and 4.
- Wave 2 (Efficiency, Completeness, Design, Tooling) had not launched when the commit landed; its scope is the
  follow-up diff.

## Open, for Mike

- Saruman's war kit is the vanilla Scholar Robe with no leg item, as specified; the ruler title stays "Warlord"; he
  is capturable and not lore-locked in the Player Switcher; Uglúk is no longer offered by the Player Switcher,
  which lists rulers' houses and clan leaders.
- Saruman's hair and beard: to follow vanilla, strip their channels and check the rest fit on his head, then a Kit
  re-import and an in-game look.
- Where the engine's upper-mesh index table comes from (rest proximity at load, or the Kit).
