# Distributed File Processor

A robust, event-driven distributed system built with **C# 14** and **.NET 10** for processing large files asynchronously. This project serves as an engineering sandbox to explore cloud-native patterns, specifically integrating **AWS S3**, **AWS SQS**, and Background Workers to handle mass data ingestion reliably.

## Table of Contents

- [Prerequisites](#prerequisites)
- [How to Run](#how-to-run)
- [Project Structure](#project-structure)
- [Architecture & Design Principles](#architecture--design-principles)

## Prerequisites

Ensure you have the following installed to run this project efficiently:

- **[.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)** (or later)
- **[Docker Desktop](https://www.docker.com/)** (Required to orchestrate PostgreSQL, Seq, and LocalStack)
- **IDE:** [Visual Studio](https://visualstudio.microsoft.com), [Visual Studio Code](https://code.visualstudio.com/), or [Rider](https://www.jetbrains.com/rider/).
- **API Client:** [Postman](https://www.postman.com/) or [Insomnia](https://insomnia.rest/).

## How to Run

### 1. Clone the Repository

```bash
git clone https://github.com/kauatwn/dotnet-sqs-s3-file-processor.git
```

### 2. Enter the Directory

```bash
cd dotnet-sqs-s3-file-processor
```

### 3. Run with Docker Compose

This command spins up the API, the Background Worker, PostgreSQL, Seq (for centralized logging), and **LocalStack** (simulating AWS S3 and SQS locally).

```bash
docker compose up -d
```

_Seq (Logs) will be accessible at `http://localhost:5341`._

### 4. Simulating a Full Upload

To test the system's high-throughput capabilities, a dedicated C# tool is provided to generate a massive CSV file dynamically.

**Step 4.1: Generate the Payload (No .NET SDK required)**
You can use Docker Compose to run the generator script isolated from your host machine. This will create a 100 thousand records CSV file (`payload-100k.csv`, approx. 6MB) in your `output` directory:

```bash
docker compose run --rm csv-generator
```

_The generated file will have the following structure:_

```csv
Date,Amount,Description,AccountId
2023-05-12,4321.50,Simulated_Transaction_1,ACC-84732
2023-11-03,12.99,Simulated_Transaction_2,ACC-10294
```

**Step 4.2: Request a Pre-signed URL**
Perform a `POST` request to the API to get temporary upload permissions:

- **Endpoint:** `POST http://localhost:8080/api/documents/upload`
- **Body:** `json { "fileName": "payload-100k.csv" }`

**Step 4.3: Direct Upload to S3 (LocalStack)**
Use the returned `url` to perform a `PUT` request via Postman or cURL.
Attach the `payload-100k.csv` file as a binary payload. _(No specific `Content-Type` header is required for this sandbox)_.

_Once the upload is complete, check the Seq Dashboard (`http://localhost:5341`) to watch the Background Worker ingest the 100,000 records into PostgreSQL in real-time._

### 5. Execute Tests

To validate the domain logic, infrastructure parsing, and distributed flows:

```bash
dotnet test
```

## Project Structure

The solution follows the **Clean Architecture** principles to ensure separation of concerns, with a dedicated split between the Web API and the Background Worker.

```plaintext
dotnet-sqs-s3-file-processor/
├── src/
│   ├── DistributedFileProcessor.API/
│   ├── DistributedFileProcessor.Application/
│   ├── DistributedFileProcessor.Domain/
│   ├── DistributedFileProcessor.Infrastructure/
│   └── DistributedFileProcessor.Worker/
└── tests/
    ├── DistributedFileProcessor.IntegrationTests/
    └── DistributedFileProcessor.UnitTests/
```

## Architecture & Design Principles

This repository prioritizes **scalability** and **asynchronous processing**, utilizing an **Event-Driven Architecture (Choreography)** to ensure maximum decoupling between services.

### 1. Event-Driven Architecture (Choreography)

Instead of the API acting as a proxy for file bytes (Double-Hop), the system uses **Pre-signed URLs** to allow direct, secure uploads to S3.

![Architecture diagram illustrating the Pre-signed URL upload and Event-Driven Choreography patterns](./docs/architecture.png)
_Figure 1: Event-driven choreography flow from pre-signed URL request to database ingestion._

1. **Request Upload (API):** The client requests temporary upload permission. The API generates a **Pre-signed URL**.
2. **Tracking:** A job record with a "Pending" status is saved to the PostgreSQL database.
3. **Direct Upload:** The client performs a `PUT` request directly to **Amazon S3** using the provided URL.
4. **S3 Event Notification:** Upon successful upload, S3 automatically triggers an event notification to **Amazon SQS**.
5. **Processing (Worker):** A background worker consumes the S3 event from SQS, downloads the file, streams and parses the CSV content (`IAsyncEnumerable`), and performs a bulk insert of the records into the database.

### 2. Design Patterns

The project utilizes established patterns to ensure modularity and cloud readiness.

|             Pattern              |                           Usage Scenario                           | Implementation                                 |
| :------------------------------: | :----------------------------------------------------------------: | :--------------------------------------------- |
|       **Pre-signed URLs**        |  Eliminating the "Double-Hop" problem, reducing API network load   | `DocumentsController` & `S3FileStorageService` |
|      **Event Choreography**      | Decoupling the API from the Worker, solving the Dual-Write problem | `LocalStackExtensions` & `SqsMessageConsumer`  |
| **Streaming (IAsyncEnumerable)** |          Processing large datasets without memory limits           | `CsvTransactionFileParser`                     |
|         **Idempotency**          | Guarding against SQS duplicated messages (At-Least-Once delivery)  | `ProcessDocumentUseCase`                       |

### 3. Resilience & Error Handling

Distributed systems are prone to network failures. The application handles this through:

- **Polly Resilience Pipelines:** Configured for S3 and SQS interactions to handle transient network errors.
- **Dead Letter Queues (DLQ):** If the worker fails to process a file (e.g., bad CSV format) after multiple retries, the message is routed to an SQS DLQ, and the job status is marked as "Failed".

### 4. Known Limitations & Pragmatic Trade-offs

This project is an engineering sandbox focused on cloud-native integration. While the architecture solves classic issues like "Dual-Write" and "Double-Hop", it introduces specific pragmatic trade-offs:

- **Orphan Jobs (Stale State):** The API creates a `Pending` job in the database _before_ the client uploads the file to S3 via the Pre-signed URL. If the client abandons the upload, the S3 Event is never triggered, leaving the job `Pending` forever. _Production Mitigation:_ Implement a background cleanup job (Sweeper Worker) or store the initial state in Redis with a Time-To-Live (TTL).
- **Loss of Immediate Edge Validation:** By bypassing the Web API for the actual file upload, we lose the ability to validate the file content synchronously. A corrupted file will only be detected asynchronously when the background worker attempts to parse it.
- **High-Throughput vs. Physical Insertion Order:** To achieve ingestion rates of over 100k+ records/sec, the `EFCore.BulkExtensions` library is configured with `PreserveInsertOrder = false`. This trades physical database insertion order for extreme speed, relying entirely on PostgreSQL's ACID properties and business logic dates for ordering.

> [!IMPORTANT]
> **Architectural Decision: Idempotency & At-Least-Once Delivery**
>
> SQS and S3 Event Notifications guarantee _At-Least-Once_ delivery, meaning duplicate messages can occur. This system handles this natively: the `ProcessDocumentUseCase` implements an **idempotency check**, gracefully ignoring duplicate messages if the Job is already marked as `Completed`.

### 5. Comprehensive Testing Strategy

The project adopts a strategy focused on **Cloud Integration** and **Isolation**.

- **Unit Tests:** Verify business rules and CSV parsing logic in isolation.
- **Integration Tests:** Verify the entire distributed pipeline.
  - **Technology:** Uses **[Testcontainers](https://testcontainers.com/)** to orchestrate real instances of PostgreSQL and **[LocalStack](https://www.localstack.cloud/)**.
  - **Worker Validation:** The test suite injects the Worker into the `WebApplicationFactory`, allowing end-to-end validation (API -> DB -> S3 -> SQS -> Worker -> DB) within a single test execution.

### 6. Performance & Load Testing

To validate the system's backpressure capabilities and the database's resilience under high concurrency, a load testing suite using **[k6](https://k6.io/)** is included.

Run the stress test via Docker Compose to simulate multiple concurrent clients performing heavy file uploads (requires the 100k payload to be generated first):

```bash
docker compose run --rm k6
```

### 7. CI/CD & Quality

The project includes a **GitHub Actions** workflow that ensures quality on every push:

- **Automated Testing:** Runs Unit and Integration tests using `XPlat Code Coverage`.
- **Static Analysis:** Integrates with **SonarCloud** for code quality gates, explicitly excluding EF Core migrations and AWS wrappers via `[ExcludeFromCodeCoverage]`.
- **Docker Build Validation:** Verifies that both API and Worker container images build successfully (`docker buildx`).
