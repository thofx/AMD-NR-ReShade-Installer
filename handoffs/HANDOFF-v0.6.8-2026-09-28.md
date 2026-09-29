# Handoff v0.6.8: OptiScaler 0.4.5, add-on v0.7.2 e a build de apoiador 0.5.1

28/09/2026. Leia depois de [HANDOFF-v0.6.6-2026-09-27.md](HANDOFF-v0.6.6-2026-09-27.md) e dos commits do v0.6.7.

## O que mudou no payload

| Componente | Antes | Agora |
|---|---|---|
| release `0.4.5-amd-nr` | não existia | OptiScaler 0.4.5 (`16300032…`, do GitHub), `opti-runtime` 0.4.3, pesos do lmxxf, mochizuki e modelo da 0.4.4 (o runtime não mudou) |
| `addon` / `bridge` | 0.7.1 | 0.7.2 (`8b025a26…`, `ac0ac755…`, `b8dc7246…`), só a 0.5.1 a mais |
| `user_runtimes` | 0.5.0 | 0.5.0 e 0.5.1 (`493b4a3b…`, corrigida `af67f066…`, `addon_since` 0.7.2) |

A 0.5.1 é build de apoiador do danielblnc: **não é distribuída**, nem aqui nem no HF. O payload só diz como
reconhecer e corrigir o arquivo que a pessoa já tem.

## Código

- `Work.Runtimes`: `KnownRuntimePrefixes` e `AcceptedRuntimes` com a 0.5.1, rodada pelo OptiScaler a partir da 0.4.5.
- Testes: o payload real com 0.4.5 primeiro e as duas builds de apoiador; o teste com o setup real
  (`AMDNR_TEST_RUNTIME_SETUP`) usa o primeiro OptiScaler que roda a build e passou com o setup da 0.5.0 e o
  da 0.5.1.

## O resto do release

- opti `v0.4.5-amd-nr`: a correção do ReShade como `dxgi.dll` (mochizuki e lmxxf ficavam desligados) e `kAmd051`.
- add-on `v0.7.2`: a 0.5.1 no `runtime_offsets.h` e no `runtime-patches.json`. Compilado nesta máquina com o
  FidelityFX SDK v2.3.0 do GitHub e um GCC 16 portátil para os gates (o MinGW 6.3 do PATH não compila C++20).
  O `framecheck` rodou 20 quadros com a 0.5.1 corrigida sem jogo.
- Nada disso foi testado em jogo com a 0.5.1.
