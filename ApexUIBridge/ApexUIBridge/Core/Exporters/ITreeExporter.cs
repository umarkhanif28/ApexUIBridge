using ApexUIBridge.ViewModels;

namespace ApexUIBridge.Core.Exporters;

public interface ITreeExporter {
    string Export(ElementViewModel element);
}