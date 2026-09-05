# Arquitetura

## Runtime observado

O build `24525702` usa Unity `6000.3.21f1`, Mono x64 e networking próprio sobre Facepunch Steamworks/Steam P2P. A lógica cliente e host está em `Assembly-CSharp.dll`.

## PoC 1

`ZB2SecurityLab.Plugin` é carregado pelo BepInEx e depende de `ZB2SecurityLab.Core`. Antes de consultar o jogo, o plugin valida o executável, `Assembly-CSharp.dll` e a versão Unity. Build desconhecido mantém somente o cabeçalho do painel e desabilita polling.

Com build válido, `LabContext` consulta a cada 250 ms:

- `MatchController.instance.InGame`;
- `ClientController.instance.GetMyPlayer()`, com fallback para `PlayersController.instance.MyPlayer()`;
- `PlayerMain.HasLocalControl`;
- papel single-player/host/client, estado de conexão, lobby ID e ping.

`LabStateTracker` registra apenas transições relevantes, evitando logar variações de ping a cada frame. O painel IMGUI é estritamente read-only.

## PoC 2

`LabContext` parte do mesmo `PlayerMain` local e produz snapshots puros de jogador, movimento, arma, inventário, progressão, moeda e rede. As leituras usam apenas membros públicos do build conhecido; cada subseção é isolada para que uma referência opcional ausente não interrompa o restante do polling.

`PlayerStateTracker` compara snapshots fora do runtime Unity. Spawn, morte, troca de arma e alteração estrutural do inventário são eventos imediatos. Saúde e munição são coalescidas e limitadas a um evento por segundo; posição, velocidade e stamina nunca geram telemetria contínua. O painel F8 continua IMGUI e ganhou scroll para apresentar os dados sem modificar objetos do jogo.

## Limites

Não existem patches Harmony, escrita em campos do jogo, interceptação ou envio manual de mensagens, alteração de saves ou experimento ativo no plugin. `ILabExperiment`, `ExperimentCoordinator` e `LabModeGuard` continuam sem integração ao runtime nesta etapa.
