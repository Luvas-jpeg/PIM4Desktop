# Capturas da interface para o relatório PIM IV

O gerador usa as janelas e os formulários reais do WPF, renderizados em Windows com `RenderTargetBitmap`. Não há reprodução HTML, imagem desenhada ou montagem de uma interface alternativa. Ele usa respostas HTTP substitutas em memória e dados fictícios; não acessa a API do Render nem grava em banco.

As imagens documentam a interface e a apresentação de respostas de teste. Não comprovam persistência, pagamento ou integração com a API real. Essa identificação deve acompanhar as figuras no relatório.

## Gerar pela GitHub Actions

Depois de enviar o commit ao GitHub, aguarde a execução **Desktop CI**. O job de Windows verifica o aplicativo e gera as capturas. A execução também pode ser iniciada em **Actions → Desktop CI → Run workflow** depois que essa versão do workflow estiver na branch principal.

Na página da execução, baixe o artefato **medishop-desktop-screenshots**, disponível por 30 dias. O pacote inclui PNGs, um README explicativo e `manifest.json` com data, SHA do commit, plataforma, fuso horário, origem dos dados e hash de cada PNG.

O artefato de capturas é enviado mesmo se uma etapa posterior de publicação do executável falhar, desde que a captura tenha produzido arquivos. Uma execução interrompida pode conter só parte das imagens: confira o resultado da etapa e a presença do manifesto.

## Gerar em um Windows local

Na raiz do PIM4Desktop, com Git e SDK .NET 10 disponíveis:

```powershell
.\scripts\Capture-Desktop.ps1
```

Se a política de scripts impedir a execução, é possível chamar o projeto diretamente:

```powershell
dotnet run --project tests/DesktopUiChecks/DesktopUiChecks.csproj -c Release -- --screenshots artifacts/screenshots
```

O script registra a revisão local e indica quando há alterações não commitadas. A chamada direta só registra o commit se `PIM4_CAPTURE_COMMIT` ou `GITHUB_SHA` estiver definido; caso contrário, o manifesto informa que a revisão não foi fornecida.

Para escolher outra pasta:

```powershell
.\scripts\Capture-Desktop.ps1 -OutputDirectory C:\PIM4\Capturas
```

O gerador abre as janelas, percorre as telas, captura seu conteúdo sem a moldura do Windows e fecha as janelas. Formulários que precisam de rolagem recebem uma segunda imagem com o sufixo `-continuacao`. Os campos usam português e valores em reais. As datas de turmas são calculadas para o futuro a cada execução.

## Imagens previstas

| Arquivo                       | Conteúdo                                                                         |
| ----------------------------- | -------------------------------------------------------------------------------- |
| `01-login.png`                | Login administrativo, sem senha exposta.                                         |
| `02-painel-cursos.png`        | Indicadores, busca, cursos e ações de cadastro/edição/arquivamento.              |
| `03-editar-curso.png`         | Formulário real de edição de curso.                                              |
| `04-lista-turmas.png`         | Turmas, capacidade, vagas e situação, incluindo cancelada.                       |
| `05-editar-turma.png`         | Data, término, local, instrutor, capacidade e situação.                          |
| `06-alunos-turma.png`         | Matrículas, situação e confirmação de participação.                              |
| `07-situacao-matricula.png`   | Formulário de alteração da situação de uma matrícula.                            |
| `08-transferir-matricula.png` | Seleção de turma de destino com vaga.                                            |
| `09-lista-pedidos.png`        | Pedidos de demonstração com estados financeiros distintos.                       |
| `10-detalhes-pedido.png`      | Itens, total e situação operacional do pedido.                                   |
| `11-erro-pedido-nao-pago.png` | HTTP 400 de teste apresentado pelo formulário ao tentar avançar pedido não pago. |
| `12-lista-alunos.png`         | Cadastros e ações disponíveis para os alunos.                                    |
| `13-editar-aluno.png`         | Formulário real de edição do cadastro.                                           |

As janelas são abertas pelos mesmos métodos `AdminActions` usados pelos botões do aplicativo. O gerador fecha os formulários sem salvar. A única tentativa de alteração é a rejeição controlada do pedido não pago, tratada exclusivamente pelo HTTP substituto. O cenário não simula sucesso de gravação na API real.

## Como usar no DOCX

Substitua a Figura 9 antiga por capturas dessa revisão e acrescente as telas que demonstram a evolução administrativa. Organize pares de imagens relacionadas em figuras separadas, preservando a legibilidade. Não atribua ao aplicativo as abas de cupons, auditoria ou conteúdo EAD que ele não possui nesta versão.

Legenda sugerida:

> Figura X — Edição de turma no aplicativo administrativo WPF. Fonte: captura da interface em Windows, com dados fictícios e HTTP substituto em memória; revisão e data identificadas no manifesto. A evidência visual não representa homologação integrada da API.

Para documentar **operações integradas**, execute o aplicativo normal com uma API de homologação e dados fictícios, usando o roteiro do README. Capture o formulário, a operação e o resultado recarregado, identificando o commit e o ambiente. Os PNGs do cenário visual não devem substituir esse teste nem a comprovação de persistência.

## Estado da validação

A geração de PNGs exige execução em Windows. Compilar o projeto no Linux valida código e XAML, mas não gera nem verifica visualmente as imagens. Antes de incluir os PNGs na entrega, confira os arquivos, o manifesto e o resultado do job Windows.
