using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Companions;
using Tsukimichi.Game;
using Tsukimichi.Ui;

namespace Tsukimichi.Commands;

/// <summary>
/// <c>/tsuki stop</c> (1.11.0, A1): one Stop for every hand-off, for a macro or a single key. In order
/// (<see cref="StopAll.Order"/>) it stops the Walk to giver or Go to giver Tsukimichi started (its walk or flight, a
/// pathfind still pending included, and its teleport or hop), an aethernet hop or <c>/li</c> task Tsukimichi handed
/// Lifestream, a Questionable run, and the AutoDuty and Artisan runs Tsukimichi started (never one the player started
/// in that plugin's own window). It reuses each Stop button's own call, and prints one chat line: what it stopped, what
/// it could not, or "Nothing to stop.".
/// <para>
/// Two stops ask first, as their buttons do: Questionable while it would run its command after a stop and Settings asks
/// to be asked, and AutoDuty inside a duty. The question is the chat line, and the answer is the same command again
/// within <see cref="StopConfirm.WindowMs"/>; everything else stops on the first press.
/// </para>
/// <para>
/// Which AutoDuty, Artisan and Lifestream runs are Tsukimichi's is kept current each frame, and only while one is
/// claimed (<see cref="HandOffClaim"/>), so idle frames ask no plugin anything.
/// </para>
/// </summary>
public sealed class StopCommand : IDisposable
{
    private const string QuestionableName = "Questionable";
    private const string AutoDutyName = "AutoDuty";
    private const string ArtisanName = "Artisan";
    private const string LifestreamName = "Lifestream";

    private readonly IFramework framework;
    private readonly TravelService travel;
    private readonly LifestreamIpc lifestream;
    private readonly AutoDutyIpc autoDuty;
    private readonly ArtisanIpc? artisan;
    private readonly QuestionableActions questionable;
    private readonly Func<bool> questionableRunning;
    private readonly Action<string> print;
    private readonly IPluginLog log;
    private readonly StopConfirm confirm = new();
    private bool warned;

    /// <param name="framework">Each frame keeps the AutoDuty, Artisan and Lifestream claims current while one is held.</param>
    /// <param name="travel">Walk to giver and Go to giver.</param>
    /// <param name="lifestream">Hops and <c>/li</c> tasks handed off outside a Go to giver run.</param>
    /// <param name="autoDuty">Run with AutoDuty.</param>
    /// <param name="artisan">Craft with Artisan; null while the hand-in items are not wired.</param>
    /// <param name="questionable">Start and Stop Questionable, and its confirmation rule.</param>
    /// <param name="questionableRunning">Whether Questionable runs now (its live status, cached by its wrapper).</param>
    /// <param name="print">Prints the one chat line.</param>
    /// <param name="log">The plugin log.</param>
    public StopCommand(
        IFramework framework,
        TravelService travel,
        LifestreamIpc lifestream,
        AutoDutyIpc autoDuty,
        ArtisanIpc? artisan,
        QuestionableActions questionable,
        Func<bool> questionableRunning,
        Action<string> print,
        IPluginLog log)
    {
        this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
        this.travel = travel ?? throw new ArgumentNullException(nameof(travel));
        this.lifestream = lifestream ?? throw new ArgumentNullException(nameof(lifestream));
        this.autoDuty = autoDuty ?? throw new ArgumentNullException(nameof(autoDuty));
        this.artisan = artisan;
        this.questionable = questionable ?? throw new ArgumentNullException(nameof(questionable));
        this.questionableRunning = questionableRunning ?? throw new ArgumentNullException(nameof(questionableRunning));
        this.print = print ?? throw new ArgumentNullException(nameof(print));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        framework.Update += OnUpdate;
    }

    public void Dispose() => framework.Update -= OnUpdate;

    /// <summary>Stops what runs, asks about what must be asked, and prints one chat line. Never throws.</summary>
    public void Run()
    {
        try
        {
            print(Stop());
        }
        catch (Exception ex)
        {
            log.Warning(ex, "/tsuki stop failed");
            print(Strings.StopFailedAll);
        }
    }

    private string Stop()
    {
        var now = Environment.TickCount64;
        var confirmed = confirm.Answer(now);
        var running = Running();
        var questionCommand = (running & StopTarget.Questionable) != 0 ? questionable.StopQuestionCommand() : null;
        var needsConfirm = (questionCommand is not null ? StopTarget.Questionable : StopTarget.None)
            | (travel.InDuty ? StopTarget.AutoDuty : StopTarget.None);
        var decision = StopAll.Decide(running, needsConfirm, confirmed);

        // Named before the stop: a stopped run no longer says whether it was a lone walk.
        var travelName = travel.JourneyIsWalkOnly ? Strings.TravelWalk : Strings.TravelGoTo;
        var stopped = new List<string>();
        var failed = new List<string>();
        foreach (var target in StopAll.Each(decision.StopNow))
        {
            var name = target switch
            {
                StopTarget.Travel => travelName,
                StopTarget.Lifestream => LifestreamName,
                StopTarget.Questionable => QuestionableName,
                StopTarget.AutoDuty => AutoDutyName,
                _ => ArtisanName,
            };
            (StopOne(target) ? stopped : failed).Add(name);
        }

        if (decision.Ask != StopTarget.None)
        {
            confirm.Ask(now);
        }

        log.Information("/tsuki stop: running {Running}, stopped {Stopped}, failed {Failed}, asked {Asked}", running, string.Join(", ", stopped), string.Join(", ", failed), decision.Ask);
        return Line(stopped, failed, decision.Ask, questionCommand);
    }

    /// <summary>Everything that runs now and that this command may stop.</summary>
    private StopTarget Running()
    {
        var running = StopTarget.None;
        if (travel.JourneyActive)
        {
            running |= StopTarget.Travel;
        }
        else if (lifestream.HandOffClaimed && lifestream.TrackHandOff())
        {
            // A Go to giver run aborts its own hop; only a hop or command handed off on its own is Lifestream's line.
            running |= StopTarget.Lifestream;
        }

        if (Safe(questionableRunning))
        {
            running |= StopTarget.Questionable;
        }

        if (autoDuty.HandOffClaimed && autoDuty.TrackHandOff())
        {
            running |= StopTarget.AutoDuty;
        }

        if (artisan is { HandOffClaimed: true } crafting && crafting.TrackHandOff())
        {
            running |= StopTarget.Artisan;
        }

        return running;
    }

    /// <summary>Each target's own Stop, as its button calls it; false when it could not be asked.</summary>
    private bool StopOne(StopTarget target)
    {
        try
        {
            switch (target)
            {
                case StopTarget.Travel:
                    // The click guard of the Stop button is for double clicks; a typed command is never one.
                    travel.Stop();
                    return true;
                case StopTarget.Lifestream:
                    lifestream.Abort();
                    return true;
                case StopTarget.Questionable:
                    return questionable.StopQuietly();
                case StopTarget.AutoDuty:
                    return autoDuty.Stop();
                case StopTarget.Artisan:
                    return artisan?.Stop() == true;
                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "A stop failed");
            return false;
        }
    }

    /// <summary>"Stopped: Go to giver, Questionable." then what failed, then any question; "Nothing to stop." when all are empty.</summary>
    private static string Line(List<string> stopped, List<string> failed, StopTarget ask, string? questionCommand)
    {
        var parts = new List<string>(4);
        if (stopped.Count > 0)
        {
            parts.Add(string.Format(CultureInfo.CurrentCulture, Strings.StopDoneFormat, string.Join(Strings.CommandListSeparator, stopped)));
        }

        if (failed.Count > 0)
        {
            parts.Add(string.Format(CultureInfo.CurrentCulture, Strings.StopFailedFormat, string.Join(Strings.CommandListSeparator, failed)));
        }

        if ((ask & StopTarget.Questionable) != 0)
        {
            parts.Add(string.Format(CultureInfo.CurrentCulture, Strings.StopAskQuestionableFormat, questionCommand ?? string.Empty, StopConfirm.WindowSeconds));
        }

        if ((ask & StopTarget.AutoDuty) != 0)
        {
            parts.Add(string.Format(CultureInfo.CurrentCulture, Strings.StopAskAutoDutyFormat, StopConfirm.WindowSeconds));
        }

        return parts.Count == 0 ? Strings.StopNothing : string.Join(" ", parts);
    }

    /// <summary>Keeps the hand-off claims current while one is held; asks nothing while none is.</summary>
    private void OnUpdate(IFramework _)
    {
        try
        {
            if (autoDuty.HandOffClaimed)
            {
                autoDuty.TrackHandOff();
            }

            if (artisan is { HandOffClaimed: true } crafting)
            {
                crafting.TrackHandOff();
            }

            if (lifestream.HandOffClaimed)
            {
                lifestream.TrackHandOff();
            }
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Hand-off tracking failed");
        }
    }

    private bool Safe(Func<bool> read)
    {
        try
        {
            return read();
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "A running check failed");
            return false;
        }
    }

    private void WarnOnce(Exception ex, string message)
    {
        if (warned)
        {
            log.Debug(ex, message);
            return;
        }

        warned = true;
        log.Warning(ex, message);
    }
}
