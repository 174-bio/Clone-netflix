using System.Text.Json;
using Microsoft.Data.Sqlite;
using NetflixClone.Models;

namespace NetflixClone.Services;

internal sealed class DataStorePayload
{
    public List<User> Users { get; set; } = new();
    public List<Movie> Movies { get; set; } = new();
    public List<WatchlistItem> Watchlist { get; set; } = new();
    public List<WatchProgress> Progress { get; set; } = new();
    public List<UserRatingRecord> Ratings { get; set; } = new();
    public List<UserSession> Sessions { get; set; } = new();
    public List<PasswordResetToken> ResetTokens { get; set; } = new();
}

internal sealed class SqliteDataStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null
    };

    private readonly string _connectionString;

    public SqliteDataStore(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        InitializeSchema();
    }

    public bool IsEmpty
    {
        get
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT NOT EXISTS (SELECT 1 FROM Users)
                    AND NOT EXISTS (SELECT 1 FROM Movies)
                    AND NOT EXISTS (SELECT 1 FROM Watchlist)
                    AND NOT EXISTS (SELECT 1 FROM Progress)
                    AND NOT EXISTS (SELECT 1 FROM Ratings)
                    AND NOT EXISTS (SELECT 1 FROM Sessions)
                    AND NOT EXISTS (SELECT 1 FROM PasswordResetTokens);
                """;
            return command.ExecuteScalar() is long result && result == 1;
        }
    }

    public DataStorePayload Load()
    {
        using var connection = OpenConnection();
        return new DataStorePayload
        {
            Users = ReadRows<User>(connection, "Users"),
            Movies = ReadRows<Movie>(connection, "Movies"),
            Watchlist = ReadRows<WatchlistItem>(connection, "Watchlist"),
            Progress = ReadRows<WatchProgress>(connection, "Progress"),
            Ratings = ReadRows<UserRatingRecord>(connection, "Ratings"),
            Sessions = ReadRows<UserSession>(connection, "Sessions"),
            ResetTokens = ReadRows<PasswordResetToken>(connection, "PasswordResetTokens")
        };
    }

    public void Save(DataStorePayload payload)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        ReplaceRows(connection, transaction, "Users", ["Id", "Email"], payload.Users,
            user => [user.Id, user.Email]);
        ReplaceRows(connection, transaction, "Movies", ["Id"], payload.Movies,
            movie => [movie.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        ReplaceRows(connection, transaction, "Watchlist", ["UserId", "MovieId"], payload.Watchlist,
            item => [item.UserId, item.MovieId.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        ReplaceRows(connection, transaction, "Progress", ["UserId", "MovieId"], payload.Progress,
            item => [item.UserId, item.MovieId.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        ReplaceRows(connection, transaction, "Ratings", ["UserId", "MovieId"], payload.Ratings,
            item => [item.UserId, item.MovieId.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        ReplaceRows(connection, transaction, "Sessions", ["Token"], payload.Sessions,
            session => [session.Token]);
        ReplaceRows(connection, transaction, "PasswordResetTokens", ["Email", "Token"], payload.ResetTokens,
            token => [token.Email, token.Token]);

        transaction.Commit();
    }

    private void InitializeSchema()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Users (
                Id TEXT PRIMARY KEY,
                Email TEXT NOT NULL UNIQUE,
                Payload TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Movies (
                Id TEXT PRIMARY KEY,
                Payload TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Watchlist (
                UserId TEXT NOT NULL,
                MovieId TEXT NOT NULL,
                Payload TEXT NOT NULL,
                PRIMARY KEY (UserId, MovieId)
            );
            CREATE TABLE IF NOT EXISTS Progress (
                UserId TEXT NOT NULL,
                MovieId TEXT NOT NULL,
                Payload TEXT NOT NULL,
                PRIMARY KEY (UserId, MovieId)
            );
            CREATE TABLE IF NOT EXISTS Ratings (
                UserId TEXT NOT NULL,
                MovieId TEXT NOT NULL,
                Payload TEXT NOT NULL,
                PRIMARY KEY (UserId, MovieId)
            );
            CREATE TABLE IF NOT EXISTS Sessions (
                Token TEXT PRIMARY KEY,
                Payload TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS PasswordResetTokens (
                Email TEXT NOT NULL,
                Token TEXT NOT NULL,
                Payload TEXT NOT NULL,
                PRIMARY KEY (Email, Token)
            );
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static List<T> ReadRows<T>(SqliteConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT Payload FROM {table}";
        using var reader = command.ExecuteReader();

        var rows = new List<T>();
        while (reader.Read())
        {
            var row = JsonSerializer.Deserialize<T>(reader.GetString(0), JsonOptions)
                ?? throw new InvalidDataException($"Registro inválido na tabela SQLite {table}.");
            rows.Add(row);
        }

        return rows;
    }

    private static void ReplaceRows<T>(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table,
        string[] keyColumns,
        IEnumerable<T> rows,
        Func<T, string[]> getKeys)
    {
        using var deleteCommand = connection.CreateCommand();
        deleteCommand.Transaction = transaction;
        deleteCommand.CommandText = $"DELETE FROM {table}";
        deleteCommand.ExecuteNonQuery();

        var keyParameters = Enumerable.Range(0, keyColumns.Length)
            .Select(index => $"$key{index}")
            .ToArray();
        using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText =
            $"INSERT INTO {table} ({string.Join(", ", keyColumns)}, Payload) " +
            $"VALUES ({string.Join(", ", keyParameters)}, $payload)";
        foreach (var parameter in keyParameters)
        {
            insertCommand.Parameters.AddWithValue(parameter, string.Empty);
        }
        insertCommand.Parameters.AddWithValue("$payload", string.Empty);

        foreach (var row in rows)
        {
            var keys = getKeys(row);
            if (keys.Length != keyParameters.Length)
            {
                throw new InvalidOperationException($"Chave inválida para a tabela SQLite {table}.");
            }

            for (var index = 0; index < keys.Length; index++)
            {
                insertCommand.Parameters[keyParameters[index]].Value = keys[index];
            }

            insertCommand.Parameters["$payload"].Value = JsonSerializer.Serialize(row, JsonOptions);
            insertCommand.ExecuteNonQuery();
        }
    }
}
