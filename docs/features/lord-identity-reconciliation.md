# Lord identity: the two halves and how they drift

**Status:** landed 2026-08-29, uncommitted. Suite 7748 green. Reviewed by nine agents; the fourteen
surviving findings are all fixed, and the write-up is
[`docs/reviews/rca-lord-identity-2026-08-29.md`](../reviews/rca-lord-identity-2026-08-29.md).
In-game smoke and `/localize` owed.

A named lord is defined in two places, and nothing before 2026-08-29 checked that they agreed.

| Half | Files | Carries |
|---|---|---|
| Who he is | `Main/_Module/ModuleData/characters/lords.xml` (1184 `<NPCCharacter>`), `Main/_Module/ModuleData/lords.xslt` (396 templates over vanilla `SandBox/ModuleData/lords.xml`) | `name`, `is_female`, `race`, `age`, `culture`, `BodyProperties`, `beard_tags`, skills, equipment |
| What the player is told | `Main/_Module/ModuleData/characters/heroes.xml` (961 `<Hero>`), `Main/_Module/ModuleData/heroes.xslt` (399 templates over vanilla `SandBox/ModuleData/heroes.xml`) | `faction`, `father`, `mother`, `spouse`, and the encyclopedia `text` |

The encyclopedia shows the name and the biography side by side, so a player reads both at once.

## Load order, and what "wins" actually means

`Main/_Module/SubModule.xml`: line 96 `lords.xslt`, line 106 `heroes.xslt`, line 148
`characters/heroes.xml`, line 157 `characters/lords.xml`. 179 ids are defined in both lords files;
their names agree on 178, the one deliberate exception being `lord_WE9_l`, where `lords.xml` has
the fuller "Duinhir, Lord of Morthond".

**The plain XML wins per ATTRIBUTE, not per node.** `MBObjectManager.MergeElementAttributes` sets
only the attributes the later document actually declares:

```csharp
foreach (XAttribute item in element2.Attributes())
    element1.SetAttributeValue(item.Name, item.Value);
```

So an attribute `characters/lords.xml` omits survives from the stylesheet's output rather than
being cleared. Only `_replaceWhileMerging="true"` wipes the element first, and TAOM does not use
it. Seventeen ids take their `is_female` from `lords.xslt` because the plain XML never states one;
all seventeen read `false` on both sides today. Both new tests model the merge this way, because a
whole-node model reads the eighteenth one wrong.

**The stylesheet is also not fed `lords.xml` alone.** `GetMergedXmlForManaged` groups by the
`XmlName id`, and ten vanilla files share `id="NPCCharacters"` (`spnpccharacters`,
`spnpccharactertemplates`, `obsolete_characters` twice, `bandits`, `caravans`,
`education_character_templates`, `spspecialcharacters`, `spgenericcharacters`, `lords`), so
`lords.xslt` runs against everything accumulated before it. Templates that match on `@id` are
sibling-independent, and none of those nine files shares a single id with a TAOM lord (checked
2026-08-29, 1,132 vanilla ids against the full TAOM lord set, zero collisions), so
`LordFamilyTransformTests` feeding `lords.xml` in isolation gives the same per-id answer. Re-check
that if a future vanilla patch adds ids in those files.

## Two localization tiers that do not interoperate

| File | Keys | Registered in `taom_xslt_strings.xml` | Translated ×12 |
|---|---|---|---|
| `lords.xslt` names | 396 | all | yes |
| `heroes.xslt` biographies | 399 | all | yes |
| `characters/lords.xml` names | 1184 | the 179 overlap ids plus a few | partly |
| `characters/heroes.xml` biographies | 456 | **none** | **no, English-only** |

So an edit confined to `characters/*.xml` has zero locale ripple, and an edit to either stylesheet
touches a key twelve languages already carry. There is no English `Languages/` folder, so the
inline literal **is** the English text.

`taom_xslt_strings.xml` is generated from `lords.xslt`, not an independent authority, and it can
drift from the locales on its own: for `lord_1_30_2` and `lord_1_30_3` the registry said Mogra and
Snaga while eleven of twelve locales still rendered vanilla's Callinia and Synesios, because only
Polish was ever re-translated after `c9434b5a`. Treat "the registry and the locales are canonical"
as a per-id check, never a blanket rule.

## The trap: a template that does not strip inherits vanilla

`heroes.xslt` templates are `<xsl:copy>` plus `<xsl:apply-templates select="@*[...]"/>`. Any
attribute the filter does not exclude is **copied from vanilla**, and vanilla's value is about a
different character, because the id was reused. Nothing in the repo records what that value is, so
no test that reads markup can see the result.

Eight defects hid there until the transform was actually run:

| id | what the engine computed | why |
|---|---|---|
| `lord_4_24_1` | Gríma Wormtongue married to Éowyn, three children by her | the Éowyn biography was pasted onto Gríma's wife and vanilla's marriage survived |
| `lord_4_16` | Erkenbrand married to `lord_4_16_2`, his own bearded son | the template pointed the added spouse at the wrong id |
| `lord_4_16_1` | Mérthú with Erkenbrand as both father and husband | vanilla's `father` survived the added `spouse` |
| `lord_WE9_u` | Duilin married to Rosfin, whom the same template makes his mother | vanilla's `spouse` survived |
| `lord_1_52_1` | Anariel married to Hirluin, whom the same template makes her father | vanilla's `spouse` survived |
| `lord_4_22` | Wulf married to Sunnifa, whom the same template makes his mother | vanilla's `spouse` survived |
| `lord_4_9`, `lord_4_12`, `lord_4_121` | a female father and a male mother | `3c7f4e25` made Grimbold male and never turned vanilla's parent wiring over |
| `lord_B8_c` | father Rodarac, mother Maireas | the same, once Maireas became the male warlord his biography describes |

**Rule:** a template that assigns `spouse`, `father` or `mother` to a reused vanilla id must strip
every family attribute it does not itself set.

## What gates it now

| Test | Checks |
|---|---|
| `TAOM.Tests/Core/LordIdentityConsistencyTests.cs` | the biography names the lord it is attached to (comma epithets count, which is how the Nazgûl are named); it spells the name with its diacritics; declared parents are the right sex; declared marriages are opposite-sex and reciprocal; nobody is both father and husband; `lords.xslt` and the registry agree on every name and neither carries stray whitespace; every lord has a `<Hero>` entry or a named exclusion |
| `TAOM.Tests/Core/LordFamilyTransformTests.cs` | runs both stylesheets over the real vanilla documents with `XslCompiledTransform` and checks the graph the engine computes, which is the only way to see inherited vanilla values. Skips when the game is not installed |
| `TAOM.Tests/Core/LordNameAndSexConsistencyTests.cs` | the pre-existing registry-versus-literal and female-with-beard checks. Its `NPCCharacter` regex used to require `id` first and `name` second, which matched 584 of 1184 entries and skipped the whole Dol Guldur roster; it is attribute-order independent now |
| `TAOM.Tests/Infrastructure/Localization/AccentStrippedTranslationTests.cs` | no language row is the English with its diacritics stripped. Such a row can never be staged again, because the translator only returns rows where `cur_text == eng_text`. Shrink-only baseline of seven rows that already shipped that way |
| `LordIdentityConsistencyTests.cs`, added by the review | two spawning lords of one culture do not share a display name (24 baselined, cross-culture orc reuse is deliberate and out of scope); no parent is less than fourteen years older than the child (68 baselined in `impossible-age-links-baseline.txt`); `lords.xslt` and the registry agree on every name and neither carries stray whitespace |

Neither `validate_moduledata.py` nor `LanguageFileCoverageTests` can see any of this. The former has
no rule touching a lord's name or sex; the latter is a presence check by design, so renaming under
an existing key stays green, and so does a row holding a near-copy of the English.

**Still ungated:** nothing pins a translation to the character it describes. If a key rename moved
the wrong translation, every test here would stay green. The renames in this pass were verified by
reading the DE, FR and RU rows by hand.

## Placeholder names, and the register each culture uses

Five lords shipped with a placeholder name. Four were caught on 2026-08-29 and one more,
`lord_3_2` "Harad **Place Holder**", only after the sweep was widened to two-word forms. Search for
both spellings plus `RandomDude`, `dummy`, `TBD` and `not break`; the "Practice Dummy" and "Gear
Dummy" entries under `characters/npcs_*.xml` are legitimate training targets and stay.

**Do not invent a name.** TAOM already ships a register per culture in
`Main/_Module/ModuleData/taom_spcultures.xml`, and drawing from it keeps a new lord consistent with
the roster he joins:

| culture | pool | register |
|---|---|---|
| Harad, inland | `shaghana` | 30 male, 30 female, Arabic-flavoured. The existing Haradrim noblewomen already sit here |
| Harad, coastal and Umbar-allied | `harad_raiders` | circumflexed and Adûnaic-adapted (`Akhôr`, `Mûzan`, `Zûran`), matching the Black Númenórean connection those groups had |
| Rhûn and Khand | `rhun_raiders` | the only Eastern vocabulary the mod has. Khand has no pool of its own, and its roster is unmodified vanilla Battanian Celtic, which is leftover rather than a decision |
| Lothlórien, Mirkwood | per-culture pools | 50 Sindarin male and 50 female each. `-ion` is the masculine patronymic, `-iel` the feminine |

Tolkien gives almost nothing for either of the Southron and Eastern peoples: the only word stated to
be Southron is *mûmak*, and "Variag" is a Slavic borrowing from the Norse Varangian mercenaries that
a draft of Appendix F assigns to the speech of the Men of the East. The pools above are therefore
the closest thing to an authority, and they are internally consistent, which matters more.

Sindarin is right for both elf realms despite their Silvan population: by the late Third Age the
Silvan tongue survived only in place and person names, and Thranduil's line is Sindar out of
Doriath.

**Seven Haradrim carried Sindarin elf names** until 2026-08-29, five of them colliding with a real
elf: `lord_3_5` Haldir, `lord_3_16` Rúmil and `lord_3_17` Orophin were Lothlórien's three
marchwardens; `lord_3_22` was a second Duilin and `lord_3_3` a second Calemir. Renaming them from
the pools above cleared the collisions as a side effect.


## Deliberate exclusions

Named in `LordsDeliberatelyWithoutAHero`, with a second assertion that fails if an exclusion stops
being needed:

- **22 `lord_EW_*`** (2, 3, 4, 5, 7, 8, 10 to 13, 15 to 19, 21, 22, 24 to 28). An undifferentiated
  pool of Gondor-west lords, each authored under a `<!--Placeholder face-->` comment. Six of their
  neighbours got clans; nothing in the repo says which house these belong to, so assigning one
  would be authoring content rather than reconciling it.
- **`lord_WE9_l_1`**, a second lord named Duilin. `lord_WE9_u` is the one the registry and all
  twelve locales call Duilin and whose biography names him elder son of Duinhir.

## Regeneration hazard

`tools/complete_lords_xslt.py --apply` rewrites **only** `lords.xslt`; `characters/lords.xml` has
no regeneration guard and is never at risk. Two things to know before running it:

1. For an id where **vanilla says `is_female="true"` and TAOM wants male**, deleting the attribute
   is not enough: the merge falls through to `elif vanilla_val is not None` and copies `true` back.
   The id must also be in `GENDER_OVERRIDES` with value `None`. Where vanilla says `false` and TAOM
   wants female, an explicit `<xsl:attribute>` survives on its own. `lord_4_6`, `lord_WE8_c`,
   `lord_WE9_u` and `lord_WE9_u2` are in that table.
2. `ATTR_ORDER` does not contain `race`, so `--apply` **silently deletes all ten `race` attributes**
   from `lords.xslt`, Sauron's included. Only `ShippedUncapturableHeroesConfigTests` would notice,
   and only for Sauron.

## Save compatibility

`Hero.SetInitialValuesFromCharacter` (`Hero.cs:2239-2251`) copies `Name`, `Culture` and `IsFemale`
once, at hero creation. `Name` is a saved member, `IsFemale` is `[SaveableProperty(200)]`, and
`EncyclopediaText` is `[SaveableProperty(190)]`, populated from the `text` attribute at
`Hero.cs:1839`. **Nothing here reaches an existing save.** Verification needs a full application
restart and a fresh campaign, because Bannerlord globs and registers ModuleData at process launch.

## Related

- `docs/reviews/lessons/data-content-cultures.md` for the lessons drawn from this pass.
- Commit `a00086da` ("fix(lords): Pelendur was Icratia, and a woman") is the same defect class,
  fourteen instances earlier. It deferred three ids, all cleared here.
