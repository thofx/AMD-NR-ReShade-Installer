# Handoff v0.6.2: o mochizuki também na rota ReShade

26/09/2026. Leia depois de [HANDOFF-v0.6.0-2026-09-25.md](HANDOFF-v0.6.0-2026-09-25.md), que explica
como o mochizuki entrou na rota OptiScaler. Este arquivo cobre só o que a v0.6.2 acrescenta.

## O que entrou

- **Add-on v0.6.8** (`zmodelerlover/dlss5-neural-amd`, tag `v0.6.8`): o add-on roda a rede pelo
  runtime do danielblnc (padrão) ou pelo mochizuki, escolhido no combo "NR runtime" do painel
  (`NrBackend` no `amd-nr.ini`, vale no próximo início). Com `NrBackend=mochizuki` e os arquivos
  ausentes, o add-on roda o danielblnc.
- **Na ficha das rotas ReShade** (64-bit e ponte 32-bit), a mesma caixa do mochizuki da rota
  OptiScaler, com o texto próprio (`Str.MochizukiCheckAddon`, `Str.MochizukiNoteAddon`). Aparece
  quando o add-on escolhido é 0.6.8 ou mais novo (`GameSheet.MochizukiAddon`) e o payload tem um
  build do mochizuki. Mesma trava de RDNA4 e mesma memória por jogo (`GameEntry.Mochizuki`).
- **Marcada**, a instalação põe `MochizukiNrRuntime.dll` e `dlssnr-amd\` ao lado do add-on (na
  ponte, ao lado do `amd-nr-host64.exe`, que roda a rede). **Desmarcada**, a próxima instalação tira
  o que uma anterior pôs, na mesma transação, como na rota OptiScaler.
- **O instalador não escreve o `amd-nr.ini`.** Ele é da pessoa e o add-on o regrava ao salvar, então
  uma entrada de configuração registrada viraria "PRESERVED" na instalação seguinte. O relatório diz
  para escolher mochizuki no painel. O fallback do add-on cobre o caso do ini que ainda nomeia o
  mochizuki depois de desmarcado.

## De onde vem o mochizuki da rota ReShade

`PayloadManifest.WithMochizuki()`: os componentes `mochizuki` e `mochizuki-model` do release mais
novo do OptiScaler que os carrega (hoje o 0.4.1-amd-nr, mochizuki 0.4.0-amd-nr e modelo 1). **O
formato do payload não mudou**: nenhum componente novo no topo, então os apps antigos não baixam
nada a mais no "Baixar tudo" e o v0.6.1 continua lendo o payload como antes. O `Selected()` da ficha
aplica isso às rotas ReShade, e o `PinnedFor` também, para o "desatualizado" comparar os arquivos do
mochizuki numa pasta ReShade que os tem.

Consequência: um OptiScaler novo com outro build do mochizuki troca também o da rota ReShade. Se um
dia o add-on precisar de um build diferente do do OptiScaler, o caminho é um componente próprio
dentro de um `releases` (onde os apps antigos não olham).

## Código

- Core: `Work.Preflight`/`Install` passam `mochizuki` às rotas ReShade (`PreflightReShade`,
  `InstallReShade`, `InstallX86`); `X86Installer.Install` aceita `extra` e `retire`;
  `InstalledManifest` e `HasMochizuki` olham os dois manifestos (x64 e x86); o uninstall chama
  `AfterMochizukiUninstall` em toda rota.
- App: `ComponentsFor` inclui os dois componentes nas rotas ReShade quando pedido;
  `OffersMochizuki` decide a seção nas três rotas.
- Testes: `ReShadeMochizukiTests` (6 casos, 260 no total).

## Publicação

1. Add-on v0.6.8: release no GitHub com os seis assets de sempre, baixados de volta e conferidos.
   `amd-nr.addon64` `7552204c…` (622.592 bytes), `amd-nr.addon32` `8bc7dd66…`, `amd-nr-host64.exe`
   `de44f95e…`, `payload.sha256` `a717b80c…`.
2. Os quatro arquivos no HF em caminhos com versão (`addon/0.6.8/`, `bridge/0.6.8/`), com o release
   do GitHub como espelho.
3. Ponta a ponta com o motor desta versão, cache vazio, tudo baixado dos endereços do
   `payload.json`: rota 64-bit com mochizuki, reinstalação, desmarcado (sai tudo), marcado de novo,
   uninstall; ponte 32-bit com mochizuki, desmarcado, uninstall. Passou.
4. `gate.ps1 -Ui` sem falhas.
5. Release v0.6.2 do instalador, `publish-payload.ps1`, espelho no AMD-NR-Extras.

## O que ficou de fora

- A linha de status do mochizuki no painel da ponte 32-bit (não passa pelo protocolo da ponte).
- Um flow de UI (`tools/uishot`) para a caixa na rota ReShade; o da rota OptiScaler continua.
