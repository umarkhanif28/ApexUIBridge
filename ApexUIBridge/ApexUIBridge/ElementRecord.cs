using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApexUIBridge
{
    public sealed record ElementRecord
    {
        public int Id { get; init; }
        public int HashedId { get; init; }
        public int? ParentId { get; init; }
        public IReadOnlyList<int> ChildIds { get; init; } = Array.Empty<int>();

        // Element identity
        public string Name { get; init; } = string.Empty;
        public string AutomationId { get; init; } = string.Empty;
        public ControlType ControlType { get; init; }
        public string ClassName { get; init; } = string.Empty;
        public string FrameworkId { get; init; } = string.Empty;

        // Location and state
        public Rectangle BoundingRectangle { get; init; }
        public bool IsOffScreen { get; init; }
        public bool IsEnabled { get; init; } = true;
        public IntPtr WindowHandle { get; init; }
        public int ProcessId { get; init; }

        // For re-finding this element
        public ElementFindCriteria FindCriteria { get; init; } = ElementFindCriteria.Empty;

        // Tracking
        public DateTime LastSeen { get; init; } = DateTime.UtcNow;
        public string Hash { get; init; } = string.Empty;
        public string HelpText { get; set; } = string.Empty;
        // The live reference (kept as a property for JIT acquisition)
        public AutomationElement? AutomationElement { get; init; }


        /// <summary>
        /// Creates an ElementRecord from a FlaUI AutomationElement.
        /// </summary>
        public static ElementRecord FromAutomationElement(
            AutomationElement element,
            int id,
            int? parentId,
            string hash,
            ElementFindCriteria findCriteria,
            IntPtr windowHandleOverride = default)
        {
            var props = element.Properties;
            var boundingRect = Rectangle.Empty;

            try
            {
                if (props.BoundingRectangle.IsSupported)
                {
                    var rect = props.BoundingRectangle.Value;
                    boundingRect = new Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
                }
            }
            catch { /* Element may be stale */ }

            IntPtr windowHandle = windowHandleOverride != IntPtr.Zero
                ? windowHandleOverride
                : SafeGetProperty(() => props.NativeWindowHandle.ValueOrDefault, IntPtr.Zero);

            return new ElementRecord
            {
                Id = id,
                ParentId = parentId,
                AutomationElement = element, // Fixed: Assigning the live reference
                Name = SafeGetProperty(() => props.Name.ValueOrDefault, string.Empty) ?? string.Empty,
                AutomationId = SafeGetProperty(() => props.AutomationId.ValueOrDefault, ""),
                ControlType = SafeGetProperty(() => props.ControlType, ControlType.Custom),
                ClassName = SafeGetProperty(() => props.ClassName.ValueOrDefault, ""),
                FrameworkId = SafeGetProperty(() => props.FrameworkId.ValueOrDefault, ""),
                HelpText = SafeGetProperty(() => props.HelpText.ValueOrDefault, ""),
                BoundingRectangle = boundingRect,
                IsOffScreen = SafeGetProperty(() => props.IsOffscreen.ValueOrDefault, false),
                IsEnabled = SafeGetProperty(() => props.IsEnabled.ValueOrDefault, true),
                WindowHandle = windowHandle,
                ProcessId = SafeGetProperty(() => props.ProcessId.ValueOrDefault, 0),
                FindCriteria = findCriteria,
                Hash = hash,
                LastSeen = DateTime.UtcNow
            };
        }

        // NOTE: You can now remove 'WithChildIds' and 'WithLastSeen'. 
        // Instead of: record.WithLastSeen(DateTime.Now);
        // Use: record with { LastSeen = DateTime.Now }; 
        // This is safer and preserves the AutomationElement reference.

        private static T SafeGetProperty<T>(Func<T> getter, T defaultValue)
        {
            try { return getter(); }
            catch { return defaultValue; }
        }

        public override string ToString() =>
            $"ElementRecord[{Id}] {ControlType} '{Name}' ({AutomationId})";


        /// <summary>
        /// Creates a copy with updated child IDs.
        /// Uses 'with' expression to preserve all properties including AutomationElement.
        /// </summary>
        public ElementRecord WithChildIds(IReadOnlyList<int> childIds)
        {
            return this with { ChildIds = childIds };
        }

        /// <summary>
        /// Creates a copy with updated LastSeen timestamp.
        /// Uses 'with' expression to preserve all properties including AutomationElement.
        /// </summary>
        public ElementRecord WithLastSeen(DateTime lastSeen)
        {
            return this with { LastSeen = lastSeen };
        }

        public bool Equals(ElementRecord? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id && Hash == other.Hash;
        }

        public override int GetHashCode() => HashCode.Combine(Id, Hash);

        //public override string ToString() =>
        //    $"ElementRecord[{Id}] {ControlType} '{Name}' ({AutomationId})";
    }




    public sealed class ElementFindCriteria
    {
        public static readonly ElementFindCriteria Empty = new();

        public IntPtr WindowHandle { get; init; }
        public string? AutomationId { get; init; }
        public string? Name { get; init; }
        public ControlType? ControlType { get; init; }
        public string? ClassName { get; init; }
        public IReadOnlyList<string> PathFromRoot { get; init; } = Array.Empty<string>();
        public int SiblingIndex { get; init; } = -1;

        /// <summary>
        /// Creates find criteria from a FlaUI AutomationElement.
        /// </summary>
        public static ElementFindCriteria FromAutomationElement(
            FlaUI.Core.AutomationElements.AutomationElement element,
            IReadOnlyList<string>? pathFromRoot = null,
            int siblingIndex = -1,
            IntPtr windowHandleOverride = default)
        {
            var props = element.Properties;

            // Use override if provided, otherwise get from element
            IntPtr windowHandle = windowHandleOverride;
            if (windowHandle == IntPtr.Zero)
            {
                try { windowHandle = props.NativeWindowHandle.ValueOrDefault; } catch { }
            }

            return new ElementFindCriteria
            {
                WindowHandle = windowHandle,
                AutomationId = SafeGetProperty(() => props.AutomationId),
                Name = SafeGetProperty(() => props.Name),
                ControlType = SafeGetProperty(() => props.ControlType),
                ClassName = SafeGetProperty(() => props.ClassName),
                PathFromRoot = pathFromRoot ?? Array.Empty<string>(),
                SiblingIndex = siblingIndex
            };
        }

        /// <summary>
        /// Builds a FlaUI condition for searching.
        /// Uses the most reliable criteria available.
        /// </summary>
        public PropertyCondition? ToCondition(ConditionFactory cf)
        {
            // Priority 1: AutomationId (most reliable)
            if (!string.IsNullOrEmpty(AutomationId))
            {
                return cf.ByAutomationId(AutomationId);
            }

            // Priority 2: Name + ControlType
            if (!string.IsNullOrEmpty(Name) && ControlType.HasValue)
            {
                // Return just name condition - caller should combine with ControlType
                return cf.ByName(Name);
            }

            // Priority 3: ClassName + ControlType
            if (!string.IsNullOrEmpty(ClassName) && ControlType.HasValue)
            {
                return cf.ByClassName(ClassName);
            }

            // Priority 4: Just Name
            if (!string.IsNullOrEmpty(Name))
            {
                return cf.ByName(Name);
            }

            return null;
        }

        /// <summary>
        /// Validates that an element matches this criteria.
        /// </summary>
        public bool Matches(FlaUI.Core.AutomationElements.AutomationElement element)
        {
            var props = element.Properties;

            // Must match AutomationId if we have one
            if (!string.IsNullOrEmpty(AutomationId))
            {
                var elementAutomationId = SafeGetProperty(() => props.AutomationId);
                if (elementAutomationId != AutomationId)
                    return false;
            }

            // Must match ControlType if we have one
            if (ControlType.HasValue)
            {
                var elementControlType = SafeGetPropertyValue(() => props.ControlType.ValueOrDefault);
                if (elementControlType != ControlType.Value)
                    return false;
            }

            // Name is a soft match (can change, e.g., page titles)
            // ClassName is also soft match

            return true;
        }

        /// <summary>
        /// Gets a Description of this criteria for debugging.
        /// </summary>
        public string GetDescription()
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(AutomationId))
                parts.Add($"AutomationId='{AutomationId}'");
            if (!string.IsNullOrEmpty(Name))
                parts.Add($"Name='{Name}'");
            if (ControlType.HasValue)
                parts.Add($"ControlType={ControlType.Value}");
            if (!string.IsNullOrEmpty(ClassName))
                parts.Add($"ClassName='{ClassName}'");
            if (SiblingIndex >= 0)
                parts.Add($"SiblingIndex={SiblingIndex}");

            return parts.Count > 0 ? string.Join(", ", parts) : "(empty criteria)";
        }

        private static T? SafeGetProperty<T>(Func<T> getter) where T : class
        {
            try
            {
                return getter();
            }
            catch
            {
                return default;
            }
        }

        private static T? SafeGetPropertyValue<T>(Func<T> getter) where T : struct
        {
            try
            {
                return getter();
            }
            catch
            {
                return default;
            }
        }

        public override string ToString() => GetDescription();
    }







}
