using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure;
using SupportDesk.Infrastructure.Services;
using SupportDesk.Presentation.Controllers;
using SupportDesk.Presentation.Extensions;
using SupportDesk.Presentation.Filters;
using SupportDesk.Presentation.Middleware;
using SupportDesk.Presentation.Security;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "frontend";

builder.Services
    .AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(options =>
    {
        // Enums travel as their names, both ways, so the API reads well from the browser.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddCors(options => options.AddPolicy(
    FrontendCorsPolicy,
    policy => policy
        .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddOptions<SlaPolicy>()
    .BindConfiguration("Sla").Validate(x => x.IsValid(), "Invalid SLA configuration.").ValidateOnStart();
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<SlaPolicy>>().Value);
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
if (Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32 || jwt.LifetimeMinutes is < 1 or > 120 || string.IsNullOrWhiteSpace(jwt.Issuer) || string.IsNullOrWhiteSpace(jwt.Audience))
    throw new InvalidOperationException("Configure Jwt__SigningKey (at least 32 bytes), issuer, audience and lifetime. See .env.example.");
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(15),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
    };
});
builder.Services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddTransient<IValidator<EscalationBody>, EscalationBodyValidator>();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Keeps a developer machine one command away from a working database.
    await app.MigrateAndSeedAsync();
}

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

// Exposes the real composition root to the HTTP integration test host.
public partial class Program { }
