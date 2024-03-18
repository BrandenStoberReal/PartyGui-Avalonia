using System;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

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

    private void Control_OnLoaded(object? sender, RoutedEventArgs e)
    {
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
}