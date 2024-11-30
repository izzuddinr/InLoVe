using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Qatalyst.Objects;

namespace Qatalyst.Services
{
    public class ConfigService
    {
        public Dictionary<string, string> CapkDictionary { get; private set; }
        public Dictionary<string, string> QDictionary { get; private set; }
        public ISO8583Filter Iso8583Filter { get; private set; }

        public ConfigService()
        {
            CapkDictionary = LoadCapkDictionary("CAPK.json");
            Iso8583Filter = LoadIso8583Filter("ISO8583FILTER.json");
            QDictionary = LoadQDictionary("RANDOMQ.json");
        }

        private static Dictionary<string, string> LoadCapkDictionary(string fileName)
        {
            try
            {
                var jsonString = LoadJsonFileToString(fileName);
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString) ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading CAPK dictionary from {fileName}: {ex.Message}");
                return new Dictionary<string, string>();
            }
        }

        private static ISO8583Filter LoadIso8583Filter(string fileName)
        {
            try
            {
                var jsonString = LoadJsonFileToString(fileName);
                return JsonConvert.DeserializeObject<ISO8583Filter>(jsonString) ?? new ISO8583Filter();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading ISO8583 filter from {fileName}: {ex.Message}");
                return new ISO8583Filter();
            }
        }

        private static Dictionary<string, string> LoadQDictionary(string fileName)
        {
            try
            {
                var jsonString = LoadJsonFileToString(fileName);
                var quotes = JsonConvert.DeserializeObject<List<RandomQ>>(jsonString) ?? new List<RandomQ>();

                var dictionary = new Dictionary<string, string>();
                foreach (var quote in quotes)
                {
                    // Use the quote text as the key and the author as the value
                    dictionary[quote.Text] = quote.From;
                }

                return dictionary;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading Q dictionary from {fileName}: {ex.Message}");
                return new Dictionary<string, string>();
            }
        }


        private static string LoadJsonFileToString(string fileName)
        {
            try
            {
                return File.ReadAllText(fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading file {fileName}: {ex.Message}");
                return string.Empty;
            }
        }
    }
}
