using MX.Api.Abstractions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Files;

namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;

public interface IFilesApi
{
    Task<ApiResult<FileEntriesCollectionDto>> ListEntries(Guid gameServerId, ListEntriesQueryDto query, CancellationToken cancellationToken = default);
    Task<ApiResult<FileContentDto>> GetContent(Guid gameServerId, GetFileContentQueryDto query, CancellationToken cancellationToken = default);
    Task<ApiResult<FileEntryMetadataDto>> GetMetadata(Guid gameServerId, GetEntryMetadataQueryDto query, CancellationToken cancellationToken = default);
    Task<ApiResult<FileMutationResultDto>> PutContent(Guid gameServerId, PutFileContentRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<FileMutationResultDto>> DeleteContent(Guid gameServerId, DeleteFileQueryDto query, CancellationToken cancellationToken = default);
    Task<ApiResult<FileMutationResultDto>> CreateDirectory(Guid gameServerId, CreateDirectoryRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<FileMutationResultDto>> DeleteDirectory(Guid gameServerId, DeleteDirectoryQueryDto query, CancellationToken cancellationToken = default);
    Task<ApiResult<FileMutationResultDto>> PatchEntry(Guid gameServerId, PatchFileEntryRequestDto request, CancellationToken cancellationToken = default);
}
