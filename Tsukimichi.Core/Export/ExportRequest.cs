namespace Tsukimichi.Core.Export;

/// <summary>
/// The arguments of <c>/tsuki export [quests|moonlit] [json|csv]</c>, in either order: without a kind both exports
/// are written, without a format the one chosen in Settings is used. <see cref="Error"/> names a word it did not
/// understand; then nothing is written.
/// </summary>
public sealed record ExportRequest(IReadOnlyList<ExportKind> Kinds, ExportFormat Format, string? Error = null)
{
    private static readonly ExportKind[] Both = [ExportKind.Quests, ExportKind.Moonlit];

    public static ExportRequest Parse(string? arguments, ExportFormat defaultFormat)
    {
        ExportKind? kind = null;
        ExportFormat? format = null;
        var words = (arguments ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var word in words)
        {
            switch (word.ToLowerInvariant())
            {
                case "quests":
                case "quest":
                    kind = ExportKind.Quests;
                    break;
                case "moonlit":
                case "rewards":
                    kind = ExportKind.Moonlit;
                    break;
                case "json":
                    format = ExportFormat.Json;
                    break;
                case "csv":
                    format = ExportFormat.Csv;
                    break;
                default:
                    return new ExportRequest([], format ?? defaultFormat, word);
            }
        }

        return new ExportRequest(kind is { } k ? [k] : Both, format ?? defaultFormat);
    }
}
