# Estado do jogador

Mapeamento estático inicial. A autoridade indicada como `INCONCLUSIVE` depende dos testes posteriores com host, segundo cliente e telemetria.

| Nome lógico | Classe | Campo/propriedade | Tipo | Origem | Rede/autoridade inicial |
|---|---|---|---|---|---|
| Vida rápida | `PlayerMain` | `healthFast` | `float` | Cliente | `INCONCLUSIVE`; existe sincronização de estado de saúde |
| Vida lenta | `PlayerMain` | `healthSlow` | `float` | Cliente | `INCONCLUSIVE` |
| Stamina | `PlayerMain` | `staminaFast`, `staminaSlow`, `maxStamina` | `float` | Cliente | `INCONCLUSIVE` |
| Posição | `PlayerMain` / `Transform` | transform do jogador | `Vector3` | Cliente local | Enviada por `PlayerPositionSynchronizer`; validação desconhecida |
| Movimento | `PlayerMovement` | `walkSpeed`, `targetSpeed`, `speedCoef` | `float`/`Vector2` | Cliente | `INCONCLUSIVE` |
| Câmera/FOV | `FOVController` | `UserDefinedFOV`, `CurrentFOV` | `float` | Cliente | Aparentemente visual/local; não testado |
| Recoil | `PlayerCamera` / `DatabaseGun` | `recoilMultiplier`, `recoil` | `float`/`Vector2` | Cliente | `INCONCLUSIVE` quanto ao impacto competitivo |
| Arma atual | `PlayerArms` | `EquippedGun` | `PhysicalGun` | Cliente | Equipamento é sincronizado; autoridade desconhecida |
| Munição | `InventoryItem` | `ammo` | `int` | Cliente | Equipamento é sincronizado; consumo/validação desconhecidos |
| Cadência | `PhysicalGun` / `DatabaseGun` | `Cooldown`, `rof` | `float` | Cliente | Shot events são enviados; validação desconhecida |
| Inventário | `PlayerInventory` | `storage`, `equippedItems` | containers | Cliente | Sincronizado parcialmente; autoridade desconhecida |
| Moeda | `Currency` | `Dollar`, `Silver`, `Gold` | `CurrencyData` | Cliente/save | Persistência e autoridade ainda não verificadas |

