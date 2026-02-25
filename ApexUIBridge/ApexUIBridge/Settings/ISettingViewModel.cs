using ApexUIBridge.Core;

namespace ApexUIBridge.Settings;

public interface ISettingViewModel {
    Editable<FlaUiAppSettings> Settings { get; }
}