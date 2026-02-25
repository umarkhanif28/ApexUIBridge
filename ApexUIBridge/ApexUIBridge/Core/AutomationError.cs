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
    public class AutomationResult<T>
    {
        /// <summary>
        /// Indicates whether the automation operation completed successfully
        /// </summary>
        public bool IsSuccess { get; private set; }

        /// <summary>
        /// The data payload returned by successful operations
        /// </summary>
        public T Data { get; private set; }

        /// <summary>
        /// Human-readable message describing the operation outcome
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// Detailed error information for diagnostic purposes
        /// </summary>
        public AutomationError Error { get; private set; }

        /// <summary>
        /// Timestamp when the operation completed
        /// </summary>
        public DateTime Timestamp { get; private set; }

        /// <summary>
        /// Duration of the operation execution
        /// </summary>
        public TimeSpan Duration { get; private set; }


        public double ExecutionTimeMs { get; private set; } //<< Not being used
        public int? ElementId { get; private set; } //<< Not being used

        /// <summary>
        /// The original command that was executed (for compatibility with AICommandResponse)
        /// </summary>
        public string Command { get; private set; }


        public AutomationResult(bool success, T data, string message, AutomationError error, TimeSpan duration, string command = null, int? elementId = null)
        {
            IsSuccess = success;
            Data = data;
            Message = message ?? string.Empty;
            Error = error;
            Timestamp = DateTime.UtcNow;
            Duration = duration;
            Command = command ?? string.Empty;
            ElementId = elementId;
        }

        /// <summary>
        /// Creates a successful result with data payload
        /// </summary>
        /// <param name="data">The data returned by the successful operation</param>
        /// <param name="message">Optional success message</param>
        /// <param name="duration">Time taken to complete the operation</param>
        /// <returns>A successful AutomationResult</returns>
        public static AutomationResult<T> CreateSuccess(T data, string message = null, TimeSpan duration = default)
        {
            return new AutomationResult<T>(true, data, message ?? "Operation completed successfully", null, duration);
        }

        /// <summary>
        /// Creates a failed result with error information
        /// </summary>
        /// <param name="errorType">The category of error that occurred</param>
        /// <param name="message">Human-readable error Description</param>
        /// <param name="exception">The underlying exception if applicable</param>
        /// <param name="elementId">The ID of the element involved in the failure</param>
        /// <param name="duration">Time taken before the operation failed</param>
        /// <returns>A failed AutomationResult</returns>
        public static AutomationResult<T> CreateFailure(AutomationErrorType errorType, string message,
            Exception exception = null, int? elementId = null, TimeSpan duration = default)
        {
            var error = new AutomationError(errorType, message, exception, elementId);
            return new AutomationResult<T>(false, default(T), message, error, duration);
        }

        /// <summary>
        /// Creates a failed result from an existing AutomationError
        /// </summary>
        /// <param name="error">The error information</param>
        /// <param name="duration">Time taken before the operation failed</param>
        /// <returns>A failed AutomationResult</returns>
        public static AutomationResult<T> CreateFailure(AutomationError error, TimeSpan duration = default)
        {
            return new AutomationResult<T>(false, default(T), error.Message, error, duration);
        }

        /// <summary>
        /// Transforms the data type of a successful result while preserving metadata
        /// </summary>
        /// <typeparam name="TNew">The new data type</typeparam>
        /// <param name="transform">Function to transform the data</param>
        /// <returns>A new AutomationResult with transformed data</returns>
        public AutomationResult<TNew> Transform<TNew>(Func<T, TNew> transform) //<< Not being used
        {
            if (!IsSuccess)
            {
                return AutomationResult<TNew>.CreateFailure(Error, Duration);
            }

            try
            {
                var newData = transform(Data);
                return AutomationResult<TNew>.CreateSuccess(newData, Message, Duration);
            }
            catch (Exception ex)
            {
                return AutomationResult<TNew>.CreateFailure(
                    AutomationErrorType.DataTransformation,
                    $"Failed to transform result data: {ex.Message}",
                    ex,
                    duration: Duration);
            }
        }

        /// <summary>
        /// Implicit conversion from OperationResult&lt;T&gt; to AutomationResult&lt;T&gt;
        /// </summary>
        public static implicit operator AutomationResult<T>(OperationResult<T> operationResult)
        {
            if (operationResult.IsSuccess)
            {
                return new AutomationResult<T>(true, operationResult.Value, "Operation completed successfully", null, default, null, operationResult.ElementId);
            }
            else
            {
                var errorType = operationResult.ResultType switch
                {
                    OperationResultType.NotFound => AutomationErrorType.ElementNotFound,
                    OperationResultType.Failed => AutomationErrorType.OperationFailed,
                    _ => AutomationErrorType.Unknown
                };
                var error = new AutomationError(errorType, operationResult.ErrorMessage, null, operationResult.ElementId);
                return new AutomationResult<T>(false, default, operationResult.ErrorMessage, error, default, null, operationResult.ElementId);
            }
        }

        /// <summary>
        /// Returns a string representation suitable for logging
        /// </summary>
        public override string ToString() //<< Not being used
        {
            var status = IsSuccess ? "SUCCESS" : "FAILURE";
            var result = $"[{status}] {Message}";

            if (!IsSuccess && Error != null)
            {
                result += $" (Error: {Error.ErrorType})";
            }

            if (Duration > TimeSpan.Zero)
            {
                result += $" [Duration: {Duration.TotalMilliseconds:F1}ms]";
            }

            return result;
        }
    }

    /// <summary>
    /// Specialized AutomationResult for operations that return no data
    /// </summary>
    public class AutomationResult : AutomationResult<object>
    {
        private AutomationResult(bool success, string message, AutomationError error, TimeSpan duration)
            : base(success, null, message, error, duration)
        {
        }

        /// <summary>
        /// Creates a successful result without data payload
        /// </summary>
        /// <param name="message">Optional success message</param>
        /// <param name="duration">Time taken to complete the operation</param>
        /// <returns>A successful AutomationResult</returns>
        public static new AutomationResult CreateSuccess(string message = null, TimeSpan duration = default)
        {
            return new AutomationResult(true, message ?? "Operation completed successfully", null, duration);
        }

        /// <summary>
        /// Creates a failed result with error information
        /// </summary>
        /// <param name="errorType">The category of error that occurred</param>
        /// <param name="message">Human-readable error Description</param>
        /// <param name="exception">The underlying exception if applicable</param>
        /// <param name="elementId">The ID of the element involved in the failure</param>
        /// <param name="duration">Time taken before the operation failed</param>
        /// <returns>A failed AutomationResult</returns>
        public static new AutomationResult CreateFailure(AutomationErrorType errorType, string message,
            Exception exception = null, int? elementId = null, TimeSpan duration = default)
        {
            var error = new AutomationError(errorType, message, exception, elementId);
            return new AutomationResult(false, message, error, duration);
        }

        /// <summary>
        /// Creates a failed result from an existing AutomationError
        /// </summary>
        /// <param name="error">The error information</param>
        /// <param name="duration">Time taken before the operation failed</param>
        /// <returns>A failed AutomationResult</returns>
        public static new AutomationResult CreateFailure(AutomationError error, TimeSpan duration = default)
        {
            return new AutomationResult(false, error.Message, error, duration);
        }
    }

    /// <summary>
    /// Detailed error information for automation operation failures
    /// </summary>
    public class AutomationError
    {


        /// <summary>
        /// The category of error that occurred
        /// </summary>
        public AutomationErrorType ErrorType { get; }

        /// <summary>
        /// Human-readable error Description
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// The underlying exception that caused the failure, if applicable
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// The ID of the UI element involved in the failure, if applicable
        /// </summary>
        public int? ElementId { get; }

        /// <summary>
        /// Additional diagnostic information
        /// </summary>
        public Dictionary<string, object> DiagnosticData { get; }

        /// <summary>
        /// Timestamp when the error occurred
        /// </summary>
        public DateTime Timestamp { get; }

        public AutomationError(AutomationErrorType errorType, string message, Exception exception = null, int? elementId = null)
        {
            ErrorType = errorType;
            Message = message ?? string.Empty;
            Exception = exception;
            ElementId = elementId;
            DiagnosticData = new Dictionary<string, object>();
            Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Adds diagnostic information to help with troubleshooting
        /// </summary>
        /// <param name="key">The diagnostic data key</param>
        /// <param name="value">The diagnostic data value</param>
        public void AddDiagnosticData(string key, object value)
        {
            DiagnosticData[key] = value;
        }

        /// <summary>
        /// Returns a detailed string representation of the error
        /// </summary>
        public override string ToString()
        {
            var result = $"[{ErrorType}] {Message}";

            if (ElementId.HasValue)
            {
                result += $" (Element ID: {ElementId.Value})";
            }

            if (Exception != null)
            {
                result += $" - {Exception.GetType().Name}: {Exception.Message}";
            }

            return result;
        }
    }

    /// <summary>
    /// Categorizes the types of errors that can occur during automation operations
    /// </summary>
    public enum AutomationErrorType
    {
        /// <summary>
        /// The target UI element could not be found
        /// </summary>
        ElementNotFound,

        /// <summary>
        /// The UI element was found but is not currently accessible
        /// </summary>
        ElementNotAccessible,

        /// <summary>
        /// The requested operation is not supported by the target element
        /// </summary>
        OperationNotSupported,

        /// <summary>
        /// The operation timed out before completion
        /// </summary>
        Timeout,

        /// <summary>
        /// The target application or window is not available
        /// </summary>
        ApplicationNotAvailable,

        /// <summary>
        /// The UI element reference became stale and needs to be re-acquired
        /// </summary>
        StaleElementReference,

        /// <summary>
        /// Failed to validate the current state before or after an operation
        /// </summary>
        StateValidationFailure,

        /// <summary>
        /// Error occurred during data transformation or processing
        /// </summary>
        DataTransformation,

        /// <summary>
        /// Network or communication related failure
        /// </summary>
        CommunicationFailure,

        /// <summary>
        /// Security or permissions related failure
        /// </summary>
        SecurityFailure,

        /// <summary>
        /// Unexpected system or framework error
        /// </summary>
        SystemError,

        /// <summary>
        /// Unknown or unclassified error
        /// </summary>
        Unknown,

        /// <summary>
        /// The operation failed to complete successfully
        /// </summary>
        OperationFailed,

        /// <summary>
        /// One or more parameters provided to the operation are invalid
        /// </summary>
        InvalidParameter,

        /// <summary>
        /// The user cancelled the operation     
        /// </summary>
        UserCancelled,
        InvalidArgument,
    }

    /// <summary>
    /// Utility class for measuring operation duration and creating timed results
    /// </summary>
    public class AutomationTimer : IDisposable
    {
        private readonly System.Diagnostics.Stopwatch _stopwatch;
        private bool _disposed;

        public AutomationTimer()
        {
            _stopwatch = System.Diagnostics.Stopwatch.StartNew();
        }

        /// <summary>
        /// Gets the elapsed time since the timer was created
        /// </summary>
        public TimeSpan Elapsed => _stopwatch.Elapsed;

        /// <summary>
        /// Creates a successful result with the elapsed time
        /// </summary>
        /// <typeparam name="T">The type of data to return</typeparam>
        /// <param name="data">The success data</param>
        /// <param name="message">Optional success message</param>
        /// <returns>A successful AutomationResult with timing information</returns>
        public AutomationResult<T> CreateSuccess<T>(T data, string message = null)
        {
            return AutomationResult<T>.CreateSuccess(data, message, Elapsed);
        }

        /// <summary>
        /// Creates a successful result without data payload
        /// </summary>
        /// <param name="message">Optional success message</param>
        /// <returns>A successful AutomationResult with timing information</returns>
        public AutomationResult CreateSuccess(string message = null)
        {
            return AutomationResult.CreateSuccess(message, Elapsed);
        }
        /// <summary>
        /// Creates a successful result without data payload
        /// </summary>
        /// <param name="message">Optional success message</param>
        /// <returns>A successful AutomationResult with timing information</returns>
        public AutomationResult CreateSuccess(string message = null, FlaUI.Core.Definitions.ToggleState newState = default)
        {
            return AutomationResult.CreateSuccess(message, Elapsed);
        }

        /// <summary>
        /// Creates a failed result with the elapsed time
        /// </summary>
        /// <typeparam name="T">The expected return type</typeparam>
        /// <param name="errorType">The category of error</param>
        /// <param name="message">Error Description</param>
        /// <param name="exception">The underlying exception</param>
        /// <param name="elementId">The element ID involved in the failure</param>
        /// <returns>A failed AutomationResult with timing information</returns>
        public AutomationResult<T> CreateFailure<T>(AutomationErrorType errorType, string message,
            Exception exception = null, int? elementId = null)
        {
            return AutomationResult<T>.CreateFailure(errorType, message, exception, elementId, Elapsed);
        }

        /// <summary>
        /// Creates a failed result without data payload
        /// </summary>
        /// <param name="errorType">The category of error</param>
        /// <param name="message">Error Description</param>
        /// <param name="exception">The underlying exception</param>
        /// <param name="elementId">The element ID involved in the failure</param>
        /// <returns>A failed AutomationResult with timing information</returns>
        public AutomationResult CreateFailure(AutomationErrorType errorType, string message,
            Exception exception = null, int? elementId = null)
        {
            return AutomationResult.CreateFailure(errorType, message, exception, elementId, Elapsed);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _stopwatch?.Stop();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Extension methods for working with AutomationResult objects
    /// </summary>
    public static class AutomationResultExtensions
    {
        /// <summary>
        /// Executes an action if the result is successful
        /// </summary>
        /// <typeparam name="T">The result data type</typeparam>
        /// <param name="result">The automation result</param>
        /// <param name="onSuccess">Action to execute with the success data</param>
        /// <returns>The original result for chaining</returns>
        public static AutomationResult<T> OnSuccess<T>(this AutomationResult<T> result, Action<T> onSuccess)
        {
            if (result.IsSuccess)
            {
                onSuccess?.Invoke(result.Data);
            }
            return result;
        }

        /// <summary>
        /// Executes an action if the result failed
        /// </summary>
        /// <typeparam name="T">The result data type</typeparam>
        /// <param name="result">The automation result</param>
        /// <param name="onFailure">Action to execute with the error information</param>
        /// <returns>The original result for chaining</returns>
        public static AutomationResult<T> OnFailure<T>(this AutomationResult<T> result, Action<AutomationError> onFailure)
        {
            if (!result.IsSuccess)
            {
                onFailure?.Invoke(result.Error);
            }
            return result;
        }

        /// <summary>
        /// Combines multiple automation results into a single result
        /// </summary>
        /// <param name="results">The results to combine</param>
        /// <returns>A successful result if all inputs succeeded, otherwise the first failure</returns>
        public static AutomationResult Combine(params AutomationResult[] results)
        {
            if (results == null || results.Length == 0)
            {
                return AutomationResult.CreateSuccess("No operations to combine");
            }

            var failures = results.Where(r => !r.IsSuccess).ToList();
            if (failures.Any())
            {
                var firstFailure = failures.First();
                return AutomationResult.CreateFailure(firstFailure.Error);
            }

            var totalDuration = TimeSpan.FromTicks(results.Sum(r => r.Duration.Ticks));
            return AutomationResult.CreateSuccess($"Combined {results.Length} successful operations", totalDuration);
        }
    }

}
