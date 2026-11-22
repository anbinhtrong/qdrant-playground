using Qdrant.Client;
using QdrantPlayground.Models;
using QdrantPlayground.Services;

const string host = "localhost";
const int grpcPort = 6334;
const string collectionName = "knowledge_chunks";
const uint vectorSize = 4;

using var client = new QdrantClient(host, grpcPort);

var vectorService = new QdrantVectorService(
    client,
    collectionName,
    vectorSize);

await vectorService.EnsureCollectionAsync();

// Giả lập stream dữ liệu từ nguồn (SQL Server / File / Chunker) thay vì nạp tất cả vào RAM
static async IAsyncEnumerable<VectorDocument> GetDocumentStreamAsync()
{
    yield return new(
        1, "tenant-001", "doc-001", 0,
        "Umbraco can be hosted on IIS.",
        [0.9f, 0.1f, 0.1f, 0.1f]);

    await Task.Yield(); // Mô phỏng I/O async (đọc từ DB / disk theo từng record)

    yield return new(
        2, "tenant-001", "doc-001", 1,
        "Umbraco supports SQL Server.",
        [0.8f, 0.2f, 0.1f, 0.1f]);

    yield return new(
        3, "tenant-002", "doc-002", 0,
        "Python is widely used for Machine Learning.",
        [0.1f, 0.9f, 0.1f, 0.1f]);
}

await vectorService.UpsertInBatchesAsync(GetDocumentStreamAsync(), batchSize: 250);

float[] queryVector = [0.95f, 0.1f, 0.1f, 0.1f];

Console.WriteLine("=== VECTOR SEARCH ===");
var results = await vectorService.SearchAsync(queryVector, topK: 3);
foreach (var result in results)
{
    Console.WriteLine($"ID: {result.Id}, Score: {result.Score:F4}");
    Console.WriteLine(result.Payload["content"].StringValue);
}

Console.WriteLine("\n=== TENANT FILTER ===");
var tenantResults = await vectorService.SearchByTenantAsync(
    queryVector,
    "tenant-001",
    topK: 5);

foreach (var result in tenantResults)
{
    Console.WriteLine(result.Payload["content"].StringValue);
}