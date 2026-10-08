# Controle de Frotas — primeira versão

Aplicação Angular + ASP.NET Core .NET 10 + SQL Server para começar o controle de veículos leves. Projeto novo, independente do Gestão Estoque.

## O que funciona nesta versão
- Login com cookie HttpOnly, senha com hash e proteção de formulários contra CSRF.
- Um administrador e uma empresa inicial (identificador 1).
- Cadastro, pesquisa, paginação visual, edição em modal e inativação de veículos e motoristas.
- Placas antigas e Mercosul, CPF com dígitos verificadores, CNH com validação de formato (não consulta órgão emissor).
- Aviso de CNH vencida, hodômetro com três casas decimais e exibição pt-BR.
- Histórico de alterações e proteção contra edições concorrentes por rowversion.

## Pré-requisitos no Windows
Instale o SDK .NET 10, Node.js compatível com Angular 21 (Node 24 LTS recomendado) e SQL Server Express. Confira `dotnet --version`, `node --version` e `npm --version`. O Visual Studio é opcional: pode usar VS Code e PowerShell.

## 1. Extrair e configurar o SQL Server
Extraia o ZIP para `C:\Projetos`, resultando em `C:\Projetos\ControleFrotas`.

A conexão padrão em `backend/appsettings.json` usa `.\SQLEXPRESS` e autenticação Windows. Se sua instância for diferente, altere somente o servidor. Exemplo: `DESKTOP-F05RB99\SQLEXPRESS` (no JSON, escreva `DESKTOP-F05RB99\\SQLEXPRESS`).

A API cria um banco novo chamado **ControleFrotas** na primeira execução. Seu usuário Windows precisa ter permissão para criá-lo. Não configure a conexão para o banco do estoque ou o banco legado. Esta versão usa `EnsureCreated` para inicialização; a evolução do esquema exigirá migrações antes de novas versões.

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
dotnet build .\backend\ControleFrotas.Api.csproj
dotnet run --project .\tests\ControleFrotas.Checks.csproj
cd frontend
npm run build
```
As verificações de regras são um programa de testes sem dependência de SQL. Não substituem testes de integração da API.

## Roteiro inicial de teste
1. Entrar com o administrador; senha errada deve ser rejeitada.
2. Novo veículo: placa ABC1D23, categoria Carro, marca/modelo de teste, ano 2024, hodômetro 12000.125.
3. Confirmar cadastro, pesquisar pela placa e editar o modelo.
4. Tentar placa duplicada; API deve bloquear.
5. Tentar reduzir o hodômetro; API deve bloquear.
6. Desmarcar “Cadastro ativo” na edição; consultar removendo “Somente ativos”.
7. Cadastrar motorista com dados de teste autorizados; CPF inválido deve ser bloqueado. Nunca usar dados pessoais de terceiros sem autorização.
8. Conferir histórico e sair. Atualizar a página não deve manter acesso após logout.
9. Em duas abas, editar o mesmo cadastro: a segunda gravação deve ser rejeitada se a versão estiver desatualizada.

## Limites e próximos ciclos
Ainda não estão implementados: gestão de empresas/unidades, outros usuários/perfis, recuperação de senha, documentos/anexos, abastecimentos, viagens, manutenção, GPS e importação do legado. Todas as contas iniciais são administradores; não há tela para criar outras contas. A empresa é um identificador preparatório, sem cadastro de razão social.

Os indicadores são contagens reais dos cadastros; disponibilidade e custo/km ainda não são calculados. O frontend busca os registros e pagina localmente; paginação no servidor será necessária com maior volume.

Uso local para avaliação. Publicação requer HTTPS, configuração adequada de cookie Secure, conexão protegida e migrações versionadas. Nenhum certificado, credencial ou banco do legado é incluído.
