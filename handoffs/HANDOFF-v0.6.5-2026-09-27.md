# Handoff v0.6.5: OptiScaler 0.4.4 e o version.dll do autor como runtime

27/09/2026. Leia depois de [HANDOFF-runtime-0.4.2-2026-09-27.md](HANDOFF-runtime-0.4.2-2026-09-27.md). A rota
OptiScaler ganhou versão nova e o app aprendeu a aproveitar o runtime que o setup do danielblnc deixa na
pasta do jogo. O add-on continua o v0.6.9 e o runtime da rota ReShade continua o 0.4.1 com patches.

## O que mudou no payload

| Componente | Antes | Agora | Onde no dataset |
|---|---|---|---|
| release `0.4.4-amd-nr` | não existia | OptiScaler 0.4.4 (`5671297e…`), `opti-runtime` 0.4.2 (`8aa2dcc5…`, o mesmo do 0.4.3), pesos do lmxxf do 0.4.3 | nada novo além do mochizuki |
| `mochizuki` do 0.4.4 | não existia | `MochizukiNrRuntime.dll` `7e1f721e…`, archive `2e628e4d…` | `mochizuki/0.4.4-amd-nr/mochizuki-0.4.4-amd-nr.zip` |

- Os shaders do mochizuki mudaram: 58 arquivos, quatro novos (`g_ffwd3w`, `g_fswinpds128`, `g_fswinpup128`,
  `runtime/runtime_encode_in`). O manifesto de prewarm foi refeito para eles numa execução a frio em 1080p e
  depois em 1440p, porque o `ffwd3w` só aparece de 1440p para cima: 37 linhas. Com ele a rede ficou pronta em
  6,0 s num exe novo, contra 23,0 s sem.
- O build do mochizuki saiu da tag `v0.4.4-amd-nr` do opti, e a saída dele é idêntica byte a byte aos goldens
  novos (`golden-2026-09-27` no opti). O modelo continua a versão 1, já publicada.
- A rota ReShade pega o mochizuki do release mais novo (`WithMochizuki`), então também recebe o mais rápido.
  A ABI não mudou.

## O que o OptiScaler 0.4.4 traz

- mochizuki mais rápido: -0,48 a -0,58 ms em 1080p e -1,03 a -1,10 ms em 1440p na fila do jogo (RX 9070 XT).
  A entrada do pré-bloco é arredondada ao par mais próximo como na rede original, a única mudança na imagem.
- O `Setup.bat` do pacote escreve `auto` em `AmdEncoding` e `AmdEveryFrame`, em vez de forçar Linear com o
  histórico desligado. O app nunca rodou o `Setup.bat`, então as instalações dele não mudam.
- Estabilizador depois do runtime (danielblnc e mochizuki) e suavização só do efeito no lmxxf, desligados por
  padrão.
- Aceita a build de apoiador 0.5.0 (`cddfb09e…`, 38,7 MB) e recomenda a 0.4.2 pública.

## Código

- **O `version.dll` do autor vira o runtime.** Antes, um `version.dll` do danielblnc na pasta do jogo (o setup
  dele carrega o runtime assim) bloqueava a rota OptiScaler, e a pessoa tinha que tirar o arquivo e rodar de
  novo. Agora, quando o OptiScaler escolhido roda essa build, ela vira o runtime da instalação
  (`dlssnr_amd_pass1-3.dll`) no lugar do download, e o `version.dll` vai para o backup; o uninstall devolve
  ele byte a byte. Uma build que o OptiScaler escolhido não roda (a 0.5.0 no 0.4.3, a 0.2.17) continua
  bloqueando, com a mensagem dizendo isso.
- **Atualizações:** sem `version.dll`, a pass 1 que já está na pasta fica quando é uma build que o OptiScaler
  roda e mais nova que a do payload (a 0.4.3 ou a 0.5.0 contra a 0.4.2), e a pasta não aparece como
  desatualizada por isso. Uma mais velha que a do payload é trocada na próxima atualização.
- `Work.OptiScaler`: tabela `AcceptedRuntimes` (SHA, versão do runtime, primeira versão do OptiScaler cujo
  `AmdLayout.h` aceita: 0.3.0 e 0.3.1 desde 0.1.0, 0.4.0 desde 0.4.1, 0.4.1 desde 0.4.2, 0.4.2 e 0.4.3 desde
  0.4.3, 0.5.0 desde 0.4.4), `OwnRuntime`, `OwnRuntimeIsCurrent`; `CheckRuntimeAsVersionDll` informa em vez de
  bloquear quando a build serve, e olha arquivos de 7 a 40 MB. Um runtime novo aceito por um opti novo é uma
  linha nessa tabela.
- `Transaction.Apply`: parâmetro `displace`, um arquivo de outra pessoa tirado da pasta com backup. A entrada
  no manifesto é dona do nome com o hash de conteúdo vazio, e o uninstall restaura o backup. Se a pessoa pôr
  outro `version.dll` e instalar de novo, esse vira o original e o backup anterior é descartado.
- `Engine.Allowed` ganhou `version.dll` (nenhuma rota escreve esse nome; só é movido e devolvido).
- `PayloadPins.OptiScalerVersion` / `OptiRuntimeVersion`, lidos do payload.
- O app ainda baixa o runtime do payload quando usa o do jogo (o download é planejado antes de olhar a
  pasta); só não o instala.
- Um manifesto com `version.dll` não é lido por builds anteriores do app (o nome não estava na lista), que
  o tratam como pasta sem manifesto próprio. O atualizador automático deixa isso improvável.

## Testes

- `ADisplacedFileGoesToTheBackupAndUninstallPutsItBack`, `AnAuthorRuntimeIsRunOnlyByAnOptiScalerThatAcceptsIt`
  e o teste do `payload.json` real com o 0.4.4 primeiro: 263 no total.
- Local e temporário, fora do repositório: os `version.dll` reais 0.4.1, 0.4.3 e 0.5.0 com um payload de
  mentira (instalar, atualizar, desatualizado, desinstalar).

## Publicação

1. opti `v0.4.4-amd-nr` (zip `5671297e…`, baixado de volta e conferido).
2. `tools/pin-optiscaler.ps1` com o zip baixado do release; `opti-runtime` 0.4.2 copiado do 0.4.3.
3. `tools/pin-mochizuki.ps1 -Version 0.4.4-amd-nr` com o build da tag; o archive subiu para o HF e foi
   baixado de volta (`2e628e4d…`).
4. Ponta a ponta com cache vazio, tudo baixado dos endereços do `payload.json` (teste temporário
   `ZzLiveE2E`): OptiScaler 0.4.3 instalado e marcado como desatualizado, atualização para o 0.4.4 com
   mochizuki (as três pass `8aa2dcc5…`, mochizuki `7e1f721e…`, manifesto de prewarm), uninstall; e o 0.4.4
   por cima dos `version.dll` 0.4.3 e 0.5.0 do autor, que viraram as pass, não marcaram a pasta como
   desatualizada e voltaram no uninstall. Passou.
5. `gate.ps1 -Ui` sem falhas.
6. Release v0.6.5 do instalador, `publish-payload.ps1`, espelho no AMD-NR-Extras.

## Testado antes

- Nada deste release foi testado em jogo. As medidas do mochizuki e dos estabilizadores vêm de harness sem
  jogo (detalhes no `handoff/README.md` do opti, seção 0.4.4).
