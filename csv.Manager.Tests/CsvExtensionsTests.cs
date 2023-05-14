using System.Collections.Generic;
using System.Linq;
using Bessett.Csv;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace csv.Manager.Tests
{
    [TestClass]
    public class CsvExtensionsTests
    {
        [TestMethod]
        public void SerializeToTypedCsvFileTest()
        {
            var filename = "testTyped.csv";

            var check1 = new List<Person>();
            check1.Add(new Person {Age = 1, Name = "Darth", Information = "plan not found"});
            check1.Add(new Person {Age = 2, Name = "Luke", Information = "Nooooooo"});
            check1.Add(new Person {Age = 3, Name = "Greedo", Information = "oota Goota, Solo"});

            check1.SerializeToCsvFile(filename);

            var test1 = Csv.DeserializeFromFile<Person>(filename);

            Assert.AreSame(check1.ToString(), test1.ToString());
        }

        [TestMethod]
        public void SerializeToCsvFileTest()
        {
            var filename = "test.csv";

            var checkHeaders1 = new[] {"Age", "Name", "Info"};
            List<string[]> result;

            var check1 = new List<string[]>
            {
                new[] {"1", "Darth", "plan not found"},
                new[] {"2", "Luke", "Nooooooo"},
                new[] {"3", "Greedo", "oota Goota, Solo"}
            };

            using (var csvFile = new CsvFileWriter(filename, checkHeaders1))
            {
                foreach (var row in check1)
                {
                    csvFile.WriteRow(row);
                }
            }

            using (var csvFile = new CsvFileReader(filename, true))
            {
                result = csvFile.GetRows().ToList();
            }

            for (var i = 0; i > check1.Count; i++)
            {
                Assert.AreSame(string.Join(",", result[i]), string.Join(",", check1[i]));
            }
        }

        private class Person
        {
            public string Name { get; set; }

            public int Age { get; set; }

            public string Information { get; set; }

            public override string ToString()
            {
                return $"{Name} - {Age} - {Information}";
            }
        }
    }
}