var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Add a health check endpoint for ECS/ALB health checks
app.MapHealthChecks("/health");

// Add a simple welcome endpoint
app.MapGet("/", () => new { 
    message = "Welcome to MyApp API",
    environment = Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "Development",
    region = Environment.GetEnvironmentVariable("REGION") ?? "local",
    version = "1.0.0",
    timestamp = DateTime.UtcNow
});

app.Run();