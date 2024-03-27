using System.ComponentModel;
using System.Net;
using Downloader;
using DownloadProgressChangedEventArgs = Downloader.DownloadProgressChangedEventArgs;

namespace PartyGui_Avalonia_New.Functions;

public class Downloader
{
    // Delegates
    public delegate void DownloadCompleteDelegate(object sender, AsyncCompletedEventArgs args);

    public delegate void DownloadProgressDelegate(object sender, DownloadProgressChangedEventArgs args);

    public delegate void DownloadStartedDelegate(object sender, DownloadStartedEventArgs args);

    // Configurations
    private readonly DownloadConfiguration downloadOpt = new()
    {
        // usually, hosts support max to 8000 bytes, default values is 8000
        // BufferBlockSize = 10240,
        // file parts to download, default value is 1
        ChunkCount = 8,
        // download speed limited to 2MB/s, default values is zero or unlimited
        // MaximumBytesPerSecond = 1024 * 1024 * 2,
        // the maximum number of times to fail
        MaxTryAgainOnFailover = 5,
        // release memory buffer after each 50 MB
        MaximumMemoryBufferBytes = 1024 * 1024 * 50,
        // download parts of file as parallel or not. Default value is false
        ParallelDownload = true,
        // number of parallel downloads. The default value is the same as the chunk count
        ParallelCount = 8,
        // timeout (millisecond) per stream block reader, default values is 1000
        Timeout = 1000,
        // set true if you want to download just a specific range of bytes of a large file
        RangeDownload = false,
        // floor offset of download range of a large file
        RangeLow = 0,
        // ceiling offset of download range of a large file
        RangeHigh = 0,
        // clear package chunks data when download completed with failure, default value is false
        ClearPackageOnCompletionWithFailure = true,
        // minimum size of chunking to download a file in multiple parts, default value is 512
        MinimumSizeOfChunking = 1024,
        // Before starting the download, reserve the storage space of the file as file size, default value is false
        ReserveStorageSpaceBeforeStartingDownload = true,
        // config and customize request headers
        RequestConfiguration =
        {
            Accept = "*/*",
            Headers = new WebHeaderCollection(), // { your custom headers }
            KeepAlive = true, // default value is false
            ProtocolVersion = HttpVersion.Version30, // default value is HTTP 1.1
            UseDefaultCredentials = false,
            // your custom user agent or your_app_name/app_version.
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true
            /*
            Proxy = new WebProxy
            {
                Address = new Uri("http://YourProxyServer/proxy.pac"),
                UseDefaultCredentials = false,
                Credentials = CredentialCache.DefaultNetworkCredentials,
                BypassProxyOnLocal = true
            }
            */
        }
    };

    // Constructor
    public Downloader(string url, string filename)
    {
        downloader = new DownloadService(downloadOpt);
        URL = url;
        FileName = filename;
        downloader.DownloadStarted += DownloadStarted_Method;
        downloader.ChunkDownloadProgressChanged += DownloadChunks_Method;
        downloader.DownloadProgressChanged += DownloadProgress_Method;
        downloader.DownloadFileCompleted += DownloadComplete_Method;
    }

    // Misc variables
    private DownloadService downloader { get; }

    private string FileName { get; set; }

    private string URL { get; set; }

    // Events
    public event DownloadStartedDelegate Started;

    public event DownloadProgressDelegate ChunkDownloadProgressed;

    public event DownloadProgressDelegate DownloadProgress;

    public event DownloadCompleteDelegate DownloadComplete;

    private void DownloadStarted_Method(object? sender, DownloadStartedEventArgs e)
    {
        if (Started != null) Started.Invoke(sender, e);
    }

    private void DownloadChunks_Method(object? sender, DownloadProgressChangedEventArgs e)
    {
        if (ChunkDownloadProgressed != null) ChunkDownloadProgressed.Invoke(sender, e);
    }

    private void DownloadProgress_Method(object? sender, DownloadProgressChangedEventArgs e)
    {
        if (DownloadProgress != null) DownloadProgress.Invoke(sender, e);
    }

    private void DownloadComplete_Method(object? sender, AsyncCompletedEventArgs e)
    {
        if (DownloadComplete != null) DownloadComplete.Invoke(sender, e);
    }
}