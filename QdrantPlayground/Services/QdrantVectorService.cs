using Qdrant.Client;
using Qdrant.Client.Grpc;
using static Qdrant.Client.Grpc.Conditions;
using QdrantPlayground.Models;

namespace QdrantPlayground.Services;

public sealed class QdrantVectorService
{
    private readonly QdrantClient _client;
    private readonly string _collectionName;
    private readonly uint _vectorSize;

    public QdrantVectorService(
        QdrantClient client,
        string collectionName,
        uint vectorSize)
    {
        _client = client;
        _collectionName = collectionName;
        _vectorSize = vectorSize;
    }

    // 1. Create Collection and Payload Indexes
    public async Task EnsureCollectionAsync()
    {
        var exists = await _client.CollectionExistsAsync(_collectionName);
        if (exists)
        {
            return;
        }

        await _client.CreateCollectionAsync(
            _collectionName,
            new VectorParams
            {
                Size = _vectorSize,
                Distance = Distance.Cosine
            });

        // Create Payload Index for fast multi-tenant filtering
        await _client.CreatePayloadIndexAsync(
            _collectionName,
            fieldName: "tenantId",
            schemaType: PayloadSchemaType.Keyword);

        await _client.CreatePayloadIndexAsync(
            _collectionName,
            fieldName: "documentId",
            schemaType: PayloadSchemaType.Keyword);
    }

    // 2. Convert application model to Qdrant Point
    private static PointStruct ToPoint(VectorDocument document)
    {
        return new PointStruct
        {
            Id = document.Id,
            Vectors = document.Embedding,
            Payload =
            {
                ["tenantId"] = document.TenantId,
                ["documentId"] = document.DocumentId,
                ["chunkIndex"] = document.ChunkIndex,
                ["content"] = document.Content
            }
        };
    }

    // 3. Insert or Update Points
    public async Task UpsertInBatchesAsync(IAsyncEnumerable<VectorDocument> documents, int batchSize = 250, CancellationToken cancellationToken = default)
    {
        var batch = new List<PointStruct>(batchSize);

        await foreach (var doc in documents.WithCancellation(cancellationToken))
        {
            batch.Add(ToPoint(doc));

            if (batch.Count >= batchSize)
            {
                await _client.UpsertAsync(_collectionName, batch, cancellationToken: cancellationToken);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await _client.UpsertAsync(_collectionName, batch, cancellationToken: cancellationToken);
            batch.Clear();
        }
    }

    public async Task UpsertInBatchesAsync(
        IEnumerable<VectorDocument> documents,
        int batchSize = 250,
        CancellationToken cancellationToken = default)
    {
        foreach (var chunk in documents.Chunk(batchSize))
        {
            var points = chunk.Select(ToPoint).ToList();
            await _client.UpsertAsync(_collectionName, points, cancellationToken: cancellationToken);
        }
    }

    // 4. Search by Vector
    public async Task<IReadOnlyList<ScoredPoint>> SearchAsync(
        float[] queryVector,
        int topK = 5)
    {
        return await _client.QueryAsync(
            _collectionName,
            query: queryVector,
            limit: (ulong)topK);
    }

    // 5. Search with Tenant Filter
    public async Task<IReadOnlyList<ScoredPoint>> SearchByTenantAsync(
        float[] queryVector,
        string tenantId,
        int topK = 5)
    {
        return await _client.QueryAsync(
            _collectionName,
            query: queryVector,
            filter: MatchKeyword("tenantId", tenantId),
            limit: (ulong)topK);
    }

    // 6. Delete a Point
    public async Task DeleteAsync(ulong pointId)
    {
        await _client.DeleteAsync(
            _collectionName,
            ids: new[] { pointId });
    }
}