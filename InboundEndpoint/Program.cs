using Microsoft.EntityFrameworkCore;
using InboundEndpoint.Context;
using InboundEndpoint.Repository;
using Infrastructure.Kafka;
using InboundEndpoint.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
);

// Configure Kafka Connector
builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Connector>>();
    var configuration = sp.GetRequiredService<IConfiguration>();
    var bootstrapServers = configuration.GetValue<string>("Kafka:BootstrapServers");
    var topic = configuration.GetValue<string>("Kafka:Topic");
    if(string.IsNullOrEmpty(bootstrapServers) || string.IsNullOrEmpty(topic))
    {
        throw new Exception("Kafka configuration is invalid");
    }
    return new Connector(logger, bootstrapServers, topic);
});

builder.Services.AddScoped<LogService>();
builder.Services.AddScoped<UserEntity>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => { c.SwaggerEndpoint("./v1/swagger.json", "LogController"); });
}

app.UseAuthorization();

app.MapControllers();

app.Run();
