using Connecto.Application.Internal;
using Connecto.Application.Internal.AI;
using Connecto.Application.Internal.Calculators;
using Connecto.Application.Internal.Impl;
using Connecto.Domain.AI;
using Connecto.Infrastructure.Services;
using Connecto.Infrastructure.Services.Data;
using Connecto.Infrastructure.Services.Impl;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOpenApi();

builder.Services
    .AddOptions<OpenAIOptions>()
    .Bind(builder.Configuration.GetSection("OpenAI"))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.ApiKey),
        "OpenAI API key is required.")
    .ValidateOnStart();

builder.Services.AddTransient<IMatchingEngine, MatchingEngine>();
builder.Services.AddScoped<INeedService, NeedService>();
builder.Services.AddScoped<IOfferingService, OfferingService>();
builder.Services.AddScoped<IMatchService, MatchService>();
builder.Services.AddScoped<IMatchingEngine, MatchingEngine>();
builder.Services.AddScoped<IListingParser, OpenAIListingParser>();

builder.Services.AddScoped<IListingParseService, ListingParseService>();

builder.Services.AddTransient<ICalculator, AvailabilityCalculator>();
builder.Services.AddTransient<ICalculator, BudgetCalculator>();
builder.Services.AddTransient<ICalculator, ExperienceCalculator>();
builder.Services.AddTransient<ICalculator, LocationCalculator>();
builder.Services.AddTransient<ICalculator, ReputationCalculator>();
builder.Services.AddTransient<ICalculator, RequirementCalculator>();
builder.Services.AddTransient<ICalculator, SkillCalculator>();

builder.Services.AddScoped<INeedDbService, NeedDbService>();
builder.Services.AddScoped<IOfferingDbService, OfferingDbService>();
builder.Services.AddScoped<IUserDbService, UserDbService>();

builder.Services.AddDbContext<ConnectoDbContext>(options => 
    options.UseSqlServer("Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=ConnectoDB;Integrated Security=True;Connect Timeout=30;Encrypt=True;Trust Server Certificate=False;Application Intent=ReadWrite;Multi Subnet Failover=False;Command Timeout=30"));//builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
