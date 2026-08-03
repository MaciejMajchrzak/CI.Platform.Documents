using CI.Platform.Documents.API.Extensions;
using CI.Platform.Documents.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDocumentsServices(builder.Configuration);
builder.Services.AddOutboxPublisher(builder.Configuration);
builder.Services.AddAuth(builder.Configuration);
builder.Services.AddObservability(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<DocumentsDbContext>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    for (var attempt = 1; attempt <= 10; attempt++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<DocumentsDbContext>()
                .Database.MigrateAsync();
            break;
        }
        catch (Exception ex) when (attempt < 10)
        {
            app.Logger.LogWarning("Migration attempt {Attempt} failed: {Message}. Retrying in 3s…", attempt, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}

app.MapHealthChecks("/health");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
