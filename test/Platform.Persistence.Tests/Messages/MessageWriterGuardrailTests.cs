using System.Text.RegularExpressions;
using Messaging.Platform.Persistence.Messages;

namespace Messaging.Platform.Persistence.Tests.Messages;

public sealed class MessageWriterGuardrailTests
{
    [Fact]
    public void MessageWriter_exposes_only_idempotent_message_insert_method()
    {
        var publicMethods = typeof(MessageWriter)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToArray();

        Assert.Contains(nameof(MessageWriter.InsertIdempotentAsync), publicMethods);
        Assert.DoesNotContain("InsertAsync", publicMethods);
    }

    [Fact]
    public void MessageWriter_compiled_insert_sql_contains_single_idempotent_messages_insert()
    {
        var sql = NormalizeSql(MessageWriter.InsertIdempotentSql);

        var insertStatements = Regex.Matches(
            sql,
            "insert\\s+into\\s+messages",
            RegexOptions.IgnoreCase);
        Assert.Single(insertStatements);

        Assert.True(Regex.IsMatch(sql, @"with\s+inserted\s+as", RegexOptions.IgnoreCase | RegexOptions.Singleline));
        Assert.True(Regex.IsMatch(sql, @"insert\s+into\s+messages", RegexOptions.IgnoreCase));
        Assert.True(Regex.IsMatch(sql, @"on\s+conflict\s+\(idempotency_key\)", RegexOptions.IgnoreCase));
        Assert.True(Regex.IsMatch(sql, @"do\s+nothing", RegexOptions.IgnoreCase));
        Assert.True(Regex.IsMatch(sql, @"coalesce\s*\(\s*\(select\s+id\s+from\s+inserted\)\s*,", RegexOptions.IgnoreCase | RegexOptions.Singleline));

        Assert.DoesNotContain("do update", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("union all", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("xmax", sql, StringComparison.OrdinalIgnoreCase);
        Assert.False(Regex.IsMatch(sql, @"\b(xmin|xmax|ctid|tableoid)\b", RegexOptions.IgnoreCase));
    }

    [Fact]
    public void MessageWriter_insert_sql_is_intent_only_and_uses_db_identity_defaults()
    {
        var insertSql = NormalizeSql(MessageWriter.InsertIdempotentSql);
        var insertColumns = ExtractInsertColumns(insertSql);

        Assert.DoesNotMatch(@"\bid\b", insertColumns);
        Assert.DoesNotContain("claimed_by", insertSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("claimed_at", insertSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sent_at", insertSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("failure_reason", insertSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("attempt_count", insertSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(@"@\bId\b", insertSql);
        Assert.Contains("returning id", insertSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WasCreated", insertSql, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractInsertColumns(string insertSql)
    {
        var match = Regex.Match(
            insertSql,
            "insert\\s+into\\s+messages\\s*\\((?<columns>[\\s\\S]*?)\\)\\s*values",
            RegexOptions.IgnoreCase);

        Assert.True(match.Success, "Could not locate insert column list in InsertIdempotentSql.");
        return match.Groups["columns"].Value;
    }

    private static string NormalizeSql(string sql)
    {
        return Regex.Replace(sql, @"\s+", " ").Trim();
    }
}
