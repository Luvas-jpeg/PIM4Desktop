# MediShop Admin — demonstração acadêmica

Aplicativo administrativo para Windows x64, com runtime .NET incluído e interface WPF básica.

- `MediShop-Admin-Windows-x64.exe`: executável pronto para abrir; utiliza a API do Render como padrão mesmo sem appsettings.json.
- `MediShop-Admin-Windows-x64.zip`: pacote completo com configuração editável e identificação da compilação. Extraia antes de executar PIM4AdminDesktop.exe.
- `SHA256SUMS.txt`: hashes SHA-256 dos downloads.

API configurada: https://pim4-api-demo.onrender.com/.

Entre com uma conta Admin existente na plataforma. As alterações são enviadas à API do Render e afetam os dados desse ambiente. Não há conta ou senha embutida no executável. É necessária conexão à internet.

Para desenvolvimento local, defina PIM4_API_BASE_URL=http://localhost:5278/ ou altere Api:BaseUrl no appsettings.json ao lado do executável. A variável de ambiente tem precedência.

Esta distribuição não possui instalador nem certificado de assinatura de código Windows. O aplicativo é destinado à demonstração acadêmica e não executa nativamente no Linux.
