namespace Skemex.Domain.Enums;

/// <summary>Where an AI execution was initiated from.</summary>
public enum AiExecutionSource
{
    /// <summary>Chat panel message (function-calling / tool routing).</summary>
    Chat = 0,

    /// <summary>Direct UI action (tool button, decompose, etc.).</summary>
    Ui = 1,

    /// <summary>Generic API execute without chat context.</summary>
    Api = 2,

    /// <summary>Document vectorization / enrichment background job.</summary>
    Documents = 3,
}
