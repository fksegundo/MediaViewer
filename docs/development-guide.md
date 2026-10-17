# Guia de Desenvolvimento

Este documento descreve como configurar o ambiente de desenvolvimento local para contribuir ou realizar manutenções no projeto **MediaViewer**.

---

## Estrutura do Ambiente

- **SDK Principal**: [.NET 10 SDK](https://dotnet.microsoft.com/)
- **IDE Recomendada**: Visual Studio 2026, Visual Studio Code (com C# Dev Kit) ou JetBrains Rider
- **Ferramentas Adicionais**:
  - Docker Desktop ou Docker Engine no Linux/WSL2
  - Android SDK (API 26+) para testes e emulação do app móvel

---

## Executando os Projetos

### 1. `MediaViewer.Server`

Para executar o servidor com *hot-reload* ativado:

```bash
cd src/MediaViewer.Server
dotnet watch run
```

O servidor iniciará escutando por padrão em:
- `http://localhost:5030`
- Painel Administrativo: `http://localhost:5030/admin`

#### Migrações do Banco de Dados (EF Core)

Se você alterar modelos em `Entities.cs`:

```bash
dotnet ef migrations add <NomeDaMigracao> --project src/MediaViewer.Server --startup-project src/MediaViewer.Server
dotnet ef database update --project src/MediaViewer.Server --startup-project src/MediaViewer.Server
```

### 2. `MediaViewer.App`

Para rodar o app no emulador Android local:

```bash
dotnet build src/MediaViewer.App -t:Run -f net10.0-android
```

> **Dica para testes locais**: Ao parear o emulador com o servidor rodando na máquina host, utilize `http://10.0.2.2:5030` como URL do servidor (o IP `10.0.2.2` aponta para o localhost do computador host dentro do emulador padrão do Android).

---

## Boas Práticas e Diretrizes de Código

1. **Separação de Camadas**:
   - DTOs e tipos de comunicação devem residir exclusivamente em `MediaViewer.Shared`.
   - Lógica de manuseio P2P e streaming deve permanecer isolada em `MediaViewer.Streaming`.
2. **Segredos e Configurações**:
   - Nunca comite chaves de API ou senhas em arquivos de configuração padrão (`appsettings.json`).
   - Utilize arquivos `.env` locais ou *User Secrets* do .NET (`dotnet user-secrets`) durante o desenvolvimento.
3. **Padrão de Código**:
   - Nullable reference types habilitados em todos os projetos (`<Nullable>enable</Nullable>`).
   - C# 13 / .NET 10 idioms (Primary constructors, pattern matching, collection expressions).
