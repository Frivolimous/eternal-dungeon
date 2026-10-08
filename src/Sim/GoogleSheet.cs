using System.Net.Http;
using System.Text;
using System.Text.Json;
using EternalDungeon.Core.Data;

/// <summary>
/// The content Google Sheet, reached through its Apps Script web app (tools/sheets-sync.gs). The sheet's address and
/// the web app's URL are in tools/google-sheet.json.
/// </summary>
sealed class GoogleSheet(string webApp)
{
    /// <summary>The VERSION tools/sheets-sync.gs must have.</summary>
    const int ScriptVersion = 1;

    public const string ConfigFile = "tools/google-sheet.json";

    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(3) };

    static readonly JsonSerializerOptions OpOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault,
    };

    public static GoogleSheet FromConfig(string root)
    {
        var path = Path.Combine(root, ConfigFile);
        using var config = JsonDocument.Parse(File.ReadAllText(path));
        var url = config.RootElement.TryGetProperty("webApp", out var w) ? w.GetString() : null;
        if (string.IsNullOrWhiteSpace(url))
            throw new SheetException($"No web app URL in {ConfigFile} yet. Set up the sheet's script first: the steps are at the top of tools/sheets-sync.gs.");
        return new GoogleSheet(url);
    }

    /// <summary>Every tab of the sheet, with its cells as values (not as displayed).</summary>
    public List<SheetTab> Read()
    {
        var reply = Answer(Send(() => Http.GetAsync(webApp)));
        return [.. reply.GetProperty("tabs").EnumerateArray().Select(tab => new SheetTab(
            tab.GetProperty("name").GetString()!,
            [.. tab.GetProperty("cells").EnumerateArray().Select(row => (IReadOnlyList<object?>)[.. row.EnumerateArray().Select(Cell)])]))];
    }

    /// <summary>Applies the edits, in order.</summary>
    public void Apply(IReadOnlyList<SheetOp> ops)
    {
        var body = JsonSerializer.Serialize(new { ops }, OpOptions);
        Answer(Send(() => Http.PostAsync(webApp, new StringContent(body, Encoding.UTF8, "text/plain"))));
    }

    static object? Cell(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Number => e.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => e.GetString(),
        _ => null,
    };

    static string Send(Func<Task<HttpResponseMessage>> request)
    {
        try
        {
            using var response = request().GetAwaiter().GetResult();
            return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            throw new SheetException($"Couldn't reach the sheet's web app: {e.Message}");
        }
    }

    static JsonElement Answer(string text)
    {
        JsonElement reply;
        try { reply = JsonDocument.Parse(text).RootElement; }
        catch (JsonException)
        {
            throw new SheetException("The web app didn't answer with data. Check its deployment: Execute as Me, Who has access Anyone, "
                + $"and that {ConfigFile} has the URL ending in /exec.");
        }
        var version = reply.TryGetProperty("version", out var v) ? v.GetInt32() : 0;
        if (version != ScriptVersion)
            throw new SheetException($"The sheet's script is version {version}, this sim needs version {ScriptVersion}: paste tools/sheets-sync.gs "
                + "into the sheet's Apps Script again and deploy a new version (steps at the top of the file).");
        if (reply.TryGetProperty("error", out var error))
            throw new SheetException($"The sheet's script failed: {error.GetString()}");
        return reply;
    }
}

sealed class SheetException(string message) : Exception(message);
