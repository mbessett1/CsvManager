using System.Collections.Generic;
using System.Linq;

namespace Bessett.Csv
{
    public static class CsvExtensions
    {
        public static void SerializeToCsvFile<T>(this IEnumerable<T> source, string filename, bool columnNamesInHeader = true) where T : class, new()
        {
            Csv.SerializeToFile(source, filename, columnNamesInHeader);
        }

        internal static string ToCsvString(this string[] values)
        {
            var cleanValues = values.Select(item => item.Replace("\"", "\"\"")).ToList();

            var result = $"\"{string.Join("\",\"", cleanValues)}\"";

            return result;
        }

    }
}