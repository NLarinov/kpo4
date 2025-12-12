using Microsoft.EntityFrameworkCore;
using OrdersService.BackgroundServices;
using OrdersService.Data;
using OrdersService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? "Server=localhost;Database=OrdersDb;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;";

builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseSqlServer(connectionString));

// Services
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddSingleton<IMessagePublisher, RabbitMQMessagePublisher>();
builder.Services.AddSingleton<IMessageConsumer, RabbitMQMessageConsumer>();

// Background services
builder.Services.AddHostedService<OutboxPublisherService>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Start message consumer
var messageConsumer = app.Services.GetRequiredService<IMessageConsumer>();
var cancellationTokenSource = new CancellationTokenSource();
_ = Task.Run(() => messageConsumer.StartConsumingAsync("payment_results", cancellationTokenSource.Token));

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
    dbContext.Database.EnsureCreated();
}

app.Run();
