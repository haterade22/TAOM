---
name: Battle / siege load hang (infinite loading screen)
about: A battle or siege that never finishes loading (works for some players, not others)
title: "[Load Hang] "
labels: bug, crash
---

<!--
  Thank you for reporting. This template collects the exact files our diagnostic
  tooling needs to tell, in minutes, whether the hang is an EQUIPMENT issue (a
  missing item/mesh on your install) or a CODE/SCENE issue. The more of the
  files below you attach, the faster we can fix it.
-->

## What happened

<!-- e.g. "Loading screen never finishes when I attack a Mordor army near Minas Morgul." -->

- Battle type (field battle / siege / hideout / arena / tournament):
- Who was fighting (your culture vs. enemy culture, if known):
- Does it happen **every time** on this battle, or only sometimes?

## The one question that matters most

**Can another player with the SAME enabled mod list load this exact battle?**
(This is the single biggest clue: if yes, the problem is almost certainly your
install's equipment/mesh data, not the mod's code.)

- [ ] Yes, another player loaded it fine
- [ ] No, it hangs for them too
- [ ] Don't know

## Files to attach (drag-and-drop into this issue)

The TAOM files live in the **`Logs\`** folder next to the game executable —
usually `<Bannerlord install>\bin\Win64_Shipping_Client\Logs\` (that's where TAOM
writes its diagnostic log + crash bundles). The **Open log folder** button on the
"last battle load may not have finished" startup popup opens it for you.

1. **`taom_debug_*.log`** — the newest one. **Required.** This phase-stamps
   the load; the last line tells us where it stalled. If TAOM showed you a
   "last battle load may not have finished" message on startup, the **Open log
   folder** button took you straight here.
2. **`taom_crash_*.zip`** — if one was written (the stall watchdog makes one
   after ~5 min). It already bundles the logs below.
3. **`rgl_log_errors_*.txt`** — the newest one, from your engine log folder:
   usually `C:\ProgramData\Mount and Blade II Bannerlord\logs\`, or
   `%USERPROFILE%\Documents\Mount and Blade II Bannerlord\logs\` on some installs
   (grab it from whichever exists). This is the engine's own log and is what
   *confirms* a missing mesh.
4. **Your save file** — if the hang reproduces from a specific save.

## Your setup

- TAOM version:
- LOTRLOME_Armory version (the armor module — equipment hangs are usually a
  mismatch here):
- Bannerlord version:
- Full enabled mod list (the launcher's "Copy to clipboard", or a screenshot):

<!--
  MAINTAINER NOTE (not for reporters): once the taom_debug log is attached, run
    python tools/triage_battle_load.py <taom_debug.log> --rgl-log <rgl_log_errors_*.txt>
  for an automatic EQUIPMENT-vs-CODE verdict + the suspect item/mesh. See
  docs/features/battle-load-diagnostics.md.
-->
