using System.Net;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Files;
using XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing.Tests;

[Trait("Category", "Unit")]
public class FakeFilesApiTests
{
    [Fact]
    public async Task ListEntries_WithConfiguredResponse_ReturnsConfiguredPayload()
    {
        var fake = new FakeFilesApi();
        var serverId = Guid.NewGuid();
        var dto = new FileEntriesCollectionDto("/cfg", "/", [new FileEntryDto("server.cfg", "/cfg/server.cfg", FileEntryType.File, 10, null)]);

        fake.AddListEntriesResponse(serverId, "/cfg", dto);

        var result = await fake.ListEntries(serverId, new ListEntriesQueryDto { Path = "/cfg" });

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Same(dto, result.Result?.Data);
        Assert.Single(fake.OperationLog);
    }

    [Fact]
    public async Task GetContent_WithErrorDefaultBehavior_ReturnsNotFound()
    {
        var fake = new FakeFilesApi().SetDefaultBehavior(DefaultBehavior.ReturnError);

        var result = await fake.GetContent(Guid.NewGuid(), new GetFileContentQueryDto { Path = "/missing.txt" });

        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        Assert.Equal("NOT_FOUND", result.Result?.Errors?.FirstOrDefault()?.Code);
    }

    [Fact]
    public async Task PutContent_WithDefaultBehavior_ReturnsCreatedMutation()
    {
        var fake = new FakeFilesApi();

        var result = await fake.PutContent(Guid.NewGuid(), new PutFileContentRequestDto { Path = "/cfg/server.cfg", TextContent = "set sv_hostname XI" });

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(FileMutationOperation.Put, result.Result?.Data?.Operation);
        Assert.Equal(FileMutationOutcome.Created, result.Result?.Data?.Outcome);
    }

    [Fact]
    public async Task PatchEntry_MapsRenameOperation()
    {
        var fake = new FakeFilesApi();

        var result = await fake.PatchEntry(
            Guid.NewGuid(),
            new PatchFileEntryRequestDto
            {
                Operation = FileEntryPatchOperation.Rename,
                SourcePath = "/cfg/a.txt",
                DestinationPath = "/cfg/b.txt",
            });

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(FileMutationOperation.Rename, result.Result?.Data?.Operation);
        Assert.Equal(FileMutationOutcome.Renamed, result.Result?.Data?.Outcome);
    }

    [Theory]
    [InlineData(FileEntryPatchOperation.Move, FileMutationOperation.Move, FileMutationOutcome.Moved)]
    [InlineData(FileEntryPatchOperation.Copy, FileMutationOperation.Copy, FileMutationOutcome.Copied)]
    public async Task PatchEntry_MapsOperationAndOutcome(
        FileEntryPatchOperation patchOperation,
        FileMutationOperation mutationOperation,
        FileMutationOutcome mutationOutcome)
    {
        var fake = new FakeFilesApi();

        var result = await fake.PatchEntry(
            Guid.NewGuid(),
            new PatchFileEntryRequestDto
            {
                Operation = patchOperation,
                SourcePath = "/cfg/a.txt",
                DestinationPath = "/cfg/b.txt",
            });

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(mutationOperation, result.Result?.Data?.Operation);
        Assert.Equal(mutationOutcome, result.Result?.Data?.Outcome);
    }

    [Fact]
    public async Task PatchEntry_WhenOperationIsInvalid_ReturnsBadRequest()
    {
        var fake = new FakeFilesApi();

        var result = await fake.PatchEntry(
            Guid.NewGuid(),
            new PatchFileEntryRequestDto
            {
                Operation = (FileEntryPatchOperation)999,
                SourcePath = "/cfg/a.txt",
                DestinationPath = "/cfg/b.txt",
            });

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal("INVALID_REQUEST", result.Result?.Errors?.FirstOrDefault()?.Code);
    }
}
