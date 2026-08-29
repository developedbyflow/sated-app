using System.Text.Json.Serialization;
using Sated.Scoring;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();

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

// Exposed so WebApplicationFactory<Program> can start this application in a test.
public partial class Program;
