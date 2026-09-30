namespace Tsukimichi.Verify.Net;

/// <summary>
/// Minimal robots.txt reader: the group for our user agent token when one exists, else the <c>*</c> group. A path is
/// disallowed when the longest matching Allow/Disallow prefix is a Disallow (the usual longest-match rule; <c>$</c> and
/// <c>*</c> wildcards are honoured as plain prefix/suffix checks). A missing or unreadable robots.txt allows everything.
/// </summary>
internal sealed class Robots
{
    private readonly List<(bool Allow, string Path)> rules;

    private Robots(List<(bool Allow, string Path)> rules) => this.rules = rules;

    public static readonly Robots AllowAll = new([]);

    public int RuleCount => rules.Count;

    public static Robots Parse(string text, string userAgentToken)
    {
        var groups = new List<(List<string> Agents, List<(bool Allow, string Path)> Rules)>();
        List<string>? agents = null;
        List<(bool, string)>? current = null;
        var lastWasAgent = false;

        foreach (var raw in text.Split('\n'))
        {
            var line = raw;
            var hash = line.IndexOf('#');
            if (hash >= 0)
            {
                line = line[..hash];
            }

            line = line.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var colon = line.IndexOf(':');
            if (colon < 0)
            {
                continue;
            }

            var key = line[..colon].Trim().ToLowerInvariant();
            var value = line[(colon + 1)..].Trim();
            switch (key)
            {
                case "user-agent":
                    if (!lastWasAgent || agents is null)
                    {
                        agents = [];
                        current = [];
                        groups.Add((agents, current));
                    }

                    agents.Add(value.ToLowerInvariant());
                    lastWasAgent = true;
                    break;
                case "allow":
                case "disallow":
                    if (current is not null)
                    {
                        current.Add((key == "allow", value));
                    }

                    lastWasAgent = false;
                    break;
                default:
                    lastWasAgent = false;
                    break;
            }
        }

        var token = userAgentToken.ToLowerInvariant();
        var specific = groups.Where(g => g.Agents.Any(a => a != "*" && (token.Contains(a) || a.Contains(token)))).ToList();
        if (specific.Count > 0)
        {
            return new Robots(specific.SelectMany(g => g.Rules).ToList());
        }

        var star = groups.Where(g => g.Agents.Contains("*")).ToList();
        return new Robots(star.SelectMany(g => g.Rules).ToList());
    }

    public bool IsAllowed(string pathAndQuery)
    {
        var best = -1;
        var allowed = true;
        foreach (var (allow, path) in rules)
        {
            if (path.Length == 0)
            {
                continue;
            }

            if (Matches(path, pathAndQuery) && path.Length > best)
            {
                best = path.Length;
                allowed = allow;
            }
        }

        return allowed;
    }

    private static bool Matches(string pattern, string path)
    {
        var anchored = pattern.EndsWith('$');
        if (anchored)
        {
            pattern = pattern[..^1];
        }

        var parts = pattern.Split('*');
        var pos = 0;
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (part.Length == 0)
            {
                continue;
            }

            var idx = i == 0 ? (path.StartsWith(part, StringComparison.Ordinal) ? 0 : -1) : path.IndexOf(part, pos, StringComparison.Ordinal);
            if (idx < 0)
            {
                return false;
            }

            pos = idx + part.Length;
        }

        return !anchored || pos == path.Length || (parts[^1].Length == 0);
    }
}
