using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace InLoVe.Services;

public class ConfigService
{
    public Dictionary<string, string> CapkDictionary { get; private set; }

    public ConfigService()
    {
        LoadCapkData();
    }

    private void LoadCapkData()
    {
        var appDirectory = AppContext.BaseDirectory;
        var filePath = Path.Combine(appDirectory, "CAPK.json");
        var json = File.ReadAllText(filePath);
        CapkDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
    }
}