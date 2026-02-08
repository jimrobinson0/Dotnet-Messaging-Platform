using System.Reflection;
using System.Text.RegularExpressions;
using Messaging.Platform.Persistence.Messages;

namespace Messaging.Platform.Persistence.Tests.Messages;

public sealed class MessageWriterGuardrailTests
{
    [Fact]
    public void MessageWriter_exposes_only_idempotent_message_insert_method()
    {
        var publicMethods = typeof(MessageWriter)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToArray();

        Assert.Contains(nameof(MessageWriter.InsertIdempotentAsync), publicMethods);
        Assert.DoesNotContain("InsertAsync", publicMethods);
    }

    [Fact]
    public void MessageWriter_source_contains_single_idempotent_messages_insert()
    {
        var source = File.ReadAllText(FindMessageWriterPath());

        Assert.DoesNotContain("private const string InsertSql", source, StringComparison.Ordinal);

        var insertStatements = Regex.Matches(
            source,
            "insert\\s+into\\s+messages",
            RegexOptions.IgnoreCase);
        Assert.Single(insertStatements);

        Assert.Contains("on conflict", source, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindMessageWriterPath()
    {
        var dir = AppContext.BaseDirectory;

        for (var i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "src", "Platform", "Persistence", "Messages", "MessageWriter.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var parent = Directory.GetParent(dir);
            if (parent is null)
            {
                break;
            }

            dir = parent.FullName;
        }

        throw new FileNotFoundException(
            "Could not locate src/Platform/Persistence/Messages/MessageWriter.cs from test output directory.");
    }
}
