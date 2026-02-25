using ApexUIBridge.Core;

namespace ApexUIBridge.ViewModels;

public class AboutViewModel : ObservableObject, IDialogViewModel {

    public string Title { get; } = "About ApexUIBridge";
    public string CloseButtonText { get; } = "Ok";
    public string SaveButtonText { get; } = "";
    public bool IsSaveVisible { get; } = false;
    public bool IsCloseVisible { get; } = true;
    public bool CanClose { get; } = true;

    public void Save() {
    }

    public void Close() {
    }
}