using System.Diagnostics;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// Runs only in a git checkout of this repository with its history: skipped when git is not on the PATH, the
/// sources are not a work tree (a source archive) or the clone is shallow (a depth-1 checkout would name HEAD as the
/// last commit to touch any path). CI checks out with full history, so the test runs there.
/// </summary>
public sealed class GitHistoryFactAttribute : FactAttribute
{
    public GitHistoryFactAttribute()
    {
        var inside = Git("rev-parse", "--is-inside-work-tree");
        if (inside != "true")
        {
            Skip = inside is null ? "git is not available" : "not a git work tree";
            return;
        }

        if (Git("rev-parse", "--is-shallow-repository") != "false")
        {
            Skip = "shallow clone: the history is not all there";
        }
    }

    /// <summary>
    /// Runs git in the repository root (the directory holding <c>Tsukimichi.sln</c>) and returns its trimmed standard
    /// output, or null when git cannot be started, fails or takes longer than 10 s.
    /// </summary>
    public static string? Git(params string[] args)
    {
        try
        {
            var start = new ProcessStartInfo("git")
            {
                WorkingDirectory = RepositoryRoot(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8, // git writes file contents as UTF-8, not the console code page
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var arg in args)
            {
                start.ArgumentList.Add(arg);
            }

            using var process = Process.Start(start);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(10_000))
            {
                process.Kill(entireProcessTree: true);
                return null;
            }

            return process.ExitCode == 0 ? output.GetAwaiter().GetResult().Trim() : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return null;
        }
    }

    private static string RepositoryRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Tsukimichi.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? AppContext.BaseDirectory;
    }
}
