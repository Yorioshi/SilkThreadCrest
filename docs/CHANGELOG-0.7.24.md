# SilkThreadCrest 0.7.24 — correção de aplicação visual

## Falhas confirmadas

A versão 0.7.23 ainda verificava a existência de `silk_eclipse_sheet.png` antes de chegar ao carregamento embutido. A saída prematura foi removida. O PNG embutido é agora lido mesmo quando a pasta externa não existe.

A inspeção da assembly `TeamCherry.TK2D.dll` mostrou que `tk2dSprite.UpdateMaterial` lê `collectionInst.spriteDefinitions[id].materialInst`. A versão anterior alterava o material do Renderer e depois chamava UpdateMaterial, que voltava ao material original do frame. Agora os dois campos de cada definição (`material` e `materialInst`) são vinculados ao material derivado, na coleção de runtime da plataforma. As referências originais são restauradas ao selecionar Original.

## Outras mudanças

A raiz das skins é a pasta da DLL efetivamente carregada, não uma pasta fixa baseada no nome exibido do plugin. O título do menu mostra **0.7.24** e o log mostra o caminho da DLL carregada. A aplicação processa as coleções da Hornet ativa em vez de percorrer 14 coleções nomeadas que podem não estar em uso. Frames duplicados não são processados repetidamente. O log informa `frames`, `active renderers` e `changed pixels`, além da textura usada pelo Renderer após UpdateMaterial.

A separação de cabeça/capa/membros é uma **máscara aproximada por cor da arte original**: tecido vermelho, máscara clara e membros escuros. Isso não equivale a máscaras desenhadas manualmente para cada frame. Acessórios da mesma cor podem entrar no mesmo grupo; contornos pretos são preservados. Uma separação anatômica perfeita em todas as poses exigirá máscaras específicas de cada frame.

A Silk Eclipse continua sendo uma camada visual experimental com oito poses, não um pack completo de todas as animações nativas, cutscenes e interações. Seu renderer usa a camada e a ordem do renderer corporal ativo e ajusta o tamanho à Hornet. Esta atualização não transforma a folha de oito poses em um atlas completo do jogo.

## Validação

Foram executados dez testes isolados com o helper real de binding e o leitor real de recursos. Eles cobrem o vínculo ao `materialInst` de cada frame, seleção da coleção de runtime, índices inválidos, restauração exata, leitura do PNG embutido sem pasta externa e prioridade do arquivo externo. Esses testes usam modelos mínimos para materiais tk2d; **não são testes de renderização Unity**.

A compilação usa as assemblies fornecidas do jogo e BepInEx. Também é feita uma compilação limpa do ZIP de fonte. O executável Windows do jogo não está disponível para teste neste ambiente; não há confirmação visual in-game nesta entrega.

## Teste no jogo

Feche o jogo, substitua a DLL instalada e mantenha apenas uma cópia do plugin. Abra F8: o título precisa mostrar **0.7.24**. Selecione Carmim para um teste forte de cor, depois Paleta personalizada e Silk Eclipse. Se o efeito ainda não aparecer, envie desde a linha `Appearance runtime 0.7.24` até `Appearance binding 0.7.24`, incluindo `Appearance renderer bound`, `Appearance unbound renderer` e `Silk Eclipse loading...`. Não apague saves nem os sidecars de invocações.
