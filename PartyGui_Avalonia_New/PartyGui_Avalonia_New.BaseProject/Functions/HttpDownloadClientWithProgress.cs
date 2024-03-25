using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading.Tasks;
using Orobouros.Managers;

public class HttpClientDownloadWithProgress : IDisposable
{
    public delegate void ProgressChangedHandler(long? totalFileSize, long totalBytesDownloaded,
        double? progressPercentage);

    private readonly string _destinationFilePath;
    private readonly string _downloadUrl;

    private HttpClient _httpClient;

    private HttpClientHandler _httpClientHandler = new()
    {
        AllowAutoRedirect = true,
        AutomaticDecompression = DecompressionMethods.All,
        SslProtocols = SslProtocols.Tls13
    };

    public HttpClientDownloadWithProgress(string downloadUrl, string destinationFilePath)
    {
        _downloadUrl = downloadUrl;
        _destinationFilePath = destinationFilePath;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    public event ProgressChangedHandler ProgressChanged;

    public async Task StartDownload()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromDays(1),
            DefaultRequestVersion = new Version("2.0.0")
        };

        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            UserAgentManager.RandomDesktopUserAgent);
        using (var response = await _httpClient.GetAsync(_downloadUrl, HttpCompletionOption.ResponseHeadersRead))
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

        using (var fileStream = new FileStream(_destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None,
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