using System.IO;
using System.Text.Json;

namespace ApexUIBridge.Settings;

/// <summary>
/// Generic JSON settings service. Serialises/deserialises a strongly typed settings
/// object to/from a JSON file on disk. Missing files produce a default
/// <typeparamref name="T"/> instance; serialisation uses indented JSON and
/// case-insensitive property matching for forward compatibility.
///
/// <para>Used in two places in the app:</para>
/// <list type="bullet">
///   <item><see cref="FlaUiAppSettings"/> — loaded at startup from
///       <c>appsettings.json</c> next to the executable.</item>
///   <item><see cref="Models.AiSettings"/> — persisted to
///       <c>%AppData%\ApexUIBridge\ai-settings.json</c> by
///       <see cref="Forms.StartupForm"/>.</item>
/// </list>
/// </summary>
public sealed class JsonSettingsService<T> : ISettingsService<T> where T : class, new() {
    private readonly string _filePath;
    private readonly JsonSerializerOptions _options;

    public JsonSettingsService(string filePath) {
        _filePath = filePath;

        _options = new JsonSerializerOptions {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }

    public T Load() {
        if (!File.Exists(_filePath))
            return new T();

        string json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<T>(json, _options) ?? new T();
    }

    public void Save(T settings) {
        string json = JsonSerializer.Serialize(settings, _options);
        File.WriteAllText(_filePath, json);
    }
}