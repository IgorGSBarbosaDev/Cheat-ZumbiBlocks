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
