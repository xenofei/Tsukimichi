using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Config;
using Dalamud.Plugin.Services;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Moonfall.Sound;

namespace Tsukimichi.Game;

/// <summary>
/// Moonfall's sound (plan v9 G8): NAudio's WASAPI shared output fed by a <see cref="MixingSampleProvider"/> of pooled
/// <see cref="MoonfallVoice"/>s, through <see cref="MoonfallMaster"/>. The sounds are rendered once, off the frame, when
/// the window opens (<see cref="MoonfallSoundBank"/>), so a hit only starts a voice: no allocation, no rendering.
/// <para>
/// The window calls in on the framework thread: <see cref="Open"/> and <see cref="Close"/> with it, <see cref="Update"/>
/// every frame, <see cref="Event"/> for each engine event it reads and <see cref="Flush"/> after them, and
/// <see cref="Shot"/>, <see cref="Click"/> and <see cref="NewLevel"/>. Sound never feeds the engine.
/// </para>
/// <para>
/// Loudness: Moonfall's own setting (<see cref="Percent"/>, Moonfall's quick settings, 70 % by default, 0 for off) times
/// the game's master volume, both on a squared curve, and nothing while the game's master is muted. Silence while
/// Moonfall is paused (every sound holds its place, as the board does), and while the game is not the foreground
/// window unless the game's own "play sounds while the window is inactive" (master) is on; if that setting cannot be
/// read, a background game is silent, the quieter guess.
/// </para>
/// <para>
/// Never throws into Dalamud: a missing or lost audio device leaves Moonfall silent, logged once.
/// </para>
/// </summary>
public sealed class MoonfallAudio : IDisposable
{
    /// <summary>The most sounds at once; past it the oldest gives way.</summary>
    public const int MaxVoices = 16;

    /// <summary>WASAPI's buffer: low enough that a chime lands with its hit.</summary>
    private const int LatencyMs = 50;

    private const double PollSeconds = 0.5;
    private const double MuteSeconds = 0.03;
    private const double VolumeSeconds = 0.15;
    private const double CloseSeconds = 0.4;
    private const double RollStopSeconds = 0.08;
    private const double MusicStopSeconds = 0.8;

    private readonly IPluginLog log;
    private readonly IGameConfig? gameConfig;
    private readonly Func<int> readPercent;
    private readonly Action<int> writePercent;
    private readonly Lock gate = new();
    private readonly int processId = Environment.ProcessId;

    private MMDeviceEnumerator? enumerator;
    private MMDevice? device;
    private WasapiOut? output;
    private MixingSampleProvider? mixer;
    private MoonfallMaster? master;
    private MoonfallVoice[] voices = [];
    private MoonfallSoundBank? bank;
    private CancellationTokenSource? rendering;
    private int generation;
    private long stamp;
    private volatile bool deviceLost;
    private bool warned;
    private bool disposed;

    private MoonfallSoundCues cues;
    private MoonfallCountTicker ticker;
    private int pendingPeg = -1;
    private float pendingPegPan;
    private bool pendingClear;
    private float pendingClearPan;
    private (MoonfallVoice? Voice, int Serial) roll;
    private (MoonfallVoice? Voice, int Serial) music;

    private double sincePoll = PollSeconds;
    private float gameLevel = 1f;
    private bool gameMuted;
    private bool background;

    /// <param name="log">Where a missing device is logged, once.</param>
    /// <param name="gameConfig">The game's sound settings; null uses Moonfall's own volume alone.</param>
    /// <param name="readPercent">Moonfall's volume setting, 0–100.</param>
    /// <param name="writePercent">Saves a new volume setting.</param>
    public MoonfallAudio(IPluginLog log, IGameConfig? gameConfig, Func<int> readPercent, Action<int> writePercent)
    {
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.gameConfig = gameConfig;
        this.readPercent = readPercent ?? throw new ArgumentNullException(nameof(readPercent));
        this.writePercent = writePercent ?? throw new ArgumentNullException(nameof(writePercent));
    }

    /// <summary>Moonfall's volume, 0 (off) to 100 in steps of <see cref="PercentStep"/>.</summary>
    public int Percent
    {
        get => Math.Clamp(readPercent(), 0, 100);
        set => writePercent(Math.Clamp(value / PercentStep * PercentStep, 0, 100));
    }

    /// <summary>The volume setting's step.</summary>
    public const int PercentStep = 10;

    /// <summary>The window opened: start the output and render the sounds, or take back a close still fading.</summary>
    public void Open()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            generation++;
            if (output is not null)
            {
                return;
            }

            try
            {
                Start();
            }
            catch (Exception ex)
            {
                Warn(ex);
                Release();
            }
        }
    }

    /// <summary>The window closed: everything fades out (the finale too), then the output and the sounds are let go.</summary>
    public void Close()
    {
        int closing;
        lock (gate)
        {
            if (output is null)
            {
                return;
            }

            master?.Set(0f, false, CloseSeconds);
            closing = ++generation;
        }

        _ = Task.Delay(TimeSpan.FromSeconds(CloseSeconds + 0.1)).ContinueWith(
            _ =>
            {
                lock (gate)
                {
                    if (generation == closing)
                    {
                        Release();
                    }
                }
            },
            TaskScheduler.Default);
    }

    /// <summary>
    /// Every frame: the board <paramref name="paused"/> or not. Reads the game's volume and focus twice a second and sets
    /// the level; lets go of a lost device.
    /// </summary>
    public void Update(bool paused, double seconds)
    {
        if (deviceLost)
        {
            lock (gate)
            {
                deviceLost = false;
                Release();
            }

            return;
        }

        if (master is not { } m)
        {
            return;
        }

        sincePoll += seconds;
        if (sincePoll >= PollSeconds)
        {
            sincePoll = 0;
            PollGame();
        }

        var level = Curve(Percent) * Curve(gameLevel * 100) * (gameMuted || background ? 0f : 1f);
        m.Set(level, paused, paused || level == 0f ? MuteSeconds : VolumeSeconds);
    }

    /// <summary>One engine event, through the table (<see cref="MoonfallSoundCues"/>). Chimes and pops wait for <see cref="Flush"/>.</summary>
    public void Event(in MoonfallEvent e)
    {
        var cue = cues.For(e);
        if (cue.Stops == MoonfallSound.FeverRoll)
        {
            Stop(roll, RollStopSeconds);
            roll = default;
        }

        if (cue.StartsFinale)
        {
            Stop(music, MusicStopSeconds);
            music = Play(MoonfallSound.Finale, 0, 0f);
        }

        switch (cue.Sound)
        {
            case MoonfallSound.None:
                break;

            // Several pegs can light in one frame (a burst, a bloom, a bolt): one chime, the highest.
            case MoonfallSound.PegHit:
                if (cue.Variant > pendingPeg)
                {
                    pendingPeg = cue.Variant;
                    pendingPegPan = cue.Pan;
                }

                break;

            case MoonfallSound.PegClear:
                pendingClear = true;
                pendingClearPan = cue.Pan;
                break;

            case MoonfallSound.FeverRoll:
                Stop(roll, RollStopSeconds);
                roll = Play(cue.Sound, cue.Variant, cue.Pan);
                break;

            default:
                Play(cue.Sound, cue.Variant, cue.Pan);
                break;
        }
    }

    /// <summary>After the frame's events: the frame's chime and pop, and the count-up's tick for the score shown.</summary>
    public void Flush(long shownScore, long score, double seconds, bool paused)
    {
        if (pendingPeg >= 0)
        {
            Play(MoonfallSound.PegHit, pendingPeg, pendingPegPan);
            pendingPeg = -1;
        }

        if (pendingClear)
        {
            Play(MoonfallSound.PegClear, 0, pendingClearPan);
            pendingClear = false;
        }

        if (!paused && ticker.Next(shownScore, score, seconds, out var tick))
        {
            Play(MoonfallSound.CountTick, tick, 0f);
        }
    }

    /// <summary>A ball was shot.</summary>
    public void Shot() => Play(MoonfallSound.Launch, 0, 0f);

    /// <summary>A button was pressed.</summary>
    public void Click() => Play(MoonfallSound.UiClick, 0, 0f);

    /// <summary>A level starts (a restart, the next level): the finale and the roll fade, the table starts afresh.</summary>
    public void NewLevel()
    {
        Stop(music, MusicStopSeconds);
        Stop(roll, RollStopSeconds);
        music = default;
        roll = default;
        cues = default;
        pendingPeg = -1;
        pendingClear = false;
    }

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            generation++;
            Release();
        }
    }

    /// <summary>A setting's share of full volume: squared, closer to how loud it sounds than a straight line.</summary>
    private static float Curve(float percent)
    {
        var x = Math.Clamp(percent / 100f, 0f, 1f);
        return x * x;
    }

    private void Start()
    {
        enumerator = new MMDeviceEnumerator();
        device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        int rate;
        using (var client = device.AudioClient)
        {
            rate = client.MixFormat.SampleRate;
        }

        // The device's own rate when it is a common one; otherwise 48 kHz, which WASAPI's shared mode converts.
        if (rate is not (44100 or 48000))
        {
            rate = MoonfallSounds.ReferenceSampleRate;
        }

        var format = WaveFormat.CreateIeeeFloatWaveFormat(rate, 2);
        mixer = new MixingSampleProvider(format) { ReadFully = true };
        voices = new MoonfallVoice[MaxVoices];
        for (var i = 0; i < voices.Length; i++)
        {
            voices[i] = new MoonfallVoice(format);
        }

        master = new MoonfallMaster(mixer);
        master.Set(0f, false, MuteSeconds);
        output = new WasapiOut(device, AudioClientShareMode.Shared, true, LatencyMs);
        output.PlaybackStopped += OnStopped;
        output.Init(master);
        output.Play();

        // The sounds render off the frame; until they are in, Moonfall is silent.
        var cancel = new CancellationTokenSource();
        rendering = cancel;
        var token = cancel.Token;
        _ = Task.Run(
            () =>
            {
                try
                {
                    var rendered = MoonfallSoundBank.Render(rate, token);
                    lock (gate)
                    {
                        if (!token.IsCancellationRequested && rendered is not null)
                        {
                            bank = rendered;
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (gate)
                    {
                        Warn(ex);
                    }
                }
            },
            token);
        sincePoll = PollSeconds;
    }

    /// <summary>Stops and lets go of the output and the sounds; safe to call twice. Under <see cref="gate"/>.</summary>
    private void Release()
    {
        rendering?.Cancel();
        rendering?.Dispose();
        rendering = null;
        if (output is { } o)
        {
            o.PlaybackStopped -= OnStopped;
            try
            {
                o.Stop();
            }
            catch (Exception ex) when (ex is COMException or InvalidOperationException)
            {
                // The device is already gone; disposing is all that is left.
            }

            o.Dispose();
        }

        output = null;
        device?.Dispose();
        device = null;
        enumerator?.Dispose();
        enumerator = null;
        mixer = null;
        master = null;
        voices = [];
        bank = null;
        roll = default;
        music = default;
        pendingPeg = -1;
        pendingClear = false;
    }

    /// <summary>Starts <paramref name="sound"/> on a free voice (or the oldest one), unless nothing would be heard.</summary>
    private (MoonfallVoice? Voice, int Serial) Play(MoonfallSound sound, int variant, float pan)
    {
        lock (gate)
        {
            if (bank is not { } b || mixer is not { } mix || master is not { Audible: true } || b.Get(sound, variant) is not { } sample)
            {
                return default;
            }

            MoonfallVoice? chosen = null;
            MoonfallVoice? oldest = null;
            foreach (var voice in voices)
            {
                if (!voice.Active)
                {
                    chosen = voice;
                    break;
                }

                // The finale is never stolen for a sound effect.
                if (voice.Sound != MoonfallSound.Finale && (oldest is null || voice.Stamp < oldest.Stamp))
                {
                    oldest = voice;
                }
            }

            if (chosen is null)
            {
                if (oldest is null)
                {
                    return default;
                }

                mix.RemoveMixerInput(oldest);
                chosen = oldest;
            }

            chosen.Start(sound, sample, pan, 1f, ++stamp);
            mix.AddMixerInput(chosen);
            return (chosen, chosen.Serial);
        }
    }

    private void Stop((MoonfallVoice? Voice, int Serial) playing, double seconds)
    {
        lock (gate)
        {
            if (playing.Voice is { } voice && voice.Serial == playing.Serial && voice.Active && master is not null)
            {
                voice.FadeOut((int)(seconds * voice.WaveFormat.SampleRate));
            }
        }
    }

    /// <summary>The game's master volume and mute, and whether it plays sound in the background, read twice a second.</summary>
    private void PollGame()
    {
        background = !GameInFront() && !PlaysInBackground();
        if (gameConfig is null)
        {
            return;
        }

        try
        {
            if (gameConfig.TryGet(SystemConfigOption.SoundMaster, out uint level))
            {
                gameLevel = Math.Clamp(level, 0u, 100u) / 100f;
            }

            gameMuted = gameConfig.TryGet(SystemConfigOption.IsSndMaster, out uint muted) && muted != 0;
        }
        catch (Exception ex)
        {
            Warn(ex);
        }
    }

    /// <summary>The game's "play sounds while the window is inactive" (master); false when it cannot be read.</summary>
    private bool PlaysInBackground()
    {
        try
        {
            return gameConfig is not null && gameConfig.TryGet(SystemConfigOption.IsSoundAlways, out uint always) && always != 0;
        }
        catch (Exception ex)
        {
            Warn(ex);
            return false;
        }
    }

    /// <summary>Whether a window of the game's own process (the game, with Moonfall drawn in it) is the foreground one.</summary>
    private bool GameInFront()
    {
        try
        {
            var window = GetForegroundWindow();
            if (window == 0)
            {
                return false;
            }

            _ = GetWindowThreadProcessId(window, out var owner);
            return owner == processId;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Off Windows (Wine without the call): treat the game as in front.
            return true;
        }
    }

    private void OnStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is { } ex)
        {
            lock (gate)
            {
                Warn(ex);
            }

            deviceLost = true;
        }
    }

    private void Warn(Exception ex)
    {
        if (warned)
        {
            return;
        }

        warned = true;
        log.Warning(ex, "Moonfall's sound is off: the audio output could not be used. The game plays on silently; reopening Moonfall tries again");
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out int processId);
}
