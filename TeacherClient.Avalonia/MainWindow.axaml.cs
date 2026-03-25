using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Teacher.Common.Contracts;
using TeacherClient.CrossPlatform.Dialogs;
using TeacherClient.CrossPlatform.Services;

namespace TeacherClient.CrossPlatform;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ProcessInfoDto> _processes = [];
    private readonly ObservableCollection<FileSystemEntryDto> _localEntries = [];
    private readonly ObservableCollection<FileSystemEntryDto> _remoteEntries = [];
    private string? _remoteParentPath;

    public MainWindow()
    {
        InitializeComponent();

        ServerUrlTextBox.Text = "http://127.0.0.1:5055";
        SharedSecretTextBox.Text = "change-this-secret";
        ProcessesGrid.ItemsSource = _processes;
        LocalFilesGrid.ItemsSource = _localEntries;
        RemoteFilesGrid.ItemsSource = _remoteEntries;
        LocalPathTextBox.Text = GetDefaultLocalPath();
    }

    private TeacherApiClient CreateClient() => new(ServerUrlTextBox.Text?.Trim() ?? string.Empty, SharedSecretTextBox.Text?.Trim() ?? string.Empty);

    private async void ConnectButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RunBusyAsync(async () =>
        {
            var client = CreateClient();
            var info = await client.GetServerInfoAsync();
            if (info is null)
            {
                SetStatus("Connection failed.");
                return;
            }

            SetStatus($"Connected to {info.MachineName} ({info.CurrentUser})");
            await LoadProcessesAsync();
            await LoadLocalDirectoryAsync(LocalPathTextBox.Text);
            await LoadRemoteDirectoryAsync(RemotePathTextBox.Text);
        });
    }

    private async void RefreshProcessesButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await LoadProcessesAsync();

    private async void KillProcessButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ProcessesGrid.SelectedItem is not ProcessInfoDto process)
        {
            SetStatus("Choose a process first.");
            return;
        }

        if (!await ConfirmationDialog.ShowAsync(this, "Terminate Process", $"Terminate process {process.Name} ({process.Id})?"))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var client = CreateClient();
            await client.KillProcessAsync(process.Id);
            await LoadProcessesAsync();
            SetStatus($"Process {process.Name} terminated");
        });
    }

    private async Task LoadProcessesAsync()
    {
        await RunBusyAsync(async () =>
        {
            var client = CreateClient();
            var processes = await client.GetProcessesAsync();
            ReplaceItems(_processes, processes);
            SetStatus($"Loaded {processes.Count} processes");
        }, "Process load error");
    }

    private async void RefreshFilesButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await LoadLocalDirectoryAsync(LocalPathTextBox.Text);
        await LoadRemoteDirectoryAsync(RemotePathTextBox.Text);
        SetStatus("Panels refreshed");
    }

    private async Task LoadLocalDirectoryAsync(string? path)
    {
        await RunBusyAsync(() =>
        {
            var resolvedPath = string.IsNullOrWhiteSpace(path) ? GetDefaultLocalPath() : path!;
            var info = new DirectoryInfo(resolvedPath);
            var entries = info.EnumerateFileSystemInfos()
                .OrderByDescending(x => (x.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(MapLocalEntry)
                .ToList();

            LocalPathTextBox.Text = info.FullName;
            ReplaceItems(_localEntries, entries);
            return Task.CompletedTask;
        }, "Local browse error");
    }

    private async Task LoadRemoteDirectoryAsync(string? path)
    {
        await RunBusyAsync(async () =>
        {
            var client = CreateClient();
            var listing = await client.GetRemoteDirectoryAsync(path);
            if (listing is null)
            {
                SetStatus("Remote listing failed.");
                return;
            }

            RemotePathTextBox.Text = listing.CurrentPath;
            _remoteParentPath = listing.ParentPath;
            ReplaceItems(_remoteEntries, listing.Entries);
        }, "Remote browse error");
    }

    private async void UploadButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (LocalFilesGrid.SelectedItem is not FileSystemEntryDto entry || entry.IsDirectory)
        {
            SetStatus("Choose a local file to upload.");
            return;
        }

        await RunBusyAsync(async () =>
        {
            var client = CreateClient();
            await client.UploadFileAsync(entry.FullPath, RemotePathTextBox.Text ?? string.Empty);
            await LoadRemoteDirectoryAsync(RemotePathTextBox.Text);
            SetStatus($"Uploaded {entry.Name}");
        }, "Upload error");
    }

    private async void DownloadButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (RemoteFilesGrid.SelectedItem is not FileSystemEntryDto entry || entry.IsDirectory)
        {
            SetStatus("Choose a remote file to download.");
            return;
        }

        await RunBusyAsync(async () =>
        {
            var client = CreateClient();
            await client.DownloadRemoteFileAsync(entry.FullPath, LocalPathTextBox.Text ?? GetDefaultLocalPath());
            await LoadLocalDirectoryAsync(LocalPathTextBox.Text);
            SetStatus($"Downloaded {entry.Name}");
        }, "Download error");
    }

    private async void DeleteLocalButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (LocalFilesGrid.SelectedItem is not FileSystemEntryDto entry)
        {
            SetStatus("Choose a local entry first.");
            return;
        }

        if (!await ConfirmationDialog.ShowAsync(this, "Delete Local Entry", $"Delete local entry {entry.Name}?"))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            if (entry.IsDirectory)
            {
                Directory.Delete(entry.FullPath, recursive: true);
            }
            else
            {
                File.Delete(entry.FullPath);
            }

            await LoadLocalDirectoryAsync(LocalPathTextBox.Text);
            SetStatus($"Deleted local entry {entry.Name}");
        }, "Local delete error");
    }

    private async void DeleteRemoteButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (RemoteFilesGrid.SelectedItem is not FileSystemEntryDto entry)
        {
            SetStatus("Choose a remote entry first.");
            return;
        }

        if (!await ConfirmationDialog.ShowAsync(this, "Delete Remote Entry", $"Delete remote entry {entry.Name}?"))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var client = CreateClient();
            await client.DeleteRemoteEntryAsync(entry.FullPath);
            await LoadRemoteDirectoryAsync(RemotePathTextBox.Text);
            SetStatus($"Deleted remote entry {entry.Name}");
        }, "Remote delete error");
    }

    private async void NewRemoteFolderButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var folderName = await TextInputDialog.ShowAsync(this, "Create Remote Folder", "Folder name:", "NewFolder");
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var client = CreateClient();
            await client.CreateRemoteDirectoryAsync(RemotePathTextBox.Text ?? string.Empty, folderName);
            await LoadRemoteDirectoryAsync(RemotePathTextBox.Text);
            SetStatus($"Created remote folder {folderName}");
        }, "Create folder error");
    }

    private async void UpLocalButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var current = LocalPathTextBox.Text;
        if (string.IsNullOrWhiteSpace(current))
        {
            return;
        }

        var parent = Directory.GetParent(current)?.FullName;
        if (!string.IsNullOrWhiteSpace(parent))
        {
            await LoadLocalDirectoryAsync(parent);
        }
    }

    private async void UpRemoteButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_remoteParentPath))
        {
            await LoadRemoteDirectoryAsync(_remoteParentPath);
        }
    }

    private async void LocalFilesGrid_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (LocalFilesGrid.SelectedItem is FileSystemEntryDto entry && entry.IsDirectory)
        {
            await LoadLocalDirectoryAsync(entry.FullPath);
        }
    }

    private async void RemoteFilesGrid_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (RemoteFilesGrid.SelectedItem is FileSystemEntryDto entry && entry.IsDirectory)
        {
            await LoadRemoteDirectoryAsync(entry.FullPath);
        }
    }

    private async void AboutMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow();
        await aboutWindow.ShowDialog(this);
    }

    private static FileSystemEntryDto MapLocalEntry(FileSystemInfo entry)
    {
        return new FileSystemEntryDto(
            entry.Name,
            entry.FullName,
            (entry.Attributes & FileAttributes.Directory) == FileAttributes.Directory,
            entry is FileInfo fileInfo ? fileInfo.Length : null,
            entry.LastWriteTimeUtc);
    }

    private async Task RunBusyAsync(Func<Task> operation, string? errorPrefix = null)
    {
        var previousCursor = Cursor;
        try
        {
            Cursor = new Cursor(StandardCursorType.Wait);
            await operation();
        }
        catch (Exception ex)
        {
            SetStatus(errorPrefix is null ? ex.Message : $"{errorPrefix}: {ex.Message}");
        }
        finally
        {
            Cursor = previousCursor;
        }
    }

    private void SetStatus(string text)
    {
        StatusTextBlock.Text = text;
    }

    private static void ReplaceItems<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private static string GetDefaultLocalPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home) && Directory.Exists(home))
        {
            return home;
        }

        return Directory.GetCurrentDirectory();
    }
}
