# Achados

Nenhuma vulnerabilidade foi confirmada.

O reconhecimento identificou superfícies que merecem teste, mas a simples existência de campos ou mensagens no cliente não constitui vulnerabilidade. Resultados futuros usarão exclusivamente:

- `LOCAL_ONLY`
- `SERVER_REJECTED`
- `SERVER_CORRECTED`
- `SERVER_ACCEPTED`
- `INCONCLUSIVE`

## PoC 3A

O PoC 3A classifica como `LOCAL_ONLY` somente quando o valor solicitado foi observado localmente e a restauração foi confirmada. Interferência, troca de contexto/alvo, erro ou restauração não confirmada produzem `INCONCLUSIVE`.

Os testes desta etapa são exclusivos de single-player. `serverEvidence` é registrado como `NOT_EVALUATED_SINGLE_PLAYER_ONLY`; `SERVER_REJECTED`, `SERVER_CORRECTED` e `SERVER_ACCEPTED` não podem ser emitidos pelo fluxo 3A. A implementação da capacidade de teste, por si só, não constitui um achado de vulnerabilidade.

## PoC 4A

O PoC 4A testa se a reposição local de `InventoryItem.ammo` mantém o fluxo normal de disparo. A validação concluída até agora foi single-player. `LOCAL_ONLY` exige ao menos um consumo reposto, readback confirmado e restauração completa; nenhuma ação de tiro resulta em `INCONCLUSIVE`.

Reduções de munição são observadas como mudanças de estado, sem interceptação do método de tiro. Portanto, a evidência não distingue automaticamente disparo de outra operação sobre o mesmo campo. Em single-player, `serverEvidence=NOT_APPLICABLE`; no multiplayer autorizado, inicia como `NOT_OBSERVED`. Nenhum resultado atual demonstra autoridade ou vulnerabilidade multiplayer.

## Authorized multiplayer foundation

A fundação multiplayer não constitui um achado. Ela apenas permite executar os mesmos writes locais quando um grant temporário corresponde ao Steam Lobby ID, servidor, conta local, papel e build atuais.

No client, `LOCAL_ONLY` significa que o write/readback local e o restore foram confirmados. `SERVER_ACCEPTED` exige evidência correlacionada do host; `SERVER_CORRECTED` exige atribuição explícita da correção ao servidor. Falta de observação remota, conflito, troca de sessão ou restore incompleto permanece `INCONCLUSIVE` conforme a qualidade da evidência.
