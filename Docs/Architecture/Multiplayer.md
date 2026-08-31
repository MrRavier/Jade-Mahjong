# Multiplayer local

O anfitrião abre uma sessão UDP na porta 7777. O código de sala `XXXX-XXXX` contém o IPv4 privado do anfitrião e um checksum; o convidado também pode informar o IP diretamente.

Os dois aparelhos recebem a mesma semente e constroem o mesmo tabuleiro de 144 peças. Cada tabuleiro é jogado localmente. A rede envia apenas início sincronizado, quantidade restante e vencedor. O anfitrião é autoritativo para o resultado.

Não há Relay, login, servidor dedicado, nuvem ou mensalidade. Os aparelhos precisam estar no mesmo Wi-Fi, e a rede não pode usar isolamento de clientes/AP.
