using Microsoft.EntityFrameworkCore;
using OrderService.Api.Infrastructure.Data;
using MassTransit;
using Serilog;
using System.Reflection;
using FluentValidation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OrderService.Api.Application.Metrics;
using Shared.Exceptions;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddGrpcClient<Shared.Grpc.InventoryGrpcService.InventoryGrpcServiceClient>(options =>
{
    // Getting URL from configuration or default to localhost
    options.Address = new Uri(builder.Configuration["InventoryServiceUrl"] ?? "http://localhost:5050");
}).AddStandardResilienceHandler();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddDbContextPool<OrderDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure()));

builder.Services.AddSingleton<OrderMetrics>();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(builder.Environment.ApplicationName))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("MassTransit")
            .AddOtlpExporter(opt =>
            {
                opt.Endpoint = new Uri(builder.Configuration["Otlp:Endpoint"] ?? "http://localhost:4317");
            });
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddMeter(OrderMetrics.MeterName)
            .AddPrometheusExporter();
    });

builder.Services.AddMediatR(cfg => 
{
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
    cfg.AddOpenBehavior(typeof(Shared.Behaviors.ValidationBehavior<,>));
});

builder.Services.AddAutoMapper(cfg => cfg.AddProfile<OrderService.Api.Application.Mapping.OrderMappingProfile>());

builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

builder.Services.AddMassTransit(x =>
{
    x.AddConfigureEndpointsCallback((context, name, cfg) =>
    {
        if (cfg is IRabbitMqReceiveEndpointConfigurator rmq)
        {
            rmq.EnablePriority(10);
        }
    });

    x.AddConsumer<OrderService.Api.Application.Consumers.PaymentProcessedEventConsumer>();
    x.AddConsumer<OrderService.Api.Application.Consumers.PaymentFailedEventConsumer>();
    x.AddConsumer<OrderService.Api.Application.Consumers.StockReleasedEventConsumer>();
    x.AddConsumer<OrderService.Api.Application.Consumers.OrderCreatedEventFaultConsumer>();

    x.AddEntityFrameworkOutbox<OrderDbContext>(o =>
    {
        o.UsePostgres();
        o.UseBusOutbox();
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
        var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
        var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";

        cfg.Host(rabbitHost, "/", h => {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

        cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));

        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
app.MapPrometheusScrapingEndpoint();

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => true
});

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.Run();
