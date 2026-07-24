using System.Text.Json.Serialization;
using Amazon.S3;
using FluentValidation;
using FluentValidation.AspNetCore;
using manassa_ticket_backend.Common;
using manassa_ticket_backend.Configuration;
using manassa_ticket_backend.Data;
using manassa_ticket_backend.Helpers;
using manassa_ticket_backend.Messaging;
using manassa_ticket_backend.Services.Contact;
using manassa_ticket_backend.Services.Email;
using manassa_ticket_backend.Services.FileStorage;
using manassa_ticket_backend.Services.Notifications;
using manassa_ticket_backend.Services.Payments;
using manassa_ticket_backend.Services.Subscriptions;
using manassa_ticket_backend.Services.TicketExtraction;
using manassa_ticket_backend.Services.TicketVerification;
using manassa_ticket_backend.Services.Tickets;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Read directly from configuration rather than via IOptions<CorsOptions> — AddCors runs here,
// before the DI container is built, so the options system isn't resolvable yet. appsettings.json
// only sets the local dev default (localhost:5173); production domains come from .env
// (Cors__AllowedOrigins__0, __1, ...) like the rest of the per-environment deploy config, since
// the server never has the repo checked out to edit an appsettings file directly.
var corsOptions = builder.Configuration.GetSection("Cors").Get<CorsOptions>() ?? new CorsOptions();
builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection("Cors"));

const string FrontendCorsPolicy = "FrontendCorsPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins(corsOptions.AllowedOrigins)
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
builder.Services.Configure<R2Options>(
    builder.Configuration.GetSection("Storage:R2"));
builder.Services.Configure<PythonExtractorOptions>(
    builder.Configuration.GetSection("PythonTicketExtractorService"));
builder.Services.Configure<ManassaApiOptions>(
    builder.Configuration.GetSection("ManassaApi"));
builder.Services.Configure<FrontendOptions>(
    builder.Configuration.GetSection("Frontend"));
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMq"));
builder.Services.Configure<SmtpOptions>(
    builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<StripeOptions>(
    builder.Configuration.GetSection("Stripe"));
builder.Services.Configure<ContactOptions>(
    builder.Configuration.GetSection("Contact"));
builder.Services.AddOptions<FeeOptions>()
    .Bind(builder.Configuration.GetSection("Fee"))
    .Validate(o => o.FlatFeeUsd >= 0, "Fee:FlatFeeUsd must not be negative.")
    .Validate(o => o.PercentFee >= 0, "Fee:PercentFee must not be negative.")
    .ValidateOnStart();
builder.Services.AddOptions<TicketPricingOptions>()
    .Bind(builder.Configuration.GetSection("TicketPricing"))
    .Validate(o => o.MaxAskingPriceIncreaseJod >= 0, "TicketPricing:MaxAskingPriceIncreaseJod must not be negative.")
    .ValidateOnStart();
Stripe.StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Postgres (env var ConnectionStrings__Postgres) must be set.");
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(postgresConnectionString));
builder.Services.AddScoped<ITicketDeleteTokenService, TicketDeleteTokenService>();
builder.Services.AddScoped<ITicketReader, TicketReader>();
builder.Services.AddScoped<ITicketDeleter, TicketDeleter>();
builder.Services.AddScoped<ITicketPoster, TicketPoster>();
builder.Services.AddScoped<ITicketProcessor, TicketProcessor>();
builder.Services.AddScoped<ITicketPurchaseService, TicketPurchaseService>();
builder.Services.AddHostedService<TicketReservationSweeper>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var r2 = sp.GetRequiredService<IOptions<R2Options>>().Value;
    var config = new AmazonS3Config
    {
        ServiceURL = $"https://{r2.AccountId}.r2.cloudflarestorage.com",
        ForcePathStyle = true
    };

    return new AmazonS3Client(r2.AccessKeyId, r2.SecretAccessKey, config);
});
builder.Services.AddScoped<IFileStorage, R2FileStorage>();
builder.Services.AddHttpClient<ITicketVerifier, ManassaTicketVerifier>();
builder.Services.AddScoped<IRandomPinGenerator, RandomPinGenerator>();
builder.Services.AddHttpClient<ITicketDataExtractor, PythonTicketDataExtractor>();
builder.Services.AddSingleton<RabbitMqConnectionProvider>();
builder.Services.AddScoped<ITicketAvailablePublisher, RabbitMqTicketAvailablePublisher>();
builder.Services.AddScoped<IEmailMessagePublisher, RabbitMqEmailMessagePublisher>();
builder.Services.AddScoped<ITicketSoldNotificationPublisher, RabbitMqTicketSoldNotificationPublisher>();
builder.Services.AddScoped<ITicketPurchasedNotificationPublisher, RabbitMqTicketPurchasedNotificationPublisher>();
builder.Services.AddScoped<ITicketAvailableNotifier, TicketAvailableNotifier>();
builder.Services.AddScoped<IContactUsService, ContactUsService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddHostedService<TicketAvailableConsumer>();
builder.Services.AddHostedService<EmailNotificationConsumer>();
builder.Services.AddHostedService<TicketSoldNotificationConsumer>();
builder.Services.AddHostedService<TicketPurchasedNotificationConsumer>();
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
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Trust the X-Forwarded-* headers set by the Caddy reverse proxy so the app
// sees the original HTTPS scheme. Without this, UseHttpsRedirection would loop
// forever behind the proxy. KnownProxies/Networks are cleared because the proxy
// is another Docker container on an arbitrary internal IP.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

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
