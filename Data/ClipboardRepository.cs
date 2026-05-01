using ClipNest.Models;
using Microsoft.Data.Sqlite;

namespace ClipNest.Data;

public sealed class ClipboardRepository(AppDatabase database)
{
    public async Task UpsertAsync(ClipboardItem item)
    {
        await using var connection = database.OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO clipboard_items(
                content_text, content_hash, source_app, created_at, updated_at,
                last_used_at, use_count, is_favorite, is_pinned, is_deleted, favorite_alias, favorite_tag
            )
            VALUES(
                $content_text, $content_hash, $source_app, $created_at, $updated_at,
                NULL, 0, 0, 0, 0, '', ''
            )
            ON CONFLICT(content_hash) DO UPDATE SET
                updated_at = excluded.updated_at,
                source_app = excluded.source_app,
                is_deleted = 0
            """;
        command.Parameters.AddWithValue("$content_text", item.ContentText);
        command.Parameters.AddWithValue("$content_hash", item.ContentHash);
        command.Parameters.AddWithValue("$source_app", item.SourceApp);
        command.Parameters.AddWithValue("$created_at", item.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updated_at", item.UpdatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<ClipboardItem>> SearchAsync(string query, int limit = 80)
    {
        await using var connection = database.OpenConnection();
        var command = connection.CreateCommand();
        if (string.IsNullOrWhiteSpace(query))
        {
            command.CommandText = """
                SELECT * FROM clipboard_items
                WHERE is_deleted = 0
                ORDER BY is_pinned DESC, is_favorite DESC, updated_at DESC
                LIMIT $limit
                """;
            command.Parameters.AddWithValue("$limit", limit);
        }
        else
        {
            command.CommandText = """
                SELECT * FROM clipboard_items
                WHERE is_deleted = 0
                  AND (content_text LIKE $query OR source_app LIKE $query OR favorite_alias LIKE $query OR favorite_tag LIKE $query)
                ORDER BY is_pinned DESC, is_favorite DESC, updated_at DESC
                LIMIT $limit
                """;
            command.Parameters.AddWithValue("$query", $"%{query}%");
            command.Parameters.AddWithValue("$limit", limit);
        }

        var result = new List<ClipboardItem>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(ReadItem(reader));
        }

        return result;
    }

    public async Task MarkUsedAsync(long id)
    {
        await using var connection = database.OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE clipboard_items
            SET use_count = use_count + 1, last_used_at = $now
            WHERE id = $id
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    public async Task ToggleFavoriteAsync(long id)
    {
        await using var connection = database.OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE clipboard_items SET is_favorite = CASE is_favorite WHEN 1 THEN 0 ELSE 1 END WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task SetFavoriteAsync(long id, string alias, string tag)
    {
        await using var connection = database.OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE clipboard_items
            SET is_favorite = 1, favorite_alias = $alias, favorite_tag = $tag
            WHERE id = $id
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$alias", alias.Trim());
        command.Parameters.AddWithValue("$tag", tag.Trim());
        await command.ExecuteNonQueryAsync();
    }

    public async Task UnfavoriteAsync(long id)
    {
        await using var connection = database.OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE clipboard_items SET is_favorite = 0 WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task SoftDeleteAsync(long id)
    {
        await using var connection = database.OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE clipboard_items SET is_deleted = 1 WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task ClearAsync()
    {
        await using var connection = database.OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE clipboard_items SET is_deleted = 1";
        await command.ExecuteNonQueryAsync();
    }

    private static ClipboardItem ReadItem(SqliteDataReader reader)
    {
        return new ClipboardItem
        {
            Id = reader.GetInt64(reader.GetOrdinal("id")),
            ContentText = reader.GetString(reader.GetOrdinal("content_text")),
            ContentHash = reader.GetString(reader.GetOrdinal("content_hash")),
            SourceApp = reader.GetString(reader.GetOrdinal("source_app")),
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_at"))),
            UpdatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("updated_at"))),
            LastUsedAt = reader.IsDBNull(reader.GetOrdinal("last_used_at")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("last_used_at"))),
            UseCount = reader.GetInt32(reader.GetOrdinal("use_count")),
            IsFavorite = reader.GetInt32(reader.GetOrdinal("is_favorite")) == 1,
            IsPinned = reader.GetInt32(reader.GetOrdinal("is_pinned")) == 1,
            IsDeleted = reader.GetInt32(reader.GetOrdinal("is_deleted")) == 1,
            FavoriteAlias = reader.GetString(reader.GetOrdinal("favorite_alias")),
            FavoriteTag = reader.GetString(reader.GetOrdinal("favorite_tag"))
        };
    }
}
