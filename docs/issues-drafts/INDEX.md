# Lords Skills+Traits — GitHub Issue Drafts

One draft per TAOM culture. To create each issue:

```bash
for f in docs/issues-drafts/lords-skills-*.md; do
  title=$(head -1 "$f" | sed 's/^# //')
  gh issue create --title "$title" --body-file "$f" --label 'enhancement' --label 'lords'
done
```

Or one-at-a-time:

- **[gondor](lords-skills-gondor.md)** (Gondor, culture_id=`gondor`, 118 NPCs)
  - Title: `feat(lords-skills): Gondor — lore-driven skills + traits for 118 adult NPCs`
- **[rohan](lords-skills-rohan.md)** (Rohan, culture_id=`vlandia`, 92 NPCs)
  - Title: `feat(lords-skills): Rohan — lore-driven skills + traits for 92 adult NPCs`
- **[erebor](lords-skills-erebor.md)** (Erebor (Dwarves of the Lonely Mountain), culture_id=`erebor`, 30 NPCs)
  - Title: `feat(lords-skills): Erebor (Dwarves of the Lonely Mountain) — lore-driven skills + traits for 30 adult NPCs`
- **[dale](lords-skills-dale.md)** (Dale (Bardings), culture_id=`sturgia`, 82 NPCs)
  - Title: `feat(lords-skills): Dale (Bardings) — lore-driven skills + traits for 82 adult NPCs`
- **[mirkwood](lords-skills-mirkwood.md)** (Mirkwood (Woodland Realm), culture_id=`mirkwood`, 29 NPCs)
  - Title: `feat(lords-skills): Mirkwood (Woodland Realm) — lore-driven skills + traits for 29 adult NPCs`
- **[rivendell](lords-skills-rivendell.md)** (Rivendell (Imladris), culture_id=`rivendell`, 7 NPCs)
  - Title: `feat(lords-skills): Rivendell (Imladris) — lore-driven skills + traits for 7 adult NPCs`
- **[lothlorien](lords-skills-lothlorien.md)** (Lothlórien, culture_id=`lothlorien`, 3 NPCs)
  - Title: `feat(lords-skills): Lothlórien — lore-driven skills + traits for 3 adult NPCs`
- **[mordor](lords-skills-mordor.md)** (Mordor, culture_id=`mordor`, 97 NPCs)
  - Title: `feat(lords-skills): Mordor — lore-driven skills + traits for 97 adult NPCs`
- **[dolguldur](lords-skills-dolguldur.md)** (Dol Guldur, culture_id=`dolguldur`, 59 NPCs)
  - Title: `feat(lords-skills): Dol Guldur — lore-driven skills + traits for 59 adult NPCs`
- **[gundabad](lords-skills-gundabad.md)** (Mount Gundabad, culture_id=`gundabad`, 50 NPCs)
  - Title: `feat(lords-skills): Mount Gundabad — lore-driven skills + traits for 50 adult NPCs`
- **[isengard](lords-skills-isengard.md)** (Isengard (Saruman), culture_id=`isengard`, 34 NPCs)
  - Title: `feat(lords-skills): Isengard (Saruman) — lore-driven skills + traits for 34 adult NPCs`
- **[dunland](lords-skills-dunland.md)** (Dunland (Hillmen / Saruman's auxiliaries), culture_id=`empire`, 68 NPCs)
  - Title: `feat(lords-skills): Dunland (Hillmen / Saruman's auxiliaries) — lore-driven skills + traits for 68 adult NPCs`
- **[harad](lords-skills-harad.md)** (Harad (Haradrim Southrons), culture_id=`aserai`, 73 NPCs)
  - Title: `feat(lords-skills): Harad (Haradrim Southrons) — lore-driven skills + traits for 73 adult NPCs`
- **[khand](lords-skills-khand.md)** (Khand (Variags), culture_id=`battania`, 56 NPCs)
  - Title: `feat(lords-skills): Khand (Variags) — lore-driven skills + traits for 56 adult NPCs`
- **[easterling](lords-skills-easterling.md)** (Easterlings of Rhûn, culture_id=`khuzait`, 71 NPCs)
  - Title: `feat(lords-skills): Easterlings of Rhûn — lore-driven skills + traits for 71 adult NPCs`
- **[umbar](lords-skills-umbar.md)** (Umbar (Corsairs), culture_id=`umbar`, 10 NPCs)
  - Title: `feat(lords-skills): Umbar (Corsairs) — lore-driven skills + traits for 10 adult NPCs`
- **[shaghana](lords-skills-shaghana.md)** (Shaghana, culture_id=`shaghana`, 9 NPCs)
  - Title: `feat(lords-skills): Shaghana — lore-driven skills + traits for 9 adult NPCs`
- **[abanissa](lords-skills-abanissa.md)** (Abanissa, culture_id=`abanissa`, 8 NPCs)
  - Title: `feat(lords-skills): Abanissa — lore-driven skills + traits for 8 adult NPCs`

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/lord-skills-authoring.md](../ai-includes/lord-skills-authoring.md)

<!-- backlinks-end -->
