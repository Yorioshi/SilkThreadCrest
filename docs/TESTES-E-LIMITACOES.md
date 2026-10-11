# Testes e limitações — Validação do SilkThreadCrest

**Base documentada:** 0.7.25  
**Escopo:** matriz manual granular, prioridades P0/P1/P2, diagnóstico, dependências de build, evidência de implementação e riscos técnicos.  
**Autoridade:** o código atual em [`src/`](../src/) prevalece sobre documentação histórica. O README e os changelogs servem apenas para registrar evidências de execução já informadas.

> Este documento não declara compatibilidade com todos os inimigos, plataformas, versões do jogo ou outros mods. Cada combinação não executada permanece uma hipótese a validar.

## 1. Resultado executivo

A versão 0.7.25 contém três áreas que precisam ser validadas de forma diferente:

1. **Combate por turnos e Brasão do Fio:** lógica integrada a `HeroController`, `HealthManager`, `PlayerData`, input, física, animação e menus nativos por Harmony/reflection.
2. **Persistência e invocações:** extensão do save nativo, fallback sidecar por slot e clones visuais somente para apresentação.
3. **Aparência:** menu F8, paletas/packs e Silk Eclipse. Os testes isolados verificam seleção de poses, binding de materiais e leitura de recurso, mas não substituem o jogo Windows.

### Status conhecido da base 0.7.25

| Área | Status e evidência | O que ainda não significa |
|---|---|---|
| Combate | **Funcionamento confirmado pelo usuário.** O fluxo está implementado em [`TurnBattleSystem.cs`](../src/TurnBattleSystem.cs), com patches de apoio no plugin. | Não prova todos os tipos de inimigo, ataques multi-hit, bosses ou estados especiais.
| Invocações/save | **Invocações no save confirmadas na 0.7.15**; a implementação atual cobre JSON nativo e sidecar em [`SilkThreadSavePatches.cs`](../src/SilkThreadSavePatches.cs). | Não substitui um teste de salvar, fechar, carregar em cada rota de save da versão local.
| Menu e paletas | **Menu e paletas confirmados na 0.7.24**; o código atual aplica restauração, cópias de materiais e máscaras por cor. | Não garante que toda coleção, resolução ou atlas externo seja classificado corretamente.
| Silk Eclipse | A 0.7.24 apresentou tamanho/animações incorretos. A 0.7.25 corrigiu a fonte do sync para o animator corporal real, escala baseada no Idle e orientação; foi compilada e passou por **30 testes isolados do seletor**. | **Ainda não confirmada in-game.** Alinhamento, troca skin/native, leitura da folha e aparência em cena continuam P0.
| Testes existentes | **10 stubs** de binding/recurso e **30 testes** de seleção de pose. Ver [§3](#3-testes-existentes-e-o-que-eles-provam). | São testes sem renderização Unity; não são uma suíte de integração do jogo.

## 2. Como usar e como reproduzir a validação

### 2.1 Instalação de teste

1. Feche o jogo e faça uma cópia do save antes de testar persistência.
2. Remova cópias antigas da modificação para não carregar duas versões do plugin.
3. Instale a saída da build em `BepInEx/plugins/SilkThreadCrest/SilkThreadCrest.dll`.
4. Inicie o jogo e confirme **0.7.25** no título do menu visual F8.
5. Para um teste visual, pressione F8 fora de uma batalha. O atalho é configurável em `Appearance / MenuShortcut`; o código inicializa isso em `SilkThreadCrestPlugin.Awake()`.
6. Para uma batalha, aproxime Hornet de um inimigo vivo e ativo, estando no chão, até a busca encontrar um alvo. Pressione a binding de `TAUNT` uma vez. A busca é refeita no frame do comando; ela não depende apenas do prompt anterior.

Durante uma batalha, cima/baixo ou esquerda/direita selecionam ações, salto executa, dash recua e ataque abre o seletor próprio de brasões. Os seletores de brasão e invocação pausam temporariamente `Time.timeScale`; o combate normal não pausa o cenário.

### 2.2 Build contra as assemblies recebidas

O projeto é SDK-style e define `<TargetFramework>net472</TargetFramework>` em [`SilkThreadCrest.csproj`](../src/SilkThreadCrest.csproj). A compilação exige:

- SDK do .NET compatível com `net472` e o pacote de assemblies de referência usado pelo projeto;
- assemblies recebidas do jogo no diretório informado por `GameManagedDir`;
- assemblies recebidas do BepInEx no diretório informado por `BepInExCoreDir`;
- Harmony/Unity/BepInEx e demais referências resolvidas pelo `.csproj`.

Use diretórios locais da instalação de teste, sem embutir DLLs do jogo ou do BepInEx no repositório:

```text
dotnet build src/SilkThreadCrest.csproj -c Release \
  -p:GameManagedDir="<diretorio-local-das-assemblies-do-jogo>" \
  -p:BepInExCoreDir="<diretorio-local-do-BepInEx-core>"
```

Os valores padrão do projeto são `../Managed` e `../deps/BepInExPack/BepInExPack/BepInEx/core`, relativos ao diretório do projeto. A build deve ser feita contra as assemblies recebidas da versão em teste; uma compilação bem-sucedida só prova resolução de referências e compilação C#, não execução no jogo.

O carregamento de PNG usa `UnityEngine.ImageConversion.LoadImage` por reflection. A folha Silk Eclipse também é incluída como recurso embutido pelo `.csproj`; o carregador tenta a folha externa em `Skins/SilkEclipse/Texture2D/` e usa o recurso embutido quando necessário.

### 2.3 Retomada e regressão

Após qualquer alteração em combate, save, reflection ou aparência:

1. faça build limpa;
2. repita os testes isolados de binding e pose;
3. repita pelo menos todos os casos P0 da matriz;
4. feche e reabra o jogo entre testes de save, cena e menu;
5. valide novamente a restauração de controle, posição, brasão e aparência ao sair;
6. preserve o save de controle e compare o resultado com o backup, sem apagar sidecars durante a investigação.

A “retomada” deve incluir tanto continuar uma sessão após fechar/carregar quanto retornar de uma batalha, menu F8, mudança de cena, morte, recuo e erro de reflection. Uma correção local não é considerada segura se regressar o fluxo já confirmado de combate ou de invocação.

## 3. Testes existentes e o que eles provam

### 3.1 Os 10 stubs de binding/recurso

[`tests/material-binding/BindingTests.cs`](../tests/material-binding/BindingTests.cs) cria tipos mínimos para `Material`, `tk2dSpriteDefinition`, coleção e sprite. Os 10 casos exercitam o helper real `SilkThreadMaterialBinding` e `SilkThreadSkinAssetReader`:

- vínculo de um frame ao novo material;
- leitura de `materialInst` pelo frame;
- atualização do array de materiais da coleção;
- restauração exata das referências asset/runtime;
- preferência pela coleção de runtime (`collectionInst`);
- resolução segura quando `materialId` é inválido, mas a referência original coincide;
- rejeição de frame não correspondente para evitar atlas errado;
- leitura do PNG embutido quando o caminho externo não existe;
- prioridade do arquivo externo quando ele existe;
- erro explícito quando faltam arquivo externo e recurso embutido.

O próprio stub imprime que valida referências, **não renderização Unity**. Ele não cobre shaders, `Renderer`, atlas real, ciclo de cena, `UpdateMaterial` em runtime nem aparência na tela.

### 3.2 Os 30 testes do seletor de poses

[`tests/eclipse-poses/PoseTests.cs`](../tests/eclipse-poses/PoseTests.cs) chama [`SilkThreadEclipsePose.Select`](../src/SilkThreadEclipsePose.cs) diretamente. A contagem é incrementada por cada `Check`; os 30 casos incluem:

- Idle no início e no fim;
- Run/Walk em início/fim e índices fora do intervalo;
- clip sem frames;
- ataque no início, faixa central e fim;
- ataque alternativo e ataque carregado;
- Bind/Focus;
- Fall/Jump;
- Double Jump, Wall Slide, DownSpikeBounce e UpSlash como não cobertos;
- Idle Hurt, Dash To Idle, ToolThrow, Wake Up Ground, Harpoon Catch, Super Jump Fall;
- Unknown Special, nome nulo;
- Shuttlecock e Fireball.

A regra testada é seletiva: `-1` preserva a arte nativa; Idle/Walk usam duas poses por progresso, Jump/Fall/Bind/Focus têm poses dedicadas e ataque só usa a pose de golpe na faixa central do clip. O teste não cria `HeroController`, animator, renderer, textura, escala, pivot ou cena.

## 4. Como funciona internamente

### 4.1 Inicialização, registro e patches

`SilkThreadCrestPlugin.Awake()` registra o menu de aparência, cria o `Harmony`, aplica classes de patch e tenta instalar separadamente os quatro patches opcionais de save. Uma falha no menu é isolada com warning; uma falha de patch individual é registrada e não impede os demais patches. `Update()` espera `ToolItemManager`, `PlayerData` e `InventoryToolCrestList.IsSetupComplete` antes de clonar/desbloquear o Brasão do Fio e registrar Sacrifício.

O brasão é clonado de `Hunter` quando disponível, ou do primeiro brasão existente. O registro usa reflection para campos internos, `crestList` e `toolItems`; o menu nativo é atualizado por `InventoryToolCrestList.Setup`. Isso explica por que o registro depende fortemente das assemblies da build local.

### 4.2 Combate e fórmulas

O alvo é o `HealthManager` inimigo vivo, ativo e na camada de inimigos (ou raiz nessa camada) mais próximo dentro de **8,0 unidades**. A varredura ocorre a cada **0,25 s**. A batalha só inicia com `GameState.PLAYING`, Hornet no chão, TAUNT pressionado e nenhum duelo ativo.

Constantes relevantes em [`TurnBattleSystem.cs`](../src/TurnBattleSystem.cs):

| Regra | Fórmula/valor atual |
|---|---:|
| Preparação | 0,45 s não escalados |
| Ação ofensiva normal | 0,72 s |
| Cura | 0,35 s |
| Invocação | 0,95 s |
| Ação inimiga | 1,15 s |
| Reação após hit | 0,55 s |
| Resultado de vitória | 3,2 s |
| Recuo/derrota | 1,0 s |
| Cura | custo base 9 seda; recupera até 3 HP |
| Invocação | custo base 3 seda |
| Golpe normal | 0 seda na ação do mod; dano usa `PlayerData.instance.nailDamage` |
| Skill | custo `PlayerData.instance.SilkSkillCost`; fallback de dano `2 × nailDamage` |
| Defender-se | próximo dano `max(1, ceil(dano × 0,5))`; recompensa base 2 seda se perder HP de verdade |
| Trocas de brasão | no máximo 1 por turno e 3 por batalha |
| Ferramentas limitadas | no máximo uma ativação de dano por ferramenta em cada turno de Hornet |

Sinergias de [`CrestSynergy.cs`](../src/CrestSynergy.cs) podem reduzir custo de skill/cura/invocação até o mínimo de 1, aumentar seda de golpe normal ou aumentar recompensa de defesa. O valor efetivo exibido deve ser conferido no estado do save em teste; não use valores fixos históricos como substituto.

O combate guarda posição, velocidade, gravidade, constraints, escala e brasão inicial. `EndBattle()` tenta restaurar o brasão, `Time.timeScale`, física, posição e input. O ataque nativo tem fallback de `HealthManager.Hit` somente quando não houve dano e o alvo não está invulnerável. O dano de ferramenta passiva/objeto é limitado por chave de ferramenta e reiniciado no próximo turno de Hornet.

### 4.3 Save, clone e invocação

[`EnemySummonRegistry.cs`](../src/EnemySummonRegistry.cs) captura o inimigo no início do duelo, coleta `DamageHero` que passam pelos filtros de ataque seguro e só confirma a invocação depois da derrota. O dano capturado é no mínimo 1. Componentes de contato, aura, corpo, hazard, espinhos, dano passivo e nomes/tags correspondentes à blacklist são excluídos; se nada passar, o registro usa `Golpe básico` de 1 dano.

O template de invocação é um clone visual: scripts que não sejam os animadores/sprites preservados são destruídos, colliders são desativados, rigidbodies deixam de simular e a camada é alterada para `Ignore Raycast`. O dano, porém, é aplicado uma vez ao inimigo real por `enemy.Hit`, com fallback `ApplyExtraDamage` apenas quando não houve dano e o alvo não está invulnerável.

[`SilkThreadSavePatches.cs`](../src/SilkThreadSavePatches.cs) injeta `SilkThreadCrest` no resultado do método fechado `GameManager.GetJson<SaveGameData>`. No carregamento, cobre `SetLoadedGameData(string, int)` e `SetLoadedGameData(SaveGameData, int)`. Em paralelo, `QueueGameSave()` escreve `SilkThreadCrest/summons-slot-N.json` em `Application.persistentDataPath`; se o bloco nativo faltar ou estiver vazio, o sidecar é tentado. `StartNewGame` limpa o sidecar e o registro em memória.

### 4.4 Menu, paletas e Silk Eclipse

`SilkThreadAppearanceMenu` salva a seleção em `appearance.json`, descobre subpastas de `Skins`, aplica presets e faz backup de materiais, texturas, frames e cores. Paletas usam regiões UV e uma máscara aproximada por cor; quando não há atlas legível, há fallback por identidade de sprite. `RestoreAllCollections()` deve ser exercitado após cada troca de preset e ao destruir o componente.

`SilkThreadEclipseVisual.Apply()` usa `HeroController.AnimCtrl.animator`, captura a referência geométrica do primeiro Idle nativo, carrega a folha 4×2 e cria oito sprites. `Tick()` escolhe a pose com o clip/frame nativo, oculta apenas o renderer corporal via `forceRenderingOff` quando há pose válida e volta à arte nativa para ações não cobertas. A escala é calculada pela altura Idle e pela largura de referência da folha; pivots e flip são aplicados por pose. Isso corrige a fonte do sync da 0.7.24, mas ainda exige verificação visual no jogo.

## 5. Matriz manual granular

**Legenda:** P0 = bloqueia confiança mínima/risco de perda ou soft-lock; P1 = funcionalidade importante ou regressão provável; P2 = cobertura ampliada, acabamento ou cenário raro. “Evidência” indica o que já existe, não aprovação automática do caso.

### P0 — executar antes de distribuir uma nova build

| ID | Cenário e passos | Resultado esperado | Diagnóstico/evidência |
|---|---|---|---|
| P0-01 | Build limpa com as assemblies recebidas e `dotnet build ... -c Release`. | Compila sem substituir dependências externas nem alterar `src/`. | `SilkThreadCrest.csproj`; build é evidência de compilação, não de runtime. |
| P0-02 | Iniciar jogo com apenas uma cópia; abrir F8. | Título mostra 0.7.25; menu abre/fecha e restaura cursor. | `Appearance runtime 0.7.25`; falha do menu não deve impedir batalha. |
| P0-03 | Selecionar Original, Carmim, Paleta personalizada, Ocre e voltar a Original em cena ativa. | Aplicação não deixa material/textura/cor modificados após restauração; combate continua iniciável. | `Appearance binding ...`; `Appearance renderer bound/unbound`. |
| P0-04 | Selecionar Silk Eclipse com folha externa presente e depois com ela ausente, usando o recurso embutido. | Folha carrega; se não carregar, arte original permanece e o jogo continua. | `Silk Eclipse loading embedded sheet from DLL` ou `loading external sheet`; `sheet load failed`. |
| P0-05 | Silk Eclipse: parado, andar para esquerda/direita, salto, queda, ataque simples, cura. | Pose acompanha clip/frame, escala/alinhamento ficam estáveis e orientação não fica invertida. | `Silk Eclipse sync: clip=...; native frame=...; pose=...; mode=...`. **Ainda não confirmado in-game.** |
| P0-06 | Silk Eclipse: dash, pogo/downspike, parede, ferramenta, dano, morte, cutscene e respawn. | Arte nativa aparece no fallback; mecânica não é interrompida nem substituída por idle incorreto. | `mode=native fallback`; observar troca visual, sem declarar cobertura completa. |
| P0-07 | Iniciar batalha a 8 unidades ou menos estando no chão; repetir no ar e sem alvo elegível. | Só o primeiro caso inicia; ar/sem alvo não inicia. | Logs `in-world battle started` ou `no eligible enemy ... 8 world units`. |
| P0-08 | Executar Golpe do brasão contra alvo vulnerável; repetir quando hitbox nativa não registrar. | Dano usa `nailDamage`; fallback ocorre no máximo uma vez e não atravessa invulnerabilidade. | `native-damage fallback`; `target is invulnerable`. |
| P0-09 | Usar skill com binding disponível, indisponível, alvo invulnerável e custo insuficiente. | Tenta evento nativo; cobra custo restante sem reembolso inventado; fallback é `2 × nailDamage` somente em alvo elegível. | `native skill action failed`, `native skill`, `fallback attack failed`. |
| P0-10 | Defender-se contra ataque que acerta, ataque que erra, dano ímpar e Hornet com pouca vida. | Dano é `max(1, ceil(dano × 0,5))`; +2 seda (mais sinergia) somente se HP real diminuir; turno encerra uma vez. | `defend reduced enemy damage ...`; `defend action rewarded ...`. |
| P0-11 | Sair por dash, vencer, perder e destruir/recarregar a cena durante batalha. | Posição, gravidade, constraints, velocity, brasão, `Time.timeScale` e input retornam ao estado seguro; não fica controle preso. | `battle ended; area positions and game time restored`; warnings de restauração. |
| P0-12 | Vencer um inimigo ainda não aprendido; fechar o jogo imediatamente após a derrota; carregar o mesmo slot. | Invocação é confirmada antes da sequência de morte, fica no JSON nativo ou sidecar e reaparece após carregar. | `confirmed summon unlock`, `learned summon`, `appended summon data`, `restored ... summon`. |
| P0-13 | Testar as duas rotas de carregamento: string JSON e `SaveGameData` tipado, com slot diferente do `profileID` corrente. | O registro correto do slot é restaurado; slot novo não herda invocações antigas. | `restored summon data from the slot sidecar fallback`. |
| P0-14 | Começar novo jogo após um save com invocação. | Registro em memória e sidecar do slot são limpos; não há invocação vazada para o novo jogo. | `NewGame` chama `ClearSidecar()` e `RestoreFromSaveData(null)`; confirmar no jogo. |
| P0-15 | Abrir Invocar após aprender golpe; executar com visual disponível e com template indisponível. | Custo base 3 (ou sinergia, mínimo 1), dano capturado uma vez ao alvo real; falha visual não cancela ação/dano/turno. | `summon visual was unavailable` não deve impedir `summon attack`/resultado. |

### P1 — executar para confirmar robustez funcional

| ID | Cenário e passos | Resultado esperado | Diagnóstico/evidência |
|---|---|---|---|
| P1-01 | Trocar brasão no seletor durante batalha, tentar duas vezes no mesmo turno e exceder três na batalha. | Uma troca por turno, três no total; abrir/fechar sem gastar turno; ações são reconstruídas. | `battle crest changed ... swaps=n/3`; mensagens de limite. |
| P1-02 | Usar ferramentas vermelhas com estoque, sem estoque, binding ausente e uso que deixa objeto ativo. | Ação mostra estoque real; `ThrowTool` nativo consome unidade; ferramenta vazia é recusada; dano repetido limitado por turno. | `limited damage activation` / `suppressed repeated damage activation`. |
| P1-03 | Testar ataque inimigo que acerta antes do fim de 1,15 s, não acerta e acerta várias vezes. | Primeiro hit encerra a janela; se nenhum hit ocorrer, fallback básico único; não há múltiplos danos por turno. | Logs de fallback; `RecordEnemyTurnHit` e `enemyTurnHitResolved`. |
| P1-04 | Testar inimigo sem clip `Idle` ou sem animator tk2d. | Posição/IA permanecem governadas pelo duelo; animação pode terminar, mas não deve travar o jogo. | `could not find an idle clip`; `found no 2D animator`. |
| P1-05 | Vencer com drops já presentes, drops novos, pickup atrasado e inventário cheio, dentro e fora de 12 unidades. | Só drops novos próximos são tentados; pickup atrasado/cheio não gera duplicação nem exceção fatal. | `could not collect ...`; conferir inventário. |
| P1-06 | Derrotar duas instâncias com o mesmo nome e carregar save com nomes alterados. | Chave normalizada evita duplicação acidental conhecida; registrar o risco de colisão de nomes. | `summon already learned`; revisar `MakeKey`. |
| P1-07 | Trocar Original ↔ pack PNG ↔ paleta várias vezes, recarregar packs e mudar de cena. | Backups não acumulam clones; materiais de runtime e frames voltam exatamente aos originais. | Contadores `frames`, `active renderers`, `changed pixels`; observar memória/console. |
| P1-08 | Paleta forte separada para capa, cabeça e sob a capa em poses/coleções distintas. | Máscaras mudam pixels esperados e preservam contornos; partes com cor semelhante podem compartilhar grupo. | `changed pixels`; `fallback parts cape/head/under-cloak`. |
| P1-09 | Pack com PNG ilegível, nomes parciais, atlas duplicado e pasta vazia. | Arquivo ruim é ignorado; nomes são normalizados para associação; nenhuma coleção é trocada incorretamente. | `Could not load skin atlas`; `loaded ... atlas(es)` / `changed ... collection(s)`. |
| P1-10 | Fechar F8 via tecla, botão e troca para batalha; interromper cena com cursor capturado. | Cursor/lock state anterior é restaurado e o menu fecha ao começar batalha. | `SilkThreadAppearanceMenu.Update`, `CloseMenu`; testar visualmente. |
| P1-11 | Alternar idioma após abrir menu/combate e usar labels de ação/troca. | Traduções injetadas continuam presentes; labels não ficam vazios. | warnings `localization injection failed`; conferir prompts. |
| P1-12 | Repetir save/load após várias invocações, sidecar vazio/corrompido e bloco nativo ausente. | Fallback restaura dados válidos; dados inválidos limpam com warning sem impedir batalha. | `Could not restore ...`; `RestoreFromSaveData(null)` quando apropriado. |

### P2 — ampliar cobertura e acabamento

| ID | Cenário e passos | Resultado esperado | Diagnóstico/evidência |
|---|---|---|---|
| P2-01 | Variar resoluções, escala de UI, janela/tela cheia e proporções extremas. | HUD/menu continuam legíveis e dentro da tela. | Inspeção visual; não há teste automatizado de layout. |
| P2-02 | Repetir a matriz em teclado remapeado e controles com identificadores Xbox/PlayStation/Switch/desconhecidos. | Labels refletem binding ativa ou fallback compreensível; comandos continuam funcionais. | `RefreshBindingLabels`; `FormatGamepadBinding`. Não inferir suporte além do dispositivo testado. |
| P2-03 | Testar cena com várias coleções tk2d, sprites pooled e renderer inativo/reativado. | Apenas coleções de runtime realmente usadas são alteradas; reaplicação ocorre quando Hornet é recriada. | `Appearance binding`; `OnSceneLoaded`; `RuntimeCollection`. |
| P2-04 | Inimigos voadores, hazards, invulnerabilidade de fase, ataques prolongados, multi-hit e bosses. | Registrar comportamento por caso; nenhum resultado positivo é extrapolado para todos. | Logs de fallback/IA; classificar como compatibilidade não confirmada. |
| P2-05 | Medir custo de aplicar paleta em atlas grande e alternar skin repetidamente. | Sem stutter, crescimento de memória ou clones persistentes perceptíveis. | Profiler/observação no jogo; não há benchmark no repositório. |
| P2-06 | Testar idioma, nome de inimigo longo, nomes duplicados e ataques sem clip. | Labels continuam utilizáveis e ordenação permanece determinística. | `CleanName` limita a 42 caracteres; `GetAllAttacks` ordena por dano/nome/ordem. |
| P2-07 | Sair para menu, carregar cena e destruir o plugin enquanto Silk Eclipse ou paleta estão ativas. | `Clear`, `RestoreAllCollections` e `OnDestroy` liberam clones e restauram renderers. | Sem exceção no log; verificar arte original depois. |
| P2-08 | Retomar o jogo depois de crash/fechamento durante vitória, save ou troca de preset. | Save nativo/sidecar consistente ou warning recuperável; não afirmar atomicidade sem teste. | Comparar backup, JSON e mensagens de save. |

## 6. Logs diagnósticos: coleta e interpretação

Use somente as linhas relevantes do log local da instalação (normalmente `BepInEx/LogOutput.log`; confira o nome real gerado pela sua instalação) ao reportar um problema; não inclua saves, dados de conta, binários privados ou o log inteiro.

| Procure por | Indica |
|---|---|
| `Silk Thread Crest loaded` | Plugin carregou e aguarda os managers.
| `Turn-based battle system attached to Hornet` | `TurnBattleSystem` foi anexado.
| `Waiting for InventoryToolCrestList...` | Registro ainda espera o menu nativo terminar `Setup`.
| `Registered and unlocked the new crest` / `Registered and unlocked item Sacrifício` | Registro/desbloqueio dos itens.
| `Silk Thread in-world battle started` / `battle ended` | Entrada/saída do FSM de batalha.
| `native skill action failed` / `native-damage fallback` | Caminho nativo falhou ou hitbox não reduziu HP; conferir invulnerabilidade.
| `defend reduced enemy damage` / `defend action rewarded` | Defesa reduziu dano ou concedeu recompensa após dano real.
| `learned summon`, `confirmed summon unlock`, `summon attack` | Captura, confirmação e execução de invocação.
| `appended summon data`, `wrote the summon sidecar`, `restored ... summon` | Escrita/leitura de persistência.
| `optional save patch` | Instalação isolada do patch; se falhar, batalha continua, mas invocações podem ficar apenas na sessão.
| `Appearance runtime 0.7.25` | Menu aponta para a assembly efetivamente carregada.
| `Appearance binding 0.7.25` | Frames vinculados, renderers ativos, pixels alterados e preset.
| `Appearance renderer bound` / `Appearance unbound renderer` | Diagnóstico de material ativo ou ausência de binding.
| `Silk Eclipse loading embedded/external sheet` | Origem da folha PNG.
| `Silk Eclipse sync` | Clip, frame nativo, pose, modo `skin` ou `native fallback` e altura Idle.
| `sheet load failed`, `Could not read...`, `Could not refresh...` | Falha recuperável de recurso, textura ou reflection; testar fallback e restauração.

Um log “bonito” não substitui o resultado visual: `Appearance binding` pode contar referências corretas enquanto um shader, pivot, sorting order ou câmera ainda apresenta erro.

## 7. Riscos técnicos e limites atuais

### Save e retomada

- O save depende de localizar o método genérico `GetJson` e fechá-lo com `SaveGameData`; uma alteração de assinatura pode impedir a instalação do patch.
- JSON nativo e sidecar são redundantes, não transacionais. Fechamento/crash durante escrita, slot trocado ou sidecar corrompido exigem P0-12/P0-14/P1-12.
- `GetProfileId()` usa reflection em `GameManager.profileID`; a rota tipada recebe o slot diretamente, mas a rota string/sidecar depende do perfil disponível.
- O registro de invocação usa nome normalizado como chave. Inimigos com o mesmo nome podem colidir; nomes diferentes para a mesma entidade podem duplicar aprendizado.

### Clone e reflection

- Brasão, Sacrifício, template visual e materiais são clones. Campos privados, listas, `materialInst`, animator, layers e componentes podem mudar entre assemblies.
- `DestroyImmediate` em componentes do clone visual é intencional, mas qualquer componente visual não reconhecido pela lista de tipos pode ser removido; qualquer componente lógico não removido pode permanecer como efeito colateral.
- Fallback de dano é deliberadamente bloqueado quando a consulta de invulnerabilidade falha; isso evita dano fantasma, mas pode deixar uma ação sem dano.
- A busca de `Idle`, `GetClipByName`, `Resume`, `UpdateMaterial` e `UpdateColors` por reflection pode falhar sem erro de compilação.

### Menu, materiais e paletas

- A paleta usa máscaras aproximadas por cor e identidade de objeto, não segmentação anatômica por frame. Acessórios podem receber a cor da capa/cabeça/sob a capa.
- A aplicação copia texturas inteiras e clona materiais. Atlas grandes ou alternâncias frequentes podem custar memória/tempo; não há benchmark automatizado.
- Coleções pooled, renderers recriados e mudanças de cena podem invalidar backups/bindings; o `LateUpdate` tenta reaplicar, mas P1-07/P2-03 são necessários.
- A folha Silk Eclipse tem oito poses. Ataques especiais, dash, pogo, parede, ferramentas, morte, dano e cutscenes usam arte nativa. A troca entre duas artes é uma limitação conhecida, não falha a ser mascarada como compatibilidade completa.

### Combate e desempenho

- O cenário não é pausado durante o combate normal; outros sistemas continuam atualizando. Só o alvo e Hornet são governados pelo FSM do mod. Isso pode expor hazards, scripts externos ou efeitos fora do duelo.
- O controlador usa buscas globais (`FindObjectsByType`) para inimigos, drops, pickups e reidratação visual. Em cenas densas isso pode causar custo de CPU e resultados dependentes do timing.
- Ataques multi-hit, prolongados, eventos especiais, invulnerabilidade de fase e ferramentas que deixam objetos ativos não equivalem automaticamente a “um ataque por turno”. O código limita ferramentas passivas/ativas em certos casos, mas cada variante deve ser exercitada.
- O fallback de drop procura apenas objetos novos num raio de **12 unidades** e respeita disponibilidade do inventário; drop tardio ou criado fora do raio pode não ser recolhido.

### Compatibilidade não demonstrada

Não está demonstrado, por esta matriz, comportamento para **todos** os inimigos, bosses, hazards, cenas, plataformas de controle, resoluções, shaders, versões do jogo ou combinações com mods. O resultado correto para um caso não testado é “não testado”, não “compatível”.

## 8. Separação entre teste unitário e teste in-game

| Tipo | O que consegue verificar | O que não consegue verificar |
|---|---|---|
| Unitário/stub | Seleção determinística de pose; referências e fallback de material/recurso; limites, nomes e índices artificiais. | Animator real, renderer, textura na GPU, pivot/escala, input, FSM, física, save, cena e timing.
| Build | Target net472, compilação C# e resolução das referências fornecidas. | Método realmente encontrado por reflection em runtime, comportamento Windows, integração de versão ou apresentação.
| In-game manual | Fluxo real de batalha, menu, cursor, input, dano, save/load, clones, animação, performance e restauração. | Cobertura universal; um caso manual não representa todos os inimigos/mods/plataformas.

A aprovação da 0.7.25 exige a combinação: **build limpa + 10 stubs + 30 poses + casos P0 no jogo Windows**. Os dois primeiros itens já fornecem evidência limitada; a confirmação in-game da Silk Eclipse e a repetição dos P0 continuam pendentes quando não houver registro explícito.

## 9. Propostas futuras — separadas do que já existe

As propostas abaixo **não são funcionalidades presentes**:

- criar um harness de integração com cena Unity controlada para animator, renderer, pivot, escala e troca de cena;
- adicionar testes de save para JSON nativo, sidecar, slot, JSON corrompido e novo jogo;
- registrar uma chave estável de entidade quando a API do jogo permitir, em vez de depender apenas de nome;
- medir tempo/memória de atlas, paleta e `FindObjectsByType` em cenas pequenas e densas;
- ampliar a folha Silk Eclipse somente com arte e mapeamento validados, sem transformar as oito poses atuais em promessa de cobertura total;
- adicionar uma matriz de inimigos escolhidos por comportamento (voador, hazard, multi-hit, invulnerável, drop tardio), em vez de alegar cobertura por quantidade;
- tornar escrita nativa/sidecar mais resiliente a interrupção, com validação e eventual arquivo temporário/renomeação atômica;
- ampliar os projetos de teste portáveis já incluídos em `tests/`, mantendo explícita a separação de testes sem Unity e testes no jogo. Os scripts `scripts/test.sh` e `scripts/test.ps1` executam os dois projetos; a CI não compila o plugin sem as assemblies locais.

## 10. Referências internas

- Código principal: [`SilkThreadCrestPlugin.cs`](../src/SilkThreadCrestPlugin.cs), [`TurnBattleSystem.cs`](../src/TurnBattleSystem.cs), [`SilkThreadSavePatches.cs`](../src/SilkThreadSavePatches.cs), [`SilkThreadAppearanceMenu.cs`](../src/SilkThreadAppearanceMenu.cs), [`SilkThreadEclipseVisual.cs`](../src/SilkThreadEclipseVisual.cs).
- Apoio técnico: [`EnemySummonRegistry.cs`](../src/EnemySummonRegistry.cs), [`SilkThreadEclipsePose.cs`](../src/SilkThreadEclipsePose.cs), [`SilkThreadMaterialBinding.cs`](../src/SilkThreadMaterialBinding.cs), [`CrestSynergy.cs`](../src/CrestSynergy.cs).
- Testes isolados: [`BindingTests.cs`](../tests/material-binding/BindingTests.cs) e [`PoseTests.cs`](../tests/eclipse-poses/PoseTests.cs).
- Histórico secundário da base: [`CHANGELOG-0.7.25.md`](./CHANGELOG-0.7.25.md) e [`CHANGELOG-0.7.24.md`](./CHANGELOG-0.7.24.md).
