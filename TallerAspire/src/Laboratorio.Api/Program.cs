using Microsoft.EntityFrameworkCore;

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

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapOpenApi();

app.MapGet("/", () => "Hello World!");

app.Run();

class LaboratorioDb(DbContextOptions<LaboratorioDb> options) : DbContext(options)
{
}
