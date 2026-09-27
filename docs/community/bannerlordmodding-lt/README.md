# Community contribution: custom creatures guide (LIVE; 2026-09-26 update NOT SENT)

A guide on adding custom creatures, mounts and humanoid races to Bannerlord, contributed to
[docs.bannerlordmodding.lt](https://docs.bannerlordmodding.lt). Six pages are live. A v1.5.x update,
four new pages and a refresh of the six, is prepared here and has not been sent.

**Status: six pages LIVE since 2026-09-01.** Litauen, the site maintainer, published all six pages
verbatim and gave them their own top-level nav section, **Custom Creatures**, rather than leaving them
loose in the Guides list. The files stayed at their authored `guides/` paths, so every cross-link we
wrote resolves.

**The 2026-09-26 update is NOT SENT.** What it holds and how to send it:
[2026-09-26 update](#2026-09-26-update).

This directory is the authoring source, and until the update is published it is ahead of the site:
`guides/` holds the six live pages as refreshed on 2026-09-26 (that day's live text with the update
pack applied) and four pages the site does not have yet. It matched the published pages heading for
heading on 2026-09-01; it does not now. To see what is published, fetch the live URL.

## The pages

| File here | URL | Nav label | Status |
|---|---|---|---|
| `guides/custom_creatures.md` | [/guides/custom_creatures/](https://docs.bannerlordmodding.lt/guides/custom_creatures/) | Custom Creatures | live; refreshed here, not yet sent |
| `guides/custom_creature_skeleton.md` | [/guides/custom_creature_skeleton/](https://docs.bannerlordmodding.lt/guides/custom_creature_skeleton/) | Skeleton | live; refreshed here, not yet sent |
| `guides/custom_creature_animation.md` | [/guides/custom_creature_animation/](https://docs.bannerlordmodding.lt/guides/custom_creature_animation/) | Animation Clips | live; refreshed here, not yet sent |
| `guides/custom_creature_xml.md` | [/guides/custom_creature_xml/](https://docs.bannerlordmodding.lt/guides/custom_creature_xml/) | XML | live; refreshed here, not yet sent |
| `guides/custom_creature_troubleshooting.md` | [/guides/custom_creature_troubleshooting/](https://docs.bannerlordmodding.lt/guides/custom_creature_troubleshooting/) | Troubleshooting | live; refreshed here, not yet sent |
| `guides/custom_creature_reference.md` | [/guides/custom_creature_reference/](https://docs.bannerlordmodding.lt/guides/custom_creature_reference/) | Reference Tables | live; refreshed here, not yet sent |
| `guides/custom_creature_race.md` | `/guides/custom_creature_race/` (planned) | Humanoid Race (planned) | new, not yet sent |
| `guides/custom_creature_clip_inspector.md` | `/guides/custom_creature_clip_inspector/` (planned) | Clip Inspector (planned) | new, not yet sent |
| `guides/custom_creature_melee.md` | `/guides/custom_creature_melee/` (planned) | Melee Attacks (planned) | new, not yet sent |
| `guides/custom_creature_battle.md` | `/guides/custom_creature_battle/` (planned) | In Battle (planned) | new, not yet sent |

[`custom_creature_updates.md`](custom_creature_updates.md), beside this README, is not a page: it is
the update pack, the change list for the six live pages.

## What each page covers

| Page | Covers |
|---|---|
| Custom Creatures | Hub. What a creature is to the engine, the reskin vs bespoke decision, prerequisites, where files live. Refreshed: two reading orders (a mount, a race), a terms box, the example creatures |
| Skeleton | The rig: authoring against the engine skeleton, bone limits, export, textures, materials, physics. Refreshed: hit capsules |
| Animation Clips | Clips: in-place authoring, `quad_movement`, gait theory, Kit compile, riders, diagnostics. Refreshed: the export mapping |
| XML | `monsters.xml`, action sets, usage sets, registration, the item, the reskin trap. Refreshed: a reskin's thin action set |
| Troubleshooting | Symptom to cause, with real crash signatures. Refreshed: debugging a native crash, what to search for in the log |
| Reference Tables | `AnimFlags`, action types, the `.tpac` format, skeleton fingerprints. Refreshed: vanilla clip recipes, the item checksum |
| Humanoid Race | Three ways to build a race; what a race is to the engine, with the cheapest race step by step; a skeleton with human names, order and axes; the human's physics copied through the rest pose; face and hand morph channels; clips (re-framing fixes axes, not poses); a standalone or inherited action set |
| Clip Inspector | What each field of the Kit's animation clip inspector does in game: a clip as a record, not keyframes; the fields one by one; priority, and why a clip plays in the viewer and not in battle; clip usages; flags at runtime; what Save writes, and the RuntimeDataCache; clip names |
| Melee Attacks | Two kinds of attack; the melee attack table and the Blends with animation box that keys it; the first-swing crash; three ways to make a swing safe, and the procedures; combat parameters (the hit window lives outside the clip); what is still open |
| In Battle | Size; three collision layers (hit capsules and the body capsule); weapons for a large race; scripted creature attacks; capping area attacks; formation spacing; banners and other jobs meant for humans |

## 2026-09-26 update

**Status: prepared, NOT SENT.**

**What was prepared,** for Bannerlord v1.5.x (every page's version box reads "Measured against
Bannerlord v1.5.x (last checked on v1.5.3)"; the three pages first written on v1.4.8 say so):

* four new pages: Humanoid Race, Clip Inspector, Melee Attacks and In Battle;
* the six live pages, refreshed: the live text fetched from the site on 2026-09-26 with every section of
  the pack applied;
* [`custom_creature_updates.md`](custom_creature_updates.md), the pack: the change list behind the six
  refreshed pages, one section per change (target, action, reason, paste text), so a page that changes
  on the site before this lands can take its sections by hand.
* Artem credited for the frame 0 rule (the Kit stores a clip's root position track relative to
  frame 0), in the export mapping and in the hub's acknowledgements (Mike, 2026-09-26).

**The unsent 2026-09-18 edits are folded in.** TAOM's copies of the six pages had carried corrections
drafted on 2026-09-18 that never reached the site: the war ram's thin set, the neck parented under the
tail, the resolved export mapping, hit capsules, a new clip's priority, the half-open being-struck
range, and new troubleshooting rows. Each went into the pack where still true, without its dates and
repo paths; the thin set's `_town_and_village` child became optional, not required. The refreshed
pages replaced those copies.

**How it was checked.** Three editing passes, each followed by an independent checker; a newcomer
re-read, by a checker reading as someone who has made one XML troop and wants a first creature; every
fact traced to a TAOM doc, Native's data or the v1.5.3 decompile; every link and anchor resolved,
counting the anchors the update creates.

**How to send it.** As [How to update a published page](#how-to-update-a-published-page) says, on
Discord to Litauen: all eleven files (the ten pages and the pack) together. The new pages link to
anchors only the refreshed pages have, and the refreshed pages correct live claims the new pages
contradict, so either half alone leaves dead links and two answers to one question; the pack's "Notes
for the maintainer" lists each case and the four nav entries needed. Once it is live, fetch each
URL, check it against `guides/` heading for heading, and update the status here, in the title and in
[docs/INDEX.md](../../INDEX.md).

## How to update a published page

**The GitHub repo is a daily mirror, not the live source.** `Litauen/docs.bannerlordmodding.lt`
receives an automated "Daily push" at 00:00 UTC, so it lags the site: on 2026-09-01 the live pages
were up while the repo's newest commit was still 2026-08-27, and the six files were not in the tree
at all.

So do not diff against GitHub to check what is published. Fetch the live URL. To land a correction,
edit the file here and send it to Litauen (Discord, linked from the site's front page) rather than
opening a PR against a mirror that has not caught up.

Site facts worth keeping:

* Markdown is the site's native format. Litauen confirmed it: "md is the exact format my site uses."
  Do not convert to HTML.
* Repo root is the docs root. Section directories hold flat `snake_case.md` files.
* Theme is MkDocs Material. Admonitions (`!!! note`, `??? abstract`) and `attr_list` are in use.
* There is no `mkdocs.yml` in the mirrored repo, so nav is configured somewhere else. That is why
  the nav section was Litauen's to create and not something we could ship.

## Editorial rules these pages follow

**Nothing is asserted that has not been measured.** Where TAOM's internal notes carried a claim that
was later disproved, the guide states the correction rather than quietly dropping it, because a
confidently-stated wrong number costs the next reader a day. Eight such claims were checked for and
kept out, including the "~40 bones per mesh" figure this repo itself published for months.

**Open questions are labelled as open.** The live animation page says the export mapping for
authoring a new clip onto a *reskinned vanilla* rig is unresolved, rather than presenting a recipe that
did not fully work. TAOM has since measured it, and the refreshed `custom_creature_animation.md`
replaces that note with `## The export mapping`. The new pages keep their own open questions open: what
Loading Type 2 plays through a bound clip, which monsters run the human animation system, and the
melee page's "What is still open".

**Third-party creature authors are named.** Per [`.claude/rules/provenance.md`](../../../.claude/rules/provenance.md),
a source gets its published name and never a euphemism. Artem (ADOD_Beasts) and Byak0 (Alliance,
Alliance.Wargs) are credited on the hub and the reference page.

**Claims carry the strength of their evidence** (the rule the 2026-09-26 pages were written to). A
finding read from the engine code says so ("TAOM's reverse engineering of the v1.5.3 DLLs found"), an
inference is hedged ("TAOM's reading is"), and anything a reader might act on that nobody has seen in
game sits in a `!!! note "Not verified in game"` box or says so in its sentence. A native crash offset
appears only as a v1.5.3 signature, because offsets move with every engine update.

**No TAOM internals, and only credited authors by name.** No Harmony patch numbers, issue numbers,
session times or dated narratives, internal paths, TAOM class or config names, or MCM settings. TAOM's
tools appear only as optional helpers, linked on the public `bannerlord-1.5.x` branch. The only people
named are the credited authors: Artem, Byak0 and szszss (TpacTool, MIT).

**The absorption playbook is deliberately absent.** TAOM learned a lot from taking another mod's
creature into its own module: guid remapping, material rebinding, the loose-versus-cooked duplicate
registration crash. The general lessons that apply to your **own** assets are included. The
step-by-step for lifting someone else's creature is not, because publishing it helps nobody build
anything.

**No local paths.** Game files are referenced as `Modules/Native/...` rather than absolute install
paths.

## Verification performed before publication

These are the checks behind the six pages published on 2026-09-01. The 2026-09-26 update's checks are
in [its section](#2026-09-26-update).

* Both quoted `Monster` blocks diffed attribute by attribute against the live game files, and the
  elephant `Horse` item likewise.
* `dump_engine_skeleton.ps1` invocations checked against its actual param block, and its hardcoded
  machine defaults called out in the text since a reader would hit them.
* All 21 cross-links resolved against the wiki repo. This found `/troubleshooting/` is not a
  directory on that site: those pages live under `guides/`.
* Every in-page anchor checked against markdown slugify. This found two broken anchors where removed
  punctuation collapses whitespace; fixed by renaming the headings rather than guessing the
  slugifier.
* `python tools/lint_docs.py` clean, including the em-dash rule.

`tools/lint_docs.py` exempts `docs/community/` from its dead-link check, because these pages link
into another site's URL space and resolving those paths against this repo reports every one as dead.

## Standing invitation from the maintainer

Litauen's framing when this started, quoted so the next session does not have to re-derive the
scope:

> give link to the AI to my site and to your code, ask to extract the valuable knowledge for the
> community and prepare .md guides

That is an open door for further contributions, not just this one. TAOM has other knowledge in the
same shape and no public home: the co-op gating model, the Harmony patch-category registry as a
crash-triage method, the ModuleData validation matrix, and the landless-culture spawn crash. None of
it is committed to, and none of it should be started without asking first.
