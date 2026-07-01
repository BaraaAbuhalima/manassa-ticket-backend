using jett_notification_service.Configuration;
using jett_notification_service.Messaging;
using jett_notification_service.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMq"));
builder.Services.Configure<SmtpOptions>(
    builder.Configuration.GetSection("Smtp"));

builder.Services.AddSingleton<RabbitMqConnectionProvider>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddHostedService<EmailNotificationConsumer>();

var host = builder.Build();
host.Run();
