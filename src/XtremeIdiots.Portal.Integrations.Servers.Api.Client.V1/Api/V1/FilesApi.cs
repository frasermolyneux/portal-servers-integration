using Microsoft.Extensions.Logging;
using MX.Api.Abstractions;
using MX.Api.Client;
using MX.Api.Client.Auth;
using MX.Api.Client.Configuration;
using MX.Api.Client.Extensions;
using RestSharp;

using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Files;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
{
    public class FilesApi : BaseApi<ServersApiClientOptions>, IFilesApi
    {
        public FilesApi(
            ILogger<BaseApi<ServersApiClientOptions>> logger,
            IApiTokenProvider? apiTokenProvider,
            IRestClientService restClientService,
            ServersApiClientOptions options)
            : base(logger, apiTokenProvider, restClientService, options)
        {
        }

        public async Task<ApiResult<FileEntriesCollectionDto>> ListEntries(Guid gameServerId, ListEntriesQueryDto query, CancellationToken cancellationToken = default)
        {
            var request = await CreateRequestAsync($"v1/files/{gameServerId}/entries", Method.Get, cancellationToken);
            AddOptionalQueryParameter(request, "path", query.Path);
            AddOptionalQueryParameter(request, "recursive", query.Recursive ? "true" : null);
            AddOptionalQueryParameter(request, "includeHidden", query.IncludeHidden ? "true" : null);
            AddOptionalQueryParameter(request, "pageSize", query.PageSize?.ToString());
            AddOptionalQueryParameter(request, "continuationToken", query.ContinuationToken);

            var response = await ExecuteAsync(request, cancellationToken);
            return response.ToApiResult<FileEntriesCollectionDto>();
        }

        public async Task<ApiResult<FileContentDto>> GetContent(Guid gameServerId, GetFileContentQueryDto query, CancellationToken cancellationToken = default)
        {
            var request = await CreateRequestAsync($"v1/files/{gameServerId}/content", Method.Get, cancellationToken);
            AddOptionalQueryParameter(request, "path", query.Path);
            request.AddQueryParameter("mode", query.Mode.ToString().ToLowerInvariant());
            request.AddQueryParameter("encoding", query.Encoding);
            AddOptionalQueryParameter(request, "rangeStart", query.RangeStart?.ToString());
            AddOptionalQueryParameter(request, "rangeEnd", query.RangeEnd?.ToString());

            var response = await ExecuteAsync(request, cancellationToken);
            return response.ToApiResult<FileContentDto>();
        }

        public async Task<ApiResult<FileEntryMetadataDto>> GetMetadata(Guid gameServerId, GetEntryMetadataQueryDto query, CancellationToken cancellationToken = default)
        {
            var request = await CreateRequestAsync($"v1/files/{gameServerId}/metadata", Method.Get, cancellationToken);
            AddOptionalQueryParameter(request, "path", query.Path);

            var response = await ExecuteAsync(request, cancellationToken);
            return response.ToApiResult<FileEntryMetadataDto>();
        }

        public async Task<ApiResult<FileMutationResultDto>> PutContent(Guid gameServerId, PutFileContentRequestDto requestBody, CancellationToken cancellationToken = default)
        {
            var request = await CreateRequestAsync($"v1/files/{gameServerId}/content", Method.Put, cancellationToken);
            request.AddJsonBody(requestBody);

            var response = await ExecuteAsync(request, cancellationToken);
            return response.ToApiResult<FileMutationResultDto>();
        }

        public async Task<ApiResult<FileMutationResultDto>> DeleteContent(Guid gameServerId, DeleteFileQueryDto query, CancellationToken cancellationToken = default)
        {
            var request = await CreateRequestAsync($"v1/files/{gameServerId}/content", Method.Delete, cancellationToken);
            AddOptionalQueryParameter(request, "path", query.Path);

            var response = await ExecuteAsync(request, cancellationToken);
            return response.ToApiResult<FileMutationResultDto>();
        }

        public async Task<ApiResult<FileMutationResultDto>> CreateDirectory(Guid gameServerId, CreateDirectoryRequestDto requestBody, CancellationToken cancellationToken = default)
        {
            var request = await CreateRequestAsync($"v1/files/{gameServerId}/directories", Method.Post, cancellationToken);
            request.AddJsonBody(requestBody);

            var response = await ExecuteAsync(request, cancellationToken);
            return response.ToApiResult<FileMutationResultDto>();
        }

        public async Task<ApiResult<FileMutationResultDto>> DeleteDirectory(Guid gameServerId, DeleteDirectoryQueryDto query, CancellationToken cancellationToken = default)
        {
            var request = await CreateRequestAsync($"v1/files/{gameServerId}/directories", Method.Delete, cancellationToken);
            AddOptionalQueryParameter(request, "path", query.Path);
            AddOptionalQueryParameter(request, "recursive", query.Recursive ? "true" : null);

            var response = await ExecuteAsync(request, cancellationToken);
            return response.ToApiResult<FileMutationResultDto>();
        }

        public async Task<ApiResult<FileMutationResultDto>> PatchEntry(Guid gameServerId, PatchFileEntryRequestDto requestBody, CancellationToken cancellationToken = default)
        {
            var request = await CreateRequestAsync($"v1/files/{gameServerId}/entries", Method.Patch, cancellationToken);
            request.AddJsonBody(requestBody);

            var response = await ExecuteAsync(request, cancellationToken);
            return response.ToApiResult<FileMutationResultDto>();
        }

        private static void AddOptionalQueryParameter(RestRequest request, string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                request.AddQueryParameter(name, value);
            }
        }
    }
}
