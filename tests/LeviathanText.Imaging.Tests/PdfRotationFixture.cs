using System.Text;

namespace LeviathanText.Imaging.Tests;

/// <summary>Independent generated PDF syntax with a standard Type1 font; no external fixture content.</summary>
internal static class PdfRotationFixture
{
    public static string Write(string directory, int pageRotation = 0, int textRotation = 0,
        bool mixed = false, bool inheritedRotation = false)
    {
        var matrix = textRotation switch
        {
            0 => "1 0 0 1 40 130", 90 => "0 1 -1 0 100 40",
            180 => "-1 0 0 -1 160 100", 270 => "0 -1 1 0 200 130",
            45 => "0.70710678 0.70710678 -0.70710678 0.70710678 100 50",
            _ => throw new ArgumentOutOfRangeException(nameof(textRotation)),
        };
        var content = (mixed ? "BT /F1 20 Tf 1 0 0 1 20 180 Tm (PLAIN) Tj ET\n" : "") +
            $"BT /F1 20 Tf {matrix} Tm (ABCD) Tj ET\n";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [3 0 R] /Count 1 {(inheritedRotation ? $"/Rotate {pageRotation}" : "")} >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 200] {(inheritedRotation ? "" : $"/Rotate {pageRotation}")} /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}endstream",
        };
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (int i = 0; i < objects.Length; i++)
        {
            offsets.Add(pdf.Length);
            pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        int xref = pdf.Length;
        pdf.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) pdf.Append($"{offset:D10} 00000 n \n");
        pdf.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        var path = Path.Combine(directory, $"page-{pageRotation}-text-{textRotation}-{mixed}-{inheritedRotation}.pdf");
        File.WriteAllText(path, pdf.ToString(), Encoding.ASCII);
        return path;
    }
}
