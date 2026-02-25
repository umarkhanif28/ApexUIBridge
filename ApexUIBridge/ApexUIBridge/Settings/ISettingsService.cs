namespace ApexUIBridge.Settings;

public interface ISettingsService<T> where T : class, new() {
    T Load();
    void Save(T settings);
}