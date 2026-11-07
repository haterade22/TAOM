"""Read the animation take names from a binary FBX file, without Blender.

The Modding Kit names each skeletal animation it imports after the file's take (its AnimationStack), and a module
may hold one name only once. Blender's exporter names a single global take after the scene, so six exports from
factory settings all carry "Scene" and the Kit refuses every import after the first (#1081, 2026-11-06).

A binary FBX stores an object's name as one string property, "<name>\\x00\\x01<class>", after a type byte 'S' and a
little-endian uint32 length. That suffix is the FBX object-name convention, so it is matched without walking the node
tree; a name over 1019 bytes raises rather than vanishing.
"""
import sys

_CLASS = b"\x00\x01AnimStack"


def take_names(raw):
    """The AnimStack names in the bytes of a binary FBX file, in file order."""
    names = []
    at = raw.find(_CLASS)
    while at != -1:
        for length in range(0, min(at, 1024) - 4):
            start = at - length
            if raw[start - 5:start - 4] == b"S" and \
                    int.from_bytes(raw[start - 4:start], "little") == length + len(_CLASS):
                names.append(raw[start:at].decode("utf-8"))
                break
        else:
            raise ValueError("an AnimStack name at byte %d is longer than this reader reads (1019 bytes)" % at)
        at = raw.find(_CLASS, at + 1)
    return names


def file_take_names(path):
    with open(path, "rb") as fh:
        return take_names(fh.read())


if __name__ == "__main__":
    for p in sys.argv[1:]:
        print(p, file_take_names(p))
