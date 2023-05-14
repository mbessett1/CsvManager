using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace Bessett.Csv
{
    /* 
     
     the idea here is to have abstract CSV class that can transform to and from a List 
     of objects, and then be serialized to or from a file (or table)

     */

    internal class CsvDefinition : List<CsvTypedColumn>
    {
        public CsvDefinition()
        {
        }

        public CsvDefinition(string[] headers)
        {
            foreach (var header in headers)
            {
                Add(header, typeof (string));
            }
        }

        public CsvDefinition(Type customType)
        {
            foreach (var propertyInfo in customType.GetProperties())
            {
                Add(propertyInfo.HeaderText(), propertyInfo.PropertyType);
            }
        }

        public void Add(string headerName, string headerText, Type columnType, int sortOrder = 0)
        {
            Add(new CsvTypedColumn
            {
                HeaderName = headerName,
                HeaderText = headerText,
                Property = columnType,
                SortOrder = sortOrder == 0 ? Count : sortOrder
            });
        }

        public void Add(string headerName, Type columnType, int sortOrder = 0)
        {
            Add(headerName, headerName, columnType, sortOrder);
        }
    }

    internal class CsvTypedColumn
    {
        public string HeaderName { get; set; }
        public string HeaderText { get; set; }
        public Type Property { get; set; }
        public int SortOrder { get; set; }
    }

    public static class Csv
    {
        public static void SerializeToFile<T>(IEnumerable<T> source, string filename, bool columnNamesInHeader = true)
            where T : class, new()
        {
            using (var file = new CsvFileWriter<T>(filename, columnNamesInHeader))
            {
                foreach (var value in source)
                {
                    file.WriteRow(value);
                }
            }
        }

        public static IEnumerable<T> DeserializeFromFile<T>(string filename, bool columnNamesInHeader = true)
            where T : class, new()
        {
            var result = new List<T>();

            using (var file = new CsvFileReader<T>(filename, columnNamesInHeader))
            {
                var allValues = file.GetRows();
                result.AddRange(allValues);
            }
            return result;
        }


        /*
                public static string SerializeHeader<T>()
                {
                    return "";
                }

                public static string SerializeRow<T>()
                {
                    return "";
                }

                public static T DeserializeRow<T>(string rowData) where T : new()
                {
                    return new T();
                }

                public static T DeserializeRow<T>(string rowData, string headerData) where T : new()
                {
                    return new T();
                }
                */
    }

    public class CsvParsingException : ApplicationException
    {
        const string DefaultMessage = "CSV Parsing Exception";

        public string LineText { get; private set; }
        public long LineNumber { get; private set; }
        public string Filename { get; private set; }

        public CsvParsingException(string filename, long lineNo, string lineText)
            : this(filename,  lineNo,  lineText,null)
        {}

        public CsvParsingException(string filename, long lineNo, string lineText,  Exception innerException)
            : base(AugmentedMessage(innerException == null ? DefaultMessage: innerException.Message,  filename,  lineNo,  lineText), innerException)
        {
            Filename = filename;
            LineNumber = lineNo;
            LineText = lineText;
        }

        private static string AugmentedMessage(string message, string filename, long lineNo, string lineText)
        {
            return $"CSV parsing exception: {message} in file [{filename}], line {lineNo}\nText:\n{lineText}";
        }
    }

    internal static class Injection
    {
        public static T InjectObject<T>(T destination, Dictionary<string, int> names, string[] values) where T : new()
        {
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;

            var properties = destination.GetType().GetProperties(flags);

            foreach (var property in properties)
            {
                if (!property.CsvIgnore())
                {
                    var headerOrder = property.HeaderOrder();
                    if (headerOrder >= 0 && headerOrder < values.Length)
                    {
                        var value = values[headerOrder];
                        InjectPropertyValue(destination, value, property);
                    }
                    else
                    {
                        var headerText = property.HeaderText();
                        if (names.ContainsKey(headerText))
                        {
                            var value = values[names[headerText]];
                            InjectPropertyValue(destination, value, property);
                        }
                    }
                }
            }

            return destination;
        }
        
        // deprecate
        public static T InjectObjectByHeader<T>(T destination, Dictionary<string, int> names, string[] values)
            where T : new()
        {
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;

            foreach (var columnName in names.Keys)
            {
                var value = values[names[columnName]];
                var p = destination.GetType().GetProperty(columnName, flags);

                if (p != null)
                {
                    InjectPropertyValue(destination, value, p);
                }
            }
            return destination;
        }

        public static Dictionary<string, int> VirtualHeaders(Type target)
        {
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;

            var result = new Dictionary<string, int>();
            var ctr = 0;

            foreach (var prop in target.GetProperties(flags))
            {
                //if (prop.PropertyType.BaseType == typeof(ValueType)
                //    || prop.PropertyType == typeof(string)
                //    || prop.PropertyType.BaseType == typeof(CsvData)
                //    )
                {
                    if (!prop.CsvIgnore())
                    {
                        result.Add(prop.HeaderText(), ctr);
                        ctr++;
                    }
                }
            }

            return result;
        }

        public static List<string> GetProperties(Type target)
        {
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;

            var result = new List<string>();

            foreach (var prop in target.GetProperties(flags))
            {
                if (!prop.CsvIgnore())
                {
                    result.Add(prop.HeaderText());
                }
            }

            return result;
        }

        public static List<string> GetPropertyValues<T>(T target)
        {
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;

            var result = new List<string>();

            foreach (var prop in target.GetType().GetProperties(flags))
            {
                if (!prop.CsvIgnore())
                {
                    var value = prop.GetValue(target, null);

                    result.Add(value != null ? value.ToString() : "");
                }
            }

            return result;
        }

        public static T InjectObject<T>(T destination, Dictionary<string, string> nameValuePairs) where T : new()
        {
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;

            foreach (var key in nameValuePairs.Keys)
            {
                var p = destination.GetType().GetProperty(key, flags);

                if (p != null)
                {
                    InjectPropertyValue(destination, nameValuePairs[key], p);
                }
            }
            return destination;
        }

        public static void InjectPropertyValue<T>(T destination, string value, PropertyInfo p) where T : new()
        {
            object propValue;

            try
            {
                TypeConverter typeConverter = TypeDescriptor.GetConverter(typeof(string));

                if (p.PropertyType.BaseType == typeof (ValueType) ||
                    p.PropertyType.BaseType == typeof (string))
                {
                    typeConverter = TypeDescriptor.GetConverter(p.PropertyType);
                }
                else if (p.PropertyType.BaseType == typeof (CsvData))
                {
                    typeConverter = new CsvConverter<CsvData>();
                }
                else if (p.PropertyType.BaseType != null)
                {
                    typeConverter = TypeDescriptor.GetConverter(p.PropertyType.BaseType.Name);

                }

                if (typeConverter.CanConvertFrom(typeof (string)))
                {
                    propValue = typeConverter.ConvertFrom(value);
                }
                else
                {
                    //propValue = typeConverter.ConvertFromString(value);
                    throw new Exception("typeConverter conversion problem");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return;
            }

            try
            {
                if (p.PropertyType.BaseType == typeof (ValueType)
                    || p.PropertyType == typeof (string)
                    )
                {
                    p.SetValue(destination, propValue, null);
                }
                else
                //if (p.PropertyType.BaseType == typeof(CsvData))
                {
                    if (value.Length > 0)
                    {
                        try
                        {
                            // this will fail if the user did not specify a constructor 
                            // with a string
                            var csv = Activator.CreateInstance(p.PropertyType, value);

                            // potential base class solution::
                            //((CsvData)csv).ConvertFromString(value);

                            p.SetValue(destination, csv, null);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var message = string.Format("{0}: {1}", p.Name, ex.Message);
                throw new Exception(message);
            }
        }
    }


    internal class CsvConverter<T> : TypeConverter where T : CsvData, new()
    {
        //        public CsvConverter() : base() { }

        // Overrides the CanConvertFrom method of TypeConverter.
        // The ITypeDescriptorContext interface provides the context for the
        // conversion. Typically, this interface is used at design time to 
        // provide information about the design-time container.
        public override bool CanConvertFrom(ITypeDescriptorContext context,
            Type sourceType)
        {
            if (sourceType == typeof (string))
            {
                return true;
            }
            return base.CanConvertFrom(context, sourceType);
        }


        // Overrides the ConvertFrom method of TypeConverter.
        public override object ConvertFrom(ITypeDescriptorContext context,
            CultureInfo culture, object value)
        {
            if (value is string)
            {
                var result = new T();
                //result.ConvertFromString((string) value);
                return result;
            }
            return base.ConvertFrom(context, culture, value);
        }

        // Overrides the ConvertTo method of TypeConverter.
        public override object ConvertTo(ITypeDescriptorContext context,
            CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof (string))
            {
                return ((T) value).ConvertToString();
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    // base csv class (not sure one this one)
    public class CsvData
    {
        [CsvIgnore]
        public string RawData { get; set; }

        public virtual string ConvertToString()
        {
            return ToString();
        }

        public virtual void ConvertFromString(string value)
        {
        }
    }
}