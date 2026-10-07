namespace NetflixClone.Models;

public record LoginRequest(string Email, string Password);

public record RegisterRequest(string Name, string Email, string Password, string? Avatar);

public record UpdateProfileRequest(string Name, string? Avatar, UserPreferences? Preferences);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record RecoverPasswordRequest(string Email);

public record ResetPasswordRequest(string Email, string Token, string NewPassword);

public record CatalogMovieRequest(
    string Title,
    string Description,
    string Type,
    string BackdropUrl,
    string PosterUrl,
    string VideoUrl,
    int Year,
    string Rating,
    double Score,
    int MatchScore,
    string Duration,
    List<string> Genres,
    List<string> Cast,
    string Director,
    bool IsFeatured,
    List<CatalogEpisodeRequest> Episodes
);

public record CatalogEpisodeRequest(
    int Season,
    int Number,
    string Title,
    string Description,
    string Duration,
    string ThumbnailUrl,
    string VideoUrl
);

public record WatchProgressRequest(int MovieId, int? EpisodeId, double PositionSeconds, double DurationSeconds);

public record RatingRequest(int Score);

public record AuthResponse(
    bool Success,
    string Message,
    string? Token,
    User? User
);

public class UserSession
{
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);
}

public class PasswordResetToken
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(15);
}
