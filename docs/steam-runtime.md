# Steam Runtime

O plugin não depende do nome `lab-runtime` em execução. `Paths.GameRootPath` determina o executável e `Assembly-CSharp.dll` que serão verificados. A instalação oficial só é utilizável quando ambos os hashes e a versão Unity correspondem ao build `24525702`.

## Experiência com launcher

O executável WPF consulta o registro do usuário e as visões 32/64-bit do registro local, interpreta `libraryfolders.vdf` e o manifesto `appmanifest_1941780.acf` e escolhe deterministicamente a primeira instalação suportada. AppID, build, update pendente, layout Mono x64 e hashes precisam passar antes de qualquer arquivo temporário ser criado.

O launcher recusa `BepInEx`, Doorstop, `winhttp.dll`, MelonLoader ou qualquer destino do payload já existente. Ele não reutiliza, atualiza ou sobrescreve loaders de terceiros. O payload aprovado é incorporado no executável final e validado novamente em memória.

O launch é solicitado com `steam.exe -applaunch 1941780`. O worker monitora somente um novo `ZumbiBlocks2.exe` no caminho canônico validado, com timeout de 120 segundos. Encerrado o processo, arquiva evidências e remove apenas os componentes pertencentes ao journal da sessão.

Dados persistentes:

```text
%LocalAppData%\ZB2SecurityLab\
  grants\authorized-session.json
  logs\launcher\
  logs\loader\
  logs\plugin\
  transactions\
```

Mutation Mode é opt-in por execução. A configuração temporária sempre força `AuthorizedMultiplayer.Enabled = false`; o suporte atual de multiplayer privado continua disponível no plugin e no ambiente `lab-runtime`, fora da UI v1.

## Descoberta e verificação

```powershell
$gamePath = pwsh -File .\scripts\Resolve-SteamInstall.ps1
pwsh -File .\scripts\Verify-Build.ps1 -GamePath $gamePath
```

O resolver consulta o app manifest Steam `1941780`, incluindo bibliotecas adicionais listadas em `libraryfolders.vdf`. Também é possível informar `-SteamPath` explicitamente.

## Build e deploy

`GameRootPath` seleciona as assemblies do jogo. `BepInExReferencePath` seleciona somente a referência de compilação do loader, permitindo compilar contra uma instalação oficial limpa sem pressupor BepInEx nela.

```powershell
dotnet build .\ZB2SecurityLab.sln -c Release -p:GameRootPath="$gamePath"
```

O deploy aceita um destino genérico, mas recusa um jogo sem `BepInEx\core\BepInEx.dll`:

```powershell
pwsh -File .\scripts\Deploy-Plugin.ps1 -GamePath $gamePath
```

Esse comando copia somente:

- `BepInEx\plugins\ZB2SecurityLab\ZB2SecurityLab.Core.dll`;
- `BepInEx\plugins\ZB2SecurityLab\ZB2SecurityLab.Plugin.dll`.

Ele não instala BepInEx, não substitui arquivos do jogo e não altera `Assembly-CSharp.dll`. A instalação ou remoção do loader é uma etapa separada que exige autorização explícita, inventário dos arquivos adicionados e rollback próprio.

## Atualizações

Depois de qualquer atualização Steam, execute novamente `Verify-Build.ps1`. Hash divergente mantém polling e mutações bloqueados. Não atualize automaticamente os fingerprints: um novo build exige novo mapeamento e validação.

O launcher aplica a mesma regra antes e depois da preparação, e o plugin repete a validação em runtime. Uma atualização ocorrida durante a sessão pode deixar o journal em `RecoveryRequired`, mas não autoriza atualização automática dos hashes nem remoção sem marker.

## Desenvolvimento

`Prepare-LabCopy.ps1`, `Deploy-Plugin.ps1` e o launch direto de `lab-runtime\build-24525702` permanecem inalterados. O launcher não é necessário para build, testes, instrumentação ou validação manual no ambiente isolado.
