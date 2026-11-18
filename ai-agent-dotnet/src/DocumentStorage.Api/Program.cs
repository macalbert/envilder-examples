using Amazon.S3;
using DocumentStorage.Api.Documents;
using DocumentStorage.Api.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();

builder.Services.AddOptions<DocumentStorageOptions>()
	.BindConfiguration(DocumentStorageOptions.SectionName)
	.ValidateDataAnnotations()
	.ValidateOnStart();

// The AWS SDK's default chain supplies region and credentials (environment, profile,
// container or instance role). The application does not know or care where they come from.
builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client());
builder.Services.AddSingleton<IDocumentStorage, S3DocumentStorage>();
builder.Services.AddSingleton<DocumentService>();

var app = builder.Build();

app.UseExceptionHandler();

app.MapGet("/health", () => Results.Ok());
app.MapDocuments();

app.Run();

public partial class Program;
