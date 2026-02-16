using System.Reflection;
using System.Text.RegularExpressions;
using Messaging.Persistence.Messages;

namespace Messaging.Persistence.Tests.Messages;

public sealed class MessageWriterGuardrailTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void MessageWriter_compiled_insert_sql_contains_single_idempotent_messages_insert()
    {
        var sql = NormalizeSql(MessageWriter.InsertIdempotentSql);

        // Exactly one insert
        Assert.Single(
            Regex.Matches(sql, @"insert\s+into\s+core\.messages", RegexOptions.IgnoreCase));

        // Concurrency-safe idempotency
        Assert.Matches(@"on\s+conflict\s+\(idempotency_key\)", sql);
        Assert.Matches(@"do\s+update", sql);
        Assert.Matches(@"set\s+updated_at\s*=\s*now\(\)", sql);
        Assert.Matches(
            new Regex(@"\(\s*xmax\s*=\s*0\s*\)\s+as\s+inserted", RegexOptions.IgnoreCase),
            sql);
        Assert.DoesNotMatch(@"set\s+id\s*=", sql);

        // Must return identity
        Assert.Matches(@"returning\s+.*\bid\b", sql);

        // Guard against unsafe regressions
        Assert.DoesNotContain("union all", sql, StringComparison.OrdinalIgnoreCase);
        Assert.False(Regex.IsMatch(sql, @"\b(xmin|ctid|tableoid)\b", RegexOptions.IgnoreCase));
    }

    [Fact]
    [Trait("Category", "Integration")]
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
        Assert.DoesNotContain("set subject", insertSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("set text_body", insertSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("set html_body", insertSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("set template_key", insertSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("set template_version", insertSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("set template_resolved_at", insertSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("set template_variables", insertSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("set reply_to_message_id", insertSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("set in_reply_to", insertSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("set references_header", insertSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(@"@\bId\b", insertSql);
        Assert.Contains("returning id", insertSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Inserted", insertSql, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractInsertColumns(string insertSql)
    {
        var match = Regex.Match(
            insertSql,
            "insert\\s+into\\s+core\\.messages\\s*\\((?<columns>[\\s\\S]*?)\\)\\s*values",
            RegexOptions.IgnoreCase);

        Assert.True(match.Success, "Could not locate insert column list in InsertIdempotentSql.");
        return match.Groups["columns"].Value;
    }

    private static string NormalizeSql(string sql)
    {
        return Regex.Replace(sql, @"\s+", " ").Trim();
    }
}
