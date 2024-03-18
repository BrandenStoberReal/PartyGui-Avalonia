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

    private int NumberOfPosts { get; set; } = 0;

    private bool PostSubfolders { get; set; } = true;
    private bool DownloadDescriptions { get; set; } = false;
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
        if (pickedFolders.Count > 0) OutputDirTextbox.Text = pickedFolders.FirstOrDefault().Path.AbsolutePath;
    }

    private void OutputDirTextbox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        OutputDirectory = OutputDirTextbox.Text;
    }
}