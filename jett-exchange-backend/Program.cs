using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.AspNetCore;
using jett_exchange_backend.Common;
using jett_exchange_backend.Configuration;
using jett_exchange_backend.Data;
using jett_exchange_backend.Helpers;
using jett_exchange_backend.Messaging;
using jett_exchange_backend.Services.Contact;
using jett_exchange_backend.Services.FileStorage;
using jett_exchange_backend.Services.Notifications;
using jett_exchange_backend.Services.Payments;
using jett_exchange_backend.Services.Subscriptions;
using jett_exchange_backend.Services.TicketExtraction;
using jett_exchange_backend.Services.TicketVerification;
using jett_exchange_backend.Services.Tickets;
using jett_exchange_backend.Validators;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "FrontendCorsPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:5174", "http://127.0.0.1:5174")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("X-Delete-Token");
    });
});

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection("Storage"));
builder.Services.Configure<PythonExtractorOptions>(
    builder.Configuration.GetSection("PythonTicketExtractorService"));
builder.Services.Configure<JettApiOptions>(
    builder.Configuration.GetSection("JettApi"));
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMq"));
builder.Services.Configure<StripeOptions>(
    builder.Configuration.GetSection("Stripe"));
builder.Services.Configure<ContactOptions>(
    builder.Configuration.GetSection("Contact"));
Stripe.StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

// SQLite's ":memory:" database only lives as long as a connection to it stays open, and
// "cache=shared" lets every scoped DbContext open its own connection (safe under concurrent
// requests) while still seeing the same in-memory data. This keep-alive connection is what
// keeps the shared in-memory database from being torn down between requests.
const string SqliteInMemoryConnectionString = "Data Source=file:JettTickets?mode=memory&cache=shared";
var keepAliveConnection = new SqliteConnection(SqliteInMemoryConnectionString);
keepAliveConnection.Open();
builder.Services.AddSingleton(keepAliveConnection);
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(SqliteInMemoryConnectionString));
builder.Services.AddScoped<ITicketDeleteTokenService, TicketDeleteTokenService>();
builder.Services.AddScoped<ITicketReader, TicketReader>();
builder.Services.AddScoped<ITicketDeleter, TicketDeleter>();
builder.Services.AddScoped<ITicketPoster, TicketPoster>();
builder.Services.AddScoped<ITicketPurchaseService, TicketPurchaseService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IFileStorage, LocalFileStorage>();
builder.Services.AddHttpClient<ITicketVerifier, JettTicketVerifier>();
builder.Services.AddScoped<IRandomPinGenerator, RandomPinGenerator>();
builder.Services.AddHttpClient<ITicketDataExtractor, PythonTicketDataExtractor>();
builder.Services.AddSingleton<RabbitMqConnectionProvider>();
builder.Services.AddScoped<ITicketAvailablePublisher, RabbitMqTicketAvailablePublisher>();
builder.Services.AddScoped<IEmailMessagePublisher, RabbitMqEmailMessagePublisher>();
builder.Services.AddScoped<ITicketAvailableNotifier, TicketAvailableNotifier>();
builder.Services.AddScoped<IContactUsService, ContactUsService>();
builder.Services.AddHostedService<TicketAvailableConsumer>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });
builder.Services.AddFluentValidationAutoValidation();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .SelectMany(x => x.Value!.Errors)
            .Select(x => x.ErrorMessage)
            .ToList();

        var response = new ApiResponse<string>
        {
            Success = false,
            Message = "Validation failed",
            Errors = errors,
            Links = new Dictionary<string, string>
            {
                { "home", "/home" },
            }
        };

        return new BadRequestObjectResult(response);
    };
});
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);
app.MapControllers();

app.Run();

public partial class Program
{
}
