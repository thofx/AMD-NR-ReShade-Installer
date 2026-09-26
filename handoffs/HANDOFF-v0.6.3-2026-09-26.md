# Handoff v0.6.3: runtime 0.4.1 do danielblnc, e a caixa do mochizuki que não aparecia

26/09/2026. Leia depois de [HANDOFF-v0.6.2-2026-09-26.md](HANDOFF-v0.6.2-2026-09-26.md). Dois
assuntos: o runtime novo do Daniel nas duas rotas, e a correção da v0.6.2, que nunca mostrou a
caixa do mochizuki na rota ReShade.

## O mochizuki na rota ReShade (bug da v0.6.2)

Duas causas, as duas corrigidas:

1. **A caixa estava dentro do `OptiPanel`** em `GameSheet.axaml`. Na rota ReShade esse painel fica
   escondido, então `MochizukiSection.IsVisible = true` não mostrava nada. A seção agora fica
   abaixo dos dois painéis de rota e serve às duas.
2. **`AddonReleases.With` perdia os `Releases`.** A ficha escolhe o add-on pela lista do GitHub, e a
   v0.6.8 está lá, então a escolha sempre passava por `With`, que remontava o manifesto sem
   `Releases`. Aí o `WithMochizuki()` não achava o build (ele mora nos releases do OptiScaler). Agora
   `With` mantém `Releases`.

Testes: `ReShadeMochizukiTests.AnAddOnPickedFromItsReleasesStillCarriesTheMochizukiBuild` (falha sem
a correção 2) e o flow `tools/uishot/Flows.Mochizuki.cs`, que agora troca para a rota ReShade e exige
`IsEffectivelyVisible` (falha sem a correção 1: `IsVisible` sozinho não prova nada, que foi o que
deixou o bug passar). O usuário confirmou num build de teste que a caixa aparece e instala.

## O que mudou no payload

| Componente | Antes | Agora | Onde no dataset |
|---|---|---|---|
| `runtime` (rota ReShade) | 0.4.0 com patches, `ff6feffa…` | 0.4.1 com patches, `c8808716…`, 9.916.928 bytes | `runtime/0.4.1/dlssnr_amd_pass1.dll` |
| `addon` | 0.6.8 | 0.6.9, `f5007509…` | `addon/0.6.9/amd-nr.addon64` |
| `bridge` | 0.6.8 | 0.6.9: `633f8a30…`, `eb0577c4…`, `payload.sha256` `8e6f3799…` | `bridge/0.6.9/…` |
| release `0.4.2-amd-nr` | não existia | OptiScaler 0.4.2 (`ea8aff0a…`) com `opti-runtime` 0.4.1 **próprio** (`823063eb…`), mochizuki e pesos do lmxxf iguais aos do 0.4.1 | `opti-runtime/dlssnr_amd_runtime-0.4.1.dll` |

- O `opti-runtime` 0.4.1 fica dentro do release pelo mesmo motivo do 0.4.0 na v0.6.1: os OptiScaler
  anteriores recusam o 0.4.1 pelo SHA. O 0.4.1-amd-nr continua com o 0.4.0.
- O add-on 0.6.9 recusa o runtime 0.4.0 e o 0.6.8 recusa o 0.4.1, então add-on e runtime trocam
  juntos, quando o `payload.json` novo sobe. Mesma limitação da v0.6.1: quem escolher um add-on
  antigo no menu recebe o runtime novo e o painel diz qual runtime aquele add-on precisa.
- A ponte 0.6.9 usa o protocolo v4 (o status leva a linha do runtime). `amd-nr.addon32` e
  `amd-nr-host64.exe` vêm sempre do mesmo release, como antes.

## Código

- `Engine.RuntimeSha` é o 0.4.1 com patches (`c8808716…`).
- `Work.OptiScaler.KnownRuntimePrefixes` reconhece o 0.4.1 (`823063eb…`) e o 0.4.1 com patches.
- `OptiScalerVersionTests`: o 0.4.2 vem primeiro com `opti-runtime` 0.4.1; o 0.4.1 continua com 0.4.0.

## Publicação

1. opti `v0.4.2-amd-nr` (zip `ea8aff0a…`), add-on `v0.6.9` (seis assets baixados de volta e conferidos).
2. `tools/pin-optiscaler.ps1` com o zip baixado do release; o mochizuki copiado do 0.4.1.
3. Os seis arquivos novos no HF em caminhos com versão, baixados de volta e conferidos, espelhos
   do GitHub também.
4. Ponta a ponta com o motor desta versão, cache vazio, tudo baixado dos endereços do `payload.json`:
   rota 64-bit (runtime `c8808716…` no jogo) com e sem mochizuki, ponte 32-bit com e sem mochizuki,
   OptiScaler 0.4.2 com mochizuki (os três pass com `823063eb…`) e os uninstalls. Passou.
5. `gate.ps1 -Ui` sem falhas.
6. Release v0.6.3 do instalador, `publish-payload.ps1`, espelho no AMD-NR-Extras.

## Testado antes

- OptiScaler 0.4.2 com o runtime 0.4.1 no Cyberpunk 2077; add-on 0.6.9 no ETS2 (64-bit) e no GTA IV
  (ponte 32-bit), com a linha "danielblnc 0.4.1: rede X ms" conferida contra o log do runtime.
- A caixa do mochizuki na rota ReShade, pelo usuário, num build de teste desta versão.
