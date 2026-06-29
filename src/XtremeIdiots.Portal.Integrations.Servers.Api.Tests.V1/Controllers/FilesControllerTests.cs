using System.Net;
using System.Text;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MX.Api.Abstractions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Files;
using XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Controllers;

[Trait("Category", "Unit")]
public class FilesControllerTests
{
    private readonly Mock<ILogger<FilesController>> _mockLogger = new();
    private readonly Mock<IGameServerFileTransportFactory> _mockFileTransportFactory = new();
    private readonly TelemetryClient _telemetryClient;

    public FilesControllerTests()
    {
        var telemetryConfig = new TelemetryConfiguration { TelemetryChannel = new Mock<ITelemetryChannel>().Object };
        _telemetryClient = new TelemetryClient(telemetryConfig);
    }

    private FilesController CreateController() => new(
        _mockLogger.Object,
        _mockFileTransportFactory.Object,
        _telemetryClient);

    [Fact]
    public async Task PutContent_WhenFileDoesNotExist_ReturnsCreated()
    {
        var gameServerId = Guid.NewGuid();
        var session = new TestFileTransportSession(files: [], directories: ["/cfg"]);
        SetupSession(gameServerId, session);

        var controller = CreateController();
        var result = await controller.PutContent(gameServerId, new PutFileContentRequestDto
        {
            Path = "/cfg/server.cfg",
            Mode = FileContentMode.Text,
            TextContent = "set sv_hostname XI",
        });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objectResult.StatusCode);

        var payload = Assert.IsType<ApiResponse<FileMutationResultDto>>(objectResult.Value);
        Assert.Equal(FileMutationOperation.Put, payload.Data?.Operation);
        Assert.Equal(FileMutationOutcome.Created, payload.Data?.Outcome);
    }

    [Fact]
    public async Task PutContent_WhenParentDirectoryDoesNotExistAndCreateParentsDisabled_ReturnsNotFound()
    {
        var gameServerId = Guid.NewGuid();
        var session = new TestFileTransportSession();
        SetupSession(gameServerId, session);

        var controller = CreateController();
        var result = await controller.PutContent(gameServerId, new PutFileContentRequestDto
        {
            Path = "/cfg/sub/server.cfg",
            Mode = FileContentMode.Text,
            TextContent = "set sv_hostname XI",
            CreateParentDirectories = false,
        });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, objectResult.StatusCode);
    }

    [Fact]
    public async Task PutContent_WhenCreateParentsEnabled_CreatesParentDirectories()
    {
        var gameServerId = Guid.NewGuid();
        var session = new TestFileTransportSession();
        SetupSession(gameServerId, session);

        var controller = CreateController();
        var result = await controller.PutContent(gameServerId, new PutFileContentRequestDto
        {
            Path = "/cfg/sub/server.cfg",
            Mode = FileContentMode.Text,
            TextContent = "set sv_hostname XI",
            CreateParentDirectories = true,
        });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objectResult.StatusCode);
        Assert.True(await session.DirectoryExists("/cfg/sub"));
    }

    [Fact]
    public async Task DeleteContent_WhenPathIsDirectory_ReturnsConflict()
    {
        var gameServerId = Guid.NewGuid();
        var session = new TestFileTransportSession(directories: ["/cfg"]);
        SetupSession(gameServerId, session);

        var controller = CreateController();
        var result = await controller.DeleteContent(gameServerId, new DeleteFileQueryDto { Path = "/cfg" });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, objectResult.StatusCode);
    }

    [Fact]
    public async Task DeleteContent_WhenPathIsMissing_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();
        var controller = CreateController();

        var result = await controller.DeleteContent(gameServerId, new DeleteFileQueryDto { Path = null });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
    }

    [Fact]
    public async Task CreateDirectory_WhenDirectoryAlreadyExistsAndIfNotExistsTrue_ReturnsAlreadyExists()
    {
        var gameServerId = Guid.NewGuid();
        var session = new TestFileTransportSession(directories: ["/cfg"]);
        SetupSession(gameServerId, session);

        var controller = CreateController();
        var result = await controller.CreateDirectory(gameServerId, new CreateDirectoryRequestDto
        {
            Path = "/cfg",
            IfNotExists = true,
        });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);

        var payload = Assert.IsType<ApiResponse<FileMutationResultDto>>(objectResult.Value);
        Assert.Equal(FileMutationOutcome.AlreadyExists, payload.Data?.Outcome);
    }

    [Fact]
    public async Task DeleteDirectory_WhenRecursiveIsFalse_ReturnsConflict()
    {
        var gameServerId = Guid.NewGuid();
        var controller = CreateController();

        var result = await controller.DeleteDirectory(gameServerId, new DeleteDirectoryQueryDto
        {
            Path = "/cfg",
            Recursive = false,
        });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, objectResult.StatusCode);

        _mockFileTransportFactory.Verify(x => x.CreateSession(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteDirectory_WhenPathIsMissing_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();
        var controller = CreateController();

        var result = await controller.DeleteDirectory(gameServerId, new DeleteDirectoryQueryDto
        {
            Path = null,
            Recursive = true,
        });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
    }

    [Fact]
    public async Task DeleteDirectory_WhenPathIsRoot_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();
        var controller = CreateController();

        var result = await controller.DeleteDirectory(gameServerId, new DeleteDirectoryQueryDto
        {
            Path = "/",
            Recursive = true,
        });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
    }

    [Fact]
    public async Task ListEntries_WhenRecursiveAndPaged_ReturnsPageAndContinuationToken()
    {
        var gameServerId = Guid.NewGuid();
        var session = new TestFileTransportSession(
            files: ["/cfg/a.txt", "/cfg/sub/b.txt"],
            directories: ["/cfg", "/cfg/sub"]);
        SetupSession(gameServerId, session);

        var controller = CreateController();
        var result = await controller.ListEntries(gameServerId, new ListEntriesQueryDto
        {
            Path = "/cfg",
            Recursive = true,
            PageSize = 1,
        });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        var payload = Assert.IsType<ApiResponse<FileEntriesCollectionDto>>(objectResult.Value);
        Assert.NotNull(payload.Data);
        Assert.Single(payload.Data.Items);
        Assert.Equal("1", payload.Data.ContinuationToken);
    }

    [Fact]
    public async Task ListEntries_WhenRecursiveWithDuplicateNames_PaginationIsDeterministic()
    {
        var gameServerId = Guid.NewGuid();
        var session = new TestFileTransportSession(
            files: ["/cfg/alpha/a.txt", "/cfg/beta/a.txt"],
            directories: ["/cfg", "/cfg/alpha", "/cfg/beta"]);
        SetupSession(gameServerId, session);

        var controller = CreateController();
        var firstPage = await controller.ListEntries(gameServerId, new ListEntriesQueryDto
        {
            Path = "/cfg",
            Recursive = true,
            PageSize = 3,
        });

        var firstObjectResult = Assert.IsType<ObjectResult>(firstPage);
        Assert.Equal(200, firstObjectResult.StatusCode);
        var firstPayload = Assert.IsType<ApiResponse<FileEntriesCollectionDto>>(firstObjectResult.Value);
        Assert.Equal("3", firstPayload.Data?.ContinuationToken);
        Assert.Equal("/cfg/alpha/a.txt", firstPayload.Data?.Items.Last().FullPath);

        var secondPage = await controller.ListEntries(gameServerId, new ListEntriesQueryDto
        {
            Path = "/cfg",
            Recursive = true,
            PageSize = 3,
            ContinuationToken = firstPayload.Data?.ContinuationToken,
        });

        var secondObjectResult = Assert.IsType<ObjectResult>(secondPage);
        Assert.Equal(200, secondObjectResult.StatusCode);
        var secondPayload = Assert.IsType<ApiResponse<FileEntriesCollectionDto>>(secondObjectResult.Value);
        Assert.Single(secondPayload.Data?.Items ?? []);
        Assert.Equal("/cfg/beta/a.txt", secondPayload.Data?.Items.Single().FullPath);
    }

    [Fact]
    public async Task GetContent_WhenRangeSpecified_ReturnsPartialText()
    {
        var gameServerId = Guid.NewGuid();
        var session = new TestFileTransportSession();
        session.SetFile("/cfg/a.txt", "abcdef");
        SetupSession(gameServerId, session);

        var controller = CreateController();
        var result = await controller.GetContent(gameServerId, new GetFileContentQueryDto
        {
            Path = "/cfg/a.txt",
            RangeStart = 1,
            RangeEnd = 3,
            Mode = FileContentMode.Text,
            Encoding = "utf-8",
        });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        var payload = Assert.IsType<ApiResponse<FileContentDto>>(objectResult.Value);
        Assert.Equal("bcd", payload.Data?.TextContent);
    }

    [Fact]
    public async Task GetContent_WhenRangeIsInvalid_ReturnsRequestedRangeNotSatisfiable()
    {
        var gameServerId = Guid.NewGuid();
        var session = new TestFileTransportSession();
        session.SetFile("/cfg/a.txt", "abcdef");
        SetupSession(gameServerId, session);

        var controller = CreateController();
        var result = await controller.GetContent(gameServerId, new GetFileContentQueryDto
        {
            Path = "/cfg/a.txt",
            RangeStart = 99,
            RangeEnd = 120,
        });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(416, objectResult.StatusCode);
    }

    [Fact]
    public async Task PatchEntry_WhenRenameFile_ReturnsRenamedAndMovesFile()
    {
        var gameServerId = Guid.NewGuid();
        var session = new TestFileTransportSession(files: ["/cfg/a.txt"], directories: ["/cfg"]);
        SetupSession(gameServerId, session);

        var controller = CreateController();

        var result = await controller.PatchEntry(gameServerId, new PatchFileEntryRequestDto
        {
            Operation = FileEntryPatchOperation.Rename,
            SourcePath = "/cfg/a.txt",
            DestinationPath = "/cfg/b.txt",
            Overwrite = true,
        });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);

        var payload = Assert.IsType<ApiResponse<FileMutationResultDto>>(objectResult.Value);
        Assert.Equal(FileMutationOutcome.Renamed, payload.Data?.Outcome);
        Assert.True(await session.FileExists("/cfg/b.txt"));
        Assert.False(await session.FileExists("/cfg/a.txt"));
    }

    [Fact]
    public async Task PatchEntry_WhenDirectoryWithoutRecursive_ReturnsConflict()
    {
        var gameServerId = Guid.NewGuid();
        var session = new TestFileTransportSession(directories: ["/cfg", "/cfg/sub"]);
        SetupSession(gameServerId, session);

        var controller = CreateController();

        var result = await controller.PatchEntry(gameServerId, new PatchFileEntryRequestDto
        {
            Operation = FileEntryPatchOperation.Move,
            SourcePath = "/cfg/sub",
            DestinationPath = "/cfg/sub2",
            Recursive = false,
        });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, objectResult.StatusCode);
    }

    [Fact]
    public async Task PatchEntry_WhenOperationIsInvalid_ReturnsBadRequestAndDoesNotMutate()
    {
        var gameServerId = Guid.NewGuid();
        var session = new TestFileTransportSession(files: ["/cfg/a.txt"], directories: ["/cfg"]);
        SetupSession(gameServerId, session);
        var controller = CreateController();

        var result = await controller.PatchEntry(gameServerId, new PatchFileEntryRequestDto
        {
            Operation = (FileEntryPatchOperation)999,
            SourcePath = "/cfg/a.txt",
            DestinationPath = "/cfg/b.txt",
            Overwrite = true,
        });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
        Assert.True(await session.FileExists("/cfg/a.txt"));
        Assert.False(await session.FileExists("/cfg/b.txt"));
    }

    private void SetupSession(Guid gameServerId, IGameServerFileTransportSession session)
    {
        var sessionResult = new ApiResult<IGameServerFileTransportSession>(
            HttpStatusCode.OK,
            new ApiResponse<IGameServerFileTransportSession>(session));

        _mockFileTransportFactory
            .Setup(x => x.CreateSession(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResult);
    }

    private sealed class TestFileTransportSession : IGameServerFileTransportSession
    {
        private readonly Dictionary<string, byte[]> _files;
        private readonly HashSet<string> _directories;

        public TestFileTransportSession(IEnumerable<string>? files = null, IEnumerable<string>? directories = null)
        {
            _files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            _directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddDirectoryHierarchy("/");

            foreach (var directory in directories ?? [])
            {
                AddDirectoryHierarchy(directory);
            }

            foreach (var file in files ?? [])
            {
                SetFile(file, "content");
            }
        }

        public ResolvedFileTransport Transport { get; } = new(FileTransportType.Ftp, "ftp", new FileTransportCredentials("localhost", 21, "user", "pwd"));

        public void SetFile(string path, string content)
        {
            var normalizedPath = NormalizePath(path);
            var parentPath = GetParentPath(normalizedPath);
            AddDirectoryHierarchy(parentPath);
            _files[normalizedPath] = Encoding.UTF8.GetBytes(content);
        }

        public Task<IReadOnlyList<FileTransportEntry>> GetListing(string path, CancellationToken cancellationToken = default)
        {
            var normalizedPath = NormalizePath(path);
            var entries = new List<FileTransportEntry>();

            foreach (var directory in _directories)
            {
                var parent = GetParentPath(directory);
                if (directory != normalizedPath && string.Equals(parent, normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    entries.Add(new FileTransportEntry(GetName(directory), directory, true, null, DateTime.UtcNow));
                }
            }

            foreach (var file in _files)
            {
                var parent = GetParentPath(file.Key);
                if (string.Equals(parent, normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    entries.Add(new FileTransportEntry(GetName(file.Key), file.Key, false, file.Value.LongLength, DateTime.UtcNow));
                }
            }

            return Task.FromResult<IReadOnlyList<FileTransportEntry>>(entries);
        }

        public Task<bool> FileExists(string path, CancellationToken cancellationToken = default)
            => Task.FromResult(_files.ContainsKey(NormalizePath(path)));

        public Task<byte[]> DownloadBytes(string path, CancellationToken cancellationToken = default)
        {
            var normalizedPath = NormalizePath(path);
            if (!_files.TryGetValue(normalizedPath, out var content))
            {
                throw new FileNotFoundException($"File '{normalizedPath}' not found.");
            }

            return Task.FromResult(content.ToArray());
        }

        public Task UploadBytes(string path, byte[] content, CancellationToken cancellationToken = default)
        {
            var normalizedPath = NormalizePath(path);
            AddDirectoryHierarchy(GetParentPath(normalizedPath));
            _files[normalizedPath] = content.ToArray();
            return Task.CompletedTask;
        }

        public Task UploadStream(string path, Stream content, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteFile(string path, CancellationToken cancellationToken = default)
        {
            _files.Remove(NormalizePath(path));
            return Task.CompletedTask;
        }

        public Task<bool> DirectoryExists(string path, CancellationToken cancellationToken = default)
            => Task.FromResult(_directories.Contains(NormalizePath(path)));

        public Task CreateDirectory(string path, CancellationToken cancellationToken = default)
        {
            AddDirectoryHierarchy(path);
            return Task.CompletedTask;
        }

        public Task DeleteDirectory(string path, CancellationToken cancellationToken = default)
        {
            var normalizedPath = NormalizePath(path);
            var prefix = normalizedPath + "/";

            foreach (var filePath in _files.Keys.Where(filePath => filePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                _files.Remove(filePath);
            }

            foreach (var directoryPath in _directories.Where(directoryPath => directoryPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                _directories.Remove(directoryPath);
            }

            _directories.Remove(normalizedPath);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static string NormalizePath(string path)
        {
            var normalized = path.Replace('\\', '/').Trim();
            if (!normalized.StartsWith('/'))
            {
                normalized = "/" + normalized;
            }

            if (normalized.Length > 1 && normalized.EndsWith('/'))
            {
                normalized = normalized.TrimEnd('/');
            }

            return normalized;
        }

        private static string GetName(string path)
        {
            var normalized = NormalizePath(path);
            var lastSlash = normalized.LastIndexOf('/');
            return lastSlash >= 0 ? normalized[(lastSlash + 1)..] : normalized;
        }

        private static string GetParentPath(string path)
        {
            var normalized = NormalizePath(path);
            if (normalized == "/")
            {
                return "/";
            }

            var lastSlash = normalized.LastIndexOf('/');
            if (lastSlash <= 0)
            {
                return "/";
            }

            return normalized[..lastSlash];
        }

        private void AddDirectoryHierarchy(string path)
        {
            var normalizedPath = NormalizePath(path);
            if (normalizedPath == "/")
            {
                _directories.Add("/");
                return;
            }

            _directories.Add("/");
            var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var currentPath = string.Empty;
            foreach (var segment in segments)
            {
                currentPath = string.Concat(currentPath, "/", segment);
                _directories.Add(currentPath);
            }
        }
    }
}
