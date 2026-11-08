# Reviewing kepano's repositories (2026-11-08)

Procedure: `/adopt-external` ([external-repo-adoption.md](../ai-includes/external-repo-adoption.md)).
**Outcome: nothing taken.** No tool, skill, vault format or text came over. The one idea worth testing, a random
revisit of old lessons, was tried by hand on five entries and found nothing to act on, so it was not added.

## Source, and what was read

Mike asked for `kepano/kepano-obsidian` and for the rest of Steph Ango's public repositories, because some are related.
The account has 39 public repositories, of which 14 are forks.

| Depth | Repository |
|---|---|
| Tree listed, runnable surface read | `kepano-obsidian` at `473697347a0eaac790a6596229c741b3915f3b77` (MIT) |
| Tree listed, every skill scanned, three skills and both manifests read in full | `obsidian-skills` at `3ccff5338ea700537839b21900aa5358a0402c98` (MIT) |
| Manifest, release workflow and `AGENTS.md` read; `fetch.ts`, `cli.ts` and four extractors searched | `defuddle` at `3e8bde9f5f855bc968e95837a750c5968a9fe639` (MIT) |
| Tree listed only | `clipper-templates` (site clipping JSON) |
| Name, description and language only | The other 35, which are forks, Obsidian themes and plugins, Flexoki ports and personal projects |

The article [How I use Obsidian](https://stephango.com/vault) was read as verbatim text through `curl`. All repository
content came through the GitHub API. Nothing was cloned or installed.

## Security

Safe to learn from: yes, for all three repositories read. Safe to install: no need arose, and installing is not recommended.

- `kepano-obsidian` holds notes, templates, `.base` files and `.obsidian` JSON. It has no scripts.
- `obsidian-skills` holds Markdown and two plugin manifests, with no hooks and no scripts. A scan of 14 files found no
  hidden Unicode characters.
- The `defuddle` and `knap` skills tell the agent to run a global npm install. The `obsidian-cli` skill documents
  `obsidian eval`, which runs arbitrary JavaScript in the app, and points to CDP and debugger controls.
- `defuddle` has no install script that runs for a consumer (`prepublishOnly` runs only for the publisher), and it
  publishes with npm provenance. Its CLI fetches the URL it is given. Its YouTube extractor calls InnerTube, and its X
  extractor calls `publish.twitter.com` and the third-party `api.fxtwitter.com`.

**Not covered:** the other `defuddle` extractors were not read. `tools/audit_claude_config.py --external` did not run,
because it needs a local copy and nothing was to be ported. That check is UNCHECKED, and any later port of this skill
text has to run it first.

## Verdict by item

| Item | Verdict | Reason |
|---|---|---|
| `obsidian-markdown` skill | Skip | Wikilinks and callouts. [ADR-010](../adrs/010-knowledge-base-architecture.md) rejected the wikilink migration, and GitHub does not render them. |
| `obsidian-bases`, `json-canvas` skills | Skip | TAOM has no `.base` or `.canvas` files, and `graph_query.py` answers the link questions. |
| `obsidian-cli` skill | Skip | It needs the app running, the reason the pablo-mano skill was skipped. Obsidian is not at its default install path on the desktop. |
| `defuddle` skill and CLI | Skip | It needs a global npm install, and `docs/raw` holds four files, so clipping is rare. Python's `html.parser` over `curl` read this article verbatim. |
| `knap` skill | Skip | TAOM's Python generators already do batch output. |
| Vault layout (flat root, `categories` property, Bases) | Skip | TAOM routes knowledge by trigger ([ADR-011](../adrs/011-knowledge-delivery-tiers.md)), and its folders carry that routing. |
| Claim-style titles ("evergreen notes") | Already done | All 38 lesson headings sampled, the oldest and newest of several categories, state a rule. |
| Unresolved links as breadcrumbs | Skip | Memory files already do this. In `docs/`, a dead link is a lint error on purpose. |
| Date format, written style guide | Already done | Both exist. TAOM docs use no tags, so the plural-tag rule has nothing to apply to. |
| 7-point ratings, fractal journaling | Skip | Personal practice. `/harness-retro` is TAOM's periodic review. |
| Flexoki, Minimal, plugins, clipper templates, forks | Skip | Out of domain. |
| Random revisit | Tried, not added | See below. |

The `defuddle` skill says it saves tokens over WebFetch. In this harness WebFetch returns a small model's answer rather
than the page, so a full page in the main context is probably larger. Nobody measured this.

## The random revisit trial

The article describes opening random notes every few months to find missing links and apply newer style rules, and the
author keeps this manual on purpose. TAOM's lesson record is uncapped (1819 `### ` entries in 13 files on this date).
`/harness-retro` has no step that re-examines old lessons, `/improve` only cites them, and a search of `tools/*.py`
found no staleness check. So the review drew five entries with `shuf` and checked each one by hand.

| Entry | Named artifacts | Duplicate? | Result |
|---|---|---|---|
| `build-tooling-workflow.md:3447`, audit tool degraded branches | RCA and all three reason codes exist in `tools/` | Close to line 628 (empty scope must be loud), but the prevention differs | Holds |
| `build-tooling-workflow.md:4131`, tests after a merge | RCA, test and filter namespace exist | Same mechanism as line 3471, but that entry replays additions and this one runs the tests | Holds |
| `localization-ui.md:689`, no chat message before the initial screen | RCA, gate test, `StallReportNotifier` and `ShaderSourceInstallWiringTests` exist | None found | Holds |
| `data-content-cultures.md:257`, weapon identity from equipment | Tools, `build_item_class_registry` and the feature doc section exist; `rebalance_troops.py` exits when the game install is missing, and its name fallback serves library callers only | None found | Holds |
| `adapters-taleworlds-api.md:593`, corrected engine fields | Against v1.5.6: `MissionCombatMechanicsHelper.cs:200`, `Agent.cs:3496` and `SetAmountOfSlot` still match, and `CareerAmmoApplier` uses the right setter | None found | Holds |

Zero of five were stale or duplicated, so the agreed rule added nothing to `/harness-retro`. Five entries cannot rule out
a low rate of bad lessons, only a high one. The handoff from the corrected local to `CalculateDamage` in the last entry
was not traced again.

**One code leftover surfaced.** The third lesson says the green "TAOM loaded successfully!" line has never displayed. That
call is still in `OnSubModuleLoad` at `Main/SubModule.cs:635`. The lesson is correct and the line is dead code. It was
left alone because this review changes no C#.
