# MediShop Admin Desktop

Painel administrativo Windows em C# com WPF e .NET 10. Usa a mesma API e as mesmas contas administrativas do site; não acessa PostgreSQL diretamente.

## Download pronto para usar

Baixe o [EXE Windows x64](https://github.com/Luvas-jpeg/PIM4Desktop/releases/download/v1.0.0-demo.20261111/MediShop-Admin-Windows-x64.exe) ou o [pacote ZIP completo](https://github.com/Luvas-jpeg/PIM4Desktop/releases/download/v1.0.0-demo.20261111/MediShop-Admin-Windows-x64.zip). Outras versões ficam em [Releases](https://github.com/Luvas-jpeg/PIM4Desktop/releases).

A API já está configurada para `https://pim4-api-demo.onrender.com/`. Abra no Windows e entre com sua conta Admin. As alterações são realizadas nos dados do Render. O EXE inclui o runtime .NET; o ZIP também fornece `appsettings.json` e informações da compilação. Não há credenciais embutidas.

O visual utiliza controles padrão do WPF, indicadores em texto e formulários compactos.

## Funcionalidades

- Login restrito à role `Admin`, JWT e renovação compartilhada entre consultas concorrentes.
- Logout com limpeza local mesmo sem conexão; revogação do refresh token quando o servidor está disponível.
- Indicadores de cursos ativos, pedidos pendentes, alunos ativos e total dos pedidos pagos.
- Busca local por curso/categoria, nome/e-mail do aluno e cliente/número/situação do pedido.
- Cursos presenciais: cadastro, edição, ativação, arquivamento e restauração.
- Turmas: consulta inclusive de canceladas, cadastro e edição de data, término, local, instrutor, capacidade e situação.
- Alunos da turma: consulta de matrícula e participação, alteração de situação e transferência para outra turma disponível do mesmo curso.
- Pedidos: itens, valores, situação financeira e alteração da situação operacional.
- Alunos: cadastro manual, edição e exclusão com confirmação e validação dos vínculos pela API.
- Formulários com validações locais e apresentação das mensagens de negócio e de validação retornadas pelo servidor.

A API continua responsável por autorização, vagas, transações, auditoria, transições de situação e restrições de exclusão. O cadastro manual de aluno não cria compra nem matrícula paga. A situação geral do aluno pode afetar todas as suas matrículas; a ação em **Turmas → Alunos da turma** identifica uma matrícula específica. Editar o cadastro não transfere vagas entre cursos.

O desktop não confirma pagamentos manualmente. Pedidos não pagos não podem avançar de situação; cancelar um pedido pago exige o fluxo de reembolso disponível no sistema. O gateway real permanece uma integração do backend. Esta evolução cobre a operação de cursos presenciais; não inclui editor EAD, cupons, usuários, auditoria ou execução de reembolso pelo desktop.

## Configuração da API

Em `appsettings.json`, informe a URL **raiz**, sem `/api`:

```json
{
  "Api": {
    "BaseUrl": "https://pim4-api-demo.onrender.com/"
  }
}
```

A configuração padrão acessa o Render. Para desenvolvimento local, use `http://localhost:5278/`. A variável `PIM4_API_BASE_URL` tem precedência sobre o arquivo:

```powershell
$env:PIM4_API_BASE_URL = "http://localhost:5278/"
dotnet run --project PIM4AdminDesktop.csproj
```

HTTP é aceito somente para loopback local. Não coloque credenciais, tokens ou string de conexão na URL/configuração. Não é necessário alterar CORS para este cliente nativo.

Os horários de turmas são informados e exibidos no fuso do Windows; os formulários convertem datas para UTC antes de enviar à API. Valores monetários usam `pt-BR`, por exemplo `199,90`.

## Executar e publicar

É necessário Windows e o SDK .NET 10 para executar a interface WPF:

```powershell
dotnet run --project PIM4AdminDesktop.csproj
dotnet publish PIM4AdminDesktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/desktop
```

Distribua a pasta publicada completa, incluindo `appsettings.json`. Configure a URL de produção antes de entregar. Não há instalador nem atualização automática nesta versão.

## Organização

```text
Configuration/              URL e validação de configuração
Models/                     Contratos de leitura e escrita da API
Services/ApiClient.cs        HTTP, autenticação e operações administrativas
Services/AdminValidation.cs  Validações e rótulos de apresentação
ViewModels/MainViewModel.cs  Sessão, consultas, seleção e filtros
Views/EditorWindow.cs        Formulário modal compartilhado
Views/AdminActions.cs        Formulários de cada operação
Views/ClassStudentsWindow.*  Gestão das matrículas da turma
MainWindow.*                Login, indicadores e navegação administrativa
tests/ApiChecks/             Testes executáveis sem dependências externas
tests/DesktopUiChecks/       Verificações WPF para Windows
```

## Validação automatizada e CI

```powershell
# Multiplataforma: autenticação, concorrência, contratos HTTP e validações
dotnet run --project tests/ApiChecks/ApiChecks.csproj -c Release

# Windows: tela após login, bindings, filtros, formulários e logout
dotnet run --project tests/DesktopUiChecks/DesktopUiChecks.csproj -c Release
```

Os executáveis retornam código diferente de zero diante de uma falha. Usam HTTP controlado em memória; não alteram a API ou o banco de produção. Os testes de contrato verificam rotas, métodos, payloads, manutenção da mensagem de negócio, renovação concorrente, logout offline e durante renovação, bloqueio de usuários não administradores e validações dos campos.

A GitHub Actions executa os testes de API no Linux; em Windows compila o WPF, verifica telas/bindings e publica um pacote `win-x64` como artefato por 14 dias. O pacote deve receber a configuração do ambiente antes da distribuição. Tags `v*` também publicam o EXE e o ZIP no GitHub Releases após os testes. A CI não altera os dados do Render.

Validação desta alteração: os testes de API foram executados localmente e o aplicativo e o projeto de verificação WPF foram compilados. A execução das telas WPF depende de Windows e permanece para a CI/validação nesse sistema.

## Capturas para a documentação

Os prints são gerados manualmente em um Windows com `.\scripts\Capture-Desktop.ps1`. O CI mantém testes e compilação, sem gerar ou enviar capturas. Consulte o [guia de capturas](docs/CAPTURAS-DESKTOP.md).

O cenário manual utiliza dados fictícios e HTTP em memória; ele não altera a API do Render nem comprova persistência. Para documentar uma operação integrada, execute o aplicativo normal em Windows com uma conta e dados de teste.

## Roteiro de homologação

Use uma API de homologação e contas/dados de teste para verificar operações com gravação:

1. Login administrativo, rejeição de conta comum e expiração/renovação de token.
2. Criar/editar curso; arquivar e restaurar; conferir o resultado no site.
3. Criar turma, alterar capacidade preservando reservas e consultar canceladas.
4. Consultar alunos da turma, alterar uma matrícula e transferir para outra turma com vaga.
5. Tentar avançar um pedido não pago e conferir a mensagem da API; alterar um pedido pago de teste.
6. Criar/editar aluno manual, conferir regras de exclusão e confirmar que cadastro não cria compra.
7. Interromper a conexão, atualizar e sair; testar retomada e nova autenticação.

Os testes em memória não comprovam a execução integrada com o Render. Se uma gravação demorar ou falhar por perda de conexão, atualize a lista antes de repetir: o servidor pode já ter recebido a operação.

[Arquitetura do sistema](../PIM4Front/docs/ARQUITETURA.md) · [Arquitetura da API](../PIM4API/docs/ARQUITETURA.md) · [Banco e DER](../PIM4API/docs/BANCO-DE-DADOS.md).
