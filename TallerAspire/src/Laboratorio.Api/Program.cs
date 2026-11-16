using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<LaboratorioDb>("db-integracion");
builder.AddRedisClient("cache-redis");

builder.Services.AddOpenApi(o =>
{
    o.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi2_0;
    o.AddDocumentTransformer((doc, _, _) =>
    {
        doc.Info.Title = "Equipos de Laboratorio";
        doc.Info.Description = "API orquestado con Aspire y expuesto con Dev Tunnels.";
        return Task.CompletedTask;
    });
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapOpenApi();

// Aseguramos que la base de datos esté creada al iniciar la aplicación
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<LaboratorioDb>().Database.EnsureCreatedAsync();
}

const string ClaveCache = "equipos:lista";
var equipos = app.MapGroup("api/v1/equipos").WithTags("Equipos");
 
 // Get - Endpoint para listar todos los equipos del laboratorio, con cache en Redis
equipos.MapGet("/", async Task<Ok<List<Equipo>>> (
    LaboratorioDb db, IConnectionMultiplexer redis, ILogger<Program> log) =>
{
    var cache = redis.GetDatabase();
    var enCache = await cache.StringGetAsync(ClaveCache);
    if (enCache.HasValue)
    {
        log.LogInformation("Lista de equipos servida desde {Origen}", "Redis");
        return TypedResults.Ok(JsonSerializer.Deserialize<List<Equipo>>(enCache.ToString(), JsonSerializerOptions.Web)!);
    }
 
    var lista = await db.Equipos.AsNoTracking().OrderBy(e => e.Id).ToListAsync();
 
    await cache.StringSetAsync(ClaveCache, JsonSerializer.Serialize(lista, JsonSerializerOptions.Web), TimeSpan.FromSeconds(60));
    log.LogInformation("Lista de equipos servida desde {Origen}", "PostgreSQL");
    return TypedResults.Ok(lista);
})
.WithName("ListarEquipos")
.WithSummary("Listar equipos")
.WithDescription("Devuelve todos los equipos del laboratorio.");

// Get - Endpoint para obtener un equipo por su id
equipos.MapGet("/{id:int}", async Task<Results<Ok<Equipo>, NotFound>> (int id, LaboratorioDb db) =>
    await db.Equipos.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id) is { } equipo
        ? TypedResults.Ok(equipo)
        : TypedResults.NotFound())
.WithName("ObtenerEquipo")
.WithSummary("Obtener equipo por id")
.WithDescription("Devuelve un equipo o 404 si no existe.");

// Post - Endpoint para registrar un equipo
equipos.MapPost("/", async Task<Results<Created<Equipo>, ValidationProblem, Conflict<string>>> (
    CrearEquipo entrada, LaboratorioDb db, IConnectionMultiplexer redis, ILogger<Program> log) =>
{
    var errores = new Dictionary<string, string[]>();
    if (string.IsNullOrWhiteSpace(entrada.Codigo)) errores["codigo"] = ["El código es obligatorio."];
    if (string.IsNullOrWhiteSpace(entrada.Nombre)) errores["nombre"] = ["El nombre es obligatorio."];
    if (errores.Count > 0) return TypedResults.ValidationProblem(errores);
 
    // Id = 0 le indica a EF Core que PostgreSQL debe generar el valor.
    var creado = new Equipo(0, entrada.Codigo.Trim(), entrada.Nombre.Trim(), entrada.Operativo, DateTimeOffset.UtcNow);
    db.Equipos.Add(creado);
 
    try
    {
        await db.SaveChangesAsync();
    }
    catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
    {
        return TypedResults.Conflict($"Ya existe un equipo con el código {entrada.Codigo}.");
    }
 
    // Limpiamos el caché de la lista de equipos para que se regenere en la próxima consulta
    await redis.GetDatabase().KeyDeleteAsync(ClaveCache);
    log.LogInformation("Equipo {Codigo} registrado con id {Id}", creado.Codigo, creado.Id);
    return TypedResults.Created($"/api/v1/equipos/{creado.Id}", creado);
})
.WithName("CrearEquipo")
.WithSummary("Registrar equipo")
.WithDescription("Registra un equipo nuevo en el laboratorio.");

app.MapGet("/", () => "Hello World!");

app.Run();

class LaboratorioDb(DbContextOptions<LaboratorioDb> options) : DbContext(options)
{
    public DbSet<Equipo> Equipos => Set<Equipo>();
 
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Equipo>().HasIndex(e => e.Codigo).IsUnique();
    }
}
record Equipo(int Id, string Codigo, string Nombre, bool Operativo, DateTimeOffset FechaRegistro);
record CrearEquipo(string Codigo, string Nombre, bool Operativo);