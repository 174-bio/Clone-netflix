using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using NetflixClone.Models;
using NetflixClone.Services;

var builder = WebApplication.CreateBuilder(args);
var port = Environment.GetEnvironmentVariable("PORT") ?? "5167";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Injeção do MovieService como Singleton
builder.Services.AddSingleton<MovieService>();

// Preserva nomes e serialização JSON padronizada
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null;
    options.SerializerOptions.WriteIndented = false;
});

// Configuração restrita de CORS (mitiga ataques cross-origin indesejados)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5167", "https://localhost:5167", "http://127.0.0.1:5167")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Rate Limiting para proteção contra ataques de força bruta e credential stuffing
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Política para endpoints sensíveis de autenticação (máximo 10 requisições por minuto por IP)
    options.AddPolicy("auth-limit", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 15,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
});

var app = builder.Build();

// Middleware de cabeçalhos de segurança HTTP recomendados pela OWASP
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    await next();
});

app.UseCors();
app.UseRateLimiter();
app.UseDefaultFiles();
app.UseStaticFiles();

// Helper seguro para autenticação de sessão via Bearer Token validado
static string? ResolveUserIdFromSession(HttpContext context, MovieService service)
{
    if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
    {
        var raw = authHeader.ToString();
        if (raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = raw["Bearer ".Length..].Trim();
            var user = service.GetUserBySessionToken(token);
            if (user != null) return user.Id;
        }
    }
    return null;
}

// ==========================================
// AUTHENTICATION & PROFILE ENDPOINTS
// ==========================================

app.MapPost("/api/auth/register", (MovieService service, RegisterRequest req) =>
{
    var res = service.Register(req);
    return res.Success ? Results.Ok(res) : Results.BadRequest(res);
}).RequireRateLimiting("auth-limit");

app.MapPost("/api/auth/login", (MovieService service, LoginRequest req) =>
{
    var res = service.Login(req);
    return res.Success ? Results.Ok(res) : Results.BadRequest(res);
}).RequireRateLimiting("auth-limit");

app.MapGet("/api/auth/me", (HttpContext ctx, MovieService service) =>
{
    var userId = ResolveUserIdFromSession(ctx, service);
    if (userId == null) return Results.Unauthorized();

    var user = service.GetUserBySessionToken(ctx.Request.Headers["Authorization"].ToString()["Bearer ".Length..].Trim());
    return user != null ? Results.Ok(user) : Results.Unauthorized();
});

app.MapPut("/api/auth/update-profile", (HttpContext ctx, MovieService service, UpdateProfileRequest req) =>
{
    var userId = ResolveUserIdFromSession(ctx, service);
    if (userId == null) return Results.Unauthorized();

    var res = service.UpdateProfile(userId, req);
    return res.Success ? Results.Ok(res) : Results.BadRequest(res);
});

app.MapPost("/api/auth/change-password", (HttpContext ctx, MovieService service, ChangePasswordRequest req) =>
{
    var userId = ResolveUserIdFromSession(ctx, service);
    if (userId == null) return Results.Unauthorized();

    var res = service.ChangePassword(userId, req);
    return res.Success ? Results.Ok(res) : Results.BadRequest(res);
}).RequireRateLimiting("auth-limit");

app.MapPost("/api/auth/recover-password", (MovieService service, RecoverPasswordRequest req) =>
{
    var res = service.RecoverPassword(req);
    return Results.Ok(res);
}).RequireRateLimiting("auth-limit");

app.MapPost("/api/auth/reset-password", (MovieService service, ResetPasswordRequest req) =>
{
    var res = service.ResetPassword(req);
    return res.Success ? Results.Ok(res) : Results.BadRequest(res);
}).RequireRateLimiting("auth-limit");

app.MapPost("/api/auth/logout", (HttpContext ctx, MovieService service) =>
{
    if (ctx.Request.Headers.TryGetValue("Authorization", out var authHeader))
    {
        var raw = authHeader.ToString();
        if (raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = raw["Bearer ".Length..].Trim();
            service.Logout(token);
        }
    }
    return Results.Ok(new { message = "Logout realizado com sucesso" });
});

// ==========================================
// CATALOG & MOVIE ENDPOINTS (PÚBLICOS & COM DECORAÇÃO DE USUÁRIO)
// ==========================================

app.MapGet("/api/movies", (HttpContext ctx, MovieService service, string? type, string? genre, int? year, double? minScore, string? rating, string? sortBy) =>
{
    var userId = ResolveUserIdFromSession(ctx, service);
    var movies = service.GetAllMovies(type, genre, year, minScore, rating, sortBy, userId);
    return Results.Ok(movies);
});

app.MapGet("/api/movies/featured", (HttpContext ctx, MovieService service) =>
{
    var userId = ResolveUserIdFromSession(ctx, service);
    var featured = service.GetFeaturedMovie(userId);
    return featured is not null ? Results.Ok(featured) : Results.NotFound();
});

app.MapGet("/api/movies/categories", (HttpContext ctx, MovieService service) =>
{
    var userId = ResolveUserIdFromSession(ctx, service);
    return Results.Ok(service.GetCategorizedMovies(userId));
});

app.MapGet("/api/movies/recommendations", (HttpContext ctx, MovieService service) =>
{
    var userId = ResolveUserIdFromSession(ctx, service);
    return Results.Ok(service.GetRecommendations(userId ?? ""));
});

app.MapGet("/api/movies/continue-watching", (HttpContext ctx, MovieService service) =>
{
    var userId = ResolveUserIdFromSession(ctx, service);
    if (userId == null) return Results.Unauthorized();
    return Results.Ok(service.GetContinueWatching(userId));
});

app.MapGet("/api/movies/{id:int}", (HttpContext ctx, MovieService service, int id) =>
{
    var userId = ResolveUserIdFromSession(ctx, service);
    var movie = service.GetMovieById(id, userId);
    return movie is not null ? Results.Ok(movie) : Results.NotFound(new { message = "Título não encontrado" });
});

app.MapGet("/api/search", (HttpContext ctx, MovieService service, string? q) =>
{
    var userId = ResolveUserIdFromSession(ctx, service);
    return Results.Ok(service.Search(q ?? string.Empty, userId));
});

// ==========================================
// WATCHLIST, PROGRESS & RATINGS (EXIGEM SESSÃO VÁLIDA)
// ==========================================

app.MapGet("/api/watchlist", (HttpContext ctx, MovieService service) =>
{
    var userId = ResolveUserIdFromSession(ctx, service);
    if (userId == null) return Results.Unauthorized();
    return Results.Ok(service.GetWatchlist(userId));
});

app.MapPost("/api/watchlist/{id:int}", (HttpContext ctx, MovieService service, int id) =>
{
    var userId = ResolveUserIdFromSession(ctx, service);
    if (userId == null) return Results.Unauthorized();

    var isInWatchlist = service.ToggleWatchlist(userId, id);
    return Results.Ok(new 
    { 
        movieId = id, 
        isInWatchlist, 
        message = isInWatchlist ? "Adicionado à Minha Lista" : "Removido da Minha Lista" 
    });
});

app.MapPost("/api/progress", (HttpContext ctx, MovieService service, WatchProgressRequest req) =>
{
    var userId = ResolveUserIdFromSession(ctx, service);
    if (userId == null) return Results.Unauthorized();

    var prog = service.SaveProgress(userId, req.MovieId, req.EpisodeId, req.PositionSeconds, req.DurationSeconds);
    return prog != null ? Results.Ok(new { success = true, progress = prog }) : Results.NotFound(new { message = "Título não encontrado" });
});

app.MapPost("/api/movies/{id:int}/rate", (HttpContext ctx, MovieService service, int id, RatingRequest req) =>
{
    var userId = ResolveUserIdFromSession(ctx, service);
    if (userId == null) return Results.Unauthorized();

    var savedScore = service.SetRating(userId, id, req.Score);
    return Results.Ok(new { movieId = id, userRating = savedScore, message = "Avaliação salva com sucesso!" });
});

app.MapPost("/api/movies/{id:int}/like", (MovieService service, int id) =>
{
    var newLikes = service.ToggleLike(id);
    return Results.Ok(new { movieId = id, likes = newLikes });
});

app.Run();
