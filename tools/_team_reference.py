"""A language team's accepted text, used to steer the AI translator toward it (#1421).

tools/translation_reference/<lang>.json holds [English, team text] pairs for every row of ours that
carries a translation team's text (tools/build_translation_reference.py writes it). For a batch of
English entries, a Reference picks:

  - terms: short pairs (names, game terms) whose English occurs in the batch, to use exactly;
  - examples: longer pairs that share the most words with one entry, to show the team's style.

guide() turns both into a block for the request's user message. A language with no reference file
gets an empty block, so its requests are unchanged.
"""

from __future__ import annotations

import json
import re
from collections import Counter, defaultdict
from pathlib import Path

REFERENCE_DIR = Path(__file__).resolve().parent / "translation_reference"
TEAM_LANGUAGES = ("CNs", "CNt")  # languages a translation team owns; each should have a reference file

POSSESSIVE = re.compile(r"['’]s\b")  # "Forlong's Gloves" names Forlong
WORD = re.compile(r"\w+(?:['’]\w+)*")
EXPRESSION = re.compile(r"\{[^{}]*\}")  # {VAR}, {?VAR}: a placeholder's words are not the English
TERM_MAX_WORDS = 4
TERM_MAX_CHARS = 40
SINGLE_WORD_MIN_CHARS = 4      # "On" or "Yes" alone would rewrite every sentence that holds the word
CONFIRMED_MIN_CHARS = 3        # "Orc" is a term once the team's sentences confirm it
CONFIRM_MIN_SENTENCES = 2      # team sentences that must confirm a three-letter word
EXAMPLE_MIN_WORDS = 6
EXAMPLE_MAX_CHARS = 300        # a long encyclopedia text would fill the request
EXAMPLE_MIN_SCORE = 2          # content words an example shares with one entry
CONTENT_MIN_CHARS = 4
STOPWORDS = frozenset("""
    about after again also been before being could does each from have here into just like make more most
    much must only other over same some such than that their them then there these they this those very
    what when where which while will with would your yours you're it's don't can't
""".split())


def words(text: str) -> list[str]:
    return WORD.findall(POSSESSIVE.sub("", EXPRESSION.sub(" ", text)))


def single_word_term(word: str) -> bool:
    """A name or a capitalised game term. A lowercase word is a context label in the team's text ("horse" is
    骑兵队, a cavalry unit), and an all-caps word is a screen label such as GENDER."""
    return len(word) >= CONFIRMED_MIN_CHARS and word[0].isupper() and not word.isupper()


def _keys(ws: list[str]):
    """Each term key in a list of words: one word with its case, two to TERM_MAX_WORDS words without."""
    lower = [w.lower() for w in ws]
    for i, w in enumerate(ws):
        yield (w,)
        for n in range(2, TERM_MAX_WORDS + 1):
            if i + n <= len(ws):
                yield tuple(lower[i:i + n])


def content_words(text: str) -> set[str]:
    return {w for w in (x.lower() for x in words(text)) if len(w) >= CONTENT_MIN_CHARS and w not in STOPWORDS}


class Reference:
    def __init__(self, pairs):
        counts: dict[str, Counter] = defaultdict(Counter)
        for en, zh in pairs:
            if en.strip() and zh.strip():
                counts[en][zh] += 1
        # One text per English: the one the team uses most; a tie goes to the first in sort order.
        self.pairs = {en: min(c.items(), key=lambda kv: (-kv[1], kv[0]))[0] for en, c in counts.items()}
        self._terms: dict[tuple[str, ...], tuple[str, str]] = {}
        self._examples: list[tuple[str, str, frozenset[str]]] = []
        self._by_word: dict[str, list[int]] = defaultdict(list)
        for en, zh in sorted(self.pairs.items()):
            ws = words(en)
            if not ws:
                continue
            if (len(ws) <= TERM_MAX_WORDS and len(en) <= TERM_MAX_CHARS and "{" not in en and "{" not in zh
                    and (single_word_term(ws[0]) if len(ws) == 1 else bool(content_words(en)))):
                key = (ws[0],) if len(ws) == 1 else tuple(w.lower() for w in ws)
                self._terms.setdefault(key, (en, zh))
            if len(ws) >= EXAMPLE_MIN_WORDS and len(en) <= EXAMPLE_MAX_CHARS:
                cw = frozenset(content_words(en))
                for w in cw:
                    self._by_word[w].append(len(self._examples))
                self._examples.append((en, zh, cw))
        self._confirm_terms()

    def _confirm_terms(self) -> None:
        """Keeps a term only where the team's longer rows that hold its English mostly hold its text too. The
        team's name row can name another person than its own sentences do (Findariel is 吕西卡 in the name
        row and 芬达瑞尔 in her description). A short single word needs that confirmation."""
        support: Counter = Counter()
        hits: Counter = Counter()
        for en, zh in self.pairs.items():
            ws = words(en)
            for key in set(_keys(ws)):
                term = self._terms.get(key)
                if term and len(ws) > len(key):
                    support[key] += 1
                    hits[key] += term[1] in zh
        for key in list(self._terms):
            if 2 * hits[key] < support[key] or (len(key) == 1 and len(key[0]) < SINGLE_WORD_MIN_CHARS
                                                and support[key] < CONFIRM_MIN_SENTENCES):
                del self._terms[key]

    def __bool__(self) -> bool:
        return bool(self.pairs)

    def terms(self, texts: list[str], cap: int = 80) -> list[tuple[str, str]]:
        """The terms whose English occurs in texts: a single word with its case, more words without.
        Longest first, then alphabetical; at most cap."""
        found = {self._terms[key] for text in texts for key in _keys(words(text)) if key in self._terms}
        return sorted(found, key=lambda t: (-len(words(t[0])), -len(t[0]), t[0]))[:cap]

    def examples(self, texts: list[str], k: int = 6) -> list[tuple[str, str]]:
        """The k longer pairs that share the most content words with any one of texts, at least
        EXAMPLE_MIN_SCORE; ties go to the shorter, then alphabetical."""
        best: dict[int, int] = {}
        for text in texts:
            shared: Counter = Counter()
            for w in content_words(text):
                shared.update(self._by_word.get(w, ()))
            for idx, score in shared.items():
                if score >= EXAMPLE_MIN_SCORE and score > best.get(idx, 0):
                    best[idx] = score
        ranked = sorted(best, key=lambda i: (-best[i], len(self._examples[i][0]), self._examples[i][0]))
        return [self._examples[i][:2] for i in ranked[:k]]

    def guide(self, target_language: str, texts: list[str]) -> str:
        """The reference block for one request, or "" when nothing in the reference applies."""
        terms, examples = self.terms(texts), self.examples(texts)
        if not terms and not examples:
            return ""
        lines = [f"REFERENCE: the {target_language} translation team's published text is the standard for "
                 f"this language. Follow it over your own choices and over rule 4."]
        if terms:
            lines.append("TERMS: where the English means the same thing, use exactly these renderings:")
            lines += [f"{en} => {zh}" for en, zh in terms]
        if examples:
            lines.append("STYLE EXAMPLES: the team's own translations of similar lines. Match their wording, "
                         "punctuation and register, but never copy a name from an example into an entry that "
                         "does not hold that name:")
            for en, zh in examples:
                lines += [f"EN: {en}", f"ZH: {zh}"]
        return "\n".join(lines)


def path_for(lang: str, directory: Path = REFERENCE_DIR) -> Path:
    return directory / f"{lang.lower()}.json"


def load(lang: str, directory: Path = REFERENCE_DIR) -> Reference:
    """The language's reference, or an empty one when it has no file."""
    path = path_for(lang, directory)
    if not path.exists():
        return Reference([])
    return Reference(json.loads(path.read_text(encoding="utf-8"))["pairs"])
