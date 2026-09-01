# Jade Mahjong

**Shanghai Mahjong competitivo para dois celulares no mesmo Wi-Fi**, feito em
Godot 4.7.2 como aplicativo Android nativo. Não usa HTML, WebView, conta,
servidor dedicado ou mensalidade.

## O jogo

- Tabuleiro Shanghai completo e solucionável com **144 peças em cinco camadas**, organizadas numa pirâmide palaciana legível para toque.
- Distribuição tradicional: 34 peças regulares em quartetos, quatro flores e
  quatro estações.
- **42 sprites de face distintos**, com marfim, jade, ouro e vermelhão; nenhum
  dominó genérico ou peça sem ilustração.
- Dois jogadores recebem a mesma semente. Vence quem limpar seu tabuleiro
  primeiro.
- Sala local por código `XXXX-XXXX` ou IP na porta UDP 7777.
- Imperador de Jade como mascote, Palácio Celestial e interface 16-bit
  ornamentada, com o tabuleiro ocupando a maior parte da tela.
- Três dicas, reorganização somente quando não há jogada e penalidade de tempo.
- Trilha original **Corte de Jade**, sintetizada no próprio aplicativo.
- Orientação horizontal forçada no Android, peças livres destacadas e uma única rota de toque para evitar seleções duplicadas.

## Estrutura

- `Godot/`: versão de produção nativa e exportável para Android.
- `Godot/scripts/mahjong_core.gd`: regras, geração determinística e solução.
- `Godot/scripts/tile_art.gd`: pipeline dos sprites das 42 faces.
- `Godot/scripts/lan_session.gd`: conexão ENet direta no mesmo Wi-Fi.
- `Godot/tests/run_tests.gd`: testes de regras, sementes, rede e sprites.
- `Assets/`: protótipo Unity preservado como histórico; não é usado no APK.

## Abrir e testar

1. Instale Godot **4.7.2**.
2. Importe `Godot/project.godot`.
3. Execute a cena principal ou rode:

   ```bash
   godot --headless --path Godot --script res://tests/run_tests.gd
   ```

## Gerar o APK

O workflow `Native Android APK` baixa os binários oficiais do Godot, executa os
testes, assina um APK de teste, valida a assinatura e instala a build em um
emulador Android. O artefato final se chama `Jade-Mahjong-APK`.

Localmente, com os templates Android configurados:

```bash
godot --headless --path Godot --export-debug Android ../build/Jade-Mahjong.apk
```

O pacote é `com.mrravier.jademahjong`, Android 8.0+ (API 26), orientação
horizontal. O APK contém ARM64 para celulares e x86_64 para o teste automatizado.

## Como conectar

1. Ligue os dois celulares ao mesmo Wi-Fi.
2. No primeiro, toque em **Criar sala**.
3. No segundo, informe o código mostrado e toque em **Entrar**.
4. O anfitrião inicia o duelo quando aparecer **Rival conectado**.

Redes com isolamento de clientes podem bloquear a conexão direta. Consulte
[a arquitetura de rede](Docs/Architecture/Multiplayer.md) e
[a proveniência da arte](Docs/Art/Asset-Provenance.md).
