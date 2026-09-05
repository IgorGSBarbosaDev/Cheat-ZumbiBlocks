# Networking

O jogo referencia `Facepunch.Steamworks.Win64.dll` e possui um transporte `SteamP2PTransmissionAdapter`. Mensagens são agrupadas em `Connection`, `Map`, `Character`, `Match`, `Entity` e `Audio`.

Superfícies relevantes observadas estaticamente:

- Cliente para host: posição, animação, roll, tiro, dano em zombie, equipamento, loot, respawn e saúde.
- Host para clientes: posição, saúde, equipamentos, loot, zombies, dano recebido, wave e estado da partida.
- `ServerListener` e `ClientListener` compartilham handlers, mas isso não demonstra que o host valida os valores recebidos.

Nenhuma mensagem é interceptada pelo PoC 1. A revisão de código e a telemetria do host serão necessárias antes de classificar autoridade.

