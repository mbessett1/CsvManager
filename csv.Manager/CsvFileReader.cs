using System;
using System.Collections.Generic;
using System.IO;

namespace Bessett.Csv
{
    public enum CsvParser
    {
        Rfc4180,
        Simple
    }
    public class CsvFileReader : IDisposable
    {
        public CsvParser ParsingStrategy {get;}

        private StreamReader textReader;
        public Dictionary<string, int> ColumnNames { get; protected set; }
    
        protected string[] _currentRow;

        public string Filename { get; private set; }

        public bool Eof
        {
            get { return textReader.EndOfStream; }
        }

        public long Line { get; private set; }

        public CsvFileReader(string filename, bool columnNamesInHeader, CsvParser parser = CsvParser.Rfc4180)
        {
            ParsingStrategy = parser;
            try
            {
                Filename = filename;
                textReader = new StreamReader(filename);
                ColumnNames = new Dictionary<string, int>();
                Line = 0;

                if (columnNamesInHeader)
                {
                    var fields = NextRow();
                    for (var count = 0; count < fields.Length; count++)
                    {
                        ColumnNames.Add(fields[count], count);
                    }
                }
            }
            catch (Exception ex)
            {
                throw NewCsvParsingException("Cannot initilize CSV parser", ex);
            }
        }

        #region Column functions deprecate or move to dedicated row class

        public string ColumnValue(int columnId)
        {
            return _currentRow.Length > columnId
                ? _currentRow[columnId]
                : null;
        }

        public string ColumnValue(string columnName)
        {
            return ColumnNames.ContainsKey(columnName)
                ? _currentRow[ColumnNames[columnName]]
                : null;
        }

        public T ColumnValue<T>(string columnName)
        {
            try
            {
                var result = (T)Convert.ChangeType(_currentRow[ColumnNames[columnName]], typeof(T));
                return result;
            }
            catch (Exception ex)
            {
                string message =
                    string.Format("Parsing exception handling column {0}, line:{1} in file {2}. Line parsed: [{3}]",
                        columnName,
                        Line,
                        Filename,
                        _currentRow
                        );
                throw NewCsvParsingException(message, ex);
            }
        }
        #endregion

        public string[] CurrentRow()
        {
            return _currentRow;
        }

        public string[] NextRow(int bufferSize = 512)
        {
            if (!textReader.EndOfStream)
            {
                bool isIncomplete;
                var buffer = "";
                string[] rowResult;

                do
                {
                    buffer += textReader.ReadLine();
                    Line++;
                    try
                    {
                        rowResult = ParseLine(buffer, out isIncomplete, bufferSize);
                    }
                    catch (Exception ex)
                    {
                        throw NewCsvParsingException(buffer, ex);
                    }
                } while (isIncomplete);

                _currentRow = rowResult;
                return _currentRow;
            }
            throw new EndOfStreamException();
        }

        public IEnumerable<string[]> GetRows()
        {
            do
            {
                yield return NextRow();

            } while (!Eof);
        }

        CsvParsingException NewCsvParsingException(string lineText)
        {
            return new CsvParsingException(Filename, Line, lineText);
        }

        CsvParsingException NewCsvParsingException(string lineText, Exception innerException)
        {
            return new CsvParsingException(Filename, Line, lineText, innerException);
        }

        string[] ParseLine(string buffer, out bool isIncomplete, int maxRowLength)
        {
            switch (ParsingStrategy)
            {
                case CsvParser.Simple:
                    return ParseLineBasic(buffer, out isIncomplete, maxRowLength);
                case CsvParser.Rfc4180:
                default:
                    return ParseLineRFC(buffer, out isIncomplete, maxRowLength);
            }
        }
        string[] ParseLineRFC(string buffer, out bool isIncomplete, int maxRowLength)
        {
            const char fieldDelimiter = ',';
            const char quoteDelimiter = '"';
            bool insideQuote = false;
            var lineContents = new List<string>();
            string currItem = string.Empty;
            char previousChar = ' ';
            bool encodingError = false;
            int charId = 0;

            if (buffer.Length > maxRowLength)
            {
                throw new Exception($"Row length ({buffer.Length}) exceeded maximum configured ({maxRowLength}), possible encoding problem. [{buffer.Substring(0, maxRowLength > 80 ? 80 : maxRowLength)}]");
            }

            foreach (var character in buffer)
            {
                charId++;

                switch (character)
                {
                    case quoteDelimiter:
                        if (!insideQuote && previousChar == quoteDelimiter)
                        {
                            currItem += character;
                        }
                        insideQuote = !insideQuote;
                        break;

                    case fieldDelimiter:
                        if (!insideQuote)
                        {
                            lineContents.Add(currItem);
                            currItem = "";
                        }
                        else
                        {
                            currItem += character;
                        }
                        break;

                    default:
                        if (!insideQuote && previousChar == quoteDelimiter)
                        {
                            encodingError = true;
                        }
                        currItem += character;
                        break;
                }
                previousChar = character;
                if (encodingError)
                {
                    throw new Exception($"CSV parsing/encoding error at column {charId} in {buffer}");
                }
            }

            isIncomplete = insideQuote;

            if (!insideQuote)
            {
                lineContents.Add(currItem);
                return lineContents.ToArray();
            }

            return null;
        }


        string[] ParseLineBasic(string buffer, out bool isIncomplete, int maxRowLength = 512)
        {
            const char fieldDelimiter = ',';
            const char quoteDelimiter = '"';
            bool insideQuote = false;
            var lineContents = new List<string>();
            string currItem = string.Empty;
            char previousChar = ' ';
            bool encodingError = false;
            int charId = 0;

            if (buffer.Length > maxRowLength)
            {
                throw new Exception($"Row length ({buffer.Length}) exceeded maximum configured ({maxRowLength}), possible encoding problem. [{buffer.Substring(0, maxRowLength > 80 ? 80 : maxRowLength)}]");
            }

            isIncomplete = false;
            string[] delimiters = new string[] { "\",\"" };

            var result = buffer.Substring(1,buffer.Length-2).Split(delimiters, StringSplitOptions.None);
            return result;

        }

        public void Dispose()
        {
            textReader.Dispose();
        }
    }

    public class CsvReadError
    {
        public long LineNumber { get; set; }
        public string ErrorMessage { get; set; }
        public string Text { get; set; }
    }
    public class CsvFileReader<T> : CsvFileReader where T : class, new()
    {
        public List<CsvReadError> Errors { get; set; } = new List<CsvReadError>();

        public CsvFileReader(string filename, bool columnNamesInHeader = true, CsvParser parser = CsvParser.Rfc4180)
            : base(filename, columnNamesInHeader, parser)
        {
            if (!columnNamesInHeader)
                ColumnNames = Injection.VirtualHeaders(typeof(T));
        }

        public new  IEnumerable<T> GetRows() //where T : new()
        {
            do
            {
                bool parsingError = false;

                var rowData = default(T);
                try
                {
                    rowData = NextRow();
                    
                }
                catch (Exception ex)
                {
                    parsingError = true;
                    Errors.Add( new CsvReadError()
                    {
                        LineNumber = Line,
                        ErrorMessage = ex.Message,
                        Text = ""
                    });
                }

                if (!parsingError)
                {
                    yield return rowData;
                }

            } while (!Eof);
        }

        public new T NextRow() //where T : new()
        {
            var rowResult = base.NextRow();
            var result = new T();

            return Injection.InjectObject<T>(result, ColumnNames, _currentRow);
        }

    }
}