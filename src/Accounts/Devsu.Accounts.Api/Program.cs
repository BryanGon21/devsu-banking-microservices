using System.Text.Json.Serialization;
using Devsu.Accounts.Api.Errors;
using Devsu.Accounts.Api.Serialization;
using Devsu.Accounts.Application.Services;
using Devsu.Accounts.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new SpanishAccountTypeJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new SpanishMovementTypeJsonConverter());
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(allowIntegerValues: false));
    });
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        ValidationProblemDetails problemDetails = new(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid request",
            Detail = "One or more request fields are invalid.",
            Instance = context.HttpContext.Request.Path,
        };
        problemDetails.Extensions["code"] = "invalid_request";
        problemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        BadRequestObjectResult result = new(problemDetails);
        result.ContentTypes.Add("application/problem+json");
        return result;
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Devsu Accounts API",
        Version = "v1",
    });
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IAccountMovementService, AccountMovementService>();
builder.Services.AddScoped<IAccountStatementService, AccountStatementService>();
builder.Services.AddAccountsInfrastructure(builder.Configuration);

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});
app.MapGet("/", () => Results.Redirect("/swagger"))
    .ExcludeFromDescription();

app.Run();

public partial class Program;
