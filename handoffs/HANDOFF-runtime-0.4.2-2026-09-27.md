# Handoff: runtime 0.4.2 do danielblnc no OptiScaler 0.4.3

27/09/2026. Leia depois de [HANDOFF-v0.6.4-2026-09-27.md](HANDOFF-v0.6.4-2026-09-27.md). Só o payload mudou:
nenhum build novo do instalador, o v0.6.4 lê o `payload.json` do HF e já pega a troca.

## O que mudou no payload

| Componente | Antes | Agora | Onde no dataset |
|---|---|---|---|
| `opti-runtime` do release `0.4.3-amd-nr` | 0.4.1 (`823063eb…`, 9.916.928 bytes) | 0.4.2 (`8aa2dcc5…`, 12.981.760 bytes) | `opti-runtime/dlssnr_amd_runtime-0.4.2.dll` |

- O danielblnc publicou a 0.4.2 em 27/09 ([Alpha 0.4.2](https://github.com/danielblnc/DLSS-NR-on-AMD/releases/tag/v0.4.2)).
  O `dlssnr_on_amd_setup.exe` público (`08aecc04…`) é byte a byte o build de apoiador que já tínhamos, e o
  `version.dll` tirado dele pelos cabeçalhos PE (offset `0x260a27`, como no `install-amd-presr.ps1`) é o
  `8aa2dcc5…`, o `kAmd042` do `AmdLayout.h` do opti 0.4.3. A 0.4.3 continua só de apoiador e fora do payload.
- **Só o 0.4.3-amd-nr troca.** O 0.4.2-amd-nr continua com o 0.4.1 e o 0.4.1-amd-nr com o 0.4.0: nenhum
  OptiScaler antes do 0.4.3 aceita a 0.4.2 pelo SHA.
- **A rota ReShade não muda:** `runtime` continua o 0.4.1 com patches (`c8808716…`) e o add-on o v0.6.9, que
  ainda não aceita a 0.4.2. Os pesos são os mesmos da 0.4.1.
- Quem já instalou o 0.4.3 vê a pasta como desatualizada (as pass não batem mais com o pin) e atualiza.

## Código

- `OptiScalerVersionTests`: o 0.4.3 vem com `opti-runtime` 0.4.2 (`8aa2dcc5…`); o 0.4.2 e o 0.4.1 como antes.
- Comentário de `Work.OptiScaler.KnownRuntimePrefixes`: a 0.4.2 deixou de ser build de apoiador.

## Publicação

1. `gate.ps1 -Ui` sem falhas.
2. `publish-payload.ps1 -Repo zmodelerlover/amd-nr -From <pasta com dlssnr_amd_runtime-0.4.2.dll>`: um arquivo
   novo no HF, `payload.json` no ar, tudo baixado de volta e conferido.
3. Ponta a ponta com o motor, cache vazio, tudo baixado dos endereços do HF: OptiScaler 0.4.3 com o payload
   anterior (as três pass `823063eb…`), o payload novo marca a pasta como desatualizada, reinstalação com
   as três pass `8aa2dcc5…`, depois não marca mais, e o uninstall deixa a pasta como estava.
4. `payload.json` copiado para o AMD-NR-Extras (segundo endereço de onde o app lê).

## Testado antes

- A 0.4.2 dentro do OptiScaler 0.4.3 em jogo, pelo usuário, com o build de acesso antecipado (o mesmo arquivo).
