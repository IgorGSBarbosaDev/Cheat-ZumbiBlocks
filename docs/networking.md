# Networking

O jogo referencia `Facepunch.Steamworks.Win64.dll` e possui um transporte `SteamP2PTransmissionAdapter`. Mensagens são agrupadas em `Connection`, `Map`, `Character`, `Match`, `Entity` e `Audio`.

Superfícies relevantes observadas estaticamente:

- Cliente para host: posição, animação, roll, tiro, dano em zombie, equipamento, loot, respawn e saúde.
- Host para clientes: posição, saúde, equipamentos, loot, zombies, dano recebido, wave e estado da partida.
- `ServerListener` e `ClientListener` compartilham handlers, mas isso não demonstra que o host valida os valores recebidos.

Nenhuma mensagem é interceptada pelo PoC 1. A revisão de código e a telemetria do host serão necessárias antes de classificar autoridade.

## Identidade da sessão

- Host: `ServerController.state == Started`, `mode == Multiplayer`, `ServerMatchmaking.LobbyLaunched` e `CurrentLobby.Id`.
- Client: `ClientController.state == Connected`, `ClientMatchmaking.IsConnected` e `ConnectedLobby.Id`.
- Endpoint: `SteamConnectionsController.ServerID` e `GetServerConnection().SteamID`.
- Owner: `Steamworks.Data.Lobby.Owner.Id`; `Lobby.GetGameServer` fornece o servidor anunciado.
- Conta local: `SteamController.MySteamID`.
- `MultiplayerController.GetMyLobbyID()` retorna o ID inteiro do jogador dentro da partida; não identifica o lobby Steam.

`ServerMatchmaking.ConfigureLobby` usa `region=Friends` quando `friendsOnly` está ativo, mas chama `Lobby.SetPublic()`. A restrição efetiva aparece em `ServerController.OnClientAuthAttempt`, que verifica presença no lobby e, quando `FriendsOnly` está ativo, amizade com o owner. Por isso a autorização do Security Lab vincula o par lobby/server e a conta local, em vez de confiar apenas no metadado `region`.

O fluxo normal de tiro do client envia `NetCharacterMessage.ShotEvent` com lobby-player ID e vetor. O evento não contém munição. A retransmissão pelo host é evidência de fluxo, mas o envio do client isoladamente não prova `SERVER_ACCEPTED`.

O grant é uma trava operacional local contra execução acidental, não uma assinatura criptográfica de autorização do desenvolvedor. Ele deve ser criado e distribuído apenas pelo operador do teste, mantido fora do repositório e usado junto com o controle real do lobby privado. O Security Lab não trata a mera capacidade de editar esse arquivo como prova de autorização.
