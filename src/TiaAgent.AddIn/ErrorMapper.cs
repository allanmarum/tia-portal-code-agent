using TiaAgent.Contracts.Errors;

namespace TiaAgent.AddIn;

/// <summary>
/// Maps TiaErrorCode values to user-facing messages for the TIA Portal Add-In UI.
/// Technical details are preserved in logs; only clear messages reach the user.
/// </summary>
public static class ErrorMapper
{
    public static string ToUserMessage(TiaError error)
    {
        return error.Code switch
        {
            TiaErrorCode.OPENCODE_UNAVAILABLE =>
                "The AI assistant is not available. Please ensure OpenCode is running on the configured address.",

            TiaErrorCode.OPENCODE_TASK_FAILED =>
                "The AI assistant encountered an error while processing your request. Please try again.",

            TiaErrorCode.TIA_NOT_CONNECTED =>
                "Not connected to TIA Portal. Please ensure TIA Portal is open and the project is loaded.",

            TiaErrorCode.TIA_PROJECT_NOT_OPEN =>
                "No TIA Portal project is open. Please open a project before using the AI assistant.",

            TiaErrorCode.TIA_SESSION_EXPIRED =>
                "The TIA Portal session has expired. Please reconnect.",

            TiaErrorCode.TIA_SELECTION_EXPIRED =>
                "The selection has expired. Please re-select the object and try again.",

            TiaErrorCode.TIA_OBJECT_NOT_FOUND =>
                $"The selected object was not found: {error.Message}",

            TiaErrorCode.TIA_OBJECT_CHANGED =>
                "The object has changed since it was selected. Please re-select and try again.",

            TiaErrorCode.TIA_OPERATION_NOT_SUPPORTED =>
                $"This operation is not supported: {error.Message}",

            TiaErrorCode.TIA_TIMEOUT =>
                "The operation timed out. Please try again.",

            TiaErrorCode.TIA_CANCELLED =>
                "The operation was cancelled.",

            TiaErrorCode.TIA_BUSY =>
                "TIA Portal is busy. Please wait for the current operation to complete.",

            TiaErrorCode.MCP_AUTHENTICATION_FAILED =>
                "MCP server authentication failed. Please check the configuration.",

            TiaErrorCode.MCP_RATE_LIMITED =>
                "Too many requests. Please wait a moment and try again.",

            TiaErrorCode.INVALID_REQUEST =>
                "Invalid request. Please try again.",

            TiaErrorCode.INTERNAL_ERROR =>
                "An unexpected error occurred. Please check the logs for details.",

            _ => $"Error ({error.Code}): {error.Message}"
        };
    }
}
