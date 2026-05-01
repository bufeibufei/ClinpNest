using Microsoft.Data.Sqlite;
using System.IO;

namespace ClipNest.Data;

public sealed class AppDatabase
{
    public AppDatabase()
    {
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipNest");
        Directory.CreateDirectory(appData);
        DatabasePath = Path.Combine(appData, "clipnest.db");
        ConnectionString = new SqliteConnectionStringBuilder { DataSource = DatabasePath }.ToString();
    }

    public string DatabasePath { get; }
    public string ConnectionString { get; }

    public async Task InitializeAsync()
    {
        await using var connection = OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS clipboard_items (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                content_text TEXT NOT NULL,
                content_hash TEXT NOT NULL UNIQUE,
                source_app TEXT NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                last_used_at TEXT NULL,
                use_count INTEGER NOT NULL DEFAULT 0,
                is_favorite INTEGER NOT NULL DEFAULT 0,
                is_pinned INTEGER NOT NULL DEFAULT 0,
                is_deleted INTEGER NOT NULL DEFAULT 0,
                favorite_alias TEXT NOT NULL DEFAULT '',
                favorite_tag TEXT NOT NULL DEFAULT '',
                favorited_at TEXT NULL,
                favorite_order INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS idx_clipboard_items_updated
                ON clipboard_items(is_deleted, is_pinned, updated_at);

            CREATE TABLE IF NOT EXISTS app_settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS favorite_tags (
                name TEXT PRIMARY KEY,
                created_at TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync();
        await AddColumnIfMissingAsync(connection, "clipboard_items", "favorite_alias", "TEXT NOT NULL DEFAULT ''");
        await AddColumnIfMissingAsync(connection, "clipboard_items", "favorite_tag", "TEXT NOT NULL DEFAULT ''");
        await AddColumnIfMissingAsync(connection, "clipboard_items", "favorited_at", "TEXT NULL");
        await AddColumnIfMissingAsync(connection, "clipboard_items", "favorite_order", "INTEGER NOT NULL DEFAULT 0");
    }

    private static async Task AddColumnIfMissingAsync(SqliteConnection connection, string table, string column, string definition)
    {
        var exists = false;
        var check = connection.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table})";
        await using (var reader = await check.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
        }

        if (exists)
        {
            return;
        }

        var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
        await alter.ExecuteNonQueryAsync();
    }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        return connection;
    }
}
