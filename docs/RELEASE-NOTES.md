# MediShop Admin — v1.0.1 demo

Aplicativo administrativo para Windows x64, com runtime .NET incluído e interface WPF renovada.

- `MediShop-Admin-Windows-x64.exe`: executável pronto para abrir; utiliza a API do Render como padrão mesmo sem appsettings.json.
- `MediShop-Admin-Windows-x64.zip`: pacote completo com configuração editável e identificação da compilação. Extraia antes de executar PIM4AdminDesktop.exe.
- `SHA256SUMS.txt`: hashes SHA-256 dos downloads.

Novidades desta versão:

- Corrigido o fechamento ao focar o campo de senha no Wine quando as fontes padrão não são resolvidas. A fonte DejaVu Sans agora é incorporada ao aplicativo, com licença incluída no pacote.
- Novo visual para o login e o painel, com cartões de indicadores, cores e estilos compartilhados para botões, tabelas e formulários.
- Teste de regressão para carregamento da fonte e foco no campo de senha vazio.

Validação local: 10 testes de API aprovados e verificações WPF aprovadas no Wine 11.22 com API simulada. A publicação automatizada também executa as verificações WPF no Windows.

API configurada: https://pim4-api-demo.onrender.com/.

Entre com uma conta Admin existente na plataforma. As alterações são enviadas à API do Render e afetam os dados desse ambiente. Não há conta ou senha embutida no executável. É necessária conexão à internet.

Para desenvolvimento local, defina PIM4_API_BASE_URL=http://localhost:5278/ ou altere Api:BaseUrl no appsettings.json ao lado do executável. A variável de ambiente tem precedência.

Esta distribuição não possui instalador nem certificado de assinatura de código Windows. O aplicativo é destinado à demonstração acadêmica e não executa nativamente no Linux.
