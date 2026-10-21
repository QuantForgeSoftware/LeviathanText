using LeviathanText.Formats;
using Xunit;

namespace LeviathanText.Formats.Tests;

public class LegacyFormatTests
{
    [Fact]
    public void Detects_page_alto_and_hocr_documents()
    {
        Assert.Equal(LegacyFormat.Page, LegacyFormats.Detect(
            """<PcGts xmlns="http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15"><Page/></PcGts>"""));
        Assert.Equal(LegacyFormat.Alto, LegacyFormats.Detect(
            """<alto xmlns="http://www.loc.gov/standards/alto/ns-v4#"><Layout/></alto>"""));
        Assert.Equal(LegacyFormat.Hocr, LegacyFormats.Detect(
            """<html xmlns="http://www.w3.org/1999/xhtml"><body><div class="ocr_page"/></body></html>"""));
    }

    [Fact]
    public void Invalid_or_unknown_documents_are_rejected()
    {
        Assert.Throws<InvalidDataException>(() => LegacyFormats.Detect("<not-xml"));
        Assert.Throws<InvalidDataException>(() => LegacyFormats.Detect("<root/>"));
        Assert.Throws<InvalidDataException>(() => LegacyFormats.Detect(
            """<!DOCTYPE html [<!ENTITY x SYSTEM "file:///etc/passwd">]><html xmlns="http://www.w3.org/1999/xhtml"/>"""));
    }
}
