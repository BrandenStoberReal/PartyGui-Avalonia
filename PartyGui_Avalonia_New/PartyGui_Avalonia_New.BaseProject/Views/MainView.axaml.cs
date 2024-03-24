using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using DownloadProgressChangedEventArgs = Downloader.DownloadProgressChangedEventArgs;

namespace PartyGui_Avalonia_New.Views;

public partial class MainView : UserControl
{
    private readonly Regex creatorUrlRegex = new("https://[A-Za-z0-9]+\\.su/[A-Za-z0-9]+/user/[A-Za-z0-9]+");

    private IStorageProvider StorageProvider;
    private TopLevel window;

    public MainView()
    {
        InitializeComponent();
    }

    private string CreatorURL { get; set; } = string.Empty;

    private int NumberOfPosts { get; set; }

    private bool PostSubfolders { get; set; } = true;
    private bool DownloadDescriptions { get; set; }
    private bool OverrideFileTime { get; set; } = true;
    private string OutputDirectory { get; set; }

    private async void ShowMessageBox(string message, string title, ButtonEnum buttons = ButtonEnum.Ok,
        Icon icon = Icon.Info)
    {
        var box = MessageBoxManager.GetMessageBoxStandard(title, message, buttons, icon);
        await box.ShowAsync();
    }

    private void DisableBoxes()
    {
        ScrapeButton.IsEnabled = false;
        OutputDirButton.IsEnabled = false;
    }

    private void EnableBoxes()
    {
        ScrapeButton.IsEnabled = true;
        OutputDirButton.IsEnabled = true;
    }

    private void Control_OnLoaded(object? sender, RoutedEventArgs e)
    {
        ScrapingManager.InitializeModules();
        window = TopLevel.GetTopLevel(this) ?? throw new InvalidOperationException();
        StorageProvider = window.StorageProvider;
    }

    private async void OutputDirButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var options = new FolderPickerOpenOptions();
        options.Title = "Select Output Directory...";
        options.AllowMultiple = false;

        var pickedFolders = await StorageProvider.OpenFolderPickerAsync(options);
        if (pickedFolders.Count > 0) OutputDirTextbox.Text = pickedFolders.First().Path.AbsolutePath;
    }

    private void OutputDirTextbox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (OutputDirTextbox.Text != null) OutputDirectory = OutputDirTextbox.Text;
    }

    private void CreatorUrlTextbox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (CreatorUrlTextbox.Text != null && creatorUrlRegex.IsMatch(CreatorUrlTextbox.Text))
            CreatorURL = CreatorUrlTextbox.Text;
    }

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

    private void PostSubfolderToggle_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        PostSubfolders = (bool)PostSubfolderToggle.IsChecked!;
    }

    private void PostDescriptionsToggle_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        DownloadDescriptions = (bool)PostDescriptionsToggle.IsChecked!;
    }

    private void PostFileTimeToggle_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        OverrideFileTime = (bool)PostFileTimeToggle.IsChecked!;
    }

    private async void ScrapeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (CreatorURL == string.Empty)
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
            var data = ScrapingManager.ScrapeURL(CreatorURL, requestedInfo, NumberOfPosts);
            if (data != null)
            {
                /*
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    downloadProgressBar.Maximum = 100;
                    postsProgressBar.Maximum = data.Content.Count;
                    outputLabel.Text = "Beginning downloader setup...";
                });
                */

                var iteration = 0;
                var downloader = new DownloadManager();
                downloader.DownloadProgressed += Downloader_DownloadProgressed;
                foreach (var scrapeData in data.Content)
                {
                    // Download the attachments
                    var post = (Post)scrapeData.Value;
                    iteration++;

                    /*
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        downloadProgressBar.Value = 0;
                        attachmentsProgressBar.Value = 0;
                        postsProgressBar.PerformStep();
                        attachmentsProgressBar.Maximum = post.Attachments.Count;
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
                        /*
                        Dispatcher.UIThread.InvokeAsync(() => { attachmentsProgressBar.PerformStep(); });
                        */
                        // Attempt to download 5 times
                        for (var i = 0; i < 5; i++)
                        {
                            /*
                            Dispatcher.UIThread.InvokeAsync(() => { downloadProgressBar.Value = 0; });
                            */
                            var success = downloader.DownloadContent(attach.URL, DownloadDir, attach.Name,
                                connections: 5);
                            if (success)
                            {
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

                            LoggingManager.LogWarning(
                                $"Attachment \"{attach.Name}\" from URL \"{attach.URL}\" failed to download, retrying! [{i + 1}/5]");
                            if (File.Exists(Path.Combine(DownloadDir, attach.Name)))
                                File.Delete(Path.Combine(DownloadDir, attach.Name));
                            var rng = new Random();
                            // 15-60 seconds
                            var waittime = rng.Next(15000, 60000);
                            Thread.Sleep(waittime);
                            LoggingManager.LogInformation(
                                $"Waited {(int)Math.Floor((decimal)(waittime / 1000))} seconds, continuing...");
                        }

                    if (OverrideFileTime)
                    {
                        Directory.SetCreationTime(DownloadDir, (DateTime)post.UploadDate);
                        Directory.SetLastAccessTime(DownloadDir, (DateTime)post.UploadDate);
                        Directory.SetLastWriteTime(DownloadDir, (DateTime)post.UploadDate);
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

            /*
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                downloadProgressBar.Value = 0;
                attachmentsProgressBar.Value = 0;
                postsProgressBar.Value = 0;
                outputProgress.Visible = false;
                outputLabel.Text = "Idle...";
            });
            */
            Dispatcher.UIThread.InvokeAsync(EnableBoxes);
        }).Start();
    }

    private void Downloader_DownloadProgressed(object sender, DownloadProgressChangedEventArgs eventargs,
        string filename)
    {
        // Write progress bar updates here
    }

    private void Control_OnUnloaded(object? sender, RoutedEventArgs e)
    {
        ScrapingManager.FlushSupplementaryMethods();
    }
}