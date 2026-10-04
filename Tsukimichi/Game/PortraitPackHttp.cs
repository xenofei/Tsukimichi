using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Tsukimichi.Core.Portraits;

namespace Tsukimichi.Game;

/// <summary>
/// The plugin's only network code (feature plan v7 F4, decision 8; docs/privacy.md): one HTTPS GET at a time for the
/// optional portrait pack, made only after the player confirms the download in Settings. It does not follow redirects
/// itself: <see cref="PortraitPackDownload"/> checks every hop against the offer, so nothing but the pinned GitHub
/// release asset (and GitHub's own asset hosts it redirects to) is ever fetched. No cookies, no credentials, no other
/// headers than a User-Agent naming the plugin. <c>NoNetworkTests</c> allows network names in this file alone.
/// </summary>
internal sealed class PortraitPackHttp : IPortraitPackTransport, IDisposable
{
    private readonly HttpClient http;
    private readonly PortraitPackOffer offer;

    /// <param name="offer">The pack this client fetches: it refuses any address the offer does not allow, whoever asks.</param>
    public PortraitPackHttp(PortraitPackOffer offer, string pluginVersion)
    {
        this.offer = offer ?? throw new ArgumentNullException(nameof(offer));
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = true,
            ConnectTimeout = TimeSpan.FromSeconds(20),
            MaxConnectionsPerServer = 1,
        };
        http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Tsukimichi/" + (string.IsNullOrWhiteSpace(pluginVersion) ? "0" : pluginVersion.Trim()) + " (portrait pack)");
    }

    /// <inheritdoc/>
    public async Task<PortraitPackResponse> GetAsync(Uri uri, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!offer.Allows(uri))
        {
            // Checked by the download already; checked here too, so this client can never fetch anything else.
            return PortraitPackResponse.Failed(403);
        }

        HttpResponseMessage? response = null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false);
            var status = (int)response.StatusCode;
            if (status is >= 300 and < 400)
            {
                var location = response.Headers.Location;
                response.Dispose();
                return location is null
                    ? PortraitPackResponse.Failed(status)
                    : PortraitPackResponse.Redirect(status, location.IsAbsoluteUri ? location : new Uri(uri, location));
            }

            if (status is < 200 or >= 300)
            {
                response.Dispose();
                return PortraitPackResponse.Failed(status);
            }

            var body = await response.Content.ReadAsStreamAsync(cancellation).ConfigureAwait(false);
            return PortraitPackResponse.Ok(body, response.Content.Headers.ContentLength);
        }
        catch (HttpRequestException ex)
        {
            response?.Dispose();
            return PortraitPackResponse.NoResponse(ex.Message);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            response?.Dispose();
            return PortraitPackResponse.NoResponse("timed out");
        }
    }

    public void Dispose() => http.Dispose();
}
