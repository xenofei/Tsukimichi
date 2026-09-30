using System.Globalization;
using Tsukimichi.Core.Export;
using Tsukimichi.Game;
using Tsukimichi.Ui;

namespace Tsukimichi.Commands;

/// <summary>
/// <c>/tsuki export [quests|moonlit] [json|csv]</c>: writes the viewed character's exports (both without a kind, in
/// the Settings format without a format word) and prints each written path, or why nothing was written, to chat.
/// </summary>
public sealed class ExportCommand(ExportService exports, Config.Configuration settings, GameLinks links)
{
    public void Run(string arguments)
    {
        var request = ExportRequest.Parse(arguments, settings.ExportFormat);
        if (request.Error is { } word)
        {
            links.PrintText(string.Format(CultureInfo.CurrentCulture, Strings.ExportChatUsageFormat, word));
            return;
        }

        foreach (var kind in request.Kinds)
        {
            var result = exports.Export(kind, request.Format);
            links.PrintText(result.Ok && result.Path is { } path
                ? string.Format(CultureInfo.CurrentCulture, Strings.ExportChatWrittenFormat, path)
                : result.Message);
            if (!result.Ok)
            {
                return;
            }
        }
    }
}
