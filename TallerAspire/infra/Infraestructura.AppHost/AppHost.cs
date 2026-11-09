var builder = DistributedApplication.CreateBuilder(args);

var cache = builder
    .AddRedis("cache-redis")
    .WithImageTag("7-alpine")
    .WithDataVolume("aspire-cache-data");

var postgres = builder
    .AddPostgres("servidor-postgres")
    .WithImageTag("16-alpine")
    .WithDataVolume("aspire-postgres-data");

var db = postgres.AddDatabase("db-integracion", "db_integracion");

var api = builder
    .AddProject<Projects.Laboratorio_Api>("api-laboratorio")
    .WithReference(cache)
    .WithReference(db)
    .WaitFor(cache)
    .WaitFor(db);

builder.Build().Run();
