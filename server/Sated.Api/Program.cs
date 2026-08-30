using System.Text.Json.Serialization;
using Sated.Scoring;
using Microsoft.EntityFrameworkCore;
using Sated.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddOpenApi();
builder.Services.AddDbContext<SatedDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Sated")));

var calibration = Calibration.Load();

builder.Services.AddSingleton(calibration);
builder.Services.AddSingleton(calibration.Engine());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();

public partial class Program;
