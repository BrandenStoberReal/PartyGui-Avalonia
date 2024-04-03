using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Text.RegularExpressions;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Orobouros;
using Orobouros.Managers;
using Orobouros.Tools.Web;

namespace PartyGui_Avalonia_New.Views;

public partial class MainView : UserControl
{
    private readonly Regex _attachmentUrlRegex = new(@"\?f=(.*)");

    private readonly HttpClient _client = new(new HttpClientHandler
    {
        AllowAutoRedirect = true,
        AutomaticDecompression = DecompressionMethods.All,
        SslProtocols = SslProtocols.Tls13,
        PreAuthenticate = true,
        MaxConnectionsPerServer = 256
    });

    /// <summary>
    ///     Shouldn't really need to be used, only for sanitization of creator URL textbox.
    /// </summary>
    private readonly Regex _creatorUrlRegex = new("https://[A-Za-z0-9]+\\.su/[A-Za-z0-9]+/user/[A-Za-z0-9]+");

    /// <summary>
    ///     Main UI StorageProvider.
    /// </summary>
    private IStorageProvider _storageProvider;

    /// <summary>
    ///     TopMost UI
    /// </summary>
    private TopLevel _window;

    /// <summary>
    ///     Public constructor for the view.
    /// </summary>
    public MainView()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     URL of the creator to scrape content from.
    /// </summary>
    private string CreatorUrl { get; set; } = string.Empty;

    /// <summary>
    ///     Number of creator posts to scrape.
    /// </summary>
    private int NumberOfPosts { get; set; }

    /// <summary>
    ///     Whether to create a subfolder for each post or not.
    /// </summary>
    private bool PostSubfolders { get; set; } = true;

    /// <summary>
    ///     Whether to download post descriptions alongside attachments.
    /// </summary>
    private bool DownloadDescriptions { get; set; }

    /// <summary>
    ///     Whether to override the OS file modification times for every attachment.
    /// </summary>
    private bool OverrideFileTime { get; set; } = true;

    /// <summary>
    ///     Output directory where scraped content will be dumped.
    /// </summary>
    private string OutputDirectory { get; set; } = "";

    /// <summary>
    ///     Attempts to disable all clickable buttons on the main GUI.
    /// </summary>
    private void DisableBoxes()
    {
        ScrapeButton.IsEnabled = false;
        OutputDirButton.IsEnabled = false;
    }

    /// <summary>
    ///     Attempts to enable all previously disabled clickable buttons on the main GUI.
    /// </summary>
    private void EnableBoxes()
    {
        ScrapeButton.IsEnabled = true;
        OutputDirButton.IsEnabled = true;
    }

    /// <summary>
    ///     Shows a simple message box to the user. Compatible with all supported platforms.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="title"></param>
    /// <param name="buttons"></param>
    /// <param name="icon"></param>
    private async void ShowMessageBox(string message, string title, ButtonEnum buttons = ButtonEnum.Ok,
        Icon icon = Icon.Info)
    {
        DisableBoxes();
        var box = MessageBoxManager.GetMessageBoxStandard(title, message, buttons, icon);
        await box.ShowAsync();
        EnableBoxes();
    }

    private void IncrementProgressBar(ProgressBar bar)
    {
        Dispatcher.UIThread.InvokeAsync(() => { bar.Value++; });
    }

    private void SetProgressBarMaximum(ProgressBar bar, int max)
    {
        Dispatcher.UIThread.InvokeAsync(() => { bar.Maximum = max; });
    }

    private void ClearProgressBar(ProgressBar bar)
    {
        Dispatcher.UIThread.InvokeAsync(() => { bar.Value = 0; });
    }

    private void SetProgressBarValue(ProgressBar bar, double value)
    {
        Dispatcher.UIThread.InvokeAsync(() => { bar.Value = value; });
    }

    /// <summary>
    ///     Event called when the main view initializes. Should HOPEFULLY only be called on program startup.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <exception cref="InvalidOperationException"></exception>
    private void Control_OnLoaded(object? sender, RoutedEventArgs e)
    {
        ScrapingManager.InitializeModules();
        _window = TopLevel.GetTopLevel(this) ?? throw new InvalidOperationException();
        _storageProvider = _window.StorageProvider;
        _client.Timeout = TimeSpan.FromMinutes(5);
        _client.DefaultRequestHeaders.Add("User-Agent",
            UserAgentManager.RandomDesktopUserAgent);
        _client.DefaultRequestHeaders.Add("Accept", "*/*");
        _client.DefaultRequestHeaders.Add("Connection", "keep-alive");
        _client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
        _client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-site");
        _client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
        _client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
    }

    /// <summary>
    ///     Button click event to select the output directory.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void OutputDirButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var options = new FolderPickerOpenOptions();
        options.Title = "Select Output Directory...";
        options.AllowMultiple = false;

        var pickedFolders = await _storageProvider.OpenFolderPickerAsync(options);
        if (pickedFolders.Count > 0) OutputDirTextbox.Text = pickedFolders.First().Path.AbsolutePath;
    }

    /// <summary>
    ///     Text changed event to handle setting the output directory variable.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OutputDirTextbox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (OutputDirTextbox.Text != null) OutputDirectory = OutputDirTextbox.Text;
    }

    /// <summary>
    ///     Text changed event to handle setting the creator URL variable. Checks against the creator regex.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void CreatorUrlTextbox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (CreatorUrlTextbox.Text != null && _creatorUrlRegex.IsMatch(CreatorUrlTextbox.Text))
            CreatorUrl = CreatorUrlTextbox.Text;
    }

    /// <summary>
    ///     Text changed event to handle setting the post number variable. Sanitized to ensure text is an integer.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void PostNumTextbox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        try
        {
            if (PostNumTextbox.Text != null) NumberOfPosts = int.Parse(PostNumTextbox.Text);
        }
        catch
        {
        }
    }

    /// <summary>
    ///     Toggle event used to manage the post subfolder variable.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void PostSubfolderToggle_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        PostSubfolders = (bool)PostSubfolderToggle.IsChecked!;
    }

    /// <summary>
    ///     Toggle event used to manage the post descriptions variable.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void PostDescriptionsToggle_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        DownloadDescriptions = (bool)PostDescriptionsToggle.IsChecked!;
    }

    /// <summary>
    ///     Toggle event used to managed the file modification time variable.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void PostFileTimeToggle_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        OverrideFileTime = (bool)PostFileTimeToggle.IsChecked!;
    }

    /// <summary>
    ///     Click event used to handle a scrape request. This is where all primary scraping logic is done.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void ScrapeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (CreatorUrl == string.Empty)
        {
            ShowMessageBox("Creator URL is empty! Please correct this issue to proceed.", "Error", ButtonEnum.Ok,
                Icon.Error);
            return;
        }

        if (NumberOfPosts == 0 || NumberOfPosts < -1)
        {
            ShowMessageBox("Number of posts is invalid! Please correct this issue to proceed.", "Error",
                ButtonEnum.Ok, Icon.Error);
            return;
        }

        if (OutputDirectory == string.Empty)
        {
            ShowMessageBox("No output folder selected! Please correct this issue to proceed.", "Error",
                ButtonEnum.Ok, Icon.Error);
            return;
        }

        DisableBoxes();
        new Thread(() =>
        {
            Thread.CurrentThread.IsBackground = true;
            /*
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                outputProgress.Visible = true;
                outputLabel.Text = "Fetching metadata from API...";
            });
            */

            // Begin scrape
            var requestedInfo = new List<OrobourosInformation.ModuleContent>
                { OrobourosInformation.ModuleContent.Subposts };
            var data = ScrapingManager.ScrapeURL(CreatorUrl, requestedInfo, NumberOfPosts);
            if (data != null)
            {
                SetProgressBarMaximum(DownloadProgressBar, 100);
                SetProgressBarMaximum(PostsProgressBar, data.Content.Count);
                /*
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    outputLabel.Text = "Beginning downloader setup...";
                });
                */

                var iteration = 0;
                foreach (var scrapeData in data.Content)
                {
                    // Download the attachments
                    var post = (Post)scrapeData.Value;
                    iteration++;

                    ClearProgressBar(DownloadProgressBar);
                    ClearProgressBar(AttachmentsProgressBar);
                    IncrementProgressBar(PostsProgressBar);
                    SetProgressBarMaximum(AttachmentsProgressBar, post.Attachments.Count);

                    /*
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        outputLabel.Text = $"Downloading post #{iteration}/{data.Content.Count}";
                    });
                    */

                    var DownloadDir = string.Empty;

                    // Dynamically assign output dir
                    if (PostSubfolders)
                    {
                        DownloadDir = Path.Combine(OutputDirectory, post.Author.Username,
                            StringManager.SanitizeText(post.Title));
                        if (!DownloadDescriptions && post.Attachments.Count == 0) continue;

                        if (!Directory.Exists(DownloadDir)) Directory.CreateDirectory(DownloadDir);
                    }
                    else
                    {
                        DownloadDir = Path.Combine(OutputDirectory, post.Author.Username);
                        if (!Directory.Exists(DownloadDir)) Directory.CreateDirectory(DownloadDir);
                    }

                    // Download descriptions
                    if (DownloadDescriptions)
                        File.WriteAllText(Path.Combine(DownloadDir, "description.txt"), post.Description);

                    // Begin downloading attachments
                    foreach (var attach in post.Attachments)
                    {
                        IncrementProgressBar(AttachmentsProgressBar);
                        // Attempt to download 5 times
                        for (var i = 0; i < 5; i++)
                        {
                            ClearProgressBar(DownloadProgressBar);

                            // Download URL parsing
                            string downloadUrl;
                            if (_attachmentUrlRegex.IsMatch(attach.URL))
                                downloadUrl = _attachmentUrlRegex.Replace(attach.URL, "");
                            else
                                downloadUrl = attach.URL;

                            // Actual download method
                            try
                            {
                                // Download code here
                                IProgress<float> downloadProgress =
                                    new Progress<float>(s => SetProgressBarValue(DownloadProgressBar, s * 100));

                                // Create a file stream to store the downloaded data.
                                // This really can be any type of writeable stream.
                                using (var file = new FileStream(Path.Combine(DownloadDir, attach.Name),
                                           FileMode.Create, FileAccess.Write, FileShare.None))
                                {
                                    // Use the custom extension method below to download the data.
                                    // The passed progress-instance will receive the download status updates.
                                    _client.DownloadAsync(downloadUrl, file, downloadProgress)
                                        .Wait();
                                }

                                if (OverrideFileTime)
                                {
                                    var handle = File.OpenHandle(Path.Combine(DownloadDir, attach.Name), FileMode.Open,
                                        FileAccess.ReadWrite);
                                    File.SetCreationTime(handle, (DateTime)attach.ParentPost.UploadDate);
                                    File.SetLastWriteTime(handle, (DateTime)attach.ParentPost.UploadDate);
                                    File.SetLastAccessTime(handle, (DateTime)attach.ParentPost.UploadDate);
                                    handle.Close();
                                }

                                break;
                            }
                            catch (Exception ex)
                            {
                                LoggingManager.LogError("Exception: " + ex.Message);
                                LoggingManager.LogWarning(
                                    $"Attachment \"{attach.Name}\" from URL \"{downloadUrl}\" failed to download, retrying! [{i + 1}/5]");
                                if (File.Exists(Path.Combine(DownloadDir, attach.Name)))
                                    File.Delete(Path.Combine(DownloadDir, attach.Name));
                                var rng = new Random();
                                // 15-60 seconds
                                var waittime = rng.Next(15000, 60000);
                                Thread.Sleep(waittime);
                                LoggingManager.LogInformation(
                                    $"Waited {(int)Math.Floor((decimal)(waittime / 1000))} seconds, continuing...");
                            }
                        }

                        if (OverrideFileTime)
                        {
                            Directory.SetCreationTime(DownloadDir, (DateTime)post.UploadDate);
                            Directory.SetLastAccessTime(DownloadDir, (DateTime)post.UploadDate);
                            Directory.SetLastWriteTime(DownloadDir, (DateTime)post.UploadDate);
                        }
                    }
                }
            }
            else
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ShowMessageBox(
                        "PartyModule returned null data! This means a creator's page could not be scraped. Did you get ratelimited? Scraping task aborted.",
                        "Error", ButtonEnum.Ok, Icon.Error);
                });
            }

            ClearProgressBar(DownloadProgressBar);
            ClearProgressBar(AttachmentsProgressBar);
            ClearProgressBar(PostsProgressBar);
            /*
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                outputProgress.Visible = false;
                outputLabel.Text = "Idle...";
            });
            */
            Dispatcher.UIThread.InvokeAsync(EnableBoxes);
        }).Start();
    }

    private void DownloadProgressed(long? totalfilesize, long totalbytesdownloaded, double? progresspercentage)
    {
        SetProgressBarValue(DownloadProgressBar, (double)progresspercentage);
    }

    /// <summary>
    ///     Event called when the main UI unloads. This should HOPEFULLY only be called on program exit.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Control_OnUnloaded(object? sender, RoutedEventArgs e)
    {
        ScrapingManager.FlushSupplementaryMethods();
    }
}