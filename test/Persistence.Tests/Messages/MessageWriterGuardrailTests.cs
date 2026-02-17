using System.Text.RegularExpressions;
using Dapper;
using Messaging.Persistence.Messages.Writes;
using Messaging.Persistence.Tests.Infrastructure;
using Npgsql;

namespace Messaging.Persistence.Tests.Messages;

public sealed class MessageWriterGuardrailTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void MessageWriter_compiled_insert_sql_contains_single_idempotent_messages_insert()
    {
        var sql = NormalizeSql(MessageWriter.InsertIdempotentSql);

        // Must use CTE with inserted
        Assert.Matches(new Regex(@"^\s*with\s+inserted\s+as\s*\(", RegexOptions.IgnoreCase), sql);

        // Must use INSERT ... ON CONFLICT DO NOTHING
        Assert.Matches(new Regex(@"\bon\s+conflict\s*\(\s*idempotency_key\s*\)\s+do\s+nothing\b", RegexOptions.IgnoreCase), sql);

        // Must use UNION ALL to select replay id
        Assert.Matches(new Regex(@"\bunion\s+all\b", RegexOptions.IgnoreCase), sql);

        // Must not update any columns in replay
        Assert.DoesNotMatch(new Regex(@"\bdo\s+update\s+set\b", RegexOptions.IgnoreCase), sql);

        // Must reference NOT EXISTS logic
        Assert.Matches(new Regex(@"not\s+exists\s*\(\s*select\s+1\s+from\s+inserted\s*\)", RegexOptions.IgnoreCase), sql);

        // Must return inserted id
        Assert.Matches(new Regex(@"\breturning\s+id\b", RegexOptions.IgnoreCase), sql);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void MessageWriter_insert_sql_is_intent_only_and_uses_db_identity_defaults()
    {
        var insertSql = NormalizeSql(MessageWriter.InsertIdempotentSql);
        var insertColumns = ExtractInsertColumns(insertSql);

        Assert.DoesNotMatch(new Regex(@"\bid\b", RegexOptions.IgnoreCase), insertColumns);
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
        Assert.DoesNotContain("set updated_at", insertSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("returning id", insertSql, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractInsertColumns(string insertSql)
    {
        var match = Regex.Match(
            insertSql,
            "with\\s+inserted\\s+as\\s*\\(\\s*insert\\s+into\\s+core\\.messages\\s*\\((?<columns>[\\s\\S]*?)\\)\\s*values",
            RegexOptions.IgnoreCase);

        Assert.True(match.Success, "Could not locate insert column list in InsertIdempotentSql.");
        return match.Groups["columns"].Value;
    }

    private static string NormalizeSql(string sql)
    {
        return Regex.Replace(sql, @"\s+", " ").Trim();
    }
}

public sealed class MessageWriterConstraintGuardrailTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task IdempotencyKeyConstraintsExist()
    {
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync();

        var constraints = (await connection.QueryAsync<string>(
            """
            select tc.constraint_name
            from information_schema.table_constraints tc
            where tc.table_schema = 'core'
              and tc.table_name = 'messages'
              and tc.constraint_name in (
                'uq_messages_idempotency_key',
                'chk_messages_idempotency_key_length',
                'chk_messages_idempotency_key_not_blank'
              );
            """)).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("uq_messages_idempotency_key", constraints);
        Assert.Contains("chk_messages_idempotency_key_length", constraints);
        Assert.Contains("chk_messages_idempotency_key_not_blank", constraints);
    }
}
