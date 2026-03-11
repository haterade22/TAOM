using System.Collections.Generic;
using System.Linq;
using TAOM.Features.FactionMap.Models;

namespace TAOM.Features.FactionMap;

public class LandmarkService : ILandmarkService
{
    private readonly List<LandmarkDef> _landmarks;

    public LandmarkService()
    {
        _landmarks = BuildLandmarks();
    }

    public IReadOnlyList<LandmarkDef> GetAllLandmarks() => _landmarks;

    public IEnumerable<LandmarkDef> GetCapitals() => _landmarks.Where(l => l.Type == LandmarkType.Capital);

    public IEnumerable<LandmarkDef> GetByFaction(int factionId) => _landmarks.Where(l => l.FactionId == factionId);

    private static List<LandmarkDef> BuildLandmarks() => new List<LandmarkDef>
    {
        // Capitals
        new LandmarkDef { Id = "minas_tirith", Name = "Minas Tirith", Description = "The White Tower - Capital of Gondor", Type = LandmarkType.Capital, X = 580, Y = 680, FactionId = 11 },
        new LandmarkDef { Id = "edoras", Name = "Edoras", Description = "Meduseld - The Golden Hall", Type = LandmarkType.Capital, X = 530, Y = 530, FactionId = 9 },
        new LandmarkDef { Id = "barad_dur", Name = "Barad-dur", Description = "The Dark Tower of Sauron", Type = LandmarkType.Capital, X = 920, Y = 620, FactionId = 8 },
        new LandmarkDef { Id = "thranduils_halls", Name = "Thranduil's Halls", Description = "The Woodland Realm of the Elven King", Type = LandmarkType.Capital, X = 920, Y = 280, FactionId = 4 },
        new LandmarkDef { Id = "dol_guldur_fortress", Name = "Dol Guldur", Description = "The Fortress of the Necromancer", Type = LandmarkType.Capital, X = 870, Y = 510, FactionId = 17 },
        new LandmarkDef { Id = "erebor", Name = "Erebor", Description = "The Lonely Mountain - Kingdom Under the Mountain", Type = LandmarkType.Capital, X = 1150, Y = 280, FactionId = 12 },
        new LandmarkDef { Id = "dale_city", Name = "Dale", Description = "The Rebuilt Trading City", Type = LandmarkType.Capital, X = 1140, Y = 320, FactionId = 16 },
        new LandmarkDef { Id = "rivendell", Name = "Rivendell", Description = "Imladris - The Last Homely House", Type = LandmarkType.Capital, X = 680, Y = 280, FactionId = 13 },
        new LandmarkDef { Id = "grey_havens", Name = "Grey Havens", Description = "Mithlond - Gateway to the Undying Lands", Type = LandmarkType.Capital, X = 180, Y = 350, FactionId = 14 },
        new LandmarkDef { Id = "mount_gundabad", Name = "Mount Gundabad", Description = "The Ancient Orc Fortress", Type = LandmarkType.Capital, X = 620, Y = 120, FactionId = 18 },
        new LandmarkDef { Id = "harad_capital", Name = "Harad", Description = "Capital of the Southlands", Type = LandmarkType.Capital, X = 780, Y = 1050, FactionId = 5 },
        new LandmarkDef { Id = "umbar_port", Name = "Umbar", Description = "The Corsair City", Type = LandmarkType.Capital, X = 400, Y = 900, FactionId = 19 },
        new LandmarkDef { Id = "khand_capital", Name = "Khand", Description = "Land of the Variags", Type = LandmarkType.Capital, X = 1300, Y = 880, FactionId = 10 },

        // Cities
        new LandmarkDef { Id = "dol_amroth", Name = "Dol Amroth", Description = "Harbor of the Swan Knights", Type = LandmarkType.City, X = 420, Y = 780, FactionId = 11 },
        new LandmarkDef { Id = "lake_town", Name = "Lake-town", Description = "Esgaroth - City on the Lake", Type = LandmarkType.City, X = 1100, Y = 350, FactionId = 3 },
        new LandmarkDef { Id = "bree", Name = "Bree", Description = "Crossroads of the Ways", Type = LandmarkType.City, X = 340, Y = 300, FactionId = 0 },
        new LandmarkDef { Id = "hobbiton", Name = "Hobbiton", Description = "In the Shire", Type = LandmarkType.City, X = 260, Y = 290, FactionId = 0 },

        // Fortresses
        new LandmarkDef { Id = "helms_deep", Name = "Helm's Deep", Description = "The Hornburg - Impregnable Fortress", Type = LandmarkType.Fortress, X = 480, Y = 550, FactionId = 9 },
        new LandmarkDef { Id = "minas_morgul", Name = "Minas Morgul", Description = "Fortress of the Nazgul", Type = LandmarkType.Fortress, X = 750, Y = 650, FactionId = 8 },
        new LandmarkDef { Id = "isengard", Name = "Isengard", Description = "Orthanc - Saruman's Tower", Type = LandmarkType.Fortress, X = 450, Y = 480, FactionId = 2 },
        new LandmarkDef { Id = "cirith_ungol", Name = "Cirith Ungol", Description = "Pass of the Spider", Type = LandmarkType.Fortress, X = 760, Y = 680, FactionId = 8 },

        // Ruins
        new LandmarkDef { Id = "osgiliath", Name = "Osgiliath", Description = "The Old Capital - In Ruins", Type = LandmarkType.Ruin, X = 620, Y = 660, FactionId = 11 },
        new LandmarkDef { Id = "moria", Name = "Moria", Description = "Khazad-dum - The Lost Dwarven Kingdom", Type = LandmarkType.Ruin, X = 540, Y = 430, FactionId = 2 },
        new LandmarkDef { Id = "weathertop", Name = "Weathertop", Description = "Amon Sul - Ruins of the Old Watchtower", Type = LandmarkType.Ruin, X = 380, Y = 280, FactionId = 0 },
        new LandmarkDef { Id = "amon_hen", Name = "Amon Hen", Description = "Hill of Sight", Type = LandmarkType.Ruin, X = 630, Y = 520, FactionId = 0 },

        // Landmarks
        new LandmarkDef { Id = "mount_doom", Name = "Mount Doom", Description = "Orodruin - Where the One Ring was Forged", Type = LandmarkType.Landmark, X = 880, Y = 640, FactionId = 8 },
        new LandmarkDef { Id = "lorien", Name = "Lothlorien", Description = "The Golden Wood", Type = LandmarkType.Landmark, X = 700, Y = 400, FactionId = 0 },
        new LandmarkDef { Id = "fangorn", Name = "Fangorn", Description = "The Ancient Forest of the Ents", Type = LandmarkType.Landmark, X = 560, Y = 450, FactionId = 0 },
        new LandmarkDef { Id = "dead_marshes", Name = "Dead Marshes", Description = "Ghostly Marshes Before Mordor", Type = LandmarkType.Landmark, X = 720, Y = 580, FactionId = 0 },
        new LandmarkDef { Id = "rhun_sea", Name = "Sea of Rhun", Description = "The Inland Sea of the East", Type = LandmarkType.Landmark, X = 1400, Y = 450, FactionId = 3 },
        new LandmarkDef { Id = "nurnen", Name = "Sea of Nurnen", Description = "Inland Sea in Mordor", Type = LandmarkType.Landmark, X = 1000, Y = 800, FactionId = 8 },

        // Port
        new LandmarkDef { Id = "pelargir", Name = "Pelargir", Description = "Great Harbor of Gondor", Type = LandmarkType.Port, X = 520, Y = 780, FactionId = 11 },
    };
}
