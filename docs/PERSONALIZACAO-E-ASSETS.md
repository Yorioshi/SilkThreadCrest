# Personalização e assets

> **Módulo documentado:** personalização visual, paletas, packs de skins e **Silk Eclipse**.
> **Base do documento:** código atual 0.7.25. O código em [`src/`](../src/) é a autoridade; o README e os changelogs servem apenas como referência de histórico e de validação.

Este módulo altera a apresentação de Hornet em runtime. Ele não cria hitboxes, não troca a animação de gameplay do jogo, não altera dano, seda, input, save ou regras do combate. A Silk Eclipse é uma camada visual própria com oito poses; **não é uma nova animação completa frame a frame**.

## Status de implementação e evidência

| Item | Estado conhecido |
|---|---|
| Menu F8, presets e paletas | **0.7.24 confirmado pelo usuário em jogo.** Mantido na base 0.7.25. |
| Binding de materiais de runtime e `materialInst` por frame | Implementado em [`SilkThreadMaterialBinding.cs`](../src/SilkThreadMaterialBinding.cs), com testes isolados de referências, restauração e recurso embutido/externo. |
| Silk Eclipse em 0.7.24 | Foi aplicada, mas apresentava tamanho e animações incorretos. |
| Silk Eclipse em 0.7.25 | Sincronização e calibração foram corrigidas no código; a versão foi compilada e o seletor teve **30 testes isolados**. **Ainda não foi confirmada in-game.** |
| Execução do jogo Windows | Não é executada no sandbox. A compilação é sempre contra as assemblies recebidas para a versão local do jogo. |

A evidência isolada não equivale a renderização real. Os testes em [`tests/eclipse-poses/PoseTests.cs`](../tests/eclipse-poses/PoseTests.cs) validam a escolha de pose; os testes em [`tests/material-binding/BindingTests.cs`](../tests/material-binding/BindingTests.cs) usam modelos mínimos para validar referências e prioridade de recurso, não uma cena Unity real.

---

## 1. Como usar

### 1.1 Abrir o menu

1. Instale a DLL normalmente na pasta do plugin do BepInEx.
2. Inicie o jogo e aguarde Hornet existir.
3. Pressione **F8** fora de uma batalha.
4. Escolha um preset. A aplicação acontece imediatamente; em uma cena ainda montando Hornet, o módulo tenta aplicar novamente.
5. Pressione F8 outra vez ou **FECHAR** para fechar. O estado anterior de `Cursor.visible` e `Cursor.lockState` é restaurado.

O atalho é registrado em [`SilkThreadCrestPlugin.cs`](../src/SilkThreadCrestPlugin.cs), em `Awake`, com:

```text
Seção: Appearance
Chave: MenuShortcut
Padrão: F8
```

O valor vem de `Config.Bind("Appearance", "MenuShortcut", new KeyboardShortcut(KeyCode.F8), ...)`. O arquivo de configuração instalado pelo BepInEx é `BepInEx/config/manus.silkthreadcrest.cfg`. A entrada é lida na inicialização do plugin; reinicie o jogo depois de alterá-la.

O menu é bloqueado enquanto `TurnBattleSystem.IsBattleActive` é verdadeiro. Isso evita que o cursor e a GUI visual concorram com o combate.

`EnsureGuiStyles()` cria o tema do menu somente no primeiro `OnGUI` real: janela e painéis quase pretos (`0,015/0,015/0,02`, alpha `0,98`), acento vermelho (`0,86/0,035/0,08`), texto branco suave (`0,94/0,94/0,92`), botão selecionado invertido e realce vermelho no hover/estado ativo. A janela fica limitada a **420–720 px** de largura e **440–760 px** de altura, respeitando a resolução disponível. O tema é apenas da GUI do customizador; ele não substitui globalmente o HUD nativo.

### 1.2 Presets incluídos

`ScanPresets()` em [`SilkThreadAppearanceMenu.cs`](../src/SilkThreadAppearanceMenu.cs) sempre lista:

- `Original` — restaura os materiais, texturas, cores e renderers que estavam ativos antes da aplicação;
- `Silk Eclipse` — camada de oito poses descrita na seção [Silk Eclipse](#7-silk-eclipse);
- `Carmim`, `Vazio`, `Seda` e `Ocre` — paletas prontas;
- `Paleta personalizada` — três grupos independentes: **Capa**, **Cabeça** e **Sob a capa**;
- uma entrada para cada subpasta encontrada em `Skins/`, exceto nomes já usados pelos presets.

A raiz é a pasta da DLL realmente carregada: `Initialize()` usa `Path.GetDirectoryName(typeof(SilkThreadCrestPlugin).Assembly.Location)` e cria a subpasta `Skins` ao inicializar. Portanto, packs devem ficar em:

```text
BepInEx/plugins/SilkThreadCrest/Skins/NomeDaSkin/
```

O estado selecionado e as cores são persistidos em `appearance.json`, ao lado da DLL. O JSON contém `preset`, os campos legados `r/g/b` e os grupos atuais `capeR/G/B`, `headR/G/B` e `underCloakR/G/B`. A leitura também aceita `bodyR/G/B` como compatibilidade para o grupo antigo “corpo”. Um JSON inválido faz o módulo voltar a `Original` sem impedir o restante do plugin.

### 1.3 Aplicar uma paleta

- Selecione `Carmim`, `Vazio`, `Seda` ou `Ocre` para aplicar uma cor aos três grupos.
- Selecione `Paleta personalizada` para editar cada parte separadamente.
- Cada canal usa slider de **0 a 1** e exibe o valor arredondado de **0 a 255**.
- Clique em **APLICAR PALETA** para confirmar os valores atuais.
- Use **RESTAURAR ORIGINAL** para destruir clones temporários e retornar às referências originais.

Os valores internos exatos dos presets são:

| Preset | `R` | `G` | `B` | Aproximação 8-bit exibida |
|---|---:|---:|---:|---:|
| Carmim | 0,78 | 0,16 | 0,18 | 199, 41, 46 |
| Vazio | 0,28 | 0,20 | 0,46 | 71, 51, 117 |
| Seda | 0,92 | 0,34 | 0,66 | 235, 87, 168 |
| Ocre | 0,92 | 0,62 | 0,16 | 235, 158, 41 |

A cor sempre é gravada com alpha `1`. Esse módulo não cobra seda, não consome turno e não altera qualquer valor de gameplay; “custo” aqui é somente o custo visual de gerar clones temporários de materiais e texturas, liberados ao trocar de preset ou destruir o menu.

---

## 2. Ciclo de aplicação e restauração

O fluxo principal está em `ApplySelectedPreset()` de [`SilkThreadAppearanceMenu.cs`](../src/SilkThreadAppearanceMenu.cs):

1. chama `RestoreAllCollections()` para desfazer a aplicação anterior;
2. destrói as texturas carregadas de packs;
3. limpa a camada Silk Eclipse;
4. zera contadores de frames, renderers e pixels;
5. aplica `Original`, `Silk Eclipse`, paleta ou pack conforme o preset;
6. grava `appearance.json`.

Após uma mudança de cena, `OnSceneLoaded()` agenda nova aplicação em aproximadamente **0,35 s**. Se um preset não original não encontrou Hornet ainda, `Update()` repete a tentativa a cada aproximadamente **0,75 s**. Para paletas e packs, `LateUpdate()` também procura coleções novas a cada aproximadamente **0,5 s** enquanto há backups.

Cada coleção de runtime recebe um único `CollectionBackup`, que guarda:

- `collection.materials`, `collection.materialInsts` e `collection.material`;
- referências de `material` e `materialInst` de cada definição de sprite;
- materiais clonados e texturas clonadas;
- cores originais de sprites usados no fallback;
- `sharedMaterial` original dos renderers atualizados.

Assim, trocar de preset não acumula clones. `RestoreAllCollections()` restaura primeiro os campos de cada frame através de `SilkThreadMaterialBinding.Restore()`, depois os arrays da coleção, os renderers e as cores dos sprites.

---

## 3. Paletas: máscaras aproximadas e fallback por nome

### 3.1 O que é e o que não é uma máscara

A paleta personalizada **não possui máscaras manuais desenhadas por frame**. `ApplyPartTexturePalette()` lê os pixels dos atlases da coleção e classifica cada pixel pela cor original:

- tecido vermelho tende a ser **Capa**;
- pixels claros e neutros tendem a ser **Cabeça**;
- outros pixels visíveis, fora desses grupos, tendem a ser **Sob a capa**;
- contornos pretos e olhos muito escuros são preservados.

Essa classificação é uma aproximação cromática, não uma segmentação anatômica. Um acessório que tenha a mesma cor da capa pode ser recolorido junto; uma parte com cor fora dos limiares pode permanecer original. Não existe no código atual um arquivo de máscara por frame, uma pintura de regiões ou uma edição de máscara dentro do menu.

### 3.2 Pipeline por atlas e região UV

Para cada coleção de Hornet encontrada:

1. `SaveBackup()` captura as referências atuais.
2. `CloneMaterials()` cria um `Material` novo para cada material não nulo.
3. Cada `tk2dSpriteDefinition` encontra a textura de `materialInst`, `material` ou do `materialId` original.
4. `GetDefinitionRegion()` converte os `uvs` em um retângulo de pixels. Se não há UV, usa `extractRegion`, `regionX`, `regionY`, `regionW` e `regionH`.
5. Cada textura original é lida uma vez. Regiões repetidas são ignoradas por uma chave `x:y:width:height`.
6. A textura inteira é clonada, os pixels da região são modificados e o clone é atribuído ao material correspondente.
7. `SilkThreadMaterialBinding.Bind()` liga o material novo em cada frame e nos arrays da coleção.
8. `RefreshActiveSprites()` atualiza o material e as cores dos sprites que estão na coleção de runtime.

A leitura tenta primeiro `Texture2D.GetPixels32()`. Se a textura não for legível, `ReadTexturePixels()` usa um `RenderTexture`, `Graphics.Blit()` e `ReadPixels()`. Se ambas as tentativas falharem, aquela região não recebe recoloração por pixels; o código não pinta o personagem inteiro como substituição silenciosa.

### 3.3 Fórmula exata da máscara e da tonalização

Em `RecolorRegion()` (`SilkThreadAppearanceMenu.cs`), pixels com `alpha <= 8` são ignorados. Para um pixel `source` em 8-bit:

```text
cloth = source.r > source.g × 1,25
     E source.r > source.b × 1,12
     E source.r > 24

high = max(source.r, source.g, source.b)
low  = min(source.r, source.g, source.b)
head = high > 110 E (high - low) < 85
```

A ordem é importante: `cloth` é testado antes de `head`. Se não for tecido nem cabeça e `high <= 12`, o pixel é preservado como contorno/olho. Nos demais casos, o alvo é `capeTint`, `headTint` ou `underCloakTint`.

`TintPixel()` mantém a luminosidade aproximada da arte original:

```text
luminance = clamp01(0,299 × R + 0,587 × G + 0,114 × B)
tintPeak  = max(tint.r, tint.g, tint.b)
brightness = clamp01(max(0,2, luminance) / max(0,001, tintPeak) × 1,15)
replacement = tint × brightness
result = Lerp(original, replacement, 0,82)
result.a = original.a
```

Há uma exceção: se os três canais da tinta forem maiores que `0,995`, o pixel original volta sem alteração. Isso faz o preset branco funcionar como restauração de aparência dentro de uma aplicação, embora `Original` continue sendo o caminho correto para restaurar referências e clones.

### 3.4 Fallback por sprite

Quando a textura não produz uma coleção com pixels modificados, `ApplyPalette()` tenta um fallback explícito em `tk2dBaseSprite.color`. O identificador analisado por `GetSpriteIdentity()` combina nome do sprite, nome do GameObject, nome da definição atual e até seis pais.

Antes de classificar, `IsCharacterSprite()` exclui identidades contendo `hitbox`, `collider`, `trail`, `effect` ou `shadow`. Depois, `ClassifySprite()` usa termos:

- **Cabeça:** `head`, `mask`, `face`, `hair`, `hornethead`;
- **Capa:** `cape`, `cloak`, `mantle`, `shawl`, `skirt`, `backcloth`;
- **Sob a capa:** `undercloak`, `body`, `torso`, `chest`, `arm`, `leg`, `hand`, `foot`, `needle`, `weapon`, `undersuit`, `shirt`, `tunic`.

Sprites sem identificador reconhecível permanecem com a cor original. Se uma coleção já recebeu uma textura derivada, seus sprites não recebem também o fallback por cor, evitando aplicar duas tonalizações. O fallback é, portanto, útil para partes que não puderam ser recoloridas por atlas, mas depende da nomenclatura do objeto.

---

## 4. Material por frame e coleção de runtime

O detalhe crítico está em [`SilkThreadMaterialBinding.cs`](../src/SilkThreadMaterialBinding.cs). O `tk2dSprite.UpdateMaterial` lê `collectionInst.spriteDefinitions[id].materialInst`; trocar somente `collection.materials`, `collection.materialInsts` ou `Renderer.sharedMaterial` pode ser sobrescrito pelo próximo refresh do tk2d.

### 4.1 Qual coleção é usada

`RuntimeCollection(tk2dBaseSprite sprite)` procura o campo `collectionInst` por reflection. Se existir, essa coleção de runtime tem prioridade. Caso contrário, usa `sprite.Collection.inst`. `FindHornetCollections()` parte dos `tk2dBaseSprite` sob `HeroController.instance` e reúne essas coleções, em vez de processar cegamente todas as coleções cujo nome contenha “Hornet”.

A busca usa `GetComponentsInChildren<tk2dBaseSprite>(true)`, portanto inclui objetos inativos da hierarquia. A contagem de binding visível, porém, só ocorre quando o `Renderer` está habilitado e o GameObject está ativo na hierarquia.

### 4.2 Captura, binding e restauração

`Capture(collection)` cria um `FrameSnapshot` para cada definição não nula, guardando:

```text
Definition
Material     = definition.material
MaterialInst = definition.materialInst
```

`Bind(collection, replacements, snapshots, originals)` resolve o material de cada frame pelo `materialId`. Se o índice for inválido, usa `FindMaterialIndex()` para comparar as referências capturadas de `materialInst` e `material` com os materiais originais. Se não houver correspondência, o frame não é ligado a um atlas arbitrário.

Quando encontra uma substituição válida, o binding faz ambos:

```text
definition.material    = replacements[index]
definition.materialInst = replacements[index]
```

Em seguida atualiza:

```text
collection.materials    = replacements
collection.materialInsts = replacements
collection.material     = replacements[0]   (quando há materiais)
```

`Restore()` devolve exatamente as referências capturadas de cada frame. Esse vínculo por frame/coleção de runtime é o que permite que o próximo `UpdateMaterial` do tk2d continue selecionando o material recolorido, em vez de desfazê-lo.

### 4.3 Atualização dos sprites ativos

`RefreshActiveSprites()` escolhe `collection.materialInsts` (ou `materials`), atribui o material correspondente a `renderer.sharedMaterial` e invoca o `UpdateMaterial` concreto do tipo de sprite por reflection. Também invoca `UpdateColors` para que alterações de cor tenham efeito imediato.

Os contadores do menu/log são diagnósticos, não garantia de renderização:

- `frames`: quantas definições foram ligadas;
- `active renderers`: renderers habilitados e ativos cuja textura coincide com o `materialInst` novo;
- `changed pixels`: quantos pixels tiveram RGB diferente na tonalização.

---

## 5. Packs e compatibilidade parcial com CustomKnight Creator

### 5.1 Formato aceito

O módulo aceita a saída PNG do botão **Pack** do CustomKnight Creator. O packer externo continua sendo responsável por editar e empacotar a arte; o menu só indexa PNGs e os aplica em runtime. O fluxo básico é:

```text
Skins/
└── MinhaSkin/
    ├── atlas-1.png
    ├── atlas-2.png
    └── subpasta/atlas-3.png
```

Abra F8, clique em **RECARREGAR PACKS** se o jogo já estava aberto e selecione `MinhaSkin`.

`LoadPackedTextures()` percorre `*.png` recursivamente, decodifica por `UnityEngine.ImageConversion.LoadImage`, define `FilterMode.Point` e `TextureWrapMode.Clamp`, e indexa cada arquivo pelo nome sem extensão. Um PNG ilegível é ignorado com aviso; o preset ainda pode ser listado, mas não aplicará aquela textura.

### 5.2 Como o nome do atlas é associado

Para cada material de cada coleção de runtime, `FindReplacementTexture()` compara o nome do PNG com:

- `collection.name`;
- `collection.spriteCollectionName`;
- `material.mainTexture.name`.

`NamesMatch()` remove tudo que não for letra ou número (`NormalizeName`) e aceita:

1. igualdade sem distinção entre maiúsculas/minúsculas;
2. o nome do atlas contido no nome do jogo, se o atlas tiver mais de três caracteres;
3. o nome do jogo contido no atlas, se o nome do jogo tiver mais de três caracteres.

A textura encontrada substitui apenas `Material.mainTexture`; depois o material é ligado a cada definição através do mesmo caminho `material` + `materialInst` descrito acima.

### 5.3 O que “compatibilidade parcial” significa

É compatível com a ideia de atlas PNG empacotado e com nomes de coleção que possam ser associados. Não é um importador completo do projeto CustomKnight e não lê um arquivo de projeto, uma configuração de poses ou metadados de pivô. Em particular, o código atual não:

- valida se largura/altura, recortes e quantidade de frames correspondem ao atlas nativo;
- remapeia UVs ou `spriteDefinitions` para uma ordem nova;
- troca clips `tk2dSpriteAnimator` ou cria animações novas;
- importa pivôs, colisões, meshes, shaders ou propriedades de material além de `mainTexture`;
- oferece máscaras manuais por parte;
- garante que todos os atlases do jogo encontrem um nome compatível.

O nome sem extensão é a chave. Dois PNGs com o mesmo basename podem disputar a mesma entrada; ao carregar uma entrada repetida, a anterior é destruída e substituída. Um pack com nomes ambíguos pode casar com uma coleção inesperada por causa do matching por substring. Dimensões ou pivôs incompatíveis podem gerar arte deslocada ou esticada, sem um erro de compatibilidade antecipado.

---

## 6. Recurso embutido e externo

A leitura comum está em [`SilkThreadSkinAssetReader.cs`](../src/SilkThreadSkinAssetReader.cs), no método `Read(externalPath, assembly, resourceName, out embedded)`:

1. se `File.Exists(externalPath)` for verdadeiro, lê o arquivo externo e retorna `embedded = false`;
2. caso contrário, chama `assembly.GetManifestResourceStream(resourceName)`;
3. se o recurso existir, copia o stream para memória e retorna `embedded = true`;
4. se não existir, lança `FileNotFoundException` indicando o recurso ausente.

Para a Silk Eclipse, `LoadSheet()` tenta primeiro:

```text
Skins/SilkEclipse/Texture2D/silk_eclipse_sheet.png
```

Se o arquivo externo não existir, usa o recurso com nome lógico:

```text
SilkThreadCrest.Skins.SilkEclipse.Texture2D.silk_eclipse_sheet.png
```

O vínculo do PNG na DLL está em [`src/SilkThreadCrest.csproj`](../src/SilkThreadCrest.csproj), no `EmbeddedResource` que aponta para `../Skins/SilkEclipse/Texture2D/silk_eclipse_sheet.png` e define esse `LogicalName`. Portanto, apagar a cópia externa não impede o fallback da Silk Eclipse quando a DLL foi construída com o recurso.

A prioridade é deliberada: uma cópia externa permite testar/trocar a folha sem reconstruir a DLL; o recurso embutido torna a instalação autocontida. Packs arbitrários do CustomKnight não são embutidos automaticamente: continuam sendo PNGs externos dentro de uma subpasta de `Skins`.

---

## 7. Silk Eclipse

### 7.1 Estrutura da folha

A implementação está em [`SilkThreadEclipseVisual.cs`](../src/SilkThreadEclipseVisual.cs) e a decisão de pose em [`SilkThreadEclipsePose.cs`](../src/SilkThreadEclipsePose.cs). A folha tem:

```text
Columns = 4
Rows    = 2
```

A ordem de leitura é da esquerda para a direita e de cima para baixo no índice lógico, com a linha vertical invertida para a coordenada de textura Unity (`bottom = (Rows - 1 - i / Columns) * cellHeight`). Cada célula é recortada ao retângulo de pixels com alpha maior que `8`; uma célula vazia causa falha de carregamento e preserva a arte original.

A folha atual contém oito amostras, não uma animação completa:

| Índice | Uso visual |
|---:|---|
| 0 | Idle, primeira metade do clip |
| 1 | Variação de idle, segunda metade do clip |
| 2 | Caminhada/corrida, primeira metade do clip |
| 3 | Caminhada/corrida, segunda metade do clip |
| 4 | Salto |
| 5 | Queda |
| 6 | Ataque simples, somente fase central |
| 7 | Bind, focus e cura |

A descrição de distribuição também está em [`Skins/SilkEclipse/SKIN.txt`](../Skins/SilkEclipse/SKIN.txt). Dash, parede, pogo/downspike, ataques verticais, dano, morte, ferramentas, cutscenes e outras ações não cobertas permanecem com a arte nativa.

### 7.2 Seleção pelo clip e frame tk2d

`Apply(pluginPath)` usa o animator corporal real:

```text
hero.AnimCtrl.animator
```

Não é escolhido um animator de efeito aleatório sob `HeroController`. Em cada `Tick()` o módulo lê `CurrentClip`, seu nome, `CurrentFrame` e o tamanho do clip, e passa esses valores para `SilkThreadEclipsePose.Select()`.

A regra atual é:

- nome vazio: `-1` (fallback nativo);
- nomes contendo `hurt`, `death`, `dead`, `respawn`, `bench`, `sit`, `wall`, `mantle`, `swim`, `dash`, `sprint`, `roll`, `spin`, `double jump`, `downspike`, `downslash`, `upslash`, `needle throw`, `throw`, `cutscene`, `recoil`, `charged`, `stun`, `wake`, `door`, `harpoon`, `fireball`, `umbrella`, `shuttlecock` ou `super jump`: `-1`;
- `bind`, `focus` ou `heal`: pose `7`;
- `fall`: pose `5`;
- `jump`: pose `4`;
- `attack` ou `slash`: pose `6` somente se o progresso estiver entre `0,20` e `0,70`, inclusive; antecipação e recuperação retornam `-1`;
- `walk` ou `run`: alterna entre poses `2` e `3` na metade do clip;
- `idle`, `stand` ou `rest`: alterna entre poses `0` e `1` na metade do clip;
- qualquer outro nome: `-1`.

Para clips com mais de um frame, o frame é limitado ao intervalo válido e a primeira pose é usada quando `bounded < (frameCount + 1) / 2`; caso contrário usa-se a segunda. Um clip com zero ou um frame usa a primeira pose do par.

Há ainda uma correção de estado para clips `Airborne` e `Float`: se `cState.falling`, usa pose `5`; se `cState.jumping`, usa pose `4`; caso contrário, não força uma pose. Estados corporais como morte, hazard death/respawn, cutscene movement, wall sliding/clinging, mantling, dash/backdash, sprint, lançamento de ferramenta, downspike, ataques para cima/baixo e certos casts também forçam `pose = -1` em `Tick()`.

### 7.3 Escala, tamanho, pivôs e posição

A referência nativa é capturada em `CaptureNativeReference()` a partir da definição do primeiro frame `Idle`, quando disponível. O centro local é o centro do bounding box de `definition.positions`; a altura e largura são transformadas para espaço de mundo. Se a definição não fornecer geometria utilizável, o fallback usa `bodyRenderer.bounds`.

Os oito sprites são criados com **170 PPU** e estes pivôs:

```text
0 (.37, .50)   1 (.32, .50)   2 (.31, .50)   3 (.70, .50)
4 (.30, .50)   5 (.50, .45)   6 (.30, .50)   7 (.51, .40)
```

A cobertura corporal usada para estimar a altura é:

```text
{ 1, 1, 1, 1, 1, 0.84, 1, 0.80 }
```

O `Tick()` usa a altura do primeiro frame da folha como referência fixa:

```text
scaleY = referenceWorldHeight / bodyHeights[0]
referenceArtWidth = frames[0].rect.width / 170 × 0.68
scaleX = referenceWorldWidth / referenceArtWidth
```

Se uma referência for pequena demais, o código usa `1` para `scaleY` ou reutiliza `scaleY` como fallback de `scaleX`. A posição é calculada pelo centro local da arte nativa transformado pelo `nativeSprite`, e o z do visual fica em `bodyRenderer.transform.position.z - 0,01`. A camada e a sorting layer seguem o renderer corporal; a ordem fica uma unidade acima.

O recorte da folha considera apenas alpha maior que `8`. Não existe uma máscara semântica que saiba, por si só, que um pixel é agulha, efeito ou corpo; os fatores `0,84` e `0,80`, os pivôs e o trim especial do índice `7` são ajustes artísticos do asset atual. Alinhamento perfeito de cada pose ainda depende de inspeção dentro do jogo.

### 7.4 Orientação e ocultação da arte nativa

A direção vem de `hero.cState.facingRight` quando há estado; caso contrário, de `nativeSprite.FlipX`. Para compensar a orientação diferente da pose de caminhada `3`, o código usa:

```text
visualRenderer.flipX = pose == 3 ? !facingRight : facingRight
visualRenderer.flipY = nativeSprite.FlipY
```

Enquanto uma pose válida está disponível e o renderer nativo está habilitado/ativo:

- o `SpriteRenderer` da Silk Eclipse fica habilitado;
- o `bodyRenderer.forceRenderingOff` fica `true`;
- a cor do visual acompanha `nativeSprite.color`;
- o animator nativo continua sendo a fonte de clip/frame.

Somente o renderer corporal é ocultado. Efeitos nativos continuam ativos. Se não houver pose coberta, se a arte nativa estiver invisível ou se o frame não existir, o visual da Silk Eclipse é desabilitado e `RestoreRendering()` devolve a flag anterior do renderer corporal. Ao trocar para `Original`, a rotina `Clear()` também destrói objeto visual, sprites e textura da folha.

Essa alternância intencional entre skin e arte nativa é o fallback de segurança. Ela evita representar uma ação especial com um idle ou ataque genérico, mas significa que a Silk Eclipse ainda não é uniforme em todas as ações.

---

## 8. Limitações e riscos conhecidos

### Paletas e materiais

- As máscaras de cor podem incluir acessórios ou excluir partes que não tenham as cores esperadas.
- Não há máscaras manuais por frame; a classificação aproximada não é uma garantia anatômica.
- O fallback por nome depende da hierarquia e dos nomes dos objetos. Sprites sem termos reconhecidos permanecem originais.
- A aplicação depende de `collectionInst`, `materialId`, `materialInst` e reflection de APIs tk2d. Mudanças de assembly podem impedir o binding ou o refresh.
- Objetos criados/reciclados depois da aplicação podem exigir a nova tentativa de `Update()`/`LateUpdate()`; não há um evento universal de “nova coleção da Hornet pronta”.
- `changed pixels` mede diferença de RGB no buffer, não confirma que a GPU exibiu o atlas em cada resolução ou material.

### Packs CustomKnight

- O vínculo é por nome aproximado, não por um identificador de coleção ou manifesto.
- PNGs com o mesmo basename substituem a entrada anterior; nomes ambíguos podem casar com a coleção errada.
- Não há validação de dimensões, pivôs, UVs, ordem de frames, shaders ou atlas completo.
- Um PNG ser decodificável não significa que ele seja visualmente compatível com o material nativo.
- O menu reaproveita a textura como `mainTexture`; não importa a edição nem o projeto do CustomKnight.

### Silk Eclipse

- Há somente oito poses amostradas. Não são centenas de frames equivalentes aos clips do jogo.
- Dash, parede, pogo, ataque vertical, ferramentas, dano, morte, cutscenes e outras ações usam fallback nativo.
- O ataque simples só usa a pose `6` entre 20% e 70% do clip.
- A folha mistura orientações de caminhada e depende da regra de `flipX` acima.
- Escala e pivôs foram calculados a partir do asset existente, mas tamanho, offsets, ataques horizontais e efeitos de cura ainda precisam de confirmação in-game.
- `FindVisibleRect()` usa alpha para recortar a célula; não distingue automaticamente corpo de efeito dentro da célula.
- O recurso embutido evita a ausência do PNG externo, mas não corrige uma folha criada com dimensões, conteúdo ou pivôs incorretos.

### Validação

A 0.7.25 foi compilada contra as assemblies fornecidas e testada de forma isolada; o jogo Windows não é executado no sandbox. Ainda não estão confirmados in-game, nesta base:

- renderização real dos presets e da paleta personalizada em todas as cenas;
- classificação de todas as partes e variantes visuais de Hornet;
- aplicação de packs externos com atlases reais do CustomKnight;
- alinhamento, tamanho, pivôs e direção da Silk Eclipse em diferentes resoluções;
- comportamento em transições, pooling, troca de cena e visibilidade do renderer;
- cobertura de todos os nomes de clips presentes na instalação local;
- ausência de arte duplicada quando efeitos ou renderers corporais mudam durante uma ação.

---

## 9. Diagnóstico prático

O título do menu mostra a versão carregada. Para paletas, a sequência mais útil é:

1. selecione `Carmim` para obter uma mudança forte;
2. observe se o status reporta renderers e pixels;
3. tente `Paleta personalizada` com valores contrastantes;
4. selecione `Original` para confirmar a restauração.

As mensagens produzidas pelos métodos atuais ajudam a separar falhas:

- `Appearance runtime ...` — confirma a assembly e a inicialização do menu;
- `Appearance binding ...` — informa `frames`, `active renderers`, `changed pixels` e preset;
- `Appearance renderer bound ...` — mostra identidade do sprite e textura após o refresh;
- `Appearance unbound renderer ...` — lista renderer que não confirmou material ativo;
- `appearance pack inspected ...` — informa quantas coleções foram examinadas e alteradas;
- `Silk Eclipse loading embedded sheet from DLL` ou `loading external sheet` — informa a origem da folha;
- `Silk Eclipse sync` — informa clip, frame nativo, pose, modo `skin`/`native fallback` e altura Idle de referência.

Se um pack não aparecer, confirme que a pasta é uma subpasta direta de `Skins`, que há PNGs legíveis e que o basename do PNG corresponde a uma coleção, `spriteCollectionName` ou textura nativa. Se a Silk Eclipse não carregar, confirme a existência do recurso embutido na build e se a folha possui pelo menos quatro colunas e duas linhas com pixels não transparentes em cada célula.

---

## 10. Propostas futuras (não implementadas)

As ideias abaixo são propostas separadas do comportamento atual:

1. **Máscaras manuais por atlas/frame:** armazenar regiões ou máscaras de capa, cabeça e sob a capa, com prioridade sobre os limiares cromáticos.
2. **Manifesto de pack:** declarar coleção, dimensão esperada, pivôs, PPU e correspondência de atlas; rejeitar incompatibilidades antes de alterar materiais.
3. **Matching determinístico:** substituir substring ambígua por chaves exatas e relatar colisões de basename.
4. **Validador de pack:** conferir dimensões, alpha, quantidade de regiões e nomes antes de aplicar; permitir relatório sem alterar a cena.
5. **Atlas completo da Silk Eclipse:** desenhar e mapear frames equivalentes para ações atualmente nativas, em vez de reutilizar uma pose genérica. Isso exigiria arte nova e mapeamento de clips, não apenas mais lógica no seletor.
6. **Calibração in-game assistida:** exibir uma grade de pivô/escala ou capturar referências para ajustar por pose sem alterar a geometria nativa.
7. **Testes de integração visual:** executar uma matriz em jogo Windows com idle, caminhada para ambos os lados, salto, queda, ataque, cura, dash, pogo, parede, cenas e troca de preset, registrando screenshots e o `Silk Eclipse sync` correspondente.
8. **Configuração de fallback:** permitir escolher, por pack, entre manter arte nativa, usar classificação por nome ou não recolorir uma parte desconhecida.

---

## Referências internas

- [`src/SilkThreadAppearanceMenu.cs`](../src/SilkThreadAppearanceMenu.cs) — `Initialize`, `Update`, `LateUpdate`, `ScanPresets`, `ApplySelectedPreset`, `ApplyPalette`, `ApplyPartTexturePalette`, `RecolorRegion`, `LoadPackedTextures`, `ApplyPackedTextures`, `FindReplacementTexture`, `RestoreAllCollections`, `LoadSettings` e `SaveSettings`.
- [`src/SilkThreadMaterialBinding.cs`](../src/SilkThreadMaterialBinding.cs) — `RuntimeCollection`, `Capture`, `Bind`, `Restore` e resolução segura de `materialId`.
- [`src/SilkThreadSkinAssetReader.cs`](../src/SilkThreadSkinAssetReader.cs) — prioridade de arquivo externo e fallback para recurso embutido.
- [`src/SilkThreadEclipseVisual.cs`](../src/SilkThreadEclipseVisual.cs) — `Apply`, `Tick`, `CaptureNativeReference`, `LoadSheet`, escala, pivôs, ocultação e fallback nativo.
- [`src/SilkThreadEclipsePose.cs`](../src/SilkThreadEclipsePose.cs) — `Select`, `ProgressIndex` e termos de clips cobertos/não cobertos.
- [`src/SilkThreadCrestPlugin.cs`](../src/SilkThreadCrestPlugin.cs) — configuração `Appearance/MenuShortcut` e inicialização do menu.
- [`src/SilkThreadCrest.csproj`](../src/SilkThreadCrest.csproj) — recurso PNG embutido e referência lógica do recurso.
- [`Skins/SilkEclipse/SKIN.txt`](../Skins/SilkEclipse/SKIN.txt) — mapa e limites declarados da folha de oito poses.
- [`Skins/README.txt`](../Skins/README.txt) — instruções de uso de packs externos.
- [`docs/CHANGELOG-0.7.24.md`](CHANGELOG-0.7.24.md) — correções de binding/recurso e validação conhecida da 0.7.24.
- [`docs/CHANGELOG-0.7.25.md`](CHANGELOG-0.7.25.md) — correções de escala/sincronização e limites da 0.7.25.
- [`tests/eclipse-poses/PoseTests.cs`](../tests/eclipse-poses/PoseTests.cs) — testes isolados do seletor de poses.
- [`tests/material-binding/BindingTests.cs`](../tests/material-binding/BindingTests.cs) — testes isolados de binding e leitura de recurso.
