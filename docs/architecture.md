# Arquitetura do Sistema

O **MediaViewer** adota uma arquitetura distribuída com separação clara de responsabilidades entre o servidor de controle/catálogo e os clientes reprodutores.

---

## Diagrama Geral da Solução

```text
               +---------------------------------------------------+
               |               Painel Web Administrativo           |
               |                (Blazor Server / Admin)            |
               +-------------------------+-------------------------+
                                         |
                                         v
+-----------------------+      +-------------------+      +----------------------+
|  The Movie Database   | <--- | MediaViewer.Server| ---> |  YouTube Data API v3 |
|      (TMDB API)       |      | (ASP.NET Core 10) |      |                      |
+-----------------------+      +---------+---------+      +----------------------+
                                         | SQLite (EF Core)
                                         | mediaviewer.db
                                         v
                              +--------------------+
                              |  REST API (/api/*) |
                              +---------+----------+
                                        ^
                    HTTP / HTTPS        | Pareamento (Bearer Token)
                    Sincronização DTOs  |
                                        v
                 +-----------------------------------------------+
                 |             MediaViewer.App (Android)         |
                 |             .NET MAUI Blazor Hybrid           |
                 +----------------------+------------------------+
                                        |
                 +----------------------+------------------------+
                 |              MediaViewer.Streaming            |
                 |     (MonoTorrent Engine + Local HTTP Server)  |
                 +----------------------+------------------------+
                                        |
                    Peças BitTorrent    v    Loopback HTTP
                   (Swarm descentralizada)  (127.0.0.1:5555 /range)
```

---

## Componentes

### 1. `MediaViewer.Shared`
- Biblioteca de classes em .NET 10.
- Contém os modelos de transferência de dados (DTOs) compartilhados:
  - `MediaItemDto`, `EpisodeDto`, `TorrentSourceDto`
  - `ProfileDto`, `DevicePairRequestDto`, `DeviceTokenResponseDto`
  - `PlaybackProgressDto`, `YouTubeVideoDto`
- Garante compatibilidade e tipagem estrita entre o cliente e o servidor.

### 2. `MediaViewer.Server`
- Aplicação ASP.NET Core 10 com Entity Framework Core e banco de dados SQLite.
- Hospeda tanto as rotas da API REST (`/api/*`) quanto as interfaces administrativas construídas em Blazor Server (`/admin`).
- Principais serviços:
  - **TmdbService**: Comunicação com a API TMDB v3 para busca e autocompletar de títulos e temporadas.
  - **YouTubeService**: Sincronização periódica em background de uploads de canais cadastrados.
  - **MagnetInspectorService**: Análise e leitura prévia de metadados embutidos em arquivos torrent/magnets.
  - **DeviceTokenAuthHandler**: Validação de autenticação de dispositivos baseada em tokens criptográficos (SHA-256).

### 3. `MediaViewer.Streaming`
- Wrapper sobre a biblioteca [MonoTorrent](https://github.com/alanmcgovern/monotorrent).
- Implementa um servidor HTTP local assíncrono baseado em `HttpListener` rodando em loopback (`127.0.0.1:5555`).
- Permite que qualquer player HTML5 (`<video>`) ou nativo (ExoPlayer) consuma um torrent como se fosse um arquivo de mídia estático comum via HTTP com suporte ao cabeçalho `Range`.

### 4. `MediaViewer.App`
- Aplicativo móvel para Android desenvolvido com .NET MAUI e Blazor Hybrid.
- Renderiza a interface do usuário com componentes Blazor responsivos e componentes nativos de reprodução quando necessário.
- Mantém o estado de pareamento, catálogo em cache e gerencia o ciclo de vida do motor de streaming e downloads em segundo plano.
