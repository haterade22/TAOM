# RCA: provenance register + third-party notices, 2026-08-13

Five review agents over an uncommitted changeset that added a provenance register, a path-scoped
rule, a shipped licence notice, an IDE-state build guard, and packaging plus tooling changes. Fifteen
confirmed findings, four of them in files that ship. All fixed before commit.

The changeset's own subject was "stop making unverifiable claims about where code came from". It
shipped six unverifiable or false claims of its own. That is the finding worth keeping.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | `package_release.py` excluded `project.mbproj` from release zips. The shipping runtime calls `XmlResource.GetMbprojxmls()` for every module and registers its `<file>` nodes as native resources; that loader is disjoint from `SubModule.xml`'s `<XmlName>` glob. TAOM's mbproj is the only registration for four voice-definition XMLs and `module_sounds.xml`; `LOTRLOME_Armory`'s is the only one for its monsters and action sets, whose absence is a documented native spawn CTD | Packaging | Classified the file as editor-only from its **name** and its three dev-path elements. Never decompiled the loader. "Research First" was applied to the `.vs` copy mechanism in the same commit and not to this | Before excluding any file from packaging, prove no engine loader reads it. New comment block at the exclusion site records the mbproj loader specifically, and the test now asserts `COPY` as a regression guard |
| 2 | HIGH | New `Main/_Module/THIRD-PARTY-LICENSES.txt` closed with "All other binaries under `Main/_Module/bin/` are original TAOM work, licensed under the MIT License". Two of the three binaries there are not: `TAOM.NativeSkinFixes.dll` is a verbatim C++ port of an unidentified upstream, and `TAOM.dll` compiles the uncleared `Features_fixed` drop | Legal artifact | Wrote a tidy catch-all closing sentence without enumerating what it swept up. The file was created to fix an omission and created a stronger defect of the opposite polarity | New rule row: "Never assert ownership you do not have." A notice enumerates, or scopes the sentence to what it can stand behind |
| 3 | HIGH | `coop-modules.txt` (ships) claimed "we do not know any co-op mod's Harmony id. TAOM does not decompile a co-op mod to find out", seven lines below a link to a dossier recording a 6-DLL / 3,270-file decompile of BannerlordCoop and four verified owner ids | Legal artifact | I *narrowed* a pre-existing over-broad claim and checked only that the new version was weaker, not that it was true. Narrowing moved it from false-in-general to false-about-exactly-this-file's-subject | When softening a claim, verify the narrowed version against evidence. A weaker false statement is still false, and a narrowed one is easier to disprove |
| 4 | HIGH | The rule, the register, and `docs/INDEX.md` all described `tools/check_provenance.py`, a baseline file, a hook, a test class, and a CI step in the present tense. None exist | Never Fabricate | Wrote the designed end-state as though built. The plan had it as Phase 5; the docs landed in Phase 0 | Future-tense anything unbuilt. This is `evidence-over-claims` §C applied to one's own roadmap, which is the case the rule does not spell out |
| 5 | HIGH | Register asserted `Main/Features/Chariot/**` on both the ADOD_Beasts row (cleared) and the chariot row (uncleared). The directory does not exist | Register | The register was designed as if a checker validated its globs. No checker existed, so five globs were wrong and nothing said so | Fixed the globs; recorded that hand-maintenance is the current state until the checker exists |
| 6 | MED | Two register `Covers` globs asserted the wrong authorship: BUTR's glob swallowed the .NET Foundation and Serilog DLLs; BetaDeps' swallowed five TAOM-original files and missed `SubModule.cs`, the one file that says it mirrors a BetaDeps list | Register | Wrote globs by directory shape rather than by enumerating what they matched | Same as #5 |
| 7 | MED | MinHook notice dropped "All rights reserved." from the copyright block. BSD-2 clause 2 requires reproducing the copyright notice, and upstream's includes that line | Legal artifact | Transcribed the licence body byte-perfectly (verified by mechanical diff) and retyped the copyright line by hand | Copy the whole block mechanically, including the lines that look like boilerplate |
| 8 | MED | `FailOnIdeStateInModule` had no `Condition`, so it fired on `-p:DisableModuleCopy=true -p:ModuleId=`, the invocation the agent operating manual mandates. MSBuild schedules a `BeforeTargets` dependency before evaluating the host target's own `Condition` | Build | Assumed a `BeforeTargets` inherits the host's gating | Conditioned on what `CopyModule` itself checks. Verified both directions empirically |
| 9 | MED | Line-wrapped euphemisms survived the bulk rename, in one case leaving broken text ("The shape came from upstream: ADOD_Beasts / beasts pack's own Monster") | Tooling | Line-oriented regex over prose that wraps mid-phrase | Grep for the orphan half of each phrase after any multi-word substitution, not just for the whole phrase |
| 10 | MED | Four euphemisms plus a false "clean-room port" claim in `Main/_Module/Prefabs/taom_howdah_agent.xml`, a shipped file | Coverage | The sweep enumerated `.cs` and `.md`. Shipped prefab XML was never in the file list | Rule's `paths:` now includes `**/_Module/**/*.xml`; the globs were also non-recursive and missed eight in-scope docs |
| 11 | MED | Register said BetaDeps had "13 source headers"; `git grep` says 10. Thirteen files mention BetaDeps | Register | Counted files, wrote headers | Recount every number against the command that produces it, especially in a document arguing "the headers are accurate" |
| 12 | MED | Two rows contradicted their own detail: ROT-Core `comparison-only` ("nothing derives from it") while naming 13 covered files; NativeSkinFixes `behavioural-port` while its detail and the cited RCA say "copied with minimal modification" | Register | Picked the flattering value from the vocabulary rather than the one the evidence supports | Reclassified. NativeSkinFixes is now the register's top-priority row: a verbatim port with no identified upstream, shipping today |
| 13 | LOW | Shipped notice published "BetaDeps license: not yet established" plus a repo-internal doc path, contradicting the rule written in the same changeset | Legal artifact | Two artifacts written hours apart, neither checked against the other | Rule now distinguishes describing a derivation factually from publishing an internal status tracker |
| 14 | LOW | `tools/README.md` still described the two faction-map scripts as "None (hardcoded paths)" after they became env-required | Doc drift | Changed the code, not its catalogue row | |
| 15 | LOW | `require_inputs()` closed over one module's globals while a second module called it to validate its own separately-read copies. Inert today, live the moment either default diverges | Tooling | A shared helper that reads globals instead of parameters | Now takes explicit arguments; the caller passes its own values |

## Root-cause pattern

**Twelve of fifteen findings are an artifact asserting something about another artifact, with nothing
checking the assertion.** The register asserts globs match files. The notices assert what binaries
contain. The rule asserts a checker runs. The CHANGELOG asserted counts. Each was written in the
voice of a validated document while nothing validated it.

That is the same defect the changeset was written to fix, one level up. The old state was code
claiming an unverifiable provenance; the new state was documentation claiming an unverifiable
coverage. Writing the register before the checker guaranteed it, because the register's whole design
(globs as a coverage contract, backticked tokens, a shrink-only baseline) presumes a machine reads it.

**The generalisation:** when a document's *format* is designed for machine validation, either build
the validator in the same change or state plainly that the document is hand-maintained. A
machine-shaped document with no machine is strictly worse than a prose one, because its shape invites
the reader to trust it.

## Why each agent caught or missed

- **Build/packaging agent** caught #1 by decompiling the engine loader instead of accepting the file's
  name, and #8 by reading the BuildResources targets and running MSBuild with `-v:diag`. Both required
  going outside the changeset. This is the agent that justified its cost.
- **Legal-accuracy agent** caught #2, #3, #7, #12, #13, by opening 17 cited `file:line` references and
  mechanically diffing the licence body. Nothing else would have found #3: it needed a reader who
  followed a link printed in the same file.
- **Completeness agent** caught #4, #5, #10, #11 by recounting every number and running the globs.
- **Tooling agent** proved the bulk rename caused no encoding damage, using the git index stat cache
  rather than `numstat` (which cannot see CRLF normalisation under `autocrlf=true`). It also caught #9
  and #15.
- **Cross-session agent** confirmed the other session's files were separable and produced the exact
  staging list. It also independently flagged #4.
- **Nobody caught** the `Main/_Module/Prefabs/**` gap until the completeness agent widened its own
  scope. Every agent brief listed `.cs` and `.md`. The brief was the blind spot, not the agents.

## Lesson to codify

Appended to `docs/reviews/lessons/build-tooling-workflow.md`: *"Prove no loader reads a file before
excluding it from packaging"* (#1), and *"A machine-shaped document with no machine is worse than
prose"* (root-cause pattern).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
