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
                last_used_at, use_count, is_favorite, is_pinned, is_deleted,
                favorite_alias, favorite_tag, favorited_at, favorite_order
            )
            VALUES(
                $content_text, $content_hash, $source_app, $created_at, $updated_at,
                NULL, 0, 0, 0, 0, '', '', NULL, 0
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

    public async Task<IReadOnlyList<ClipboardItem>> SearchAsync(
        string query,
        int limit = 80,
        bool favoritesOnly = false,
        bool includeDeletedFavorites = false)
    {
        await using var connection = database.OpenConnection();
        var command = connection.CreateCommand();
        var filters = favoritesOnly
            ? "is_favorite = 1"
            : includeDeletedFavorites
                ? "(is_deleted = 0 OR is_favorite = 1)"
                : "is_deleted = 0";
        if (!string.IsNullOrWhiteSpace(query))
        {
            filters += " AND (content_text LIKE $query OR source_app LIKE $query OR favorite_alias LIKE $query OR favorite_tag LIKE $query)";
            command.Parameters.AddWithValue("$query", $"%{query}%");
        }

        command.CommandText = favoritesOnly
            ? $"""
                SELECT * FROM clipboard_items
                WHERE {filters}
                ORDER BY is_pinned DESC,
                         CASE WHEN favorite_order > 0 THEN 0 ELSE 1 END ASC,
                         favorite_order ASC,
                         COALESCE(favorited_at, updated_at) DESC
                LIMIT $limit
                """
            : $"""
                SELECT * FROM clipboard_items
                WHERE {filters}
                ORDER BY updated_at DESC
                LIMIT $limit
                """;
        command.Parameters.AddWithValue("$limit", limit);

        var result = new List<ClipboardItem>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(ReadItem(reader));
        }

        return result;
    }

    public async Task<int> CountActiveAsync()
    {
        await using var connection = database.OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM clipboard_items WHERE is_deleted = 0";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async Task<int> CountFavoritesAsync()
    {
        await using var connection = database.OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM clipboard_items WHERE is_favorite = 1";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async Task<IReadOnlyList<string>> GetFavoriteTagsAsync()
    {
        await using var connection = database.OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name FROM favorite_tags
            UNION
            SELECT favorite_tag FROM clipboard_items WHERE favorite_tag <> ''
            ORDER BY name COLLATE NOCASE
            """;

        var result = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }

    public async Task AddFavoriteTagAsync(string tag)
    {
        tag = tag.Trim();
        if (string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        await using var connection = database.OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO favorite_tags(name, created_at)
            VALUES($name, $created_at)
            ON CONFLICT(name) DO NOTHING
            """;
        command.Parameters.AddWithValue("$name", tag);
        command.Parameters.AddWithValue("$created_at", DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    public async Task RenameFavoriteTagAsync(string oldName, string newName)
    {
        oldName = oldName.Trim();
        newName = newName.Trim();
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName) ||
            string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await using var connection = database.OpenConnection();
        await using var transaction = await connection.BeginTransactionAsync();

        var insert = connection.CreateCommand();
        insert.Transaction = (SqliteTransaction)transaction;
        insert.CommandText = """
            INSERT INTO favorite_tags(name, created_at)
            VALUES($name, $created_at)
            ON CONFLICT(name) DO NOTHING
            """;
        insert.Parameters.AddWithValue("$name", newName);
        insert.Parameters.AddWithValue("$created_at", DateTime.UtcNow.ToString("O"));
        await insert.ExecuteNonQueryAsync();

        var updateItems = connection.CreateCommand();
        updateItems.Transaction = (SqliteTransaction)transaction;
        updateItems.CommandText = "UPDATE clipboard_items SET favorite_tag = $new_name WHERE favorite_tag = $old_name";
        updateItems.Parameters.AddWithValue("$old_name", oldName);
        updateItems.Parameters.AddWithValue("$new_name", newName);
        await updateItems.ExecuteNonQueryAsync();

        var deleteOld = connection.CreateCommand();
        deleteOld.Transaction = (SqliteTransaction)transaction;
        deleteOld.CommandText = "DELETE FROM favorite_tags WHERE name = $old_name";
        deleteOld.Parameters.AddWithValue("$old_name", oldName);
        await deleteOld.ExecuteNonQueryAsync();

        await transaction.CommitAsync();
    }

    public async Task DeleteFavoriteTagAsync(string tag)
    {
        tag = tag.Trim();
        if (string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        await using var connection = database.OpenConnection();
        await using var transaction = await connection.BeginTransactionAsync();

        var clearItems = connection.CreateCommand();
        clearItems.Transaction = (SqliteTransaction)transaction;
        clearItems.CommandText = "UPDATE clipboard_items SET favorite_tag = '' WHERE favorite_tag = $tag";
        clearItems.Parameters.AddWithValue("$tag", tag);
        await clearItems.ExecuteNonQueryAsync();

        var deleteTag = connection.CreateCommand();
        deleteTag.Transaction = (SqliteTransaction)transaction;
        deleteTag.CommandText = "DELETE FROM favorite_tags WHERE name = $tag";
        deleteTag.Parameters.AddWithValue("$tag", tag);
        await deleteTag.ExecuteNonQueryAsync();

        await transaction.CommitAsync();
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

    public async Task SetFavoriteAsync(long id, string alias, string tag)
    {
        await using var connection = database.OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE clipboard_items
            SET is_favorite = 1,
                favorite_alias = $alias,
                favorite_tag = $tag,
                favorited_at = $now
            WHERE id = $id
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$alias", alias.Trim());
        command.Parameters.AddWithValue("$tag", tag.Trim());
        command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync();
        await AddFavoriteTagAsync(tag);
    }

    public async Task UnfavoriteAsync(long id)
    {
        await using var connection = database.OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE clipboard_items
            SET is_favorite = 0, is_pinned = 0, favorite_order = 0
            WHERE id = $id
            """;
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task SetPinnedAsync(long id, bool isPinned)
    {
        await using var connection = database.OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE clipboard_items SET is_pinned = $is_pinned WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$is_pinned", isPinned ? 1 : 0);
        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateFavoriteOrderAsync(IReadOnlyList<ClipboardItem> orderedItems)
    {
        await using var connection = database.OpenConnection();
        await using var transaction = await connection.BeginTransactionAsync();
        for (var index = 0; index < orderedItems.Count; index++)
        {
            var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = "UPDATE clipboard_items SET favorite_order = $order WHERE id = $id";
            command.Parameters.AddWithValue("$id", orderedItems[index].Id);
            command.Parameters.AddWithValue("$order", index + 1);
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
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
        command.CommandText = "UPDATE clipboard_items SET is_deleted = 1 WHERE is_favorite = 0";
        await command.ExecuteNonQueryAsync();
    }

    public async Task ClearFavoritesAsync()
    {
        await using var connection = database.OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE clipboard_items
            SET is_favorite = 0,
                is_pinned = 0,
                favorite_order = 0,
                favorite_alias = '',
                favorite_tag = '',
                favorited_at = NULL
            WHERE is_favorite = 1
            """;
        await command.ExecuteNonQueryAsync();
    }

    public async Task TrimHistoryAsync(int maxItems)
    {
        if (maxItems < 1)
        {
            maxItems = 1;
        }

        await using var connection = database.OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE clipboard_items
            SET is_deleted = 1
            WHERE id IN (
                SELECT id
                FROM clipboard_items
                WHERE is_deleted = 0
                  AND is_favorite = 0
                ORDER BY updated_at DESC
                LIMIT -1 OFFSET $max_items
            )
            """;
        command.Parameters.AddWithValue("$max_items", maxItems);
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
            FavoritedAt = reader.IsDBNull(reader.GetOrdinal("favorited_at")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("favorited_at"))),
            UseCount = reader.GetInt32(reader.GetOrdinal("use_count")),
            FavoriteOrder = reader.GetInt32(reader.GetOrdinal("favorite_order")),
            IsFavorite = reader.GetInt32(reader.GetOrdinal("is_favorite")) == 1,
            IsPinned = reader.GetInt32(reader.GetOrdinal("is_pinned")) == 1,
            IsDeleted = reader.GetInt32(reader.GetOrdinal("is_deleted")) == 1,
            FavoriteAlias = reader.GetString(reader.GetOrdinal("favorite_alias")),
            FavoriteTag = reader.GetString(reader.GetOrdinal("favorite_tag"))
        };
    }
}
