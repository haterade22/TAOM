# Writing tells

The full list of AI-writing patterns for TAOM prose, for every AI client. The short form loads in each
Claude session from [output-style.md](../../.claude/rules/output-style.md) Part 2, and the AGENTS.md
"Human prose" row links here for Codex and other clients. Read this file before a release note, a long
issue, a feature doc or an RCA, and whenever a rewrite still sounds wrong.

Language models pick the likeliest next word, which suits the widest range of readers and subjects. A
person writes for one reader about one subject, so their choices are uneven and specific. The fix for
AI-sounding prose is nearly always a more specific claim.

## What this covers

**In scope:** commit bodies, CHANGELOG entries, release notes, issues, PRs, feature docs, RCAs, doc
paragraphs, chat replies, status reports and new code comments. New writing only.

**Out of scope:** existing prose you are not rewriting; code and identifiers; quotes, logs and error
text; player-facing strings, which keep their Tolkien voice and go through `/localize` (an English edit
stales 12 languages).

**ASD-STE100:** the output style governs chat, status, procedures and code comments. Where it conflicts
with this list, STE wins in that text: short sentences stay, and lists for three or more parallel items
stay. STE does not cover commit bodies, release notes, issues, PRs or docs, because `/release` publishes
commit bodies to players.

## The pass

1. **Mark the tells,** strongest first. Check paragraph shape as well as sentences: a contrast split
   over two sentences, or the same closer after each section, is one tell at a larger scale.
2. **Rewrite around the claim.** Do not patch flagged phrases one at a time. Keep every supported claim.
   Add no fact, name, number, date or citation that the source does not give. If a sentence needs a
   detail you lack, find it or write a simpler sentence.
3. **Compare the rewrite with the draft.** Look for a fact, name, number, date, ranking or
   "at the same time" claim that appeared or vanished. An addition with no source is an error. A loss
   is an error unless a pattern below calls for the cut.
4. **Search again** for the tells that survive rewrites most often: 1, 2, 6 and 8.

Patterns 1 to 5 justify an edit on one sighting. A pattern marked *weak alone* needs other tells in the
same passage before you act on it. Numbers follow upstream v3.1.0, so a later comparison is direct.

## A. Staging in place of stating

| # | Tell | Watch for | Keep it when |
|---|---|---|---|
| 1 | Contrast that only adds weight | "not just X but Y", "it's not X, it's Y", X rather than Y; the split form ("This does not mean X. It means Y."); a clipped tail (", no guessing") | The negative half corrects a belief the reader holds, or both halves carry information. In TAOM, "X, never Y" that names a real constraint or a failed alternative stays |
| 2 | One-line closer or fragment | A one-sentence paragraph that repeats the last one; "That distinction matters."; "Let that sink in."; a line after an example that names what it showed; a row of fragments; one ordinary word in capitals for effect (status words such as UNVERIFIED are not this) | The short sentence adds a fact or consequence the text has not shown |
| 3 | A saying in place of the claim | "at its core", "the real question is", "what really matters", "X is the language of Y", "X becomes a trap" | Replace it with the specific claim. It stays only in a quotation |
| 4 | Run-up before the point | "let's dive in", "here's what you need to know", "Honestly?", "Here's the thing", "quick note" | "Honestly" or "look" inside an ordinary casual sentence. The tell is the standalone opener |
| 5 | Arguing with no one | "To be clear", "I'm not saying", "A tempting approach would be", "You might think... but" | Someone raised the objection, or a reader would weigh the option. A dead end or a rejected design in an RCA, ADR or adoption record is real history |

## B. Rhythm by rule

| # | Tell | Watch for | Keep it when |
|---|---|---|---|
| 6 | Forced threes | Three items or three parallel examples where the meaning has fewer parts | There are three real items. In STE text, lists stay |
| 7 | Repeated openings | Several sentences in a row that start with the same subject | The repetition is deliberate |
| 8 | Dashes | Em dash (U+2014), en dash (U+2013), a double hyphen used as a dash | Never in prose. AGENTS.md lists the substitutes and the exemptions: code, URLs, verbatim outside quotes |
| 9 | Stacked qualifiers | "could potentially", "might arguably", "to be fair" (*weak alone*) | The doubt is real. Confidence tags, UNVERIFIED, NOT_SEEN and scoped negative results always stay (`evidence-over-claims.md`) |
| 10 | Hyphen after the noun | "the process is well-documented" (*weak alone*) | The word always takes a hyphen, such as third-party |
| 11 | Passive voice that hides the actor | "The results are preserved"; a dropped subject (*weak alone*) | The actor is unknown or does not matter. Otherwise name what validates, parses or writes |

## C. Inflation and borrowed authority

The fact underneath is usually sound. Keep it and remove the dressing.

| # | Tell | Watch for | Keep it when |
|---|---|---|---|
| 12 | AI vocabulary | additionally, align with, crucial, delve, enduring, enhance, garner, highlight (verb), interplay, intricate, key (adjective), landscape, meticulous, pivotal, quietly, showcase, tapestry, testament, underscore, valuable, vibrant | The use is literal or technical. TAOM's `gate`, `gated` and `robust` in their technical sense stay |
| 13 | Inflated significance | "a pivotal moment", "reflects a broader", "setting the stage", "the future looks bright"; a stock challenges-and-outlook section | Never. End on the last concrete fact |
| 14 | Vague connection | "associated with", "linked to", "in connection with" | The source does not say how. Do not invent the relation |
| 15 | Shallow -ing riders | ", highlighting...", ", ensuring...", ", reflecting..." on a plain fact | The source supports what the rider claims |
| 16 | Sales language | rich (figurative), profound, nestled, renowned, breathtaking, stunning | Never in docs or commits |
| 17 | Borrowed authority | "experts argue", "observers have cited", a list of prestige names | The text names the source and what it said |
| 18 | Avoiding is, are, has | serves as, stands as, functions as, boasts, features | The longer verb says something different |

## D. Formatting by rule

| # | Tell | TAOM disposition |
|---|---|---|
| 19 | Bold as decoration, bold-label lists | **Does not apply to the house style.** Boldface, inline `**Label:**` headers, tables and backticked paths are deliberate markers. A bold word that marks nothing still goes |
| 20 | Decorative headings: title case, emojis, arrows, a heading written for effect | Applies. Use sentence case, and name what the section holds. The Discord release note keeps its emoji section headers (`/release` Phase 5) |
| 21 | Curly quotes where the file uses straight ones (*weak alone*) | Applies only when it clashes with the file |

## E. Leftovers from the chat and the draft

| # | Tell | Watch for | Keep it when |
|---|---|---|---|
| 22 | Chatbot residue | "I hope this helps", "Certainly!", "Great question!", "Let me know if" | Never in produced prose |
| 23 | Knowledge-limit disclaimers and guesses | "up to my last training update", "not widely documented", "likely grew up" | A scoped statement of what was searched and not found. That is a TAOM requirement (`evidence-over-claims.md` C.4) |
| 24 | A heading repeated by the first sentence | "## Performance" then "Speed matters." | Never |
| 25 | Text about the document | "was added to replace", "compiled from", "the table below compares" | Commit bodies, CHANGELOG, release notes, RCAs and adoption records, which describe change and method by design. A caveat that changes what the reader does |

## F. Writing for the wrong reader

| # | Tell | Watch for | Keep it when |
|---|---|---|---|
| 26 | Re-explaining what the reader knows | A short reply that restates the problem and walks through the diagnosis before the decision; the answer in the last line | Evidence the rules require beside a claim (`evidence-over-claims.md` B). Otherwise lead with the decision, and put the diagnosis in the issue or doc |

## TAOM additions

Older TAOM reviews found these too: metaphor nouns (substrate, wedge, ratchet, north star: use the
concrete word); a feeling in place of the mechanism or a number; filler ("in order to"); over-compression
(dropped articles and arrows in place of sentences).

## When not to act

Leave a watched phrase alone inside a quotation, a title, a proper name, or a passage that discusses the
phrase. People who judge by feel do little better than chance, so one weak tell proves nothing. Keep
specific, odd detail and any aside that carries real information.

## Source

Adapted from [blader/humanizer](https://github.com/blader/humanizer) v3.1.0, commit `0ef9fe5bc` (MIT),
which draws on Wikipedia's [Signs of AI writing](https://en.wikipedia.org/wiki/Wikipedia:Signs_of_AI_writing).
The review is in [adopt-humanizer-2026-11-08.md](../reviews/adopt-humanizer-2026-11-08.md).
