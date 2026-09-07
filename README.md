# ZB2SecurityLab

PoC de instrumentação e mutação controlada para um teste autorizado do Zumbi Blocks 2 Open Alpha. O plugin detecta o contexto da partida, mapeia o jogador local e permite três testes de 10 segundos — FOV, recarga única de stamina e Infinite Ammo reativo — com restauração obrigatória. Single-player é permitido diretamente; multiplayer exige um grant temporário vinculado ao lobby Steam, servidor, conta local e papel exatos. As mutações ficam desabilitadas por padrão e nunca alteram pacotes de rede, reserva de munição ou saves.

## Escopo de segurança

- Use somente o build `24525702` verificado e sessões privadas autorizadas.
- Não execute mutações em servidores públicos.
- Não há bypass, stealth, persistência, packet injection ou mecanismo de distribuição.
- Mutation Mode e Authorized Multiplayer são opt-ins independentes; multiplayer também exige grant exato e não expirado.
- Se Steam ou uma proteção impedir a cópia, pare e solicite ao desenvolvedor um build de laboratório.

## Build conhecido

- Unity `6000.3.21f1`, Mono x64.
- `ZumbiBlocks2.exe`: `66C3ED6829349AAC8B5CB5FDB2A85EF62D17C1BDED4334352AF7349CA608EA0A`.
- `Assembly-CSharp.dll`: `C41A298975D35F0DAD0A05531BCE6E0B6E274D0DDF265217D65CE3AC5CBC84E1`.
- BepInEx `5.4.23.5` x64, arquivo local esperado com SHA-256 `82F9878551030F54657792C0740D9D51A09500EEAE1FBA21106B0C441E6732C4`.

O plugin falha fechado se o executável, `Assembly-CSharp.dll` ou a versão do Unity não corresponderem.

## Uso

```powershell
pwsh -File .\scripts\Prepare-LabCopy.ps1
dotnet test .\ZB2SecurityLab.sln --configuration Release
pwsh -File .\scripts\Deploy-Plugin.ps1
```

Inicie manualmente `lab-runtime\build-24525702\ZumbiBlocks2.exe` somente no ambiente autorizado. `F8` abre e fecha o painel. Fechar o painel durante um teste restaura o valor imediatamente. Os eventos ficam em `BepInEx\logs\ZB2SecurityLab\<session-id>.jsonl`; o log do loader fica em `lab-runtime\build-24525702\BepInEx\LogOutput.log`.

O diretório de logs pode ser alterado em `BepInEx\config\com.igorgsbarbosa.zb2securitylab.cfg`. Para habilitar os botões, defina `Enabled = true` em `[ControlledMutations]`; o padrão é `false`.

Multiplayer permanece bloqueado até que `[AuthorizedMultiplayer] Enabled = true` e `GrantPath` aponte para um JSON válido. Não existe modo curinga nem botão para confiar automaticamente na sessão atual:

```json
{
  "schemaVersion": 1,
  "buildId": "24525702",
  "steamLobbyId": "109775241012345678",
  "serverSteamId": "76561198000000001",
  "authorizedLocalSteamId": "76561198000000002",
  "allowedRole": "CLIENT",
  "expiresUtc": "2026-09-06T18:00:00Z",
  "runLabel": "poc-4a-private-01"
}
```

O grant deve corresponder ao lobby Friends Only atual e expirar em no máximo 24 horas. Mudança de lobby, servidor, papel, jogador ou autorização durante a janela encerra o experimento e solicita restauração.

Infinite Ammo só pode iniciar com uma arma válida e carregador cheio. Durante a janela, cada redução observada em `InventoryItem.ammo` é reposta ao baseline; armas novas parciais, melee e seleção vazia pausam a proteção sem receber writes. A reserva permanece somente leitura. Consulte `docs/mutation-tests.md` antes da validação manual.

Mapeamento e procedimentos detalhados: `docs/networking.md`, `docs/mutation-tests.md` e `docs/steam-runtime.md`.

## Projetos

- `ZB2SecurityLab.Core`: contratos, snapshots, fingerprint, tracking, guarda, lifecycle e restauração testáveis sem Unity.
- `ZB2SecurityLab.Plugin`: captura BepInEx/Unity, adaptadores de mutação limitados e painel IMGUI.
- `ZB2SecurityLab.Core.Tests`: testes automatizados do código puro.

Nenhuma DLL do jogo, runtime de laboratório, ferramenta ou log deve ser versionado.

## Instalação Steam suportada

`scripts\Resolve-SteamInstall.ps1` localiza o app Steam `1941780`. `Verify-Build.ps1 -GamePath <path>` aceita tanto a cópia de laboratório quanto uma instalação oficial, mas `Deploy-Plugin.ps1 -GamePath <path>` recusa destinos sem BepInEx já instalado. A instalação do loader é uma operação separada: os scripts deste repositório não substituem executável, `Assembly-CSharp.dll` ou outros arquivos originais do jogo.
