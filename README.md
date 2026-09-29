# MediShop Admin Desktop

Aplicativo administrativo Windows desenvolvido em C# com WPF e .NET 10.

## Configuracao

A URL local da API fica em `appsettings.json`:

```json
{
  "Api": {
    "BaseUrl": "http://localhost:5278/"
  }
}
```

Em producao, use HTTPS. A variavel `PIM4_API_BASE_URL` sobrescreve o arquivo:

```powershell
$env:PIM4_API_BASE_URL = "https://api.seudominio.com.br/"
```

## Executar

```powershell
dotnet run
```

O login aceita somente usuarios cuja role retornada pela API seja `Admin`. O aplicativo nao acessa o banco diretamente; cursos, pedidos e alunos sao consultados pela API com JWT.

## Publicar para Windows

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```