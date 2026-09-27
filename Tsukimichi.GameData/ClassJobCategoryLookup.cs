using System.Collections.Frozen;
using Lumina.Data;
using Lumina.Excel;
using Tsukimichi.Core.Evaluation;

namespace Tsukimichi.GameData;

/// <summary>
/// ClassJobCategory membership as one bitset per category row.
/// The sheet is one string column (Name) followed by one bool column per class/job, in ClassJob row-id order, so
/// column <c>jobId + 1</c> answers "does this category admit jobId". Reading the raw columns rather than the generated
/// struct's named properties keeps jobs without a named property (e.g. the newest limited job) covered; the
/// data-driven test cross-checks a few named ones.
/// </summary>
public sealed class ClassJobCategoryLookup : IClassJobCategoryLookup
{
    private const string SheetName = "ClassJobCategory";

    /// <summary>Bits per word; job ids above <see cref="MaxJobs"/> are ignored.</summary>
    private const int MaxJobs = 128;

    private readonly FrozenDictionary<uint, Bits> categories;

    /// <summary>Number of job columns the sheet had, i.e. the highest job id + 1 the lookup can answer for.</summary>
    public int JobColumns { get; }

    private ClassJobCategoryLookup(FrozenDictionary<uint, Bits> categories, int jobColumns)
    {
        this.categories = categories;
        JobColumns = jobColumns;
    }

    public static readonly ClassJobCategoryLookup Empty = new(FrozenDictionary<uint, Bits>.Empty, 0);

    /// <summary>Reads the whole ClassJobCategory sheet. Membership is language independent; the language only picks the sheet page.</summary>
    public static ClassJobCategoryLookup Build(ExcelModule excel, Language? language = null)
    {
        ArgumentNullException.ThrowIfNull(excel);
        var sheet = excel.GetSheet<RawRow>(language, SheetName);
        var jobColumns = Math.Min(Math.Max(sheet.Columns.Count - 1, 0), MaxJobs);

        var result = new Dictionary<uint, Bits>(sheet.Count);
        foreach (var row in sheet)
        {
            var bits = new Bits();
            for (var job = 0; job < jobColumns; job++)
            {
                if (row.ReadColumn(job + 1) is true)
                {
                    bits.Set(job);
                }
            }

            result[row.RowId] = bits;
        }

        return new ClassJobCategoryLookup(result.ToFrozenDictionary(), jobColumns);
    }

    /// <summary>Builds from explicit membership, for tests and offline tools.</summary>
    public static ClassJobCategoryLookup FromMembership(IEnumerable<KeyValuePair<uint, IEnumerable<byte>>> membership)
    {
        var result = new Dictionary<uint, Bits>();
        var max = 0;
        foreach (var (category, jobs) in membership)
        {
            var bits = new Bits();
            foreach (var job in jobs)
            {
                bits.Set(job);
                max = Math.Max(max, job + 1);
            }

            result[category] = bits;
        }

        return new ClassJobCategoryLookup(result.ToFrozenDictionary(), max);
    }

    public int Count => categories.Count;

    public bool Admits(uint categoryId, byte classJobId)
        => categories.TryGetValue(categoryId, out var bits) && bits.Get(classJobId);

    public IEnumerable<byte> JobsIn(uint categoryId)
    {
        if (!categories.TryGetValue(categoryId, out var bits))
        {
            yield break;
        }

        for (var job = 0; job < MaxJobs; job++)
        {
            if (bits.Get(job))
            {
                yield return (byte)job;
            }
        }
    }

    /// <summary>Fixed-size 128-bit set; a struct so the frozen dictionary stores it inline.</summary>
    private struct Bits
    {
        private ulong lo;
        private ulong hi;

        public void Set(int job)
        {
            if (job < 64)
            {
                lo |= 1UL << job;
            }
            else if (job < MaxJobs)
            {
                hi |= 1UL << (job - 64);
            }
        }

        public readonly bool Get(int job) => job switch
        {
            < 0 => false,
            < 64 => (lo & (1UL << job)) != 0,
            < MaxJobs => (hi & (1UL << (job - 64))) != 0,
            _ => false,
        };
    }
}
