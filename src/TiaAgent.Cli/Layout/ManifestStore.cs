using System;
using System.IO;
using System.Text.Json;

namespace TiaAgent.Cli.Layout;

/// <summary>
/// Atomic JSON manifest persistence store.
/// Ensures atomic file updates via temporary files and safe error handling on corruption.
/// </summary>
public static class ManifestStore
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static T Read<T>(string filePath) where T : class, new()
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            return new T();
        }

        try
        {
            var json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidDataException($"Manifest file '{filePath}' is empty.");
            }

            var result = JsonSerializer.Deserialize<T>(json, s_jsonOptions);
            if (result == null)
            {
                throw new InvalidDataException($"Manifest file '{filePath}' deserialized to null.");
            }

            return result;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Manifest file '{filePath}' contains malformed JSON: {ex.Message}", ex);
        }
    }

    public static void WriteAtomic<T>(string filePath, T data)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = filePath + ".tmp." + Guid.NewGuid().ToString("N");

        try
        {
            var json = JsonSerializer.Serialize(data, s_jsonOptions);
            File.WriteAllText(tempPath, json);

            File.Move(tempPath, filePath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
            throw;
        }
    }
}
