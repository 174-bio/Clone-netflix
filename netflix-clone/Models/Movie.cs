namespace NetflixClone.Models;

public class UserPreferences
{
    public bool AutoPlayNextEpisode { get; set; } = true;
    public string PreferredQuality { get; set; } = "1080p";
    public string PreferredAudioLanguage { get; set; } = "Português (Brasil)";
    public string PreferredSubtitleLanguage { get; set; } = "Português (Brasil)";
}

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Avatar { get; set; } = "https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?auto=format&fit=crop&w=150&q=80";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public UserPreferences Preferences { get; set; } = new();
}

public class Episode
{
    public int Id { get; set; }
    public int SeriesId { get; set; }
    public int Season { get; set; } = 1;
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
}

public class Movie
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = "movie"; // "movie" or "series"
    public string BackdropUrl { get; set; } = string.Empty;
    public string PosterUrl { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Rating { get; set; } = "16+"; // "Livre", "10+", "12+", "14+", "16+", "18+"
    public double Score { get; set; } = 9.2; // Nota 0-10
    public int MatchScore { get; set; } = 96; // 98% de relevância
    public string Duration { get; set; } = "2h 10m"; // ou "3 Temporadas"
    public List<string> Genres { get; set; } = new();
    public List<string> Cast { get; set; } = new();
    public string Director { get; set; } = string.Empty;
    public bool IsFeatured { get; set; }
    public int Likes { get; set; }
    public List<Episode> Episodes { get; set; } = new();

    // Client/session decorated attributes
    public bool IsInWatchlist { get; set; }
    public int? UserRating { get; set; } // 1 a 5 estrelas
    public double? WatchProgressSeconds { get; set; }
    public double? WatchDurationSeconds { get; set; }
    public int? WatchedPercent { get; set; }
    public int? LastWatchedEpisodeId { get; set; }
}

public class Category
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<Movie> Movies { get; set; } = new();
}

public class WatchlistItem
{
    public string UserId { get; set; } = string.Empty;
    public int MovieId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}

public class WatchProgress
{
    public string UserId { get; set; } = string.Empty;
    public int MovieId { get; set; }
    public int? EpisodeId { get; set; }
    public double PositionSeconds { get; set; }
    public double DurationSeconds { get; set; }
    public DateTime LastWatchedAt { get; set; } = DateTime.UtcNow;

    public int WatchedPercent => DurationSeconds > 0 ? (int)Math.Clamp(Math.Round((PositionSeconds / DurationSeconds) * 100), 0, 100) : 0;
}

public class UserRatingRecord
{
    public string UserId { get; set; } = string.Empty;
    public int MovieId { get; set; }
    public int Score { get; set; } // 1 a 5
    public DateTime RatedAt { get; set; } = DateTime.UtcNow;
}
