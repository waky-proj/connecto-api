using Connecto.Application.Internal;
using Connecto.Application.Internal.AI;
using Connecto.Application.Internal.Calculators;
using Connecto.Application.Internal.Impl;
using Connecto.Domain.AI;
using Connecto.Infrastructure.Services;
using Connecto.Infrastructure.Services.Data;
using Connecto.Infrastructure.Services.Impl;
using Microsoft.EntityFrameworkCore;
using Connecto.Api.Authentication;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCognitoAuthentication(builder.Configuration);

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
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy"
})).AllowAnonymous();

app.Run();

public partial class Program;
