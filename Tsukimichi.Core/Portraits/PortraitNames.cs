using System.Globalization;
using System.Text;

namespace Tsukimichi.Core.Portraits;

/// <summary>
/// The name rules the portrait index matches by. Every source names its NPC differently ("Y'shtola" on a Trust bust,
/// <c>FACE_GRAPHIC_YSHTOLA</c> in a quest battle, "Tataru Taru" on a card), so names are compared in a normal form:
/// letters and digits only, lower case ("yshtola", "tatarutaru", "n4486"). Generic givers ("troubled adventurer": the
/// game writes them lower case, and roles such as "Resistance fighter" end in a lower-case word) are never matched,
/// since a name like that names a crowd, not a face.
/// </summary>
public static class PortraitNames
{
    /// <summary>
    /// "Y'shtola" → "yshtola", "G'raha Tia" → "grahatia", "N-4486" → "n4486": letters and digits only (accents
    /// dropped), lower case. Digits stay so numbered NPCs ("N-4486", "N-7000") never read as one name.
    /// </summary>
    public static string Normalize(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(name.Length);
        foreach (var c in name.Normalize(NormalizationForm.FormD))
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// A generic giver: blank, or the name starts with a lower-case letter ("troubled adventurer"), or it has several
    /// words and the last starts with a lower-case letter (a role: "Resistance fighter", "House Fortemps knight").
    /// Particles inside a proper name ("Nero tol Scaeva") do not make it generic.
    /// </summary>
    public static bool IsGeneric(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return true;
        }

        var words = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var first = FirstLetter(words[0]);
        if (first == '\0' || char.IsLower(first))
        {
            return true;
        }

        return words.Length > 1 && FirstLetter(words[^1]) is var last && last != '\0' && char.IsLower(last);
    }

    /// <summary>The first word's normal form ("Tataru Taru" → "tataru"); empty for a one-word or blank name.</summary>
    public static string FirstWordOfSeveral(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var words = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length < 2 ? string.Empty : Normalize(words[0]);
    }

    /// <summary>Whether the name is one word ("Tataru", "Y'shtola"; "G'raha Tia" is two).</summary>
    public static bool IsOneWord(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length == 1;

    /// <summary>
    /// One or two capitals for the initials medallion: the first letter of the first word, and of the last word when
    /// there are several ("Tataru Taru" → "TT", "Munavanu" → "M", "G'raha Tia" → "GT"); empty when the name has no letter.
    /// </summary>
    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var words = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpper(FirstLetter(w), CultureInfo.InvariantCulture))
            .Where(c => c != '\0')
            .ToList();
        return words.Count switch
        {
            0 => string.Empty,
            1 => words[0].ToString(),
            _ => string.Concat(words[0], words[^1]),
        };
    }

    private static char FirstLetter(string word)
    {
        foreach (var c in word)
        {
            if (char.IsLetter(c))
            {
                return c;
            }
        }

        return '\0';
    }
}
