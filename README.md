# QdrantPlayground

A .NET 10 playground demonstrating integration with the [Qdrant](https://qdrant.tech/) vector database using the official gRPC .NET client (`Qdrant.Client`).

This project showcases common patterns for building AI-powered vector search, Retrieval-Augmented Generation (RAG) backends, and multi-tenant document indexing systems.

---

## Features

- **Collection & Index Initialization:**
  - Automatically verifies and creates collections configured with Cosine distance metric.
  - Creates keyword payload indexes for high-performance filtering (e.g., `tenantId`, `documentId`).
- **Memory-Efficient Batch Upserting:**
  - Streams vector points using `IAsyncEnumerable<VectorDocument>` directly from the data source (database, disk, or chunker) to avoid high RAM consumption.
  - Supports configurable batch sizes (`batchSize`, default 250) to optimize gRPC throughput.
  - Overload available for standard in-memory collections (`IEnumerable<VectorDocument>`) using LINQ `Chunk()`.
- **Vector Similarity Search:**
  - Performs nearest-neighbor vector similarity search using Qdrant's unified Query API (`QueryAsync`).
- **Multi-Tenant Payload Filtering:**
  - Scopes search queries to specific tenants (`tenantId`) using filter conditions (`MatchKeyword`).
- **Point Management:**
  - Deletes points by ID when documents are updated or removed.

---

## Project Structure

```text
QdrantPlayground/
├── QdrantPlayground.slnx
├── README.md
└── QdrantPlayground/
    ├── Program.cs                  # Demo entry point showing stream upsert & search queries
    ├── QdrantPlayground.csproj     # Project file (.NET 10, Qdrant.Client 1.20+)
    ├── appsettings.json            # Qdrant connection configuration
    ├── Models/
    │   └── VectorDocument.cs       # Data model representing chunked documents & embeddings
    └── Services/
        └── QdrantVectorService.cs  # Service wrapping QdrantClient gRPC operations
```

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) (to run a local Qdrant instance)

---

## Getting Started

### 1. Start Qdrant via Docker

Run the official Qdrant Docker container exposing HTTP (port 6333) and gRPC (port 6334):

```bash
docker run -d -p 6333:6333 -p 6334:6334 \
    -v $(pwd)/qdrant_storage:/qdrant/storage:z \
    qdrant/qdrant
```

You can open the Qdrant Web UI at `http://localhost:6333/dashboard`.

### 2. Configure Connection

Check `QdrantPlayground/appsettings.json` or constants in `Program.cs` to ensure the host and port match your environment:

```json
{
  "Qdrant": {
    "Host": "localhost",
    "GrpcPort": 6334,
    "CollectionName": "knowledge_chunks",
    "VectorSize": 4
  }
}
```

### 3. Build & Run

```bash
dotnet build
dotnet run --project QdrantPlayground
```

---

## Usage Example

### Streaming Batch Upsert

```csharp
// Stream records from source without loading all documents into RAM
static async IAsyncEnumerable<VectorDocument> GetDocumentStreamAsync()
{
    // Fetch and yield records sequentially from DB or file
    yield return new VectorDocument(
        Id: 1,
        TenantId: "tenant-001",
        DocumentId: "doc-001",
        ChunkIndex: 0,
        Content: "Umbraco can be hosted on IIS.",
        Embedding: [0.9f, 0.1f, 0.1f, 0.1f]
    );
}

await vectorService.UpsertInBatchesAsync(GetDocumentStreamAsync(), batchSize: 250);
```

### Multi-Tenant Search

```csharp
float[] queryVector = [0.95f, 0.1f, 0.1f, 0.1f];

var tenantResults = await vectorService.SearchByTenantAsync(
    queryVector,
    tenantId: "tenant-001",
    topK: 5
);

foreach (var hit in tenantResults)
{
    Console.WriteLine($"Score: {hit.Score:F4} | Content: {hit.Payload["content"].StringValue}");
}
```

---

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
