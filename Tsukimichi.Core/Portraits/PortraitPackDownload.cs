using System.Security.Cryptography;

namespace Tsukimichi.Core.Portraits;

/// <summary>Why getting or installing the portrait pack stopped (feature plan v7 F4); <see cref="None"/> when it worked.</summary>
public enum PortraitPackFailure : byte
{
    None = 0,

    /// <summary>This build offers no pack (no <c>portrait_pack.json</c>, or one without a release).</summary>
    NotOffered,

    /// <summary>No connection: the PC is offline, or GitHub could not be reached.</summary>
    Offline,

    /// <summary>The release has no such asset (404).</summary>
    NotFound,

    /// <summary>Another HTTP status.</summary>
    HttpError,

    /// <summary>A redirect to a host other than GitHub's own, or too many redirects.</summary>
    Redirected,

    /// <summary>The download is larger than the offer says.</summary>
    TooLarge,

    /// <summary>The download is not the size the offer says.</summary>
    SizeMismatch,

    /// <summary>The download's SHA-256 is not the offer's.</summary>
    HashMismatch,

    /// <summary>The zip cannot be read.</summary>
    BadArchive,

    /// <summary>The zip holds a file the pack never has: a folder path, a program, anything but its images and manifest.</summary>
    UnsafeEntry,

    /// <summary>The manifest is missing or not one this build reads.</summary>
    BadManifest,

    /// <summary>An image does not match its hash in the manifest, or does not decode.</summary>
    BadImage,

    /// <summary>The disk is full.</summary>
    DiskFull,

    /// <summary>Another file-system error.</summary>
    DiskError,

    /// <summary>The player cancelled.</summary>
    Cancelled,
}

/// <summary>What a transport got for one request: an HTTP status, a redirect target, the body; or no response at all.</summary>
public sealed class PortraitPackResponse : IDisposable
{
    private PortraitPackResponse(int status, long? length, Stream? body, Uri? location, string? error)
    {
        Status = status;
        Length = length;
        Body = body;
        Location = location;
        Error = error;
    }

    /// <summary>The HTTP status; 0 when there was no response (<see cref="Error"/> says why).</summary>
    public int Status { get; }

    /// <summary>The body's length when the server said it.</summary>
    public long? Length { get; }

    /// <summary>The body of a 2xx response.</summary>
    public Stream? Body { get; }

    /// <summary>The target of a redirect (3xx), absolute.</summary>
    public Uri? Location { get; }

    /// <summary>Why there was no response (a connection error's message).</summary>
    public string? Error { get; }

    public static PortraitPackResponse Ok(Stream body, long? length) => new(200, length, body ?? throw new ArgumentNullException(nameof(body)), null, null);

    public static PortraitPackResponse Redirect(int status, Uri location) => new(status, null, null, location, null);

    public static PortraitPackResponse Failed(int status) => new(status, null, null, null, null);

    public static PortraitPackResponse NoResponse(string error) => new(0, null, null, null, error);

    public void Dispose() => Body?.Dispose();
}

/// <summary>
/// The one way the plugin reaches the network (feature plan v7 F4): a GET that does not follow redirects, so
/// <see cref="PortraitPackDownload"/> checks every hop against the offer (<see cref="PortraitPackOffer.Allows"/>). The
/// plugin's implementation is <c>Tsukimichi/Game/PortraitPackHttp.cs</c>; tests pass their own.
/// </summary>
public interface IPortraitPackTransport
{
    /// <summary>
    /// Requests <paramref name="uri"/> and returns the response without following a redirect; a connection that fails
    /// returns <see cref="PortraitPackResponse.NoResponse"/> rather than throwing. Cancellation throws
    /// <see cref="OperationCanceledException"/>.
    /// </summary>
    Task<PortraitPackResponse> GetAsync(Uri uri, CancellationToken cancellation);
}

/// <summary>What a download received, for the Copy report line after a fingerprint mismatch (spec-1.20 F4).</summary>
public sealed class PortraitPackReceipt
{
    /// <summary>Bytes received.</summary>
    public long Bytes { get; set; }

    /// <summary>The SHA-256 of what was received (lower-case hex); empty when the download did not finish.</summary>
    public string Sha256 { get; set; } = string.Empty;
}

/// <summary>How far a download is: bytes so far of the expected total.</summary>
public readonly record struct PortraitPackProgress(long Received, long Total)
{
    /// <summary>0–1.</summary>
    public float Fraction => Total > 0 ? (float)Math.Clamp((double)Received / Total, 0d, 1d) : 0f;
}

/// <summary>
/// Downloads the offered pack to a file (feature plan v7 F4): from <see cref="PortraitPackOffer.DownloadUri"/> only,
/// following at most <see cref="MaxRedirects"/> redirects and only to GitHub's asset hosts; never more bytes than the
/// offer's size; then the size and SHA-256 must be the offer's, or the file is deleted and the download refused. Runs on
/// whatever thread calls it (the plugin calls it from a worker), reports progress, and stops on cancellation.
/// </summary>
public static class PortraitPackDownload
{
    /// <summary>The most redirects followed (GitHub uses one).</summary>
    public const int MaxRedirects = 4;

    private const int BufferSize = 81_920;

    /// <summary>How long one read of the body may stall before the download counts as offline.</summary>
    public static readonly TimeSpan ReadStall = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Once the offered size has arrived, how long the body may take to end (or to show a byte more, a mismatch) before
    /// the download goes on without waiting for its end.
    /// </summary>
    public static readonly TimeSpan EndGrace = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Fetches <paramref name="offer"/>'s pack into <paramref name="destination"/> (created or replaced). Returns
    /// <see cref="PortraitPackFailure.None"/> when the file is exactly the offered pack; otherwise the reason, with the
    /// file deleted. Disk errors are reported, not thrown.
    /// </summary>
    public static async Task<PortraitPackFailure> RunAsync(IPortraitPackTransport transport, PortraitPackOffer offer, string destination, IProgress<PortraitPackProgress>? progress, CancellationToken cancellation, PortraitPackReceipt? receipt = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentException.ThrowIfNullOrEmpty(destination);
        var result = PortraitPackFailure.DiskError;
        try
        {
            result = await Fetch(transport, offer, destination, progress, cancellation, receipt).ConfigureAwait(false);
            return result;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            result = PortraitPackFailure.Cancelled;
            return result;
        }
        catch (OperationCanceledException)
        {
            // Not ours: a transport's own time-out.
            result = PortraitPackFailure.Offline;
            return result;
        }
        catch (IOException ex)
        {
            result = PortraitPackStore.IsDiskFull(ex) ? PortraitPackFailure.DiskFull : PortraitPackFailure.DiskError;
            return result;
        }
        catch (UnauthorizedAccessException)
        {
            result = PortraitPackFailure.DiskError;
            return result;
        }
        finally
        {
            if (result != PortraitPackFailure.None)
            {
                PortraitPackStore.TryDelete(destination);
            }
        }
    }

    private static async Task<PortraitPackFailure> Fetch(IPortraitPackTransport transport, PortraitPackOffer offer, string destination, IProgress<PortraitPackProgress>? progress, CancellationToken cancellation, PortraitPackReceipt? receipt)
    {
        if (offer.Size <= 0 || offer.Size > PortraitPackOffer.MaxSize)
        {
            return PortraitPackFailure.TooLarge;
        }

        var uri = offer.DownloadUri;
        for (var hop = 0; ; hop++)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!offer.Allows(uri))
            {
                return PortraitPackFailure.Redirected;
            }

            using var response = await transport.GetAsync(uri, cancellation).ConfigureAwait(false);
            if (response.Status is >= 300 and < 400 && response.Location is { } next)
            {
                if (hop >= MaxRedirects)
                {
                    return PortraitPackFailure.Redirected;
                }

                uri = next.IsAbsoluteUri ? next : new Uri(uri, next);
                continue;
            }

            if (response.Status == 0)
            {
                return PortraitPackFailure.Offline;
            }

            if (response.Status == 404)
            {
                return PortraitPackFailure.NotFound;
            }

            if (response.Status is < 200 or >= 300 || response.Body is not { } body)
            {
                return PortraitPackFailure.HttpError;
            }

            if (response.Length is { } length && length != offer.Size)
            {
                return length > offer.Size ? PortraitPackFailure.TooLarge : PortraitPackFailure.SizeMismatch;
            }

            return await Save(body, offer, destination, progress, cancellation, receipt).ConfigureAwait(false);
        }
    }

    private static async Task<PortraitPackFailure> Save(Stream body, PortraitPackOffer offer, string destination, IProgress<PortraitPackProgress>? progress, CancellationToken cancellation, PortraitPackReceipt? receipt)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(destination));
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[BufferSize];
        long received = 0;
        await using (var file = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
        {
            progress?.Report(new PortraitPackProgress(0, offer.Size));
            while (true)
            {
                int read;
                using (var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                {
                    stall.CancelAfter(ReadStall);
                    try
                    {
                        read = await body.ReadAsync(buffer, stall.Token).ConfigureAwait(false);
                    }
                    catch (IOException)
                    {
                        // The connection dropped mid-download (a disk error is the write's, below).
                        return PortraitPackFailure.Offline;
                    }
                    catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
                    {
                        // The connection stalled.
                        return PortraitPackFailure.Offline;
                    }
                }

                if (read <= 0)
                {
                    break;
                }

                received += read;
                if (received > offer.Size)
                {
                    // More than promised: stop reading at once (a size cap, whatever the server says).
                    return PortraitPackFailure.TooLarge;
                }

                hash.AppendData(buffer, 0, read);
                await file.WriteAsync(buffer.AsMemory(0, read), cancellation).ConfigureAwait(false);
                progress?.Report(new PortraitPackProgress(received, offer.Size));
                if (received == offer.Size)
                {
                    // Every byte promised is here: never wait on for the end of a body that may not come (a chunked
                    // response without its terminator). A byte more, already sent, is still a mismatch.
                    if (await MoreAfterEnd(body, cancellation).ConfigureAwait(false))
                    {
                        return PortraitPackFailure.TooLarge;
                    }

                    break;
                }
            }

            await file.FlushAsync(cancellation).ConfigureAwait(false);
        }

        if (received != offer.Size)
        {
            if (receipt is not null)
            {
                receipt.Bytes = received;
            }

            return PortraitPackFailure.SizeMismatch;
        }

        var sha = Convert.ToHexStringLower(hash.GetHashAndReset());
        if (receipt is not null)
        {
            receipt.Bytes = received;
            receipt.Sha256 = sha;
        }

        return string.Equals(sha, offer.Sha256, StringComparison.Ordinal)
            ? PortraitPackFailure.None
            : PortraitPackFailure.HashMismatch;
    }

    /// <summary>
    /// After the offered size has arrived: whether the body holds more. Waits at most <see cref="EndGrace"/> for its
    /// end; a body that neither ends nor sends more by then, or whose connection then drops, is taken as ended.
    /// </summary>
    private static async Task<bool> MoreAfterEnd(Stream body, CancellationToken cancellation)
    {
        var probe = new byte[1];
        using var grace = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        grace.CancelAfter(EndGrace);
        try
        {
            return await body.ReadAsync(probe, grace.Token).ConfigureAwait(false) > 0;
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
