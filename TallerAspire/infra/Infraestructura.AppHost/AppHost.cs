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

builder.Build().Run();
