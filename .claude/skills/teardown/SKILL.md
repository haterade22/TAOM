---
name: teardown
description: "Use when an outside tool, source or review gave a reference or result for something we build ourselves, or the maintainer rejected one result twice. Run before the next round."
argument-hint: [part or result]
---

# Teardown: learn from a result that beat ours

Take the result apart, find what it does that ours does not and why that is better, and make it our default. The
output is a record in `docs/knowledge/teardowns/` and one entry per default in `docs/knowledge/techniques/`
([knowledge-library.md](../../../docs/features/knowledge-library.md)).

## When it is owed

- A reference for a part we build ourselves came from an outside tool or source: an image to 3D model, a
  generated picture, a bought kit, an existing set.
- The maintainer rejected the same result twice. A third round of patches comes after the teardown.
- A tool, a review or another session solved what our own rounds did not. No such case is recorded yet.

Not this skill: a tool's mesh that becomes the asset itself (`/prep-tripo-prop`), and an outside repository or
article (`/adopt-external`; its record is then the `adopt` source of the entries).

## Step 1: Have the result, and open it

1. If the trigger is a second rejection and no reference exists, get one first: ask the maintainer for one, or
   make one with a tool under that tool's rules (cost, credits, what may be sent).
2. Open the file that holds the result: the model, the kit's source, the report. A preview picture of a model is
   not the model.
3. Read a third-party reference where it lies. It is measured only: never copy it into the repository, a module
   or the asset. If the maintainer said not to name a source, write a neutral name for it in every tracked file.

Done when a reference file exists or the maintainer was asked for one, and you can name the file you measured
with its size or its counts.

## Step 2: Name what the eye sees, and a property for each

List each thing the result shows that ours does not. Name for each a property that a script can measure, with
its unit. For a stack of bands and a shell, start from the property table at the end of the
[first record](../../../docs/knowledge/teardowns/2026-11-26-horse-armour-neck-and-head.md). For work that is not
a mesh (a review, a tool run), the property is a count: the findings for each file, the check that found each,
what our own checks missed.

Done when each feature that the reference shows, and each thing the maintainer marked, has a row with a property
and a unit, or the word "taste". A taste goes to the maintainer as a question.

## Step 3: Measure the result and ours with one script

1. Measure three things with the same script: the result, our last build before it, our build now. Where no
   build of ours came before the result, write "none".
2. Make the script write its arguments and its input files into its output. Keep the script beside the output.
3. Choose one section and one landmark that exist on both sides, and name them. A result with no true size
   gives ratios, not lengths: state the scale you used. For a picture, name the views and log the pixel columns
   or rows that you measured.
4. State for each row the subset it was measured on, its count, the method, its resolution, and the parameter
   that the number is sensitive to. Mark a row whose two forms are only loosely comparable.
5. Where our number is worse, measure it at each stage of our chain: the reference, the prepared input, what
   the code takes, the built result. The stage where the number drops is where the fix goes.
6. For a mesh: sections 5 and 6 of `/blender-asset` give the headless Blender launch and the views.
   `tools/blender/probe_fbx.py` gives the triangle counts and sizes of an FBX, and
   `tools/blender/measure_tripo_layout.py` gives the layout of a Tripo model. No probe for sections and circle
   fits is in the repository yet. The model for one is `td_probe.py` of the first case, which also opens a GLB.
   It is outside git, on the build machine only: `E:\LOTRAOMAssets\_auto_workspace\*\out\tripo_in\_teardown\`.

Done when every row has a number for the result and for ours, and each number names its script and its report
file.

## Step 4: Say why, and what it costs

1. Write three lists, each line with its number: the result has it and ours did not; the result has it and
   ours still does not; ours has it and the result does not (for a mesh: triangle budget, LODs, skin weights,
   clearances).
2. For each line of the first two lists, state the mechanism in one sentence (for a mesh: light, silhouette,
   rhythm, cleanliness, fit). A line with no mechanism is a taste.
3. State the cost of taking it: triangles against the cap, a rebuild of other pieces, a pinned number that
   changes.

Done when the three lists exist, each line of the first two has a mechanism or the word "taste", and each
technique that adds geometry or forces a rebuild has its cost.

## Step 5: Find how each technique acts today, in the code

Read our code and checks for each line of the first two lists, and give it one state of the ladder ("The ladder"
in the feature doc). A method in one asset's own build scripts is an `instance`. It is `code` only when a shared
function in the repository builds it. Then ask two more questions:

- **Does anything act against it?** A constant or a gate that refuses what the result has: an exact mirror, a
  clamp, a forced gap. Name it. The technique cannot act until that changes, and a change of art direction is
  the maintainer's decision.
- **Is the gate blind?** A gate exists for the property and cannot see the measured fault, because of its
  threshold or its input. Prove a gate: break the default in a copy and run the check. It must fail.

Done when each `code`, `gate`, `report` and "against" names a file and a text in it that a build or a test
calls, and each `gate` was seen to fail on a broken copy or says "not proven".

## Step 6: Write the record

Write `docs/knowledge/teardowns/<date>-<part>.md` with the sections of the first record, and add what that
record lacks:

- for the reference: the measured files with their sizes or counts, which subset was used, and how the result is
  used (as a picture, as a measure, or as data that our code reads);
- for each build that was measured: its state or its hash;
- for each number: its script and report file, its subset, its resolution and the parameter it is sensitive to,
  and a unit for each property (the first record's crest numbers came from two scripts, and only `td_probe.py`
  writes its arguments);
- the three lists of step 4 with their mechanisms and costs (the first record merges the first two lists into one
  table and keeps the mechanisms in the entries);
- in each row: what acts against the technique, and each blind gate;
- what was not taken, by whose decision and when;
- the maintainer's verdicts in his words, with their time, apart from your own judgement.

The level of each default is in its entry, where `check` reads it, not in the record. A teardown that gave no
technique gets the line `No technique: <reason>`.

Done when the record has each section of the first record and each item above, or says for an item that there
is none.

## Step 7: Write one entry per default

Follow "How to add a technique" in the feature doc. A technique for the files of one area of the repository is
one line in that area's path rule, not an entry. For a teardown also:

- The source is `teardown = "<the record's file stem>"`.
- The level is the state from step 5. `next` says what would make it act, and names what acts against it.
- Take a limit from the reference that matches the look he approved. It is your target until he confirms it: say
  so in the entry.
- An approved asset stays as he approved it. The default is for the next part of that kind, or for his word.
- The trigger names the builder files where the work happens, never a whole work folder. A folder that holds
  only reference results is the exception (`*/tripo_in/*`).

Done when `python tools/knowledge.py check` exits 0 and `python tools/knowledge.py for <a builder file>` prints
the entry. A `teardown-uncited` finding means that no entry names the record.

## Step 8: Have a second reader check it

Spawn one checker that reads the record and the entries against the code and the reports: each state, each
number, and each reason. A reason that is your own reading has the grade `inferred`, never the maintainer's
name. Both faults were in the first case: its first draft called things defaults that no shared build obeyed,
and its first entries gave the writer's reading as the reason with no source.

Done when each finding of the checker is applied, or answered in the record.

## Step 9: Hand over

1. Put the entries into the brief of every builder you start:
   `python tools/knowledge.py brief --paths <files> --task <phrases>`. A subagent cannot invoke this skill.
2. Tell the maintainer the three lists, the tastes, what acts against a technique, and the three defaults that
   would repay the work most.
3. Take one default up the ladder when the next part of that kind is built ("How to move an entry up the
   ladder" in the feature doc).

Done when the report is sent and the brief of each running builder holds the entries.
