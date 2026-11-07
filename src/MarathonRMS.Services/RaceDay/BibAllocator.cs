namespace MarathonRMS.Services.RaceDay;

/// <summary>Chọn số BIB còn trống trong dải của cự ly – logic thuần, không cần DB (BR-19).</summary>
public interface IBibAllocator
{
    /// <summary>Tối đa <paramref name="count"/> số nhỏ nhất trong [bibFrom, bibTo] chưa nằm trong <paramref name="used"/>.</summary>
    IReadOnlyList<int> Allocate(int bibFrom, int bibTo, IReadOnlySet<int> used, int count);
}

public class BibAllocator : IBibAllocator
{
    public IReadOnlyList<int> Allocate(int bibFrom, int bibTo, IReadOnlySet<int> used, int count)
    {
        var numbers = new List<int>();
        for (var n = bibFrom; n <= bibTo && numbers.Count < count; n++)
            if (!used.Contains(n)) numbers.Add(n);
        return numbers;
    }
}
