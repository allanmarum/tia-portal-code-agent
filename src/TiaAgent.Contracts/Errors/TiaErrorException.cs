using System;

namespace TiaAgent.Contracts.Errors;

/// <summary>
/// Exception wrapper for TiaError, allowing it to be thrown and caught.
/// </summary>
public class TiaErrorException : Exception
{
    public TiaError Error { get; }

    public TiaErrorException(TiaError error)
        : base(error.Message, error.InternalException)
    {
        Error = error;
    }

    public TiaErrorException(TiaErrorCode code, string message, string? correlationId = null)
        : base(message)
    {
        Error = new TiaError
        {
            Code = code,
            Message = message,
            CorrelationId = correlationId
        };
    }
}
