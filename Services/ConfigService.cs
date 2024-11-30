using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Qatalyst.Objects;

namespace Qatalyst.Services
{
    public class ConfigService
    {
        public List<CapkLabel> CapkLabels { get; private set; }
        public List<RandomQ> RandomQs { get; private set; }

        public List<AppColor> AppColors { get; private set; }

        public ISO8583Filter Iso8583Filter { get; private set; }

        public ConfigService()
        {
            CapkLabels = LoadCapkDictionary("CAPK.json");
            Iso8583Filter = LoadIso8583Filter("ISO8583FILTER.json");
            RandomQs = LoadQDictionary("RANDOMQ.json");
        }

        private static List<CapkLabel> LoadCapkDictionary(string fileName)
        {
            try
            {
                var jsonString = LoadJsonFileToString(fileName);
                var parsedJson = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString) ?? [];

                return parsedJson.Select(kvp => new CapkLabel(kvp.Key, kvp.Value)).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading CAPK dictionary from {fileName}: {ex.Message}");
                return new List<CapkLabel>();
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

        private static List<RandomQ> LoadQDictionary(string fileName)
        {
            try
            {
                var jsonString = LoadJsonFileToString(fileName);
                return JsonConvert.DeserializeObject<List<RandomQ>>(jsonString) ?? new List<RandomQ>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading Q dictionary from {fileName}: {ex.Message}");
                return new List<RandomQ>();
            }
        }
        private static List<AppColor> LoadAppColors(string fileName)
        {
            try
            {
                var jsonString = LoadJsonFileToString(fileName);
                return JsonConvert.DeserializeObject<List<AppColor>>(jsonString) ?? [];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading Q dictionary from {fileName}: {ex.Message}");
                return new List<AppColor>();
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
