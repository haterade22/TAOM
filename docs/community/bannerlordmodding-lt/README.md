# Community contribution: custom creatures guide (PUBLISHED)

A six-page guide on adding custom creatures and mounts to Bannerlord, contributed to
[docs.bannerlordmodding.lt](https://docs.bannerlordmodding.lt).

**Status: LIVE since 2026-09-01.** Litauen, the site maintainer, published all six pages verbatim
and gave them their own top-level nav section, **Custom Creatures**, rather than leaving them loose
in the Guides list. The files stayed at their authored `guides/` paths, so every cross-link we wrote
resolves.

This directory is the authoring source. It matches what is published, heading for heading, verified
2026-09-01.

## The live pages

| File here | Live URL | Nav label |
|---|---|---|
| `guides/custom_creatures.md` | [/guides/custom_creatures/](https://docs.bannerlordmodding.lt/guides/custom_creatures/) | Custom Creatures |
| `guides/custom_creature_skeleton.md` | [/guides/custom_creature_skeleton/](https://docs.bannerlordmodding.lt/guides/custom_creature_skeleton/) | Skeleton |
| `guides/custom_creature_animation.md` | [/guides/custom_creature_animation/](https://docs.bannerlordmodding.lt/guides/custom_creature_animation/) | Animation Clips |
| `guides/custom_creature_xml.md` | [/guides/custom_creature_xml/](https://docs.bannerlordmodding.lt/guides/custom_creature_xml/) | XML |
| `guides/custom_creature_troubleshooting.md` | [/guides/custom_creature_troubleshooting/](https://docs.bannerlordmodding.lt/guides/custom_creature_troubleshooting/) | Troubleshooting |
| `guides/custom_creature_reference.md` | [/guides/custom_creature_reference/](https://docs.bannerlordmodding.lt/guides/custom_creature_reference/) | Reference Tables |

## What each page covers

| Page | Covers |
|---|---|
| Custom Creatures | Hub. What a creature is to the engine, the reskin vs bespoke decision, prerequisites, where files live |
| Skeleton | The rig: authoring against the engine skeleton, bone limits, export, textures, materials, physics |
| Animation Clips | Clips: in-place authoring, `quad_movement`, gait theory, Kit compile, riders, diagnostics |
| XML | `monsters.xml`, action sets, usage sets, registration, the item, the reskin trap |
| Troubleshooting | Symptom to cause, with real crash signatures |
| Reference Tables | `AnimFlags`, action types, the `.tpac` format, skeleton fingerprints |

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

**Open questions are labelled as open.** The export mapping for authoring a new clip onto a
*reskinned vanilla* rig is still unresolved in TAOM's own work, and `custom_creature_animation.md`
says so rather than presenting a recipe that does not fully work.

**Third-party creature authors are named.** Per [`.claude/rules/provenance.md`](../../../.claude/rules/provenance.md),
a source gets its published name and never a euphemism. Artem (ADOD_Beasts) and Byak0 (Alliance,
Alliance.Wargs) are credited on the hub and the reference page.

**The absorption playbook is deliberately absent.** TAOM learned a lot from taking another mod's
creature into its own module: guid remapping, material rebinding, the loose-versus-cooked duplicate
registration crash. The general lessons that apply to your **own** assets are included. The
step-by-step for lifting someone else's creature is not, because publishing it helps nobody build
anything.

**No local paths.** Game files are referenced as `Modules/Native/...` rather than absolute install
paths.

## Verification performed before publication

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
