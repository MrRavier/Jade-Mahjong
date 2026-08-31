# Jade Mahjong

**Shanghai Mahjong competitivo para dois celulares no mesmo Wi-Fi**, feito em Unity 6 como aplicativo Android nativo. Não usa HTML, WebView, servidor dedicado, conta ou mensalidade.

## O jogo

- Tabuleiro Shanghai completo com **144 peças** e distribuição tradicional.
- Dois jogadores recebem a mesma disposição; vence quem limpar primeiro.
- Sala local por código `XXXX-XXXX` ou IP, porta UDP 7777.
- Arte 16-bit detalhada: Palácio Celestial, peças ornamentadas e seis poses do Imperador de Jade.
- Dicas limitadas, embaralhamento quando não houver jogadas e cronômetro sincronizado.
- Trilha original **Corte de Jade**, gerada dentro do aplicativo.
- Layout horizontal protegido por safe area, com o tabuleiro sempre visível.

## Abrir no Unity

1. Instale Unity **6000.3.18f1** com Android Build Support, SDK, NDK e OpenJDK.
2. Abra a pasta como projeto.
3. Use **Jade Mahjong > Abrir/Criar cena**.
4. Pressione Play ou use **Jade Mahjong > Construir APK Android**.

O APK sai em `Builds/Android/Jade-Mahjong.apk`.

## GitHub Actions

O workflow `Android APK` executa testes e publica o APK como artefato. Por exigência de licenciamento do Unity, configure `UNITY_LICENSE`, `UNITY_EMAIL` e `UNITY_PASSWORD` nos Secrets do repositório.

## Controles

Toque uma peça livre e depois outra igual. Flores combinam com flores; estações combinam com estações. O anfitrião mostra o código da sala e inicia quando o segundo aparelho entrar.

Consulte [a arquitetura de rede](Docs/Architecture/Multiplayer.md) e [a origem da arte](Docs/Art/Asset-Provenance.md).
