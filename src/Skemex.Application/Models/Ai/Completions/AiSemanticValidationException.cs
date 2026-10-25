namespace Skemex.Application.Models.Ai.Completions;

/// <summary>
/// Thrown when the model returned HTTP-success content that cannot be parsed or fails domain validation.
/// Consumed by the semantic resilience pipeline to trigger a fresh model invocation.
/// </summary>
public sealed class AiSemanticValidationException : Exception
{
    public AiSemanticValidationException(string reason)
        : base(reason)
    {
        Reason = reason;
    }

    public string Reason { get; }
}
