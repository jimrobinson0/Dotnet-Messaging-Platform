namespace Messaging.Persistence.Tests.Infrastructure;

internal static class SchemaLocator
{
    /// <summary>
    ///     Finds the "migrations" directory by walking upward from the test output directory.
    ///     This supports running tests from IDEs, CLI, and CI without hardcoded absolute paths.
    /// </summary>
    public static string FindMigrationsDirectory()
    {
        var dir = AppContext.BaseDirectory;

        for (var i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "migrations");
            if (Directory.Exists(candidate))
            {
                // Require that at least one initial migration exists.
                var initial = Path.Combine(candidate, "0001_initial.sql");
                if (File.Exists(initial)) return candidate;
            }

            var parent = Directory.GetParent(dir);
            if (parent is null) break;

            dir = parent.FullName;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the 'migrations' directory containing '0001_initial.sql'. " +
            "Ensure migrations are present at the repository root (e.g., ./migrations/0001_initial.sql) " +
            "or adjust SchemaLocator.FindMigrationsDirectory().");
    }
}