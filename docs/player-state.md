# Estado do jogador — PoC 2 / PoC 3A

Mapeamento do build autorizado `24525702`, obtido por inspeção estática de `Assembly-CSharp.dll` e observação read-only. A existência de um campo ou mensagem não confirma uma vulnerabilidade nem prova ausência de validação em outros pontos.

## Local Player

A classe concreta do jogador é `PlayerMain`, um `UnityEngine.MonoBehaviour`. O plugin obtém o jogador local por `ClientController.instance.GetMyPlayer()` e usa `PlayersController.instance.MyPlayer()` como fallback. `PlayerMain.HasLocalControl` diferencia o objeto controlado localmente de representações remotas.

Os PoCs 1 e 2 não escrevem valores. O PoC 3A escreve temporariamente apenas `staminaFast` e `staminaSlow`, sob a guarda single-player e com restauração registrada antes da aplicação.

## Object Graph

```text
ClientController.instance.GetMyPlayer()
  fallback: PlayersController.instance.MyPlayer()
└─ PlayerMain
   ├─ transform
   ├─ movement -> PlayerMovement
   │  └─ body -> UnityEngine.Rigidbody
   ├─ arms -> PlayerArms
   │  ├─ selectedItem -> PlayerInventory.GetEquipment() -> InventoryItem
   │  │  └─ GetDataBaseItem() -> DatabaseItem / DatabaseGun
   │  └─ EquippedGun -> PhysicalGun -> DbReference -> DatabaseGun
   ├─ inventory -> PlayerInventory
   │  ├─ storage -> ItemContainer -> items
   │  └─ equippedItems -> PlayerEquippedItems -> AllItemsIndexed()
   ├─ lobbyPlayer -> LobbyPlayer -> loadoutLevel / perks
   └─ posSync -> PlayerPositionSynchronizer

MatchController.instance
└─ currency -> Currency -> Dollar / Silver / Gold
```

## Classification

- `CONFIG`: definição ou tuning usado para criar/calcular comportamento.
- `LOCAL_RUNTIME`: estado mutável mantido localmente; não foi encontrada serialização direta do valor.
- `NETWORK_SYNCED`: há serialização, envio ou recepção explícita do atributo ou de sua representação.
- `UI_ONLY`: valor derivado usado apenas para apresentação; não é fonte de verdade.
- `UNKNOWN`: a evidência atual não permite classificação mais forte.

## Health

| Attribute | Class | Member | Type | Classification | Network evidence | Confidence |
|---|---|---|---|---|---|---|
| Saúde imediata | `PlayerMain` | `healthFast` | `float` | `LOCAL_RUNTIME` | O valor numérico não aparece em `PlayerHealthState`; é consumido por dano, regeneração e HUD | HIGH |
| Saúde lenta | `PlayerMain` | `healthSlow` | `float` | `LOCAL_RUNTIME` | O valor numérico não aparece em `PlayerHealthState`; é consumido por dano, regeneração e HUD | HIGH |
| Saúde máxima | `PlayerMain` / `Perks` | `MaxHealth` / `GetLocalPlayerMaxHealth()` | `float` | `CONFIG` | Derivada do padrão 100 e perks; não é serializada | HIGH |
| Estado vivo/morrendo/morto | `PlayerMain` | `healthState` | `HealthState` | `NETWORK_SYNCED` | `PlayerPositionSynchronizer.SyncHealth` envia `Alive`, `Dying` ou `Dead`; o host retransmite | HIGH |
| Dano recebido | `ServerSpeaker` / `ClientListener` | `SyncPlayerDamage` / `OnPlayerDamage` | `Damage` | `NETWORK_SYNCED` | O host envia quantidade, permanência, direção e stagger; o cliente processa no jogador local | HIGH |

`PlayerStatusDisplay.UpdateDisplay(PlayerMain)` lê `healthFast` e `healthSlow`; portanto o HUD é consumidor, não a fonte da vida.

## Movement

| Attribute | Class | Member | Type | Classification | Network evidence | Confidence |
|---|---|---|---|---|---|---|
| Posição | `PlayerMain` / `Transform` | `transform.position` | `Vector3` | `NETWORK_SYNCED` | `SendPlayerPosition` transmite posição; `ReceivePosition` atualiza representações remotas | HIGH |
| Rotação horizontal | `Transform` / `PlayerPositionSynchronizer` | `eulerAngles.y` | `float` | `NETWORK_SYNCED` | Yaw é incluído em `PlayerPosition`; ângulo de câmera segue como byte separado | HIGH |
| Rotação completa | `Transform` | `eulerAngles` | `Vector3` | `LOCAL_RUNTIME` | Apenas yaw e ângulo de câmera compactado foram encontrados no pacote | MEDIUM |
| Velocidade física | `PlayerMovement` | `body.linearVelocity` | `Vector3` | `LOCAL_RUNTIME` | Não é serializada diretamente; o receptor deriva movimento de deltas de posição | HIGH |
| Velocidade configurada | `PlayerMovement` | `walkSpeed`, `jumpSpeed` | `float` | `CONFIG` | Não encontrada nos pacotes | HIGH |
| Velocidade desejada | `PlayerMovement` | `targetSpeed`, `speedCoef` | `Vector2` | `LOCAL_RUNTIME` | Estado usado pelo movimento local | HIGH |
| Estado de movimento | `PlayerMovement` | `state` | `State` | `LOCAL_RUNTIME` | Animações e roll possuem mensagens próprias, mas o enum completo não é serializado | MEDIUM |
| Grounded | `PlayerMovement` | `touchingGround` | `bool` | `LOCAL_RUNTIME` | Nenhuma serialização encontrada | HIGH |
| Sprint | `PlayerMovement` | `IsSprinting` / `State.Sprint` | `bool` / `State` | `LOCAL_RUNTIME` | Nenhuma serialização direta encontrada | HIGH |
| Jump | `PlayerMovement` | `State.Jump`, `State.Air` | `State` | `LOCAL_RUNTIME` | Inferido do estado local | HIGH |
| Crouch | — | — | — | `UNKNOWN` | Não foi encontrado estado ou membro de crouch em `PlayerMain`/`PlayerMovement` | MEDIUM |

O handler do host para posição retransmite os valores recebidos. Isso é evidência de fluxo cliente → host → clientes, mas não prova que não existam sanity checks anteriores ou externos ao método observado.

## Weapon

| Attribute | Class | Member | Type | Classification | Network evidence | Confidence |
|---|---|---|---|---|---|---|
| Arma/item selecionado | `PlayerArms` | `selectedItem` | `EquipmentIndex` | `NETWORK_SYNCED` | `PlayerEquipment` envia tipo/índice do slot e IDs equipados | HIGH |
| Instância física | `PlayerArms` | `EquippedGun` | `PhysicalGun` | `LOCAL_RUNTIME` | Representação local; equipamento remoto é reconstruído a partir de IDs | HIGH |
| ID/tipo | `InventoryItem` | `id` | `InventoryItem.ID` | `NETWORK_SYNCED` | IDs de itens equipados são serializados | HIGH |
| Munição no carregador | `InventoryItem` | `ammo` | `int` | `LOCAL_RUNTIME` | `PlayerEquipment` não serializa `ammo`; disparo envia apenas lobby ID e vetor | HIGH |
| Reserva utilizável | `PlayerInventory` | `StoredItemCount(ammoID)` | `int` | `LOCAL_RUNTIME` | Reload retira munição com `PullStoredItems`; total não é enviado no sync de equipamento | HIGH |
| Tamanho do carregador | `DatabaseGun` | `maxAmmo` | `int` | `CONFIG` | Não serializado | HIGH |
| Fire rate | `DatabaseGun` | `rof` | `float` | `CONFIG` | Não serializado; shot event não contém cadência | HIGH |
| Intervalo de disparo | `PhysicalGun` | `BaseCooldownTime` = `1 / rof` | `float` | `CONFIG` | Derivado localmente | HIGH |
| Cooldown atual | `PhysicalGun` | `Cooldown` | `float` | `LOCAL_RUNTIME` | Não serializado | HIGH |
| Recoil | `DatabaseGun` | `recoil` | `Vector2` | `CONFIG` | Aplicado na câmera local; não serializado | HIGH |
| Spread | `DatabaseGun` | `spread` | `float` | `CONFIG` | Altera localmente a direção; somente o vetor resultante é enviado no shot event | HIGH |
| Damage | `DatabaseGun` | `dmg` | `float` | `CONFIG` | Usado por `PhysicalGun.SetupShotDamage`; dano em zombie possui mensagem própria | HIGH |
| Reload time efetivo | `ReloadAnimationDatabase` | `GetAnimation(Perks.ModifyReloadAnimation(gun)).reloadTime` | `float` | `CONFIG` | Configuração local modificada por perk; não serializada | HIGH |

`EquipmentHUDAmmo.Show` e `AmmoStringDisplay` apenas formatam `InventoryItem.ammo` e os totais de `PlayerInventory`; são `UI_ONLY`.

## Inventory

| Attribute | Class | Member | Type | Classification | Network evidence | Confidence |
|---|---|---|---|---|---|---|
| Itens armazenados | `ItemContainer` | `items` | `List<InventoryItem>` | `LOCAL_RUNTIME` | Loot possui entrega/remoção em rede, mas o container completo não é serializado em `PlayerEquipment` | MEDIUM |
| Itens equipados | `PlayerEquippedItems` | `AllItemsIndexed()` | item + `EquipmentIndex` | `NETWORK_SYNCED` | O sync envia slot selecionado e ID de cada item equipado | HIGH |
| Quantidade | `InventoryItem` | `stackCount` | `int` | `LOCAL_RUNTIME` | `PlayerEquipment` envia somente ID, sem stack count | HIGH |
| Munição anexada ao item | `InventoryItem` | `ammo` | `int` | `LOCAL_RUNTIME` | Não aparece no pacote de equipamento | HIGH |
| Capacidade | `ItemContainer` | `UsableSize`, `TotalSize` | `IntVec2` | `LOCAL_RUNTIME` | Nenhuma serialização encontrada | MEDIUM |
| Slot selecionado | `PlayerArms` | `selectedItem` | `EquipmentIndex` | `NETWORK_SYNCED` | Set type e índice são enviados | HIGH |
| Moeda | `Currency` | `Dollar`, `Silver`, `Gold` | `CurrencyData` | `LOCAL_RUNTIME` | Carregada de `SaveProgress`; nenhuma mensagem de moeda foi encontrada | MEDIUM |

O tracker usa uma assinatura canônica de IDs, stacks, posições e slots. A munição da arma equipada e o slot selecionado ficam fora dessa assinatura para não duplicar `ammo_changed` e `weapon_changed`.

## Progression

| Attribute | Class | Member | Type | Classification | Network evidence | Confidence |
|---|---|---|---|---|---|---|
| Loadout level | `LobbyPlayer` | `loadoutLevel` | `int` | `NETWORK_SYNCED` | `SyncLobbyLoadout` envia o level | HIGH |
| Perks | `LobbyPlayer` / `PerkSelection` | `perks` / `GetSyncList()` | `List<PerkID>` | `NETWORK_SYNCED` | Lista de perks é enviada no loadout do lobby | HIGH |
| XP | — | — | — | `UNKNOWN` | Nenhum estado de XP do jogador foi encontrado | MEDIUM |
| Pontos/skills | — | — | — | `UNKNOWN` | Nenhuma árvore ou saldo de pontos de skills foi encontrado | MEDIUM |

O `LoadoutLevelCalculator` calcula nível/tier para o loadout; não representa XP acumulado do personagem.

## Networking Evidence

O build usa networking próprio sobre Facepunch Steamworks/Steam P2P, não `NetworkVariable`, `SyncVar`, Mirror ou Netcode for GameObjects.

- `PlayerPositionSynchronizer.SendUpdate` chama `ClientSpeaker.SendPlayerPosition` no cliente ou `ServerSpeaker.BroadCastPlayerPosition` no host.
- `GenericListener.OnPlayerPositionSync` desserializa posição, yaw, câmera, fight mode e spine input. `ServerListener.TreatPlayerPositionSync` retransmite; o override do cliente não retransmite.
- `PlayerPositionSynchronizer.SyncHealth` envia somente `PlayerMain.HealthState`. Vida numérica chega indiretamente por mensagens de dano do host e é mantida localmente.
- `NetCharacterMessage.PlayerEquipment` serializa lobby ID, slot selecionado e IDs equipados. Não inclui ammo nem stack count.
- `NetCharacterMessage.ShotEvent` contém lobby ID e vetor do tiro.
- `NetCharacterMessage` e `Buffer` fazem serialização explícita; `ClientSpeaker`/`ServerSpeaker` escolhem confiabilidade e transporte.

Esses pontos são pistas de autoridade, não resultados de exploração. O PoC 2 não intercepta, cria ou envia qualquer pacote.

## Candidate Tests

| Test | Expected risk | Client value found | Network evidence | Recommended? |
|---|---|---|---|---|
| Custom FOV | LOW | `FOVController.UserDefinedFOV`, `CurrentFOV` | Nenhuma sincronização encontrada; efeito visual local | YES |
| Stamina | MEDIUM | `PlayerMain.staminaFast`, `staminaSlow`, `maxStamina` | Nenhuma sincronização direta encontrada | YES |
| Movement Speed | HIGH | `PlayerMovement.walkSpeed`, `targetSpeed`, `speedCoef` | Posição é enviada pelo cliente e retransmitida pelo host | LATER |
| Recoil | MEDIUM | `PlayerCamera.recoilMultiplier`, `DatabaseGun.recoil` | Vetor final do tiro é enviado; recoil não é | LATER |
| Ammo | HIGH | `InventoryItem.ammo` | Ammo não aparece no sync de equipamento; shot event é separado | LATER |
| Fire Rate | HIGH | `DatabaseGun.rof`, `PhysicalGun.Cooldown` | Shot event não carrega timing; validação temporal não foi confirmada | LATER |
| Health | HIGH | `healthFast`, `healthSlow`, `healthState` | Host envia dano, mas somente o estado discreto de saúde é sincronizado | NO |

Os candidatos continuam como priorização documental; somente FOV e stamina fazem parte do PoC 3A.

## Controlled Mutation Mapping — PoC 3A

O teste de FOV captura e restaura exclusivamente `FOVController.UserDefinedFOV`. `BaseFOV`, `CurrentFOV`, `curZoom`, `Camera.fieldOfView` e `SaveGraphics.fov` são somente leitura. O valor solicitado é `110`; a câmera continua responsável por sua interpolação e zoom.

O teste de stamina captura `PlayerMain.staminaFast`, `staminaSlow` e `maxStamina`, escreve uma única vez `staminaFast = maxStamina` e `staminaSlow = maxStamina`, e restaura os dois valores mutados após no máximo 10 segundos. `maxStamina`, `staminaUsabilityCooldown`, `staminaRegenCooldown`, regeneração e fatores de drenagem nunca são alterados.

Ambos os testes exigem o mesmo token de jogador durante toda a janela. Não há reaplicação por frame, alteração persistente ou observação remota nesta etapa.
