# MediaViewer

MediaViewer é uma plataforma completa para gerenciamento e consumo de mídia audiovisual voltada para uso residencial com controle parental. A solução combina um painel administrativo web centralizado para curadoria de conteúdo com um cliente móvel (.NET MAUI Blazor Hybrid para Android) projetado com interface inspirada em serviços modernos de streaming.

---

## Principais Recursos

- **Catálogo Centralizado**: Curadoria de acervo via painel administrativo, permitindo catalogar filmes, séries e temporadas completas a partir de fontes Torrent (Magnet Links), além de canais e playlists selecionadas do YouTube.
- **Integração com Metadados (TMDB)**: Obtenção automatizada de sinopses, capas, pôsteres, temporadas e títulos de episódios por meio da API do The Movie Database.
- **Streaming P2P Direto no Cliente**: O aplicativo Android executa localmente o motor [MonoTorrent](https://github.com/alanmcgovern/monotorrent). As peças são baixadas da rede BitTorrent sob demanda e servidas em um endpoint HTTP interno com suporte a `Range Requests` e priorização de seek. O servidor central não trafega nem armazena arquivos pesados de mídia.
- **Reprodução Offline**: Mecanismo de persistência no dispositivo cliente, possibilitando o download integral de episódios ou filmes para reprodução sem conexão à internet.
- **Controle Parental e Perfis Múltiplos**: Definição de perfis de visualização (ex.: crianças e adolescentes) e associação a dispositivos autorizados via código de pareamento seguro.
- **YouTube Curado**: Monitoramento e sincronização em segundo plano dos vídeos mais recentes de canais permitidos, filtrando conteúdos indesejados.

---

## Arquitetura da Solução

O repositório é estruturado em quatro módulos independentes:

```text
MediaViewer/
├── src/
│   ├── MediaViewer.Shared/       # Contratos, DTOs e entidades compartilhadas
│   ├── MediaViewer.Streaming/    # Motor MonoTorrent encapsulado e servidor HTTP interno
│   ├── MediaViewer.Server/       # Backend ASP.NET Core, EF Core/SQLite e Painel Blazor
│   └── MediaViewer.App/          # Aplicação móvel Android (.NET MAUI Blazor Hybrid)
├── docs/                         # Documentações detalhadas de arquitetura e operação
├── docker-compose.yml            # Orquestração do servidor em contêiner
└── Dockerfile                    # Multi-stage build do MediaViewer.Server
```

Para uma análise aprofundada dos componentes, consulte a [Documentação de Arquitetura](docs/architecture.md).

---

## Configuração do Ambiente

### Variáveis de Configuração

As configurações do servidor podem ser definidas via arquivo `.env` ou variáveis de ambiente do sistema operacional:

```bash
cp .env.example .env
```

| Variável | Padrão | Descrição |
|---|---|---|
| `ADMIN_USERNAME` | `admin` | Nome de usuário para acesso ao painel administrativo |
| `ADMIN_PASSWORD` | *(definir)* | Senha de acesso ao painel administrativo |
| `TMDB_API_KEY` | *(opcional)* | Chave da API v3 do The Movie Database para metadados |
| `YOUTUBE_API_KEY` | *(opcional)* | Chave de acesso à YouTube Data API v3 |
| `YOUTUBE_REFRESH_HOURS` | `6` | Intervalo em horas para atualização do feed de canais |

---

## Execução do Servidor

### Utilizando Docker Compose (Recomendado)

O serviço do servidor web e banco de dados SQLite persistente podem ser inicializados via Docker:

```bash
docker compose up -d --build
```

- **Interface Administrativa**: `http://localhost:5080/admin`
- **Volume de Dados**: O banco SQLite é persistido no volume gerenciado `server-data` (`/data/mediaviewer.db`).

### Execução Direta (.NET SDK)

Com o .NET 10 SDK instalado:

```bash
dotnet run --project src/MediaViewer.Server
```

- **Interface Administrativa**: `http://localhost:5030/admin`

---

## Compilação do Aplicativo Android

### Pré-requisitos
- .NET 10 SDK com a carga de trabalho `maui-android` instalada:
  ```bash
  dotnet workload install maui-android
  ```
- Android SDK (API Target 26 ou superior).

### Gerando o Pacote de Distribuição (APK/AAB)

```bash
dotnet publish src/MediaViewer.App/MediaViewer.App.csproj -f net10.0-android -c Release
```

Os artefatos compilados serão gerados no diretório:
`src/MediaViewer.App/bin/Release/net10.0-android/`

---

## Fluxo Operacional

1. **Gestão de Conteúdo**:
   - Acesse o painel em `/admin` e autentique-se com as credenciais configuradas.
   - Cadastre os perfis dos usuários da residência.
   - Na seção **Mídias**, busque títulos no TMDB e insira os links magnéticos (*Magnet Links*) correspondentes.
   - Na seção **YouTube**, cadastre os canais ou *handles* aprovados.
2. **Autorização de Dispositivo**:
   - Na aba **Dispositivos**, gere um código de autorização temporário de 6 dígitos.
   - No aplicativo Android, informe a URL do servidor e o código gerado. O dispositivo receberá um token seguro e persistente.
3. **Consumo de Mídia**:
   - O aplicativo apresentará a grade de títulos permitidos para o perfil selecionado.
   - O streaming é iniciado imediatamente através do motor P2P local.

---

## Documentação Adicional

Documentos técnicos complementares disponíveis no diretório `/docs`:

- [Arquitetura do Sistema](docs/architecture.md)
- [Mecanismo de Streaming P2P](docs/streaming-engine.md)
- [Autenticação e Pareamento de Dispositivos](docs/security-and-pairing.md)
- [Guia de Desenvolvimento e Contribuição](docs/development-guide.md)
