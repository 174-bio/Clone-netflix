using System.Data.Common;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Npgsql;
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
    private const string SqliteProvider = "sqlite";
    private const string PostgresProvider = "postgres";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null
    };

    private readonly string _provider;
    private readonly string _connectionString;

    public SqliteDataStore(
        string databasePath,
        string? configuredProvider,
        string? configuredConnectionString)
    {
        _provider = ResolveProvider(databasePath, configuredProvider, configuredConnectionString);
        _connectionString = ResolveConnectionString(databasePath, _provider, configuredConnectionString);

        if (_provider == SqliteProvider)
        {
            var directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        InitializeSchema();
    }

    public bool IsEmpty
    {
        get
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = _provider == PostgresProvider
                ? "SELECT NOT EXISTS (SELECT 1 FROM users) AND NOT EXISTS (SELECT 1 FROM movies) AND NOT EXISTS (SELECT 1 FROM watchlist) AND NOT EXISTS (SELECT 1 FROM progress) AND NOT EXISTS (SELECT 1 FROM ratings) AND NOT EXISTS (SELECT 1 FROM sessions) AND NOT EXISTS (SELECT 1 FROM password_reset_tokens);"
                : """
                    SELECT NOT EXISTS (SELECT 1 FROM Users)
                        AND NOT EXISTS (SELECT 1 FROM Movies)
                        AND NOT EXISTS (SELECT 1 FROM Watchlist)
                        AND NOT EXISTS (SELECT 1 FROM Progress)
                        AND NOT EXISTS (SELECT 1 FROM Ratings)
                        AND NOT EXISTS (SELECT 1 FROM Sessions)
                        AND NOT EXISTS (SELECT 1 FROM PasswordResetTokens);
                    """;
            return Convert.ToBoolean(command.ExecuteScalar());
        }
    }

    public DataStorePayload Load()
    {
        using var connection = OpenConnection();
        return new DataStorePayload
        {
            Users = ReadRows<User>(connection, _provider == PostgresProvider ? "users" : "Users"),
            Movies = ReadRows<Movie>(connection, _provider == PostgresProvider ? "movies" : "Movies"),
            Watchlist = ReadRows<WatchlistItem>(connection, _provider == PostgresProvider ? "watchlist" : "Watchlist"),
            Progress = ReadRows<WatchProgress>(connection, _provider == PostgresProvider ? "progress" : "Progress"),
            Ratings = ReadRows<UserRatingRecord>(connection, _provider == PostgresProvider ? "ratings" : "Ratings"),
            Sessions = ReadRows<UserSession>(connection, _provider == PostgresProvider ? "sessions" : "Sessions"),
            ResetTokens = ReadRows<PasswordResetToken>(connection, _provider == PostgresProvider ? "password_reset_tokens" : "PasswordResetTokens")
        };
    }

    public void Save(DataStorePayload payload)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        ReplaceRows(connection, transaction, _provider == PostgresProvider ? "users" : "Users", ["id", "email"], payload.Users,
            user => [user.Id, user.Email]);
        ReplaceRows(connection, transaction, _provider == PostgresProvider ? "movies" : "Movies", ["id"], payload.Movies,
            movie => [movie.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        ReplaceRows(connection, transaction, _provider == PostgresProvider ? "watchlist" : "Watchlist", ["user_id", "movie_id"], payload.Watchlist,
            item => [item.UserId, item.MovieId.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        ReplaceRows(connection, transaction, _provider == PostgresProvider ? "progress" : "Progress", ["user_id", "movie_id"], payload.Progress,
            item => [item.UserId, item.MovieId.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        ReplaceRows(connection, transaction, _provider == PostgresProvider ? "ratings" : "Ratings", ["user_id", "movie_id"], payload.Ratings,
            item => [item.UserId, item.MovieId.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        ReplaceRows(connection, transaction, _provider == PostgresProvider ? "sessions" : "Sessions", ["token"], payload.Sessions,
            session => [session.Token]);
        ReplaceRows(connection, transaction, _provider == PostgresProvider ? "password_reset_tokens" : "PasswordResetTokens", ["email", "token"], payload.ResetTokens,
            token => [token.Email, token.Token]);

        transaction.Commit();
    }

    private void InitializeSchema()
    {
        if (_provider == PostgresProvider)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS users (
                    id TEXT PRIMARY KEY,
                    email TEXT NOT NULL UNIQUE,
                    payload JSONB NOT NULL
                );
                CREATE TABLE IF NOT EXISTS movies (
                    id TEXT PRIMARY KEY,
                    payload JSONB NOT NULL
                );
                CREATE TABLE IF NOT EXISTS watchlist (
                    user_id TEXT NOT NULL,
                    movie_id TEXT NOT NULL,
                    payload JSONB NOT NULL,
                    PRIMARY KEY (user_id, movie_id)
                );
                CREATE TABLE IF NOT EXISTS progress (
                    user_id TEXT NOT NULL,
                    movie_id TEXT NOT NULL,
                    payload JSONB NOT NULL,
                    PRIMARY KEY (user_id, movie_id)
                );
                CREATE TABLE IF NOT EXISTS ratings (
                    user_id TEXT NOT NULL,
                    movie_id TEXT NOT NULL,
                    payload JSONB NOT NULL,
                    PRIMARY KEY (user_id, movie_id)
                );
                CREATE TABLE IF NOT EXISTS sessions (
                    token TEXT PRIMARY KEY,
                    payload JSONB NOT NULL
                );
                CREATE TABLE IF NOT EXISTS password_reset_tokens (
                    email TEXT NOT NULL,
                    token TEXT NOT NULL,
                    payload JSONB NOT NULL,
                    PRIMARY KEY (email, token)
                );
                CREATE INDEX IF NOT EXISTS idx_users_email ON users (email);
                CREATE INDEX IF NOT EXISTS idx_watchlist_user_id ON watchlist (user_id);
                CREATE INDEX IF NOT EXISTS idx_progress_user_id ON progress (user_id);
                CREATE INDEX IF NOT EXISTS idx_ratings_user_id ON ratings (user_id);
                CREATE INDEX IF NOT EXISTS idx_sessions_token ON sessions (token);
                """;
            command.ExecuteNonQuery();
            return;
        }

        using var sqliteConnection = OpenConnection();
        using var sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.CommandText = """
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
        sqliteCommand.ExecuteNonQuery();
    }

    private DbConnection OpenConnection()
    {
        if (_provider == PostgresProvider)
        {
            var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            return connection;
        }

        var sqliteConnection = new SqliteConnection(_connectionString);
        sqliteConnection.Open();
        return sqliteConnection;
    }

    private static string ResolveProvider(
        string databasePath,
        string? configuredProvider,
        string? configuredConnectionString)
    {
        if (!string.IsNullOrWhiteSpace(configuredProvider))
        {
            if (string.Equals(configuredProvider, PostgresProvider, StringComparison.OrdinalIgnoreCase))
            {
                return PostgresProvider;
            }

            if (string.Equals(configuredProvider, SqliteProvider, StringComparison.OrdinalIgnoreCase))
            {
                return SqliteProvider;
            }

            throw new InvalidOperationException(
                $"Provedor de banco de dados não suportado: {configuredProvider}.");
        }

        if (!string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            return PostgresProvider;
        }

        if (databasePath.Contains("Host=", StringComparison.OrdinalIgnoreCase)
            || databasePath.Contains("postgres", StringComparison.OrdinalIgnoreCase)
            || databasePath.Contains("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return PostgresProvider;
        }

        return SqliteProvider;
    }

    private static string ResolveConnectionString(
        string databasePath,
        string provider,
        string? configuredConnectionString)
    {
        if (provider == PostgresProvider)
        {
            if (!string.IsNullOrWhiteSpace(configuredConnectionString))
            {
                return NormalizePostgresConnectionString(configuredConnectionString);
            }

            if (databasePath.Contains("Host=", StringComparison.OrdinalIgnoreCase))
            {
                return databasePath;
            }

            throw new InvalidOperationException(
                "O PostgreSQL foi selecionado, mas DATABASE_URL ou ConnectionStrings:DefaultConnection não foi configurada.");
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };
        return builder.ToString();
    }

    private static string NormalizePostgresConnectionString(string connectionString)
    {
        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri)
            || (uri.Scheme != "postgres" && uri.Scheme != "postgresql"))
        {
            return connectionString;
        }

        var credentials = uri.UserInfo.Split(':', 2);
        if (credentials.Length != 2 || string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new InvalidOperationException("DATABASE_URL não contém credenciais PostgreSQL válidas.");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = Uri.UnescapeDataString(credentials[1])
        };

        if (string.IsNullOrWhiteSpace(builder.Database))
        {
            throw new InvalidOperationException("DATABASE_URL não contém o nome do banco PostgreSQL.");
        }

        return builder.ConnectionString;
    }

    private static List<T> ReadRows<T>(DbConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = connection is NpgsqlConnection
            ? $"SELECT payload::text FROM {table};"
            : $"SELECT Payload FROM {table};";
        using var reader = command.ExecuteReader();

        var rows = new List<T>();
        while (reader.Read())
        {
            var payload = reader.GetString(0);
            var row = JsonSerializer.Deserialize<T>(payload, JsonOptions)
                ?? throw new InvalidDataException($"Registro inválido na tabela {table}.");
            rows.Add(row);
        }

        return rows;
    }

    private static void ReplaceRows<T>(
        DbConnection connection,
        DbTransaction transaction,
        string table,
        string[] keyColumns,
        IEnumerable<T> rows,
        Func<T, string[]> getKeys)
    {
        if (connection is NpgsqlConnection)
        {
            using var deleteCommand = connection.CreateCommand();
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = $"DELETE FROM {table};";
            deleteCommand.ExecuteNonQuery();

            foreach (var row in rows)
            {
                var keys = getKeys(row);
                var keyList = string.Join(", ", keyColumns);
                var valueList = string.Join(", ", keyColumns.Select((_, index) => $"@key{index}"));
                var payloadValue = JsonSerializer.Serialize(row, JsonOptions);
                using var postgresInsertCommand = connection.CreateCommand();
                postgresInsertCommand.Transaction = transaction;
                postgresInsertCommand.CommandText = $"INSERT INTO {table} ({keyList}, payload) VALUES ({valueList}, @payload) ON CONFLICT ({keyList}) DO UPDATE SET payload = EXCLUDED.payload;";

                for (var index = 0; index < keyColumns.Length; index++)
                {
                    var parameter = postgresInsertCommand.CreateParameter();
                    parameter.ParameterName = $"@key{index}";
                    parameter.Value = keys[index];
                    postgresInsertCommand.Parameters.Add(parameter);
                }

                var payloadParameter = postgresInsertCommand.CreateParameter();
                payloadParameter.ParameterName = "@payload";
                payloadParameter.Value = payloadValue;
                postgresInsertCommand.Parameters.Add(payloadParameter);

                postgresInsertCommand.ExecuteNonQuery();
            }

            return;
        }

        using var sqliteDeleteCommand = connection.CreateCommand();
        sqliteDeleteCommand.Transaction = transaction;
        sqliteDeleteCommand.CommandText = $"DELETE FROM {table}";
        sqliteDeleteCommand.ExecuteNonQuery();

        var sqliteKeyParameters = Enumerable.Range(0, keyColumns.Length)
            .Select(index => "$key" + index)
            .ToArray();
        using var sqliteInsertCommand = connection.CreateCommand();
        sqliteInsertCommand.Transaction = transaction;
        sqliteInsertCommand.CommandText =
            $"INSERT INTO {table} ({string.Join(", ", keyColumns)}, Payload) " +
            $"VALUES ({string.Join(", ", sqliteKeyParameters)}, $payload)";

        foreach (var parameterName in sqliteKeyParameters)
        {
            var parameter = sqliteInsertCommand.CreateParameter();
            parameter.ParameterName = parameterName;
            parameter.Value = string.Empty;
            sqliteInsertCommand.Parameters.Add(parameter);
        }

        var sqlitePayloadParameter = sqliteInsertCommand.CreateParameter();
        sqlitePayloadParameter.ParameterName = "$payload";
        sqlitePayloadParameter.Value = string.Empty;
        sqliteInsertCommand.Parameters.Add(sqlitePayloadParameter);

        foreach (var row in rows)
        {
            var keys = getKeys(row);
            if (keys.Length != sqliteKeyParameters.Length)
            {
                throw new InvalidOperationException($"Chave inválida para a tabela SQLite {table}.");
            }

            for (var index = 0; index < keys.Length; index++)
            {
                sqliteInsertCommand.Parameters[sqliteKeyParameters[index]].Value = keys[index];
            }

            sqliteInsertCommand.Parameters["$payload"].Value = JsonSerializer.Serialize(row, JsonOptions);
            sqliteInsertCommand.ExecuteNonQuery();
        }
    }
}
