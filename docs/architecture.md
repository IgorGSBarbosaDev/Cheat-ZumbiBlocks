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

`LabStateTracker` registra apenas transições relevantes, evitando logar variações de ping a cada frame. Nesta etapa, o painel IMGUI é estritamente read-only.

## PoC 2

`LabContext` parte do mesmo `PlayerMain` local e produz snapshots puros de jogador, movimento, arma, inventário, progressão, moeda e rede. As leituras usam apenas membros públicos do build conhecido; cada subseção é isolada para que uma referência opcional ausente não interrompa o restante do polling.

`PlayerStateTracker` compara snapshots fora do runtime Unity. Spawn, morte, troca de arma e alteração estrutural do inventário são eventos imediatos. Saúde e munição são coalescidas e limitadas a um evento por segundo; posição, velocidade e stamina nunca geram telemetria contínua. O painel F8 continua IMGUI e ganhou scroll para apresentar os dados sem modificar objetos do jogo.

## PoC 3A

O PoC 3A adiciona uma fundação de mutação controlada, desabilitada por padrão e limitada ao build conhecido em single-player. `LabModeGuard` exige build suportado, partida ativa, jogador local com controle e papel `SINGLE_PLAYER`. O painel enfileira comandos que são processados no `Update`; nunca escreve durante `OnGUI`.

`ExperimentCoordinator` permite um teste por vez, registra a restauração antes da primeira escrita, observa a aplicação e encerra após no máximo 10 segundos. `RestoreManager` executa restaurações em ordem LIFO, tenta todas as entradas mesmo quando uma falha e torna chamadas repetidas inertes. Saída da partida, troca de jogador, fechamento do painel, falha de runtime e desligamento do plugin também encerram o teste.

Os únicos membros escritos são:

- `FOVController.UserDefinedFOV`, temporariamente definido como `110`;
- `PlayerMain.staminaFast` e `PlayerMain.staminaSlow`, carregados uma vez com o `maxStamina` capturado.

`SaveGraphics.fov`, `maxStamina`, cooldowns, inventário, saves e rede permanecem intocados. Resultados do 3A são somente `LOCAL_ONLY` ou `INCONCLUSIVE`; estados de servidor ficam reservados para um teste multiplayer futuro e autorizado.

## Limites

Não existem patches Harmony, interceptação ou envio manual de mensagens, alteração de saves ou persistência das mutações. Encerramento abrupto do processo não permite callback de restauração, mas os campos escolhidos são exclusivamente runtime e não são gravados pelo plugin.
