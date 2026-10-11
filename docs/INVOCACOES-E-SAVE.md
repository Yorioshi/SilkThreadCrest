# Invocações, catálogo e persistência por slot

**Base documentada: 0.7.25** · módulo `EnemySummonRegistry`, integração de combate em `TurnBattleSystem` e persistência em `SilkThreadSavePatches`.

Este documento descreve o comportamento implementado no código atual. O código é a autoridade; README e relatórios anteriores não substituem esta leitura. O escopo aqui é somente: captura de invocações, catálogo de golpes, blacklist de danos passivos, snapshot e commit após vitória, clone visual isolado, dano e fallback, seda, ordenação, save nativo/sidecar, schema, caminhos e carregamento por slots.

> **Limite importante:** o catálogo é heurístico. Ele não promete capturar perfeitamente todos os inimigos, variantes, bosses, ataques de fase, multi-hits ou convenções de nomes. A captura pode selecionar um golpe seguro, criar o fallback `Golpe básico`, ou não conseguir reconstruir a aparência visual sem que isso impeça a ação lógica de causar dano.

## 1. Como usar

### 1.1 Aprender uma invocação

1. Esteja com Hornet no chão e inicie um encontro pela ação `TAUNT` quando houver um inimigo elegível próximo.
2. Derrote o alvo dentro da batalha por turnos.
3. O mod prepara um snapshot no início do duelo, mas só torna a invocação permanente quando o alvo realmente morre e a vitória é resolvida.
4. Na vitória, o inimigo é adicionado uma única vez pelo seu `Key`. Derrota, recuo ou encerramento sem vitória descartam o snapshot pendente.
5. O commit grava o sidecar do slot e solicita o save nativo do jogo. Não é necessário esperar o painel de resultado terminar para o desbloqueio ser confirmado.

Se o mesmo nome de `GameObject` já tiver sido aprendido, a nova captura não cria uma segunda entrada. Variantes que compartilham o mesmo nome também compartilham a entrada.

### 1.2 Usar um golpe aprendido

1. Inicie outra batalha depois que existir pelo menos uma entrada aprendida.
2. Na lista de ações de Hornet, selecione **Invocar**. A ação só aparece quando `EnemySummonRegistry.HasAny` é verdadeiro.
3. Confirme com a binding de salto para abrir o seletor de golpes. Cima/baixo ou esquerda/direita navegam; ataque ou dash fecham o seletor.
4. Escolha o golpe. O menu mostra o inimigo de origem, o nome do ataque e `DANO X`.
5. Confirme. A seda é consumida imediatamente, a ação dura **0,95 s**, o clone visual é mostrado quando possível e, ao final, o dano é aplicado ao inimigo real da batalha.
6. O clone é destruído antes de o alvo sobrevivente iniciar o turno inimigo. Se o visual não puder ser criado, a ação, o custo e o dano lógico continuam válidos.

O seletor de invocação pausa temporariamente `Time.timeScale`, mas a confirmação retoma o tempo normal da batalha. Abrir e fechar o menu não consome o turno; confirmar um golpe consome o turno de Hornet.

Referências: [`OpenSummonSelectionMenu`](../src/TurnBattleSystem.cs#L811), [`BeginSummonAttack`](../src/TurnBattleSystem.cs#L853), [`FinishSummonAction`](../src/TurnBattleSystem.cs#L1221) e [`DrawSummonSelectionMenu`](../src/TurnBattleSystem.cs#L2163).

## 2. Modelo interno: snapshot, commit e descarte

O registro em memória é estático durante o processo e contém `LearnedEnemySummon` com:

- `Key`: identificador derivado do nome do `GameObject`;
- `DisplayName`: nome limpo para exibição;
- `AcquisitionOrder`: ordem em que o tipo foi aprendido;
- `Attacks`: lista de `SummonAttack` aprendidos;
- `VisualTemplate`: clone visual arquivado, não serializado no save.

O fluxo é deliberadamente dividido em duas fases:

### 2.1 Snapshot no começo do duelo

`TurnBattleSystem.StartBattle` procura uma entrada já aprendida. Se ela não existir, chama `EnemySummonRegistry.Capture(selectedEnemy)` enquanto o inimigo ainda está presente e vivo. A captura:

- calcula a chave do inimigo;
- coleta os `DamageHero` aceitos;
- cria o ataque básico de reserva se nenhum ataque seguro for encontrado;
- tenta arquivar um clone visual separado;
- devolve uma entrada **pendente**, ainda fora da lista permanente `learned`.

Se a chave já existir, não há novo snapshot nem duplicata.

### 2.2 Commit ligado à morte real

O patch de `HealthManager.Hit` chama `TurnBattleSystem.ObserveEnemyDeath` no postfix. Quando o alvo da batalha chega a `hp <= 0` ou fica `isDead`, o snapshot pendente é imediatamente passado a `EnemySummonRegistry.Commit`.

Há também um segundo caminho de segurança em `BeginResult(..., victory: true)`: se o postfix de morte não tiver confirmado o snapshot, a vitória tenta fazer o commit antes de mostrar o resultado. Isso cobre a sequência em que a morte nativa destrói ou altera o inimigo entre o último hit e o estado visual de vitória.

`Commit` rejeita uma entrada nula, evita duplicata pela chave, ajusta `nextAcquisitionOrder`, adiciona a entrada à lista e chama `QueueGameSave()`. Ao detectar uma duplicata, o clone pendente é descartado e a entrada existente é reutilizada.

### 2.3 Descarte

Se a batalha termina por derrota, recuo ou outro caminho sem vitória, `EndBattle` chama `EnemySummonRegistry.Discard` no snapshot pendente. O descarte remove o clone visual arquivado, mas não altera entradas já aprendidas.

Referências: [`Capture`](../src/EnemySummonRegistry.cs#L102), [`Commit`](../src/EnemySummonRegistry.cs#L125), [`Discard`](../src/EnemySummonRegistry.cs#L146), [`BeginResult`](../src/TurnBattleSystem.cs#L1542), [`ObserveEnemyDeath`](../src/TurnBattleSystem.cs#L2017) e o patch [`HealthManager.Hit`](../src/TurnBattleSystem.cs#L2624).

## 3. Catálogo de inimigos e golpes

### 3.1 Chave e normalização

`MakeKey` aplica `CleanName` e converte o resultado para minúsculas invariantes. `CleanName`:

- remove a ocorrência literal `(Clone)`;
- remove espaços nas extremidades;
- limita o texto a **42 caracteres**.

Não existe um identificador de prefab, GUID ou variante. Portanto, dois inimigos com o mesmo nome limpo são o mesmo tipo de invocação. O nome pode continuar contendo espaços e pontuação; a chave não é um slug completo.

### 3.2 O que entra no catálogo

`CollectAttacks` inspeciona todos os `DamageHero` nos filhos do inimigo, inclusive componentes inativos. Um componente só entra se:

```text
damageDealt > 0
E não for espinho/hazard explicitamente excluído
E não for identificado como dano passivo
E (hazardType == ENEMY OU o nome parecer um ataque)
```

O teste de nome usa os termos abaixo, procurando substrings em minúsculas:

```text
attack, slash, strike, charge, dash, shoot, shot, spit, slam,
jump, stab, bite, beam, blast, throw, fire, acid, roar, hit,
damage, cast
```

O rótulo do ataque vem do nome do `GameObject` do `DamageHero`; se esse nome for genérico (`DamageHero`), tenta o nome do pai. Se ainda for vazio, `DamageHero` ou `Collider`, o nome vira `Golpe N`. O texto é limpo e limitado aos mesmos 42 caracteres.

Ataques com o mesmo `Name` e o mesmo dano dentro da captura são ignorados como duplicatas. O valor persistido é sempre no mínimo 1 (`Mathf.Max(1, damageDealt)`).

### 3.3 Blacklist de dano passivo

Antes do teste de ataque, `IsPassiveDamageComponent` percorre o próprio `DamageHero` e toda a cadeia de pais. Se qualquer nome contiver um dos tokens abaixo, o componente é excluído:

```text
bodydamage, body_damage, body-damage, contact, contactdamage,
contact_damage, contact-damage, touchdamage, touch_damage, touch-damage,
damageaura, damage_aura, damage-aura, aura, proximitydamage,
proximity_damage, proximity-damage, passive, constantdamage,
constant_damage, constant-damage, environmental, damagecollider,
damage_collider, damage-collider, collisiondamage, collision_damage,
collision-damage, passivehitbox, passive_hitbox, passive-hitbox,
hurtbox, spikes, spike, thorn, thorns
```

O mesmo filtro também verifica tags não vazias. As tags consideradas passivas são:

```text
hazard, spike, spikes, thorn, thorns, contactdamage,
contact_damage, passive, environmental, damage
```

Além da blacklist, `SPIKES` e `COAL_SPIKES` são rejeitados diretamente pelo `hazardType`. A blacklist reduz falsos ataques de contato, corpo, aura, espinho e perigos ambientais, mas é uma convenção de nomes/tags, não uma análise semântica do comportamento. Uma variante que use nomes diferentes pode ser classificada de forma incorreta e exigir uma nova regra.

### 3.4 Ataque básico de reserva

Se nenhum `DamageHero` passar pelos filtros, o catálogo recebe:

```text
Name: Golpe básico
Damage: 1
AttackOrder: 1
AnimationClip: melhor clip encontrado para "attack", ou nulo
```

Isso mantém uma ação selecionável para inimigos que não expõem hitboxes reconhecíveis, mas não significa que o ataque real do inimigo tenha sido identificado.

### 3.5 Clip de animação

Para cada ataque, `FindAttackClip` percorre os clips das instâncias `tk2dSpriteAnimator` do inimigo. O score aumenta quando o nome do clip contém palavras do rótulo do ataque e quando contém termos gerais de ataque; clips cujo nome contém `idle` não são escolhidos nessa busca.

O nome do clip é persistido em `AnimationClip` quando encontrado. Ao reproduzir, `PlayAttack` tenta, nesta ordem prática:

1. `tk2dSpriteAnimator.GetClipByName` e `Play`;
2. `Animator.Play` para o mesmo nome;
3. o `DefaultClip` do animador TK2D quando nenhum clip específico foi executado.

O clip escolhido é uma aproximação visual. Ele não é uma prova de que o frame reproduz exatamente o hitbox usado para registrar o dano.

Referências: [`CollectAttacks`](../src/EnemySummonRegistry.cs#L393), [`IsSafeAttackSource`](../src/EnemySummonRegistry.cs#L429), [`IsPassiveDamageComponent`](../src/EnemySummonRegistry.cs#L441), [`FindAttackClip`](../src/EnemySummonRegistry.cs#L473) e [`PlayAttack`](../src/EnemySummonRegistry.cs#L330).

## 4. Ordenação e apresentação

O enum `SummonSortMode` possui três modos:

- `DamageDescending`: dano decrescente; desempate por `Owner.DisplayName` sem diferenciar maiúsculas/minúsculas; depois `AttackOrder`;
- `AcquisitionOrder`: ordem de aquisição do inimigo; depois `AttackOrder`; depois nome do golpe sem diferenciar maiúsculas/minúsculas;
- `Name`: nome do inimigo; depois nome do golpe; depois dano decrescente.

A batalha usa `DefaultSummonSortMode = DamageDescending`. `GetAllAttacks` achata todos os golpes de todos os tipos aprendidos em uma única lista. O menu mostra cada linha no formato visual equivalente a:

```text
<DisplayName do inimigo> — <Name do ataque>  ·  DANO <Damage>
```

O código não aplica multiplicador, teto ou curva de dano durante a ordenação. O dano exibido é o dano inteiro registrado no catálogo, com mínimo 1.

Referências: [`SummonSortMode`](../src/EnemySummonRegistry.cs#L8), [`GetAllAttacks`](../src/EnemySummonRegistry.cs#L166) e [`DisplayLabel`](../src/EnemySummonRegistry.cs#L15).

## 5. Clone visual isolado

### 5.1 Arquivamento e instanciação

No snapshot, `CreateVisualTemplate` instancia o `GameObject` original, dá a ele o nome `SilkThread_SummonArchive_<Key>`, desanexa da hierarquia, desativa, configura como visual e marca `DontDestroyOnLoad`.

Na ação, `CreateVisual` instancia esse template, usa o nome `SilkThread_SummonVisual_<Key>`, desanexa, desativa durante a configuração, reativa e força todos os objetos para a camada `Ignore Raycast` (ou camada 2 como fallback). A batalha posiciona o visual perto de Hornet, inverte o eixo X para encarar o inimigo e chama `PlayAttack`.

### 5.2 O que é removido

`ConfigureVisualOnly` mantém habilitados apenas comportamentos cujos nomes de tipo sejam os renderizadores/animadores visuais esperados (`tk2dSpriteAnimator`, `tk2dSprite`, `tk2dBaseSprite` e `Animator`). Os demais `Behaviour` são destruídos. Além disso:

- `Collider2D` é desabilitado;
- `Rigidbody2D` deixa de simular;
- `Rigidbody` fica cinemático;
- renderizadores são habilitados.

Assim, a aparição não tem IA, vida, FSM, dano, colisão ou física de gameplay. Ela não vira um segundo inimigo nem entra na seleção de alvos. O dano é sempre aplicado ao `HealthManager enemy` real da batalha.

Se o template ou a instanciação falhar, `CreateVisual` devolve nulo e o fluxo de invocação continua. A falta de aparência não cancela o custo, o turno ou a resolução de dano.

Referências: [`CreateVisualTemplate`](../src/EnemySummonRegistry.cs#L502), [`ConfigureVisualOnly`](../src/EnemySummonRegistry.cs#L522), [`CreateVisual`](../src/EnemySummonRegistry.cs#L307) e [`DestroyVisual`](../src/EnemySummonRegistry.cs#L387).

## 6. Dano, fallback e seda

### 6.1 Fórmula e hit da invocação

O custo-base é definido por:

```text
SummonSilkCost = 3
custo final = max(1, 3 - descontos de sinergia)
```

`GetCurrentSummonCost` delega a fórmula a `CrestSynergy.GetActionCost(CrestSynergyAction.Summon, 3)`:

- brasão cujo nome interno ou localizado contenha `Bard` ou `Witch`: **-1**;
- ferramenta equipada cujo nome interno ou localizado contenha `SpoolExtender`, `SilkSpool` ou `Spool`: **-1**;
- piso absoluto: **1 seda**.

Os dois descontos podem se acumular. O custo é verificado ao abrir o menu e novamente ao confirmar. Na confirmação, `hero.TakeSilk(summonCost)` é chamado antes da ação começar.

O dano do golpe é:

```text
Dano do hit = max(1, attack.Damage)
```

`CreateSummonHit` cria um `HitInstance` com `Source = hero.gameObject`, `AttackType = AttackTypes.Nail`, `IsFirstHit = true`, sem ferramenta representada e com `SilkGeneration = default`. Não há `AddSilk` explícito para a invocação; a ação não gera seda por conta própria.

O primeiro caminho é `enemy.Hit(CreateSummonHit(attack))`. O valor efetivamente retirado da vida é medido comparando o HP antes e depois, portanto o resultado observado pode depender das regras nativas do alvo.

### 6.2 Fallback da invocação

Se o `Hit` não reduzir o HP, mas o alvo continuar vivo, o mod consulta `IsTargetInvulnerableForFallback`, que verifica `target.IsInvincible || target.CheckInvincible()`.

- Se a consulta indicar invulnerabilidade, o fallback é bloqueado.
- Se a consulta falhar, o método também bloqueia o fallback para não transformar uma falha de consulta em dano fantasma.
- Caso o alvo não esteja invulnerável, `enemy.ApplyExtraDamage(CreateSummonHit(attack))` é chamado **uma única vez**.

O visual é destruído depois dessa tentativa, independentemente de o dano ter sido aplicado. O fallback evita que um `Hit` rejeitado torne a ação sempre inútil, mas não é uma garantia de compatibilidade com toda transição de fase, proteção especial ou regra proprietária de cada inimigo.

### 6.3 Distinguir os fallbacks

O fallback da invocação usa o dano capturado e `ApplyExtraDamage`. Já o fallback de um ataque/skill comum é tratado em `FinishPlayerAction`: se não houver perda de HP, o mod tenta `enemy.Hit(CreatePlayerHit(currentMove))`, também bloqueando a tentativa quando o alvo está invulnerável. Para skills, o `BattleMove` é construído com `2 × PlayerData.instance.nailDamage` como dano de reserva; isso não altera o dano bruto catalogado das invocações.

### 6.4 Outros valores de seda relacionados

Para não confundir a economia do módulo:

| Ação | Regra no código atual |
|---|---:|
| Invocação | 3 base; sinergias até o mínimo de 1 |
| Golpe do brasão | 0 de custo explícito; a geração nativa é separada |
| Cura | 9 de base, com sinergia de cura aplicável; recupera até 3 HP |
| Defender-se | 0; pode conceder 2 seda, mais bônus de `Reaper`, quando há dano real |
| Skill | custo lido de `PlayerData.SilkSkillCost`, com sinergia de ferramenta |

As regras de capacidade máxima de seda continuam sendo as do jogo. Os descontos acima vêm de [`CrestSynergy.GetActionCost`](../src/CrestSynergy.cs#L17) e são recalculados conforme o brasão/ferramenta atualmente equipados.

Referências: constantes de combate [`SummonSilkCost` e `SummonActionDuration`](../src/TurnBattleSystem.cs#L62), [`GetCurrentSummonCost`](../src/TurnBattleSystem.cs#L614), [`CreateSummonHit`](../src/TurnBattleSystem.cs#L1287) e [`FinishSummonAction`](../src/TurnBattleSystem.cs#L1221).

## 7. Formato JSON real

### 7.1 Bloco no save nativo

O patch de serialização substitui/cria a propriedade de raiz `SilkThreadCrest`. O conteúdo é produzido por `JObject.FromObject(EnemySummonRegistry.ExportSaveData())` e gravado sem formatação. A forma lógica é:

```json
{
  "...outros campos do save nativo...": "...",
  "SilkThreadCrest": {
    "Version": 1,
    "NextAcquisitionOrder": 2,
    "Invocations": [
      {
        "Key": "husk guard",
        "DisplayName": "Husk Guard",
        "AcquisitionOrder": 1,
        "Attacks": [
          {
            "Name": "Slash",
            "Damage": 8,
            "AnimationClip": "Slash",
            "AttackOrder": 1
          },
          {
            "Name": "Golpe básico",
            "Damage": 1,
            "AnimationClip": null,
            "AttackOrder": 2
          }
        ]
      }
    ]
  }
}
```

O exemplo mostra todos os campos do módulo. O objeto real pode ter mais campos nativos fora de `SilkThreadCrest`; o mod preserva os demais campos quando consegue fazer parse e serializar o JSON.

Schema efetivo do bloco:

```text
SilkThreadCrest: object
  Version: int (atual 1)
  NextAcquisitionOrder: int (mínimo exportado 1)
  Invocations: array
    Key: string
    DisplayName: string
    AcquisitionOrder: int
    Attacks: array
      Name: string
      Damage: int (exportado com mínimo 1)
      AnimationClip: string ou null
      AttackOrder: int
```

`VisualTemplate` nunca aparece no JSON. Nenhum `GameObject`, componente Unity, prefab, renderer ou textura é serializado.

### 7.2 Conteúdo do sidecar

O sidecar contém somente o objeto interno de `SilkThreadSummonSaveData`, sem o envelope `SilkThreadCrest`:

```json
{
  "Version": 1,
  "NextAcquisitionOrder": 2,
  "Invocations": [
    {
      "Key": "husk guard",
      "DisplayName": "Husk Guard",
      "AcquisitionOrder": 1,
      "Attacks": [
        {
          "Name": "Slash",
          "Damage": 8,
          "AnimationClip": "Slash",
          "AttackOrder": 1
        }
      ]
    }
  ]
}
```

A serialização usa `JsonConvert.SerializeObject(..., Formatting.None)`. Campos nulos, como `AnimationClip`, pertencem ao formato possível do registro; não devem ser interpretados como erro de persistência.

### 7.3 Regras de leitura

Ao restaurar, `RestoreFromSaveData` primeiro limpa a lista e destrói templates visuais antigos. Depois:

- `null` ou ausência de `Invocations` limpa o registro;
- registros sem `Key` são ignorados;
- `Key` é novamente limpo e normalizado para minúsculas;
- `DisplayName` vazio cai para o nome da chave;
- ataques sem `Name` são ignorados;
- dano é limitado a pelo menos 1;
- entradas sem ataques válidos não entram em `learned`;
- `AcquisitionOrder <= 0` recebe uma ordem nova;
- `nextAcquisitionOrder` avança para além da maior ordem restaurada.

O formato gerado pelo próprio mod não produz entradas duplicadas, mas `RestoreFromSaveData` não é um validador completo de arquivos editados manualmente. JSON com registros duplicados, ordens conflitantes ou ataques semanticamente inválidos deve ser tratado como dado não suportado.

Referências: classes de dados [`SilkThreadSummonSaveData`](../src/EnemySummonRegistry.cs#L35), [`SilkThreadSummonSaveRecord`](../src/EnemySummonRegistry.cs#L42), [`SilkThreadSummonAttackSaveRecord`](../src/EnemySummonRegistry.cs#L50), [`ExportSaveData`](../src/EnemySummonRegistry.cs#L196) e [`RestoreFromSaveData`](../src/EnemySummonRegistry.cs#L230).

## 8. Save nativo, sidecar e caminhos por slot

### 8.1 Save nativo

A classe `SilkThreadSerializeSavePatch` localiza por reflexão a definição genérica `GameManager.GetJson<T>` e fecha especificamente `GetJson<SaveGameData>`. O postfix recebe o JSON produzido pelo caminho de save ativo e chama `InjectIntoSaveJson`.

A escolha da instância genérica fechada é intencional: o comentário do código registra que o Harmony não é confiável quando recebe a definição genérica aberta neste build. Portanto, o ponto documentado é o caminho efetivamente usado por `GetBytesForSaveData`, não um método `SerializeSaveData` genérico presumido.

Quando o JSON é válido, `InjectIntoSaveJson` grava/atualiza `root["SilkThreadCrest"]`. Se o parse ou a serialização falhar, ele devolve o JSON original e registra o aviso; nesse caso o sidecar continua sendo a redundância esperada quando a captura foi commitada.

### 8.2 Sidecar por slot

`WriteSidecar` escreve em:

```text
Application.persistentDataPath/SilkThreadCrest/summons-slot-N.json
```

onde `N` é o `profileID` obtido por reflexão de `GameManager`. O diretório `SilkThreadCrest` é criado automaticamente. Se a leitura do campo falhar ou não houver instância disponível, o código usa **0** como fallback.

O sidecar é escrito em `QueueGameSave`, chamado pelo `Commit` antes de tentar `GameManager.instance.QueueSaveGame()`. Assim, a persistência redundante acontece no momento do desbloqueio, e não somente quando o jogador deixa o painel de vitória.

### 8.3 Carregamento pela sobrecarga JSON

O patch de `SetLoadedGameData(string, int)` chama `RestoreFromSaveJson(json)`. A regra é:

1. JSON vazio: tenta o sidecar identificado pelo `GetProfileId()` atual; se não houver, limpa o registro.
2. JSON válido sem `SilkThreadCrest`: tenta o sidecar do `GetProfileId()` atual; se não houver, limpa.
3. JSON com bloco nulo: mesma tentativa de sidecar.
4. JSON com bloco, mas `Invocations` nulo ou vazio: tenta o sidecar.
5. JSON com invocações: restaura o bloco nativo e não precisa do sidecar.
6. Parse ou conversão com exceção: tenta o sidecar; se falhar, limpa o registro.

Esta sobrecarga recebe um inteiro de slot no método do jogo, mas o postfix atual usa o JSON e o `GetProfileId()` para resolver o sidecar; ele não passa o argumento de slot para `RestoreFromSaveJson`.

### 8.4 Carregamento pela sobrecarga tipada

O patch de `SetLoadedGameData(SaveGameData, int)` recebe `__1` e chama diretamente `RestoreFromSidecar(__1)`. Esta é a proteção contra a troca de slot quando a rota tipada do jogo já entrega o número selecionado: o caminho usa o inteiro recebido para abrir `summons-slot-N.json`.

Na rota tipada atual, o patch não extrai novamente o bloco `SilkThreadCrest` do objeto `SaveGameData`; ele depende do sidecar correspondente ao slot. Por isso a redundância por slot é parte funcional do suporte à sobrecarga tipada, não apenas um backup informal.

### 8.5 New Game e troca de slots: riscos operacionais

`StartNewGame(bool, bool)` executa duas ações:

- `ClearSidecar()` para o `profileID` que `GetProfileId()` retornar naquele momento;
- `EnemySummonRegistry.RestoreFromSaveData(null)`, limpando a memória e destruindo templates.

Há riscos que devem ser considerados ao trocar de slot ou iniciar um novo jogo:

- na sobrecarga string, o slot recebido pelo jogo não é usado diretamente; se `profileID` ainda não tiver sido atualizado no callback, o sidecar errado pode ser consultado;
- na sobrecarga tipada, o inteiro `__1` é a fonte correta do nome do sidecar, mas a disponibilidade desse sidecar é essencial;
- o fallback de `profileID` é 0, portanto uma falha de leitura pode concentrar dados em `summons-slot-0.json`;
- `ClearSidecar` limpa somente o slot resolvido naquele instante, não todos os sidecars; se a troca de perfil ocorrer depois do postfix de `StartNewGame`, um sidecar antigo pode permanecer;
- um save nativo vazio ou sem o bloco pode restaurar um sidecar antigo do mesmo identificador. Isso é útil para recuperar um save que não recebeu a extensão, mas pode ressuscitar dados se arquivos de slot tiverem sido copiados ou trocados manualmente;
- se o bloco nativo tiver `Invocations.Count > 0`, mas todos os registros forem inválidos e acabarem sem ataques, o critério de “não vazio” pode impedir a tentativa do sidecar. Isso é um caso de corrupção parcial a validar.

**Procedimento seguro para o usuário:** faça backup dos saves antes de trocar slots, não renomeie sidecars manualmente sem manter a correspondência `N`, e valide a primeira batalha após carregar cada slot. Um novo jogo deve ser tratado como limpeza do catálogo daquele contexto, não como migração automática entre slots.

Referências: [`InjectIntoSaveJson`](../src/SilkThreadSavePatches.cs#L20), [`RestoreFromSaveJson`](../src/SilkThreadSavePatches.cs#L37), [`WriteSidecar`](../src/SilkThreadSavePatches.cs#L70), [`RestoreFromSidecar(int)`](../src/SilkThreadSavePatches.cs#L100), [`GetSidecarPath`](../src/SilkThreadSavePatches.cs#L127), patches de [`GetJson<SaveGameData>`](../src/SilkThreadSavePatches.cs#L153), [`SetLoadedGameData(string, int)`](../src/SilkThreadSavePatches.cs#L178), [`SetLoadedGameData(SaveGameData, int)`](../src/SilkThreadSavePatches.cs#L197) e [`StartNewGame`](../src/SilkThreadSavePatches.cs#L216).

## 9. Reidratação visual depois de carregar

O save restaura ataques, nomes e ordens, mas não consegue restaurar `VisualTemplate`: referências Unity não fazem parte do JSON. Depois do carregamento, as entradas podem existir no catálogo e ser usadas para dano, enquanto o campo visual permanece nulo.

`TurnBattleSystem.Update` chama `EnemySummonRegistry.TryRehydrateVisualTemplates` a cada aproximadamente **1 s**. Esse método:

1. verifica se há entradas sem template;
2. procura `HealthManager` disponíveis na cena com `FindObjectsByType`;
3. compara `MakeKey(candidate.gameObject)` com a chave persistida;
4. quando encontra uma instância compatível, cria novamente o template visual e o mantém em `DontDestroyOnLoad`.

Não há lookup de prefab por nome, Addressables, arquivo de arte ou catálogo externo. Portanto:

- se uma instância do mesmo tipo aparecer depois em uma cena, o template pode ser reidratado;
- se o inimigo não estiver presente na cena atual, o template continua ausente até que uma instância compatível seja encontrada;
- se a versão do jogo não carregar mais aquele `GameObject`, se o nome mudar ou se a criação do clone falhar, o visual pode permanecer indisponível;
- mesmo sem template, a entrada continua no save e o dano lógico continua disponível;
- a ausência de clip específico também não cancela a ação: o código tenta o clip padrão TK2D quando possível.

Essa separação é importante: **persistência do catálogo não equivale a persistência do prefab visual**. O sistema foi desenhado para degradar para “menu + dano, sem aparição” em vez de falhar a batalha.

Referências: [`TryRehydrateVisualTemplates`](../src/EnemySummonRegistry.cs#L278) e a atualização periódica do sistema em [`Update`](../src/TurnBattleSystem.cs#L206).

## 10. Status de implementação e evidência

- O código da base 0.7.25 contém captura pendente, commit ligado à morte/vitória, catálogo com blacklist, clone visual isolado, dano direto com fallback protegido, custo de seda, ordenação e persistência nativa + sidecar.
- O combate foi confirmado como funcionamento conforme o status fornecido para esta base.
- A persistência de invocações foi confirmada na linha de desenvolvimento 0.7.15 e permanece representada no código atual por `SilkThreadSavePatches` e `QueueGameSave`.
- O menu e as paletas foram confirmados na 0.7.24; neste documento, apenas o menu de invocação é parte do módulo. Não se deve interpretar a existência do menu como prova de que cada inimigo tem um catálogo visual perfeito.
- A skin experimental **Silk Eclipse** aplicada em 0.7.24 tinha tamanho e animações incorretos; 0.7.25 corrigiu a sincronização/tamanho, foi compilada e acompanhada de 30 testes do seletor, mas a aplicação **ainda não foi confirmada in-game**. Isso é uma observação de status da base, não uma dependência do dano ou do save de invocações.
- A compilação é feita contra as assemblies recebidas. O jogo Windows não foi executado neste sandbox. Assim, continuam sem confirmação in-game ampla: persistência real em todas as rotas de save, troca de slots, reidratação em todas as cenas, aparência de cada inimigo, seleção de clips, bosses, variantes e regras de dano especiais.

Não há uma porcentagem de conclusão atribuída aqui: os itens acima distinguem comportamento presente no código de evidência validada e de áreas ainda não testadas.

## 11. Riscos conhecidos

1. **Identidade por nome:** inimigos distintos com o mesmo nome limpo são agrupados; variantes não têm chave própria.
2. **Heurística de ataques:** `DamageHero`, nomes, tags e `hazardType` podem não representar com precisão ataques prolongados, multi-hit, partes de boss ou dano contextual.
3. **Blacklist incompleta por convenção:** nomes/tags inéditos de contato, aura ou hazard podem passar; uma blacklist ampla também pode excluir um ataque legítimo cujo nome coincida com um token passivo.
4. **Fallback básico enganoso:** `Golpe básico` com dano 1 garante uma opção, mas pode dar a impressão de que o ataque real foi capturado quando não foi.
5. **Dano bruto:** não existe multiplicador ou teto geral do dano aprendido. Um `damageDealt` alto entra no catálogo com o mesmo valor mínimo corrigido, sujeito às regras do `HealthManager` no momento do hit.
6. **Animação separada da lógica:** o clip é escolhido por score de nome e pode não corresponder exatamente ao hit selecionado; o dano não depende do clone nem do clip.
7. **Visual ausente após restart:** só nomes, golpes e ordens são persistidos. Sem uma instância compatível na cena, não há template para instanciar.
8. **Sidecar e slot:** a rota string depende do `profileID` atual, a rota tipada depende do inteiro recebido, e o fallback para perfil 0 pode misturar arquivos em caso de falha de contexto.
9. **Corrupção parcial:** um bloco nativo estruturalmente não vazio, mas sem ataques válidos, pode impedir a tentativa de recuperar o sidecar.
10. **Estados especiais do inimigo:** a consulta `IsInvincible/CheckInvincible` protege o fallback conhecido, mas não transforma todos os estados de transição, fases e imunidades em um contrato uniforme.
11. **Múltiplos eventos de dano:** o módulo captura componentes seguros individualmente e mede a perda de HP do alvo; inimigos com vários eventos no mesmo turno precisam de validação específica.

## 12. Propostas futuras — não implementadas nesta base

As ideias abaixo são propostas, não funcionalidades existentes:

- substituir a chave baseada em nome por um identificador estável de tipo/prefab, preservando uma regra de migração para saves antigos;
- armazenar um identificador de fonte visual reidratável ou um catálogo de prefabs, em vez de depender de uma instância do inimigo na cena;
- passar explicitamente o slot recebido também pela rota string e adicionar validação de perfil ao conteúdo do sidecar;
- registrar integridade, versão de schema e origem do save para evitar que um sidecar antigo seja usado como fallback silencioso;
- adicionar metadados explícitos de ataque seguro/perigoso para complementar ou substituir a heurística de nomes/tags;
- definir teto, multiplicador ou curva de dano de invocações, caso o balanceamento do dano bruto seja considerado excessivo;
- modelar ataques multi-hit e ataques de fase como ações próprias, em vez de tratar cada `DamageHero` como uma opção independente;
- ampliar a matriz de teste in-game para inimigos comuns, variantes com o mesmo nome, hazards, inimigos aéreos, bosses, transições de fase, restart sem o inimigo na cena, carregamento pelas duas sobrecargas e troca de todos os slots.

## Referências rápidas

- [`EnemySummonRegistry.cs`](../src/EnemySummonRegistry.cs): dados, chave, captura, blacklist, ordenação, exportação, restauração e visual.
- [`TurnBattleSystem.cs`](../src/TurnBattleSystem.cs): custo, menu, ação, dano, fallback, vitória e patch de morte.
- [`SilkThreadSavePatches.cs`](../src/SilkThreadSavePatches.cs): extensão do JSON nativo, sidecar por perfil/slot, sobrecargas de carregamento e `StartNewGame`.
- [`CrestSynergy.cs`](../src/CrestSynergy.cs): descontos de custo e piso mínimo de seda.
- [Visão geral do repositório](../README.md): em caso de divergência, prevalece o código acima.
