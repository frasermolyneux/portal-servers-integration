namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Helpers;

internal static class ResolverFixtureLoader
{
    private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Resolvers");

    public static string Load(string relativePath)
    {
        var fullPath = Path.Combine(FixtureRoot, relativePath);
        return File.ReadAllText(fullPath);
    }
}
