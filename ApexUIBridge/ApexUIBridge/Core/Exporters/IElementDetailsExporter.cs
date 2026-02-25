using ApexUIBridge.Models;

namespace ApexUIBridge.Core.Exporters;

public interface IElementDetailsExporter {

    string Export(IEnumerable<ElementPatternItem> automationElement);
}