# ZB2SecurityLab

PoC de instrumentação **read-only** para um teste autorizado do Zumbi Blocks 2 Open Alpha. O PoC confirma carregamento de código, detecta o contexto da partida e mapeia o estado do jogador local em um painel de diagnóstico. Ele não altera gameplay, pacotes de rede, inventário ou saves.

## Escopo de segurança

- Use somente a cópia de laboratório do build `24525702` e servidor privado autorizado.
- Não conecte a cópia instrumentada a servidores públicos.
- Não há bypass, stealth, persistência, packet injection ou mecanismo de distribuição.
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

Inicie manualmente `lab-runtime\build-24525702\ZumbiBlocks2.exe` somente no ambiente autorizado. `F8` abre e fecha o painel. Os eventos ficam em `logs\security-tests\<session-id>.jsonl`; o log do loader fica em `lab-runtime\build-24525702\BepInEx\LogOutput.log`.

O diretório padrão dos logs pressupõe a estrutura deste repositório. Ele pode ser alterado em `BepInEx\config\com.igorgsbarbosa.zb2securitylab.cfg` após a primeira execução.

## Projetos

- `ZB2SecurityLab.Core`: contratos, snapshots, fingerprint, tracking de estado e guardas testáveis sem Unity.
- `ZB2SecurityLab.Plugin`: captura read-only BepInEx/Unity e painel IMGUI com estado do jogador.
- `ZB2SecurityLab.Core.Tests`: testes automatizados do código puro.

Nenhuma DLL do jogo, runtime de laboratório, ferramenta ou log deve ser versionado.
