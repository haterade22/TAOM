using System.Collections.Generic;
using TAOM.Features.Realms.Domain.World;

namespace TAOM.Tests.Features.Realms;

/// <summary>A small made-up map: realm "north" with three towns, a castle and villages; realm "south" with one town.</summary>
internal static class TestWorld
{
    public static FiefInfo Town(string id, string kingdom, string owner, float x, float y = 0) =>
        new(id, id + " name", FiefKind.Town, kingdom, owner, null, x, y, "culture_" + kingdom);

    public static FiefInfo Castle(string id, string kingdom, string owner, float x, float y = 0) =>
        new(id, id + " name", FiefKind.Castle, kingdom, owner, null, x, y, "culture_" + kingdom);

    public static FiefInfo Village(string id, string bound, string owner) =>
        new(id, id + " name", FiefKind.Village, null, owner, bound, 0, 0, null);

    public static WorldSnapshot Build() => new(
        new List<KingdomInfo>
        {
            new("north", "North", "culture_north", "king_n"),
            new("south", "South", "culture_south", "king_s"),
        },
        new List<FiefInfo>
        {
            Town("town_a", "north", "king_n", 0),
            Town("town_b", "north", "lord_b", 10),
            Town("town_c", "north", "lord_c", 100),
            Castle("castle_a", "north", "lord_b", 12),
            Village("village_a1", "town_a", "king_n"),
            Village("village_ca", "castle_a", "lord_b"),
            Town("town_s", "south", "king_s", 500),
        });
}
