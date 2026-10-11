# Combate por turnos, dano, ferramentas e HUD

**Base documentada:** 0.7.25  
**Escopo:** somente o módulo de combate por turnos em cena, resolução de dano, ferramentas/estoque, sinergias de brasão e interface do combate.  
**Fonte de autoridade:** o código atual em `src/`; o README e o relatório antigo são apenas contexto secundário.

> Esta documentação descreve o que está implementado no código atual. Não trata proposta de balanceamento como funcionalidade existente e não presume que uma compilação contra as assemblies equivalha a uma validação dentro do jogo Windows.

## 1. Como usar

### Iniciar um confronto

1. Entre em uma cena jogável (`GameState.PLAYING`).
2. Aproxime Hornet de um inimigo vivo até **8,0 unidades de mundo**.
3. Hornet precisa estar **no chão** (`hero.cState.onGround`).
4. Pressione a ação **TAUNT**. A busca do alvo é refeita no próprio frame do comando; portanto, o início não depende de o prompt ter sido atualizado no ciclo anterior.
5. O sistema escolhe o `HealthManager` inimigo elegível mais próximo. Alvos mortos, sem HP, inativos, pertencentes à própria Hornet ou fora da camada de inimigos são descartados.

O prompt nativo é remapeável porque usa `HeroActionButton.TAUNT`. O fallback desenhado pelo mod exibe `V` e clique no analógico direito, que são apenas a indicação padrão; durante o combate, as bindings do HUD são lidas do `InputHandler` ativo.

### Jogar o turno de Hornet

| Entrada real | Função no combate |
|---|---|
| Cima/esquerda | Seleciona a ação anterior |
| Baixo/direita | Seleciona a próxima ação |
| Salto | Executa a ação selecionada |
| Dash | Recuar e encerrar a batalha durante o turno de Hornet |
| Ataque | Abrir o seletor próprio de brasões |

No seletor de brasões e no seletor de golpes de invocação, cima/baixo/esquerda/direita navegam, **salto** confirma e **ataque** ou **dash** fecham. Abrir um seletor não consome o turno; o seletor pausa temporariamente `Time.timeScale`.

Os lembretes flutuantes nativos de ação, recuo e brasões são ocultados durante o duelo. As instruções ficam no HUD do mod e acompanham as bindings detectadas.

## 2. Máquina de estados e ciclo do duelo

`TurnBattleSystem` mantém uma única instância ativa e usa os estados privados abaixo:

| Estado | Entrada/saída e duração |
|---|---|
| `Idle` | Procura um alvo próximo e mostra o prompt. Ao confirmar TAUNT, chama `StartBattle`. |
| `Preparing` | Preparação de **0,45 s**. Posições, física, alvo, dano básico, brasão inicial e snapshot de invocação já foram capturados. |
| `PlayerTurn` | Aguarda input sem limite de tempo. A lista de ações é atualizada entre turnos e depois de troca de brasão. |
| `CrestMenu` | Seletor de brasões; pausa o tempo global até confirmar/fechar. |
| `SummonMenu` | Seletor de golpes aprendidos; pausa o tempo global até confirmar/fechar. |
| `PlayerAction` | Ação normal/skill/ferramenta por **0,72 s**, cura por **0,35 s**, ou invocação por **0,95 s**. |
| `EnemyAction` | O alvo atua por **1,15 s**. Se acertar, há uma janela visual de reação de **0,55 s**; se não houver perda real de vida, é aplicado o dano de reserva uma vez e a reação dura 0,55 s. |
| `Result` | Vitória por **3,2 s**; derrota ou recuo por **1,0 s**. Depois chama `EndBattle`. |

Os tempos usam `Time.unscaledTime`, mas a batalha normal **não pausa o cenário**. Somente Hornet e o alvo selecionado são restringidos pela lógica do duelo. O patch de `HeroController.Update` impede a atualização normal de Hornet quando o sistema não a liberou; o patch de `GameManager.PauseGameToggle` bloqueia a pausa nativa enquanto o duelo está ativo.

### 2.1 Preparação e posições

`StartBattle` salva posição, velocidade, gravidade, constraints de física, escala do inimigo e direção inicial de Hornet. Hornet permanece no ponto em que TAUNT foi confirmado. O inimigo é colocado de frente para ela. A distância entre centros é calculada pelas extensões das colliders e limitada a **1,6–3,0 unidades**.

Durante o confronto, `LockBody`, `LockHeroAtDuelPosition`, `LockEnemyAtDuelPosition` e `LateUpdate` zeram velocidade, removem gravidade temporariamente e congelam posição/rotação. O FSM do alvo somente pode avançar no turno inimigo, durante a reação ao ataque do jogador ou no resultado de vitória. Quando não está agindo, o sistema procura um clip cujo nome contenha `Idle`; se não encontrar, a posição continua bloqueada, mas a animação corrente pode terminar.

### 2.2 Fim do confronto

O resultado é iniciado quando:

- o inimigo fica morto ou com `hp <= 0`: vitória;
- a vida de Hornet chega a zero ou `hero.cState.dead` fica ativo: derrota;
- Hornet pressiona dash durante `PlayerTurn`: recuo voluntário.

Na vitória, o sistema mantém a sequência nativa de morte do inimigo e procura moedas e pickups **novos** em um raio de **12,0 unidades** a partir de `lootOrigin`. Itens cujo inventário já esteja cheio são ignorados. Na saída, `EndBattle` tenta restaurar posição, velocidade, gravidade, constraints, escala, animação, input e o brasão equipado no começo do combate.

A derrota não confirma uma captura pendente de invocação. O recuo também não confirma, não custa seda e não inicia o turno inimigo.

## 3. Lista de ações e resolução

A lista é construída por `BuildMoves`, nesta ordem:

1. **Golpe do brasão** — sempre;
2. **Invocar** — se `EnemySummonRegistry.HasAny` for verdadeiro;
3. **Curar (3 pontos)** — somente se `PlayerData.instance.health < CurrentMaxHealth`;
4. **Defender-se** — sempre;
5. ferramentas equipadas do tipo `Skill` e `Red`, ordenadas por tipo (vermelhas primeiro) e depois pelo nome exibido.

A lista é reconstruída ao trocar de brasão e no começo do turno de Hornet. Uma ferramenta vermelha é incluída somente se não estiver vazia e possuir binding de ataque.

### 3.1 Golpe do brasão

| Propriedade | Implementação atual |
|---|---|
| Dano de referência | `Mathf.Max(1, PlayerData.instance.nailDamage)` |
| Custo | **0 seda** |
| Ação nativa | `HeroController.Attack(AttackDirection.normal)` |
| Duração | **0,72 s** |
| Fallback | `enemy.Hit(CreatePlayerHit(currentMove))`, uma tentativa ao fim da janela se não houve redução de HP |
| Seda do hit | Ataque nativo e `HitSilkGeneration.Full` no `HitInstance` de fallback; o valor final é resolvido pelo jogo |

O prefixo de `HealthManager.Hit` redirecionado ao inimigo da batalha zera o multiplicador original e, quando a ação não é skill, substitui `DamageDealt` pelo dano de `currentMove`. Isso mantém o dano do golpe alinhado ao ferrão atual. Se o ataque nativo não registrar dano, `FinishPlayerAction` usa o fallback, exceto quando `IsTargetInvulnerableForFallback` detecta `IsInvincible` ou `CheckInvincible()`.

Quando o golpe realmente reduz HP, `CrestSynergy.GetNormalAttackSilkBonus()` pode acrescentar seda explicitamente. Essa geração é separada da geração nativa do hit.

### 3.2 Skills e ferramentas vermelhas

`BuildMoves` usa o dano de **2 × `nailDamage`** como dano de fallback para skills e ferramentas ofensivas. O valor nativo de uma skill, quando aceito pelo jogo, permanece prioritário.

`TryStartNativeSkill`:

1. verifica `ToolItemManager.GetAttackToolBinding(skill)`;
2. chama `hero.AcceptInput()`;
3. atribui a ferramenta ao campo nativo `HeroController.willThrowTool`;
4. chama `HeroController.ThrowTool(false)`;
5. devolve o controle de input sem resetar a ação do jogo.

Para uma **skill**, o custo listado é `CrestSynergy.GetActionCost(Skill, PlayerData.instance.SilkSkillCost)`. O código compara a seda antes/depois da ação e cobra eventual diferença que faltou; não há reembolso automático se a rotina nativa gastar mais do que o custo calculado.

Para uma **ferramenta vermelha**, o custo de seda da ação é **0** e o consumo de unidades é delegado a `ThrowTool`, preservando a lógica nativa de estoque, upgrades e limites. Se a ferramenta nativa não causar redução de HP, o fluxo ofensivo comum também pode chegar ao fallback genérico de 2 × `nailDamage`; isso é comportamento literal do código, não uma garantia de que toda ferramenta vermelha deveria ser um ataque de ferrão.

### 3.3 Cura

A ação só é adicionada quando falta vida normal. Ao confirmar:

```text
custo efetivo = CrestSynergy.GetActionCost(Heal, 9)
vida recuperada = hero.AddHealth(3)
```

O custo base é **9 seda** e a recuperação é de até **3 HP**, limitada pelo máximo do jogo. A cura usa 0,35 s, não libera a atualização normal de Hornet durante a animação e entrega o turno ao inimigo. Não aparece se `health >= CurrentMaxHealth`.

### 3.4 Defender-se

Defender-se custa **0 seda**, não causa dano e inicia imediatamente `StartEnemyAction`. Ele marca `defendPending` e descreve a regra no HUD:

```text
dano reduzido = max(1, ceil(dano recebido × 0,5))
recompensa = 2 seda + bônus defensivo do brasão
```

`ReduceEnemyDamageIfDefending` aplica a redução somente ao dano permitido do alvo da batalha. `RecordEnemyTurnHit` compara a vida total antes/depois do turno:

```text
vida total = PlayerData.health + PlayerData.healthBlue
```

Se a vida total realmente diminuir, `GrantDefendSilkReward` concede a recompensa uma única vez. Sem perda real de vida, não há seda. O dano de reserva também passa pelo mesmo patch de redução quando é chamado com a fonte do inimigo.

### 3.5 Invocação

Depois que existe pelo menos uma invocação aprendida, **Invocar** abre um menu com todos os `SummonAttack` registrados. A ordenação padrão é `DamageDescending`. O menu mostra nome do inimigo, nome do golpe e dano capturado.

Na confirmação, `BeginSummonAttack`:

- verifica vida do alvo e seda disponível;
- calcula `GetCurrentSummonCost()`;
- cobra o custo imediatamente;
- inicia `PlayerAction` por 0,95 s;
- cria uma aparição somente visual, quando possível;
- tenta tocar o clip de ataque associado;
- em `FinishSummonAction`, chama `enemy.Hit(CreateSummonHit(attack))`;
- se não houver redução de HP e o alvo não estiver invulnerável, tenta uma vez `enemy.ApplyExtraDamage(CreateSummonHit(attack))`;
- destrói a aparição antes do turno inimigo.

A fórmula de dano é:

```text
dano da invocação = max(1, SummonAttack.Damage)
```

`SummonAttack.Damage` vem de `DamageHero.damageDealt` do inimigo derrotado; não existe multiplicador global de invocação no código atual. A invocação usa `HitSilkGeneration.default`, sem `AddSilk` explícito.

A captura é preparada no começo do duelo, enquanto o inimigo ainda existe, e confirmada na vitória. O postfix de `HealthManager.Hit` chama `ObserveEnemyDeath` assim que o HP chega a zero, evitando perder a captura durante a destruição nativa. `EnemySummonRegistry.Commit` enfileira o save. O registro é serializado no JSON nativo e possui sidecar por slot como fallback; consulte [`EnemySummonRegistry.cs`](../src/EnemySummonRegistry.cs#L102-L143) e [`SilkThreadSavePatches.cs`](../src/SilkThreadSavePatches.cs#L20-L68).

A aparição é um clone de visual: IA, `HealthManager`, `DamageHero`, colliders, física e demais comportamentos são removidos/desativados. O alvo real recebe o dano. Se o clone ou o clip não estiver disponível, a ação lógica e o dano continuam.

A captura ignora espinhos, coal spikes e componentes reconhecidos como contato, corpo, aura, dano constante, hazards e hitboxes passivas. Se nenhum golpe seguro for encontrado, é criado `Golpe básico` com dano 1.

## 4. Dano do inimigo, bloqueios e fallback

No início da batalha, `FindEnemyBasicDamage` captura o primeiro `DamageHero` com `hazardType == ENEMY`; se não houver, usa o primeiro `DamageHero`; se não houver nenhum, usa **1**.

Durante `EnemyAction`:

1. a IA e os animadores do alvo são liberados;
2. `ShouldAllowDamageFrom` permite somente uma fonte pertencente ao alvo selecionado (o próprio objeto, um filho ou a mesma raiz);
3. o primeiro hit que realmente reduzir a vida total marca `enemyTurnHitResolved` e inicia a reação de 0,55 s;
4. hits seguintes no mesmo turno são bloqueados;
5. se os 1,15 s acabarem sem redução de vida e Hornet ainda não estiver morta, `ApplyEnemyFallbackHit` chama `hero.TakeDamage` uma vez com o dano básico capturado.

Dano ambiental e dano de outros inimigos normalmente são bloqueados enquanto o duelo está ativo. O patch também impede que o FSM do alvo avance fora das janelas autorizadas.

### Invulnerabilidade

O fallback do jogador e o fallback da invocação verificam:

```text
target.IsInvincible || target.CheckInvincible()
```

Se a consulta falhar, o código assume o estado seguro e não aplica o fallback. Portanto, esses dois caminhos não foram desenhados para atravessar invulnerabilidade nativa. O dano nativo de skills e o dano nativo do inimigo continuam sujeitos às próprias rotinas do jogo.

## 5. Estoque e passivas de ferramentas

### 5.1 Ferramentas ativas

`BuildMoves` considera ferramentas equipadas dos tipos `ToolItemType.Skill` e `ToolItemType.Red`:

- skills usam seda e procuram binding de ataque;
- vermelhas usam `ThrowTool` e custo de seda zero;
- uma vermelha vazia não entra na lista;
- `GetToolStock` lê `tool.SavedData.AmountLeft` para ferramentas vermelhas contadas e limita o valor mínimo a zero;
- ferramentas vermelhas não contadas são exibidas como `∞`;
- a checagem de uso antes de executar também testa `Skill.IsEmpty`.

O texto da ação é criado com o nome localizado e, para vermelhas, com o estoque entre colchetes, por exemplo `Ferramenta — Nome [2]`. O consumo efetivo é nativo; o mod não decrementa `AmountLeft` manualmente.

### 5.2 Passiva uma vez por turno

`ShouldSuppressLimitedToolDamage` limita a uma ativação de dano por ferramenta em cada turno de Hornet. A lista `limitedToolDamageThisTurn` é limpa:

- ao sair de `Preparing` para o primeiro turno de Hornet;
- depois que o turno inimigo termina e começa outro turno de Hornet.

São consideradas limitadas:

- todas as ferramentas azuis e amarelas;
- ferramentas com `Usage.MaxActive > 0` ou `Usage.MaxActiveAlt > 0`.

O primeiro hit é aceito; ativações repetidas da mesma chave no mesmo turno recebem `MagnitudeMultiplier = 0`. O objeto pode continuar visível na cena, mas não causa dano adicional naquele turno. O identificador preferido é `tool.name`, com fallback para `GetInstanceID()` quando o nome está vazio.

## 6. Sinergias de brasão e ferramentas

As correspondências de [`CrestSynergy.cs`](../src/CrestSynergy.cs#L17-L61) aceitam substring no nome interno ou no nome localizado. Custos positivos nunca ficam abaixo de 1 seda.

| Brasão/ferramenta | Efeito atual |
|---|---|
| `Toolmaster` ou `Architect` | −1 no custo de cada skill |
| `Wanderer` | −1 no custo da cura |
| `Bard` ou `Witch` | −1 no custo da invocação |
| `SpoolExtender`, `SilkSpool` ou `Spool` equipado | −1 adicional no custo da invocação |
| `Hunter` | +1 seda quando um Golpe do brasão causa dano |
| `WhiteRing`, `SilkRegen` ou `SilkRing` equipado | +1 seda quando um Golpe do brasão causa dano |
| `Reaper` | +1 seda na recompensa de Defender-se |

Exemplos exatos:

```text
custo de skill = max(1, PlayerData.SilkSkillCost − 1 se Toolmaster/Architect)
custo de cura = max(1, 9 − 1 se Wanderer)
custo de invocação = max(1, 3 − bônus do brasão − bônus da ferramenta)
recompensa de defesa = 2 + 1 se Reaper
bônus de golpe = 1 se Hunter + 1 se WhiteRing/SilkRegen/SilkRing
```

O bônus de golpe só é concedido quando `damageDone > 0`. Equipar uma sinergia não altera permanentemente a vida, o dano base do ferrão ou a capacidade máxima de seda.

## 7. Troca de brasão

O ataque abre `OpenCrestSelectionMenu`, que lista brasões desbloqueados e visíveis, além do brasão atual. A troca:

- não consome o turno;
- pausa o jogo enquanto o menu está aberto;
- usa `ToolItemManager.SetEquippedCrest` e `SendEquippedChangedEvent(force: true)`;
- reconstrói `moves` imediatamente;
- é limitada a **uma troca por turno** e **três trocas por batalha**;
- não gasta uma troca ao selecionar o mesmo brasão;
- não pode ser confirmada se o limite estiver atingido, embora o menu possa ser consultado;
- restaura, em `EndBattle`, o brasão salvo em `battleHomeCrestId`.

`crestSwappedThisTurn` é zerado ao terminar o turno inimigo. `crestSwapsUsedThisBattle` é zerado na saída do combate.

## 8. HUD e apresentação

`OnGUI` desenha o HUD conforme o estado:

- **indicador de turno:** `TURNO DE HORNET` ou `TURNO DO INIMIGO`, com linha vermelha central;
- **painel de status:** HP normal de Hornet, HP máximo normal, seda, nome e sprite do brasão equipado;
- **barra do inimigo:** acima do alvo no mundo, usando collider/renderer para calcular o topo e `enemyMaxHp` capturado no início;
- **painel inferior:** ação selecionada, mensagem de combate e controles atuais;
- **seletor de brasões:** lista rolável, brasão equipado, contagem de trocas e instruções;
- **seletor de invocação:** golpe, origem e dano, ordenado por dano decrescente;
- **transições:** wipe diagonal somente na entrada e saída do combate, com animação não escalada.

O painel de status usa `PlayerData.health` e não exibe `healthBlue` separadamente, embora a vida azul participe da comparação que decide a recompensa de Defender-se. A barra do inimigo usa `max(0, enemy.hp) / max(1, enemyMaxHp)` com clamp entre 0 e 1.

As labels de teclado/comando são atualizadas a cada 0,75 s. Em teclado, o sistema lê `GetKeyBindingForAction`; em controle, lê `GetButtonBindingForAction` e converte nomes para convenções Xbox, PlayStation e Switch. Se a leitura falhar, usa fallbacks como `↑`, `↓`, `PULO`, `DASH` e `ATAQUE`.

## 9. Constantes e métodos de referência

### 9.1 `TurnBattleSystem.cs`

| Referência | Papel |
|---|---|
| [`EncounterDistance`, `SearchInterval`, durações e custos](../src/TurnBattleSystem.cs#L53-L69) | Distância 8,0; busca 0,25 s; preparação 0,45 s; ação 0,72 s; inimigo 1,15 s; reação 0,55 s; vitória 3,2 s; outros resultados 1,0 s; drops 12,0; cura 9/3; invocação 3; troca 3. |
| [`Update`, `UpdateEncounterPrompt`, `FindClosestEnemy`](../src/TurnBattleSystem.cs#L206-L371) | Loop de estados, prompt, busca e condição de início. |
| [`StartBattle`, `CalculateDuelPositions`](../src/TurnBattleSystem.cs#L417-L496) | Captura inicial, preparação, posições e travas de física. |
| [`BuildMoves`, `GetToolStock`](../src/TurnBattleSystem.cs#L498-L612) | Lista de ações, custos, filtros de ferramentas e estoque exibido. |
| [`UpdatePlayerTurn`, menus de brasão e invocação](../src/TurnBattleSystem.cs#L647-L922) | Inputs, seleção e limites de troca/seleção. |
| [`BeginPlayerMove`, `TryStartNativeSkill`](../src/TurnBattleSystem.cs#L1026-L1129) | Validação, cobrança, ataque nativo, skill/ferramenta e cura. |
| [`FinishPlayerAction`, `FinishSummonAction`](../src/TurnBattleSystem.cs#L1131-L1269) | Fallback, mensagens, invocação e transição ao turno inimigo. |
| [`CreatePlayerHit`, `CreateSummonHit`](../src/TurnBattleSystem.cs#L1271-L1300) | Estrutura de `HitInstance`, dano e geração de seda. |
| [`ShouldSuppressLimitedToolDamage`, `IsLimitedDamageTool`](../src/TurnBattleSystem.cs#L1303-L1349) | Passivas e ferramentas de dano limitado a uma vez por turno. |
| [`OverrideActiveBattleDamage` até `ReduceEnemyDamageIfDefending`](../src/TurnBattleSystem.cs#L1351-L1444) | Escala de dano, gate de fontes, reação, recompensa e defesa. |
| [`StartEnemyAction`, `FinishEnemyAction`, `ApplyEnemyFallbackHit`](../src/TurnBattleSystem.cs#L1464-L1533) | IA, dano nativo, dano de reserva e fim do turno inimigo. |
| [`BeginResult`, `CollectNewDrops`, `EndBattle`](../src/TurnBattleSystem.cs#L1542-L2002) | Vitória/derrota/recuo, drops, restauração e limpeza. |
| [`OnGUI` e rotinas `Draw...`](../src/TurnBattleSystem.cs#L2042-L2541) | HUD, menus, status, barra de HP, bindings e wipe. |
| [`TurnBattleDamageScalePatch`, `TurnBattleDamageGatePatch`, `TurnBattleEnemyFsmPatch`](../src/TurnBattleSystem.cs#L2624-L2679) | Hooks Harmony de dano, bloqueio de fontes e atualização do FSM inimigo. |

### 9.2 `CrestSynergy.cs`

- [`GetActionCost`](../src/CrestSynergy.cs#L17-L45): aplica reduções por ação e piso de 1.
- [`GetNormalAttackSilkBonus`](../src/CrestSynergy.cs#L47-L56): calcula bônus de seda do golpe.
- [`GetDefendRewardBonus`](../src/CrestSynergy.cs#L58-L61): calcula bônus de Defender-se.
- [`DescribeCurrentSynergy`](../src/CrestSynergy.cs#L63-L73): resumo textual de custos e bônus.
- [`CurrentCrest`, `MatchesCrest`, `IsToolEquipped`, `MatchesAny`](../src/CrestSynergy.cs#L75-L126): resolução tolerante a nomes internos/localizados.

### 9.3 Invocações e save

- [`Capture`, `Commit`, `Register`](../src/EnemySummonRegistry.cs#L102-L164): captura pendente, confirmação e deduplicação.
- [`GetAllAttacks`](../src/EnemySummonRegistry.cs#L166-L193): catálogo e ordenação.
- [`ExportSaveData`, `RestoreFromSaveData`](../src/EnemySummonRegistry.cs#L196-L268): serialização e restauração do registro.
- [`CollectAttacks` e filtros passivos](../src/EnemySummonRegistry.cs#L393-L453): seleção de `DamageHero` seguros.
- [`QueueGameSave`](../src/EnemySummonRegistry.cs#L585-L597): sidecar e fila de save nativo.
- [`InjectIntoSaveJson`, `RestoreFromSaveJson`](../src/SilkThreadSavePatches.cs#L20-L68): bloco `SilkThreadCrest` no JSON nativo e fallback.
- [`SilkThreadSerializeSavePatch`, patches de carregamento`](../src/SilkThreadSavePatches.cs#L153-L228): hook fechado de `GetJson<SaveGameData>` e duas sobrecargas de `SetLoadedGameData`.

## 10. Status de implementação e evidência

### Implementado e verificável no código atual

- Máquina de estados, início no chão e busca por proximidade.
- Preparação, posicionamento, travamento e restauração do duelo.
- Golpe nativo, skill/ferramenta nativa, cura, defesa e invocação.
- Dano dinâmico do ferrão, fallback de 2 × ferrão para ação ofensiva sem dano, proteção de fallback contra invulnerabilidade do alvo e dano de reserva do inimigo.
- Custos de seda, sinergias, estoque lido do item e troca limitada de brasões.
- Limite de dano de ferramentas passivas/ativas persistentes a uma ativação por turno.
- Gate de dano externo, gate de FSM do inimigo, reação, vitória/derrota/recuo e recolhimento de drops novos.
- Captura e persistência de invocações em JSON nativo com sidecar, incluindo confirmação no postfix de `HealthManager.Hit`.
- HUD de turno, status, brasão, HP do alvo, ação, mensagens, bindings e menus próprios.

### Evidência disponível, sem extrapolar para teste in-game

A base 0.7.25 foi compilada contra as assemblies recebidas. O contexto de release também registra 30 testes do seletor de poses e a correção de sincronização/tamanho da skin Silk Eclipse; isso não confirma a execução visual dentro do jogo e não altera as fórmulas ou a ordem deste módulo.

O contexto fornecido registra ainda que o combate foi confirmado funcional, que o save de invocações foi confirmado na 0.7.15 e que menu/paletas foram confirmados na 0.7.24. Esses marcos são evidência histórica declarada; para esta documentação, o comportamento detalhado continua ancorado no código atual da 0.7.25.

O sandbox não executa o jogo Windows. Portanto, a existência dos caminhos de código acima é evidência de implementação estática, não evidência de que cada inimigo, ferramenta, binding ou resolução de tela se comporte corretamente em runtime.

### Áreas ainda não testadas dentro do jogo

- leitura efetiva de teclado remapeado e de cada família de controle;
- renderização do HUD em resoluções e proporções diferentes;
- abertura/fechamento dos dois seletores durante diferentes fases do combate;
- consumo real de estoque por cada ferramenta vermelha e atualização da UI depois do lançamento;
- eventos especiais, skills prolongadas e ferramentas que não usam um hit simples;
- inimigos voadores, bosses, múltiplos `DamageHero`, multi-hit, transições de fase e invulnerabilidade;
- dano de reserva diante de i-frames ou morte durante `TakeDamage`;
- drops gerados tardiamente, pickups na capacidade máxima e persistência após salvar/carregar em Windows;
- reidratação de visuais de invocações salvas e correspondência de clips;
- retorno de input, física e brasão após derrota, recuo e destruição inesperada do alvo.

## 11. Bugs potenciais e riscos — não confirmados

Os itens abaixo são hipóteses de revisão estática ou riscos de design. **Não são afirmações de que foram reproduzidos.**

1. **Fallback inimigo pode marcar um hit sem confirmar a perda de vida.** `FinishEnemyAction` chama `ApplyEnemyFallbackHit`, marca `enemyTurnHitResolved = true` e agenda reação mesmo sem comparar novamente a vida depois de `hero.TakeDamage`. Se a rotina nativa rejeitar o dano por invulnerabilidade ou estado de morte, o turno pode ser encerrado e a mensagem pode dizer que acertou sem dano real.
2. **Não há trava universal de um hit ofensivo por ação do jogador.** O limite explícito existe para ferramentas passivas/ativas persistentes, mas não para todos os callbacks de uma animação nativa. Uma skill ou ataque com vários eventos pode, em tese, causar vários callbacks dentro dos 0,72 s.
3. **Ferramenta vermelha sem dano pode cair no fallback genérico.** Como `isOffensiveMove` inclui `MoveKind.Tool`, uma ferramenta ativa que não reduz HP pode receber dano de 2 × `nailDamage`, mesmo que sua intenção seja apenas um efeito especial ou controle.
4. **Rótulo de estoque pode ficar defasado na mesma ação.** `moves` é reconstruído no início do turno, após troca de brasão ou quando uma tentativa encontra ferramenta vazia; depois de um lançamento bem-sucedido, o texto `[n]` pode não refletir imediatamente o `AmountLeft` nativo até a próxima reconstrução.
5. **Chave por `tool.name` pode agrupar instâncias homônimas.** Duas ferramentas distintas com o mesmo nome interno podem compartilhar o bloqueio de uma vez por turno.
6. **Aliases de sinergia usam substring.** `MatchesAny` pode reconhecer um nome interno/localizado que apenas contenha o alias, ativando uma sinergia não pretendida em um item ou brasão com nomenclatura coincidente.
7. **Dano de reserva pode não representar o ataque que estava sendo preparado.** O valor é capturado do primeiro `DamageHero` elegível, não do golpe/estado específico que a IA teria executado. Inimigos com vários componentes podem receber uma aproximação inadequada.
8. **Filtro de fonte por raiz pode ser amplo.** Um objeto pertencente à mesma raiz do alvo é tratado como fonte válida. Prefabs com múltiplos sub-inimigos ou hazards irmãos podem permitir uma origem que não seja exatamente o atacante esperado.
9. **Persistência depende da instalação dos patches opcionais.** O plugin isola os patches de save para não impedir a batalha; se um alvo de Harmony não estiver disponível, a captura pode permanecer apenas na sessão. O sidecar reduz o risco, mas ainda depende de caminho de slot e permissões do ambiente do jogo.
10. **Invocação salva pode ficar sem aparição visual.** A lógica salva nome/dano/clip, mas o template visual só é reidratado quando existe um `HealthManager` correspondente na cena. Nesse caso, a invocação deve continuar causando dano, porém pode aparecer sem clone.

## 12. Propostas futuras, separadas do que já existe

- Expor custos, multiplicadores, tetos de dano e limite de trocas em configuração validada, sem alterar os valores atuais por padrão.
- Registrar uma resolução de hit por ação ofensiva, com política explícita para multi-hit, em vez de depender apenas da janela de animação.
- Após `ApplyEnemyFallbackHit`, comparar vida antes/depois e só marcar reação/recompensa quando houver dano efetivo.
- Diferenciar ferramentas que causam dano de ferramentas de efeito, evitando aplicar automaticamente o fallback de 2 × ferrão a uma ferramenta não ofensiva.
- Reconstruir `moves` imediatamente após o consumo de uma ferramenta vermelha e mostrar o estoque atualizado.
- Usar identificador estável de item, não apenas `tool.name`, para o bloqueio de passivas por turno.
- Capturar o ataque efetivamente escolhido pela IA para o fallback, ou definir uma regra de balanceamento por inimigo em vez do primeiro `DamageHero`.
- Criar uma matriz de teste in-game para skills, ferramentas, invulnerabilidade, multi-hit, bosses, drops, save/load e controles.
- Testar o HUD em várias resoluções e substituir o texto de fallback de TAUNT por uma leitura dinâmica quando o prompt não estiver disponível.
- Adicionar validação de integridade para dados de invocação carregados, incluindo dano máximo, duplicatas e ausência de template visual.

## Referências principais

- [`TurnBattleSystem.cs`](../src/TurnBattleSystem.cs)
- [`CrestSynergy.cs`](../src/CrestSynergy.cs)
- [`EnemySummonRegistry.cs`](../src/EnemySummonRegistry.cs)
- [`SilkThreadSavePatches.cs`](../src/SilkThreadSavePatches.cs)
- [`SilkThreadCrestPlugin.cs`](../src/SilkThreadCrestPlugin.cs#L128-L135)
- [Visão geral do repositório](../README.md)
