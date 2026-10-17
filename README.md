# MediaViewer

MediaViewer é um ecossistema completo de streaming de mídia estilo Netflix voltado para controle parental e uso familiar. Os administradores gerenciam o catálogo (filmes, séries via torrents e canais/vídeos selecionados do YouTube) através de um painel web, e as crianças assistem diretamente no app Android (.NET MAUI Blazor Hybrid).

---

## 🚀 Destaques & Arquitetura

- **Streaming P2P Direto no App**: O aplicativo Android executa o motor [MonoTorrent](https://github.com/alanmcgovern/monotorrent) em background local (`http://127.0.0.1:5555`), fazendo o download das peças sob demanda e priorizando buffers conforme o seek do player de vídeo. O servidor central **não** faz proxy de tráfego de vídeo.
- **Painel Administrativo Completo**: Interface Blazor Server para cadastro de perfis, gerenciamento de tokens de dispositivos, metadados via TMDB e canais seguros do YouTube.
- **Controle Parental**: Cada dispositivo pode ser vinculado a um perfil específico, permitindo selecionar o conteúdo apropriado para cada criança.
- **Modo Offline**: Suporte a download completo de torrents para armazenamento local no dispositivo.

### Visão Geral dos Projetos

| Projeto | Tecnologia | Descrição |
|---|---|---|
| `src/MediaViewer.Shared` | .NET 10 Class Library | Modelos, DTOs e contratos compartilhados |
| `src/MediaViewer.Streaming` | .NET 10 Class Library | Motor MonoTorrent com servidor HTTP local e priorização de seek |
| `src/MediaViewer.Server` | ASP.NET Core 10 & Blazor | API REST, Autenticação de Dispositivos e Painel Admin |
| `src/MediaViewer.App` | .NET MAUI Blazor Hybrid (Android) | Aplicativo móvel para reprodução e download |

---

## 🛠️ Configuração e Execução

### 1. Variáveis de Ambiente

Copie o arquivo de exemplo `.env.example` para `.env`:

```bash
cp .env.example .env
```

| Variável | Padrão | Descrição |
|---|---|---|
| `ADMIN_USERNAME` | `admin` | Usuário administrador do painel web |
| `ADMIN_PASSWORD` | `admin` | Senha do administrador (altere antes de expor na internet) |
| `TMDB_API_KEY` | *(opcional)* | Chave da API v3 do The Movie Database para metadados automáticos |
| `YOUTUBE_API_KEY` | *(opcional)* | Chave da Google API (YouTube Data API v3) para feed de vídeos |
| `YOUTUBE_REFRESH_HOURS` | `6` | Frequência de atualização periódica dos canais em horas |

---

### 2. Rodando o Servidor

#### Opção A: Docker Compose (Recomendado para VPS / Produção)

```bash
docker compose up -d --build
```

- **Painel Web**: `http://localhost:5080/admin`
- Os dados do banco SQLite e configurações ficam salvos no volume persistente `server-data` (`/data/mediaviewer.db`).

#### Opção B: Localmente (.NET CLI)

```bash
dotnet run --project src/MediaViewer.Server
```

- **Painel Web**: `http://localhost:5030/admin` (porta padrão de desenvolvimento).

---

### 3. Compilando o Aplicativo Android (.NET MAUI)

**Pré-requisitos**:
- .NET 10 SDK
- Carga de trabalho instalada: `dotnet workload install maui-android`
- Android SDK (API 26+)

**Compilar e Gerar APK**:
```bash
dotnet publish src/MediaViewer.App/MediaViewer.App.csproj -f net10.0-android -c Release
```

O APK gerado estará disponível na pasta:
`src/MediaViewer.App/bin/Release/net10.0-android/`

---

## 📱 Fluxo de Uso e Pareamento

1. **Acesse o Painel Web** (`/admin`):
   - Crie os perfis das crianças.
   - Adicione mídias (buscando automaticamente pelo TMDB e colando os magnet links).
   - Vá na aba **Dispositivos** e gere um **Código de Pareamento** de 6 dígitos.
2. **Abra o App no Android**:
   - Insira o endereço do servidor (Ex: `http://10.0.2.2:5080` no emulador ou `https://seu-dominio.com` em produção) e o código gerado.
   - Selecione o perfil desejado.
   - Assista via streaming instantâneo ou baixe para reprodução offline.

---

## 🔒 Produção & Deploy com HTTPS (Caddy)

Como o servidor roda em HTTP na porta `5080`, utilize um reverse proxy como o Caddy para terminação TLS automática:

```caddyfile
media.seudominio.com {
    reverse_proxy localhost:5080
}
```

---

## 🤖 Integração Contínua & Releases (GitHub Actions)

O repositório inclui automação via GitHub Actions (`.github/workflows/release.yml`) que:
1. Compila os binários do **MediaViewer.Server** para Linux x64 e Windows x64.
2. Compila e empacota o **APK / AAB** do **MediaViewer.App**.
3. Publica a imagem Docker no GitHub Container Registry (`ghcr.io`).
4. Cria uma **GitHub Release** automaticamente para cada push na branch `main` com os artefatos anexados para download direto.
