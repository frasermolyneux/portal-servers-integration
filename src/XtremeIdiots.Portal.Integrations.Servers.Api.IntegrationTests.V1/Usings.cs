global using Xunit;
global using Moq;

// Disable parallel test execution to avoid CryptographicException with concurrent WebApplicationFactory host creation
[assembly: CollectionBehavior(DisableTestParallelization = true)]
