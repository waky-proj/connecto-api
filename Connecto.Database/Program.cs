using Connecto.Infrastructure.Services.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<ConnectoDbContext>(options =>
    options.UseSqlServer(DatabaseConfiguration.GetConnectionString(builder.Configuration)));

using var host = builder.Build();

using var scope = host.Services.CreateScope();

var db = scope.ServiceProvider
    .GetRequiredService<ConnectoDbContext>();

await db.Database.MigrateAsync();
