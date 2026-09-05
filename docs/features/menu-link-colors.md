# MenuLinkColors — per-faction hyperlink colours in the game menu

**Status:** implemented 2026-07-26 · **Issue:** [#362](https://github.com/haterade22/TAOM/issues/362) · **Patch category:** `Patch64_MenuLinkColors` · **Code:** `Main/Features/MenuLinkColors/`

## What it does

The game-menu body text renders settlement, lord, kingdom and clan names as clickable
encyclopedia links. Vanilla gives each *link type* one fixed colour — every settlement is the same
tan, every kingdom the same grey-blue — and those colours were chosen for Bannerlord's dark menu
panel. TAOM reskinned the menu to a light parchment and set the body text to black, so the
inherited link colours sat at under 3:1 contrast and read as washed-out noise.

This feature colours each link by the **culture of the object it points at**, in a palette tuned
for the parchment. Entering Minas Morgul, the settlement, the Witch-King and Mordor all read in
Mordor's oxblood. After Gondor takes the town, the place name stays Mordor-coloured (its culture
does not change) while the new governor and his realm read in Gondor's steel-blue — the colour
carries information, not just flavour.

## Why it needs C# at all

The obvious approach — edit the brush XML — cannot do it. Three facts, all verified against the
v1.4.7 decompile:

1. **The style name is hardcoded engine-side.** `TaleWorlds.Core.HyperlinkTexts`
   (`HyperlinkTexts.cs:59/67/75/91`) emits
   `<a style="Link.Settlement" href="event:{LINK}"><b>{NAME}</b></a>`. The string is the same
   whether the settlement is Gondorian or Mordorian.
2. **There is no inline colour markup.** `RichText.cs:486-516` parses only `img`, `a` and `span`,
   and `a`/`span` accept only a *named* `style`. So a colour cannot be computed at runtime and
   injected — it must be a style the brush already defines. This also rules out driving colours
   from `CultureObject.Color`.
3. **`taom_spcultures.xml` colours would be the wrong source anyway.** `erebor`, `gondor` and
   `mordor` carry byte-identical `color="0xFF23432D"`, the 8 bandit cultures carry none, and the
   values are dark banner tones unusable on a light background.

So the design is: **static named styles in the brush XML, chosen at runtime by a style-name
rewrite.**

## How it works

```
GameMenuVM.set_ContextText(value)          ← Patch64 prefix, ref string value
        │
MenuLinkStyleRewriter.Rewrite(text)        ← pure string transform, fully unit-tested
        │  for each <a style="Link.{Settlement|Hero|Kingdom|Clan}" href="event:X-id">
        ├─ IEncyclopediaCultureLookup      ← X-id → MBObjectManager → object → Culture.StringId
        ├─ TaomCultureLinkStyles           ← is this culture authored?  → "Link.Taom.<id>"
        └─ IMenuBrushStyleProbe            ← does the LIVE brush define that style?
        │
GameMenu.InfoText brush                    ← Main/_Module/GUI/Brushes/GameMenu.xml
```

The href payload is `"<ElementName>-<StringId>"` — `Settlement-town_ES1`, `Hero-lord_1_1`,
`Kingdom-gondor`, and for clans **`Faction-clan_xyz`**, because `Clan` is registered in the object
manager under the element name `Faction` (`Campaign.cs:1543`). Splitting on the first dash and
calling `MBObjectManager.GetObject(elementName, stringId)` mirrors vanilla's own round-trip in
`EncyclopediaManager.GoToLink`.

### Why the setter is the patch target

`GameMenuManager.GetMenuText` and `GameMenu.GetText()` return the **same cached `TextObject`
reference** on every call, and `GameMenuVM.IsMenuTextChanged()` compares by reference **every
frame**. A postfix there returning a new object would rebuild the menu text at frame rate forever;
mutating the returned object would corrupt the menu's stored template. `set_ContextText` is
reached only when `_requireContextTextUpdate` is set — once per menu open or refresh — and the
prefix runs before vanilla's `if (value != _contextText)` guard, so the field and the comparison
both see the rewritten string and no extra property-change notification fires.

Patching `HyperlinkTexts` directly would have been simpler and is the wrong answer: it is
process-global, so TAOM's style names would reach the encyclopedia, quest log and tooltips, whose
brushes do not define them. `Brush.GetStyleOrDefault` (`Brush.cs:304`) returns the brush's
`Default` style for an unknown name **with no error**, so the failure mode would be *every
hyperlink in the game silently turning into plain body text*. Rewriting at the menu seam makes
that structurally impossible — no other UI surface passes through this ViewModel.

## The palette

20 cultures × 3 states (base, `.MouseOver`, `.MouseDown`) = 60 styles, in the `GameMenu.InfoText`
brush. Colours live **only** in that XML; the C# holds culture ids and nothing else, so retuning
is an XML edit with no rebuild.

| Culture id | TAOM name | Colour | Culture id | TAOM name | Colour |
|---|---|---|---|---|---|
| `gondor` | Gondorian | `#243B66` | `khuzait` | Easterlings | `#6B4A0E` |
| `rivendell` | Ñoldor Elves | `#3B2A6B` | `aserai` | Haradrim | `#8A3B08` |
| `lothlorien` | Galadhrim Elves | `#5C5518` | `abanissa` | Âbanissa | `#1E3560` |
| `mirkwood` | Silvan Elves | `#1F4A2E` | `shaghana` | Shaghâna | `#7A4A12` |
| `erebor` | Dwarves | `#6B3410` | `umbar` | Umbar | `#6B2044` |
| `vlandia` | Rohirrim | `#3D5216` | `mordor` | Mordor | `#7A1010` |
| `sturgia` | Barding | `#0F4A55` | `isengard` | Isengard | `#3B4650` |
| `empire` | Dunlendings | `#5A3E1E` | `gundabad` | Gundabad Orcs | `#4A423C` |
| `battania` | Variag | `#6B2626` | `dolguldur` | Dol Guldur Orcs | `#46295E` |
| | | | `goblin` | Goblins | `#2E4322` |
| | | | `mistymountainorcs` | Misty Mountain Orcs | `#3F3A33` |

### The contrast rule

Every menu link colour must sit inside relative luminance **0.035 ≤ L ≤ 0.10**:

- **upper bound** — legible against the pale parchment (L ≈ 0.78);
- **lower bound** — distinguishable from the **black** body text, so a link still reads as a link.

The lower bound is the one that is easy to forget. On a dark panel you make links *lighter*; on
parchment the instinct to reach for "Gondor silver" or "Rohan gold" produces something invisible.
The LOTR identity is carried by **hue**, with value pinned into a narrow dark band.

For the same reason hover and press **darken** (`TextColorFactor` 0.72 / 0.52). Vanilla's `Link.*`
styles brighten (`TextColorFactor="1.5"`) because its panel is dark — copying that convention here
would wash the text out. Do not copy it.

`MenuLinkBrushCoverageTests` re-derives both bounds from the shipped XML, so retuning the palette
is safe: an out-of-window colour fails `dotnet test`, it does not ship and get noticed in-game.

### Why every style restates its glow

Each style zeroes `TextGlowColor` / `TextGlowRadius` / `TextBlur` / `TextOutlineAmount` explicitly
rather than relying on the brush `Default`. That looks redundant and is not — **inheritance differs
between the two groups of styles**:

- A style name **not** on the inherited `Info.Text` brush (all 60 `Link.Taom.*`) becomes a fresh
  `Style` whose unset attributes genuinely do fall back to this brush's `Default`
  (`BrushFactory.cs:560` assigns `style.DefaultStyle = brush.DefaultStyle`).
- A style name that **is** inherited (the 21 retinted fallback styles) was cloned by
  `Style.FillFrom` (`Style.cs:564`), which assigns through the **property setters** — and each
  setter latches `_isTextGlowColorChanged = true`. Vanilla sets `TextGlowColor="#111111FF"` on
  every `Info.Text` link style, so that value is already baked in before TAOM's redefinition is
  applied. Omitting the attribute leaves vanilla's dark halo behind dark link text on the pale
  parchment.

Requiring the attribute on every style removes the trap instead of documenting it.
`EveryLinkStyle_StatesItsGlowExplicitly` enforces it. (Caught by deep-review 2026-07-26 — the
original comment in the XML asserted the fallback worked for both groups, which was wrong.)

## What is deliberately left alone

The rewriter matches only the four link types that *have* an owning faction. These keep vanilla's
style name, and the styles themselves are separately retinted for parchment legibility in the same
brush:

| Case | Renders as |
|---|---|
| The 8 bandit cultures (`dunland_raiders`, `umbar_corsairs`, …) | `Link.Settlement` / `Link.Hero` etc., retinted |
| A culture from another mod, or one added without a style | same |
| An object that cannot be resolved from its href | same |
| `Link.Concept`, `Link.Unit`, `Link.Ship`, bare `Link` | their own retinted styles |

`Link.Ship` is worth noting: v1.4.7 added `HyperlinkTexts.GetShipHyperlinkText`, and **no brush in
the game defines `Link.Ship`** — every ship hyperlink already renders through the silent
`GetStyleOrDefault` fallback today. That is this exact failure mode shipping in vanilla.

## Failure modes and their guards

| Risk | Guard |
|---|---|
| A style name emitted by C# but missing from the XML → black body text, no error | `MenuLinkBrushCoverageTests` asserts every emittable name exists in the shipped XML with all three states and an explicit `FontColor` |
| A style in the XML that no culture can emit | same test, reverse direction — fails on orphans |
| A culture id with a style but no culture in `taom_spcultures.xml` | same test |
| A colour retuned outside the legible window | contrast test recomputes luminance from the XML |
| **Another module replaces `GUI/Brushes/GameMenu.xml` wholesale at runtime** | `IMenuBrushStyleProbe` checks the live brush before emitting; falls back to vanilla and logs once. No offline test can see this |
| Lookup or regex throws | caught; original text returned; one warning, not one per menu refresh |
| **A cached rewrite outliving the game state it was computed from** | there is no cache — see below |

### Why there is no cache

An earlier revision memoised the last (input, output) pair. The memo key is the menu **string**,
but the answer depends on the linked objects' **culture** — and the menu text is byte-identical
before and after a culture conversion, or across a load of a different save where the same
settlement has a different owner. The memo could therefore return a stale faction colour with
nothing to indicate it was wrong.

It was removed rather than invalidated. The setter runs once per menu open, so recomputing costs
one regex pass over a few hundred characters plus a handful of object lookups — the cache was
guarding a cost that does not exist. `Rewrite_SameTextAfterCultureChanged_ReflectsTheNewCulture`
and `Rewrite_SameTextAfterBrushBecameAvailable_StopsFallingBackToVanilla` pin the behaviour.

### The load-order hazard

`ResourceDepot.CollectResources` keys GUI files by lowercased module-relative path and does
`_files[key] = value` on collision — **the last module in load order replaces the entire file, with
no merging.** `Modules/DOTS/GUI/Brushes/GameMenu.xml` is byte-identical to TAOM's, so if DOTS is
enabled and ordered after TAOM, every style added here silently disappears. The probe turns that
from invisible-black-text into one log line naming the cause. **TAOM must load after DOTS.**

## Testing

`dotnet test TAOM.Tests --filter MenuLinkColors` — 30 tests covering the rewrite for all four link
types, mixed cultures in one string, the `Faction-` clan prefix, every fallback path (unsupported
culture, unresolvable object, malformed markup, lookup throwing, brush missing the style),
idempotency, warn-once behaviour, staleness after a culture change, and the XML↔C# coverage,
glow and contrast guards.

**Not unit-testable — requires an in-game smoke test:**

1. Enter a Mordor town → settlement, lord and kingdom all oxblood.
2. Enter Minas Tirith → steel-blue. Enter Edoras → Rohan green-gold.
3. A town held by a foreign lord → place name in the settlement's culture, lord and realm in his.
4. Hover a link (darkens, no wash-out), click it (navigates to the encyclopedia as before).
5. A bandit hideout menu → retinted fallback colours, never black body text.
6. **Scope check:** open the encyclopedia and quest log and confirm their link colours are
   unchanged — this is what proves the style names did not leak.

## Files

| File | Role |
|---|---|
| `Main/Features/MenuLinkColors/MenuLinkStyleRewriter.cs` | The rewrite; pure, uncached (see below), exception-safe |
| `Main/Features/MenuLinkColors/TaomCultureLinkStyles.cs` | Supported culture ids + `Link.Taom.<id>` naming. No colours |
| `Main/Features/MenuLinkColors/Hooks/Patch64_MenuLinkColors.cs` | The `set_ContextText` prefix |
| `Main/Adapters/EncyclopediaCultureLookup.cs` | href → object → culture StringId |
| `Main/Adapters/MenuBrushStyleProbe.cs` | Live-brush style existence check |
| `Main/_Module/GUI/Brushes/GameMenu.xml` | `GameMenu.InfoText` — the only place a colour hex appears |

## Known adjacent defects (not fixed here)

- `erebor`, `gondor` and `mordor` share identical `color`/`color2` in `taom_spcultures.xml`.
- `sturgia` and `battania` are renamed to Barding and Variag but never recoloured — they still
  carry vanilla Bannerlord blue and Battanian green.
- `taom_spkingdoms.xml:11` has `color="FF004D26"`, missing the `0x` prefix every sibling carries.
- TAOM's `GameMenu.xml` is a 573-line clone of Native's 634-line file — it predates newer vanilla
  brushes, which currently fall through to `DefaultBrush`. Same class as
  `rca-party-troop-thumbnail-stale-prefab-clone-2026-05-31.md`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/id-cheatsheet.md](../modding/id-cheatsheet.md)

<!-- backlinks-end -->
