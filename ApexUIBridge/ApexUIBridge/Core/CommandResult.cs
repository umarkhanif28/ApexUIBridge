using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApexUIBridge.Core
{
    public class CommandResult
    {
        /// <summary>
        /// Whether the command executed successfully
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Human-readable message about the result
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// The original command that was executed
        /// </summary>
        public string Command { get; set; } = string.Empty;

        public int? ElementId { get; set; }


        public string? ElementName { get; set; }


        /// <summary>
        /// Optional data returned from the command (JSON or text)
        /// </summary>
        public string? Data { get; set; }


        public static CommandResult Failed(string command, string message)
        {
            return new CommandResult
            {
                IsSuccess = false,
                Message = message,
                Command = command
            };
        }

        public override string ToString()
        {
            return Message;
        }
    }

}
