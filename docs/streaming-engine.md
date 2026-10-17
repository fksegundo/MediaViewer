# Mecanismo de Streaming P2P

O **MediaViewer** adota uma abordagem de streaming descentralizado: o servidor central não consome largura de banda com tráfego de vídeo, nem necessita de armazenamento em disco massivo para os arquivos de mídia. Todo o processo de download e streaming ocorre diretamente no dispositivo móvel do usuário.

---

## Como Funciona o Streaming Sob Demanda

Tradicionalmente, clientes BitTorrent baixam arquivos de forma não sequencial (as peças mais raras primeiro). Para possibilitar a reprodução contínua e instantânea de vídeo, o módulo `MediaViewer.Streaming` implementa as seguintes técnicas:

### 1. Servidor HTTP Local (Loopback)
O motor inicia uma instância interna de servidor HTTP vinculada ao endereço `http://127.0.0.1:5555/stream`:
- Quando o componente de vídeo HTML5 ou ExoPlayer solicita a reprodução, ele envia requisições HTTP normais para esse endereço.
- O servidor local processa o cabeçalho HTTP `Range: bytes=inicio-fim` e responde com `206 Partial Content`.

### 2. Conversão de Byte Offsets em Peças Torrent
Quando o player solicita um determinado intervalo de bytes (seja o início do arquivo ou uma posição adiante decorrente de um *seek*):
1. O offset em bytes é mapeado para o índice da peça (*piece index*) correspondente no arquivo torrent selecionado.
2. O motor define **alta prioridade** (`Priority.Immediate` / `Priority.High`) para a peça requisitada e para a janela de peças subsequentes (buffer de avanço).
3. As peças que já ficaram para trás ou que estão fora da janela de reprodução têm sua prioridade reduzida.

### 3. Gerenciamento de Buffer e Bloqueio Assíncrono
Se o player tentar ler um trecho do stream cujas peças ainda estão em trânsito pela rede BitTorrent:
- A leitura do stream HTTP aguarda de forma não bloqueante a emissão do evento de conclusão da peça correspondente.
- Assim que o bloco é gravado e validado via SHA-1, a resposta do stream HTTP é liberada para o player.

---

## Modo de Download Offline

Além do streaming em tempo real, o cliente permite o download completo do arquivo:
- As peças são baixadas com a prioridade padrão do protocolo BitTorrent (maximizando a velocidade da swarm).
- O arquivo final é gravado no diretório de dados privados do aplicativo no Android (`DownloadStore`).
- O app registra o arquivo na aba **Baixados**, permitindo reprodução direta do sistema de arquivos local sem necessidade de conexão à internet ou ativação do motor P2P.
