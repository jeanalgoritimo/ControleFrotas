# Controle de Frotas

Aplicação Angular + ASP.NET Core .NET 10 + SQL Server para começar o controle de veículos leves. Projeto novo, independente do Gestão Estoque.

## O que funciona nesta versão
- Login com cookie HttpOnly, senha com hash e proteção de formulários contra CSRF.
- Um administrador e uma empresa inicial (identificador 1).
- Cadastro, pesquisa, paginação visual, edição em modal e inativação de veículos e motoristas.
- Placas antigas e Mercosul, CPF com dígitos verificadores, CNH com validação de formato (não consulta órgão emissor).
- Aviso de CNH vencida, hodômetro com três casas decimais e exibição pt-BR.
- Histórico de alterações e proteção contra edições concorrentes por rowversion.
- Marcas e modelos com criação/edição em modal, inativação e seletores dependentes no veículo.
- Abastecimentos em modal, totais em reais, filtros e consumo por intervalo entre tanques cheios.

## Pré-requisitos no Windows
Instale o SDK .NET 10, Node.js compatível com Angular 21 (Node 24 LTS recomendado) e SQL Server Express. Confira `dotnet --version`, `node --version` e `npm --version`. O Visual Studio é opcional: pode usar VS Code e PowerShell.

## 1. Extrair e configurar o SQL Server
Extraia o ZIP para `C:\Projetos`, resultando em `C:\Projetos\ControleFrotas`.

A conexão padrão em `backend/ControleFrotas.Api/appsettings.json` usa `.\SQLEXPRESS` e autenticação Windows. Se sua instância for diferente, altere somente o servidor. Exemplo: `DESKTOP-F05RB99\SQLEXPRESS` (no JSON, escreva `DESKTOP-F05RB99\\SQLEXPRESS`).

A API cria um banco novo chamado **ControleFrotas** na primeira execução. Seu usuário Windows precisa ter permissão para criá-lo. Não configure a conexão para o banco do estoque ou o banco legado. Esta versão usa migrations. Ao iniciar, bancos novos são criados e bancos da v0.1 são adotados após validar tabelas, colunas, tipos e índices. Os cadastros existentes não são recriados. Um esquema incompatível interrompe a atualização.

## 2. Iniciar API — terminal 1
```powershell
cd C:\Projetos\ControleFrotas
powershell -ExecutionPolicy Bypass -File .\scripts\iniciar-api.ps1
```
Informe seu usuário e uma senha com pelo menos 12 caracteres. Não existe admin/admin no código. O cadastro inicial só é feito quando não existem contas; reiniciar não modifica a senha já cadastrada.

API: http://localhost:5038. Deixe o terminal aberto. Caso o banco esteja indisponível, a API não inicia e o erro aparece neste terminal.

## 3. Iniciar Angular — terminal 2
```powershell
cd C:\Projetos\ControleFrotas\frontend
npm ci
npm start
```
Abra **http://localhost:4200** e use o administrador criado. O proxy encaminha `/api` à porta 5038. Não é necessário configurar CORS para este fluxo local.

Se houver falha de download no npm, tente `npm ci --maxsockets=1 --fetch-retries=3`. Se houver EPERM, encerre o servidor deste projeto antes de reinstalar dependências.

## 4. Verificar o código
```powershell
cd C:\Projetos\ControleFrotas
dotnet build .\backend\ControleFrotas.slnx
dotnet run --project .\tests\ControleFrotas.Checks.csproj
cd frontend
npm run build
```
As verificações de regras são um programa de testes sem dependência de SQL. Não substituem testes de integração da API.

## Roteiro inicial de teste
1. Entrar com o administrador; senha errada deve ser rejeitada.
2. Em Marcas e modelos, cadastre uma marca e seus modelos. Novo veículo: placa ABC1D23, categoria Carro, selecione a marca/modelo de teste, ano 2024, hodômetro 12000.125.
3. Confirmar cadastro, pesquisar pela placa e editar selecionando outro modelo da mesma marca.
4. Tentar placa duplicada; API deve bloquear.
5. Tentar reduzir o hodômetro; API deve bloquear.
6. Desmarcar “Cadastro ativo” na edição; consultar removendo “Somente ativos”.
7. Cadastrar motorista com dados de teste autorizados; CPF inválido deve ser bloqueado. Nunca usar dados pessoais de terceiros sem autorização.
8. Conferir histórico e sair. Atualizar a página não deve manter acesso após logout.
9. Em duas abas, editar o mesmo cadastro: a segunda gravação deve ser rejeitada se a versão estiver desatualizada.

## Limites e próximos ciclos
Ainda não estão implementados: gestão de empresas/unidades, outros usuários/perfis, recuperação de senha, documentos/anexos, viagens, manutenção, GPS e importação do legado. Todas as contas iniciais são administradores; não há tela para criar outras contas. A empresa é um identificador preparatório, sem cadastro de razão social.

Os indicadores são contagens reais dos cadastros; disponibilidade e custo/km ainda não são calculados. O frontend busca os registros e pagina localmente; paginação no servidor será necessária com maior volume.

Uso local para avaliação. Publicação requer HTTPS, configuração adequada de cookie Secure e conexão protegida. Nenhum certificado, credencial ou banco do legado é incluído.

## Arquitetura

A solução segue a separação usada na Gestão de Estoque:

| Projeto | Responsabilidade |
|---|---|
| ControleFrotas.Domain | Entidades, sem EF ou ASP.NET |
| ControleFrotas.Application | Contratos, validações e casos de uso |
| ControleFrotas.Infrastructure | SQL Server, EF e transações com auditoria |
| ControleFrotas.Api | HTTP, autenticação, CSRF e composição |

Application depende de Domain; Infrastructure implementa IFleetStore da Application. API compõe os serviços. O frontend centraliza HTTP em core e contratos por funcionalidade em features. Login, visão geral, histórico e cadastros ficam em componentes por funcionalidade; App coordena sessão e carregamento.

As rotas e os cadastros da v0.1 são preservados. InitialFleetBaseline cria um banco vazio ou valida/adota o esquema anterior; AddFuelEntries acrescenta a tabela operacional e AddVehicleCatalog cria os cadastros relacionados e vincula os veículos existentes. O baseline não permite downgrade automático para evitar exclusão dos cadastros.

Execute `dotnet build backend/ControleFrotas.slnx` para validar todos os projetos. O GitHub Actions verifica backend, regras e build Angular. Sonar não está configurado: não há declaração de aprovação no Sonar.

GET `/api/health` confirma que a API está iniciada (não é uma verificação de conectividade do banco).

## Abastecimentos

O menu Abastecimentos permite registrar veículo ativo, data, hodômetro, combustível líquido (Gasolina/Etanol/Diesel), litros, preço por litro, posto, documento opcional e tanque cheio. O total é arredondado em centavos no servidor. Volume tem até três casas decimais e preço por litro até quatro. GNV, recarga elétrica, anexos, correção e cancelamento de lançamentos ainda não estão incluídos.

Cada lançamento atualiza o hodômetro e a auditoria na mesma transação. Datas futuras, lançamentos antes do último abastecimento, hodômetro regressivo, veículo inativo e cadastro de outra empresa são bloqueados. A versão do veículo protege contra concorrência. Reenvios da mesma solicitação com os mesmos dados retornam o registro existente, sem duplicar.

O consumo só aparece após um intervalo entre dois tanques cheios: distância dividida pela soma de todos os litros registrados após o primeiro tanque cheio, incluindo abastecimentos parciais e o segundo tanque cheio. O primeiro tanque cheio estabelece a referência. A qualidade do indicador depende de registrar todos os abastecimentos e marcar tanque cheio corretamente. O filtro de período não recalcula intervalos: exibe o consumo apurado no lançamento.

A listagem é paginada no servidor (20 registros), com filtros por veículo/período e totais para o filtro inteiro.

### Atualizar e testar

Pare os dois servidores, atualize o código e inicie `scripts/iniciar-api.ps1`. A API aplica as migrations pendentes antes do login. Faça uma cópia de segurança do banco de avaliação antes da primeira atualização de esquema. Não apague o banco e não execute EnsureCreated ou scripts de criação manual.

1. Cadastre um veículo de teste com hodômetro 1000 km.
2. Registre tanque cheio em 1000 km, 40 litros, preço 6,1234: total R$ 244,94 e consumo ainda sem referência completa.
3. Registre um abastecimento parcial em 1200 km, 20 litros.
4. Registre tanque cheio em 1500 km, 30 litros: consumo de 10,00 km/L (500 km / 50 litros), hodômetro atualizado para 1500.
5. Confira os filtros, totais e o histórico. Tente volume zero, preço negativo, data futura e hodômetro inferior ao atual: as gravações devem ser bloqueadas.

### Integração SQL no CI

GitHub Actions utiliza SQL Server 2022 em um container isolado, com credencial descartável exclusiva de teste. O programa de verificações cria bancos aleatórios Fleet_CI_* e os remove ao terminar. Valida banco novo, adoção da v0.1 com registros, preservação de hash/cadastros/auditoria, execução repetida, consumo, idempotência, concorrência e rollback. Sem FLEET_TEST_SQL, os testes de integração são explicitamente omitidos; as verificações de regras continuam sendo executadas.

Os testes de integração devem usar uma instância isolada com permissão para criar bancos. A connection string fornecida não é usada diretamente como banco de teste; cada cenário usa um novo banco aleatório. Não inclua credenciais reais no repositório.

O pacote System.Security.Cryptography.Xml é fixado na versão 10.0.12 para atualizar a dependência transitiva de design das migrations, mantendo a auditoria NuGet ativa.

## Marcas e modelos

O menu **Marcas e modelos** tem os dois cadastros, com edição em modal, busca de marca e filtro de ativos. Selecione a marca para consultar os modelos dela. No veículo, os campos são seletores: trocar a marca limpa o modelo anterior e consulta `/api/vehicle-models?brandId=...` no servidor. O botão Salvar aguarda os dados e exige as duas seleções.

Nomes de marcas têm até 80 caracteres e modelos até 100. Não é permitido repetir uma marca na mesma empresa nem um modelo na mesma marca, desconsiderando espaços nas extremidades e maiúsculas/minúsculas. Um modelo já cadastrado não muda de marca; cadastre outro modelo na marca correta. A API resolve os nomes pelos identificadores, valida marca/modelo/empresa e o banco também impõe esse vínculo por chave estrangeira composta.

Inativação preserva veículos existentes. Cadastros inativos não podem ser usados em novas associações; editar um veículo sem trocar sua associação inativa continua permitido. Renomear a marca ou modelo atualiza os rótulos dos veículos vinculados na mesma transação com a auditoria. Isso altera a versão desses veículos: uma edição aberta anteriormente precisa atualizar os dados antes de salvar.

Ao iniciar a API, a migration converte as marcas e modelos já preenchidos nos veículos em cadastros, agrupando pela empresa e marca. Os textos originais do veículo e o histórico são preservados. Se um veículo legado tiver marca ou modelo em branco, a atualização para com uma mensagem para corrigir esses registros; não apaga o banco.

### Testar este cadastro

1. Cadastre Toyota com modelos Corolla e Yaris, e Honda com Civic.
2. Abra Novo veículo: ao selecionar Toyota, somente Corolla e Yaris devem aparecer.
3. Selecione Corolla e troque para Honda: o modelo deve ser limpo e a lista deve apresentar Civic.
4. Cadastre o veículo, abra Editar e confira as seleções salvas.
5. Tente repetir ` toyota ` e `corolla` na mesma marca: deve haver bloqueio; o mesmo nome de modelo em outra marca é permitido.
6. Renomeie uma marca/modelo e confira o veículo e a auditoria.
7. Inative um modelo: ele deve sumir de novas seleções e continuar visível no veículo já vinculado.
