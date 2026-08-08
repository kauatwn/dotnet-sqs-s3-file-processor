# Cloud-Native Bulk Ingestion Engine

## 1. O Problema e o Domínio

Processar arquivos massivos de forma síncrona alocando todo o payload na memória RAM resulta inevitavelmente em erros de _Out of Memory_. Por outro lado, persistir registros linha por linha esgota o pool de conexões do banco de dados e degrada a vazão do sistema.

Para resolver esse gargalo clássico de alta volumetria e concorrência de I/O, um ecossistema distribuído focado em ingestão assíncrona foi modelado. O objetivo central é receber e processar arquivos de grande porte garantindo baixo consumo de recursos e isolamento térmico da aplicação principal.

## 2. A Arquitetura da Aplicação e Padrões

O fluxo de processamento funciona de forma coreografada e descentralizada:

- **Garantia de Borda (Pre-signed URLs):** O upload direto de bytes na API é totalmente evitado para mitigar o problema do _Double-Hop_. Uma URL pré-assinada é gerada em milissegundos, permitindo que o cliente envie o arquivo CSV de forma segura e direta ao **Amazon S3**.
- **Mensageria Desacoplada:** A conclusão do upload no S3 engatilha automaticamente uma notificação para o **Amazon SQS**, isolando completamente a camada de recepção da camada de processamento.
- **Consumo com Backpressure:** Em background, um _Worker Service_ realiza _Long Polling_ na fila, ditando seu próprio ritmo de consumo sob demanda para proteger a infraestrutura contra picos de tráfego.
- **Otimização de Código (Streaming & Bulk Insert):** O download do arquivo inteiro em disco é evitado. Os dados são lidos em fluxo contínuo direto da rede via **Streaming (`IAsyncEnumerable`)**, mantendo a memória RAM estável. Os dados limpos são acumulados em memória e despejados no **PostgreSQL** por meio de **Bulk Inserts** em lotes estruturados.
- **Idempotência Rígida:** Mecanismos de checagem de estado no banco de dados bloqueiam o reprocessamento de arquivos já concluídos, protegendo o sistema contra a entrega duplicada (_At-Least-Once_) nativa do SQS.

## 3. Engenharia de Resiliência e Tolerância a Falhas

Ambientes distribuídos operam sob a premissa de que falhas de rede e indisponibilidades parciais são inevitáveis. A estabilidade mecânica do ecossistema é assegurada por políticas defensivas robustas aplicadas de forma granular:

| Componente / Cenário               | Risco Técnico                                                                  | Mecanismo de Proteção      | Estratégia de Implementação                                                                                                                           |
| ---------------------------------- | ------------------------------------------------------------------------------ | -------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Consumo do SQS**                 | _At-Least-Once Delivery_ resultando em mensagens duplicadas.                   | Idempotência de Domínio    | O `ProcessDocumentUseCase` intercepta a mensagem e valida o estado do Job no banco; payloads já processados são rejeitados antes de acionar o parser. |
| **Integração com AWS SDK**         | Instabilidade transitória na chamada de rede HTTP ao S3 ou SQS.                | Polly Resilience Pipelines | Políticas de _Retry_ com _Exponential Backoff_ e _Jitter_ isolam e tratam erros de I/O de rede sem derrubar o contêiner.                              |
| **Payload Inválido ou Corrompido** | Arquivos fora do padrão que travam o processamento contínuo da aplicação.      | Dead Letter Queue (DLQ)    | Mensagens que falham após 5 tentativas (`max_receive_count = 5`) são expurgadas da fila principal e isoladas na DLQ para auditoria.                   |
| **Sobrecarga do Banco (I/O)**      | Enxurrada de escritas simultâneas bloqueando o pool de conexões do PostgreSQL. | Backpressure por Pull      | O Worker controla ativamente o ritmo de consumo através de _Long Polling_ direto na fila. A API nunca empurra conexões destrutivas ao banco.          |

## 4. Decisões Arquiteturais e Abordagem de FinOps

A seleção das ferramentas obedece a critérios rigorosos de vazão (_throughput_) e eficiência de custo:

- **Por que ECS com AWS Fargate em vez de AWS Lambda?** Rotinas de ETL complexas e ingestão de dados em massa que demandam tempo livre de execução e alto consumo de CPU/Memória não se adequam ao limite restrito de 15 minutos do Lambda. O modelo de contêineres do Fargate provê previsibilidade de custo e suporta _Auto Scaling_ elástico atrelado ao volume de mensagens no SQS.
- **Dispensabilidade do RDS Proxy:** Como a computação é baseada em contêineres persistentes no ECS Fargate e não em funções efêmeras paralelas, o pool de conexões com o banco de dados PostgreSQL é gerenciado de forma nativa, centralizada e previsível pela própria aplicação. A sobrecarga de conexões é mitigada na origem, eliminando custos extras com proxies de banco.

## 5. A Infraestrutura como Código

Toda a infraestrutura foi provisionada de forma declarativa utilizando **Terraform**. O repositório segue o padrão de **Módulos Puros (Pure Modules)**, onde os recursos são isolados logicamente e acoplados via **Glue Code** no ambiente de desenvolvimento:

- **Camada de Computação:** Um Cluster **Amazon ECS** instrumentado com _Container Insights_ gerencia as tarefas executadas sob demanda na infraestrutura _Serverless_ do **AWS Fargate**.
- **Camada de Registro:** Repositórios no **Amazon ECR** armazenam de forma privada as imagens Docker imutáveis da API e do Worker.
- **Mensageria e Resiliência:** Filas principais do SQS são estabelecidas com políticas de redirecionamento para **Dead Letter Queues (DLQ)**, barrando a perda de mensagens falhas.
- **Dados e Persistência:** Um banco relacional **Amazon RDS PostgreSQL** é inicializado e totalmente blindado contra acessos públicos externos.
- **Políticas de Rígido IAM:** A `task_role` da aplicação restringe as ações estritamente ao necessário (`sqs:ReceiveMessage`, `sqs:DeleteMessage`, `s3:GetObject`), impondo o princípio do menor privilégio e impedindo privilégios administrativos cruzados.

## 6. Limitações Conhecidas e Trade-offs

Decisões de engenharia pragmáticas geram _trade-offs_ explícitos no sistema:

- **Orphan Jobs (Estados Obsoletos):** Como o registro do job é criado na tabela antes do upload real do cliente no S3, caso o cliente desista do upload, o job permanecerá em estado `Pending` indefinidamente.
  - _Mitigação para Produção:_ A implementação de um _Sweeper Worker_ para expurgo de dados antigos em background ou a alocação desse estado transitório no **Redis** com _Time-To-Live (TTL)_.

- **Perda de Validação Imediata:** Ao contornar a API para o envio direto do arquivo à nuvem, validações síncronas de estrutura do _payload_ na borda são sacrificadas. Arquivos corrompidos ou com layouts inválidos só serão detectados de forma assíncrona pelo Worker, resultando no roteamento da mensagem para a DLQ.
- **High-Throughput vs. Physical Insertion Order:** Para atingir a métrica de muitos registros inseridos por segundo, a ordenação física de inserção no banco de dados é abdicada em favor da velocidade de gravação massiva. A consistência temporal passa a depender exclusivamente das propriedades ACID do PostgreSQL e das lógicas de data do próprio domínio.
