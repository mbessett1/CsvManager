using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Bessett.Csv
{
    public class CsvHeader : Attribute
    {
        public CsvHeader(string text)
        {
            Text = text;
            Order = -1;
        }
        public string Text { get; set; }
        internal int Order { get; set; }
    }

    public class CsvIgnore : Attribute
    {
    }

    internal static class TypeAttributeExtensions
    {
        public static bool CsvIgnore(this PropertyInfo member)
        {
            var result = member.GetCustomAttributes(true).Any(a => a is CsvIgnore);
            return result;
        }

        public static bool HasCsvHeader(this PropertyInfo member)
        {
            var result = member.GetCustomAttributes(true).Any(a => a is CsvHeader);
            return result;
        }

        public static string HeaderText(this PropertyInfo member)
        {
            var result = member.GetCustomAttributes(true).FirstOrDefault(a => a is CsvHeader);

            if (result != null)
            {
                var header = (CsvHeader)result;
                if (!string.IsNullOrEmpty(header.Text))
                {
                    return header.Text;
                }
                return member.Name;
            }

            return member.Name;
        }

        public static Dictionary<string, int> HeaderNames(this Type target)
        {
            const BindingFlags flags = //BindingFlags.DeclaredOnly |
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;

            var result = new Dictionary<string, int>();
            var properties = target.GetType().GetProperties(flags);
            var ctr = 0;
            foreach (var property in properties)
            {
                if (!property.CsvIgnore())
                {
                    result.Add(property.HeaderText(), ctr);
                    ctr++;
                }
            }
            return result;
        }

        public static int HeaderOrder(this PropertyInfo member)
        {
            var result = member.GetCustomAttributes(true).FirstOrDefault(a => a is CsvHeader);

            if (result != null)
            {
                var header = (CsvHeader)result;
                return header.Order;
            }

            return -1;
        }
    }
}
