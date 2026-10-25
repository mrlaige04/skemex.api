namespace Skemex.Application.Services.Ai.Usage;

/// <summary>Canonical <c>OperationName</c> values stored on usage activities.</summary>
public static class AiUsageOperationNames
{
    public const string Stage1Routing = "stage1_routing";
    public const string TaskDecomposition = "task_decomposition";
    public const string ProjectQa = "project_qa";
    public const string ChunkEnrichment = "chunk_enrichment";
    public const string DocumentEmbedding = "document_embedding";
    public const string RagQueryEmbedding = "rag_query_embedding";
    public const string ToolExecution = "tool_execution";
    public const string UnspecifiedCompletion = "completion";
    public const string UnspecifiedFunctionCall = "function_call";
    public const string UnspecifiedEmbedding = "embedding";
}
