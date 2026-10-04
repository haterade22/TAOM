using System;
using System.IO;

namespace TAOM.Tests.Features.XmlMerge;

/// <summary>
/// A small module-XML fixture set for the engine-equivalence tests (plan 042): one XSD the engine's
/// XmlResource.ReadXsdFileAndExtractInformation reads (Things keyed by @id), three files that exercise a same-key merge,
/// a new key, a comment, an xsi attribute and _replaceWhileMerging, and two stylesheets (one drops Thing b, one marks
/// every Thing). Written to a fresh temp folder; the caller deletes it.
/// </summary>
internal sealed class XmlMergeFixtures
{
    public const string Drop =
        "<xsl:stylesheet version=\"1.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\">" +
        "<xsl:output omit-xml-declaration=\"yes\"/>" +
        "<xsl:template match=\"@*|node()\"><xsl:copy><xsl:apply-templates select=\"@*|node()\"/></xsl:copy></xsl:template>" +
        "<xsl:template match=\"Thing[@id='b']\"/>" +
        "</xsl:stylesheet>";

    public const string Touch =
        "<xsl:stylesheet version=\"1.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\">" +
        "<xsl:output omit-xml-declaration=\"yes\"/>" +
        "<xsl:template match=\"@*|node()\"><xsl:copy><xsl:apply-templates select=\"@*|node()\"/></xsl:copy></xsl:template>" +
        "<xsl:template match=\"Thing\"><xsl:copy><xsl:apply-templates select=\"@*\"/>" +
        "<xsl:attribute name=\"touched\">yes</xsl:attribute><xsl:apply-templates select=\"node()\"/></xsl:copy></xsl:template>" +
        "</xsl:stylesheet>";

    // The XML declaration must stay the first line: nothing may precede it.
    private const string ThingsXsd =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
        "<xs:schema xmlns:xs=\"http://www.w3.org/2001/XMLSchema\">\r\n" +
        "  <xs:element name=\"Things\">\r\n" +
        "    <xs:complexType>\r\n" +
        "      <xs:sequence>\r\n" +
        "        <xs:element name=\"Thing\" minOccurs=\"0\" maxOccurs=\"unbounded\">\r\n" +
        "          <xs:complexType>\r\n" +
        "            <xs:sequence>\r\n" +
        "              <xs:element name=\"Part\" minOccurs=\"0\" maxOccurs=\"unbounded\">\r\n" +
        "                <xs:complexType>\r\n" +
        "                  <xs:attribute name=\"name\" type=\"xs:string\" use=\"optional\" />\r\n" +
        "                  <xs:attribute name=\"value\" type=\"xs:string\" use=\"optional\" />\r\n" +
        "                </xs:complexType>\r\n" +
        "              </xs:element>\r\n" +
        "            </xs:sequence>\r\n" +
        "            <xs:attribute name=\"id\" type=\"xs:string\" use=\"required\" />\r\n" +
        "            <xs:attribute name=\"label\" type=\"xs:string\" use=\"optional\" />\r\n" +
        "          </xs:complexType>\r\n" +
        "        </xs:element>\r\n" +
        "      </xs:sequence>\r\n" +
        "    </xs:complexType>\r\n" +
        "    <xs:unique name=\"UniqueThingId\">\r\n" +
        "      <xs:selector xpath=\"Thing\" />\r\n" +
        "      <xs:field xpath=\"@id\" />\r\n" +
        "    </xs:unique>\r\n" +
        "  </xs:element>\r\n" +
        "</xs:schema>\r\n";

    private XmlMergeFixtures(string folder)
    {
        Folder = folder;
        Xsd = Write("Things.xsd", ThingsXsd);
        A = Write("a.xml", "<Things><Thing id=\"a\" label=\"first\"><Part name=\"p1\" value=\"1\"/></Thing><Thing id=\"b\"/></Things>");
        B = Write("b.xml",
            "<Things xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:noNamespaceSchemaLocation=\"Things.xsd\">" +
            "<!-- second file --><Thing id=\"a\" label=\"second\"><Part name=\"p2\"/></Thing><Thing id=\"c\"/></Things>");
        C = Write("c.xml", "<Things><Thing id=\"a\" _replaceWhileMerging=\"true\" label=\"replaced\"><Part name=\"p9\"/></Thing></Things>");
        DropXslt = Write("drop.xslt", Drop);
        TouchXslt = Write("touch.xslt", Touch);
        Missing = Path.Combine(folder, "missing.xml");
    }

    public string Folder { get; }
    public string Xsd { get; }
    public string A { get; }
    public string B { get; }
    public string C { get; }
    public string DropXslt { get; }
    public string TouchXslt { get; }
    public string Missing { get; }

    public static XmlMergeFixtures Create()
    {
        var folder = Path.Combine(Path.GetTempPath(), "taom-xmlmerge-fixtures-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return new XmlMergeFixtures(folder);
    }

    public void Delete()
    {
        if (!Directory.Exists(Folder))
            return;
        try
        {
            Directory.Delete(Folder, recursive: true);
        }
        catch (IOException)
        {
            // The engine's ApplyXslt never disposes the StreamReader it opens on a stylesheet, so the handle stays
            // open until that reader is finalized. Collect it, then try once more; a leftover temp folder is harmless.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            try
            {
                Directory.Delete(Folder, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private string Write(string name, string text)
    {
        var path = Path.Combine(Folder, name);
        File.WriteAllText(path, text);
        return path;
    }
}
