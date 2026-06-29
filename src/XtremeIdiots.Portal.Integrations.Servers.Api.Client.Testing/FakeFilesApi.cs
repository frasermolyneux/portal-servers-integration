using System.Collections.Concurrent;
using System.Net;
using MX.Api.Abstractions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Files;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;

/// <summary>
/// In-memory fake of <see cref="IFilesApi"/> for unit and integration testing.
/// Supports canned responses, error simulation, and call tracking.
/// </summary>
public class FakeFilesApi : IFilesApi
{
    private readonly ConcurrentDictionary<(Guid ServerId, string Path), ApiResult<FileEntriesCollectionDto>> _listEntriesResponses = new();
    private readonly ConcurrentDictionary<(Guid ServerId, string Path), ApiResult<FileContentDto>> _contentResponses = new();
    private readonly ConcurrentDictionary<(Guid ServerId, string Path), ApiResult<FileEntryMetadataDto>> _metadataResponses = new();
    private readonly ConcurrentDictionary<(Guid ServerId, string Path), ApiResult<FileMutationResultDto>> _putResponses = new();
    private readonly ConcurrentDictionary<(Guid ServerId, string Path), ApiResult<FileMutationResultDto>> _deleteContentResponses = new();
    private readonly ConcurrentDictionary<(Guid ServerId, string Path), ApiResult<FileMutationResultDto>> _createDirectoryResponses = new();
    private readonly ConcurrentDictionary<(Guid ServerId, string Path), ApiResult<FileMutationResultDto>> _deleteDirectoryResponses = new();
    private readonly ConcurrentDictionary<(Guid ServerId, string SourcePath), ApiResult<FileMutationResultDto>> _patchResponses = new();
    private readonly ConcurrentBag<(string Operation, Guid ServerId, object? Params)> _operationLog = [];

    public IReadOnlyCollection<(string Operation, Guid ServerId, object? Params)> OperationLog => _operationLog.ToArray();

    public DefaultBehavior DefaultResponseBehavior { get; private set; } = DefaultBehavior.ReturnGenericSuccess;

    public FakeFilesApi AddListEntriesResponse(Guid gameServerId, string path, FileEntriesCollectionDto dto)
    {
        _listEntriesResponses[(gameServerId, NormalizePath(path))] = new ApiResult<FileEntriesCollectionDto>(HttpStatusCode.OK, new ApiResponse<FileEntriesCollectionDto>(dto));
        return this;
    }

    public FakeFilesApi AddContentResponse(Guid gameServerId, string path, FileContentDto dto)
    {
        _contentResponses[(gameServerId, NormalizePath(path))] = new ApiResult<FileContentDto>(HttpStatusCode.OK, new ApiResponse<FileContentDto>(dto));
        return this;
    }

    public FakeFilesApi AddMetadataResponse(Guid gameServerId, string path, FileEntryMetadataDto dto)
    {
        _metadataResponses[(gameServerId, NormalizePath(path))] = new ApiResult<FileEntryMetadataDto>(HttpStatusCode.OK, new ApiResponse<FileEntryMetadataDto>(dto));
        return this;
    }

    public FakeFilesApi AddPutResponse(Guid gameServerId, string path, FileMutationResultDto dto, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _putResponses[(gameServerId, NormalizePath(path))] = new ApiResult<FileMutationResultDto>(statusCode, new ApiResponse<FileMutationResultDto>(dto));
        return this;
    }

    public FakeFilesApi AddDeleteContentResponse(Guid gameServerId, string path, FileMutationResultDto dto, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _deleteContentResponses[(gameServerId, NormalizePath(path))] = new ApiResult<FileMutationResultDto>(statusCode, new ApiResponse<FileMutationResultDto>(dto));
        return this;
    }

    public FakeFilesApi AddCreateDirectoryResponse(Guid gameServerId, string path, FileMutationResultDto dto, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _createDirectoryResponses[(gameServerId, NormalizePath(path))] = new ApiResult<FileMutationResultDto>(statusCode, new ApiResponse<FileMutationResultDto>(dto));
        return this;
    }

    public FakeFilesApi AddDeleteDirectoryResponse(Guid gameServerId, string path, FileMutationResultDto dto, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _deleteDirectoryResponses[(gameServerId, NormalizePath(path))] = new ApiResult<FileMutationResultDto>(statusCode, new ApiResponse<FileMutationResultDto>(dto));
        return this;
    }

    public FakeFilesApi AddPatchResponse(Guid gameServerId, string sourcePath, FileMutationResultDto dto, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _patchResponses[(gameServerId, NormalizePath(sourcePath))] = new ApiResult<FileMutationResultDto>(statusCode, new ApiResponse<FileMutationResultDto>(dto));
        return this;
    }

    public FakeFilesApi SetDefaultBehavior(DefaultBehavior behavior)
    {
        DefaultResponseBehavior = behavior;
        return this;
    }

    public void Reset()
    {
        _listEntriesResponses.Clear();
        _contentResponses.Clear();
        _metadataResponses.Clear();
        _putResponses.Clear();
        _deleteContentResponses.Clear();
        _createDirectoryResponses.Clear();
        _deleteDirectoryResponses.Clear();
        _patchResponses.Clear();
        _operationLog.Clear();
        DefaultResponseBehavior = DefaultBehavior.ReturnGenericSuccess;
    }

    public Task<ApiResult<FileEntriesCollectionDto>> ListEntries(Guid gameServerId, ListEntriesQueryDto query, CancellationToken cancellationToken = default)
    {
        _operationLog.Add(("ListEntries", gameServerId, query));
        var path = NormalizePath(query.Path);

        if (_listEntriesResponses.TryGetValue((gameServerId, path), out var configured))
        {
            return Task.FromResult(configured);
        }

        return Task.FromResult(DefaultResponseBehavior switch
        {
            DefaultBehavior.ReturnGenericSuccess => new ApiResult<FileEntriesCollectionDto>(
                HttpStatusCode.OK,
                new ApiResponse<FileEntriesCollectionDto>(new FileEntriesCollectionDto(path, GetParentPath(path), []))),
            DefaultBehavior.ReturnError => new ApiResult<FileEntriesCollectionDto>(
                HttpStatusCode.NotFound,
                new ApiResponse<FileEntriesCollectionDto>(new ApiError("NOT_FOUND", "Path not found"))),
            _ => throw new InvalidOperationException($"Unknown default behavior: {DefaultResponseBehavior}")
        });
    }

    public Task<ApiResult<FileContentDto>> GetContent(Guid gameServerId, GetFileContentQueryDto query, CancellationToken cancellationToken = default)
    {
        _operationLog.Add(("GetContent", gameServerId, query));
        var path = NormalizePath(query.Path);

        if (_contentResponses.TryGetValue((gameServerId, path), out var configured))
        {
            return Task.FromResult(configured);
        }

        return Task.FromResult(DefaultResponseBehavior switch
        {
            DefaultBehavior.ReturnGenericSuccess => new ApiResult<FileContentDto>(
                HttpStatusCode.OK,
                new ApiResponse<FileContentDto>(new FileContentDto(path, FileContentMode.Text, query.Encoding, 0, string.Empty, null))),
            DefaultBehavior.ReturnError => new ApiResult<FileContentDto>(
                HttpStatusCode.NotFound,
                new ApiResponse<FileContentDto>(new ApiError("NOT_FOUND", "File not found"))),
            _ => throw new InvalidOperationException($"Unknown default behavior: {DefaultResponseBehavior}")
        });
    }

    public Task<ApiResult<FileEntryMetadataDto>> GetMetadata(Guid gameServerId, GetEntryMetadataQueryDto query, CancellationToken cancellationToken = default)
    {
        _operationLog.Add(("GetMetadata", gameServerId, query));
        var path = NormalizePath(query.Path);

        if (_metadataResponses.TryGetValue((gameServerId, path), out var configured))
        {
            return Task.FromResult(configured);
        }

        return Task.FromResult(DefaultResponseBehavior switch
        {
            DefaultBehavior.ReturnGenericSuccess => new ApiResult<FileEntryMetadataDto>(
                HttpStatusCode.OK,
                new ApiResponse<FileEntryMetadataDto>(new FileEntryMetadataDto("entry", path, FileEntryType.File, 0, null))),
            DefaultBehavior.ReturnError => new ApiResult<FileEntryMetadataDto>(
                HttpStatusCode.NotFound,
                new ApiResponse<FileEntryMetadataDto>(new ApiError("NOT_FOUND", "Entry not found"))),
            _ => throw new InvalidOperationException($"Unknown default behavior: {DefaultResponseBehavior}")
        });
    }

    public Task<ApiResult<FileMutationResultDto>> PutContent(Guid gameServerId, PutFileContentRequestDto request, CancellationToken cancellationToken = default)
    {
        _operationLog.Add(("PutContent", gameServerId, request));
        var path = NormalizePath(request.Path);

        if (_putResponses.TryGetValue((gameServerId, path), out var configured))
        {
            return Task.FromResult(configured);
        }

        return Task.FromResult(CreateMutationDefaultResponse(FileMutationOperation.Put, path));
    }

    public Task<ApiResult<FileMutationResultDto>> DeleteContent(Guid gameServerId, DeleteFileQueryDto query, CancellationToken cancellationToken = default)
    {
        _operationLog.Add(("DeleteContent", gameServerId, query));
        var path = NormalizePath(query.Path);

        if (_deleteContentResponses.TryGetValue((gameServerId, path), out var configured))
        {
            return Task.FromResult(configured);
        }

        return Task.FromResult(CreateMutationDefaultResponse(FileMutationOperation.DeleteFile, path));
    }

    public Task<ApiResult<FileMutationResultDto>> CreateDirectory(Guid gameServerId, CreateDirectoryRequestDto request, CancellationToken cancellationToken = default)
    {
        _operationLog.Add(("CreateDirectory", gameServerId, request));
        var path = NormalizePath(request.Path);

        if (_createDirectoryResponses.TryGetValue((gameServerId, path), out var configured))
        {
            return Task.FromResult(configured);
        }

        return Task.FromResult(CreateMutationDefaultResponse(FileMutationOperation.CreateDirectory, path));
    }

    public Task<ApiResult<FileMutationResultDto>> DeleteDirectory(Guid gameServerId, DeleteDirectoryQueryDto query, CancellationToken cancellationToken = default)
    {
        _operationLog.Add(("DeleteDirectory", gameServerId, query));
        var path = NormalizePath(query.Path);

        if (_deleteDirectoryResponses.TryGetValue((gameServerId, path), out var configured))
        {
            return Task.FromResult(configured);
        }

        return Task.FromResult(CreateMutationDefaultResponse(FileMutationOperation.DeleteDirectory, path));
    }

    public Task<ApiResult<FileMutationResultDto>> PatchEntry(Guid gameServerId, PatchFileEntryRequestDto request, CancellationToken cancellationToken = default)
    {
        _operationLog.Add(("PatchEntry", gameServerId, request));
        var sourcePath = NormalizePath(request.SourcePath);

        if (!Enum.IsDefined(request.Operation))
        {
            return Task.FromResult(new ApiResult<FileMutationResultDto>(
                HttpStatusCode.BadRequest,
                new ApiResponse<FileMutationResultDto>(new ApiError("INVALID_REQUEST", $"The patch operation '{request.Operation}' is unsupported."))));
        }

        if (_patchResponses.TryGetValue((gameServerId, sourcePath), out var configured))
        {
            return Task.FromResult(configured);
        }

        var operation = request.Operation switch
        {
            FileEntryPatchOperation.Rename => FileMutationOperation.Rename,
            FileEntryPatchOperation.Move => FileMutationOperation.Move,
            FileEntryPatchOperation.Copy => FileMutationOperation.Copy,
            _ => throw new InvalidOperationException("Unsupported patch operation."),
        };

        return Task.FromResult(CreateMutationDefaultResponse(operation, sourcePath));
    }

    private ApiResult<FileMutationResultDto> CreateMutationDefaultResponse(FileMutationOperation operation, string path)
    {
        var outcome = operation switch
        {
            FileMutationOperation.Put => FileMutationOutcome.Created,
            FileMutationOperation.DeleteFile => FileMutationOutcome.Deleted,
            FileMutationOperation.CreateDirectory => FileMutationOutcome.Created,
            FileMutationOperation.DeleteDirectory => FileMutationOutcome.Deleted,
            FileMutationOperation.Rename => FileMutationOutcome.Renamed,
            FileMutationOperation.Move => FileMutationOutcome.Moved,
            FileMutationOperation.Copy => FileMutationOutcome.Copied,
            _ => FileMutationOutcome.NoOp,
        };

        return DefaultResponseBehavior switch
        {
            DefaultBehavior.ReturnGenericSuccess => new ApiResult<FileMutationResultDto>(
                HttpStatusCode.OK,
                new ApiResponse<FileMutationResultDto>(new FileMutationResultDto(operation, outcome, path))),
            DefaultBehavior.ReturnError => new ApiResult<FileMutationResultDto>(
                HttpStatusCode.InternalServerError,
                new ApiResponse<FileMutationResultDto>(new ApiError("FAILED", "Operation failed"))),
            _ => throw new InvalidOperationException($"Unknown default behavior: {DefaultResponseBehavior}")
        };
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var normalized = path.Replace('\\', '/').Trim();
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        return normalized;
    }

    private static string? GetParentPath(string path)
    {
        if (path == "/")
        {
            return null;
        }

        var lastSlash = path.LastIndexOf('/');
        if (lastSlash <= 0)
        {
            return "/";
        }

        return path[..lastSlash];
    }
}
