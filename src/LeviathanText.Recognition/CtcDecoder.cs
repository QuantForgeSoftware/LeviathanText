namespace LeviathanText.Recognition;

/// <summary>
/// CTC decoding. Greedy decoding collapses repeated indices and drops blanks, which is
/// the minimum viable decoder; a beam-search decoder can be added without changing the
/// interface (project-plan §5 leaves decoding strategy to measurement).
/// </summary>
public static class CtcDecoder
{
    public static int[] Greedy(IReadOnlyList<int> argmaxPerTimeStep, int blank = LabelSet.BlankIndex)
    {
        var collapsed = new List<int>(argmaxPerTimeStep.Count);
        int previous = -1;
        foreach (var index in argmaxPerTimeStep)
        {
            if (index != previous && index != blank) collapsed.Add(index);
            previous = index;
        }
        return [.. collapsed];
    }
}
