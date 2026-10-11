# Arquitetura e mapa de desenvolvimento

O plugin é um conjunto de componentes Unity e patches Harmony. Não existe um servidor, banco de dados remoto ou framework de UI adicional. A DLL é `net472`; a interface é desenhada via IMGUI e os visuais nativos usam tk2d. O projeto depende das assemblies da instalação do jogo/BepInEx, que não são distribuídas aqui.

## Mapa de arquivos

| Arquivo | Responsabilidade | Documentação |
|---|---|---|
| [`SilkThreadCrestPlugin.cs`](../src/SilkThreadCrestPlugin.cs) | GUID/versão, config, instalação de patches, espera managers, registro/clonagem brasão/item, localização; patches fio/pogo/Sacrifício | [Brasão e Sacrifício](BRASAO-E-SACRIFICIO.md) |
| [`SilkThreadMove.cs`](../src/SilkThreadMove.cs) | Cast descendente, captura primeiro hit, puxão, invulnerabilidade temporária, segundo dano e fio visual | [Brasão e Sacrifício](BRASAO-E-SACRIFICIO.md) |
| [`TurnBattleSystem.cs`](../src/TurnBattleSystem.cs) | Máquina do duelo, input, ações, menus, HUD, dano, gates, invocações, drops e restauração | [Combate e balanceamento](COMBATE-E-BALANCEAMENTO.md) |
| [`CrestSynergy.cs`](../src/CrestSynergy.cs) | Custos efetivos e bônus de geração/recompensa por aliases de brasões/ferramentas | [Combate e balanceamento](COMBATE-E-BALANCEAMENTO.md#6-sinergias-de-brasão-e-ferramentas) |
| [`EnemySummonRegistry.cs`](../src/EnemySummonRegistry.cs) | Registro estático, snapshot/commit, catálogo/blacklist, ordenação, export/import e visuais arquivados | [Invocações e save](INVOCACOES-E-SAVE.md) |
| [`SilkThreadSavePatches.cs`](../src/SilkThreadSavePatches.cs) | JSON nativo, sidecar por slot e patches dos overloads de carregamento/new game | [Invocações e save](INVOCACOES-E-SAVE.md#8-save-nativo-sidecar-e-caminhos-por-slot) |
| [`SilkThreadAppearanceMenu.cs`](../src/SilkThreadAppearanceMenu.cs) | F8/cursor/theme, presets/config, paletas por atlas, packs, backup/restore, reaplicação após cena | [Personalização e assets](PERSONALIZACAO-E-ASSETS.md) |
| [`SilkThreadMaterialBinding.cs`](../src/SilkThreadMaterialBinding.cs) | Resolve coleção tk2d de runtime; captura, liga e restaura `material`/`materialInst` por frame | [Personalização e assets](PERSONALIZACAO-E-ASSETS.md#4-material-por-frame-e-coleção-de-runtime) |
| [`SilkThreadSkinAssetReader.cs`](../src/SilkThreadSkinAssetReader.cs) | Prioridade de PNG externo e recurso embutido, sem depender de runtime Unity | [Personalização e assets](PERSONALIZACAO-E-ASSETS.md#6-recurso-embutido-e-externo) |
| [`SilkThreadEclipsePose.cs`](../src/SilkThreadEclipsePose.cs) | Seleção determinística de oito poses por nome de clip/frame; `-1` para arte nativa | [Personalização e assets](PERSONALIZACAO-E-ASSETS.md#7-silk-eclipse) |
| [`SilkThreadEclipseVisual.cs`](../src/SilkThreadEclipseVisual.cs) | Folha 4×2, alpha crop, sprites/pivôs, escala Idle, direção, visual corporal e fallback | [Personalização e assets](PERSONALIZACAO-E-ASSETS.md#7-silk-eclipse) |
| [`SilkThreadCrest.csproj`](../src/SilkThreadCrest.csproj) | Target, referências locais, recurso PNG embutido | [Contribuição](../CONTRIBUTING.md) |

## Fluxos principais

```text
BepInEx -> Plugin.Awake
  -> menu visual opcional (falha isolada)
  -> patches de gameplay individualmente
  -> patches opcionais de save
Plugin.Update -> aguarda PlayerData/ToolItemManager/InventoryToolCrestList
  -> cria/desbloqueia brasão + Sacrifício
  -> anexa TurnBattleSystem à Hornet

TAUNT + chão + alvo próximo -> StartBattle
  -> captura física/brasão/hp/snapshot da invocação
  -> Preparing -> PlayerTurn
  -> ação / seletor brasão / seletor invocação
  -> PlayerAction -> EnemyAction -> PlayerTurn
  -> vitória/derrota/recuo -> Result -> EndBattle (restauração)

Morte do alvo -> ObserveEnemyDeath / BeginResult
  -> Commit invocação -> sidecar + QueueSaveGame
  -> JSON nativo recebe bloco SilkThreadCrest
Load -> patches string/tipado -> restaura catálogo
  -> reidratação visual quando um inimigo correspondente aparece

F8 -> menu/cursor -> preset -> restaura estado anterior
  -> paleta: clona atlas/material, pinta regiões e liga materiais a frames
  -> pack: PNG por nome, mesmo binding de materiais
  -> Eclipse: folha própria, segue animator real, oculta só corpo em poses cobertas
```

## Estados do combate

`Idle`, `Preparing`, `PlayerTurn`, `CrestMenu`, `SummonMenu`, `PlayerAction`, `EnemyAction`, `Result`. Os detalhes das durações/condições estão em [Combate](COMBATE-E-BALANCEAMENTO.md#2-máquina-de-estados-e-ciclo-do-duelo). A janela visual não equivale a um scheduler de animação completo: ações prolongadas, eventos tardios e multi-hit precisam de política explícita antes de ampliar o sistema.

## Limites de separação atuais

A apresentação está concentrada no menu visual e nos helpers, mas `TurnBattleSystem.cs` ainda é grande e reúne lógica, GUI e patches. Dados de invocação e templates Unity coexistem no registro, porém só os dados simples são serializados. Fio/Sacrifício compartilham o plugin com registro e patches de runtime.

Uma refatoração futura pode separar domínio do combate, resolução de recursos/dano, apresentação e adapters Unity; deve preservar comportamento por testes/diff e não ser misturada com um patch urgente. Atualmente não existe abstração completa de um inimigo ou de uma ferramenta: o sistema se apoia em componentes reais, nomes e `HitInstance`.

## Pontos de extensão e cuidados

| Pedido | Onde começar | Verificação crítica |
|---|---|---|
| Nova ação/custo | `BuildMoves`, `BeginPlayerMove`, `FinishPlayerAction`, `CrestSynergy` | Cancelamento, cobrança única, fallback, consumo de turno |
| Balanceamento de invocação | `CollectAttacks`, `BeginSummonAttack`, `FinishSummonAction` | Dano nativo vs capturado, invulnerabilidade, seda/turno |
| Novo filtro de inimigo | `IsSafeAttackSource`, `IsPassiveDamageComponent`, `MakeKey` | Não excluir ataque legítimo nem catalogar contato/passiva |
| Save/migração | DTOs registro, `ExportSaveData`, `RestoreFromSaveData`, patches | Todos os slots, new game, corrupções, rollback e backup |
| Ferramenta/passiva | `BuildMoves`, `TryStartNativeSkill`, `ShouldSuppressLimitedToolDamage` | Estoque real, RepresentingTool, multihit, ativo sem dano |
| HUD | métodos `Draw...` / `OnGUI` | Sem mudar dano/estado; bounds/resolução e GUI matrix |
| Paleta precisa | `RecolorRegion`, máscara/manual futuro | Não usar tint global em sprite corporal inteiro |
| Nova skin completa | packs PNG + metadados futuros/atlas | Frames/UV/dimensões/pivôs nativos, não só oito desenhos |
| F8/cursor | menu `Update`, `LateUpdate`, `OnGUI`, `CloseMenu` | Focus/lock restaurado e concorrência com pause/batalha |

## Reflexão e patches

Os nomes/assinaturas precisam ser verificados nas DLLs locais. Os patches de save miram o método **fechado** `GetJson<SaveGameData>` e duas rotas `SetLoadedGameData`; substituir por um método genérico aberto pode instalar incorretamente ou falhar. O plugin isola falhas por classe para não impedir o combate, mas isso não garante que todas as funcionalidades permanecem ativas: logs são necessários.

No tk2d, o frame usa `collectionInst.spriteDefinitions[id].materialInst`; o renderer pode voltar a ele no próximo refresh. Por isso o helper guarda ambos `material` e `materialInst` e restaura suas referências exatamente. Os testes exercitam esse contrato por stubs, não a renderização GPU.

Não clonar personagens para HUD. Nas invocações, os clones existentes são sanitizados para visual; a instanciação pode disparar callbacks antes de remover componentes, o que merece auditoria dedicada. Combate/vida/dano devem sempre atingir objetos reais do duelo.
