namespace QdrantPlayground.Models;

public sealed record VectorDocument(
    ulong Id,
    string TenantId,
    string DocumentId,
    int ChunkIndex,
    string Content,
    float[] Embedding
);