namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Constants
{
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
    }
}
