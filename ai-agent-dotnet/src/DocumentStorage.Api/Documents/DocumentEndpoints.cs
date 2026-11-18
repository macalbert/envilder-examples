namespace DocumentStorage.Api.Documents;

public static class DocumentEndpoints
{
	public static IEndpointRouteBuilder MapDocuments(this IEndpointRouteBuilder app)
	{
		var documents = app.MapGroup("/documents");

		// A JSON API without cookie authentication: no antiforgery token to check.
		documents.MapPost("/", UploadAsync).DisableAntiforgery();
		documents.MapGet("/{id}", DownloadAsync);

		return app;
	}

	private static async Task<IResult> UploadAsync(
		HttpRequest request,
		DocumentService documents,
		CancellationToken cancellationToken)
	{
		if (!request.HasFormContentType)
		{
			return Results.Problem("Send the document as multipart/form-data in a field named file.", statusCode: 400);
		}

		var form = await request.ReadFormAsync(cancellationToken);
		var file = form.Files.GetFile("file");

		if (file is null)
		{
			return Results.Problem("Send the document as multipart/form-data in a field named file.", statusCode: 400);
		}

		if (file.Length > DocumentService.MaxContentBytes)
		{
			return Results.Problem($"The document is larger than {DocumentService.MaxContentBytes} bytes.", statusCode: 400);
		}

		using var buffer = new MemoryStream();
		await file.CopyToAsync(buffer, cancellationToken);

		try
		{
			var id = await documents.UploadAsync(buffer.ToArray(), file.ContentType, cancellationToken);
			return Results.Created($"/documents/{id}", new { id = id.ToString() });
		}
		catch (InvalidDocumentException exception)
		{
			return Results.Problem(exception.Message, statusCode: 400);
		}
	}

	private static async Task<IResult> DownloadAsync(
		string id,
		DocumentService documents,
		CancellationToken cancellationToken)
	{
		if (!DocumentId.TryParse(id, out var documentId))
		{
			return Results.Problem("The document id is not valid.", statusCode: 400);
		}

		var document = await documents.DownloadAsync(documentId, cancellationToken);

		return document is null
			? Results.NotFound()
			: Results.File(document.Content, document.ContentType);
	}
}
