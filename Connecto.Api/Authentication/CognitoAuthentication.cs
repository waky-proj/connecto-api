using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace Connecto.Api.Authentication;

public static class CognitoAuthentication
{
    public static IServiceCollection AddCognitoAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var issuer = configuration["Cognito:Issuer"];
        var clientId = configuration["Cognito:Audience"];
        if (!Uri.TryCreate(issuer, UriKind.Absolute, out var uri) || uri.Scheme != "https" || string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("Cognito:Issuer (HTTPS) and Cognito:Audience (app client ID) are required.");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.Authority = issuer;
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = issuer,
                ValidateAudience = false, // Cognito access tokens use client_id, checked below.
                ValidateLifetime = true, RequireExpirationTime = true,
                RequireSignedTokens = true, ValidateIssuerSigningKey = true,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256], ClockSkew = TimeSpan.FromSeconds(30)
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    var user = context.Principal!;
                    if (user.FindFirst("token_use")?.Value != "access" || user.FindFirst("client_id")?.Value != clientId || string.IsNullOrWhiteSpace(user.FindFirst("sub")?.Value))
                        context.Fail("A Cognito access token for the configured client is required.");
                    return Task.CompletedTask;
                }
            };
        });
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            options.AddPolicy("Owner", policy => policy.RequireAuthenticatedUser().AddRequirements(new OwnerRequirement()));
        });
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IAuthorizationHandler, OwnershipAuthorization>();
        return services;
    }
}
