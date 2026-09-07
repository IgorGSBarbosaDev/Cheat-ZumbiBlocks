# Steam Runtime

O plugin não depende do nome `lab-runtime` em execução. `Paths.GameRootPath` determina o executável e `Assembly-CSharp.dll` que serão verificados. A instalação oficial só é utilizável quando ambos os hashes e a versão Unity correspondem ao build `24525702`.

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
