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

## PoC 4A

O PoC 4A adiciona Infinite Ammo reativo. O teste só arma quando a seleção atual é um `DatabaseGun` com `InventoryItem.ammo == DatabaseGun.maxAmmo`, `maxAmmo > 0` e `ammoConsumption > 0`. Não há write inicial. A validação original ocorreu em single-player; a fundação posterior permite o mesmo experimento em multiplayer privado autorizado sem alterar sua lógica de write.

Enquanto o experimento está ativo, a guarda de build, partida, controle local, papel e token do jogador é reavaliada em todo `Update`. A política pura mantém alvos por identidade de instância; o adaptador Unity revalida item, slot, ID e configuração antes de qualquer compare-and-set. Um write ocorre apenas quando um alvo conhecido apresenta munição abaixo do baseline, atribuindo exclusivamente `InventoryItem.ammo = baseline` e confirmando a leitura imediatamente.

Trocas para armas novas cheias adicionam alvos ao mesmo experimento. Armas parciais, melee e seleção vazia pausam sem write. Todos os alvos rastreados continuam sendo validados e são restaurados em ordem reversa. Cada write real produz um evento `controlled_mutation_write`; nenhum evento de tiro ou pacote é interceptado.

## Authorized multiplayer foundation

`LabContext` produz um `MultiplayerSessionSnapshot` tipado com estados finais de host/client, Steam Lobby ID, owner, game server, endpoint P2P, conta local e lobby-player ID. `AuthorizedSessionPolicy` compara esse snapshot a um grant JSON temporário. A ausência de qualquer identidade, estado final, sinal Friends Only ou correspondência exata bloqueia a mutação.

`LabModeGuard` retorna `SINGLE_PLAYER`, `AUTHORIZED_MULTIPLAYER_CLIENT`, `AUTHORIZED_MULTIPLAYER_HOST` ou `NOT_ELIGIBLE`. Ao iniciar, `ExperimentCoordinator` fixa o token formado por build, sessão e jogador, além do identificador do grant. Ambos são reavaliados em todo `Update`; qualquer drift restaura e conclui como `INCONCLUSIVE`.

FOV, Stamina e Infinite Ammo continuam independentes da autorização. O coordenador fornece o mesmo envelope de sessão aos três, preservando uma mutação ativa, timeout e restore. Nenhum novo membro do jogo é escrito.

O JSONL possui campos explícitos para scope, papel, conexão, lobby, servidor, owner, conta local, autorização, execução e origem de evidência. `SERVER_ACCEPTED` e `SERVER_CORRECTED` exigem evidência explícita; funcionamento local ou ausência de desconexão não bastam.

## Runtime oficial

O plugin valida arquivos a partir de `Paths.GameRootPath`, portanto pode executar em qualquer instalação do build suportado. Compilação separa as referências do jogo das referências do loader. O deploy genérico verifica o fingerprint e exige BepInEx existente antes de copiar somente as duas DLLs do Security Lab. A instalação do loader não é automatizada.

## Launcher oficial

`ZB2SecurityLab.Launcher` é um aplicativo WPF .NET 8 x64, independente de PowerShell em runtime. `SupportedBuild` centraliza o AppID, build, Unity, layout e hashes consumidos pelo launcher e pelo plugin; um teste de paridade impede que `Verify-Build.ps1` divirja desses valores.

O launcher principal permanece sem elevação. Um worker iniciado pelo mesmo executável usa um named pipe restrito ao usuário e nonce de 256 bits, repete a descoberta e os fingerprints e solicita UAC somente se a pasta do jogo não aceitar uma escrita-probe reversível. O worker cria o payload com `FileMode.CreateNew`, mantém journal atômico por arquivo e não mescla instalações preexistentes.

O processo curto da Steam não é tratado como processo do jogo. Depois de `steam.exe -applaunch 1941780`, o worker procura um processo novo cujo caminho canônico corresponda exatamente a `ZumbiBlocks2.exe`, confirma o loader por log/JSONL e aguarda seu encerramento. Se a janela fechar, o worker continua independente; se todo o fluxo for interrompido, o próximo launcher recupera o journal antes de iniciar outra sessão.

O cleanup exige ausência do processo, marker exato e hashes dos arquivos raiz. A árvore `BepInEx` é removida apenas quando criada pela sessão; arquivos raiz alterados são preservados e reportados. Ao final, os fingerprints originais são recalculados. Logs do plugin são escritos diretamente fora da Steam e `LogOutput.log` é arquivado antes da remoção.

## Limites

Não existem patches Harmony, interceptação ou envio manual de mensagens, alteração de saves ou persistência das mutações. Encerramento abrupto do processo não permite callback de restauração; o plugin não grava saves, mas não pode executar a confirmação final nesse caso.

O launcher adiciona arquivos temporários ao diretório Steam durante a sessão; ele não torna o carregamento invisível e não contorna proteção ou política da plataforma. Antivírus, loader preexistente, build divergente ou cleanup inseguro causam bloqueio explícito.
