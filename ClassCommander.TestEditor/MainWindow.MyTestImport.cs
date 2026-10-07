using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ClassCommander.TestEditor.Localization;
using ClassCommander.Testing.Core.Import;

namespace ClassCommander.TestEditor;

public partial class MainWindow
{
    private async Task ImportMtfAsync(string sourcePath)
    {
        var output = Path.Combine(Path.GetTempPath(), "ClassCommander", "mtf-preview", Guid.NewGuid().ToString("N"));
        IsEnabled = false;
        StatusTextBlock.Text = TestEditorText.ImportProgress(1, 1);
        try
        {
            var result = await Task.Run(() => new MyTestBatchConverter().ConvertAsync(sourcePath, output));
            if (!result.Succeeded)
            {
                IsEnabled = true;
                await ShowImportReportAsync(TestEditorText.ImportError(result.ErrorCode) + "\n" + result.Detail + "\n" + string.Join("\n", result.Warnings));
                return;
            }
            await LoadPackageAsync(result.PackagePath!);
            _packagePath = null;
        }
        finally
        {
            IsEnabled = true;
            try
            { if (Directory.Exists(output)) Directory.Delete(output, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private async void BatchMyTestMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = TestEditorText.BatchSourceTitle,
            AllowMultiple = true,
            FileTypeFilter = [new FilePickerFileType("MyTestX") { Patterns = ["*.xml", "*.mtf"] }],
        });
        if (files.Count == 0)
        {
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = TestEditorText.BatchOutputTitle,
            AllowMultiple = false,
        });
        if (folders.Count == 0)
        {
            return;
        }

        using var cancellation = new CancellationTokenSource();
        var output = folders[0].Path.LocalPath;
        var report = new StringBuilder(TestEditorText.ImportLimitations).AppendLine().AppendLine();
        var text = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var button = new Button { Content = TestEditorText.ImportCancel, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        var panel = new DockPanel { Margin = new Thickness(16), LastChildFill = true };
        DockPanel.SetDock(button, Dock.Bottom);
        panel.Children.Add(button);
        panel.Children.Add(text);
        var dialog = new Window
        {
            Title = TestEditorText.ImportReportTitle,
            Width = 760,
            Height = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = panel,
        };
        var running = true;
        button.Click += (_, _) =>
        {
            if (running)
            {
                cancellation.Cancel();
                button.IsEnabled = false;
            }
            else
            {
                dialog.Close();
            }
        };
        dialog.Closing += (_, args) =>
        {
            if (!running)
            {
                return;
            }

            args.Cancel = true;
            cancellation.Cancel();
            button.IsEnabled = false;
        };
        dialog.Opened += async (_, _) =>
        {
            try
            {
                var converter = new MyTestBatchConverter();
                for (var i = 0; i < files.Count; i++)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    dialog.Title = TestEditorText.ImportProgress(i + 1, files.Count);
                    var path = files[i].Path.LocalPath;
                    var result = await Task.Run(() => converter.ConvertAsync(path, output, cancellation.Token));
                    report.AppendLine(Path.GetFileName(path));
                    report.AppendLine(result.Succeeded
                        ? TestEditorText.ImportSuccess(result.QuestionCount, result.PackagePath!)
                        : TestEditorText.ImportError(result.ErrorCode));
                    if (!string.IsNullOrWhiteSpace(result.Detail))
                    {
                        report.AppendLine(result.Detail);
                    }

                    foreach (var warning in result.Warnings)
                    {
                        report.AppendLine(warning);
                    }

                    report.AppendLine();
                    text.Text = report.ToString();
                }
            }
            catch (OperationCanceledException)
            {
                report.AppendLine(TestEditorText.ImportCancelled);
            }
            catch (Exception)
            {
                report.AppendLine(TestEditorText.ImportError("conversion-failed"));
            }
            finally
            {
                try
                {
                    var reportPath = Path.Combine(output, $"mytest-import-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.txt");
                    await File.WriteAllTextAsync(reportPath, report.ToString());
                    report.AppendLine(TestEditorText.ImportReportSaved(reportPath));
                }
                catch (Exception)
                {
                    report.AppendLine(TestEditorText.ImportError("conversion-failed"));
                }

                running = false;
                dialog.Title = TestEditorText.ImportReportTitle;
                text.Text = report.ToString();
                button.Content = TestEditorText.ImportClose;
                button.IsEnabled = true;
            }
        };
        await dialog.ShowDialog(this);
    }

    private async Task ShowImportReportAsync(string report)
    {
        var close = new Button { Content = TestEditorText.ImportClose };
        var panel = new DockPanel { Margin = new Thickness(16) };
        DockPanel.SetDock(close, Dock.Bottom);
        panel.Children.Add(close);
        panel.Children.Add(new TextBox
        {
            Text = TestEditorText.ImportLimitations + "\n\n" + report,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });
        var dialog = new Window
        {
            Title = TestEditorText.ImportReportTitle,
            Width = 700,
            Height = 440,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = panel,
        };
        close.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
    }
}
