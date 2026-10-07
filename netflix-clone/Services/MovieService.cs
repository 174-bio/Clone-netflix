using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NetflixClone.Models;

namespace NetflixClone.Services;

public class MovieService
{
    private readonly SqliteDataStore _dataStore;
    private readonly ILogger<MovieService> _logger;
    private readonly HashSet<string> _adminEmails;
    private readonly object _lock = new();

    private List<User> _users = new();
    private List<Movie> _movies = new();
    private List<WatchlistItem> _watchlist = new();
    private List<WatchProgress> _progress = new();
    private List<UserRatingRecord> _ratings = new();
    private List<UserSession> _sessions = new();
    private List<PasswordResetToken> _resetTokens = new();

    private const int Pbkdf2Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public MovieService(IConfiguration configuration, ILogger<MovieService> logger)
    {
        _logger = logger;
        _adminEmails = (configuration["Admin:Emails"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var databasePath = configuration["Database:Path"] ?? "Data/cinestream.db";
        if (!Path.IsPathRooted(databasePath))
        {
            databasePath = Path.Combine(AppContext.BaseDirectory, databasePath);
        }

        _dataStore = new SqliteDataStore(databasePath);

        LoadData();
    }

    #region Security Helpers (PBKDF2, Timing-Safe Comparison & Session Token)

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string enteredPassword, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrWhiteSpace(enteredPassword))
            return false;

        // Suporte ao formato seguro Salt:Hash (PBKDF2)
        var parts = storedHash.Split(':');
        if (parts.Length == 2)
        {
            try
            {
                var salt = Convert.FromBase64String(parts[0]);
                var expectedHash = Convert.FromBase64String(parts[1]);

                var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                    Encoding.UTF8.GetBytes(enteredPassword),
                    salt,
                    Pbkdf2Iterations,
                    HashAlgorithmName.SHA256,
                    KeySize);

                return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            }
            catch
            {
                return false;
            }
        }

        // Fallback seguro de transição para o hash legado (com atualização imediata de senha)
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes("CineStream_Salt_2026_" + enteredPassword);
        var legacyHash = Convert.ToBase64String(sha256.ComputeHash(bytes));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(legacyHash),
            Encoding.UTF8.GetBytes(storedHash));
    }

    private static string GenerateSecureToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 100) return false;
        return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
    }

    private static string SanitizeInput(string? input, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var trimmed = input.Trim();
        if (trimmed.Length > maxLength) trimmed = trimmed[..maxLength];
        return WebUtility.HtmlEncode(trimmed);
    }

    #endregion

    #region Persistence & Seeding (Atomic File Writing)

    private void LoadData()
    {
        lock (_lock)
        {
            var payload = _dataStore.Load();
            if (payload.Movies.Count >= 20)
            {
                RestorePayload(payload);
                _sessions.RemoveAll(s => s.ExpiresAt <= DateTime.UtcNow);
                return;
            }

            if (!_dataStore.IsEmpty)
            {
                throw new InvalidDataException("O banco SQLite contém dados incompletos e não será sobrescrito.");
            }

            var legacyDataPath = Path.Combine(AppContext.BaseDirectory, "Data", "cinestream_data.json");
            if (File.Exists(legacyDataPath))
            {
                try
                {
                    var json = File.ReadAllText(legacyDataPath);
                    var legacyPayload = JsonSerializer.Deserialize<DataStorePayload>(json);
                    if (legacyPayload?.Movies.Count >= 20)
                    {
                        RestorePayload(legacyPayload);
                        _sessions.RemoveAll(s => s.ExpiresAt <= DateTime.UtcNow);
                        SaveData();
                        _logger.LogInformation("Dados migrados de {LegacyDataPath} para SQLite.", legacyDataPath);
                        return;
                    }
                }
                catch (Exception ex) when (ex is JsonException or IOException)
                {
                    _logger.LogError(ex, "Não foi possível migrar os dados legados de {LegacyDataPath}.", legacyDataPath);
                    throw new InvalidDataException("Falha ao migrar os dados legados para SQLite.", ex);
                }
            }

            SeedInitialData();
            SaveData();
        }
    }

    private void RestorePayload(DataStorePayload payload)
    {
        _users = payload.Users ?? new();
        _movies = payload.Movies ?? new();
        _watchlist = payload.Watchlist ?? new();
        _progress = payload.Progress ?? new();
        _ratings = payload.Ratings ?? new();
        _sessions = payload.Sessions ?? new();
        _resetTokens = payload.ResetTokens ?? new();
    }

    private void SaveData()
    {
        lock (_lock)
        {
            try
            {
                _dataStore.Save(new DataStorePayload
                {
                    Users = _users,
                    Movies = _movies,
                    Watchlist = _watchlist,
                    Progress = _progress,
                    Ratings = _ratings,
                    Sessions = _sessions,
                    ResetTokens = _resetTokens
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar os dados no banco SQLite.");
                throw;
            }
        }
    }

    private void SeedInitialData()
    {
        _users.Clear();
        _movies.Clear();
        _watchlist.Clear();
        _progress.Clear();
        _ratings.Clear();
        _sessions.Clear();

        // Usuário Demo com PBKDF2 seguro
        var demoUser = new User
        {
            Id = "demo-user-cinestream-01",
            Name = "Alexandre Silva",
            Email = "demo@cinestream.tv",
            PasswordHash = HashPassword("cine123456"),
            Avatar = "https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=200&q=80",
            CreatedAt = DateTime.UtcNow.AddMonths(-3),
            Preferences = new UserPreferences
            {
                AutoPlayNextEpisode = true,
                PreferredQuality = "1080p",
                PreferredAudioLanguage = "Português (Brasil)",
                PreferredSubtitleLanguage = "Português (Brasil)"
            }
        };
        _users.Add(demoUser);

        // Gera sessão inicial ativa para o usuário demo
        var demoToken = "cs_demo_" + GenerateSecureToken();
        _sessions.Add(new UserSession
        {
            Token = demoToken,
            UserId = demoUser.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        });

        // Vídeos de demonstração licenciados e abertos
        const string videoSintel = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/Sintel.mp4";
        const string videoTears = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/TearsOfSteel.mp4";
        const string videoBunny = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4";
        const string videoElephants = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ElephantsDream.mp4";
        const string videoBlazes = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerBlazes.mp4";
        const string videoEscapes = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerEscapes.mp4";
        const string videoFun = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerFun.mp4";
        const string videoJoyrides = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerJoyrides.mp4";
        const string videoMeltdowns = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerMeltdowns.mp4";
        const string videoBullrun = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/WeAreGoingOnBullrun.mp4";

        var movies = new List<Movie>
        {
            new Movie
            {
                Id = 1,
                Title = "Cyberpunk: O Último Protocolo",
                Description = "Em uma Neo-São Paulo de 2088 dominada por megacorporações biônicas, um ex-hacker tático descobre um código neural capaz de reprogramar a consciência humana antes do Grande Blecaute.",
                Type = "movie",
                BackdropUrl = "https://images.unsplash.com/photo-1578632767115-351597cf2477?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1542751371-adc38448a05e?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoTears,
                Year = 2026,
                Rating = "18+",
                Score = 9.4,
                MatchScore = 99,
                Duration = "2h 18m",
                Genres = new() { "Ficção Científica", "Ação", "Cyberpunk" },
                Cast = new() { "Lucas Santana", "Renata Valente", "Viktor Chen", "Elena Rostova" },
                Director = "Denis Villeneuve Jr.",
                IsFeatured = true,
                Likes = 4820
            },
            new Movie
            {
                Id = 2,
                Title = "Horizonte Interestelar: Êxodo",
                Description = "Quando o núcleo da Terra começa sua desaceleração térmica, uma tripulação de físicos e pilotos de elite embarca em uma missão sem retorno através de uma anomalia gravitacional nas luas de Júpiter.",
                Type = "movie",
                BackdropUrl = "https://images.unsplash.com/photo-1451187580459-43490279c0fa?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1506703719100-a0f3a48c0f86?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoSintel,
                Year = 2025,
                Rating = "14+",
                Score = 9.6,
                MatchScore = 98,
                Duration = "2h 45m",
                Genres = new() { "Ficção Científica", "Drama", "Aventura" },
                Cast = new() { "Mateus Becker", "Camila Lins", "David Oyelowo", "Sara Kim" },
                Director = "Christopher Nolan",
                IsFeatured = false,
                Likes = 6120
            },
            new Movie
            {
                Id = 3,
                Title = "Operação Sombra: Ouroboros",
                Description = "Um agente renegado do serviço de inteligência descobre uma conspiração de nível governamental que manipula eleições e atentados globais por meio de satélites quânticos.",
                Type = "movie",
                BackdropUrl = "https://images.unsplash.com/photo-1509198397868-475647b2a1e5?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1536440136628-849c177e76a1?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoBlazes,
                Year = 2026,
                Rating = "16+",
                Score = 8.9,
                MatchScore = 95,
                Duration = "1h 56m",
                Genres = new() { "Ação", "Suspense", "Policial" },
                Cast = new() { "Daniel Craigman", "Rodrigo Santoro", "Zoe Saldana" },
                Director = "Chad Stahelski",
                IsFeatured = false,
                Likes = 3410
            },
            new Movie
            {
                Id = 4,
                Title = "O Legado do Dragão Celestial",
                Description = "No topo das cordilheiras proibidas, o último guardião das chamas celestiais precisa treinar uma órfã rebelde antes que o exército sombrio das cinzas tome o santuário milenar.",
                Type = "movie",
                BackdropUrl = "https://images.unsplash.com/photo-1518709268805-4e9042af9f23?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1579783900882-c0d3dad7b119?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoSintel,
                Year = 2025,
                Rating = "12+",
                Score = 9.1,
                MatchScore = 94,
                Duration = "2h 12m",
                Genres = new() { "Fantasia", "Ação", "Artes Marciais" },
                Cast = new() { "Tony Jaa", "Michelle Yeoh", "Simu Liu" },
                Director = "Yimou Zhang",
                IsFeatured = false,
                Likes = 2950
            },
            new Movie
            {
                Id = 5,
                Title = "Labirinto da Meia-Noite",
                Description = "Uma renomada detetive forense investiga uma série de desaparecimentos misteriosos em um hospital psiquiátrico abandonado nas montanhas da Suíça.",
                Type = "movie",
                BackdropUrl = "https://images.unsplash.com/photo-1486406146926-c627a92ad1ab?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1509281373149-e957c6296406?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoEscapes,
                Year = 2024,
                Rating = "18+",
                Score = 8.8,
                MatchScore = 92,
                Duration = "1h 50m",
                Genres = new() { "Suspense", "Mistério", "Terror Psicológico" },
                Cast = new() { "Rebecca Ferguson", "Mads Mikkelsen", "Stellan Skarsgård" },
                Director = "David Fincher",
                IsFeatured = false,
                Likes = 2810
            },
            new Movie
            {
                Id = 6,
                Title = "As Crônicas de Aethelgard",
                Description = "Quando o portal entre os reinos dos deuses antigos e dos mortais se rompe, três campeões improváveis precisam forjar a lâmina de oricalco nas profundezas da forja esquecida.",
                Type = "movie",
                BackdropUrl = "https://images.unsplash.com/photo-1514539079130-25950c84af65?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1534447677768-be436bb09401?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoElephants,
                Year = 2025,
                Rating = "14+",
                Score = 9.3,
                MatchScore = 96,
                Duration = "2h 35m",
                Genres = new() { "Fantasia", "Aventura", "Épico" },
                Cast = new() { "Henry Cavill", "Katheryn Winnick", "Alexander Skarsgård" },
                Director = "Peter Jackson",
                IsFeatured = false,
                Likes = 5200
            },
            new Movie
            {
                Id = 7,
                Title = "Velocidade Terminal",
                Description = "Um ex-piloto de Fórmula 1 é chantageado por um sindicato do crime internacional para transportar um protótipo de supercondutor através dos desfiladeiros de Mônaco em 60 minutos.",
                Type = "movie",
                BackdropUrl = "https://images.unsplash.com/photo-1503376780353-7e6692767b70?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1568605117036-5fe5e7bab0b7?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoJoyrides,
                Year = 2026,
                Rating = "14+",
                Score = 8.7,
                MatchScore = 91,
                Duration = "1h 48m",
                Genres = new() { "Ação", "Policial", "Adrenalina" },
                Cast = new() { "Michael B. Jordan", "Ana de Armas", "Jason Statham" },
                Director = "Justin Lin",
                IsFeatured = false,
                Likes = 3100
            },
            new Movie
            {
                Id = 8,
                Title = "O Guardião das Florestas Encantadas",
                Description = "Uma encantadora e deslumbrante animação que narra a amizade entre uma pequena raposa mística e um espírito ancestral da floresta em sua jornada para restaurar a Árvore da Vida.",
                Type = "movie",
                BackdropUrl = "https://images.unsplash.com/photo-1511447333015-45b65e60f6d5?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1534447677768-be436bb09401?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoBunny,
                Year = 2025,
                Rating = "Livre",
                Score = 9.5,
                MatchScore = 97,
                Duration = "1h 35m",
                Genres = new() { "Animação", "Família", "Fantasia" },
                Cast = new() { "Selena Gomez (Voz)", "Tom Holland (Voz)", "Wagner Moura (Voz)" },
                Director = "Hayao Miyazaki",
                IsFeatured = false,
                Likes = 4300
            },
            new Movie
            {
                Id = 9,
                Title = "Golpe em Tóquio: Código Neon",
                Description = "Dois trapaceiros carismáticos e uma hacker genial planejam o maior assalto virtual da história ao cofre subterrâneo do Banco Imperial do Japão.",
                Type = "movie",
                BackdropUrl = "https://images.unsplash.com/photo-1503899036084-c55cdd92da26?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1514565131-fce0801e5785?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoFun,
                Year = 2025,
                Rating = "14+",
                Score = 8.8,
                MatchScore = 93,
                Duration = "2h 05m",
                Genres = new() { "Comédia", "Ação", "Policial" },
                Cast = new() { "Ryan Reynolds", "Hiroyuki Sanada", "Karen Fukuhara" },
                Director = "Edgar Wright",
                IsFeatured = false,
                Likes = 3890
            },
            new Movie
            {
                Id = 10,
                Title = "Profundezas Abissais",
                Description = "A 11.000 metros de profundidade na Fossa das Marianas, uma estação oceanográfica pioneira faz contato com uma civilização bio-luminescente desconhecida.",
                Type = "movie",
                BackdropUrl = "https://images.unsplash.com/photo-1682687220063-4742bd7fd538?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1544551763-46a013bb70d5?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoMeltdowns,
                Year = 2024,
                Rating = "16+",
                Score = 8.6,
                MatchScore = 89,
                Duration = "2h 02m",
                Genres = new() { "Ficção Científica", "Suspense", "Aventura" },
                Cast = new() { "Jessica Chastain", "Cillian Murphy", "Hiroyuki Sanada" },
                Director = "James Cameron",
                IsFeatured = false,
                Likes = 2750
            },
            new Movie
            {
                Id = 11,
                Title = "O Pianista de Varsóvia: Ressonância",
                Description = "A comovente história de um virtuoso pianista que usa a música clássica como arma secreta de resistência e união durante os dias mais sombrios da Europa.",
                Type = "movie",
                BackdropUrl = "https://images.unsplash.com/photo-1520523839898-507127092801?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1511671782779-c97d3d27a1d4?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoSintel,
                Year = 2024,
                Rating = "14+",
                Score = 9.5,
                MatchScore = 96,
                Duration = "2h 22m",
                Genres = new() { "Drama", "Histórico", "Música" },
                Cast = new() { "Adrien Brody", "Marion Cotillard", "Daniel Brühl" },
                Director = "Damien Chazelle",
                IsFeatured = false,
                Likes = 4120
            },
            new Movie
            {
                Id = 12,
                Title = "Revolução dos Algoritmos",
                Description = "Um documentário investigativo revelando os bastidores dos maiores laboratórios de inteligência artificial e a corrida global pelo avanço da mente humana.",
                Type = "movie",
                BackdropUrl = "https://images.unsplash.com/photo-1518770660439-4636190af475?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1526374965328-7f61d4dc18c5?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoBullrun,
                Year = 2025,
                Rating = "10+",
                Score = 9.0,
                MatchScore = 92,
                Duration = "1h 42m",
                Genres = new() { "Documentário", "Tecnologia", "Ficção Científica" },
                Cast = new() { "Dr. Stuart Russell", "Yuval Noah Harari", "Fei-Fei Li" },
                Director = "Alex Gibney",
                IsFeatured = false,
                Likes = 1980
            },
            new Movie
            {
                Id = 13,
                Title = "Sol de Inverno em Paris",
                Description = "Dois arquitetos rivais são contratados para restaurar um palácio histórico na margem do Sena, descobrindo cartas de amor secretas de um século atrás que mudarão seus destinos.",
                Type = "movie",
                BackdropUrl = "https://images.unsplash.com/photo-1502602898657-3e91760cbb34?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1511739001486-6bfe10ce785f?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoSintel,
                Year = 2025,
                Rating = "12+",
                Score = 8.5,
                MatchScore = 88,
                Duration = "1h 54m",
                Genres = new() { "Romance", "Drama", "Comédia" },
                Cast = new() { "Timothée Chalamet", "Léa Seydoux", "Louis Garrel" },
                Director = "Céline Sciamma",
                IsFeatured = false,
                Likes = 2450
            },
            new Movie
            {
                Id = 14,
                Title = "A Fortaleza dos Ventos",
                Description = "Na Idade Média nórdica, um clã de guerreiros solitários defende uma cidadela esculpida nas rochas contra hordas invasoras e monstros da névoa eterna.",
                Type = "movie",
                BackdropUrl = "https://images.unsplash.com/photo-1464822759023-fed622ff2c3b?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1506744038136-46273834b3fb?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoTears,
                Year = 2024,
                Rating = "16+",
                Score = 8.9,
                MatchScore = 93,
                Duration = "2h 08m",
                Genres = new() { "Ação", "Fantasia", "Histórico" },
                Cast = new() { "Travis Fimmel", "Clive Standen", "Alyssa Sutherland" },
                Director = "Robert Eggers",
                IsFeatured = false,
                Likes = 3120
            },
            new Movie
            {
                Id = 15,
                Title = "Ilusão Mortal",
                Description = "Um mestre dos truques de mágica e hipnose se vê preso em um jogo de gato e rato com a Interpol após um roubo espetacular durante uma transmissão ao vivo.",
                Type = "movie",
                BackdropUrl = "https://images.unsplash.com/photo-1516450360452-9312f5e86fc7?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1492684223066-81342ee5ff30?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoEscapes,
                Year = 2026,
                Rating = "14+",
                Score = 8.7,
                MatchScore = 90,
                Duration = "1h 58m",
                Genres = new() { "Suspense", "Mistério", "Policial" },
                Cast = new() { "Jesse Eisenberg", "Woody Harrelson", "Isla Fisher" },
                Director = "Louis Leterrier",
                IsFeatured = false,
                Likes = 2890
            },
            new Movie
            {
                Id = 16,
                Title = "Cosmos: Além das Galáxias",
                Description = "Uma odisseia visual espetacular capturada pelos telescópios espaciais James Webb e Hubble, narrando o nascimento das primeiras estrelas e buracos negros supermassivos.",
                Type = "movie",
                BackdropUrl = "https://images.unsplash.com/photo-1446776811953-b23d57bd21aa?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1451187580459-43490279c0fa?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoElephants,
                Year = 2025,
                Rating = "Livre",
                Score = 9.7,
                MatchScore = 99,
                Duration = "1h 38m",
                Genres = new() { "Documentário", "Ciência", "Espaço" },
                Cast = new() { "Neil deGrasse Tyson (Narração)", "Morgan Freeman" },
                Director = "Ann Druyan",
                IsFeatured = false,
                Likes = 5890
            },
            new Movie
            {
                Id = 17,
                Title = "A Noite dos Renegados",
                Description = "Em uma Los Angeles chuvosa e decadente dos anos 80, um grupo de mercenários aposentados se reúne para uma última missão de resgate.",
                Type = "movie",
                BackdropUrl = "https://images.unsplash.com/photo-1519501025264-65ba15a82390?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1508873696983-2df5293cb32b?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoBlazes,
                Year = 2024,
                Rating = "18+",
                Score = 8.6,
                MatchScore = 88,
                Duration = "2h 01m",
                Genres = new() { "Ação", "Suspense", "Policial" },
                Cast = new() { "Kurt Russell", "Sylvester Stallone", "Willem Dafoe" },
                Director = "John Carpenter Jr.",
                IsFeatured = false,
                Likes = 2100
            },
            new Movie
            {
                Id = 18,
                Title = "O Mistério do Expresso Oriental 2050",
                Description = "Um trem magnético transcontinental para no meio dos Alpes congelados após o assassinato do homem mais rico do planeta. Todos a bordo são suspeitos.",
                Type = "movie",
                BackdropUrl = "https://images.unsplash.com/photo-1474487548417-781cb71495f3?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1517649763962-0c623266ddc0?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoFun,
                Year = 2025,
                Rating = "14+",
                Score = 9.0,
                MatchScore = 93,
                Duration = "2h 15m",
                Genres = new() { "Mistério", "Suspense", "Ficção Científica" },
                Cast = new() { "Kenneth Branagh", "Penélope Cruz", "Willem Dafoe" },
                Director = "Kenneth Branagh",
                IsFeatured = false,
                Likes = 3490
            },
            new Movie
            {
                Id = 19,
                Title = "Samurai do Fim do Mundo",
                Description = "Após o apocalipse nuclear, um ronin solitário empunha sua katana de titânio para proteger um comboio de sobreviventes através dos desertos de cinzas.",
                Type = "movie",
                BackdropUrl = "https://images.unsplash.com/photo-1579783902614-a3fb3927b675?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1528164344705-475426879c0d?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoTears,
                Year = 2026,
                Rating = "18+",
                Score = 9.2,
                MatchScore = 95,
                Duration = "2h 11m",
                Genres = new() { "Ação", "Artes Marciais", "Pós-Apocalíptico" },
                Cast = new() { "Tadanobu Asano", "Ken Watanabe", "Rinko Kikuchi" },
                Director = "Takashi Miike",
                IsFeatured = false,
                Likes = 4450
            },
            new Movie
            {
                Id = 20,
                Title = "Amor em Gravidade Zero",
                Description = "Dois astrofísicos isolados em estações espaciais opostas ao redor da Lua comunicam-se secretamente enquanto uma guerra diplomática se desenrola na Terra.",
                Type = "movie",
                BackdropUrl = "https://images.unsplash.com/photo-1451187580459-43490279c0fa?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1446776811953-b23d57bd21aa?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoSintel,
                Year = 2025,
                Rating = "12+",
                Score = 8.8,
                MatchScore = 90,
                Duration = "1h 52m",
                Genres = new() { "Romance", "Ficção Científica", "Drama" },
                Cast = new() { "Saoirse Ronan", "Paul Mescal", "Andrew Scott" },
                Director = "Garth Davis",
                IsFeatured = false,
                Likes = 2780
            },
            new Movie
            {
                Id = 21,
                Title = "Riso Solto no Rio",
                Description = "Uma comédia vibrante e musical ambientada nas praias e ladeiras do Rio de Janeiro, com encontros e desencontros entre artistas de rua e produtores excêntricos.",
                Type = "movie",
                BackdropUrl = "https://images.unsplash.com/photo-1483729558449-99ef09a8c325?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1516450360452-9312f5e86fc7?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoBunny,
                Year = 2024,
                Rating = "12+",
                Score = 8.4,
                MatchScore = 87,
                Duration = "1h 45m",
                Genres = new() { "Comédia", "Música", "Nacional" },
                Cast = new() { "Fabio Porchat", "Tata Werneck", "Paulo Gustavo Jr." },
                Director = "Guel Arraes",
                IsFeatured = false,
                Likes = 3670
            },
            new Movie
            {
                Id = 22,
                Title = "A Sombra de Sherlock",
                Description = "Uma jovem prodígio descobre anotações inéditas de Sherlock Holmes em Londres e se vê perseguida pela sociedade secreta dos Descendentes de Moriarty.",
                Type = "movie",
                BackdropUrl = "https://images.unsplash.com/photo-1513635269975-59663e0ac1ad?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1526374965328-7f61d4dc18c5?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoBlazes,
                Year = 2025,
                Rating = "14+",
                Score = 8.9,
                MatchScore = 92,
                Duration = "2h 04m",
                Genres = new() { "Mistério", "Aventura", "Suspense" },
                Cast = new() { "Millie Bobby Brown", "Henry Cavill", "Helena Bonham Carter" },
                Director = "Harry Bradbeer",
                IsFeatured = false,
                Likes = 4120
            }
        };

        var seriesList = new List<Movie>
        {
            new Movie
            {
                Id = 23,
                Title = "Nexus: Cidade dos Deuses Digitais",
                Description = "Em um mundo onde cidadãos vendem horas de memória para servidores em nuvem, um inspetor de polícia cibernética descobre uma seita que promete a imortalidade digital.",
                Type = "series",
                BackdropUrl = "https://images.unsplash.com/photo-1509198397868-475647b2a1e5?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1578632767115-351597cf2477?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoTears,
                Year = 2026,
                Rating = "18+",
                Score = 9.6,
                MatchScore = 99,
                Duration = "2 Temporadas",
                Genres = new() { "Ficção Científica", "Cyberpunk", "Suspense" },
                Cast = new() { "Pedro Pascal", "Gemma Chan", "Hiroyuki Sanada" },
                Director = "Alex Garland",
                IsFeatured = false,
                Likes = 7890,
                Episodes = new()
                {
                    new Episode { Id = 101, SeriesId = 23, Season = 1, Number = 1, Title = "Episódio 1: Protocolo Fantasma", Duration = "52 min", Description = "O detetive Marcos é chamado para investigar a morte súbita do arquiteto da rede neural Nexus.", ThumbnailUrl = "https://images.unsplash.com/photo-1578632767115-351597cf2477?auto=format&fit=crop&w=400&q=80", VideoUrl = videoTears },
                    new Episode { Id = 102, SeriesId = 23, Season = 1, Number = 2, Title = "Episódio 2: Memória Corrompida", Duration = "48 min", Description = "Um implante com memórias roubadas de um senador aponta para um complô corporativo na Zona Franca.", ThumbnailUrl = "https://images.unsplash.com/photo-1509198397868-475647b2a1e5?auto=format&fit=crop&w=400&q=80", VideoUrl = videoSintel },
                    new Episode { Id = 103, SeriesId = 23, Season = 1, Number = 3, Title = "Episódio 3: O Mercado Negro de Almas", Duration = "55 min", Description = "Infiltrado nos esgotos digitais de Neo-Tokyo, Marcos precisa negociar com um traficante de identidades.", ThumbnailUrl = "https://images.unsplash.com/photo-1542751371-adc38448a05e?auto=format&fit=crop&w=400&q=80", VideoUrl = videoBlazes },
                    new Episode { Id = 104, SeriesId = 23, Season = 1, Number = 4, Title = "Episódio 4: Ressonância Neural", Duration = "58 min", Description = "A batalha final na torre dos servidores centrais testa os limites entre homem e máquina.", ThumbnailUrl = "https://images.unsplash.com/photo-1518770660439-4636190af475?auto=format&fit=crop&w=400&q=80", VideoUrl = videoTears }
                }
            },
            new Movie
            {
                Id = 24,
                Title = "Dinastia dos Tronos de Ferro",
                Description = "Alianças traiçoeiras, magia esquecida e guerras sangrentas entre as sete grandes casas nobres pelo controle do Trono Imperial de Valyria.",
                Type = "series",
                BackdropUrl = "https://images.unsplash.com/photo-1518709268805-4e9042af9f23?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1514539079130-25950c84af65?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoSintel,
                Year = 2025,
                Rating = "18+",
                Score = 9.7,
                MatchScore = 98,
                Duration = "4 Temporadas",
                Genres = new() { "Fantasia", "Drama", "Aventura" },
                Cast = new() { "Kit Harington", "Emilia Clarke", "Peter Dinklage" },
                Director = "Miguel Sapochnik",
                IsFeatured = false,
                Likes = 9450,
                Episodes = new()
                {
                    new Episode { Id = 201, SeriesId = 24, Season = 1, Number = 1, Title = "Episódio 1: O Inverno que Desperta", Duration = "61 min", Description = "O Lorde do Norte é convocado à capital imperial após a morte suspeita da Mão do Rei.", ThumbnailUrl = "https://images.unsplash.com/photo-1518709268805-4e9042af9f23?auto=format&fit=crop&w=400&q=80", VideoUrl = videoSintel },
                    new Episode { Id = 202, SeriesId = 24, Season = 1, Number = 2, Title = "Episódio 2: A Estrada dos Lobos", Duration = "56 min", Description = "Um ataque surpresa na travessia do rio muda o rumo da comitiva real.", ThumbnailUrl = "https://images.unsplash.com/photo-1514539079130-25950c84af65?auto=format&fit=crop&w=400&q=80", VideoUrl = videoElephants },
                    new Episode { Id = 203, SeriesId = 24, Season = 1, Number = 3, Title = "Episódio 3: O Banquete Sangrento", Duration = "59 min", Description = "Um casamento nobre torna-se o palco de uma traição milenar.", ThumbnailUrl = "https://images.unsplash.com/photo-1464822759023-fed622ff2c3b?auto=format&fit=crop&w=400&q=80", VideoUrl = videoTears }
                }
            },
            new Movie
            {
                Id = 25,
                Title = "Arquivos Ocultos: Setor 7",
                Description = "Dois agentes federais com visões de mundo opostas investigam avistamentos ufológicos e fenômenos paranormais acobertados pelo governo desde 1947.",
                Type = "series",
                BackdropUrl = "https://images.unsplash.com/photo-1451187580459-43490279c0fa?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1509281373149-e957c6296406?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoEscapes,
                Year = 2026,
                Rating = "16+",
                Score = 9.3,
                MatchScore = 96,
                Duration = "3 Temporadas",
                Genres = new() { "Suspense", "Ficção Científica", "Mistério" },
                Cast = new() { "David Duchovny", "Gillian Anderson", "Mitch Pileggi" },
                Director = "Chris Carter",
                IsFeatured = false,
                Likes = 6320,
                Episodes = new()
                {
                    new Episode { Id = 301, SeriesId = 25, Season = 1, Number = 1, Title = "Episódio 1: Luzes no Deserto de Nevada", Duration = "47 min", Description = "Relatos de naves triangulares atraem a dupla para uma base militar secreta.", ThumbnailUrl = "https://images.unsplash.com/photo-1451187580459-43490279c0fa?auto=format&fit=crop&w=400&q=80", VideoUrl = videoEscapes },
                    new Episode { Id = 302, SeriesId = 25, Season = 1, Number = 2, Title = "Episódio 2: O Homem Sem Sombra", Duration = "51 min", Description = "Um misterioso informante deixa pistas criptografadas em cabines telefônicas.", ThumbnailUrl = "https://images.unsplash.com/photo-1509281373149-e957c6296406?auto=format&fit=crop&w=400&q=80", VideoUrl = videoBlazes }
                }
            },
            new Movie
            {
                Id = 26,
                Title = "Cozinha Brutal: No Limite",
                Description = "Um jovem e talentoso chef de alta gastronomia retorna a Chicago para comandar a lanchonete falida da família, enfrentando o caos diário e traumas do passado.",
                Type = "series",
                BackdropUrl = "https://images.unsplash.com/photo-1555396273-367ea4eb4db5?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1514933651103-005eec06c04b?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoFun,
                Year = 2025,
                Rating = "16+",
                Score = 9.5,
                MatchScore = 97,
                Duration = "3 Temporadas",
                Genres = new() { "Drama", "Comédia", "Gastronomia" },
                Cast = new() { "Jeremy Allen White", "Ayo Edebiri", "Ebon Moss-Bachrach" },
                Director = "Christopher Storer",
                IsFeatured = false,
                Likes = 7120,
                Episodes = new()
                {
                    new Episode { Id = 401, SeriesId = 26, Season = 1, Number = 1, Title = "Episódio 1: O Sistema", Duration = "32 min", Description = "Carmim tenta impor a hierarquia da brigada francesa em uma equipe resistente à mudança.", ThumbnailUrl = "https://images.unsplash.com/photo-1555396273-367ea4eb4db5?auto=format&fit=crop&w=400&q=80", VideoUrl = videoFun },
                    new Episode { Id = 402, SeriesId = 26, Season = 1, Number = 2, Title = "Episódio 2: A Revista Sanitária", Duration = "30 min", Description = "Uma inspeção surpresa testa a paciência e a lealdade de todos na cozinha.", ThumbnailUrl = "https://images.unsplash.com/photo-1514933651103-005eec06c04b?auto=format&fit=crop&w=400&q=80", VideoUrl = videoJoyrides }
                }
            },
            new Movie
            {
                Id = 27,
                Title = "Lendas do Asfalto: Drift Noturno",
                Description = "Nas rodovias montanhosas do Japão, pilotos amadores competem em corridas clandestinas ilegais onde a honra e o domínio mecânico valem mais que a vida.",
                Type = "series",
                BackdropUrl = "https://images.unsplash.com/photo-1503376780353-7e6692767b70?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1568605117036-5fe5e7bab0b7?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoJoyrides,
                Year = 2025,
                Rating = "14+",
                Score = 9.1,
                MatchScore = 93,
                Duration = "2 Temporadas",
                Genres = new() { "Ação", "Automobilismo", "Adrenalina" },
                Cast = new() { "Mackenyu", "Anna Sawai", "Sung Kang" },
                Director = "Rob Cohen",
                IsFeatured = false,
                Likes = 5230,
                Episodes = new()
                {
                    new Episode { Id = 501, SeriesId = 27, Season = 1, Number = 1, Title = "Episódio 1: Descida em Akina", Duration = "45 min", Description = "Um humilde entregador de tofu humilha o piloto número um da região em uma curva fechada.", ThumbnailUrl = "https://images.unsplash.com/photo-1503376780353-7e6692767b70?auto=format&fit=crop&w=400&q=80", VideoUrl = videoJoyrides },
                    new Episode { Id = 502, SeriesId = 27, Season = 1, Number = 2, Title = "Episódio 2: O Motor do Trovão", Duration = "43 min", Description = "A equipe adversária exige uma revanche de alta octanagem na chuva.", ThumbnailUrl = "https://images.unsplash.com/photo-1568605117036-5fe5e7bab0b7?auto=format&fit=crop&w=400&q=80", VideoUrl = videoBullrun }
                }
            },
            new Movie
            {
                Id = 28,
                Title = "Círculo de Espiões: Guerra Fria 2.0",
                Description = "A intrincada teia de mentiras e desinformação entre agentes do MI6 e agentes duplos em Berlim, numa corrida silenciosa contra o apocalipse nuclear.",
                Type = "series",
                BackdropUrl = "https://images.unsplash.com/photo-1486406146926-c627a92ad1ab?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1536440136628-849c177e76a1?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoBlazes,
                Year = 2024,
                Rating = "16+",
                Score = 9.2,
                MatchScore = 94,
                Duration = "3 Temporadas",
                Genres = new() { "Suspense", "Policial", "Histórico" },
                Cast = new() { "Gary Oldman", "Colin Firth", "Tom Hardy" },
                Director = "Tomas Alfredson",
                IsFeatured = false,
                Likes = 4780,
                Episodes = new()
                {
                    new Episode { Id = 601, SeriesId = 28, Season = 1, Number = 1, Title = "Episódio 1: Ponto Cego", Duration = "54 min", Description = "Um diplomata britânico é assassinado em Budapeste, levantando suspeitas de um espião infiltrado.", ThumbnailUrl = "https://images.unsplash.com/photo-1486406146926-c627a92ad1ab?auto=format&fit=crop&w=400&q=80", VideoUrl = videoBlazes },
                    new Episode { Id = 602, SeriesId = 28, Season = 1, Number = 2, Title = "Episódio 2: A Fita de Viena", Duration = "50 min", Description = "Uma gravação analógica encontrada em um cofre revela a identidade de um traidor.", ThumbnailUrl = "https://images.unsplash.com/photo-1536440136628-849c177e76a1?auto=format&fit=crop&w=400&q=80", VideoUrl = videoEscapes }
                }
            },
            new Movie
            {
                Id = 29,
                Title = "Império Subterrâneo: Cartéis",
                Description = "A ascensão impiedosa e os conflitos sangrentos entre famílias rivais pelo domínio das rotas clandestinas na fronteira continental.",
                Type = "series",
                BackdropUrl = "https://images.unsplash.com/photo-1519501025264-65ba15a82390?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1508873696983-2df5293cb32b?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoMeltdowns,
                Year = 2025,
                Rating = "18+",
                Score = 9.4,
                MatchScore = 95,
                Duration = "4 Temporadas",
                Genres = new() { "Policial", "Drama", "Ação" },
                Cast = new() { "Wagner Moura", "Diego Luna", "Pedro Pascal" },
                Director = "José Padilha",
                IsFeatured = false,
                Likes = 6890,
                Episodes = new()
                {
                    new Episode { Id = 701, SeriesId = 29, Season = 1, Number = 1, Title = "Episódio 1: A Primeira Fronteira", Duration = "57 min", Description = "Um pequeno contrabandista constrói o primeiro túnel transfronteiriço com tecnologia de mineração.", ThumbnailUrl = "https://images.unsplash.com/photo-1519501025264-65ba15a82390?auto=format&fit=crop&w=400&q=80", VideoUrl = videoMeltdowns },
                    new Episode { Id = 702, SeriesId = 29, Season = 1, Number = 2, Title = "Episódio 2: Pacto de Sangue", Duration = "52 min", Description = "A polícia federal fecha o cerco ao armazém central, forçando uma decisão drástica.", ThumbnailUrl = "https://images.unsplash.com/photo-1508873696983-2df5293cb32b?auto=format&fit=crop&w=400&q=80", VideoUrl = videoBlazes }
                }
            },
            new Movie
            {
                Id = 30,
                Title = "Planeta Selvagem: Santuários",
                Description = "Uma série documental premiada filmada com câmeras 8K de última geração, capturando os comportamentos mais raros e emocionantes da vida selvagem nos cinco continentes.",
                Type = "series",
                BackdropUrl = "https://images.unsplash.com/photo-1511447333015-45b65e60f6d5?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1544551763-46a013bb70d5?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoBunny,
                Year = 2025,
                Rating = "Livre",
                Score = 9.8,
                MatchScore = 99,
                Duration = "2 Temporadas",
                Genres = new() { "Documentário", "Natureza", "Família" },
                Cast = new() { "Sir David Attenborough (Narração)" },
                Director = "Alastair Fothergill",
                IsFeatured = false,
                Likes = 8120,
                Episodes = new()
                {
                    new Episode { Id = 801, SeriesId = 30, Season = 1, Number = 1, Title = "Episódio 1: Os Oceanos Azuis", Duration = "50 min", Description = "Mergulho profundo nos recifes de corais e o balé das baleias jubarte no Pacífico Sul.", ThumbnailUrl = "https://images.unsplash.com/photo-1544551763-46a013bb70d5?auto=format&fit=crop&w=400&q=80", VideoUrl = videoBunny },
                    new Episode { Id = 802, SeriesId = 30, Season = 1, Number = 2, Title = "Episódio 2: A Selva Impenetrável", Duration = "49 min", Description = "A sobrevivência dos felinos nas densas florestas tropicais da Amazônia e Bornéu.", ThumbnailUrl = "https://images.unsplash.com/photo-1511447333015-45b65e60f6d5?auto=format&fit=crop&w=400&q=80", VideoUrl = videoElephants }
                }
            },
            new Movie
            {
                Id = 31,
                Title = "A Academia de Heróis Esquecidos",
                Description = "Jovens desajustados com poderes singulares são reunidos em uma mansão vitoriana secreta por um milionário excêntrico para impedir o colapso temporal.",
                Type = "series",
                BackdropUrl = "https://images.unsplash.com/photo-1516450360452-9312f5e86fc7?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1534447677768-be436bb09401?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoSintel,
                Year = 2026,
                Rating = "14+",
                Score = 9.2,
                MatchScore = 95,
                Duration = "3 Temporadas",
                Genres = new() { "Ação", "Comédia", "Fantasia" },
                Cast = new() { "Elliot Page", "Tom Hopper", "Robert Sheehan" },
                Director = "Steve Blackman",
                IsFeatured = false,
                Likes = 5980,
                Episodes = new()
                {
                    new Episode { Id = 901, SeriesId = 31, Season = 1, Number = 1, Title = "Episódio 1: O Reencontro no Funeral", Duration = "56 min", Description = "Após a morte do patriarca, os irmãos afastados voltam à mansão e descobrem um aviso do futuro.", ThumbnailUrl = "https://images.unsplash.com/photo-1516450360452-9312f5e86fc7?auto=format&fit=crop&w=400&q=80", VideoUrl = videoSintel },
                    new Episode { Id = 902, SeriesId = 31, Season = 1, Number = 2, Title = "Episódio 2: O Salto Quântico Falho", Duration = "53 min", Description = "O Número Cinco relata sua sobrevivência em um mundo pós-apocalíptico desolado.", ThumbnailUrl = "https://images.unsplash.com/photo-1534447677768-be436bb09401?auto=format&fit=crop&w=400&q=80", VideoUrl = videoElephants }
                }
            },
            new Movie
            {
                Id = 32,
                Title = "Vale do Silício: Segredo Código",
                Description = "A jornada hilária e vertiginosa de seis programadores excêntricos que criam um algoritmo revolucionário de compressão de dados e desafiam os titãs da tecnologia.",
                Type = "series",
                BackdropUrl = "https://images.unsplash.com/photo-1526374965328-7f61d4dc18c5?auto=format&fit=crop&w=1920&q=80",
                PosterUrl = "https://images.unsplash.com/photo-1518770660439-4636190af475?auto=format&fit=crop&w=600&q=80",
                VideoUrl = videoFun,
                Year = 2025,
                Rating = "16+",
                Score = 9.3,
                MatchScore = 96,
                Duration = "5 Temporadas",
                Genres = new() { "Comédia", "Tecnologia", "Drama" },
                Cast = new() { "Thomas Middleditch", "Martin Starr", "Kumail Nanjiani" },
                Director = "Mike Judge",
                IsFeatured = false,
                Likes = 6740,
                Episodes = new()
                {
                    new Episode { Id = 1001, SeriesId = 32, Season = 1, Number = 1, Title = "Episódio 1: A Proposta Mínima Viável", Duration = "30 min", Description = "Richard cria um algoritmo revolucionário e recebe ofertas milionárias de fundos rivais.", ThumbnailUrl = "https://images.unsplash.com/photo-1526374965328-7f61d4dc18c5?auto=format&fit=crop&w=400&q=80", VideoUrl = videoFun },
                    new Episode { Id = 1002, SeriesId = 32, Season = 1, Number = 2, Title = "Episódio 2: O Quadro Branco do Caos", Duration = "29 min", Description = "A equipe tenta definir a estrutura societária da nova startup antes do Demo Day.", ThumbnailUrl = "https://images.unsplash.com/photo-1518770660439-4636190af475?auto=format&fit=crop&w=400&q=80", VideoUrl = videoBullrun }
                }
            }
        };

        _movies.AddRange(movies);
        _movies.AddRange(seriesList);

        _watchlist.Add(new WatchlistItem { UserId = demoUser.Id, MovieId = 1 });
        _watchlist.Add(new WatchlistItem { UserId = demoUser.Id, MovieId = 23 });
        _watchlist.Add(new WatchlistItem { UserId = demoUser.Id, MovieId = 6 });

        _progress.Add(new WatchProgress
        {
            UserId = demoUser.Id,
            MovieId = 1,
            PositionSeconds = 2450,
            DurationSeconds = 8280,
            LastWatchedAt = DateTime.UtcNow.AddHours(-2)
        });

        _progress.Add(new WatchProgress
        {
            UserId = demoUser.Id,
            MovieId = 23,
            EpisodeId = 101,
            PositionSeconds = 1120,
            DurationSeconds = 3120,
            LastWatchedAt = DateTime.UtcNow.AddDays(-1)
        });

        _ratings.Add(new UserRatingRecord { UserId = demoUser.Id, MovieId = 1, Score = 5 });
        _ratings.Add(new UserRatingRecord { UserId = demoUser.Id, MovieId = 2, Score = 5 });
        _ratings.Add(new UserRatingRecord { UserId = demoUser.Id, MovieId = 23, Score = 4 });
    }

    #endregion

    #region Authentication, Session Security & Profile

    public AuthResponse Register(RegisterRequest req)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password) || string.IsNullOrWhiteSpace(req.Name))
            {
                return new AuthResponse(false, "Todos os campos obrigatórios devem ser preenchidos.", null, null);
            }

            var cleanEmail = req.Email.Trim().ToLowerInvariant();
            if (!IsValidEmail(cleanEmail))
            {
                return new AuthResponse(false, "Formato de e-mail inválido.", null, null);
            }

            if (req.Password.Length < 6 || req.Password.Length > 100)
            {
                return new AuthResponse(false, "A senha deve ter entre 6 e 100 caracteres.", null, null);
            }

            if (_users.Any(u => u.Email.Equals(cleanEmail, StringComparison.OrdinalIgnoreCase)))
            {
                return new AuthResponse(false, "Já existe uma conta cadastrada com este e-mail.", null, null);
            }

            var cleanName = SanitizeInput(req.Name, 60);
            var cleanAvatar = !string.IsNullOrWhiteSpace(req.Avatar) && req.Avatar.Length <= 500
                ? req.Avatar.Trim()
                : "https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=200&q=80";

            var user = new User
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = cleanName,
                Email = cleanEmail,
                PasswordHash = HashPassword(req.Password),
                Avatar = cleanAvatar,
                CreatedAt = DateTime.UtcNow,
                Preferences = new UserPreferences()
            };

            _users.Add(user);
            var session = CreateSession(user.Id);
            SaveData();

            return new AuthResponse(true, "Conta criada com sucesso!", session.Token, SanitizeUser(user));
        }
    }

    public AuthResponse Login(LoginRequest req)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            {
                return new AuthResponse(false, "Informe e-mail e senha.", null, null);
            }

            var cleanEmail = req.Email.Trim().ToLowerInvariant();
            var user = _users.FirstOrDefault(u => u.Email.Equals(cleanEmail, StringComparison.OrdinalIgnoreCase));

            if (user == null || !VerifyPassword(req.Password, user.PasswordHash))
            {
                // Mensagem genérica para mitigar enumeração de contas
                return new AuthResponse(false, "E-mail ou senha incorretos.", null, null);
            }

            // Atualiza para formato seguro de hash caso ainda esteja no formato antigo
            if (!user.PasswordHash.Contains(':'))
            {
                user.PasswordHash = HashPassword(req.Password);
            }

            var session = CreateSession(user.Id);
            SaveData();

            return new AuthResponse(true, "Login realizado com sucesso!", session.Token, SanitizeUser(user));
        }
    }

    public User? GetUserBySessionToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        lock (_lock)
        {
            var session = _sessions.FirstOrDefault(s => s.Token == token);
            if (session == null) return null;

            if (session.ExpiresAt <= DateTime.UtcNow)
            {
                _sessions.Remove(session);
                SaveData();
                return null;
            }

            var user = _users.FirstOrDefault(u => u.Id == session.UserId);
            return user != null ? SanitizeUser(user) : null;
        }
    }

    public AuthResponse UpdateProfile(string userId, UpdateProfileRequest req)
    {
        lock (_lock)
        {
            var user = _users.FirstOrDefault(u => u.Id == userId);
            if (user == null)
            {
                return new AuthResponse(false, "Usuário não encontrado.", null, null);
            }

            if (!string.IsNullOrWhiteSpace(req.Name))
            {
                user.Name = SanitizeInput(req.Name, 60);
            }

            if (!string.IsNullOrWhiteSpace(req.Avatar) && req.Avatar.Length <= 500)
            {
                user.Avatar = req.Avatar.Trim();
            }

            if (req.Preferences != null)
            {
                user.Preferences = req.Preferences;
            }

            SaveData();
            return new AuthResponse(true, "Perfil atualizado com sucesso!", null, SanitizeUser(user));
        }
    }

    public AuthResponse ChangePassword(string userId, ChangePasswordRequest req)
    {
        lock (_lock)
        {
            var user = _users.FirstOrDefault(u => u.Id == userId);
            if (user == null)
            {
                return new AuthResponse(false, "Usuário não encontrado.", null, null);
            }

            if (!VerifyPassword(req.CurrentPassword, user.PasswordHash))
            {
                return new AuthResponse(false, "A senha atual está incorreta.", null, null);
            }

            if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6 || req.NewPassword.Length > 100)
            {
                return new AuthResponse(false, "A nova senha deve ter entre 6 e 100 caracteres.", null, null);
            }

            user.PasswordHash = HashPassword(req.NewPassword);
            
            // Invalida outras sessões ao alterar senha
            _sessions.RemoveAll(s => s.UserId == userId);
            SaveData();

            return new AuthResponse(true, "Senha alterada com sucesso! Faça login novamente com a nova senha.", null, SanitizeUser(user));
        }
    }

    public AuthResponse RecoverPassword(RecoverPasswordRequest req)
    {
        lock (_lock)
        {
            var cleanEmail = req.Email?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(cleanEmail) || !IsValidEmail(cleanEmail))
            {
                return new AuthResponse(true, "Se o e-mail estiver cadastrado, as instruções foram enviadas.", null, null);
            }

            var user = _users.FirstOrDefault(u => u.Email.Equals(cleanEmail, StringComparison.OrdinalIgnoreCase));
            if (user != null)
            {
                var resetToken = GenerateSecureToken();
                _resetTokens.RemoveAll(r => r.Email.Equals(cleanEmail, StringComparison.OrdinalIgnoreCase) || r.ExpiresAt <= DateTime.UtcNow);
                _resetTokens.Add(new PasswordResetToken
                {
                    Email = cleanEmail,
                    Token = resetToken,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(15)
                });
            }

            // NUNCA retorna senha na resposta HTTP!
            return new AuthResponse(true, "Se o e-mail estiver cadastrado em nossa base, as instruções para redefinição foram enviadas com sucesso.", null, null);
        }
    }

    public AuthResponse ResetPassword(ResetPasswordRequest req)
    {
        lock (_lock)
        {
            var cleanEmail = req.Email?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(cleanEmail) || string.IsNullOrWhiteSpace(req.Token) || string.IsNullOrWhiteSpace(req.NewPassword))
            {
                return new AuthResponse(false, "Dados insuficientes para redefinição.", null, null);
            }

            var tokenRecord = _resetTokens.FirstOrDefault(t => 
                t.Email.Equals(cleanEmail, StringComparison.OrdinalIgnoreCase) && 
                t.Token == req.Token && 
                t.ExpiresAt > DateTime.UtcNow);

            if (tokenRecord == null)
            {
                return new AuthResponse(false, "Token de redefinição inválido ou expirado.", null, null);
            }

            var user = _users.FirstOrDefault(u => u.Email.Equals(cleanEmail, StringComparison.OrdinalIgnoreCase));
            if (user == null)
            {
                return new AuthResponse(false, "Usuário não encontrado.", null, null);
            }

            if (req.NewPassword.Length < 6 || req.NewPassword.Length > 100)
            {
                return new AuthResponse(false, "A nova senha deve ter entre 6 e 100 caracteres.", null, null);
            }

            user.PasswordHash = HashPassword(req.NewPassword);
            _resetTokens.Remove(tokenRecord);
            _sessions.RemoveAll(s => s.UserId == user.Id);
            SaveData();

            return new AuthResponse(true, "Senha redefinida com sucesso!", null, SanitizeUser(user));
        }
    }

    public void Logout(string token)
    {
        lock (_lock)
        {
            _sessions.RemoveAll(s => s.Token == token);
            SaveData();
        }
    }

    private UserSession CreateSession(string userId)
    {
        var session = new UserSession
        {
            Token = "cs_" + GenerateSecureToken(),
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        _sessions.Add(session);
        return session;
    }

    private User SanitizeUser(User user)
    {
        return new User
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            PasswordHash = "",
            Avatar = user.Avatar,
            CreatedAt = user.CreatedAt,
            Preferences = user.Preferences,
            IsAdmin = _adminEmails.Contains(user.Email)
        };
    }

    #endregion

    #region Catalog & Movie Queries

    private Movie DecorateMovie(Movie movie, string? userId)
    {
        var copy = new Movie
        {
            Id = movie.Id,
            Title = movie.Title,
            Description = movie.Description,
            Type = movie.Type,
            BackdropUrl = movie.BackdropUrl,
            PosterUrl = movie.PosterUrl,
            VideoUrl = movie.VideoUrl,
            Year = movie.Year,
            Rating = movie.Rating,
            Score = movie.Score,
            MatchScore = movie.MatchScore,
            Duration = movie.Duration,
            Genres = new(movie.Genres),
            Cast = new(movie.Cast),
            Director = movie.Director,
            IsFeatured = movie.IsFeatured,
            Likes = movie.Likes,
            Episodes = movie.Episodes.Select(e => new Episode
            {
                Id = e.Id,
                SeriesId = e.SeriesId,
                Season = e.Season,
                Number = e.Number,
                Title = e.Title,
                Description = e.Description,
                Duration = e.Duration,
                ThumbnailUrl = e.ThumbnailUrl,
                VideoUrl = e.VideoUrl
            }).ToList()
        };

        if (!string.IsNullOrWhiteSpace(userId))
        {
            copy.IsInWatchlist = _watchlist.Any(w => w.UserId == userId && w.MovieId == movie.Id);
            
            var userRating = _ratings.FirstOrDefault(r => r.UserId == userId && r.MovieId == movie.Id);
            copy.UserRating = userRating?.Score;

            var progress = _progress.FirstOrDefault(p => p.UserId == userId && p.MovieId == movie.Id);
            if (progress != null)
            {
                copy.WatchProgressSeconds = progress.PositionSeconds;
                copy.WatchDurationSeconds = progress.DurationSeconds;
                copy.WatchedPercent = progress.WatchedPercent;
                copy.LastWatchedEpisodeId = progress.EpisodeId;
            }
        }

        return copy;
    }

    public List<Movie> GetAllMovies(string? type = null, string? genre = null, int? year = null, double? minScore = null, string? rating = null, string? sortBy = null, string? userId = null)
    {
        lock (_lock)
        {
            var query = _movies.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(type) && !type.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(m => m.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(genre) && !genre.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(m => m.Genres.Any(g => g.Equals(genre, StringComparison.OrdinalIgnoreCase)));
            }

            if (year.HasValue)
            {
                query = query.Where(m => m.Year == year.Value);
            }

            if (minScore.HasValue)
            {
                query = query.Where(m => m.Score >= minScore.Value);
            }

            if (!string.IsNullOrWhiteSpace(rating) && !rating.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(m => m.Rating.Equals(rating, StringComparison.OrdinalIgnoreCase));
            }

            query = (sortBy?.ToLowerInvariant()) switch
            {
                "recent" or "novidades" => query.OrderByDescending(m => m.Year).ThenByDescending(m => m.Id),
                "top_rated" or "melhores" => query.OrderByDescending(m => m.Score),
                "popular" or "populares" => query.OrderByDescending(m => m.Likes),
                _ => query.OrderByDescending(m => m.MatchScore)
            };

            return query.Select(m => DecorateMovie(m, userId)).ToList();
        }
    }

    public Movie? GetMovieById(int id, string? userId = null)
    {
        lock (_lock)
        {
            var movie = _movies.FirstOrDefault(m => m.Id == id);
            return movie == null ? null : DecorateMovie(movie, userId);
        }
    }

    public Movie? GetFeaturedMovie(string? userId = null)
    {
        lock (_lock)
        {
            var featured = _movies.FirstOrDefault(m => m.IsFeatured) ?? _movies.FirstOrDefault();
            return featured == null ? null : DecorateMovie(featured, userId);
        }
    }

    public List<Category> GetCategorizedMovies(string? userId = null)
    {
        lock (_lock)
        {
            var categories = new List<Category>();

            if (!string.IsNullOrWhiteSpace(userId))
            {
                var continueWatching = GetContinueWatching(userId);
                if (continueWatching.Any())
                {
                    categories.Add(new Category
                    {
                        Id = "continue_watching",
                        Title = "Continuar Assistindo",
                        Movies = continueWatching
                    });
                }

                var watchlistMovies = GetWatchlist(userId);
                if (watchlistMovies.Any())
                {
                    categories.Add(new Category
                    {
                        Id = "watchlist",
                        Title = "Minha Lista",
                        Movies = watchlistMovies
                    });
                }
            }

            categories.Add(new Category
            {
                Id = "trending",
                Title = "Em Alta na CineStream",
                Movies = _movies.OrderByDescending(m => m.MatchScore).Take(10).Select(m => DecorateMovie(m, userId)).ToList()
            });

            categories.Add(new Category
            {
                Id = "releases",
                Title = "Lançamentos e Novidades",
                Movies = _movies.OrderByDescending(m => m.Year).ThenByDescending(m => m.Id).Take(10).Select(m => DecorateMovie(m, userId)).ToList()
            });

            categories.Add(new Category
            {
                Id = "popular",
                Title = "Mais Populares e Aclamados",
                Movies = _movies.OrderByDescending(m => m.Likes).Take(10).Select(m => DecorateMovie(m, userId)).ToList()
            });

            categories.Add(new Category
            {
                Id = "action",
                Title = "Filmes de Ação e Adrenalina",
                Movies = _movies.Where(m => m.Type == "movie" && m.Genres.Contains("Ação")).Take(10).Select(m => DecorateMovie(m, userId)).ToList()
            });

            categories.Add(new Category
            {
                Id = "series",
                Title = "Séries Que Você Precisa Maratonar",
                Movies = _movies.Where(m => m.Type == "series").OrderByDescending(m => m.Score).Take(10).Select(m => DecorateMovie(m, userId)).ToList()
            });

            categories.Add(new Category
            {
                Id = "scifi",
                Title = "Ficção Científica e Futuros Distópicos",
                Movies = _movies.Where(m => m.Genres.Contains("Ficção Científica") || m.Genres.Contains("Cyberpunk")).Take(10).Select(m => DecorateMovie(m, userId)).ToList()
            });

            categories.Add(new Category
            {
                Id = "thriller",
                Title = "Suspense e Mistérios Eletrizantes",
                Movies = _movies.Where(m => m.Genres.Contains("Suspense") || m.Genres.Contains("Mistério")).Take(10).Select(m => DecorateMovie(m, userId)).ToList()
            });

            categories.Add(new Category
            {
                Id = "comedy",
                Title = "Comédias para Maratonar Rindo",
                Movies = _movies.Where(m => m.Genres.Contains("Comédia")).Take(10).Select(m => DecorateMovie(m, userId)).ToList()
            });

            categories.Add(new Category
            {
                Id = "docs",
                Title = "Documentários e Obras Visuais",
                Movies = _movies.Where(m => m.Genres.Contains("Documentário") || m.Genres.Contains("Natureza")).Take(10).Select(m => DecorateMovie(m, userId)).ToList()
            });

            return categories;
        }
    }

    public List<Movie> GetContinueWatching(string userId)
    {
        lock (_lock)
        {
            var userProgressList = _progress
                .Where(p => p.UserId == userId && p.PositionSeconds > 5 && p.WatchedPercent < 98)
                .OrderByDescending(p => p.LastWatchedAt)
                .Take(10)
                .ToList();

            var movies = new List<Movie>();
            foreach (var prog in userProgressList)
            {
                var movie = _movies.FirstOrDefault(m => m.Id == prog.MovieId);
                if (movie != null)
                {
                    movies.Add(DecorateMovie(movie, userId));
                }
            }
            return movies;
        }
    }

    public List<Movie> GetRecommendations(string userId)
    {
        lock (_lock)
        {
            var userFavMovieIds = _watchlist.Where(w => w.UserId == userId).Select(w => w.MovieId).ToHashSet();
            var favoriteGenres = _movies
                .Where(m => userFavMovieIds.Contains(m.Id))
                .SelectMany(m => m.Genres)
                .GroupBy(g => g)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(2)
                .ToList();

            if (!favoriteGenres.Any())
            {
                favoriteGenres.Add("Ficção Científica");
                favoriteGenres.Add("Ação");
            }

            return _movies
                .Where(m => !userFavMovieIds.Contains(m.Id) && m.Genres.Any(g => favoriteGenres.Contains(g)))
                .OrderByDescending(m => m.Score)
                .Take(8)
                .Select(m => DecorateMovie(m, userId))
                .ToList();
        }
    }

    public List<Movie> Search(string query, string? userId = null)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new();
            }

            var cleanQuery = query.Trim().ToLowerInvariant();

            return _movies
                .Where(m =>
                    m.Title.ToLowerInvariant().Contains(cleanQuery) ||
                    m.Genres.Any(g => g.ToLowerInvariant().Contains(cleanQuery)) ||
                    m.Cast.Any(c => c.ToLowerInvariant().Contains(cleanQuery)) ||
                    m.Director.ToLowerInvariant().Contains(cleanQuery) ||
                    m.Description.ToLowerInvariant().Contains(cleanQuery))
                .OrderByDescending(m => m.Title.ToLowerInvariant().StartsWith(cleanQuery) ? 10 : 0)
                .ThenByDescending(m => m.MatchScore)
                .Select(m => DecorateMovie(m, userId))
                .ToList();
        }
    }

    public List<Movie> GetWatchlist(string userId)
    {
        lock (_lock)
        {
            var userItems = _watchlist
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.AddedAt)
                .Select(w => w.MovieId)
                .ToList();

            return _movies
                .Where(m => userItems.Contains(m.Id))
                .Select(m => DecorateMovie(m, userId))
                .ToList();
        }
    }

    public bool ToggleWatchlist(string userId, int movieId)
    {
        lock (_lock)
        {
            if (!_movies.Any(m => m.Id == movieId)) return false;

            var existing = _watchlist.FirstOrDefault(w => w.UserId == userId && w.MovieId == movieId);
            if (existing != null)
            {
                _watchlist.Remove(existing);
                SaveData();
                return false;
            }
            else
            {
                _watchlist.Add(new WatchlistItem
                {
                    UserId = userId,
                    MovieId = movieId,
                    AddedAt = DateTime.UtcNow
                });
                SaveData();
                return true;
            }
        }
    }

    public WatchProgress? SaveProgress(string userId, int movieId, int? episodeId, double positionSeconds, double durationSeconds)
    {
        lock (_lock)
        {
            if (!_movies.Any(m => m.Id == movieId)) return null;

            var existing = _progress.FirstOrDefault(p => p.UserId == userId && p.MovieId == movieId);
            if (existing == null)
            {
                existing = new WatchProgress
                {
                    UserId = userId,
                    MovieId = movieId,
                    EpisodeId = episodeId,
                    PositionSeconds = Math.Max(0, positionSeconds),
                    DurationSeconds = Math.Max(0, durationSeconds),
                    LastWatchedAt = DateTime.UtcNow
                };
                _progress.Add(existing);
            }
            else
            {
                existing.EpisodeId = episodeId;
                existing.PositionSeconds = Math.Max(0, positionSeconds);
                existing.DurationSeconds = Math.Max(0, durationSeconds);
                existing.LastWatchedAt = DateTime.UtcNow;
            }

            SaveData();
            return existing;
        }
    }

    public int SetRating(string userId, int movieId, int score)
    {
        lock (_lock)
        {
            if (!_movies.Any(m => m.Id == movieId)) return 0;

            score = Math.Clamp(score, 1, 5);
            var existing = _ratings.FirstOrDefault(r => r.UserId == userId && r.MovieId == movieId);
            if (existing == null)
            {
                _ratings.Add(new UserRatingRecord
                {
                    UserId = userId,
                    MovieId = movieId,
                    Score = score,
                    RatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.Score = score;
                existing.RatedAt = DateTime.UtcNow;
            }

            SaveData();
            return score;
        }
    }

    public List<Movie> GetAdminCatalog()
    {
        lock (_lock)
        {
            return _movies.Select(movie => DecorateMovie(movie, null)).ToList();
        }
    }

    public Movie? SaveCatalogMovie(int? id, CatalogMovieRequest request)
    {
        lock (_lock)
        {
            var existing = id.HasValue ? _movies.FirstOrDefault(movie => movie.Id == id.Value) : null;
            if (id.HasValue && existing == null)
            {
                return null;
            }

            var movieId = existing?.Id ?? (_movies.Count == 0 ? 1 : _movies.Max(movie => movie.Id) + 1);
            var previousEpisodes = existing?.Episodes ?? new List<Episode>();
            var nextEpisodeId = _movies.SelectMany(movie => movie.Episodes)
                .Select(episode => episode.Id)
                .DefaultIfEmpty(0)
                .Max();

            var episodes = request.Episodes.Select(episode =>
            {
                var previousEpisode = previousEpisodes.FirstOrDefault(item =>
                    item.Season == episode.Season && item.Number == episode.Number);
                return new Episode
                {
                    Id = previousEpisode?.Id ?? ++nextEpisodeId,
                    SeriesId = movieId,
                    Season = episode.Season,
                    Number = episode.Number,
                    Title = episode.Title.Trim(),
                    Description = episode.Description?.Trim() ?? string.Empty,
                    Duration = episode.Duration?.Trim() ?? string.Empty,
                    ThumbnailUrl = episode.ThumbnailUrl?.Trim() ?? string.Empty,
                    VideoUrl = episode.VideoUrl?.Trim() ?? string.Empty
                };
            }).ToList();

            var movie = new Movie
            {
                Id = movieId,
                Title = request.Title.Trim(),
                Description = request.Description.Trim(),
                Type = request.Type,
                BackdropUrl = request.BackdropUrl.Trim(),
                PosterUrl = request.PosterUrl.Trim(),
                VideoUrl = request.VideoUrl.Trim(),
                Year = request.Year,
                Rating = request.Rating,
                Score = request.Score,
                MatchScore = request.MatchScore,
                Duration = request.Duration.Trim(),
                Genres = request.Genres.Where(genre => !string.IsNullOrWhiteSpace(genre)).Select(genre => genre.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Cast = request.Cast.Where(person => !string.IsNullOrWhiteSpace(person)).Select(person => person.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Director = request.Director?.Trim() ?? string.Empty,
                IsFeatured = request.IsFeatured,
                Likes = existing?.Likes ?? 0,
                Episodes = episodes
            };

            if (movie.IsFeatured)
            {
                foreach (var featuredMovie in _movies)
                {
                    featuredMovie.IsFeatured = false;
                }
            }

            if (existing == null)
            {
                _movies.Add(movie);
            }
            else
            {
                _movies[_movies.IndexOf(existing)] = movie;
            }

            SaveData();
            return DecorateMovie(movie, null);
        }
    }

    public Movie? DeleteCatalogMovie(int id)
    {
        lock (_lock)
        {
            var movie = _movies.FirstOrDefault(item => item.Id == id);
            if (movie == null)
            {
                return null;
            }

            _movies.Remove(movie);
            _watchlist.RemoveAll(item => item.MovieId == id);
            _progress.RemoveAll(item => item.MovieId == id);
            _ratings.RemoveAll(item => item.MovieId == id);

            if (movie.IsFeatured && _movies.Count > 0)
            {
                _movies[0].IsFeatured = true;
            }

            SaveData();
            return DecorateMovie(movie, null);
        }
    }

    public int ToggleLike(int movieId)
    {
        lock (_lock)
        {
            var movie = _movies.FirstOrDefault(m => m.Id == movieId);
            if (movie != null)
            {
                movie.Likes += 1;
                SaveData();
                return movie.Likes;
            }
            return 0;
        }
    }

    #endregion
}
