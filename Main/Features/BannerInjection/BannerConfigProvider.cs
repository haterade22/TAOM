using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;

namespace TAOM.Features.BannerInjection;

public class BannerConfigProvider : IBannerConfigProvider
{
    private static readonly XNamespace Xsl = "http://www.w3.org/1999/XSL/Transform";

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;

    public BannerConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public Dictionary<string, string> GetKingdomBannerKeys()
    {
        var result = new Dictionary<string, string>();
        ParseBannerKeys(Path.Combine(_pathService.ModuleDataPath, "taom_spkingdoms.xml"),
            "Kingdom", result);
        ParseXsltBannerKeys(Path.Combine(_pathService.ModuleDataPath, "spkingdoms.xslt"),
            "Kingdom", result);
        return result;
    }

    public Dictionary<string, string> GetClanBannerKeys()
    {
        var result = new Dictionary<string, string>();
        ParseBannerKeys(Path.Combine(_pathService.ModuleDataPath, "characters", "clans.xml"),
            "Faction", result);
        ParseXsltBannerKeys(Path.Combine(_pathService.ModuleDataPath, "spclans.xslt"),
            "Faction", result);
        return result;
    }

    private void ParseBannerKeys(string xmlPath, string elementName, Dictionary<string, string> result)
    {
        if (!File.Exists(xmlPath))
        {
            _logger.LogWarning($"BannerConfigProvider: File not found: {xmlPath}");
            return;
        }

        try
        {
            var doc = XDocument.Load(xmlPath);
            foreach (var element in doc.Descendants(elementName))
            {
                var id = element.Attribute("id")?.Value;
                var bannerKey = element.Attribute("banner_key")?.Value;

                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(bannerKey))
                {
                    result[id] = bannerKey;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"BannerConfigProvider: Failed to parse {xmlPath}: {ex.Message}");
        }
    }

    private void ParseXsltBannerKeys(string xsltPath, string elementName, Dictionary<string, string> result)
    {
        if (!File.Exists(xsltPath))
        {
            _logger.LogWarning($"BannerConfigProvider: File not found: {xsltPath}");
            return;
        }

        try
        {
            var doc = XDocument.Load(xsltPath);
            var idPattern = new Regex(elementName + @"\[@id='([^']+)'\]");

            foreach (var template in doc.Descendants(Xsl + "template"))
            {
                var match = template.Attribute("match")?.Value;
                if (match == null) continue;

                var idMatch = idPattern.Match(match);
                if (!idMatch.Success) continue;

                var id = idMatch.Groups[1].Value;

                foreach (var attr in template.Descendants(Xsl + "attribute"))
                {
                    if (attr.Attribute("name")?.Value == "banner_key")
                    {
                        var bannerKey = attr.Value;
                        if (!string.IsNullOrEmpty(bannerKey))
                        {
                            result[id] = bannerKey;
                        }
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"BannerConfigProvider: Failed to parse {xsltPath}: {ex.Message}");
        }
    }
}
