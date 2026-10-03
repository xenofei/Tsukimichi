using Lumina.Excel.Sheets;
using Tsukimichi.Core.Portraits;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

public class ZzProbeTests(GameDataFixture fixture) : IClassFixture<GameDataFixture>
{
    [GameDataFact]
    public void Probe()
    {
        var path = Environment.GetEnvironmentVariable("PROBE_OUT") ?? "probe.txt";
        using var w = new StreamWriter(path);
        var excel = fixture.Game.Excel;
        var curation = CuratedData.Load(FixtureCatalog.CuratedDir()).GiverPortraits;
        var inputs = GiverPortraitSources.Read(excel, icon => fixture.Game.FileExists(RewardArtIndex.IconPath(icon)));
        var index = PortraitIndex.Build(inputs, curation);
        var names = inputs.Givers.ToDictionary(g => g.NpcId, g => g.Name);
        var questSheet = excel.GetSheet<Quest>(Lumina.Data.Language.English);

        w.WriteLine("== all-later picks");
        foreach (var g in inputs.Quests.GroupBy(q => (q.GiverId, Pick: index.For(q.GiverId, q.QuestId))).Where(g => g.Key.Pick.HasArt && g.Key.Pick.Era > g.First().Expansion))
        {
            var q = g.First();
            w.WriteLine($"{names[q.GiverId]} ({q.GiverId}) icon {g.Key.Pick.Icon} {g.Key.Pick.Source} faceEra {g.Key.Pick.Era} questEras [{string.Join(",", g.Select(x => x.Expansion).Distinct())}] quests [{string.Join(",", g.Select(x => x.QuestId + ":" + questSheet.GetRowOrDefault(x.QuestId)?.Name.ExtractText() + (questSheet.GetRowOrDefault(x.QuestId)?.Festival.RowId is > 0 ? "(F)" : "")))}] variants [{string.Join(" ", index.Variants(q.GiverId).Select(v => $"{v.Icon}/{v.Source}/{v.Era}/{v.MatchedName}"))}]");
        }

        w.WriteLine("== triple triad cards worn");
        var res = excel.GetSheet<TripleTriadCardResident>();
        var cards = excel.GetSheet<TripleTriadCard>(Lumina.Data.Language.English);
        var worn = inputs.Givers.SelectMany(g => index.Variants(g.NpcId).Select(v => (g, v))).Where(x => x.v.Source == PortraitSource.TripleTriadCard).GroupBy(x => x.v.Icon).OrderBy(x => x.Key);
        foreach (var g in worn)
        {
            var row = g.Key - 87000;
            var r = res.GetRowOrDefault(row);
            w.WriteLine($"{g.Key} {cards.GetRowOrDefault(row)?.Name.ExtractText()} order {r?.Order} sort {r?.SortKey} uip {r?.UIPriority} era {g.First().v.Era} givers [{string.Join(",", g.Select(x => x.g.Name).Distinct())}]");
        }

        w.WriteLine("== all cards by order");
        foreach (var r in res.Where(r => r.RowId != 0).OrderBy(r => r.Order))
        {
            w.WriteLine($"row {r.RowId} order {r.Order} sort {r.SortKey} uip {r.UIPriority} quest {r.Quest.RowId} {cards.GetRowOrDefault(r.RowId)?.Name.ExtractText()}");
        }

        w.WriteLine("== dawn members");
        var labels = excel.GetSheet<DawnMemberUIParam>(Lumina.Data.Language.English);
        var faceEra = inputs.Faces.Where(f => f.Source is PortraitSource.TrustBust or PortraitSource.TrustStrip).ToList();
        foreach (var row in excel.GetSheet<DawnQuestMember>())
        {
            var f = faceEra.FirstOrDefault(x => x.Icon == row.BigImageOld || x.Icon == row.BigImageNew);
            w.WriteLine($"row {row.RowId} member {row.Member.RowId} old {row.BigImageOld} new {row.BigImageNew} u0 {row.Unknown0} u1 {row.Unknown1} class {row.Class.RowId} {labels.GetRowOrDefault(row.Class.RowId)?.Name.ExtractText()} name {f?.Name} era {f?.Era}");
        }

        w.WriteLine("== icons 72600-73000 present");
        w.WriteLine(string.Join(" ", Enumerable.Range(72600, 400).Where(i => fixture.Game.FileExists(RewardArtIndex.IconPath((uint)i)))));

        w.WriteLine("== first era from festival quests");
        foreach (var giver in inputs.Givers)
        {
            var qs = inputs.Quests.Where(q => q.GiverId == giver.NpcId).ToList();
            var story = qs.Where(q => questSheet.GetRowOrDefault(q.QuestId)?.Festival.RowId is null or 0).ToList();
            if (index.Variants(giver.NpcId).Count > 0 && qs.Count > 0 && (story.Count == 0 || story.Min(q => q.Expansion) != qs.Min(q => q.Expansion)))
            {
                w.WriteLine($"{giver.Name} ({giver.NpcId}) all-min {qs.Min(q => q.Expansion)} story-min {(story.Count == 0 ? -1 : story.Min(q => q.Expansion))}");
            }
        }

        w.WriteLine("== null-era faces worn");
        foreach (var f in inputs.Faces.Where(f => f.Era is null && f.Source != PortraitSource.TripleTriadCard))
        {
            w.WriteLine($"{f.Icon} {f.Source} {f.Name}");
        }
    }
}
