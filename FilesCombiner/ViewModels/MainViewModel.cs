using CommunityToolkit.Maui.Storage;
using FilesCombiner.Models;
using FilesCombiner.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace FilesCombiner.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private string _rootDirectory = string.Empty;
    private string _outputDirectory = string.Empty;
    private string _whitelistPattern = "*";
    private string _blacklistPattern = string.Empty;
    private bool _isProcessing;
    private string _statusMessage = string.Empty;
    private ObservableCollection<string> _processedFiles = new();
    private AppSettings _settings = new();

    public string RootDirectory
    {
        get => _rootDirectory;
        set { _rootDirectory = value; OnPropertyChanged(); }
    }

    public string OutputDirectory
    {
        get => _outputDirectory;
        set { _outputDirectory = value; OnPropertyChanged(); }
    }

    public string WhitelistPattern
    {
        get => _whitelistPattern;
        set { _whitelistPattern = value; OnPropertyChanged(); }
    }

    public string BlacklistPattern
    {
        get => _blacklistPattern;
        set { _blacklistPattern = value; OnPropertyChanged(); }
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        set { _isProcessing = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public ObservableCollection<string> ProcessedFiles
    {
        get => _processedFiles;
        set { _processedFiles = value; OnPropertyChanged(); }
    }

    public Command SelectRootDirectoryCommand { get; }
    public Command SelectOutputDirectoryCommand { get; }
    public Command CombineFilesCommand { get; }
    public Command SaveSettingsCommand { get; }
    public Command LoadSettingsCommand { get; }

    public MainViewModel()
    {
        SelectRootDirectoryCommand = new Command(async () => await SelectRootDirectory());
        SelectOutputDirectoryCommand = new Command(async () => await SelectOutputDirectory());
        CombineFilesCommand = new Command(async () => await CombineFiles());
        SaveSettingsCommand = new Command(async () => await SaveSettings());
        LoadSettingsCommand = new Command(async () => await LoadSettings());

        Task.Run(async () => await LoadSettings());
    }

    private async Task SelectRootDirectory()
    {
        try
        {
            var result = await FolderPicker.Default.PickAsync();
            if (result != null && result.IsSuccessful)
            {
                RootDirectory = result.Folder.Path;
                StatusMessage = $"Root directory selected: {RootDirectory}";
            }
        }
        catch (Exception ex)
        {
            await ShowAlert("Error", $"Failed to select directory: {ex.Message}");
        }
    }

    private async Task SelectOutputDirectory()
    {
        try
        {
            var result = await FolderPicker.Default.PickAsync();
            if (result != null && result.IsSuccessful)
            {
                OutputDirectory = result.Folder.Path;
                StatusMessage = $"Output directory selected: {OutputDirectory}";
            }
        }
        catch (Exception ex)
        {
            await ShowAlert("Error", $"Failed to select directory: {ex.Message}");
        }
    }

    private async Task CombineFiles()
    {
        if (string.IsNullOrWhiteSpace(RootDirectory))
        {
            await ShowAlert("Error", "Please select a root directory first.");
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            await ShowAlert("Error", "Please select an output directory first.");
            return;
        }

        if (!Directory.Exists(RootDirectory))
        {
            await ShowAlert("Error", "Root directory does not exist.");
            return;
        }

        if (!Directory.Exists(OutputDirectory))
        {
            await ShowAlert("Error", "Output directory does not exist.");
            return;
        }

        IsProcessing = true;
        ProcessedFiles.Clear();
        StatusMessage = "Processing files...";

        try
        {
            await Task.Run(() =>
            {
                var rootFolderName = new DirectoryInfo(RootDirectory).Name;
                var outputFilePath = Path.Combine(OutputDirectory, $"{rootFolderName}_context.txt");
                var whitelist = string.IsNullOrWhiteSpace(WhitelistPattern) ? "*" : WhitelistPattern;
                var blacklistMatcher = new BlacklistMatcher(BlacklistPattern);

                // Build the directory tree with filtered files and directories
                var directoryTree = BuildDirectoryTree(RootDirectory, whitelist, blacklistMatcher);
                var allFiles = GetAllFilesFromTree(directoryTree);

                using (var writer = new StreamWriter(outputFilePath, false, Encoding.UTF8))
                {
                    // Write header
                    //writer.WriteLine($"Files Combined Report");
                    //writer.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    //writer.WriteLine($"Root Directory: {RootDirectory}");
                    //writer.WriteLine($"Whitelist Pattern: {whitelist}");
                    //writer.WriteLine($"Blacklist Pattern:");
                    //writer.WriteLine(BlacklistPattern);
                    //writer.WriteLine($"Total Files Processed: {allFiles.Count}");
                    //writer.WriteLine(new string('=', 80));
                    //writer.WriteLine();

                    // Write the directory tree structure
                    writer.WriteLine("Project Structure:");
                    writer.WriteLine(new string('=', 80));
                    WriteDirectoryTree(writer, directoryTree, "", true);
                    writer.WriteLine();
                    writer.WriteLine(new string('=', 80));
                    writer.WriteLine();

                    // Write file contents
                    writer.WriteLine("File Contents:");
                    writer.WriteLine(new string('=', 80));

                    foreach (var file in allFiles)
                    {
                        try
                        {
                            var relativePath = file.RelativePath;
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                ProcessedFiles.Add(relativePath);
                            });

                            writer.WriteLine($"File: {relativePath}");
                            writer.WriteLine(new string('-', 60));
                            writer.WriteLine($"Path: {file.FullPath}");
                            writer.WriteLine($"Size: {file.Size:N0} bytes");
                            writer.WriteLine($"Last Modified: {file.LastModified:yyyy-MM-dd HH:mm:ss}");
                            writer.WriteLine(new string('-', 60));

                            var content = File.ReadAllText(file.FullPath, Encoding.UTF8);
                            writer.WriteLine(content);
                            writer.WriteLine();
                            writer.WriteLine(new string('-', 60));
                            writer.WriteLine();
                        }
                        catch (Exception ex)
                        {
                            writer.WriteLine($"Error reading file {file.FullPath}: {ex.Message}");
                            writer.WriteLine();
                        }
                    }
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    StatusMessage = $"Successfully combined {allFiles.Count} files into: {outputFilePath}";
                    ShowAlert("Success", $"Files combined successfully!\nOutput file: {outputFilePath}");
                });
            });
        }
        catch (Exception ex)
        {
            await ShowAlert("Error", $"Failed to combine files: {ex.Message}");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    // Helper classes for directory tree structure
    private class DirectoryNode
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public List<DirectoryNode> Subdirectories { get; set; } = new();
        public List<FileNode> Files { get; set; } = new();
    }

    private class FileNode
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
    }

    // Recursively build the directory tree with blacklist matching
    private DirectoryNode BuildDirectoryTree(string directoryPath, string whitelist, BlacklistMatcher blacklistMatcher)
    {
        var directoryInfo = new DirectoryInfo(directoryPath);
        var relativePath = Path.GetRelativePath(RootDirectory, directoryInfo.FullName);

        // Use empty string for root directory
        var checkPath = relativePath == "." ? "" : relativePath.Replace('\\', '/');

        // Check if this directory should be blacklisted
        if (blacklistMatcher.IsBlacklisted(checkPath, true))
        {
            // Skip this directory entirely
            return null;
        }

        var node = new DirectoryNode
        {
            Name = directoryInfo.Name,
            FullPath = directoryInfo.FullName
        };

        try
        {
            // Get all subdirectories recursively
            var subdirectories = directoryInfo.GetDirectories();
            foreach (var subDir in subdirectories)
            {
                var subNode = BuildDirectoryTree(subDir.FullName, whitelist, blacklistMatcher);
                if (subNode != null && (subNode.Subdirectories.Any() || subNode.Files.Any()))
                {
                    node.Subdirectories.Add(subNode);
                }
            }

            // Get files in current directory
            var files = directoryInfo.GetFiles();
            foreach (var file in files)
            {
                var fileName = file.Name;
                var matchesWhitelist = IsMatch(fileName, whitelist);

                if (matchesWhitelist)
                {
                    var fileRelativePath = Path.GetRelativePath(RootDirectory, file.FullName).Replace('\\', '/');

                    // Check if file is blacklisted
                    if (!blacklistMatcher.IsBlacklisted(fileRelativePath, false))
                    {
                        node.Files.Add(new FileNode
                        {
                            Name = file.Name,
                            FullPath = file.FullName,
                            RelativePath = fileRelativePath,
                            Size = file.Length,
                            LastModified = file.LastWriteTime
                        });
                    }
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directories we can't access
        }
        catch (Exception)
        {
            // Skip other errors
        }

        return node;
    }

    // Get all files from the tree (flattened list)
    private List<FileNode> GetAllFilesFromTree(DirectoryNode node)
    {
        var files = new List<FileNode>();
        files.AddRange(node.Files);

        foreach (var subDir in node.Subdirectories)
        {
            files.AddRange(GetAllFilesFromTree(subDir));
        }

        return files;
    }

    // Write the directory tree structure
    private void WriteDirectoryTree(StreamWriter writer, DirectoryNode node, string indent, bool isLast)
    {
        // Skip the root directory name if we're at the root
        if (!string.IsNullOrEmpty(indent))
        {
            writer.Write(indent);
            writer.Write(isLast ? "└───" : "├───");
            writer.WriteLine(node.Name);
        }

        // Update indent for children
        var newIndent = indent + (isLast ? "    " : "│   ");

        // Write all files in this directory
        for (int i = 0; i < node.Files.Count; i++)
        {
            var file = node.Files[i];
            var isLastFile = (i == node.Files.Count - 1) && (node.Subdirectories.Count == 0);

            writer.Write(newIndent);
            writer.Write(isLastFile ? "└───" : "├───");
            writer.WriteLine(file.Name);
        }

        // Write all subdirectories
        for (int i = 0; i < node.Subdirectories.Count; i++)
        {
            var subDir = node.Subdirectories[i];
            var isLastDir = (i == node.Subdirectories.Count - 1);
            WriteDirectoryTree(writer, subDir, newIndent, isLastDir);
        }
    }

    // Helper method for wildcard matching
    private bool IsMatch(string fileName, string pattern)
    {
        if (pattern == "*") return true;

        var patternParts = pattern.Split(';', StringSplitOptions.RemoveEmptyEntries);
        return patternParts.Any(part => IsWildcardMatch(fileName, part.Trim()));
    }

    private bool IsWildcardMatch(string input, string pattern)
    {
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        return System.Text.RegularExpressions.Regex.IsMatch(input, regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private async Task SaveSettings()
    {
        try
        {
            _settings.WhitelistPattern = WhitelistPattern;
            _settings.BlacklistPattern = BlacklistPattern;
            await _settings.SaveAsync();
            await ShowAlert("Success", "Settings saved successfully!");
        }
        catch (Exception ex)
        {
            await ShowAlert("Error", $"Failed to save settings: {ex.Message}");
        }
    }

    private async Task LoadSettings()
    {
        try
        {
            _settings = await AppSettings.LoadAsync();
            WhitelistPattern = _settings.WhitelistPattern ?? "*";
            BlacklistPattern = _settings.BlacklistPattern ?? string.Empty;
            StatusMessage = "Settings loaded successfully";
        }
        catch (Exception ex)
        {
            await ShowAlert("Error", $"Failed to load settings: {ex.Message}");
        }
    }

    private async Task ShowAlert(string title, string message)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Application.Current?.MainPage?.DisplayAlert(title, message, "OK");
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}