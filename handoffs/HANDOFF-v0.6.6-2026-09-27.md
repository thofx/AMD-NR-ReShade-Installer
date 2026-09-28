# Handoff v0.6.6: a pasta com o setup do danielblnc não é mais "ReShade instalado"

27/09/2026. Leia depois de [HANDOFF-v0.6.5-2026-09-27.md](HANDOFF-v0.6.5-2026-09-27.md). Só o app muda; o
payload, o HF e o AMD-NR-Extras continuam os do v0.6.5.

## O problema

O runtime do danielblnc grava `dlssnr_on_amd_weights.bin` ao lado do `version.dll`. Esse nome é um dos
`InstalledMarkers`, então uma pasta só com o setup dele (sem manifesto, sem add-on) aparecia como rota
ReShade instalada. Escolher OptiScaler pedia a troca de rota, e o uninstall dessa troca apagava os pesos do
autor pelo nome. O OptiScaler instalava depois e usava o `version.dll`, então parecia funcionar.

Mesmo sem a troca, o uninstall do OptiScaler descartava o backup desses pesos, porque o nome é nosso.

## Código

- `Work.IsAuthorsWeights`: os pesos ao lado de um `version.dll` não contam em `GameScanner.IsInstalled`.
- `Work.Uninstall`: com o `version.dll` do autor na pasta ou no backup de um manifesto, os pesos não são
  "nossos" (o backup é restaurado) e a varredura por nome não os apaga.
- `AuthorRuntimeName` virou `internal`.

## Testes

- `LibraryStateTests.TheAuthorsOwnSetupIsNotAnInstallOfOurs`: pasta com `version.dll` e pesos do autor não é
  instalação, instala o OptiScaler, desinstala, os pesos do autor voltam. 264 no total; `gate.ps1 -Ui` sem
  falhas.
- Testado pelo usuário no God of War com um build local antes do release.
