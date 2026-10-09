using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.FileProviders;
using NetflixClone.Models;
using NetflixClone.Services;

var builder = WebApplication.CreateBuilder(args);
var port = Environment.GetEnvironmentVariable("PORT") ?? "5167";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Injeção do MovieService como Singleton
builder.Services.AddSingleton<MovieService>();
builder.Services.AddSingleton<VideoUploadService>();

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
var uploadService = app.Services.GetRequiredService<VideoUploadService>();
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadService.UploadDirectory),
    RequestPath = "/uploads"
});

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

static bool IsAdminRequest(HttpContext context, MovieService service)
{
    if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
    {
        return false;
    }

    var raw = authHeader.ToString();
    if (!raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    var token = raw["Bearer ".Length..].Trim();
    return service.GetUserBySessionToken(token)?.IsAdmin == true;
}

static string? ValidateCatalogMovie(CatalogMovieRequest request, VideoUploadService uploads)
{
    static bool IsHttpUrl(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 2048
        && Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 150)
        return "Informe um título com até 150 caracteres.";
    if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Length > 4000)
        return "Informe uma sinopse com até 4.000 caracteres.";
    if (request.Type is not "movie" and not "series")
        return "O tipo deve ser filme ou série.";
    if (request.Year < 1888 || request.Year > DateTime.UtcNow.Year + 10)
        return "Informe um ano de lançamento válido.";
    if (request.Rating is not "Livre" and not "10+" and not "12+" and not "14+" and not "16+" and not "18+")
        return "Selecione uma classificação indicativa válida.";
    if (!double.IsFinite(request.Score) || request.Score < 0 || request.Score > 10)
        return "A nota deve estar entre 0 e 10.";
    if (request.MatchScore is < 0 or > 100)
        return "A relevância deve estar entre 0 e 100.";
    if (string.IsNullOrWhiteSpace(request.Duration) || request.Duration.Length > 60)
        return "Informe uma duração com até 60 caracteres.";
    if (request.Director?.Length > 120)
        return "O nome da direção deve ter até 120 caracteres.";
    if (request.Genres is null || request.Genres.Count > 12 || request.Genres.Any(item => string.IsNullOrWhiteSpace(item) || item.Length > 40))
        return "Informe até 12 gêneros com no máximo 40 caracteres cada.";
    if (request.Cast is null || request.Cast.Count > 50 || request.Cast.Any(item => string.IsNullOrWhiteSpace(item) || item.Length > 100))
        return "Informe até 50 pessoas no elenco com no máximo 100 caracteres cada.";
    if (request.Episodes is null || request.Episodes.Count > 100)
        return "Uma série pode ter no máximo 100 episódios.";
    if (request.Episodes.Any(episode =>
        episode is null
        || episode.Season < 1
        || episode.Number < 1
        || string.IsNullOrWhiteSpace(episode.Title)
        || episode.Title.Length > 150
        || episode.Description?.Length > 2000
        || episode.Duration?.Length > 60
        || (!string.IsNullOrEmpty(episode.ThumbnailUrl) && !IsHttpUrl(episode.ThumbnailUrl))
        || !IsAllowedVideoUrl(episode.VideoUrl, uploads)))
        return "Revise os dados dos episódios. Cada episódio precisa de título e vídeo válido.";
    if (request.Episodes.GroupBy(episode => (episode.Season, episode.Number)).Any(group => group.Count() > 1))
        return "Não repita o número de um episódio na mesma temporada.";
    if (!IsHttpUrl(request.PosterUrl) || !IsHttpUrl(request.BackdropUrl))
        return "Informe URLs HTTP ou HTTPS válidas para o pôster e a imagem de fundo.";
    if (request.Type == "movie" && !IsAllowedVideoUrl(request.VideoUrl, uploads))
        return "Informe ou envie o vídeo do filme.";
    if (request.Type == "series" && request.Episodes.Count == 0 && !IsAllowedVideoUrl(request.VideoUrl, uploads))
        return "Adicione episódios com vídeo ou informe um vídeo para a série.";
    if (!string.IsNullOrWhiteSpace(request.VideoUrl) && !IsAllowedVideoUrl(request.VideoUrl, uploads))
        return "O vídeo principal precisa usar uma URL HTTP/HTTPS ou um arquivo enviado pelo painel.";

    return null;

    static bool IsAllowedVideoUrl(string? value, VideoUploadService uploadService) =>
        !string.IsNullOrWhiteSpace(value)
        && (uploadService.IsManagedVideoUrl(value)
            || (Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)));
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

// ==========================================
// ADMIN CATALOG MANAGEMENT
// ==========================================

app.MapGet("/api/admin/movies", (HttpContext ctx, MovieService service) =>
{
    if (!IsAdminRequest(ctx, service)) return Results.Unauthorized();
    return Results.Ok(service.GetAdminCatalog());
});

app.MapPost("/api/admin/movies", (HttpContext ctx, MovieService service, VideoUploadService uploads, CatalogMovieRequest request) =>
{
    if (!IsAdminRequest(ctx, service)) return Results.Unauthorized();

    var validationError = ValidateCatalogMovie(request, uploads);
    if (validationError != null) return Results.BadRequest(new { Message = validationError });

    var movie = service.SaveCatalogMovie(null, request);
    return Results.Created($"/api/movies/{movie!.Id}", movie);
});

app.MapPut("/api/admin/movies/{id:int}", async (HttpContext ctx, MovieService service, VideoUploadService uploads, int id, CatalogMovieRequest request) =>
{
    if (!IsAdminRequest(ctx, service)) return Results.Unauthorized();

    var validationError = ValidateCatalogMovie(request, uploads);
    if (validationError != null) return Results.BadRequest(new { Message = validationError });

    var previous = service.GetMovieById(id);
    if (previous == null) return Results.NotFound(new { Message = "Título não encontrado." });

    var movie = service.SaveCatalogMovie(id, request);
    var retainedVideoUrls = service.GetAdminCatalog()
        .SelectMany(item => new[] { item.VideoUrl }.Concat(item.Episodes.Select(episode => episode.VideoUrl)))
        .ToHashSet(StringComparer.Ordinal);
    foreach (var oldVideoUrl in new[] { previous.VideoUrl }.Concat(previous.Episodes.Select(episode => episode.VideoUrl)).Distinct(StringComparer.Ordinal))
    {
        if (!retainedVideoUrls.Contains(oldVideoUrl))
            await uploads.DeleteManagedVideoAsync(oldVideoUrl, ctx.RequestAborted);
    }

    return Results.Ok(movie);
});

app.MapDelete("/api/admin/movies/{id:int}", async (HttpContext ctx, MovieService service, VideoUploadService uploads, int id) =>
{
    if (!IsAdminRequest(ctx, service)) return Results.Unauthorized();

    var deleted = service.DeleteCatalogMovie(id);
    if (deleted == null) return Results.NotFound(new { Message = "Título não encontrado." });

    var retainedVideoUrls = service.GetAdminCatalog()
        .SelectMany(item => new[] { item.VideoUrl }.Concat(item.Episodes.Select(episode => episode.VideoUrl)))
        .ToHashSet(StringComparer.Ordinal);
    foreach (var oldVideoUrl in new[] { deleted.VideoUrl }.Concat(deleted.Episodes.Select(episode => episode.VideoUrl)).Distinct(StringComparer.Ordinal))
    {
        if (!retainedVideoUrls.Contains(oldVideoUrl))
            await uploads.DeleteManagedVideoAsync(oldVideoUrl, ctx.RequestAborted);
    }

    return Results.Ok(new { Message = "Título removido do catálogo." });
});

app.MapGet("/api/admin/videos/storage", async (HttpContext ctx, MovieService service, VideoUploadService uploads) =>
{
    if (!IsAdminRequest(ctx, service))
        return Results.Unauthorized();

    return Results.Ok(await uploads.GetStorageUsageAsync(ctx.RequestAborted));
});

app.MapPost("/api/admin/videos", async (HttpContext ctx, MovieService service, VideoUploadService uploads) =>
{
    if (!IsAdminRequest(ctx, service))
        return Results.Unauthorized();

    var maxFileSize = uploads.MaxFileSize;
    var requestBodyLimit = ctx.Features.Get<IHttpMaxRequestBodySizeFeature>();
    if (requestBodyLimit is { IsReadOnly: false })
    {
        requestBodyLimit.MaxRequestBodySize = maxFileSize;
    }

    if (ctx.Request.ContentLength > maxFileSize)
    {
        return Results.Json(new { Message = "O vídeo excede o limite de tamanho configurado." }, statusCode: StatusCodes.Status413PayloadTooLarge);
    }

    try
    {
        var videoUrl = await uploads.SaveAsync(
            ctx.Request.Body,
            ctx.Request.Query["fileName"].ToString(),
            ctx.Request.ContentLength,
            ctx.RequestAborted);
        var storageUsage = await uploads.GetStorageUsageAsync(ctx.RequestAborted);
        return Results.Ok(new { Url = videoUrl, StorageUsage = storageUsage });
    }
    catch (VideoUploadException ex)
    {
        return Results.Json(new { Message = ex.Message }, statusCode: ex.StatusCode);
    }
});

app.MapGet("/uploads/{fileName}", (string fileName, VideoUploadService uploads) =>
{
    var path = uploads.GetManagedVideoPath(fileName);
    if (path == null) return Results.NotFound();

    var contentType = Path.GetExtension(fileName).Equals(".webm", StringComparison.OrdinalIgnoreCase)
        ? "video/webm"
        : "video/mp4";
    return Results.File(path, contentType, enableRangeProcessing: true);
});

app.Run();
