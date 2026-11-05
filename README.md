# KRT Onboarding – API de Contas

API em **.NET 8** para o time de Onboarding do banco KRT: CRUD de contas de clientes
(Id, nome do titular, CPF e status *Ativa/Inativa*). A cada criação, atualização ou remoção
de conta, as demais áreas do banco (prevenção a fraude, cartões etc.) são **notificadas por eventos**.
Para reduzir o custo de consultas ao banco de dados, as consultas por conta usam **cache distribuído
válido até o fim do dia**.

## Stack

| Item | Escolha |
|---|---|
| Linguagem | .NET 8 / ASP.NET Core MVC (controllers) |
| Banco de dados | SQL Server (EF Core 8 + migrations) |
| Cache | Redis (`IDistributedCache`) — em produção, AWS ElastiCache |
| Mensageria | AWS SNS (fan-out para filas SQS de cada área) com Transactional Outbox |
| Testes | xUnit + Moq + EF Core InMemory |
| Ambiente local | Docker Compose (SQL Server, Redis, LocalStack para SNS/SQS) |

## Arquitetura (DDD em camadas)

```
src/
 ├─ KRT.Onboarding.Domain          → Agregado Account, Value Object Cpf, eventos de domínio, contratos (repositório/UoW)
 ├─ KRT.Onboarding.Application     → Casos de uso (AccountService), DTOs, abstração de cache
 ├─ KRT.Onboarding.Infrastructure  → EF Core/SQL Server, repositório, Outbox, publicador SNS, cache Redis
 └─ KRT.Onboarding.Api             → Controllers MVC, tratamento global de erros (ProblemDetails), Swagger
tests/
 └─ KRT.Onboarding.UnitTests       → Domínio, aplicação, cache e outbox
```

As dependências apontam sempre para dentro (Api → Infrastructure → Application → Domain).
O domínio não conhece EF, AWS nem Redis.

### Princípios aplicados

- **DDD**: `Account` é o agregado raiz. Toda mudança passa por métodos do agregado (`Open`, `Update`, `Delete`),
  que validam invariantes e registram eventos. `Cpf` é um Value Object: só existe CPF válido no domínio.
  O CPF é imutável após a abertura (identifica o titular).
- **SOLID**: serviços dependem de abstrações (`IAccountRepository`, `IUnitOfWork`, `IAccountCache`,
  `IEventPublisher`, `TimeProvider`); cada classe tem uma responsabilidade; trocar Redis/SNS por outra tecnologia
  não exige mudar domínio nem aplicação.
- **Clean Code**: nomes expressivos, métodos curtos, controllers sem regra de negócio e sem `try/catch`
  (exceções viram `ProblemDetails` em um único `IExceptionHandler`).

## Requisito 1 — Avisar as outras áreas (criada / atualizada / deletada)

**Solução: eventos de domínio publicados em um tópico SNS, com Transactional Outbox.**

```
API ──(mesma transação)──► SQL Server [Accounts + OutboxMessages]
                                   │
                   OutboxProcessor (BackgroundService)
                                   │
                                   ▼
                        SNS: krt-account-events
                     ┌─────────────┴─────────────┐
                     ▼                           ▼
     SQS fraud-prevention-account-events   SQS cards-account-events
          (todos os eventos)               (filtro: eventType = AccountCreated)
```

- **Desacoplamento (pub/sub)**: a API não conhece os consumidores. Uma nova área só precisa assinar o tópico
  com a própria fila — sem nenhum deploy desta API (princípio Aberto/Fechado no nível da arquitetura).
- **Transactional Outbox**: o evento é gravado na tabela `OutboxMessages` **na mesma transação** da conta.
  Se o SNS estiver fora do ar, nada se perde: o worker publica depois. Isso evita o problema de *dual write*
  (salvar no banco e falhar ao publicar, ou o contrário).
- **Ordem e idempotência**: o worker interrompe o lote na primeira falha para não publicar um `AccountDeleted`
  antes de um `AccountCreated`. Com tópico **SNS FIFO** (ARN terminando em `.fifo`), o `MessageGroupId` é o Id da
  conta e o `MessageDeduplicationId` é o `EventId`. A entrega é *at-least-once*: os consumidores devem ser
  idempotentes usando o `eventId`.
- **Filtros por área**: cada mensagem tem o atributo `eventType` (`AccountCreated`, `AccountUpdated`,
  `AccountDeleted`), permitindo *subscription filter policies* (ex.: cartões só recebe criações).
- **Event-carried state transfer**: os eventos levam os dados da conta, então as áreas podem manter sua
  própria cópia e nem precisar chamar esta API — o que também ajuda no requisito 2.

Exemplo de payload publicado:

```json
{
  "accountId": "cc8bb3d6-c38f-4290-a893-678e14f45c10",
  "holderName": "Maria da Silva",
  "cpf": "52998224725",
  "status": "Active",
  "eventId": "91049411-f212-424c-93a1-54197325ab99",
  "occurredAt": "2026-11-05T15:28:21.9435479Z"
}
```

## Requisito 2 — Evitar consultas repetidas ao banco no mesmo dia

**Solução: cache distribuído (Redis/ElastiCache) com padrão cache-aside e expiração à meia-noite.**

- `GET /api/v1/accounts/{id}` consulta primeiro o Redis; só vai ao SQL Server em caso de *miss*, e então grava no cache.
- A entrada **expira à meia-noite (horário de Brasília, configurável)** — exatamente "consultada naquele mesmo dia".
  Na próxima consulta do dia seguinte o dado é recarregado do banco.
- **Consistência**: atualização faz *write-through* (cache recebe a versão nova) e remoção invalida a chave,
  então o cache nunca serve uma conta desatualizada por causa de uma alteração feita pela própria API.
- **Cache aquecido na criação**: as áreas reagem ao evento `AccountCreated` consultando a conta logo em seguida.
- **Distribuído**: compartilhado entre todas as instâncias da API (um cache em memória local multiplicaria os
  *misses* por instância).
- **Resiliência**: se o Redis cair, o erro é logado e a API segue respondendo pelo banco.

Outras medidas complementares (não implementadas, citadas como evolução): os consumidores guardarem a cópia
recebida nos eventos (elimina a consulta), e cabeçalhos HTTP de cache (`Cache-Control`/`ETag`) para clientes/API Gateway.

## Outras decisões

- **Concorrência otimista** (`rowversion`): duas atualizações simultâneas da mesma conta não se sobrescrevem
  silenciosamente — a segunda recebe `409 Conflict`.
- **CPF único**: validado na aplicação e garantido por índice único no banco (corrida entre requisições → `409`).
- **Erros** no padrão RFC 7807 (`ProblemDetails`): `400` regra de negócio, `404` não encontrada, `409` conflito.
- **Datas em UTC**; `TimeProvider` injetado para testes determinísticos.
- **Health check** em `/health` (inclui conexão com o banco).

## Endpoints

| Método | Rota | Descrição |
|---|---|---|
| `POST` | `/api/v1/accounts` | Abre conta (`201`, status inicial `Active`) |
| `GET` | `/api/v1/accounts/{id}` | Consulta conta (com cache) |
| `GET` | `/api/v1/accounts?page=1&pageSize=20` | Lista paginada |
| `PUT` | `/api/v1/accounts/{id}` | Atualiza nome e status (`Active`/`Inactive`) |
| `DELETE` | `/api/v1/accounts/{id}` | Remove conta (`204`) |

```bash
curl -X POST http://localhost:8080/api/v1/accounts -H "Content-Type: application/json" \
  -d '{"holderName":"Maria da Silva","cpf":"529.982.247-25"}'

curl -X PUT http://localhost:8080/api/v1/accounts/{id} -H "Content-Type: application/json" \
  -d '{"holderName":"Maria Souza","status":"Inactive"}'
```

## Como executar

### Tudo com Docker (API + SQL Server + Redis + LocalStack)

```bash
docker compose up --build
```

Swagger: http://localhost:8080/swagger — as migrations são aplicadas na subida. Para ver os eventos chegando
nas filas:

```bash
docker compose exec localstack awslocal sqs receive-message --queue-url http://localhost:4566/000000000000/fraud-prevention-account-events
```

### API local (Visual Studio / `dotnet run`)

```bash
docker compose up -d sqlserver redis
dotnet run --project src/KRT.Onboarding.Api
```

Sem `Messaging:TopicArn` configurado, os eventos são apenas logados (útil para desenvolvimento).
Sem `ConnectionStrings:Redis`, o cache usa memória local.

### Testes

```bash
dotnet test
```
