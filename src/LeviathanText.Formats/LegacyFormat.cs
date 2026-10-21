using System.Xml.Linq;

namespace LeviathanText.Formats;

/// <summary>Bounded format detection for legacy OCR imports (PAGE, ALTO 4, hOCR).</summary>
public enum LegacyFormat { Page, Alto, Hocr, Abbyy }

public static class LegacyFormats
{
    public static LegacyFormat Detect(string xml)
    {
        XDocument doc;
        try { doc = XDocument.Parse(xml, LoadOptions.None); }
        catch (Exception ex) { throw new InvalidDataException("Document is not well-formed XML.", ex); }
        if (doc.DocumentType is not null)
            throw new InvalidDataException("DOCTYPE/external entities are not accepted in legacy imports.");
        var root = doc.Root ?? throw new InvalidDataException("Missing document root.");
        return root.Name.LocalName switch
        {
            "PcGts" => LegacyFormat.Page,
            "alto" => LegacyFormat.Alto,
            "html" when root.Descendants().Any(e => (string?)e.Attribute("class") == "ocr_page") => LegacyFormat.Hocr,
            "document" when root.Name.NamespaceName == "http://www.abbyy.com/FineReader_xml/FineReader10-schema-v1.xml" => LegacyFormat.Abbyy,
            _ => throw new InvalidDataException($"Unsupported legacy document root {root.Name.LocalName}; expected PAGE PcGts, ALTO, hOCR html or FineReader XML."),
        };
    }
}
