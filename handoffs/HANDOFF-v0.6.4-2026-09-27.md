# Handoff v0.6.4: OptiScaler 0.4.3 e o mochizuki corrigido

27/09/2026. Leia depois de [HANDOFF-v0.6.3-2026-09-26.md](HANDOFF-v0.6.3-2026-09-26.md). Só a rota
OptiScaler ganhou versão nova; o add-on continua o v0.6.9 e o runtime da rota ReShade continua o 0.4.1
com patches.

## O que mudou no payload

| Componente | Antes | Agora | Onde no dataset |
|---|---|---|---|
| release `0.4.3-amd-nr` | não existia | OptiScaler 0.4.3 (`e31a2637…`), `opti-runtime` 0.4.1 (`823063eb…`, o mesmo do 0.4.2), pesos do lmxxf do 0.4.2 | nada novo além do mochizuki |
| `mochizuki` do 0.4.3 | o build do 0.4.0 (`682f2087…`) | `MochizukiNrRuntime.dll` `4c3b0b3a…`, archive `f57b9d00…` | `mochizuki/0.4.3-amd-nr/mochizuki-0.4.3-amd-nr.zip` |

- Os shaders do mochizuki são os mesmos 54 arquivos do 0.4.0, conferidos um a um, então o manifesto de
  prewarm do 0.4.0 continua valendo. O modelo é a versão 1, já publicada.
- A rota ReShade pega o mochizuki do release mais novo (`WithMochizuki`), então ela também recebe a
  correção sem mudar o add-on.
- O runtime padrão continua o 0.4.1. As builds 0.4.2 e 0.4.3 que o danielblnc dá aos apoiadores não
  estão no payload nem no dataset, e não devem entrar: o OptiScaler 0.4.3 aceita quando a pessoa traz.

## O que o OptiScaler 0.4.3 traz

- Aceita as builds de apoiador 0.4.2 e 0.4.3. Basta trocar `dlssnr_amd_pass1.dll`: o host copia a pass 1
  para a pass 2 e a 3 quando elas faltam ou são de outro runtime.
- "DLSS 5 mode" (`AmdQuality`, 0.4.2 ou mais novo) e "Tone curve" (`AmdToneCurve`, 0.4.0 ou mais novo).
- O mochizuki não desliga mais abaixo de 1080p: o driver recusava o handle compartilhado dos buffers
  pequenos (até 4,2 MB); agora nenhum fica abaixo de 16 MB, e a recusa é tentada de novo em 1, 2, 5, 10,
  30 e 60 s.

## Código

- `Work.OptiScaler.KnownRuntimePrefixes` reconhece a 0.4.2 (`8aa2dcc5…`) e a 0.4.3 (`d1e32086…`), e o
  aviso de `version.dll` do danielblnc na pasta do jogo agora olha arquivos de 7 a 14 MB. Antes só de 7 a
  8 MB, então nenhum runtime 0.4.x (9,9 MB e 12,8 MB) disparava o aviso.
- `OptiScalerVersionTests`: o 0.4.3 vem primeiro com `opti-runtime` 0.4.1; o 0.4.2 também tem 0.4.1; o
  mochizuki do 0.4.3 é diferente do 0.4.0 a 0.4.2, que continuam iguais entre si.

## Publicação

1. opti `v0.4.3-amd-nr` (zip `e31a2637…`, baixado de volta e conferido).
2. `tools/pin-optiscaler.ps1` com o zip baixado do release; `opti-runtime` copiado do 0.4.2.
3. `tools/pin-mochizuki.ps1 -Version 0.4.3-amd-nr` com o build do commit `e3048774` do opti; o archive
   subiu para o HF e foi baixado de volta.
4. Ponta a ponta com cache vazio, tudo baixado dos endereços do `payload.json`: rota 64-bit e ponte 32-bit
   com e sem mochizuki, OptiScaler 0.4.3 com mochizuki (as três pass com `823063eb…`, mochizuki
   `4c3b0b3a…`) e os uninstalls. Passou.
5. `gate.ps1 -Ui` sem falhas.
6. Release v0.6.4 do instalador, `publish-payload.ps1`, espelho no AMD-NR-Extras.

## Testado antes

- No Cyberpunk 2077: o runtime 0.4.3, o DLSS 5 mode, o Tone curve e a correção do mochizuki (descida de
  1080p até 640x360 no FSR sem perder o efeito).
- A cópia da pass 1 para a pass 2 e a 3 não foi testada em jogo antes do release.
