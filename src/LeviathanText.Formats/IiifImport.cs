using System.Text.Json;

namespace LeviathanText.Formats;

/// <summary>
/// Bounded IIIF Presentation 3.0 import (todo §3: bounded IIIF workflow — preserve
/// source/canvas identifiers, attribution and available views; keep cached projects
/// usable offline). Only canvas/image structure is mapped; IIIF hosting is out of scope.
/// Multiple views of one surface stay separate captures — coordinates are never merged
/// without registration.
/// </summary>
public static class IiifImport
{
    public sealed record CanvasRecord(
        string CanvasId,
        string? CanvasLabel,
        IReadOnlyList<ImageRecord> Images);

    public sealed record ImageRecord(string ImageUrl, string? ServiceUrl);

    public sealed record ManifestResult(
        string? ManifestId,
        string? ManifestLabel,
        string? RightsStatement,
        IReadOnlyList<CanvasRecord> Canvases);

    public static ManifestResult ParseManifest(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("@context", out var context) && context.ValueKind == JsonValueKind.String
            && !root.GetProperty("@context").GetString()!.Contains("presentation/3"))
            throw new InvalidDataException("Only IIIF Presentation 3.0 manifests are accepted.");

        var canvases = new List<CanvasRecord>();
        if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var canvas in items.EnumerateArray())
            {
                var canvasId = canvas.TryGetProperty("id", out var cid) ? cid.GetString() : null;
                var canvasLabel = LabelOf(canvas);
                var images = new List<ImageRecord>();

                if (canvas.TryGetProperty("items", out var annotationPages) && annotationPages.ValueKind == JsonValueKind.Array)
                    foreach (var page in annotationPages.EnumerateArray())
                        if (page.TryGetProperty("items", out var annotations) && annotations.ValueKind == JsonValueKind.Array)
                            foreach (var annotation in annotations.EnumerateArray())
                        {
                            if (annotation.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.Object
                                && body.TryGetProperty("type", out var type) && type.GetString() == "Image"
                                && body.TryGetProperty("id", out var bodyId))
                            {
                                string? serviceUrl = null;
                                if (body.TryGetProperty("service", out var svc))
                                {
                                    if (svc.ValueKind == JsonValueKind.Array)
                                    {
                                        var first = svc.EnumerateArray().FirstOrDefault();
                                        serviceUrl = first.ValueKind == JsonValueKind.Object && first.TryGetProperty("id", out var sid) ? sid.GetString() : null;
                                    }
                                    else if (svc.ValueKind == JsonValueKind.Object)
                                        serviceUrl = svc.TryGetProperty("id", out var sid2) ? sid2.GetString() : null;
                                }
                                images.Add(new ImageRecord(bodyId.GetString()!, serviceUrl));
                            }
                        }

                if (canvasId is not null)
                    canvases.Add(new CanvasRecord(canvasId, canvasLabel, images));
            }
        }

        string? rightsStatement = null;
        if (root.TryGetProperty("requiredStatement", out var required) && required.ValueKind == JsonValueKind.Object)
            rightsStatement = FirstEnglish(required.GetPropertySafe("value"));

        return new ManifestResult(
            root.TryGetProperty("id", out var mid) ? mid.GetString() : null,
            LabelOf(root),
            rightsStatement,
            canvases);
    }

    private static string? LabelOf(JsonElement element) =>
        element.TryGetProperty("label", out var label) ? FirstEnglish(label) : null;

    /// <summary>First string of a localized value: either {"en": [...]} or a bare array; null otherwise.</summary>
    private static string? FirstEnglish(JsonElement localized)
    {
        var array = localized.ValueKind switch
        {
            JsonValueKind.Array => localized,
            JsonValueKind.Object when localized.TryGetProperty("en", out var en) && en.ValueKind == JsonValueKind.Array => en,
            _ => default,
        };
        return array.ValueKind == JsonValueKind.Array
            && array.GetArrayLength() > 0
            && array[0].ValueKind == JsonValueKind.String
            ? array[0].GetString()
            : null;
    }

    private static JsonElement GetPropertySafe(this JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;
}
