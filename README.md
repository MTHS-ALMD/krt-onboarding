# KRT Onboarding – API de Contas

API para o CRUD de contas de clientes do banco KRT (Id, nome do titular, CPF e status Ativa/Inativa).

## Tecnologias

- **.NET 8** / ASP.NET Core MVC
- **SQL Server** com Entity Framework Core 8
- **Redis** (cache distribuído)
- **AWS SNS + SQS** (eventos), com LocalStack para rodar localmente
- **xUnit + Moq** (testes unitários)
- **Docker Compose**
- Arquitetura em camadas com **DDD**, **SOLID** e **Clean Code**

## Endpoints

| Método | Rota | Descrição |
|---|---|---|
| `POST` | `/api/v1/accounts` | Cria uma conta (status inicial: `Active`) |
| `GET` | `/api/v1/accounts/{id}` | Consulta uma conta pelo Id |
| `GET` | `/api/v1/accounts?page=1&pageSize=20` | Lista as contas com paginação |
| `PUT` | `/api/v1/accounts/{id}` | Atualiza o nome do titular e o status (`Active`/`Inactive`) |
| `DELETE` | `/api/v1/accounts/{id}` | Remove uma conta |

Documentação interativa (Swagger): `http://localhost:8080/swagger`

## Eventos: avisar as outras áreas do banco

**Problema:** várias áreas (prevenção a fraude, cartões etc.) precisam saber quando uma conta é **criada, atualizada ou deletada**.

**Solução:** a cada operação, a API publica um evento em um tópico do **AWS SNS**:

| Evento | Quando é publicado |
|---|---|
| `AccountCreated` | Conta criada |
| `AccountUpdated` | Nome ou status alterado |
| `AccountDeleted` | Conta removida |

- Cada área assina o tópico com **sua própria fila SQS** e recebe os eventos sem que a API precise conhecê-la. Uma área nova só precisa assinar o tópico, sem mudar a API.
- Cada área pode filtrar só os eventos que interessam a ela. Exemplo: cartões recebe apenas `AccountCreated`.
- O evento carrega os dados da conta, então a área consumidora não precisa consultar a API para obtê-los.
- **Garantia de entrega (Outbox):** o evento é salvo no banco **na mesma transação** da conta, e um processo em segundo plano o publica no SNS depois. Se o SNS estiver fora do ar, o evento não se perde: ele é publicado assim que o serviço voltar, mantendo a ordem dos eventos.

## Redis: evitar consultas repetidas ao banco no mesmo dia

**Problema:** as áreas consultam as mesmas contas várias vezes ao dia, e a AWS cobra por consulta ao banco de dados.

**Solução:** cache das contas no **Redis**:

1. Na consulta de uma conta, a API procura primeiro no Redis.
2. Se a conta estiver lá, ela é devolvida **sem consultar o banco**.
3. Se não estiver, a API busca no SQL Server e guarda o resultado no Redis.
4. A conta fica no cache **até a meia-noite** (horário de Brasília). Assim, cada conta é lida do banco no máximo uma vez por dia.

- Quando uma conta é **atualizada**, o cache recebe a versão nova. Quando é **deletada**, ela sai do cache. O cache nunca devolve dados desatualizados.
- O Redis é compartilhado entre todas as instâncias da API.
- Se o Redis ficar indisponível, a API continua funcionando, consultando direto o banco.

## Como executar

```bash
docker compose up --build
```

## Testes

```bash
dotnet test
```
