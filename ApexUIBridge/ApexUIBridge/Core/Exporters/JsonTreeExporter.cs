using System.Text.Json;
using System.Text.Json.Nodes;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Identifiers;
using ApexUIBridge.ViewModels;

namespace ApexUIBridge.Core.Exporters;

public class JsonTreeExporter(AutomationBase automation, bool enableXPath) : ITreeExporter {

    private readonly PatternItemsFactory _patternItemsFactory = new(automation);

    public string Export(ElementViewModel element) {
        JsonObject root = ExportElement(element);
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private JsonObject ExportElement(ElementViewModel element) {
        JsonObject obj = new() {
            ["elementId"] = element.ElementId,
            ["name"] = element.Name,
            ["controlType"] = element.ControlType.ToString()
        };

        if (enableXPath) {
            obj["xpath"] = element.XPath;
        }

        // Add all details (identification, details, patterns)
        if (element.AutomationElement != null) {
            try {
                var bounds = element.AutomationElement.Properties.BoundingRectangle.Value;
                obj["boundingRectangle"] = new JsonObject {
                    ["x"] = (int)bounds.X,
                    ["y"] = (int)bounds.Y,
                    ["width"] = (int)bounds.Width,
                    ["height"] = (int)bounds.Height
                };
            } catch {
                // Element may not have a bounding rectangle
            }

            try {
                HashSet<PatternId> supportedPatterns = [.. element.AutomationElement.GetSupportedPatterns()];
                IDictionary<string, PatternItem[]> patternItems = _patternItemsFactory.CreatePatternItemsForElement(element.AutomationElement, supportedPatterns);

                JsonObject details = new();
                foreach ((string sectionName, PatternItem[] items) in patternItems) {
                    JsonObject section = new();
                    foreach (PatternItem item in items) {
                        section[item.Key] = item.Value;
                    }
                    details[sectionName] = section;
                }
                obj["details"] = details;
            } catch {
                // Element may have become unavailable
            }
        }


        // Recursively add children
        JsonArray children = [];
        try {
            foreach (ElementViewModel child in element.LoadChildren()) {
                try {
                    children.Add(ExportElement(child));
                } catch {
                    // ignored
                }
            }
        } catch {
            // ignored
        }
        obj["children"] = children;

        return obj;
    }
}
