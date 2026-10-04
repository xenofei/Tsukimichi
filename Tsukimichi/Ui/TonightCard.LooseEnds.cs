using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Todo;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// "When a storyline's finale is Ready" in the Tonight card (feature plan v7 N8; spec-1.21 N8): with the setting on
/// (off by default), one line for the first finale the viewed character can take now of a line it started and never
/// finished, finales the game marks only in yellow first (<see cref="LooseEnds"/>' order). The chat line is
/// <c>ChatNotifier</c>'s. Kept apart from the other lines so they can change without touching this.
/// </summary>
public sealed partial class TonightCard
{
    private IReadOnlyList<LooseEnd>? finaleEnds;
    private int finaleShield;
    private TodoRow? finaleRow;

    /// <summary>The Loose ends; set by the plugin. Null draws nothing.</summary>
    public LooseEndsSource? LooseEnds { get; set; }

    /// <summary>Whether the finale line shows (Settings › Alerts › When a storyline's finale is Ready); set by the plugin.</summary>
    public Func<bool>? ShowFinales { get; set; }

    private void DrawFinale(CatalogBundle bundle)
    {
        if (LooseEnds is not { } source || ShowFinales?.Invoke() != true || runner.Session is not { } session)
        {
            return;
        }

        var ends = source.Viewed;
        var spoilers = session.Spoilers;
        if (!ReferenceEquals(ends, finaleEnds) || spoilers.Fingerprint != finaleShield)
        {
            finaleEnds = ends;
            finaleShield = spoilers.Fingerprint;
            finaleRow = null;
            foreach (var end in ends)
            {
                if (!end.IsReadyFinale)
                {
                    continue;
                }

                var name = LooseEndsSource.NextName(end, spoilers, out _);
                finaleRow = new TodoRow(end.Next.RowId, name, end.NextState, source.NameOf(end.Line, spoilers), TodoRowKind.LooseEnd);
                break;
            }
        }

        if (finaleRow is not { } row)
        {
            return;
        }

        Chrome.Hairline();
        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.TextUnformatted(Strings.LooseEndsTonightLabel);
        }

        Row(bundle, row, FinaleRowId);
    }

    /// <summary>The finale row's id, clear of the main scenario and pinned rows'.</summary>
    private const int FinaleRowId = 900;
}
