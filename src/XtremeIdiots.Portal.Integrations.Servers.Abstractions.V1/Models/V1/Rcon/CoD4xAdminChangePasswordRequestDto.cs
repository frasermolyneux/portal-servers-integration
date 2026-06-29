namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record CoD4xAdminChangePasswordRequestDto
{
    public string? User { get; init; }

    public string? NewPassword { get; init; }
}
