namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Constants;

/// <summary>
/// Defines standardized error codes for the Portal Servers Integration API.
/// Error codes follow the pattern: CATEGORY_SPECIFIC_DESCRIPTION
/// </summary>
public static class ErrorCodes
{
    /// <summary>
    /// The request contains invalid or missing required parameters.
    /// </summary>
    public const string INVALID_REQUEST = "INVALID_REQUEST";

    /// <summary>
    /// The requested game server was not found.
    /// </summary>
    public const string GAME_SERVER_NOT_FOUND = "GAME_SERVER_NOT_FOUND";

    /// <summary>
    /// The requested map was not found in the repository.
    /// </summary>
    public const string MAP_NOT_FOUND = "MAP_NOT_FOUND";

    /// <summary>
    /// The requested map does not have any associated files.
    /// </summary>
    public const string MAP_FILES_NOT_FOUND = "MAP_FILES_NOT_FOUND";

    /// <summary>
    /// The requested dvar was not found on the game server.
    /// </summary>
    public const string DVAR_NOT_FOUND = "DVAR_NOT_FOUND";

    /// <summary>
    /// Failed to establish connection to the game server's FTP host.
    /// </summary>
    public const string FTP_CONNECTION_FAILED = "FTP_CONNECTION_FAILED";

    /// <summary>
    /// An FTP operation failed during execution.
    /// </summary>
    public const string FTP_OPERATION_FAILED = "FTP_OPERATION_FAILED";

    /// <summary>
    /// The game server does not have a configured RCON password.
    /// </summary>
    public const string RCON_PASSWORD_NOT_CONFIGURED = "RCON_PASSWORD_NOT_CONFIGURED";

    /// <summary>
    /// Failed to establish RCON connection to the game server.
    /// </summary>
    public const string RCON_CONNECTION_FAILED = "RCON_CONNECTION_FAILED";

    /// <summary>
    /// An RCON operation failed during execution.
    /// </summary>
    public const string RCON_OPERATION_FAILED = "RCON_OPERATION_FAILED";

    /// <summary>
    /// Failed to establish query connection to the game server.
    /// </summary>
    public const string QUERY_CONNECTION_FAILED = "QUERY_CONNECTION_FAILED";

    /// <summary>
    /// A query operation failed during execution.
    /// </summary>
    public const string QUERY_OPERATION_FAILED = "QUERY_OPERATION_FAILED";

    /// <summary>
    /// The requested operation is not implemented for this game server type.
    /// </summary>
    public const string OPERATION_NOT_IMPLEMENTED = "OPERATION_NOT_IMPLEMENTED";

    /// <summary>
    /// Player verification failed - the player name does not match the player in the specified slot.
    /// </summary>
    public const string PLAYER_VERIFICATION_FAILED = "PLAYER_VERIFICATION_FAILED";

    /// <summary>
    /// The requested configuration file was not found on the game server.
    /// </summary>
    public const string CONFIG_FILE_NOT_FOUND = "CONFIG_FILE_NOT_FOUND";

    /// <summary>
    /// The requested variable was not found in the configuration file.
    /// </summary>
    public const string CONFIG_VARIABLE_NOT_FOUND = "CONFIG_VARIABLE_NOT_FOUND";

    /// <summary>
    /// A configuration file operation failed during execution.
    /// </summary>
    public const string CONFIG_OPERATION_FAILED = "CONFIG_OPERATION_FAILED";

    /// <summary>
    /// The game server does not have FTP credentials configured.
    /// </summary>
    public const string FTP_CREDENTIALS_MISSING = "FTP_CREDENTIALS_MISSING";

    /// <summary>
    /// The game server does not have RCON credentials configured.
    /// </summary>
    public const string RCON_CREDENTIALS_MISSING = "RCON_CREDENTIALS_MISSING";

    /// <summary>
    /// Transport-neutral alias for missing file transport credentials.
    /// </summary>
    public const string FILE_TRANSPORT_CREDENTIALS_MISSING = FTP_CREDENTIALS_MISSING;

    /// <summary>
    /// Transport-neutral alias for file transport connection failures.
    /// </summary>
    public const string FILE_TRANSPORT_CONNECTION_FAILED = FTP_CONNECTION_FAILED;

    /// <summary>
    /// Transport-neutral alias for file transport operation failures.
    /// </summary>
    public const string FILE_TRANSPORT_OPERATION_FAILED = FTP_OPERATION_FAILED;
}
