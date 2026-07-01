using FluentValidation;
using FluentValidation.AspNetCore;
using jett_exchange_backend.Common;
using jett_exchange_backend.Configuration;
using jett_exchange_backend.Data;
using jett_exchange_backend.Helpers;
using jett_exchange_backend.Messaging;
using jett_exchange_backend.Services.FileStorage;
using jett_exchange_backend.Services.Notifications;
using jett_exchange_backend.Services.Payments;
using jett_exchange_backend.Services.Subscriptions;
using jett_exchange_backend.Services.TicketExtraction;
using jett_exchange_backend.Services.TicketVerification;
using jett_exchange_backend.Services.Tickets;
using jett_exchange_backend.Validators;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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
Stripe.StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];
builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("JettTickets"));
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
builder.Services.AddHostedService<TicketAvailableConsumer>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

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
            Errors = errors
        };

        return new BadRequestObjectResult(response);
    };
});
var app = builder.Build();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

public partial class Program
{
}
