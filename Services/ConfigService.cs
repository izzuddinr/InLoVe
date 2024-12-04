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

        public Dictionary<string, List<string>> HostRecordTags { get; set; }

        public ISO8583Filter Iso8583Filter { get; private set; }

        public ConfigService()
        {
            CapkLabels = LoadCapkDictionary("capk.json");
            Iso8583Filter = LoadIso8583Filter("iso8583filter.json");
            RandomQs = LoadQDictionary("RANDOMQ.json");
            HostRecordTags = LoadHostRecordTags("tags.json");
        }

        private static List<CapkLabel> LoadCapkDictionary(string fileName)
        {
            try
            {
                var jsonString = LoadJsonFileToString(fileName);
                var parsedJson = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString) ?? [];
                var parsedCapkLabels = parsedJson.Select(kvp => new CapkLabel(kvp.Key, kvp.Value)).ToList();
                foreach (var capkLabel in parsedCapkLabels)
                {
                    Console.WriteLine($"CapkLabel: {capkLabel.Key} | {capkLabel.Label}");
                }

                return parsedCapkLabels;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading CAPK dictionary from {fileName}: {ex.Message}");
                return [];
            }
        }

        private static Dictionary<string, List<string>> LoadHostRecordTags(string fileName)
        {
            try
            {
                var jsonString = LoadJsonFileToString(fileName);
                var parsedJson = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(jsonString) ?? [];

                return parsedJson;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading Host Record Tags dictionary from {fileName}: {ex.Message}");
                return new Dictionary<string, List<string>>();
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
                return JsonConvert.DeserializeObject<List<RandomQ>>(jsonString) ?? [];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading Q dictionary from {fileName}: {ex.Message}");
                return [];
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
                return [];
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
