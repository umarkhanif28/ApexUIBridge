using FlaUI.Core.AutomationElements;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApexUIBridge.Core
{
    /// <summary>
    /// Represents the outcome of an automation operation with standardized success indication,
    /// error reporting, and optional data payload.
    /// </summary>
    /// <typeparam name="T">The type of data returned on successful operations</typeparam>
    /// <summary>
    /// Result of an element operation.
    /// </summary>
    public class OperationResult
    {
        /// <summary>
        /// Indicates whether the automation operation completed successfully
        /// </summary>
        public bool IsSuccess { get; private set; }


        /// <summary>
        /// Human-readable message describing the operation outcome
        /// </summary>
        public string Message { get; private set; } //<< Not being used


        /// <summary>
        /// Detailed error information for diagnostic purposes
        /// </summary>
        public AutomationElement AutomationElement { get; private set; }

        /// <summary>
        /// Detailed error information for diagnostic purposes
        /// </summary>
        public AutomationError Error { get; private set; } //<< Not being used

        /// <summary>
        /// Timestamp when the operation completed
        /// </summary>
        public DateTime Timestamp { get; private set; } //<< Not being used

        /// <summary>
        /// Duration of the operation execution
        /// </summary>
        public TimeSpan Duration { get; private set; } //<< Not being used

        public int ElementId { get; }
        public string? ErrorMessage { get; }
        public OperationResultType ResultType { get; }

        protected OperationResult(int elementId, AutomationElement? automationElement, bool isSuccess, string? errorMessage, OperationResultType resultType)
        {
            ElementId = elementId;
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            ResultType = resultType;
            AutomationElement = automationElement;
        }

        //public static OperationResult Success(int elementId) =>
        //    new(elementId,null, true, null, OperationResultType.Success);

        public static OperationResult Success(int elementId, AutomationElement? automationElement = null) =>
            new(elementId, automationElement, true, null, OperationResultType.Success);

        public static OperationResult NotFound(int elementId) =>
            new(elementId, null, false, "Element not found", OperationResultType.NotFound);

        public static OperationResult Failed(int elementId, string errorMessage) =>
            new(elementId, null, false, errorMessage, OperationResultType.Failed);
    }

    /// <summary>
    /// Result of an element operation with a value.
    /// </summary>
    public class OperationResult<T> : OperationResult
    {
        public T? Value { get; }

        private OperationResult(int elementId, AutomationElement? automationElement, bool isSuccess, string? errorMessage, OperationResultType resultType, T? value)
            : base(elementId, automationElement, isSuccess, errorMessage, resultType)
        {
            Value = value;
        }

        public static OperationResult<T> Success(int elementId, T value) =>
            new(elementId, null, true, null, OperationResultType.Success, value);

        public static OperationResult<T> Success(int elementId, AutomationElement automationElement, T value) =>
            new(elementId, automationElement, true, null, OperationResultType.Success, value);

        public new static OperationResult<T> NotFound(int elementId) =>
            new(elementId, null, false, "Element not found", OperationResultType.NotFound, default);

        public new static OperationResult<T> Failed(int elementId, string errorMessage) =>
            new(elementId, null, false, errorMessage, OperationResultType.Failed, default);
    }

    /// <summary>
    /// Type of operation result.
    /// </summary>
    public enum OperationResultType
    {
        Success,
        NotFound,
        Failed
    }

}
