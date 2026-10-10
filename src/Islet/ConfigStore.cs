using System;
using System.IO;
using System.Text.Json;
using Islet.Core;

namespace Islet;

/// <summary>Loads/saves <see cref="IsletConfig"/> from %APPDATA%/Islet/config.json.</summary>
internal static class ConfigStore
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Islet");
    public static string FilePath => Path.Combine(Dir, "config.json");

    public static IsletConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<IsletConfig>(File.ReadAllText(FilePath), Opts) ?? new IsletConfig();
        }
        catch { /* fall through to defaults */ }
        return new IsletConfig();
    }

    public static void Save(IsletConfig cfg)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(cfg, Opts));
        }
        catch { }
    }
}
