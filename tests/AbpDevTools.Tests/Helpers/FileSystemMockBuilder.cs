using System.Collections.ObjectModel;
using NSubstitute;

namespace AbpDevTools.Tests.Helpers;

/// <summary>
/// Builder helper for creating mocked IFileSystem and related file system objects.
/// Provides fluent interface for setting up common file system operations.
/// </summary>
public class FileSystemMockBuilder
{
    private readonly Dictionary<string, string> _files = new();
    private readonly HashSet<string> _directories = new();
    private readonly Dictionary<string, string[]> _directoryFiles = new();
    private readonly List<string> _existingPaths = new();

    /// <summary>
    /// Creates a new FileSystemMockBuilder instance.
    /// </summary>
    public static FileSystemMockBuilder Create() => new();

    /// <summary>
    /// Adds a file that exists in the mocked file system.
    /// </summary>
    public FileSystemMockBuilder WithFile(string path, string content = "")
    {
        _files[path] = content;
        _existingPaths.Add(path);
        return this;
    }

    /// <summary>
    /// Adds multiple files that exist in the mocked file system.
    /// </summary>
    public FileSystemMockBuilder WithFiles(Dictionary<string, string> files)
    {
        foreach (var file in files)
        {
            _files[file.Key] = file.Value;
            _existingPaths.Add(file.Key);
        }
        return this;
    }

    /// <summary>
    /// Adds a directory that exists in the mocked file system.
    /// </summary>
    public FileSystemMockBuilder WithDirectory(string path)
    {
        _directories.Add(path);
        _existingPaths.Add(path);
        return this;
    }

    /// <summary>
    /// Adds multiple directories that exist in the mocked file system.
    /// </summary>
    public FileSystemMockBuilder WithDirectories(params string[] paths)
    {
        foreach (var path in paths)
        {
            _directories.Add(path);
            _existingPaths.Add(path);
        }
        return this;
    }

    /// <summary>
    /// Sets up files that will be found when enumerating a directory.
    /// </summary>
    public FileSystemMockBuilder WithDirectoryFiles(string directoryPath, params string[] files)
    {
        _directoryFiles[directoryPath] = files;
        return this;
    }

    /// <summary>
    /// Builds a mock IFileSystem (requires System.IO.Abstractions or similar).
    /// For now, returns the internal state for use with direct mocking.
    /// </summary>
    public FileSystemState Build()
    {
        return new FileSystemState(
            new Dictionary<string, string>(_files),
            new HashSet<string>(_directories),
            new Dictionary<string, string[]>(_directoryFiles),
            new List<string>(_existingPaths)
        );
    }

    /// <summary>
    /// Builds NSubstitute mocks for file operations.
    /// Use these mocks when testing code that uses file system operations.
    /// </summary>
    public FileMocks BuildMocks()
    {
        var state = Build();
        return new FileMocks(state);
    }
}

/// <summary>
/// Represents the state of a mocked file system for test assertions.
/// </summary>
public class FileSystemState
{
    public Dictionary<string, string> Files { get; }
    public HashSet<string> Directories { get; }
    public Dictionary<string, string[]> DirectoryFiles { get; }
    public List<string> ExistingPaths { get; }

    public FileSystemState(
        Dictionary<string, string> files,
        HashSet<string> directories,
        Dictionary<string, string[]> directoryFiles,
        List<string> existingPaths)
    {
        Files = files;
        Directories = directories;
        DirectoryFiles = directoryFiles;
        ExistingPaths = existingPaths;
    }

    /// <summary>
    /// Checks if a file exists in this file system state.
    /// </summary>
    public bool FileExists(string path) => Files.ContainsKey(path);

    /// <summary>
    /// Gets the content of a file in this file system state.
    /// </summary>
    public string? GetFileContent(string path) =>
        Files.TryGetValue(path, out var content) ? content : null;

    /// <summary>
    /// Checks if a directory exists in this file system state.
    /// </summary>
    public bool DirectoryExists(string path) => Directories.Contains(path);
}

/// <summary>
/// Contains mock delegates for file system operations.
/// Use with NSubstitute to mock file operations in your tests.
/// </summary>
public class FileMocks
{
    private readonly FileSystemState _state;

    public FileMocks(FileSystemState state)
    {
        _state = state;
        FileExistsMock = path => _state.Files.ContainsKey(path);
        DirectoryExistsMock = path => _state.Directories.Contains(path);
        ReadAllTextMock = path =>
            _state.Files.TryGetValue(path, out var content) ? content : throw new FileNotFoundException($"File not found: {path}");
        WriteAllTextMock = (path, content) => { _state.Files[path] = content; };
        EnumerateFilesMock = (path, pattern, searchOption) =>
            _state.DirectoryFiles.TryGetValue(path, out var files) ? files : Array.Empty<string>();
    }

    /// <summary>
    /// Mock delegate for File.Exists.
    /// </summary>
    public Func<string, bool> FileExistsMock { get; }

    /// <summary>
    /// Mock delegate for Directory.Exists.
    /// </summary>
    public Func<string, bool> DirectoryExistsMock { get; }

    /// <summary>
    /// Mock delegate for File.ReadAllText.
    /// </summary>
    public Func<string, string> ReadAllTextMock { get; }

    /// <summary>
    /// Mock delegate for File.WriteAllText.
    /// </summary>
    public Action<string, string> WriteAllTextMock { get; }

    /// <summary>
    /// Mock delegate for Directory.EnumerateFiles.
    /// </summary>
    public Func<string, string, SearchOption, IEnumerable<string>> EnumerateFilesMock { get; }

    /// <summary>
    /// Creates a mock for FileExplorer's FindDescendants method.
    /// </summary>
    public IEnumerable<string> FindDescendantsMock(string path, string pattern, string[]? excludeFolders = null)
    {
        if (_state.DirectoryFiles.TryGetValue(path, out var files))
        {
            var result = files.AsEnumerable();
            if (excludeFolders != null && excludeFolders.Length > 0)
            {
                result = result.Where(f => !excludeFolders.Any(f.Contains));
            }
            return result;
        }
        return Enumerable.Empty<string>();
    }
}
