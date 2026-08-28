"""Sync the English name fallbacks in characters/lords.xml to taom_xslt_strings.xml.

A lord's name is authored as name="{=aom_lord_<id>_name}English". The inline literal is
a fallback: the 12 language files under Languages/ supply the text for every other locale,
and there is no English folder, so English is the only locale that ever renders the literal.
When a lord is renamed in the registry and the language files but not in lords.xml, English
players keep seeing the old name while everyone else sees the new one.

Reads the registry, rewrites only mismatched literals, and leaves everything else byte-identical.

    python tools/oneoff/sync_lord_name_fallbacks.py            # dry run
    python tools/oneoff/sync_lord_name_fallbacks.py --apply
"""
import re
import sys

LORDS = 'Main/_Module/ModuleData/characters/lords.xml'
STRINGS = 'Main/_Module/ModuleData/taom_xslt_strings.xml'

# Rows deliberately not synced. The registry is not always the better text.
SKIP = {
    # lords.xml carries the fuller "Duinhir, Lord of Morthond"; the registry has the bare
    # given name. Syncing down would drop the title from the only locale that shows it.
    'aom_lord_WE9_l_name',
}


def read(path):
    return open(path, 'rb').read().decode('utf-8')


def main():
    apply_ = '--apply' in sys.argv
    strings = read(STRINGS)
    registry = {
        m.group(1): m.group(2)
        for m in re.finditer(r'<string id="(aom_lord_[^"]+_name)" text="\{=\1\}([^"]*)"', strings)
    }

    text = read(LORDS)
    changes = []

    def repl(m):
        cid, key, inline = m.group(1), m.group(2), m.group(3)
        want = registry.get(key)
        if want is None or want == inline or key in SKIP:
            return m.group(0)
        changes.append((cid, inline, want))
        return '<NPCCharacter id="%s" name="{=%s}%s"' % (cid, key, want)

    out = re.sub(r'<NPCCharacter id="([^"]+)" name="\{=(aom_lord_[^}]+)\}([^"]*)"', repl, text)

    for cid, was, now in changes:
        print('%-14s %-26s -> %s' % (cid, was, now))
    skipped = [k for k in SKIP if k in registry]
    for k in skipped:
        print('skipped %s (see SKIP)' % k)
    print('\n%d name(s) %s' % (len(changes), 'rewritten' if apply_ else 'would change'))

    if not changes:
        return
    if apply_:
        open(LORDS, 'wb').write(out.encode('utf-8'))
        print('written to %s' % LORDS)
    else:
        print('dry run; pass --apply to write')


if __name__ == '__main__':
    main()
