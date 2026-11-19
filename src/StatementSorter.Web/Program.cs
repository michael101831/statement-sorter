using Microsoft.AspNetCore.Http.Features;
using StatementSorter.Core;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 2 * 1024 * 1024;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 2 * 1024 * 1024;
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        if (app.Environment.IsDevelopment())
            context.Context.Response.Headers.CacheControl = "no-cache";
    },
});

app.MapGet("/samples/messy-statement.csv", () => Results.Text(SampleStatement.Csv, "text/csv"));
app.MapGet("/samples/earlier-statement.csv", () => Results.Text(SampleStatement.EarlierCsv, "text/csv"));

app.MapPost("/api/sort", async (HttpRequest request) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "Upload a CSV file." });

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file is null || file.Length == 0)
        return Results.BadRequest(new { error = "Choose a CSV file first." });

    if (file.Length > 2 * 1024 * 1024)
        return Results.BadRequest(new { error = "That file is larger than 2 MB." });

    string text;
    using (var reader = new StreamReader(file.OpenReadStream()))
        text = await reader.ReadToEndAsync();

    try
    {
        return Results.Ok(StatementProcessor.Process(text));
    }
    catch (StatementFormatException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Run();
