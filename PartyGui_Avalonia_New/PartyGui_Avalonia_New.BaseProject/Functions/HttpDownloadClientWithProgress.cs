using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading.Tasks;
using Orobouros.Managers;

public class HttpClientDownloadWithProgress(string downloadUrl, string destinationFilePath) : IDisposable
{
    /// <summary>
    ///     Delegate for progress event.
    /// </summary>
    public delegate void ProgressChangedHandler(long? totalFileSize, long totalBytesDownloaded,
        double? progressPercentage);

    /// <summary>
    ///     HttpClient class.
    /// </summary>
    private static readonly HttpClient HttpClient = new(new HttpClientHandler
    {
        AllowAutoRedirect = true,
        AutomaticDecompression = DecompressionMethods.All,
        SslProtocols = SslProtocols.Tls13
    })
    {
        Timeout = TimeSpan.FromDays(1),
        DefaultRequestVersion = new Version("3.0.0"),
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
    };

    /// <summary>
    ///     Custom dispose method.
    /// </summary>
    public void Dispose()
    {
        HttpClient?.Dispose();
    }

    /// <summary>
    ///     Event fired whenever progress is made.
    /// </summary>
    public event ProgressChangedHandler? ProgressChanged;

    /// <summary>
    ///     Starts a new download.
    /// </summary>
    public async Task StartDownload()
    {
        HttpClient.DefaultRequestHeaders.Add("User-Agent", UserAgentManager.RandomDesktopUserAgent);
        HttpClient.DefaultRequestHeaders.Add("Accept", "*/*");
        HttpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
        HttpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
        HttpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-site");
        HttpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
        HttpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
        using (var response = await HttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            await DownloadFileFromHttpResponseMessage(response);
        }
    }

    private async Task DownloadFileFromHttpResponseMessage(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;

        using (var contentStream = await response.Content.ReadAsStreamAsync())
        {
            await ProcessContentStream(totalBytes, contentStream);
        }
    }

    private async Task ProcessContentStream(long? totalDownloadSize, Stream contentStream)
    {
        var totalBytesRead = 0L;
        var readCount = 0L;
        var buffer = new byte[8192];
        var isMoreToRead = true;

        using (var fileStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None,
                   8192, true))
        {
            do
            {
                var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    isMoreToRead = false;
                    TriggerProgressChanged(totalDownloadSize, totalBytesRead);
                    continue;
                }

                await fileStream.WriteAsync(buffer, 0, bytesRead);

                totalBytesRead += bytesRead;
                readCount += 1;

                TriggerProgressChanged(totalDownloadSize, totalBytesRead);
            } while (isMoreToRead);
        }
    }

    private void TriggerProgressChanged(long? totalDownloadSize, long totalBytesRead)
    {
        if (ProgressChanged == null)
            return;

        double? progressPercentage = null;
        if (totalDownloadSize.HasValue)
            progressPercentage = Math.Round((double)totalBytesRead / totalDownloadSize.Value * 100, 2);

        ProgressChanged(totalDownloadSize, totalBytesRead, progressPercentage);
    }
}