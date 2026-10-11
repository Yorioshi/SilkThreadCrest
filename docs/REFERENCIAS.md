# Referências técnicas, visuais e créditos

Este índice registra fontes usadas durante o desenvolvimento anterior como estudo/inspiração. Não é uma declaração de compatibilidade completa, uma cópia de seus projetos nem uma revisão nova de todos os links. O comportamento atual é determinado pelo código do repositório; conteúdos externos podem mudar.

## Skins e customização

| Referência | Uso no projeto / limite |
|---|---|
| [CustomKnight Creator Fixed](https://github.com/asd9176506911298/CustomKnight-Creator-Fixed) | Estudo do fluxo editar frames/empacotar atlas PNG; não é o editor Python inteiro integrado ao jogo |
| [Vídeo do fluxo de criação](https://www.youtube.com/watch?v=1NoAW9ouONU&t=2s) | Referência de pipeline de arte, edição e pack; a integração atual só importa PNGs com associação de nomes |
| [Nexus 142](https://www.nexusmods.com/hollowknightsilksong/mods/142) | Referência do fluxo CustomizerT2D/Texture2D; mod/arquivos externos não incluídos |
| [Nexus 203](https://www.nexusmods.com/hollowknightsilksong/mods/203) | Referência de skin/organização de assets; nenhum pack foi redistribuído |
| [Nexus 653](https://www.nexusmods.com/hollowknightsilksong/mods/653) | Referência adicional do pipeline indicado; arquivos não incluídos |
| [FireAlpaca](https://firealpaca.com/) | Ferramenta externa de pintura/edição mencionada no fluxo; não é componente nem dependência de runtime do mod |
| [SilksongWardrobe](https://github.com/Zmarfan/SilksongWardrobe) | Estudo de aplicação de aparência/coleções/materiais e carregamento de assets |
| [Silksong Patchwork](https://github.com/Ashiepaws/silksong-patchwork) | Referência de carregamento/substituição de texturas no runtime |
| [HKSkins](https://hkskins.art/) | Catálogo de referência; packs de terceiros não incluídos |

A implementação específica de binding por frame está em `SilkThreadMaterialBinding.cs`; a paleta usa máscaras aproximadas por cor e a Silk Eclipse é uma folha própria de oito poses. [Personalização e assets](PERSONALIZACAO-E-ASSETS.md) documenta exatamente o que foi portado e o que não existe.

## Save e modding

Foram consultados como referência de formato/fluxo os projetos [Silksong Save Viewer](https://github.com/BlueOrcaz/silksong-save-viewer), [Silksong Save Editor](https://github.com/just-addwater/silksong-saveeditor) e [Stagstation](https://github.com/dizzyfrogs/stagstation). A solução deste mod é seu próprio hook Harmony do JSON nativo com sidecar por slot; não depende desses aplicativos em runtime.

[Documentação BepInEx](https://docs.bepinex.dev/master/articles/dev_guide/dev_tools.html), [Harmony](https://harmony.pardeike.net/) e as assemblies locais são referências para hooks/inspeção. A versão atual usa IMGUI, apesar de uGUI/Canvas ter sido considerado como possível evolução em pesquisa. Não anunciar uma migração para uGUI que não ocorreu.

## Estilo visual e referências indicadas

| Link | Papel |
|---|---|
| [Referência ATLUS/Persona](https://www.youtube.com/watch?v=YRq1AQIdu4k&t=28s) | Inspiração de alto contraste, recortes, vermelho/preto/branco, HUD e transições |
| [Referência de HUD radial](https://www.youtube.com/watch?v=gYWQFf2RFWo) | Exploração visual estudada; o baseline mantém o HUD da linha estável, não um HUD radial implementado |
| [Catálogo Thunderstore Silksong](https://old.thunderstore.io/c/hollow-knight-silksong/) | Contexto do ecossistema; não é prova de compatibilidade com todos os mods listados |
| [Página Steam indicada](https://steamcommunity.com/sharedfiles/filedetails/?id=3645482846) | Referência adicional de pesquisa anterior; não é dependência do mod |

Outros vídeos indicados durante a pesquisa: [vvTPPywvUlM](https://www.youtube.com/watch?v=vvTPPywvUlM), [QMiWbu0il6w](https://www.youtube.com/watch?v=QMiWbu0il6w), [z35cFvU0McQ](https://www.youtube.com/watch?v=z35cFvU0McQ), [S119vi0fZWQ](https://www.youtube.com/watch?v=S119vi0fZWQ), [5sYNzDnrjGY](https://www.youtube.com/watch?v=5sYNzDnrjGY), [h_Mq0Dcw4PE](https://www.youtube.com/watch?v=h_Mq0Dcw4PE), [GP5uCW36dM8](https://www.youtube.com/watch?v=GP5uCW36dM8), [JAKgvH1s7FM](https://www.youtube.com/watch?v=JAKgvH1s7FM), [iD5y-oZOFAM](https://www.youtube.com/watch?v=iD5y-oZOFAM) e [LMayapYyEr8](https://www.youtube.com/watch?v=LMayapYyEr8). Permanecem como referências históricas; instruções técnicas futuras devem ser verificadas na fonte e na API real da instalação.

## Antes de reutilizar material externo

Confirme se é Silksong ou Hollow Knight original, o runtime/plataforma, os nomes/UVs/atlas da instalação, a licença de código e a licença de assets separadamente. Creditar uma URL não concede direitos de redistribuição. O código original está sob [MIT](../LICENSE); [NOTICE.md](../NOTICE.md) preserva os direitos de dependências e assets de terceiros.
