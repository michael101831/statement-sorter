using System.Runtime.InteropServices.JavaScript;
using System.Text.Json.Serialization;
using StatementSorter.Core;

[assembly: System.Runtime.Versioning.SupportedOSPlatform("browser")]

namespace StatementSorter.Wasm;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SortResult))]
[JsonSerializable(typeof(SortError))]
public partial class SorterJsonContext : JsonSerializerContext;

public sealed record SortError(string Error);

public static partial class SorterExports
{
    [JSExport]
    public static string SortCsv(string csv)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Serialize(StatementProcessor.Process(csv), SorterJsonContext.Default.SortResult);
        }
        catch (StatementFormatException ex)
        {
            return System.Text.Json.JsonSerializer.Serialize(new SortError(ex.Message), SorterJsonContext.Default.SortError);
        }
    }

    [JSExport]
    public static string SampleCsv(string name)
    {
        if (name == SampleStatement.FileName)
            return SampleStatement.Csv;
        if (name == SampleStatement.EarlierFileName)
            return SampleStatement.EarlierCsv;
        return "";
    }
}
