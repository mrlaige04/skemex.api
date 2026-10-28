using System.Text.Json.Serialization;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Domain.Enums;

namespace Skemex.Web.Models.Ai;

public sealed class ExecuteAiRequest
{
    /// <summary>When set, runs that tool directly (e.g. task_decomposition). When null, chat + function calling.</summary>
    public string? ToolName { get; set; }

    public Guid? ProjectId { get; set; }
    public Guid? ChatId { get; set; }

    public string UserInput { get; set; } = string.Empty;
    public string? ArgumentsJson { get; set; }
    public string? Model { get; set; }

    /// <summary>Linked project artefacts referenced via <c>{artefact:N}</c> in <see cref="UserInput"/>.</summary>
    public IReadOnlyList<AiChatArtefactRef>? Artefacts { get; set; }

    /// <summary>Origin of the call: Chat, Ui, Api. Inferred when omitted.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AiExecutionSource? Source { get; set; }
}
