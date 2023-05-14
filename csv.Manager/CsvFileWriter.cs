using System;
using System.IO;

namespace Bessett.Csv
{
    public class CsvFileWriter<T> : CsvFileWriter
    {
        public CsvFileWriter(string filename, bool columnNamesInHeader = true) : base(filename)
        {
            if (columnNamesInHeader)
            {
                WriteHeader();
            }
        }

        void WriteHeader()
        {
            var headers = Injection.GetProperties(typeof(T)).ToArray();

            base.WriteRow(headers);
        }

        public string WriteRow(T typedValues)
        {
            var values = Injection.GetPropertyValues(typedValues).ToArray();
            base.WriteRow(values);
            return values.ToCsvString();
        }
    }

    public class CsvFileWriter : IDisposable
    {
        private CsvDefinition definition;
        private int columnCount = 0;

        public string Filename { get; private set; }
        private StreamWriter textWriter;
        public long RowCount { get; private set; }

        private CsvFileWriter()
        {
            definition = new CsvDefinition();
        }
        public CsvFileWriter(string filename):this()
        {
            Filename = filename;
            textWriter = new StreamWriter(filename);
        }
        public CsvFileWriter(string filename, string[] headers) : this(filename)
        {
            definition = new CsvDefinition(headers);
            WriteRow(headers);
        }

        public long WriteRow(string[] values)
        {
            if (RowCount == 0)
            {
                columnCount = values.Length;
            }

            if (columnCount == values.Length)
            {
                textWriter.WriteLine(values.ToCsvString());
                RowCount++;
                return RowCount;

            }

            throw new InvalidOperationException(
                $"Expecting {columnCount} columns, {values.Length} columns specified.");
        }

        public void Dispose()
        {
            textWriter.Close();
            textWriter.Dispose();
        }

    }
}