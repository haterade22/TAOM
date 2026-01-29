using System;
using System.IO;
using System.Xml;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;

namespace TAOM.Features.HeroRace;

public class FaceGenIconService : IFaceGenIconService
{
    private readonly IModulePathAdapter _modulePathAdapter;
    private readonly IModLogger _logger;
    private XmlDocument _skinsDoc;
    private bool _loadAttempted;

    public FaceGenIconService(IModulePathAdapter modulePathAdapter, IModLogger logger)
    {
        _modulePathAdapter = modulePathAdapter;
        _logger = logger;
    }

    public string GetBeardName(int index, int race, int gender)
    {
        if (index < 0)
            return null;

        var doc = GetSkinsDocument();
        if (doc == null)
            return null;

        var raceNode = doc.SelectNodes("skins/race")?[race];
        if (raceNode == null)
            return null;

        var skinNode = GetSkinNode(raceNode, gender);
        if (skinNode == null)
            return null;

        var beardMeshes = skinNode.SelectNodes("beard_meshes/beard_mesh");
        if (beardMeshes == null || index >= beardMeshes.Count)
            return null;

        return beardMeshes[index].Attributes?["name"]?.Value;
    }

    public string GetHairName(int index, int race, int gender)
    {
        if (index < 0)
            return null;

        var doc = GetSkinsDocument();
        if (doc == null)
            return null;

        var raceNode = doc.SelectNodes("skins/race")?[race];
        if (raceNode == null)
            return null;

        var skinNode = GetSkinNode(raceNode, gender);
        if (skinNode == null)
            return null;

        var hairMeshes = skinNode.SelectNodes("hair_meshes/hair_mesh");
        if (hairMeshes == null || index >= hairMeshes.Count)
            return null;

        return hairMeshes[index].Attributes?["name"]?.Value;
    }

    private XmlNode GetSkinNode(XmlNode raceNode, int gender)
    {
        var skins = raceNode.SelectNodes("skin");
        if (skins == null)
            return null;

        foreach (XmlNode skin in skins)
        {
            var genderAttr = skin.Attributes?["gender"]?.Value;
            if (genderAttr != null && int.TryParse(genderAttr, out var g) && g == gender)
                return skin;
        }

        return null;
    }

    private XmlDocument GetSkinsDocument()
    {
        if (_loadAttempted)
            return _skinsDoc;

        _loadAttempted = true;

        try
        {
            var modulePath = _modulePathAdapter.GetModuleFullPath("LOTRLOME_Armory");
            var skinsPath = Path.Combine(modulePath, "ModuleData", "skins.xml");
            _skinsDoc = new XmlDocument();
            _skinsDoc.Load(skinsPath);
        }
        catch (Exception ex)
        {
            _logger.LogError($"FaceGenIconService: Failed to load skins.xml: {ex.Message}");
            _skinsDoc = null;
        }

        return _skinsDoc;
    }
}
