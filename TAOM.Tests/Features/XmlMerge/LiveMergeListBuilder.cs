using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace TAOM.Tests.Features.XmlMerge;

/// <summary>
/// Builds, for one game type and module order, exactly the lists the engine's <c>MBObjectManager.GetMergedXmlForManaged</c>
/// builds for each XML type (v1.5.3, MBObjectManager.cs:873-914; module paths from ModuleHelper): the module's own
/// <c>ModuleData/XmlSchemas/&lt;id&gt;.xsd</c> when it exists, else the game's <c>XmlSchemas/&lt;id&gt;.xsd</c>; a file
/// node's XSLT probed as <c>.xsl</c> then <c>.xslt</c>; a folder node's files in the order
/// <c>DirectoryInfo.GetFiles("*.xml")</c> returns them, joined as <c>folder + "/" + name</c>, with the engine's
/// <c>Replace(".xml", ...)</c> calls; and a <c>("", "")</c> entry for a node that names neither a file nor a folder.
/// XmlNodes are read as <c>XmlResource.GetXmlListAndApply</c> reads them (XmlResource.cs:258-326): <c>XmlName</c>'s
/// <c>id</c> and <c>path</c>, and the <c>value</c> of every element child of <c>IncludedGameTypes</c>.
/// </summary>
internal static class LiveMergeListBuilder
{
    internal const string ModulesVariable = "TAOM_XMLMERGE_MODULES";

    internal const string DefaultModuleOrder =
        "TAOM.Dependencies;Native;SandBoxCore;CustomBattle;SandBox;StoryMode;BirthAndDeath;FastMode;LOTRLOME_Armory;TAOM_Map;TAOM";

    /// <summary>Default-order modules the gate lets be absent: the v1.5.3 install has no FastMode folder.</summary>
    internal static readonly IReadOnlyList<string> OptionalDefaultModules = new[] { "FastMode" };

    /// <summary>The modules a run covers, in load order, and whether the environment asked for them by name.</summary>
    internal sealed class ModuleSelection
    {
        public ModuleSelection(IReadOnlyList<string> modules, bool explicitOrder)
        {
            Modules = modules;
            Explicit = explicitOrder;
        }

        public IReadOnlyList<string> Modules { get; }

        public bool Explicit { get; }
    }

    internal sealed class MergeList
    {
        public MergeList(string id)
        {
            Id = id;
        }

        public string Id { get; }

        public List<Tuple<string, string>> Entries { get; } = new List<Tuple<string, string>>();

        public List<string> Xslts { get; } = new List<string>();

        public int Files => Entries.Count(e => e.Item1 != "");

        public int XsltCount => Xslts.Skip(1).Count(x => x != "");
    }

    internal sealed class Result
    {
        /// <summary>The requested modules that exist, in order.</summary>
        public List<string> Modules { get; } = new List<string>();

        /// <summary>The requested modules with no <c>SubModule.xml</c> under the game folder.</summary>
        public List<string> Missing { get; } = new List<string>();

        /// <summary>One list per type id kept for the game type, in first-seen order.</summary>
        public List<MergeList> Lists { get; } = new List<MergeList>();

        /// <summary>Every default XSD that exists, for every XmlNode of every module, as the engine reads them at startup.</summary>
        public List<string> DefaultXsdPaths { get; } = new List<string>();
    }

    /// <summary>
    /// The lists of <paramref name="modules"/>, in that order. A module with no <c>SubModule.xml</c> adds nothing and is
    /// recorded in <see cref="Result.Missing"/>, which the gate fails (LiveGateRules.CheckModules) unless it is the default
    /// order's optional FastMode.
    /// </summary>
    public static Result Build(string gameDir, string gameType, IReadOnlyList<string> modules)
    {
        string game = gameDir.Replace('\\', '/').TrimEnd('/') + "/";
        var result = new Result();
        var byId = new Dictionary<string, MergeList>(StringComparer.Ordinal);

        foreach (var module in modules)
        {
            string moduleRoot = game + "Modules/" + module + "/";
            string subModule = moduleRoot + "SubModule.xml";
            if (!File.Exists(subModule))
            {
                result.Missing.Add(module);
                continue;
            }
            result.Modules.Add(module);

            var document = new XmlDocument();
            document.Load(subModule);
            var nodes = document.SelectSingleNode("Module")?.SelectNodes("Xmls/XmlNode");
            if (nodes == null)
                continue;

            foreach (XmlNode node in nodes)
            {
                var name = node.SelectSingleNode("XmlName");
                string? id = name?.Attributes?["id"]?.InnerText;
                string? path = name?.Attributes?["path"]?.InnerText;
                if (id == null || path == null)
                    continue;

                string defaultXsd = game + "XmlSchemas/" + id + ".xsd";
                if (File.Exists(defaultXsd) && !result.DefaultXsdPaths.Contains(defaultXsd))
                    result.DefaultXsdPaths.Add(defaultXsd);

                var gameTypes = new List<string>();
                var included = node.SelectSingleNode("IncludedGameTypes");
                if (included != null)
                {
                    foreach (XmlNode child in included.ChildNodes)
                    {
                        if (child.NodeType == XmlNodeType.Element && child.Attributes?["value"] != null)
                            gameTypes.Add(child.Attributes["value"]!.InnerText);
                    }
                }
                if (gameTypes.Count != 0 && !gameTypes.Contains(gameType))
                    continue;

                if (!byId.TryGetValue(id, out var list))
                {
                    list = new MergeList(id);
                    byId[id] = list;
                    result.Lists.Add(list);
                }
                AddNode(list, moduleRoot, id, path, defaultXsd);
            }
        }
        return result;
    }

    /// <summary>The run's module order: <see cref="ModulesVariable"/> when the environment sets it, else the default.</summary>
    public static ModuleSelection Select() => Select(Environment.GetEnvironmentVariable(ModulesVariable));

    /// <summary>
    /// <paramref name="fromEnvironment"/> (semicolon-separated, trimmed, a segment that is empty or only white space
    /// dropped) is an explicit order when it holds anything but white space; otherwise the default order, which is not
    /// explicit. The gate holds an explicit order to every module it names, so a blank segment must not become a name.
    /// </summary>
    public static ModuleSelection Select(string? fromEnvironment)
    {
        bool explicitOrder = !string.IsNullOrWhiteSpace(fromEnvironment);
        string order = explicitOrder ? fromEnvironment! : DefaultModuleOrder;
        var modules = order.Split(';').Select(m => m.Trim()).Where(m => m.Length != 0).ToList();
        return new ModuleSelection(modules, explicitOrder);
    }

    // GetMergedXmlForManaged's body for one XmlInformation record.
    private static void AddNode(MergeList list, string moduleRoot, string id, string name, string defaultXsd)
    {
        string xsd = moduleRoot + "ModuleData/XmlSchemas/" + id + ".xsd";
        if (!File.Exists(xsd))
            xsd = defaultXsd;

        string xmlPath = moduleRoot + "ModuleData/" + name + ".xml";
        if (File.Exists(xmlPath))
        {
            list.Entries.Add(Tuple.Create(xmlPath, xsd));
            AddXslt(list, moduleRoot + "ModuleData/" + name + ".xsl");
            return;
        }

        string folder = xmlPath.Replace(".xml", "");
        if (Directory.Exists(folder))
        {
            foreach (var file in new DirectoryInfo(folder).GetFiles("*.xml"))
            {
                string filePath = folder + "/" + file.Name;
                list.Entries.Add(Tuple.Create(filePath, xsd));
                AddXslt(list, filePath.Replace(".xml", ".xsl"));
            }
        }
        else
        {
            list.Entries.Add(Tuple.Create("", ""));
            AddXslt(list, moduleRoot + "ModuleData/" + name + ".xsl");
        }
    }

    // HandleXsltList (MBObjectManager.cs:945-960).
    private static void AddXslt(MergeList list, string xslPath)
    {
        if (File.Exists(xslPath))
            list.Xslts.Add(xslPath);
        else if (File.Exists(xslPath + "t"))
            list.Xslts.Add(xslPath + "t");
        else
            list.Xslts.Add("");
    }
}
