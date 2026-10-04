using System.Globalization;
using Tsukimichi.Core.Text;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › In game › Chat (plan v7, 1.21.0 P8): "Say what's next in chat", off by default, with a sample of the line
/// it prints, built by the same <see cref="GuidanceText"/> as <c>/tsuki next</c> so the sample never drifts from it.
/// </summary>
public sealed partial class ConfigWindow
{
    private string? sayNextSample;
    private int sayNextSampleLanguage = -1;

    private void DrawSayWhatsNext()
    {
        Header(Strings.GuidanceChatHeader);
        var on = settings.SayWhatsNext;
        if (Toggle(Strings.GuidanceSayNext, Strings.GuidanceSayNextHint, ref on, "chat next step quest text-to-speech tts read aloud say what's next msq"))
        {
            settings.SayWhatsNext = on;
            Save();
        }

        if (sayNextSample is null || sayNextSampleLanguage != Localization.Loc.Version)
        {
            // The first quest of the game, so the sample spoils nothing.
            var line = GuidanceText.NextReady("Close to Home", 0, "Mother Miounne", new GuidancePlace("New Gridania", 11.4f, 13.2f), null);
            sayNextSample = string.Format(CultureInfo.CurrentCulture, Strings.GuidanceSayNextExampleFormat, line.Text);
            sayNextSampleLanguage = Localization.Loc.Version;
        }

        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            TextFlow.Wrapped(sayNextSample);
        }
    }
}
