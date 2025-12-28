using Backend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Globalization;




var builder = WebApplication.CreateBuilder(args);

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Chilling OG API", Version = "v1" });
});

// DB (Postgres)
var connStr = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    opt.UseNpgsql(connStr);
});

var app = builder.Build();

// Aplicar migraciones al levantar (solo para esta etapa)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Swagger middleware
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Chilling OG API v1");
    c.RoutePrefix = "swagger";
});

app.MapGet("/health", () => Results.Ok(new { ok = true }));

// --- API pública ---
// Temporario: season fija
app.MapGet("/api/season/current", () =>
{
    return Results.Ok(new { season = "2025-W52" });
});

app.MapGet("/api/leaderboard", async (AppDbContext db, string? season) =>
{
    string s;

    if (string.IsNullOrWhiteSpace(season))
    {
        // 🔥 usar la última season cargada
        s = await db.Leaderboard
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => x.Season)
            .FirstOrDefaultAsync() ?? "2025-W52";
    }
    else
    {
        s = season.Trim();
    }

    var rows = await db.Leaderboard
        .Where(x => x.Season == s)
        .OrderBy(x => x.Rank)
        .Select(x => new
{
    rank = x.Rank,
    username = x.Username,
    displayName = x.DisplayName,
    prize = x.Prize,
    bet = x.Bet
})

        .ToListAsync();

    var top = rows.Take(3).ToList();

    DateTime? updatedAtUtc = await db.Leaderboard
        .Where(x => x.Season == s)
        .MaxAsync(x => (DateTime?)x.CreatedAtUtc);

    return Results.Ok(new
    {
        season = s,
        updatedAtUtc,
        top,
        rows
    });
});


// --- Admin import ---
// Espera JSON: { "season": "2025-W52", "csv": "username,displayName,prize,bet\n..." }
app.MapPost("/admin/import", async (HttpRequest req, AppDbContext db, IConfiguration config) =>
{
    // Auth por header
    var key = req.Headers["x-admin-key"].ToString();
    var expected = config["Admin:Key"];

    if (string.IsNullOrWhiteSpace(expected))
        return Results.Problem("Admin key no configurada (Admin__Key).", statusCode: 500);

    if (!string.Equals(key, expected, StringComparison.Ordinal))
        return Results.Unauthorized();

    // Leer body JSON
    var payload = await req.ReadFromJsonAsync<ImportPayload>();
    if (payload is null)
        return Results.BadRequest(new { ok = false, error = "Body inválido (JSON requerido)." });

    var season = string.IsNullOrWhiteSpace(payload.Season) ? "2025-W52" : payload.Season.Trim();
    var csv = payload.Csv ?? "";

    if (string.IsNullOrWhiteSpace(csv))
        return Results.BadRequest(new { ok = false, error = "Falta 'csv'." });

    // Parse CSV
    // Formato esperado:
    // username,displayName,prize,bet
    // nico,Nicolás,15000,5000
    var parsed = ParseCsv(csv);

    if (!string.IsNullOrWhiteSpace(parsed.Error))
        return Results.BadRequest(new { ok = false, error = parsed.Error });

    if (parsed.Rows.Count == 0)
        return Results.BadRequest(new { ok = false, error = "CSV sin filas válidas." });

    // Reemplazar la season completa (simple y efectivo)
    var existing = await db.Leaderboard.Where(x => x.Season == season).ToListAsync();
    if (existing.Count > 0)
    {
        db.Leaderboard.RemoveRange(existing);
        await db.SaveChangesAsync();
    }

    // Calcular rank: por Prize desc, luego Bet desc (ajustamos cuando definas regla real)
    var ordered = parsed.Rows
        .OrderByDescending(x => x.Prize)
        .ThenByDescending(x => x.Bet)
        .ToList();

    var now = DateTime.UtcNow;

    for (int i = 0; i < ordered.Count; i++)
    {
        ordered[i].Season = season;
        ordered[i].Rank = i + 1;
        ordered[i].CreatedAtUtc = now;
    }

    await db.Leaderboard.AddRangeAsync(ordered);
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        ok = true,
        season,
        imported = ordered.Count,
        updatedAtUtc = now
    });
});

app.Run();


// ----- Helpers -----
static ParseCsvResult ParseCsv(string csv)
{
    var list = new List<LeaderboardRow>();

    // tolerante a \r\n / \n
    var lines = csv
        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
        .Select(x => x.Trim())
        .Where(x => x.Length > 0)
        .ToList();

    if (lines.Count == 0)
        return new ParseCsvResult(list, "CSV vacío.");

    var headerParts = lines[0]
        .Split(',')
        .Select(x => x.Trim())
        .ToList();

    if (headerParts.Count == 0)
        return new ParseCsvResult(list, "Header CSV inválido.");

    headerParts[0] = headerParts[0].TrimStart('\uFEFF');

    var headerMap = headerParts
        .Select((name, index) => new { name = name.ToLowerInvariant(), index })
        .ToDictionary(x => x.name, x => x.index);

    var requiredColumns = new[] { "username", "displayname", "prize", "bet" };
    var missingColumns = requiredColumns.Where(col => !headerMap.ContainsKey(col)).ToList();

    if (missingColumns.Count > 0)
    {
        return new ParseCsvResult(list, "Header CSV inválido. Columnas requeridas: username, displayName, prize, bet.");
    }

    if (lines.Count <= 1) return new ParseCsvResult(list, null);

    // 1ra línea header
    var usernameIndex = headerMap["username"];
    var displayNameIndex = headerMap["displayname"];
    var prizeIndex = headerMap["prize"];
    var betIndex = headerMap["bet"];

    for (int i = 1; i < lines.Count; i++)
    {
        var line = lines[i];
        // split simple (si después te pasan comillas y comas adentro, lo mejoramos)
        var parts = line.Split(',');

        if (parts.Length <= Math.Max(Math.Max(usernameIndex, displayNameIndex), Math.Max(prizeIndex, betIndex)))
            continue;

        var username = parts[usernameIndex].Trim();
        var displayName = parts[displayNameIndex].Trim();

        if (string.IsNullOrWhiteSpace(username)) continue;
        if (string.IsNullOrWhiteSpace(displayName)) displayName = username;

        // números: aceptamos "." o ",". 
        // Convertimos con Invariant y reemplazos básicos
        if (!TryParseDecimal(parts[prizeIndex], out var prize)) prize = 0;
        if (!TryParseDecimal(parts[betIndex], out var bet)) bet = 0;

        list.Add(new LeaderboardRow
        {
            Username = username,
            DisplayName = displayName,
            Prize = prize,
            Bet = bet
        });
    }

    return new ParseCsvResult(list, null);

    static bool TryParseDecimal(string s, out decimal val)
    {
        s = (s ?? "").Trim();
        s = s.Replace(" ", "");

        // si viene con miles "15.000" (AR) -> "15000"
        // si viene "15000,50" -> "15000.50"
        // bastante tolerante para pruebas
        if (s.Contains('.') && s.Contains(','))
        {
            // asumimos miles '.' y decimales ','
            s = s.Replace(".", "").Replace(",", ".");
        }
        else if (s.Contains(',') && !s.Contains('.'))
        {
            s = s.Replace(",", ".");
        }

        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out val);
    }
}

record ImportPayload(string? Season, string? Csv);
record ParseCsvResult(List<LeaderboardRow> Rows, string? Error);
