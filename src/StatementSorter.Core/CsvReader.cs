using System.Text;

namespace StatementSorter.Core;

public static class CsvReader
{
    public static IReadOnlyList<CsvRecord> Read(string text, char delimiter = ',')
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length > 0 && text[0] == '\uFEFF')
            text = text[1..];

        var records = new List<CsvRecord>();
        var field = new StringBuilder();
        var fields = new List<string>();
        var inQuotes = false;
        var line = 1;
        var recordLine = 1;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    if (c == '\n')
                        line++;
                    if (c != '\r')
                        field.Append(c);
                }

                continue;
            }

            if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == delimiter)
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else if (c == '\n' || (c == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n')))
            {
                fields.Add(field.ToString());
                field.Clear();
                records.Add(new CsvRecord(recordLine, fields.ToArray()));
                fields.Clear();
                line++;
                recordLine = line;
            }
            else if (c != '\r')
            {
                field.Append(c);
            }
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            records.Add(new CsvRecord(recordLine, fields.ToArray()));
        }

        return records;
    }
}
