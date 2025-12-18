namespace PasswordManager.Shared.Common.Result;

/// <summary>
/// Represents error information for a failed operation.
/// </summary>
public class ResultError
{
    public string Message { get; }
    public string? Code { get; }
    public Dictionary<string, object>? Metadata { get; }

    public ResultError(string message, string? code = null, Dictionary<string, object>? metadata = null)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Code = code;
        Metadata = metadata;
    }

    public ResultError AddMetadata(string key, object value)
    {
        var newMetadata = Metadata != null
            ? new Dictionary<string, object>(Metadata)
            : new Dictionary<string, object>();
        newMetadata[key] = value;
        return new ResultError(Message, Code, newMetadata);
    }

    public override string ToString() => Code != null 
        ? $"[{Code}] {Message}" 
        : Message;
}

