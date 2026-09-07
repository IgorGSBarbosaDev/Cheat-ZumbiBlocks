# Mutation Tests

Procedimentos para o build autorizado `24525702`. Não use mutações em servidores públicos. Mutation Mode inicia desabilitado e deve ser habilitado explicitamente em `[ControlledMutations] Enabled = true`. Multiplayer exige também `[AuthorizedMultiplayer] Enabled = true` e grant exato.

## Regras comuns

- Verifique o fingerprint antes do deploy.
- Execute um teste por vez pelo painel F8.
- Fechar o painel, sair da partida, trocar jogador/lobby/servidor, invalidar o grant, atingir o timeout ou desligar o plugin solicita restauração.
- Não encerre o processo deliberadamente durante uma janela de mutação; o processo encerrado não executa callbacks.
- Preserve o JSONL e o `BepInEx/LogOutput.log` como evidência.

## PoC 4A — Infinite Ammo

### Preparação

1. Em single-player, inicie uma partida local. Em multiplayer, complete primeiro o procedimento autorizado abaixo.
2. Equipe uma arma com carregador cheio e anote arma, slot, munição, reserva, fire rate, cooldown, recoil, spread e damage exibidos.
3. Não use reload, unload, drop ou rearranjo de inventário durante a janela; sem hook de tiro, essas operações podem ser confundidas com consumo.
4. Pressione `Run Infinite Ammo (10 s)`.

### Disparo e troca

1. Dispare mais projéteis que a capacidade do carregador, primeiro em semiautomático e depois em full-auto ou burst quando disponível.
2. Confirme que o carregador retorna ao baseline, a reserva não muda e não ocorre reload ou dry-fire transitório.
3. Troque para outra arma já cheia, aguarde o painel indicar o novo alvo rastreado e repita.
4. Selecione melee ou vazio; confirme `PAUSED` e ausência de writes.
5. Selecione uma arma nova parcial; confirme `MAGAZINE_NOT_FULL` e ausência de write nesse item.
6. Retorne a uma arma rastreada ou a uma nova arma cheia; confirme retomada.

### Restauração e evidência

Valide separadamente timeout, `Restore Now`, fechamento do painel e saída da partida. Ao final, todas as armas rastreadas devem estar no baseline capturado e a reserva deve permanecer inalterada.

No JSONL, confirme:

- `controlled_mutation_target_tracked` para cada arma elegível nova;
- `controlled_mutation_target_paused` somente na transição para uma seleção inelegível;
- um `controlled_mutation_write` por reposição real, com `oldValue`, `newValue`, readback e contexto do alvo;
- lifecycle de restore e resultado final;
- `serverEvidence=NOT_APPLICABLE` em single-player ou `NOT_OBSERVED` enquanto não existir evidência remota multiplayer.

O resultado esperado é `LOCAL_ONLY` somente após pelo menos um write confirmado e restauração completa. Sem disparos, interferência, alvo removido ou restore incompleto produzem `INCONCLUSIVE`.

## Multiplayer privado autorizado

### Host

1. Verifique o fingerprint `24525702` no host.
2. Crie um lobby `Friends Only`; não use servidor público.
3. No painel, registre `Steam lobby ID`, `Server Steam ID`, owner e conta local.
4. Confirme que owner, game server e server Steam ID convergem para o mesmo valor.
5. Para observação inicial, mantenha Mutation Mode desabilitado no host.

### Grant do client

Crie `BepInEx\config\ZB2SecurityLab\authorized-session.json` com:

```json
{
  "schemaVersion": 1,
  "buildId": "24525702",
  "steamLobbyId": "<Steam lobby ID do host>",
  "serverSteamId": "<Steam ID do host>",
  "authorizedLocalSteamId": "<Steam ID do client tester>",
  "allowedRole": "CLIENT",
  "expiresUtc": "<UTC futuro e curto>",
  "runLabel": "poc-4a-private-01"
}
```

Use uma expiração de no máximo 24 horas. Depois habilite `AuthorizedMultiplayer.Enabled` e `ControlledMutations.Enabled`. O painel deve mostrar `AUTHORIZED_MULTIPLAYER_CLIENT`. Qualquer campo divergente deve produzir um motivo específico e manter os botões bloqueados.

### Infinite Ammo client

1. Entre no lobby autorizado e equipe uma arma cheia.
2. Inicie Infinite Ammo e dispare além de `maxAmmo`.
3. Confirme no client a reposição do carregador e reserva inalterada.
4. Observe no host tiros, impactos e efeitos depois do limite original.
5. Valide timeout, `Restore Now` e saída do lobby separadamente.
6. Correlacione os logs por `runLabel`, `experimentRunId`, lobby, servidor, jogador e horário UTC.

O envio normal de `ShotEvent`, a continuidade visual apenas no client ou a ausência de desconexão não provam aceitação. Sem evidência correlacionada do host, não marque `SERVER_ACCEPTED`.

### Teste negativo

Use outro lobby privado controlado ou altere uma cópia do grant para um ID divergente. O resultado esperado é `NOT_ELIGIBLE`, sem baseline capturado e sem write. Não faça esse teste negativo em servidor público.
