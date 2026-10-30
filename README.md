# Portfolio API

[![CI](https://github.com/mlrneves/postech-fase1-portfolio-api/actions/workflows/ci.yml/badge.svg)](https://github.com/mlrneves/postech-fase1-portfolio-api/actions/workflows/ci.yml)
[![CD](https://github.com/mlrneves/postech-fase1-portfolio-api/actions/workflows/cd.yml/badge.svg)](https://github.com/mlrneves/postech-fase1-portfolio-api/actions/workflows/cd.yml)

API REST para acompanhar uma carteira de investimentos pessoal. Cada pessoa se
cadastra, autentica, recebe um token e a partir daí registra, consulta, altera e
apaga os próprios aportes — e só os próprios.

Foi escrita como entrega da atividade substitutiva da Fase 1 da pós em
Arquitetura de Sistemas .NET com Azure (POSTECH). O enunciado pedia uma API em
.NET conectada a um banco SQL Server, com um endpoint de autenticação e os demais
endpoints protegidos por ele. A Fase 2 acrescentou a esteira de CI/CD com GitHub
Actions, a imagem Docker como artefato e o deploy automático em containers — está
na seção [CI/CD](#cicd).

**Stack:** .NET 10 · ASP.NET Core · Entity Framework Core 10 · SQL Server · JWT ·
BCrypt · xUnit · Docker · GitHub Actions

---

## Sumário

- [O que a API faz](#o-que-a-api-faz)
- [Como o projeto está organizado](#como-o-projeto-está-organizado)
- [Decisões de projeto](#decisões-de-projeto)
- [Preparando o ambiente](#preparando-o-ambiente)
- [Rodando](#rodando)
- [O banco de dados](#o-banco-de-dados)
- [Endpoints](#endpoints)
- [Erros](#erros)
- [Testes](#testes)
- [Configuração](#configuração)
- [CI/CD](#cicd)
- [Roteiro do vídeo de demonstração](#roteiro-do-vídeo-de-demonstração)
- [Quando algo não funciona](#quando-algo-não-funciona)
- [Onde cada assunto da fase aparece no código](#onde-cada-assunto-da-fase-aparece-no-código)

---

## O que a API faz

O domínio tem três coisas:

**Investidor** é quem usa a API. Cadastra-se com nome, e-mail e senha, e o e-mail
é a credencial de login. A senha nunca é guardada — o que vai para o banco é um
hash BCrypt.

**Aporte** é um registro de dinheiro aplicado: valor, data, classe de ativo, e
opcionalmente a instituição e uma observação. Todo aporte pertence a um
investidor, e essa posse é levada a sério: não existe consulta na aplicação que
busque um aporte só pelo id, sem passar o id do investidor junto.

**Classe de ativo** é a categorização do aporte: Ações, Renda Fixa, Fundos
Imobiliários, Tesouro Direto, Criptomoedas. Não é um `enum` — é tabela, mantida
pelos endpoints `/asset-classes`, então dá para acrescentar "Previdência Privada"
ou "Debêntures" em produção sem recompilar nem gerar migration.

Além do CRUD, `GET /investments/summary` devolve o total aplicado e a
participação percentual de cada classe de ativo na carteira.

---

## Como o projeto está organizado

Quatro projetos, em camadas, com as dependências apontando sempre para dentro:

```
Portfolio.WebApi  ->  Portfolio.Infra  ->  Portfolio.UseCases  ->  Portfolio.Core
```

```
src/
  Portfolio.Core          Entidades, regras de negócio e as interfaces de repositório
  Portfolio.UseCases      Serviços de aplicação, contratos de entrada/saída, hash de senha
  Portfolio.Infra         EF Core (DbContext, mapeamentos, migrations), repositórios, emissão de JWT
  Portfolio.WebApi        Controllers, pipeline HTTP, Swagger, tratamento de erros
tests/
  Portfolio.UnitTests         Regras de domínio e casos de uso, com repositórios simulados
  Portfolio.IntegrationTests  A API inteira de pé, exercitada por HTTP
```

`Portfolio.Core` é o coração e não referencia nada: nem outro projeto, nem um
pacote NuGet. Isso é proposital — é o que garante que uma regra como "aporte não
pode ter data no futuro" não dependa de EF Core, de ASP.NET ou de nada que possa
mudar por baixo dela.

Dentro de cada projeto os arquivos estão agrupados **por assunto**, não por tipo
de artefato. Ou seja, `Core/Investments/` guarda a entidade `Investment` junto da
interface `IInvestmentRepository`, em vez de uma pasta `Entities/` e outra
`Repositories/` separadas. Quem for mexer em aporte encontra tudo no mesmo lugar.

### Por dentro das camadas

```
Portfolio.Core
  Abstractions/      IUnitOfWork, BusinessRuleException, Slugger
  Investors/         Investor, IInvestorRepository
  Investments/       Investment, AssetClassTotal, IInvestmentRepository
  AssetClasses/      AssetClass, AssetClassUsage, IAssetClassRepository

Portfolio.UseCases
  Abstractions/      IPasswordHasher, IAccessTokenFactory
  Accounts/          AccountService + contratos de cadastro e login
  Investments/       InvestmentService + contratos de aporte e do resumo
  AssetClasses/      AssetClassService + contratos
  Errors/            ResourceNotFound, DuplicateResource, InvalidCredentials
  Security/          BCryptPasswordHasher

Portfolio.Infra
  Persistence/       PortfolioDbContext, Mappings/, Converters/, Repositories/, Migrations/
  Identity/          JwtOptions, JwtAccessTokenFactory

Portfolio.WebApi
  Controllers/       AccountsController, InvestmentsController, AssetClassesController
  Setup/             Registro de autenticação, JSON, Swagger, limite de tentativas, saúde e migrations
  Errors/            ApiExceptionHandler
  Middleware/        RequestTimingMiddleware
  Extensions/        Leitura do id do investidor a partir do token
```

---

## Decisões de projeto

Algumas escolhas que não são óbvias e valem a explicação.

**As entidades não têm setter público.** `Investment` e `Investor` validam no
construtor e só mudam de estado por métodos (`Revise`, `Rename`). Quando a regra
é violada elas jogam `BusinessRuleException`. O efeito prático é que não existe
instância inválida circulando: se o objeto existe, ele passou pela validação. Os
construtores privados sem parâmetro estão ali porque o EF Core precisa deles para
materializar a entidade vinda do banco.

**Validação em dois níveis, de propósito.** Os contratos de entrada usam
DataAnnotations (`[Required]`, `[Range]`, `[MaxLength]`), que barram o lixo na
borda e alimentam o Swagger. As regras de verdade ficam na entidade. Há
sobreposição, e ela é intencional: a camada de domínio continua correta mesmo se
alguém chamá-la de outro lugar que não passe por HTTP.

**Classe de ativo tem nome e slug.** O nome é o que a pessoa digitou, com acento
("Fundos Imobiliários"). O slug é a forma canônica ("fundos-imobiliarios"), e é
nele que está o índice único e por onde a busca acontece. Assim `"Ações"`,
`"ACOES"` e `"acoes"` chegam todos na mesma classe, e a checagem de duplicidade
não depende do *collation* do banco.

**A data do aporte é `DateOnly`, não `DateTime`.** Hora e fuso não significam nada
para "quando você aplicou esse dinheiro", e carregá-los só criaria a chance de um
aporte feito hoje à noite virar "amanhã" na comparação com o limite. No SQL Server
a coluna é `date`.

**O `DbContext` é a unidade de trabalho.** `PortfolioDbContext` implementa
`IUnitOfWork` diretamente, porque ele já *é* a unidade de trabalho do EF Core — um
wrapper em volta só acrescentaria uma camada de indireção sem nada em troca. Os
repositórios montam as mudanças em memória e nada vai para o banco antes do
`SaveAsync` do caso de uso.

**Não existe `Update()` nos repositórios.** O aporte devolvido por
`FindAsync` vem rastreado pelo `DbContext`, então alterá-lo e chamar `SaveAsync`
basta. Um `Update()` explícito marcaria o grafo inteiro como modificado e geraria
UPDATE em colunas que não mudaram.

**Acesso indevido responde 404, não 403.** Se a Ana pedir o aporte do Bruno, a
resposta é "não encontrado". Um 403 confirmaria que aquele id existe, e isso já é
informação a mais do que ela deveria ter.

**A mensagem de login errado é sempre a mesma.** E-mail inexistente e senha
incorreta devolvem o mesmo texto. Diferenciar os dois permitiria descobrir quais
e-mails estão cadastrados na base.

**BCrypt com work factor 12.** Cerca de 250 ms por hash no hardware atual: caro o
suficiente para inviabilizar força bruta, barato o suficiente para caber dentro de
uma requisição de login. O salt vai embutido no próprio hash, por isso não existe
coluna separada para ele.

**`/auth` e `/users` têm limite de tentativas.** Dez por minuto, por origem. Isso
resolve dois problemas que moram no mesmo lugar. O primeiro é força bruta contra a
senha. O segundo é menos óbvio e mais grave: o BCrypt é caro *de propósito*, e
justamente por isso um endpoint que o executa sem limite vira uma alavanca de
negação de serviço — algumas centenas de tentativas paralelas consomem a CPU da
aplicação inteira sem que o atacante precise acertar senha nenhuma.

O limite é particionado por origem (`X-Forwarded-For` quando há proxy, IP da
conexão caso contrário), para que o excesso de um cliente não impeça os outros de
autenticar. Não há fila: em endpoint de credencial, segurar a requisição só
prolongaria o custo que se quer evitar. Quem estoura recebe **429** com
`Retry-After`, no mesmo formato ProblemDetails dos demais erros. Os endpoints
protegidos não passam por esse limite — eles já exigem um token válido, e um
token válido custa muito mais caro de obter do que uma tentativa de login.

**A sonda de saúde responde por `/health`, sem token.** Ela não checa só se o
processo está de pé: `AddDbContextCheck` tenta alcançar o banco, porque uma API
que responde mas perdeu a conexão não serve para nada — e é exatamente esse o
estado que um orquestrador precisa enxergar para reciclar a instância. Fica fora
do Swagger e fora da autenticação porque o *probe* do App Service, do Container
Apps ou do Docker não tem token para apresentar; em troca, o corpo não expõe nada
sensível — nome da verificação, status e duração, com a exceção original indo só
para o log.

**Erros saem em ProblemDetails.** O tratamento usa `IExceptionHandler`, do
ASP.NET Core 8, em vez de um middleware de `try/catch` escrito à mão. Com isso os
erros que a aplicação lança e os que o *model binding* gera saem no mesmo formato,
pelo mesmo formatador.

**Campo desconhecido no corpo é recusado.** `UnmappedMemberHandling.Disallow`
faz um `{"amout": 100}` devolver 400 em vez de gravar o aporte com valor zero.

---

## Preparando o ambiente

### 1. O que precisa estar instalado

| Item | Versão | Como conferir |
|------|--------|---------------|
| SDK do .NET | 10.0 | `dotnet --version` |
| SQL Server | LocalDB, Express, Developer ou container | ver passo 2 |

Os projetos têm `net10.0` como alvo (a Fase 1 foi entregue em .NET 8 e migrada
na Fase 2, junto com a esteira). Quem não quiser instalar o SDK pode rodar tudo
pelo Docker: `docker compose up -d --build` sobe a API e o SQL Server (ver
[Rodando](#rodando)).

Para gerar migrations também é preciso a ferramenta do EF Core, uma vez por
máquina:

```bash
dotnet tool install --global dotnet-ef --version 8.*
```

Se ela já estiver instalada em uma versão antiga:

```bash
dotnet tool update --global dotnet-ef --version 10.*
```

### 2. Escolha o banco

Duas opções. A primeira já vem configurada; a segunda é para quem não usa Windows
ou prefere container.

**Opção A — LocalDB (padrão).** Vem com o Visual Studio e com o
[SQL Server Express](https://www.microsoft.com/sql-server/sql-server-downloads).
Confirme que a instância está no ar:

```bash
sqllocaldb info MSSQLLocalDB
```

Se aparecer `State: Stopped`, suba com `sqllocaldb start MSSQLLocalDB`. Se o
comando não for encontrado, o LocalDB não está instalado — use a opção B.

Nada mais a fazer: a connection string do `appsettings.json` já aponta para
`(localdb)\MSSQLLocalDB`, banco `PortfolioDb`.

**Opção B — SQL Server em container.**

```bash
docker compose up -d sqlserver
```

(Só o serviço `sqlserver`; sem o nome, o compose sobe também a API em container —
veja [Rodando](#rodando).) Espere o healthcheck ficar `healthy` (`docker compose ps`) e então aponte a API
para o container por variável de ambiente. No PowerShell:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost,1433;Database=PortfolioDb;User Id=sa;Password=Portfolio@2026;TrustServerCertificate=True"
```

No bash:

```bash
export ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=PortfolioDb;User Id=sa;Password=Portfolio@2026;TrustServerCertificate=True"
```

A variável vale só para o terminal em que foi definida — é ali que a API precisa
ser iniciada.

### 3. Restaure e compile

```bash
dotnet restore
```

```bash
dotnet build
```

### 4. Confira que está tudo de pé

```bash
dotnet test
```

Devem passar 94 testes, e eles não precisam de banco nenhum: os de integração
sobem a API com o provedor InMemory do EF Core.

Você **não** precisa criar o banco nem rodar migration à mão. Com
`Database:AutoMigrate` ligado (o padrão), a API aplica as migrations pendentes e
insere as cinco classes de ativo na primeira subida.

---

## Rodando

```bash
dotnet run --project src/Portfolio.WebApi --launch-profile http
```

A API sobe em **http://localhost:5080** e o navegador abre direto no Swagger:
**http://localhost:5080/swagger**.

Para usar os endpoints protegidos pelo Swagger: chame `POST /auth`, copie o valor
de `accessToken` da resposta, clique em **Authorize** no topo da página e cole
apenas o token, sem escrever `Bearer` na frente.

O perfil `https` também existe (`--launch-profile https`, porta 7080), mas exige
o certificado de desenvolvimento confiável — `dotnet dev-certs https --trust`.

Enquanto a API roda, o terminal mostra uma linha por requisição:

```
19:01:41 info: Portfolio.WebApi.Middleware.RequestTimingMiddleware[0] POST /investments respondeu 201 em 42 ms
```

Requisições acima de 1 segundo saem como `warn`, para aparecerem na triagem.

### Tudo em container

Sem SDK nem SQL Server instalados — só Docker:

```bash
docker compose up -d --build
```

O compose constrói a imagem da API pelo [`Dockerfile`](Dockerfile) (multi-stage:
SDK só no estágio de build, imagem final com o runtime do ASP.NET Core), sobe o
SQL Server, espera o healthcheck dele e então sobe a API, que aplica as migrations
sozinha. Mesma porta: **http://localhost:5080/swagger**. Para acompanhar:

```bash
docker compose logs -f api
```

`docker compose down` para tudo; `docker compose down -v` também apaga o volume
do banco. É esse mesmo arquivo que o CD usa em produção, trocando a construção
local pela imagem publicada pelo CI (variável `API_IMAGE`).

---

## O banco de dados

Três tabelas, criadas pela migration `EsquemaInicial`.

**Investors**

| Coluna | Tipo | Observação |
|--------|------|------------|
| Id | `uniqueidentifier` | PK, gerada na aplicação |
| FullName | `nvarchar(120)` | obrigatório |
| Email | `nvarchar(160)` | obrigatório, índice único `UX_Investors_Email` |
| PasswordHash | `varchar(100)` | hash BCrypt, 60 caracteres |
| RegisteredAt | `datetime2` | UTC |

**AssetClasses**

| Coluna | Tipo | Observação |
|--------|------|------------|
| Id | `int identity` | PK |
| Name | `nvarchar(60)` | como foi digitado, com acento |
| Slug | `varchar(60)` | forma canônica, índice único `UX_AssetClasses_Slug` |
| CreatedAt / ChangedAt | `datetime2` | UTC; `ChangedAt` nulo até o primeiro rename |

Já vem com cinco linhas: Ações, Renda Fixa, Fundos Imobiliários, Tesouro Direto e
Criptomoedas.

**Investments**

| Coluna | Tipo | Observação |
|--------|------|------------|
| Id | `uniqueidentifier` | PK |
| InvestorId | `uniqueidentifier` | FK para Investors, `ON DELETE CASCADE` |
| AssetClassId | `int` | FK para AssetClasses, `ON DELETE NO ACTION` |
| Amount | `decimal(18,2)` | dinheiro, nunca `float` |
| InvestedOn | `date` | sem hora nem fuso |
| Institution | `nvarchar(80)` | opcional |
| Notes | `nvarchar(240)` | opcional |
| CreatedAt / ChangedAt | `datetime2` | UTC |

Índice `IX_Investments_Investor_InvestedOn` em `(InvestorId, InvestedOn)`: toda
consulta de aporte começa filtrando pelo investidor, e a listagem ordena pela
data, então o índice composto atende as duas coisas.

As duas FKs têm comportamento diferente de propósito. Apagar um investidor leva
os aportes dele junto (`Cascade`) — são dados dele e não fazem sentido sozinhos.
Apagar uma classe de ativo em uso é bloqueado (`Restrict`), porque aí os aportes
de outras pessoas ficariam órfãos.

As datas `datetime2` passam por um conversor que grava e lê sempre em UTC. Sem ele
a coluna do SQL Server perde o `Kind` do `DateTime` e o JSON sairia sem o `Z` no
fim, fazendo o cliente ler UTC como hora local.

### Migrations

Para gerar uma nova depois de mexer em entidade ou mapeamento:

```bash
dotnet ef migrations add NomeDaMigration -p src/Portfolio.Infra -s src/Portfolio.WebApi -o Persistence/Migrations
```

Para aplicar à mão (a API já faz isso sozinha na subida):

```bash
dotnet ef database update -p src/Portfolio.Infra -s src/Portfolio.WebApi
```

O histórico fica na tabela `__MigrationsHistory`.

---

## Endpoints

| Método | Rota | Token | O que faz |
|--------|------|:-----:|-----------|
| `POST` | `/users` | — | Cadastra o investidor |
| `POST` | `/auth` | — | Autentica e devolve o token |
| `GET` | `/users/me` | sim | Mostra o dono do token |
| `GET` | `/investments` | sim | Lista os aportes, do mais recente para o mais antigo |
| `GET` | `/investments/summary` | sim | Total aplicado e participação por classe |
| `GET` | `/investments/{id}` | sim | Um aporte do investidor autenticado |
| `POST` | `/investments` | sim | Registra um aporte |
| `PUT` | `/investments/{id}` | sim | Substitui os dados de um aporte |
| `DELETE` | `/investments/{id}` | sim | Remove um aporte |
| `GET` | `/asset-classes` | sim | Lista as classes, com a contagem de aportes de cada uma |
| `GET` | `/asset-classes/{id}` | sim | Uma classe pelo id |
| `POST` | `/asset-classes` | sim | Cadastra uma classe |
| `PUT` | `/asset-classes/{id}` | sim | Renomeia uma classe |
| `DELETE` | `/asset-classes/{id}` | sim | Remove uma classe sem aportes vinculados |
| `GET` | `/health` | — | Sonda de saúde: responde se a aplicação e o banco estão de pé |

As requisições prontas, na ordem da demonstração, estão em
[`src/Portfolio.WebApi/Portfolio.http`](src/Portfolio.WebApi/Portfolio.http) —
abre no Visual Studio ou no VS Code com a extensão REST Client.

### Cadastro e login

```http
POST /users
Content-Type: application/json

{
  "fullName": "Marcio Rodrigues",
  "email": "marcio@exemplo.com",
  "password": "Senha@12345"
}
```

```json
201 Created
{
  "id": "b43909c9-9de0-4bf3-bf2e-38e8d1957ec1",
  "fullName": "Marcio Rodrigues",
  "email": "marcio@exemplo.com",
  "registeredAt": "2026-09-13T22:01:40.5759777Z"
}
```

O e-mail é normalizado para minúsculas antes de gravar, então `Marcio@Exemplo.com`
e `marcio@exemplo.com` são o mesmo cadastro — o segundo recebe 409. A senha tem
mínimo de 8 caracteres e máximo de 72, que é o limite que o BCrypt considera.

```http
POST /auth
Content-Type: application/json

{ "email": "marcio@exemplo.com", "password": "Senha@12345" }
```

```json
200 OK
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "tokenType": "Bearer",
  "expiresAtUtc": "2026-09-14T00:01:40Z",
  "investor": { "id": "b43909c9-...", "fullName": "Marcio Rodrigues", "email": "marcio@exemplo.com" }
}
```

Nos endpoints protegidos, mande o header:

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### Aportes

```http
POST /investments
Authorization: Bearer <token>
Content-Type: application/json

{
  "assetClass": "Ações",
  "amount": 1500.75,
  "investedOn": "2026-09-01",
  "institution": "XP",
  "notes": "PETR4"
}
```

```json
201 Created
{
  "id": "fb6f86b7-2dfa-4d5d-b218-6af93992092e",
  "assetClass": { "id": 1, "name": "Ações", "slug": "acoes" },
  "amount": 1500.75,
  "investedOn": "2026-09-01",
  "institution": "XP",
  "notes": "PETR4",
  "createdAt": "2026-09-13T22:01:41.9889201Z"
}
```

`assetClass`, `amount` e `investedOn` são obrigatórios; `institution` e `notes`,
não. Campos opcionais vazios saem da resposta em vez de virar `null` — por isso o
exemplo acima não tem `changedAt`.

Em `assetClass` vale o nome ou o slug, com ou sem acento, em qualquer caixa:
`"Ações"`, `"ACOES"`, `"acoes"` e `"Tesouro Direto"`, `"tesouro-direto"` todos
funcionam. Um nome que não existe devolve 400 com a lista do que existe:

```json
400 Bad Request
{
  "title": "Requisicao invalida",
  "status": 400,
  "detail": "A classe de ativo 'Previdencia' nao existe. Disponiveis: Ações, Criptomoedas, Fundos Imobiliários, Renda Fixa, Tesouro Direto. Para incluir outra, use POST /asset-classes."
}
```

### Resumo da carteira

```http
GET /investments/summary
Authorization: Bearer <token>
```

```json
200 OK
{
  "totalInvested": 2000.75,
  "count": 2,
  "byAssetClass": [
    { "assetClass": "Ações", "count": 1, "total": 1500.75, "share": 75.01 },
    { "assetClass": "Tesouro Direto", "count": 1, "total": 500.00, "share": 24.99 }
  ]
}
```

A soma e o agrupamento acontecem no banco (`GROUP BY`), não em memória, e
consideram apenas os aportes de quem está autenticado.

### Classes de ativo

São dados de referência compartilhados: a lista é a mesma para todos os
investidores, e qualquer um autenticado pode consultá-la e mantê-la.

```http
POST /asset-classes
Authorization: Bearer <token>
Content-Type: application/json

{ "name": "Previdência Privada" }
```

```json
201 Created
{
  "id": 6,
  "name": "Previdência Privada",
  "slug": "previdencia-privada",
  "investmentCount": 0,
  "createdAt": "2026-09-13T22:03:30.1854822Z"
}
```

Cadastrar `"previdencia privada"` depois disso devolve 409: o slug é o mesmo.

`investmentCount` mostra quantos aportes referenciam a classe — é ele que explica
por que um `DELETE` pode voltar 409:

```json
409 Conflict
{
  "title": "Conflito",
  "status": 409,
  "detail": "'Previdência Privada' esta em uso por 1 aporte(s) e nao pode ser removida."
}
```

---

## Erros

Toda resposta de erro segue o formato ProblemDetails (RFC 9457), com
`Content-Type: application/problem+json`.

| Status | Quando acontece |
|:------:|-----------------|
| 400 | Campo obrigatório ausente, valor fora da faixa, data no futuro, campo desconhecido no corpo, classe de ativo inexistente |
| 401 | Token ausente, expirado ou inválido; e-mail ou senha incorretos |
| 404 | Aporte ou classe que não existe — ou aporte que pertence a outro investidor |
| 409 | E-mail já cadastrado, classe de ativo equivalente já existente, classe em uso |
| 429 | Tentativas demais em `/auth` ou `/users` a partir da mesma origem |
| 500 | Falha não prevista |

O 500 é o único que não devolve a mensagem original: o cliente recebe um texto
genérico e o detalhe vai para o log. Mensagem de exceção interna costuma carregar
nome de tabela, de constraint ou caminho de arquivo, e isso não deve sair da
aplicação.

O mapeamento fica em
[`ApiExceptionHandler`](src/Portfolio.WebApi/Errors/ApiExceptionHandler.cs):

| Exceção | Status |
|---------|:------:|
| `BusinessRuleException` (domínio) | 400 |
| `ResourceNotFoundException` | 404 |
| `DuplicateResourceException` | 409 |
| `InvalidCredentialsException` | 401 |
| qualquer outra | 500 |

---

## Testes

```bash
dotnet test
```

97 testes no total, divididos em dois projetos. Nenhum deles precisa de SQL Server.

**`Portfolio.UnitTests` — 59 testes.** Regras de domínio e casos de uso, com os
repositórios substituídos por dublês (NSubstitute). Cobre valor maior que zero e
dentro do limite da coluna, arredondamento para duas casas, data obrigatória e não
futura, limite dos campos de texto, campo opcional em branco virando nulo,
normalização de e-mail, geração de slug, e o comportamento dos serviços: o que
grava, o que recusa e o que não chega a commitar.

Dois testes valem destaque porque checam o *contrário* do caminho feliz:
`Revise_invalido_nao_deve_deixar_o_aporte_em_estado_quebrado` garante que uma
alteração recusada não deixa a entidade meio-modificada, e
`SignIn_de_email_inexistente_deve_dar_a_mesma_mensagem_da_senha_errada` compara as
duas mensagens de erro para garantir que ninguém consegue descobrir quais e-mails
existem.

**`Portfolio.IntegrationTests` — 38 testes.** A API de verdade — pipeline,
autenticação, roteamento, serialização, EF Core — subindo com
`WebApplicationFactory`, com o SQL Server trocado pelo provedor InMemory. Cobre o
ciclo completo dos aportes e das classes de ativo, os 401 sem token, o 409 de
e-mail duplicado, o isolamento entre dois investidores, o `Location` dos 201
apontando para rota que existe de fato, e que a resposta do cadastro nunca contém
o hash da senha.

Três deles cuidam do limite de tentativas e da sonda de saúde: que `/auth` passa a
responder 429 com `Retry-After` depois do limite, que `/health` responde sem token
e reporta o banco, e — o mais importante dos três — que o limite **não** alcança os
endpoints protegidos, que continuam respondendo normalmente com a cota de
credenciais já esgotada.

Para rodar só um dos projetos:

```bash
dotnet test tests/Portfolio.UnitTests
```

Para rodar um teste específico:

```bash
dotnet test --filter "FullyQualifiedName~Ciclo_completo_de_vida_de_um_aporte"
```

---

## Configuração

Tudo vive em [`appsettings.json`](src/Portfolio.WebApi/appsettings.json) e pode ser
sobrescrito por variável de ambiente, trocando `:` por `__` (dois sublinhados).

| Chave | Padrão | Para que serve |
|-------|--------|----------------|
| `ConnectionStrings:DefaultConnection` | LocalDB, banco `PortfolioDb` | Onde o banco está |
| `Database:AutoMigrate` | `true` | Aplicar migrations pendentes na subida |
| `Jwt:Issuer` | `portfolio-api` | Quem emitiu o token |
| `Jwt:Audience` | `portfolio-app` | Para quem o token vale |
| `Jwt:Secret` | valor de exemplo | Chave de assinatura HMAC-SHA256 |
| `Jwt:LifetimeMinutes` | `120` | Validade do token, entre 5 e 1440 |
| `RateLimiting:PermitLimit` | `10` | Tentativas em `/auth` e `/users` por origem, a cada janela |
| `RateLimiting:WindowSeconds` | `60` | Tamanho da janela do limite, em segundos |

Sobre a chave de assinatura: o valor do `appsettings.json` serve para desenvolver
e **não deve ir para nenhum ambiente compartilhado**. Ele está versionado de
propósito, para o projeto rodar logo após o clone. Em qualquer outro lugar, defina
por variável de ambiente:

```bash
export Jwt__Secret="uma-chave-longa-aleatoria-com-pelo-menos-32-caracteres"
```

A chave tem mínimo de 32 caracteres, porque é o que o HMAC-SHA256 exige para uma
chave de 256 bits. Se for mais curta, a aplicação **não sobe** — a validação roda
na inicialização (`ValidateOnStart`), e falhar no boot é melhor do que emitir
tokens fracos em silêncio.

`Database:AutoMigrate` é conveniente para a demonstração e para o container.
Em produção a migration seria um passo separado do deploy, e não algo que a
aplicação faz sozinha ao subir.

---

## CI/CD

Entrega da Fase 2. A esteira é feita com **GitHub Actions** em dois workflows.
O artefato de deploy é uma **imagem Docker** publicada no GitHub Container
Registry (GHCR), e o ambiente de produção é o **Docker da máquina local**,
alimentado por um runner self-hosted — uma das três opções que o enunciado admite
("Azure, outra cloud ou a sua máquina"). A mesma imagem subiria sem mudança em
qualquer serviço de containers (Azure Container Apps, App Service for
Containers), bastando trocar o passo de deploy.

```
push na main ──► CI (ubuntu-latest)                         ──► CD (runner self-hosted, Windows)
                 job 1: restore → build → test                  docker pull ghcr.io/.../portfolio-api:1.0.N
                 job 2: docker build → push no GHCR             docker compose up -d (API + SQL Server)
                 artefato: ghcr.io/mlrneves/portfolio-api:1.0.N smoke test em GET /health
```

### O workflow de CI — [`.github/workflows/ci.yml`](.github/workflows/ci.yml)

Dispara em todo push e pull request para `main` (e manualmente, por
*workflow_dispatch*). Roda em runners Linux hospedados pelo GitHub, em dois jobs
encadeados:

**Job `build-and-test`**

1. `dotnet restore` e `dotnet build` em Release, com a versão `1.0.<número do run>`
   gravada nas DLLs — é como se confere, no destino, qual build está no ar.
2. `dotnet test` dos dois projetos, gerando `.trx`. O resultado aparece como um
   *check* no commit (relatório de testes) e os arquivos brutos ficam no artefato
   `test-results`. Se um teste falha, o workflow falha: a imagem não é gerada e
   **o CD não roda**.

**Job `build-image`** (só depois do anterior passar)

3. `docker build` pelo [`Dockerfile`](Dockerfile), passando a versão e o SHA do
   commit como *build args*; o `dotnet publish` acontece dentro do estágio de
   build da imagem.
4. Push para o GHCR com três tags: `1.0.<run>` (a que o CD usa), `sha-<commit>` e
   `latest` (só na `main`). Em pull request a imagem é construída, mas não
   publicada — serve para provar que o Dockerfile continua válido.

### O workflow de CD — [`.github/workflows/cd.yml`](.github/workflows/cd.yml)

Dispara por `workflow_run`: quando o CI **termina com sucesso na `main`**. Um CI
vermelho ou um CI de pull request não publica nada. Roda no runner self-hosted
(`runs-on: [self-hosted, Windows]`), dentro do *environment* `production`, e:

1. Valida que o secret `PROD_JWT_SECRET` existe e tem ao menos 32 caracteres —
   antes de derrubar qualquer coisa.
2. Faz login no GHCR com o `GITHUB_TOKEN` e baixa **a imagem do run de CI que o
   disparou** (`1.0.<run>`), e não "a última", para não publicar um build
   diferente do que foi testado.
3. `docker compose up -d --no-build` com `API_IMAGE` apontando para essa tag.
   O compose troca o container da API pelo novo; o SQL Server e o volume de dados
   continuam onde estão. Na primeira subida a API cria o banco e aplica as
   migrations.
4. Executa [`deploy/Smoke-Test.ps1`](deploy/Smoke-Test.ps1): espera `GET /health`
   responder 200 (até 2 minutos, por causa do SQL Server e das migrations) e
   imprime o corpo. Se não responder, mostra os logs do container e marca o deploy
   como falho.

Os segredos **não ficam no repositório nem na imagem**: entram pelo
*Settings → Secrets and variables → Actions* e chegam ao container como variáveis
de ambiente, pelo compose.

| Secret | Obrigatório | Para que serve |
|--------|-------------|----------------|
| `PROD_JWT_SECRET` | sim | `Jwt:Secret` de produção, com pelo menos 32 caracteres. O deploy falha sem ele, de propósito: é a chave que assina os tokens |
| `PROD_MSSQL_PASSWORD` | não | Senha do `sa` do SQL Server em container. Sem ele, usa a de desenvolvimento do compose |

### Preparando a máquina (uma vez só)

1. **Docker Desktop** instalado e rodando (é ele que executa a API e o banco).
2. **Runner self-hosted.** No GitHub: *Settings → Actions → Runners → New
   self-hosted runner → Windows*, e seguir os comandos que a página mostra
   (download, `config.cmd` com o token). O runner precisa ter o label `Windows`
   (vem por padrão no runner Windows); é o que o `runs-on` do CD procura. Para o
   vídeo basta `.\run.cmd` em um terminal comum; para deixar permanente,
   `config.cmd --runasservice`. O usuário do runner precisa conseguir chamar o
   `docker` (estar no grupo `docker-users`). Não precisa de administrador.
3. Cadastrar o secret `PROD_JWT_SECRET` no repositório. Um jeito rápido de gerar:

   ```powershell
   -join ((48..57)+(65..90)+(97..122) | Get-Random -Count 48 | % {[char]$_})
   ```

   Ou pela CLI: `gh secret set PROD_JWT_SECRET`.
4. Na primeira execução do CI, o pacote `portfolio-api` aparece em *Packages* do
   perfil. Ele já nasce vinculado ao repositório (label
   `org.opencontainers.image.source`), então o `GITHUB_TOKEN` do CD consegue
   baixá-lo sem configuração extra.

Depois disso, qualquer push na `main` percorre a esteira inteira e termina com a
API nova respondendo em http://localhost:5080/swagger.

### Roteiro do vídeo da Fase 2

1. **Mostrar os dois workflows** no repositório e explicar o gatilho de cada um:
   `push`/`pull_request` no CI, `workflow_run` do CI no CD. Mostrar o
   `Dockerfile` e o serviço `api` do `docker-compose.yml`.
2. **Mostrar o runner** on-line em *Settings → Actions → Runners* e o estado atual
   da produção: `docker compose ps` e o `/health` respondendo. Anotar a imagem que
   está no ar: `docker inspect portfolio-api --format '{{.Config.Image}}'`.
3. **Fazer uma mudança visível** — por exemplo, trocar o `DocumentTitle` do
   Swagger no `Program.cs` — e dar push na `main`.
4. **Acompanhar o CI** na aba *Actions*: compilação, os 97 testes passando no
   relatório, a imagem publicada com a tag `1.0.<run>` no resumo do job.
5. **Mostrar o CD disparando sozinho** assim que o CI fica verde: o `docker pull`
   da tag exata, o `docker compose up`, o smoke test com o `/health` em 200.
6. **Abrir http://localhost:5080/swagger** e mostrar a mudança no ar; repetir o
   `docker inspect` e mostrar a tag nova.
7. **Mostrar que o CD é condicionado ao CI**: abrir um pull request com um teste
   quebrado (ou um commit que não compila) e mostrar o CI vermelho, sem imagem
   publicada e sem CD.

---

## Roteiro do vídeo de demonstração

Os números são os passos do arquivo
[`Portfolio.http`](src/Portfolio.WebApi/Portfolio.http), que já está na ordem.

1. **Mostrar o projeto.** As quatro camadas na solução e a dependência apontando
   para dentro. Abrir `Investment.cs` e mostrar as validações no construtor.
2. **Subir a API** com `dotnet run` e mostrar o log aplicando as migrations na
   primeira execução. Abrir o Swagger.
3. **Cadastro** — `POST /users` → 201 *(passo 1)*.
4. **Autenticação** — `POST /auth` → 200 com o `accessToken` *(passo 2)*.
5. **Sem token não passa** — `GET /investments` → 401 *(passo 3)*.
6. **Autorizar** — clicar em *Authorize* no Swagger e colar o token, ou preencher
   a variável `@token` no arquivo `.http`. Conferir com `GET /users/me` → 200
   *(passo 4)*.
7. **Criar** — `POST /investments` → 201 *(passos 5 e 6)*.
8. **Listar** — `GET /investments` → 200 com os dois aportes *(passo 7)*.
9. **Resumir** — `GET /investments/summary` → total e participação por classe
   *(passo 8)*.
10. **Alterar** — `PUT /investments/{id}` → 200, apontar o `changedAt` que apareceu
    *(passo 10)*.
11. **Remover** — `DELETE /investments/{id}` → 204; repetir a mesma chamada → 404
    *(passos 11 e 12)*.
12. **Validação** — aporte sem data → 400, com data futura → 400, com classe
    inexistente → 400 já listando as disponíveis *(passos 13 a 15)*.
13. **Classes de ativo** — cadastrar "Previdência Privada" → 201, tentar
    "previdencia privada" → 409, usar a classe nova em um aporte → 201, tentar
    apagá-la em uso → 409 *(passos 16 a 20)*.
14. **Conflitos de cadastro** — mesmo e-mail duas vezes → 409, senha errada → 401
    *(passos 22 e 23)*.
15. **A senha no banco.** Abrir a tabela `Investors` e mostrar que a coluna
    `PasswordHash` guarda um hash BCrypt de 60 caracteres, começando por `$2a$12$`,
    e não a senha:

    ```sql
    SELECT FullName, Email, PasswordHash, LEN(PasswordHash) AS Tamanho
    FROM Investors;
    ```

16. **Limite de tentativas** — repetir `POST /auth` com senha errada umas doze vezes
    seguidas: as primeiras voltam 401 e, passado o limite, a resposta vira **429**
    com o cabeçalho `Retry-After`. Vale dizer em voz alta por que isso existe: sem
    o limite, o custo proposital do BCrypt viraria alavanca de negação de serviço.
17. **Saúde** — `GET /health` sem token: 200 com o estado da aplicação e do banco.
18. **Testes** — rodar `dotnet test` e mostrar os 97 passando.

Um detalhe que rende na gravação: crie dois investidores, registre um aporte com o
primeiro e tente lê-lo com o token do segundo. A resposta é 404, e isso mostra o
controle de acesso funcionando de verdade — não é só o `[Authorize]` na porta, é
o filtro por investidor dentro de cada consulta.

---

## Quando algo não funciona

**`ERR_CONNECTION_REFUSED` em localhost:5080.** A API não está rodando. Ela
precisa de um terminal aberto com `dotnet run` — se você fechou o terminal ou
apertou Ctrl+C, o processo morreu.

**A aplicação não sobe e reclama de `Jwt:Secret`.** A chave tem menos de 32
caracteres, ou a variável `Jwt__Secret` foi definida com um valor curto e está
sobrescrevendo o `appsettings.json`. A validação é na inicialização, de propósito.

**`A network-related or instance-specific error occurred while establishing a
connection to SQL Server`.** O banco não está acessível.

- No LocalDB: rode `sqllocaldb info MSSQLLocalDB`. Se o estado for `Stopped`,
  `sqllocaldb start MSSQLLocalDB`.
- No container: `docker compose ps` para ver se está `healthy`, e confirme que a
  variável `ConnectionStrings__DefaultConnection` está definida **no mesmo
  terminal** em que a API foi iniciada.

**Todo endpoint protegido devolve 401 mesmo com o token.** Três causas comuns, em
ordem de frequência: o token foi colado com o prefixo `Bearer ` dentro do campo do
Swagger (ali vai só o token); o token passou da validade (`Jwt:LifetimeMinutes`,
2 horas por padrão) — basta chamar `POST /auth` outra vez; ou a API foi reiniciada
com outra `Jwt:Secret`, o que invalida os tokens emitidos antes.

**`GET /investments/{id}` devolve 404 e você tem certeza de que o aporte existe.**
Provavelmente ele é de outro investidor. Confira de quem é o token com
`GET /users/me`. Isso é o comportamento correto, não um defeito.

**O `POST /investments` devolve 400 dizendo que a classe de ativo não existe.**
A mensagem já lista os nomes válidos. Se o nome que você quer não está lá,
cadastre com `POST /asset-classes`.

**`dotnet ef` não é reconhecido.** A ferramenta não está instalada:
`dotnet tool install --global dotnet-ef --version 8.*`. Depois de instalar, abra
um terminal novo para o PATH ser recarregado.

**Migration aparece como pendente mesmo depois do `database update`.** Acontece
quando o `-s` (startup project) aponta para outro projeto e o EF Core lê outra
connection string. Use sempre `-p src/Portfolio.Infra -s src/Portfolio.WebApi`.

**Quero começar do zero.** Apague o banco e deixe a API recriá-lo na próxima
subida:

```sql
DROP DATABASE PortfolioDb;
```

No container, `docker compose down -v` remove o volume junto.

---

## Onde cada assunto da fase aparece no código

| Assunto | Onde está |
|---------|-----------|
| Desenvolvimento de API com .NET | `src/Portfolio.WebApi/Controllers/` — três controllers, respostas tipadas e códigos de status declarados |
| Middlewares | `Middleware/RequestTimingMiddleware.cs` (log e duração) e `Errors/ApiExceptionHandler.cs` (`IExceptionHandler`) |
| Injeção de dependência | `Portfolio.UseCases/DependencyInjection.cs` e `Portfolio.Infra/DependencyInjection.cs`; o `Program.cs` só escolhe as peças |
| Logs | `ILogger` nos serviços, no middleware e no tratador de erros; requisição lenta sai como `warn` |
| Serialização de dados | `Setup/JsonSetup.cs` — camelCase, omissão de nulos e recusa de campo desconhecido |
| Autenticação e autorização | JWT Bearer em `Setup/AuthenticationSetup.cs`, emissão em `Infra/Identity/`, `[Authorize]` nos controllers, id do investidor lido da claim `sub` |
| Documentação da API | Swagger com botão *Authorize* em `Setup/SwaggerSetup.cs`, alimentado pelos comentários XML |
| Entity Framework Core | `Infra/Persistence/` — `DbContext`, `IEntityTypeConfiguration` por entidade, conversores de valor, migrations, índices e `GROUP BY` no banco |
| Análise de projetos com DDD | Entidades que protegem as próprias invariantes, interfaces de repositório no domínio, `IUnitOfWork` e separação em camadas |
| Qualidade de software e pirâmide de testes | 59 testes de unidade e 35 de integração, em projetos separados |
| Segurança | BCrypt work factor 12, e-mail normalizado, mensagem de login uniforme, 404 em vez de 403, filtro por investidor em toda consulta, limite de tentativas em `/auth`, detalhe de erro interno fora da resposta |
| CI/CD (Fase 2) | `.github/workflows/ci.yml` (build, testes, imagem Docker no GHCR) e `cd.yml` (deploy via `docker compose` após o CI, por `workflow_run`); `Dockerfile`, `docker-compose.yml` e `deploy/Smoke-Test.ps1` |
