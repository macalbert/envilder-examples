using DocumentStorage.Api.Documents;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddSingleton<DocumentService>();

var app = builder.Build();

app.UseExceptionHandler();

app.MapGet("/health", () => Results.Ok());
app.MapDocuments();

app.Run();

public partial class Program;
