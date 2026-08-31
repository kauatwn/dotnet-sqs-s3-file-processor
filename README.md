# Cloud-Native Bulk Ingestion Engine

## 1. The Problem and Domain

Processing massive files synchronously while allocating the entire payload in RAM inevitably leads to _Out of Memory_ errors. On the other hand, persisting records one by one exhausts the database connection pool and degrades system throughput.

To address this classic high-volume, I/O-concurrency bottleneck, a distributed ecosystem focused on asynchronous ingestion was designed. The primary goal is to receive and process large files while maintaining low resource consumption and providing workload isolation from the main application.

## 2. Application Architecture and Patterns

The processing flow operates in a choreographed and decentralized manner:

- **Edge Offloading (Pre-signed URLs):** Direct byte uploads through the API are completely avoided to mitigate the _Double-Hop_ problem. A pre-signed URL is generated within milliseconds, allowing the client to securely upload the CSV file directly to **Amazon S3**.
- **Decoupled Messaging:** The completion of an upload to S3 automatically triggers a notification to **Amazon SQS**, completely isolating the ingestion layer from the processing layer.
- **Backpressure Through Pull-Based Consumption:** In the background, a _Worker Service_ performs _Long Polling_ against the queue, controlling its own consumption rate on demand to protect the infrastructure from traffic spikes.
- **Code Optimization (Streaming & Bulk Insert):** Downloading the entire file to disk is avoided. Data is continuously read directly from the network using **Streaming (`IAsyncEnumerable`)**, keeping RAM usage stable. Clean records are accumulated in memory and flushed to **PostgreSQL** through structured **Bulk Inserts**.
- **Strict Idempotency:** Database state checks prevent already-completed files from being processed again, protecting the system against duplicate delivery resulting from SQS's native _At-Least-Once_ delivery model.

## 3. Resilience and Fault-Tolerance Engineering

Distributed environments operate under the assumption that network failures and partial outages are inevitable. The mechanical stability of the ecosystem is ensured through robust defensive policies applied at a granular level:

| Component / Scenario             | Technical Risk                                                            | Protection Mechanism       | Implementation Strategy                                                                                                                                                |
| -------------------------------- | ------------------------------------------------------------------------- | -------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **SQS Consumption**              | _At-Least-Once Delivery_ resulting in duplicate messages.                 | Domain Idempotency         | The `ProcessDocumentUseCase` intercepts the message and validates the Job state in the database; already-processed payloads are rejected before the parser is invoked. |
| **AWS SDK Integration**          | Transient instability during HTTP network calls to S3 or SQS.             | Polly Resilience Pipelines | _Retry_ policies with _Exponential Backoff_ and _Jitter_ isolate and handle network I/O failures without bringing down the container.                                  |
| **Invalid or Corrupted Payload** | Files outside the expected format causing continuous processing failures. | Dead Letter Queue (DLQ)    | Messages that fail after 5 attempts (`max_receive_count = 5`) are removed from the main queue and isolated in the DLQ for auditing.                                    |
| **Database Overload (I/O)**      | A flood of concurrent writes exhausting the PostgreSQL connection pool.   | Pull-Based Backpressure    | The Worker actively controls the consumption rate through _Long Polling_ directly against the queue. The API never pushes excessive database connections.              |

## 4. Architectural Decisions and FinOps Approach

The selection of technologies follows rigorous throughput and cost-efficiency criteria:

- **Why ECS with AWS Fargate instead of AWS Lambda?** Complex ETL routines and bulk data ingestion workloads that require longer execution times and high CPU/memory consumption are not well suited to Lambda's 15-minute execution limit. Fargate's container-based model provides predictable costs and supports elastic _Auto Scaling_ driven by the volume of messages in SQS.
- **Why RDS Proxy is unnecessary:** Since the compute layer is based on persistent containers running on ECS Fargate rather than parallel ephemeral functions, the PostgreSQL connection pool can be managed natively, centrally, and predictably by the application itself. Connection overhead is mitigated at the source, eliminating additional proxy costs.

## 5. Infrastructure as Code

The entire infrastructure is provisioned declaratively using **Terraform**. The repository follows a **Pure Modules** pattern, where resources are logically isolated and composed through **Glue Code** in the development environment:

- **Compute Layer:** An **Amazon ECS** cluster instrumented with _Container Insights_ manages on-demand tasks running on the _Serverless_ infrastructure provided by **AWS Fargate**.
- **Registry Layer:** **Amazon ECR** repositories privately store immutable Docker images for the API and Worker.
- **Messaging and Resilience:** SQS primary queues are configured with redirection policies to **Dead Letter Queues (DLQs)**, preventing failed messages from being lost.
- **Data and Persistence:** An **Amazon RDS PostgreSQL** relational database is provisioned and fully protected against public external access.
- **Strict IAM Policies:** The application's `task_role` restricts actions strictly to those required (`sqs:ReceiveMessage`, `sqs:DeleteMessage`, `s3:GetObject`), enforcing the principle of least privilege and preventing cross-service administrative privileges.

## 6. Known Limitations and Trade-offs

Pragmatic engineering decisions introduce explicit trade-offs into the system:

- **Orphan Jobs (Stale States):** Since the job record is created in the database before the client's actual upload to S3, a client abandoning the upload will leave the job indefinitely in the `Pending` state.
  - _Production Mitigation:_ Implement a _Sweeper Worker_ to purge stale data in the background or move this transient state to **Redis** with a _Time-To-Live (TTL)_.

- **Loss of Immediate Validation:** By bypassing the API and uploading the file directly to the cloud, synchronous payload structure validation at the edge is sacrificed. Corrupted files or invalid layouts are detected asynchronously by the Worker, resulting in the message being routed to the DLQ.

- **High Throughput vs. Physical Insertion Order:** To achieve high records-per-second insertion rates, physical insertion ordering in the database is sacrificed in favor of bulk write performance. Temporal consistency instead relies exclusively on PostgreSQL's ACID properties and the domain's own date-handling logic.

- **Single-Worker Assumption vs. Distributed Concurrency:** The ecosystem is designed and sized assuming a single active worker replica (`desired_count = 1`). If the service is scaled horizontally (multiple parallel workers or ECS Fargate Auto Scaling), the current in-memory state check may encounter race conditions due to SQS's native _At-Least-Once_ delivery.
  - _Production Mitigation:_ Evolve the status transition into an atomic/conditional update in the database (`UPDATE document_process_jobs SET status = 'Processing' WHERE id = @jobId AND status = 'Pending'`) or implement optimistic concurrency control using PostgreSQL's native `xmin` / `RowVersion` in EF Core.
