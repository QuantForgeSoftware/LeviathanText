using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Layout;

/// <summary>One source reading-order group expanded to region and line ids.</summary>
public sealed record ReadingOrderNode(
    string GroupId,
    bool IsTotal,
    IReadOnlyList<EntityId> RegionIds,
    IReadOnlyList<EntityId> LineIds);

/// <summary>
/// Expands explicit PAGE/ALTO reading-order groups into line-level orders. Group order is
/// preserved; a group marked total can be used as the page order, while partial groups
/// remain separate alternatives. No order is inferred from geometry here.
/// </summary>
public static class HierarchicalReadingOrder
{
    public static IReadOnlyList<ReadingOrderNode> Build(
        IReadOnlyList<PageXml.ReadingOrderGroup> groups,
        IReadOnlyList<NativeRegion> regions,
        IReadOnlyList<NativeLine> lines)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentNullException.ThrowIfNull(lines);

        var linesByRegion = lines
            .GroupBy(line => line.RegionId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var result = new List<ReadingOrderNode>(groups.Count);
        foreach (var group in groups)
        {
            var regionIds = new List<EntityId>();
            var lineIds = new List<EntityId>();
            foreach (var reference in group.RegionRefs)
            {
                var id = new EntityId(EntityKind.Region, reference);
                if (!regions.Any(region => region.Id == id)) continue;
                regionIds.Add(id);
                if (linesByRegion.TryGetValue(id, out var regionLines))
                    lineIds.AddRange(regionLines.Select(line => line.Id));
            }
            result.Add(new ReadingOrderNode(group.Id, group.IsTotal, regionIds, lineIds));
        }
        return result;
    }
}
