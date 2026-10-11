# Brasão do Fio, Sacrifício e inicialização

**Base documentada:** `0.7.25` (`SilkThreadCrestPlugin.PluginVersion`). Este documento descreve **exatamente o módulo do Brasão do Fio, do item Sacrifício e de sua inicialização**, com o código atual como autoridade. A implementação é um mod BepInEx/Harmony para Hollow Knight: Silksong.

> **Regra de leitura:** quando uma descrição de mecânica, texto localizado ou README divergir do comportamento implementado, prevalece o código. Os trechos marcados como **Código atual** são observações de implementação; os trechos marcados como **Mecânica para o jogador** descrevem o efeito que se espera ao jogar.

## Fontes do módulo

- [src/SilkThreadCrestPlugin.cs](../src/SilkThreadCrestPlugin.cs) — registro, inicialização, localização e patches Harmony do ataque descendente, pogo e Sacrifício.
- [src/SilkThreadMove.cs](../src/SilkThreadMove.cs) — projétil do ferrão, fio visual, captura do alvo, puxão, invulnerabilidade e segundo golpe.
- [src/SilkThreadSavePatches.cs](../src/SilkThreadSavePatches.cs) — patches opcionais de serialização/carregamento que são instalados pelo plugin; neste documento, apenas a parte necessária para entender a inicialização e a persistência relacionada às invocações.
- [src/EnemySummonRegistry.cs](../src/EnemySummonRegistry.cs) — fonte dos dados que os patches de save exportam e restauram; a mecânica de invocações em si está fora do escopo deste documento.

## 1. Visão rápida

O módulo faz quatro coisas relacionadas:

1. cria e registra o **Brasão do Fio** (`SilkThread`) quando o inventário de brasões do jogo está pronto;
2. cria e desbloqueia o item **Sacrifício** (`SilkThreadSacrifice`) em um espaço compatível do brasão;
3. intercepta o ataque para baixo de Hornet para estender o ferrão por um fio, puxar Hornet até um inimigo e aplicar um segundo golpe;
4. quando Sacrifício está equipado, converte determinados danos ambientais em um pogo normal e, em regra, um ponto de dano, com proteção contra dano repetido e custo de seda no uso consecutivo.

O código **não define um dano fixo próprio para o ataque normal**. O primeiro golpe usa o `Downspike`/`DamageEnemies` nativo, e o segundo reutiliza o `HitInstance` do primeiro golpe. Também não há uma seção de configuração para alterar os números de combate: os valores desta mecânica são constantes no código.

## 2. Como usar

### Pré-requisitos de runtime

O fluxo presume que o jogo já disponibilizou:

- uma instância de `ToolItemManager`;
- `PlayerData.HasInstance`;
- pelo menos uma `InventoryToolCrestList` de cena com `IsSetupComplete == true`;
- um `HeroController` ativo para o controlador de combate auxiliar.

O módulo não exige que o jogador tenha o brasão `Hunter` desbloqueado. `Hunter` é apenas o **template preferencial** para clonar o novo brasão; se não existir, o código tenta o primeiro brasão disponível.

### Passo a passo

1. Instale o plugin na instalação BepInEx do jogo.
2. Inicie o jogo e aguarde o inventário terminar a configuração inicial. O plugin tenta novamente a cada meio segundo até poder registrar os itens.
3. No inventário, equipe **Brasão do Fio**. Ele é criado e desbloqueado automaticamente pelo plugin.
4. Se quiser a mecânica de conversão de dano, equipe também **Sacrifício** em um dos espaços aceitos pelo brasão.
5. Fora do combate por turnos, fique no ar e use o ataque para baixo:
   - se o ferrão encontrar um inimigo válido, Hornet é puxada até ele e ocorre o segundo golpe;
   - se encontrar terreno, terreno macio ou espinhos reconhecidos, termina em um pogo normal;
   - se não houver contato, o movimento termina por distância/tempo limite.
6. Com Sacrifício equipado, receba um dano cujo `HazardType` seja ambiental elegível. A primeira conversão não custa seda; uma nova conversão dentro da janela consecutiva exige 1 seda, além de respeitar o cooldown curto.

O ícone de Sacrifício e o desenho do fio/ferrão ainda são **placeholders**. O ícone copia temporariamente um item existente; o fio é desenhado por `LineRenderer`, não por uma arte final do brasão.

## 3. Inicialização e ciclo de registro

### 3.1 `Awake`: ordem real

A ordem está em [`SilkThreadCrestPlugin.Awake`](../src/SilkThreadCrestPlugin.cs#L33-L74):

1. grava a instância estática `Instance`;
2. cria a configuração `Appearance / MenuShortcut`, com padrão `F8`;
3. tenta adicionar `SilkThreadAppearanceMenu` e inicializá-lo usando a pasta da própria assembly;
4. se a inicialização visual falhar, registra aviso e **não desativa o sistema de combate/itens**;
5. cria `Harmony` com o GUID `manus.silkthreadcrest`;
6. enumera os tipos da assembly com atributo `[HarmonyPatch]` e aplica cada classe individualmente;
7. deixa quatro patches de save fora dessa primeira passagem e instala-os separadamente em [`TryInstallOptionalSavePatches`](../src/SilkThreadCrestPlugin.cs#L76-L99);
8. registra que está aguardando `ToolItemManager` e `PlayerData`.

Cada patch normal é protegido por `try/catch`. Se uma classe falhar, as demais continuam sendo tentadas. Os patches opcionais de save têm tratamento próprio: falha de instalação gera aviso e o restante do sistema continua disponível. Se nenhuma rota de persistência funcionar, a progressão de invocações pode ficar apenas na sessão.

### 3.2 `Update`: espera por dados prontos

[`SilkThreadCrestPlugin.Update`](../src/SilkThreadCrestPlugin.cs#L101-L126) só faz uma verificação a cada `0,5 s` de tempo não escalado. Em cada verificação:

- chama [`EnsureTurnBattleSystem`](../src/SilkThreadCrestPlugin.cs#L128-L135), que adiciona `TurnBattleSystem` a Hornet se ainda não houver um componente;
- exige `ToolItemManager` e `PlayerData.HasInstance`;
- espera uma lista de menu de brasões em cena com `IsSetupComplete`;
- chama [`TryRegisterCrest`](../src/SilkThreadCrestPlugin.cs#L188-L251).

Enquanto o menu ainda não está pronto, o plugin registra uma única mensagem de espera e não tenta inserir o brasão prematuramente. Isso evita alterar uma lista ainda em construção.

### 3.3 Registro do Brasão do Fio

`TryRegisterCrest` usa o identificador `CrestId = "SilkThread"`.

**Se já existe uma entrada:**

- reaplica [`CrestLocalization.Configure`](../src/SilkThreadCrestPlugin.cs#L406-L415);
- desbloqueia a entrada se ela ainda estiver bloqueada;
- tenta registrar Sacrifício;
- marca o `ToolItemManager` como registrado;
- agenda a atualização do menu.

**Se não existe:**

1. procura `ToolItemManager.GetCrestByName("Hunter")`;
2. se não encontrar, usa o primeiro brasão existente;
3. clona o template com `UnityEngine.Object.Instantiate`;
4. define o nome como `SilkThread`;
5. limpa `isHidden`, `previousVersion`, `oldPreviousVersion` e `upgradedVersion` por reflexão;
6. troca as localizações para nome, descrição, prefixo, prompt de obtenção e texto de equipamento;
7. acessa o campo privado `ToolItemManager.crestList` por `AccessTools.Field`;
8. adiciona o clone, incrementa a versão do gerenciador, aplica traduções e chama `Unlock()`;
9. registra Sacrifício e agenda a atualização do menu.

O resultado é um brasão que **herda ataques, slots e demais configurações do template**. O texto de log deixa isso explícito: outras configurações/ataques vêm do template, preferencialmente `Hunter`. O módulo adiciona o comportamento do fio por patches; ele não cria um ataque de dano independente.

Após o registro, [`ScheduleCrestMenuRefresh`](../src/SilkThreadCrestPlugin.cs#L341-L345) chama `InventoryToolCrestList.Setup` após `0,35 s`, em todas as listas de cena válidas. Se `Setup` não puder ser localizado, o menu deverá atualizar ao ser reaberto.

### 3.4 Registro do Sacrifício e escolha de slot

O item usa `SacrificeId = "SilkThreadSacrifice"` e é tratado por [`TryRegisterSacrifice`](../src/SilkThreadCrestPlugin.cs#L253-L339).

Se já existir, o plugin reaplica o texto localizado e o desbloqueia sem popup. Se não existir, ele:

1. examina os slots do brasão e seus dados de save;
2. escolhe o primeiro slot desbloqueado **não ofensivo** (`!slots[i].Type.IsAttackType()`);
3. se não houver, escolhe o primeiro slot desbloqueado de qualquer tipo;
4. procura um `ToolItemBasic` de template do tipo escolhido, ou o primeiro `ToolItemBasic` disponível;
5. clona o template e define o nome, tipo e campos básicos;
6. zera `baseStorageAmount` e `unlockStartAmount`, marca `isCounted = false`, `preventTutorialMsg = true`, limpa `usageOptions`, `hasCustomAction` e `damageFlags`;
7. adiciona o item à lista privada `ToolItemManager.toolItems`;
8. incrementa a versão, chama `Unlock(popupFlags: None)`, envia `SendEquippedChangedEvent(force: true)` e aplica traduções.

O item não tem estoque consumível próprio e não possui um custo de equipá-lo. O **custo de seda é aplicado somente na conversão consecutiva de dano**, descrita na seção 7.

> **Limite de slot:** se não houver slot desbloqueado compatível, o item não é registrado naquele ciclo e o plugin tentará novamente. O fallback para qualquer slot desbloqueado não elimina a exigência de haver um slot.

## 4. Patches que ativam o movimento

A mecânica usa quatro pontos de Harmony no `SilkThreadCrestPlugin.cs`.

### 4.1 Início no ataque para baixo

[`DownAttackPatch`](../src/SilkThreadCrestPlugin.cs#L529-L543) é postfix de `HeroController.DownAttack`:

- ignora Hornet no chão;
- ignora qualquer brasão diferente de `SilkThread`;
- obtém ou adiciona `SilkThreadMove` ao objeto de Hornet;
- lê o campo privado `HeroController.currentDownspike`;
- chama `SilkThreadMove.BeginMove(hero, attack)`.

Se o `Downspike` ativo não puder ser lido, o componente ainda é ativado, mas perde a hitbox customizada e registra um aviso. Nesse caso, o jogo pode continuar usando a hitbox inimiga original.

### 4.2 Captura do primeiro alvo e do primeiro `HitInstance`

Há dois caminhos de resposta:

- [`NailHitEnemyPatch`](../src/SilkThreadCrestPlugin.cs#L545-L556), postfix de `HeroController.NailHitEnemy`;
- [`DownAttackHitResponsePatch`](../src/SilkThreadCrestPlugin.cs#L558-L570), prefix de `HeroDownAttack.OnHitResponded`.

Ambos procuram o `SilkThreadMove` ativo e chamam `SetTarget(HealthManager, HitInstance)`. O primeiro alvo aceito fica preso ao movimento. Um `HitInstance` só é guardado como primeiro golpe se tiver `Source` ou `DamageDealt > 0`; sem uma resposta de dano positiva, o segundo golpe pode ser omitido.

### 4.3 Hitbox nativa e cast customizado

[`DownAttackNativeHitboxFilterPatch`](../src/SilkThreadCrestPlugin.cs#L572-L588) filtra `HeroDownAttack.OnTriggerEnter2D` somente quando o movimento possui uma hitbox customizada (`attack.EnemyDamager`). Colisores de inimigo ou com `HealthManager` não seguem o caminho nativo nesse caso, para evitar dupla resolução; o cast customizado procura o alvo e chama `DamageEnemies.TryDoDamage`.

Se não houver `EnemyDamager`, o patch não filtra a hitbox nativa e a execução fica no caminho original do jogo.

## 5. Movimento do fio e do ferrão

As constantes de [`SilkThreadMove`](../src/SilkThreadMove.cs#L5-L13) são:

| Constante | Valor exato | Uso |
|---|---:|---|
| `SpearSpeed` | `24` unidades/s | Velocidade de avanço descendente |
| `SpearRadius` | `0,12` unidade | Raio do `CircleCastAll` |
| `SpearMaxDistance` | `12` unidades | Alcance máximo descendente |
| `PullSpeed` | `18` unidades/s | Velocidade de aproximação de Hornet |
| `StopDistance` | `0,55` unidade | Distância para concluir o puxão |
| `MaxPullTime` | `0,7 s` | Limite do puxão |
| `NoHitTimeout` | `1,8 s` | Limite para encerrar um movimento sem alvo |

### 5.1 Armamento e estado

[`BeginMove`](../src/SilkThreadMove.cs#L38-L62) encerra um movimento anterior, limpa alvo e estados, marca `ActiveMove`, salva o horário e começa o ferrão `0,25` unidade abaixo de Hornet. Também cria os visuais do fio.

O método não cria uma nova arma de dano: recebe o `Downspike` nativo e o usa como fonte de `EnemyDamager` quando disponível. O cast é uma sondagem complementar para localizar terreno, espinhos e inimigos durante o avanço.

### 5.2 Avanço e colisões

Em [`FixedUpdate`](../src/SilkThreadMove.cs#L135-L161), enquanto não há puxão, o componente chama `AdvanceSpear` se:

- o ataque existir;
- `attack.IsDamagerActive` for verdadeiro;
- `attack.CanDamageEnemies` for verdadeiro;
- o projétil ainda não tiver terminado.

[`AdvanceSpear`](../src/SilkThreadMove.cs#L163-L212) usa `Physics2D.CircleCastAll` com direção para baixo e todas as layers (`~0`). Para cada colisão:

1. identifica terreno ou terreno macio pela layer `TERRAIN`/`SOFT_TERRAIN`;
2. identifica espinhos somente quando o `DamageHero` tem `SPIKES` ou `COAL_SPIKES`;
3. ao acertar terreno/espinho, termina o projétil, chama `hero.DownspikeBounce(false)` e deixa o jogo resolver um pogo normal;
4. caso contrário, procura `HealthManager` no collider ou no pai;
5. ignora o próprio alvo de Hornet, inimigos mortos e objetos filhos de Hornet;
6. registra um alvo pendente, tenta `attack.EnemyDamager.TryDoDamage(hit.collider)` e preserva a posição do ferrão se o dano for aceito.

O alcance é acumulado em `spearTravel`. Ao atingir `12` unidades, o cast marca `projectileFinished`; se não houver alvo, o timeout de `1,8 s` encerra o movimento.

> **Importante:** “espinho” no código não significa qualquer objeto visual com espinhos. A conversão para o ramo de pogo depende da layer de terreno ou do componente `DamageHero` com exatamente os tipos de hazard reconhecidos.

### 5.3 Puxão e recuperação de controle

Após um alvo ser definido, `Update` espera `0,14 s` antes de chamar [`BeginPull`](../src/SilkThreadMove.cs#L101-L113), salvo se o callback nativo de pogo disparar o puxão antes. O método:

- marca `pulling`;
- registra o início do puxão;
- concede a invulnerabilidade descrita na seção 6;
- chama `hero.FinishDownspike(false)`;
- chama `hero.RelinquishControl()`;
- desativa a gravidade;
- zera a velocidade do corpo.

Em `FixedUpdate`, Hornet é movida por `Rigidbody2D.MovePosition` até `target.transform.position + Vector2.up * 0,42`. O puxão termina quando chega a até `0,55` unidade do destino ou quando passa `0,7 s`.

### 5.4 Segundo golpe e pogo curto

[`CompletePull`](../src/SilkThreadMove.cs#L214-L243) aplica o segundo golpe no máximo uma vez:

```text
se target existe, firstHit.DamageDealt > 0 e o alvo ainda está vivo:
    segundo HitInstance = cópia do primeiro
    segundo.Source = hero.gameObject
    segundo.IsFirstHit = false
    segundo.IgnoreInvulnerable = true
    target.ApplyExtraDamage(segundo)
```

Depois disso, chama `hero.DownspikeBounceSlightlyShort(false)`, registra o pogo curto sem altura extra e encerra o movimento.

**Fórmula efetiva quando o caminho funciona:**

```text
Dano total = dano do primeiro Downspike + dano do segundo HitInstance
Dano potencial = 2 × dano positivo do primeiro golpe
```

O segundo golpe é pulado se não houver `HitInstance` positivo ou se o alvo já estiver morto. Como o clone define `IgnoreInvulnerable = true`, ele pode atravessar uma invulnerabilidade que impediria um hit normal naquele instante. Esse é um ponto de risco de balanceamento, não uma promessa de invulnerabilidade do jogo.

## 6. Invulnerabilidade e regras de pogo

### Invulnerabilidade durante o puxão

[`GrantPullInvulnerability`](../src/SilkThreadMove.cs#L245-L257) cria um token novo, adiciona-o com `hero.AddInvulnerabilitySource` e define expiração em `Time.unscaledTime + 1 s`.

- A duração é **1,0 segundo**, independente de `Time.timeScale`.
- [`UpdatePullInvulnerability`](../src/SilkThreadMove.cs#L259-L268) remove o token quando expira.
- `EndMove` não remove o token imediatamente; ele expira pelo `Update` do componente. `OnDestroy` remove o token se o componente for destruído antes disso.
- Se o puxão acabar antes de um segundo, a invulnerabilidade ainda pode permanecer até o prazo de expiração.

### Interceptação de `DownspikeBounce`

[`DownspikeBouncePatch`](../src/SilkThreadCrestPlugin.cs#L591-L613) decide qual pogo usar:

- quando `AllowNativeDownspikeBounce` está ativo, deixa a chamada nativa passar;
- com Brasão do Fio, movimento ativo e alvo capturado, chama `BeginPull` e bloqueia o bounce nativo naquele ponto;
- com Brasão do Fio, mas sem alvo utilizável, chama `DownspikeBounceSlightlyShort(harpoonRecoil)`, registra um pogo curto e encerra o movimento;
- sem o brasão, não interfere.

`StartNormalSacrificePogo` usa a flag `AllowNativeDownspikeBounce` temporariamente para chamar `hero.DownspikeBounce(false)` sem que o próprio patch transforme o pogo do Sacrifício em puxão. Assim, o pogo resultante é o caminho nativo normal.

## 7. Sacrifício: ativação, dano e uso repetido

### 7.1 Quando o item está ativo

[`IsSacrificeEquipped`](../src/SilkThreadCrestPlugin.cs#L153-L159) só retorna verdadeiro quando as duas condições são atendidas:

1. `PlayerData.instance.CurrentCrestID == "SilkThread"`;
2. `ToolItemManager.GetToolByName("SilkThreadSacrifice")` existe e `IsEquipped` é verdadeiro.

Portanto, possuir ou desbloquear Sacrifício não basta: ele precisa estar equipado dentro do Brasão do Fio.

### 7.2 Quais danos são interceptados

[`SacrificeHazardPatch`](../src/SilkThreadCrestPlugin.cs#L644-L708) é prefix/postfix de `HeroController.TakeDamage`. O prefixo **não converte** quando:

- Sacrifício não está ativo;
- `hazardType == ENEMY`;
- `hazardType == NON_HAZARD`;
- `hazardType == EXPLOSION`.

Todos os demais `HazardType` passam pelo ramo de conversão, se as condições de cooldown e seda permitirem. A descrição localizada usa “espinhos e outros perigos ambientais”, mas a lista real de exclusões é a lista acima; ela não testa uma categoria genérica chamada “ambiental”. Isso cria um limite: um hazard não ambiental que use outro tipo pode ser convertido por engano.

### 7.3 Conversão e vida mínima

Quando converte, o prefixo salva:

- `HealthBefore = data.health + data.healthBlue`;
- `WasAtOneHealth = data.health <= 1`;
- o custo consecutivo;
- o estado `Converted`.

Depois altera os argumentos da chamada nativa:

```text
damageAmount = (PlayerData.health > 1) ? 1 : 0
hazardType = NON_HAZARD
damagePropertyFlags = None
```

Na prática:

- com mais de 1 ponto de vida vermelha, a tentativa causa 1 ponto de dano;
- com 1 ponto ou menos de vida vermelha, o dano alterado é 0, preservando pelo menos 1;
- o código compara a vida total (`health + healthBlue`) no postfix antes de confirmar uma perda real;
- se a vida vermelha estiver em 1, `WasAtOneHealth` torna a conversão elegível mesmo sem redução de vida total, e o pogo/cooldown pode ser confirmado.

O postfix só conclui a conversão normal quando a chamada não foi rejeitada e a condição de perda de vida foi satisfeita — ou quando `WasAtOneHealth` é verdadeiro. Em seguida, cobra a seda consecutiva, inicia cooldown, registra a conversão, chama `StartNormalSacrificePogo` e grava o log.

### 7.4 Cooldown imediato

O componente [`SacrificeHazardCooldown`](../src/SilkThreadCrestPlugin.cs#L615-L634) mantém dois relógios em tempo não escalado:

| Regra | Valor |
|---|---:|
| Cooldown após uma conversão | `0,6 s` |
| Janela para uso consecutivo | `2,5 s` |
| Custo do uso consecutivo | `1 seda` |

Enquanto `IsActive` for verdadeiro, o prefixo retorna `false` e bloqueia aquela chamada de `TakeDamage`; não há dano nativo, custo nem novo pogo nessa chamada.

O relógio da janela consecutiva só é atualizado por `RegisterConversion`, isto é, após uma conversão confirmada. Uma tentativa recusada por falta de seda não abre uma nova janela.

### 7.5 Uso repetido e custo

A regra exata é:

```text
se (tempo desde a última conversão <= 2,5 s):
    custo = 1 seda
senão:
    custo = 0 seda
```

Antes de alterar o dano, o prefixo verifica `PlayerData.silk >= 1` para o caso consecutivo. Se não houver seda suficiente:

- a conversão é recusada;
- o `TakeDamage` nativo continua (`return true`);
- o dano ambiental original permanece.

Se houver seda, o postfix chama `hero.TakeSilk(1)` **somente depois** de confirmar a conversão. A primeira conversão após uma janela de mais de `2,5 s` não custa seda. Sacrifício não gera seda por si só.

## 8. Requisitos, custos e valores

### Requisitos funcionais

| Item | Requisito atual |
|---|---|
| Brasão do Fio | `ToolItemManager` e menu de brasões prontos; é desbloqueado pelo plugin |
| Template do brasão | `Hunter`, preferencialmente; caso contrário, primeiro brasão existente |
| Sacrifício | Slot desbloqueado do Brasão do Fio; primeiro tenta slot não ofensivo |
| Ataque de fio | Brasão equipado e ataque para baixo no ar |
| Puxão | Inimigo válido com `HealthManager` e captura de dano/target |
| Pogo por terreno | Layer `TERRAIN`/`SOFT_TERRAIN` ou `DamageHero` com `SPIKES`/`COAL_SPIKES` |
| Conversão Sacrifício | Brasão e item equipados; hazard não excluído; cooldown livre |

### Custos e dano

| Ação | Dano/custo implementado |
|---|---|
| Primeiro golpe do fio | Dano real do `Downspike`/`DamageEnemies` nativo; não é constante do mod |
| Segundo golpe do fio | Mesmo `DamageDealt` positivo do primeiro `HitInstance`; `ApplyExtraDamage` uma vez |
| Pogo no terreno/espinho | Pogo nativo; sem segundo golpe |
| Puxão | Sem custo de seda explícito |
| Invulnerabilidade do puxão | `1,0 s` em tempo não escalado |
| Conversão Sacrifício | Normalmente `1` ponto de dano; com `health <= 1`, dano alterado `0` |
| Primeira conversão fora da janela | `0` seda |
| Conversão até `2,5 s` da anterior | `1` seda, se disponível |
| Cooldown de conversão | `0,6 s`; chamada repetida é bloqueada |
| Equipamento do item | Sem custo; item não contado e sem estoque próprio |

Os números de vida, dano base do ferrão, capacidade de seda e eventuais efeitos adicionais continuam sendo responsabilidade do jogo e do save. O módulo não altera permanentemente vida máxima, capacidade de seda ou dano base.

## 9. Persistência e patches de carregamento

A inicialização separa os patches de save porque eles são opcionais. As quatro classes estão em [src/SilkThreadSavePatches.cs](../src/SilkThreadSavePatches.cs):

- `SilkThreadSerializeSavePatch`;
- `SilkThreadLoadSavePatch`;
- `SilkThreadLoadTypedSavePatch`;
- `SilkThreadNewGamePatch`.

### Gravação

[`SilkThreadSerializeSavePatch.TargetMethod`](../src/SilkThreadSavePatches.cs#L154-L167) fecha explicitamente `GameManager.GetJson<SaveGameData>`. O comentário do código explica que o Harmony não é confiável ao receber a definição genérica aberta; por isso o patch mira o método fechado usado pelo caminho de gravação do jogo.

No postfix, [`SilkThreadSaveData.InjectIntoSaveJson`](../src/SilkThreadSavePatches.cs#L20-L34) adiciona a propriedade JSON raiz `SilkThreadCrest`, preenchida por `EnemySummonRegistry.ExportSaveData()`.

Esse bloco é para **progressão das invocações**, não para serializar a definição do Brasão do Fio ou do item Sacrifício. O registro/desbloqueio dos itens é refeito por `TryRegisterCrest`/`TryRegisterSacrifice`, enquanto o equipamento é tratado pelo sistema de itens do jogo.

### Carregamento pelas duas rotas do jogo

- `SilkThreadLoadSavePatch` intercepta `SetLoadedGameData(string, int)` e chama `RestoreFromSaveJson`.
- `SilkThreadLoadTypedSavePatch` intercepta `SetLoadedGameData(SaveGameData, int)` e usa diretamente o slot recebido para procurar o fallback.

`RestoreFromSaveJson` primeiro tenta o bloco nativo `SilkThreadCrest`. Se ele estiver ausente, vazio ou inválido, tenta o sidecar. O fallback usa o caminho lógico:

```text
Application.persistentDataPath/SilkThreadCrest/summons-slot-N.json
```

Se nenhum dado puder ser restaurado, o registro de invocações é limpo. Em `StartNewGame`, o patch limpa o sidecar do slot e chama `EnemySummonRegistry.RestoreFromSaveData(null)`.

### Limite de falha

Uma falha ao encontrar um método de save ou ao escrever/ler o sidecar não impede a inicialização do combate/brasão. Ela reduz a persistência disponível. O código registra avisos para a falha; não há tentativa de esconder esse estado.

## 10. Configuração

A única configuração criada por [`Awake`](../src/SilkThreadCrestPlugin.cs#L33-L46) é:

```text
Seção: Appearance
Chave: MenuShortcut
Padrão: F8
Descrição: Abre o menu de presets e skins visuais do Silk Thread Crest.
```

O arquivo de configuração do BepInEx fica, de forma relativa à instalação do jogo, em `BepInEx/config/manus.silkthreadcrest.cfg`.

**Não existem atualmente opções configuráveis para:**

- `SpearSpeed`, `SpearRadius`, `SpearMaxDistance`;
- velocidade/tempo/distância do puxão;
- duração da invulnerabilidade;
- dano ou custo de Sacrifício;
- cooldown de `0,6 s`;
- janela consecutiva de `2,5 s`;
- custo consecutivo de 1 seda;
- seleção de hazards;
- escolha do template do brasão.

Alterar esses valores exige mudança de código e nova compilação; não é uma alteração suportada pelo arquivo `.cfg`.

## 11. Código versus mecânica descrita

| Assunto | Mecânica descrita para o jogador | O que o código realmente garante |
|---|---|---|
| Brasão | Brasão experimental que prende o ferrão por seda | Clone de `Hunter` ou do primeiro brasão; efeito especial é aplicado por Harmony |
| Primeiro golpe | Ferrão desce e atinge o alvo | `Downspike` nativo + `DamageEnemies.TryDoDamage` quando há damager |
| Segundo golpe | Hornet chega ao inimigo e golpeia de novo | Reutiliza `HitInstance` positivo; pula se o hit não foi capturado ou alvo morreu |
| Invulnerabilidade | Proteção durante o puxão | Token de invulnerabilidade por 1 s não escalado; pode sobreviver ao fim do movimento até expirar |
| Pogo | Terreno/espinho gera pogo normal | Somente layers/tipos de hazard reconhecidos pelo cast; chama bounce nativo com flag de bypass |
| Sacrifício | Converte perigos ambientais e evita morte em 1 HP | Exclui apenas `ENEMY`, `NON_HAZARD` e `EXPLOSION`; outros tipos passam pela regra |
| Uso consecutivo | Novo uso próximo custa seda | Janela é `<= 2,5 s` desde uma conversão confirmada; sem seda, mantém dano nativo |
| Ícone | Item próprio | Placeholder clonado de outro `ToolItemBasic` |
| Visual do fio | Fio/ferrão com arte própria | Dois `LineRenderer` com geometria e cores fixas |
| Configuração | Menu visual ajustável | Apenas atalho `Appearance/MenuShortcut`; números da mecânica não são configuráveis |

## 12. Status de implementação e evidência

### Implementado no código atual

- Registro automático e desbloqueio de `SilkThread`.
- Herança de configuração do template preferencial `Hunter`.
- Registro automático de `SilkThreadSacrifice` em slot compatível.
- Localização do brasão, do item e dos prompts relacionados.
- Espera pelo carregamento de `ToolItemManager`, `PlayerData` e `InventoryToolCrestList`.
- Instalação isolada dos patches Harmony, incluindo as rotas opcionais de save.
- Movimento descendente, cast estreito, terreno/espinhos, captura de alvo e puxão.
- Invulnerabilidade temporária durante o puxão.
- Segundo golpe com o mesmo dano do primeiro `HitInstance`, quando capturado.
- Pogo curto do fio sem altura extra e pogo nativo normal do Sacrifício.
- Cooldown de `0,6 s`, janela consecutiva de `2,5 s` e custo repetido de 1 seda.
- Serialização/restauração da progressão de invocações no save nativo e em sidecar de slot.
- Configuração do atalho do menu visual.

### Evidência/validação fornecida para esta base

- A versão de referência é **0.7.25**.
- O funcionamento do combate foi confirmado pelo usuário.
- A persistência das invocações no save foi confirmada na **0.7.15**; o código atual mantém patches para as duas rotas de carregamento e para a gravação JSON fechada.
- O menu e as paletas foram confirmados na **0.7.24**.
- A **Silk Eclipse** aplicada em 0.7.24 tinha tamanho/animações incorretos; em 0.7.25 o sync foi corrigido, a build foi compilada e foram executados **30 testes do seletor**. A aplicação visual da Silk Eclipse ainda **não foi confirmada dentro do jogo**.
- A compilação é feita contra as assemblies recebidas. O jogo Windows não é executado no sandbox.

Essas evidências não substituem testes de cada inimigo, hazard ou cenário com a build 0.7.25.

## 13. Áreas ainda não testadas e limites

- Não foi possível executar Silksong Windows neste ambiente; a apresentação final do fio, do ferrão, do ícone e da Silk Eclipse depende de validação in-game.
- A sincronização de tamanho/animação da Silk Eclipse foi corrigida e coberta pelos testes do seletor, mas ainda não foi confirmada em jogo.
- O comportamento depende dos nomes, layers e componentes reais dos inimigos. Variantes sem `HealthManager`, com hitboxes incomuns ou com `DamageHero` atípico podem não ser capturadas como esperado.
- O filtro de espinhos cobre `SPIKES` e `COAL_SPIKES`; outros hazards visuais podem não acionar o pogo do fio.
- O segundo golpe usa `IgnoreInvulnerable = true`; isso deve ser testado contra inimigos com fases, invulnerabilidade temporária ou transições especiais.
- O dano ambiental externo ao duelo por turnos é uma preocupação de integração do sistema de combate; esta documentação não promete que hazards de terceiros sejam convertidos quando outra rotina bloquear `TakeDamage`.
- O uso de reflexão sobre campos privados (`currentDownspike`, `crestList`, `toolItems` e campos do clone) pode falhar se as assemblies mudarem. Em alguns casos o código registra erro e tenta novamente; em outros, aplica fallback nativo.
- Multi-hit, callbacks atrasados e objetos que tenham vários `HealthManager`/colliders podem produzir resultados diferentes do fluxo de um único hit. O estado do movimento foi desenhado em torno de um alvo e de um `HitInstance` inicial.
- O cooldown do Sacrifício não possui indicador visual nem fila de dano; hits durante `0,6 s` são simplesmente bloqueados pelo prefixo.
- A comparação de vida mínima usa `PlayerData.health <= 1`, enquanto a verificação de perda de vida usa `health + healthBlue`; máscaras azuis e estados limítrofes merecem teste específico.

## 14. Riscos e propostas futuras

As propostas a seguir são **trabalho futuro**, não funcionalidades já presentes:

1. **Tornar parâmetros configuráveis:** expor em BepInEx a velocidade/alcance do fio, puxão, invulnerabilidade, cooldown, janela consecutiva e custo de seda.
2. **Lista explícita de hazards:** substituir a regra “qualquer tipo não excluído” por uma whitelist de hazards ambientais suportados, reduzindo conversões acidentais.
3. **Política de invulnerabilidade:** decidir se o segundo golpe deve respeitar `IsInvincible`/condições de fase em vez de sempre marcar `IgnoreInvulnerable = true`.
4. **Estado visual do Sacrifício:** exibir cooldown e custo da próxima conversão para o jogador, sem alterar a resolução nativa.
5. **Arte final:** substituir `LineRenderer` e o ícone copiado por assets próprios, mantendo o fallback caso o recurso visual não carregue.
6. **Robustez de colisão:** testar inimigos com múltiplos colliders, múltiplos callbacks e attacks que não entregam um `HitInstance` positivo ao `OnHitResponded`.
7. **Compatibilidade de assemblies:** adicionar diagnósticos explícitos para cada campo privado ausente, em vez de depender do `SetField` silencioso.
8. **Matriz de testes in-game:** validar no Windows o ataque em terreno, terreno macio, ambos os tipos de espinho, inimigos comuns, inimigos invulneráveis, vida 1, vida com máscaras azuis, falta de seda no uso consecutivo e troca de cena/save.

Essas propostas devem ser avaliadas separadamente para não serem confundidas com a implementação da base `0.7.25`.
