# Estado do projeto — baseline 0.7.25

**Registro: 11 de outubro de 2026. Licença do código original: [MIT](../LICENSE).** A inclusão no GitHub organiza o código atual; não altera mecânicas nem cria uma versão de gameplay nova. O termo “pronto” usado para o combate significa que o fluxo atende ao uso pontual do proprietário, não que houve QA completo de todas as situações do jogo.

## Como interpretar os estágios

| Estágio | Significado |
|---|---|
| Implementado | Caminho presente e revisado no código atual |
| Compilado | C# e referências foram resolvidos contra as assemblies fornecidas |
| Testado isoladamente | Lógica exercitada sem execução do jogo/Unity completo |
| Confirmado pelo usuário | Teste pontual do proprietário em sua instalação; não representa cobertura universal |
| Experimental | Funcionalidade parcial, aproximada ou ainda com acabamento/integração pendentes |
| Proposto | Não implementado; depende de decisão e trabalho futuro |

Não usamos percentual de conclusão inventado. A fonte detalhada de valores é o código em `src/` e os documentos de cada módulo.

## Evidências disponíveis

O proprietário confirmou o funcionamento do combate após as correções da linha 0.7.x; a persistência de invocações foi explicitamente confirmada após a 0.7.15; o menu e a aplicação de paletas/skin foram confirmados após a 0.7.24. A aplicação da Silk Eclipse nessa última versão revelou escala/animações inadequadas. A 0.7.25 passou a acompanhar clips/frames tk2d e a referência Idle nativa; foi compilada e passou por 30 verificações do seletor. **O alinhamento e a apresentação in-game da 0.7.25 ainda precisam de confirmação.**

O ambiente de desenvolvimento compila contra assemblies fornecidas, mas não executou Silksong Windows. Os testes de binding/recurso usam modelos mínimos; não são uma cena Unity. Não há evidência padronizada da versão exata do executável, de todas as dependências do testador ou de um inventário de todos os inimigos/ferramentas testados. Esses metadados devem ser acrescentados em cada relato futuro.

## Estado por sistema

| Sistema | Estado atual | Trabalho restante / critério de conclusão |
|---|---|---|
| Registro de brasão/item | Implementado, inicialização tolerante a erros individuais | Validar todas as rotas de criação/carregamento e slots; substituir placeholders artísticos |
| Fio descendente/puxão | Implementado com hitbox nativa/cast e segundo golpe | Testar colliders múltiplos, morte entre hits, hazards e invulnerabilidade do segundo golpe |
| Sacrifício | Implementado com regras ambientais, vida mínima, cooldown e custo repetido | Testar hazards incomuns, vida azul e colisão de patches; avaliar whitelist |
| Início/fim do duelo | Confirmado em teste pontual; entrada exige chão | Matriz de derrota, recuo, save/cena/dano inesperado e restauração |
| Ataque/cura/defesa | Implementados com custos, perda de HP e recompensa | Testar danos ímpares, mínimo 1, i-frames, atrasos e múltiplos eventos |
| Skills | Implementadas via ferramenta/rotina nativa e fallback | Lista por skill de custo efetivo, dano, duração, hitboxes e eventos tardios |
| Ferramentas vermelhas | Implementadas respeitando estoque e consumo nativo | Testar cada ferramenta equipada/fabricada, não ofensivas, quantidade e pooling |
| Passivas/objetos persistentes | Gate implementado por ferramenta | Validar `RepresentingTool` em todas as fontes, chave estável e dano realmente aceito |
| Sinergias | Aliases implementados para custos e seda | IDs explícitos, catálogo de itens locais e testes por efeito; descontos de skill precisam validar gasto nativo |
| Seletor de brasões | Implementado; uma troca/turno e três/batalha | Equipamentos/slots/upgrades especiais e restauração de todos os estados |
| Invocações/aprendizado | Funcional em teste pontual; snapshot e commit antes da morte | Catálogo semântico, variantes, bosses, filtros e balanceamento de dano bruto |
| Visual de invocação | Clone visual isolado; dano independe dele | Disponibilidade após restart, clips corretos, eliminar riscos de callbacks de instanciação |
| Persistência | Confirmada após correção tipada na 0.7.15 | Todos os slots, rotas string/tipada, new game, integridade, backup e escrita atômica |
| Ordenação de invocações | Dano decrescente no menu; outros modos existem na API | Expor seletor de modo/pesquisa/paginação; não há botão de troca de modo no baseline |
| HUD | Composição aprovada pontualmente, bindings e barra acima do alvo | Resoluções, traduções, controles, vida azul e sobreposição com UI nativa |
| Menu F8/cursor | Implementado, aplicação visual confirmada na 0.7.24 | Window focus, pause, pooling/cenas, PowerShell e dispositivos sem teclado |
| Paleta Capa/Cabeça/Sob a capa | Funciona por máscaras cromáticas aproximadas | Máscaras manuais por frame/atlas para independência anatômica exata |
| Packs externos | Importação PNG parcial por nome | Manifesto, dimensões/UVs, colisões de nomes e packs reais equivalentes aos atlases locais |
| Silk Eclipse | Oito poses experimentais; sync/escala corrigidos na 0.7.25 | Validar visual no jogo e desenhar cobertura frame a frame das demais animações |
| Testes/documentação | 10 checks binding/recurso + 30 poses; CI isolada e scripts | Testes de save/dano, harness Unity, matriz manual com evidências e benchmarks |

## Dados e persistência

`appearance.json` acompanha a pasta da DLL e vale para a aparência da instalação, não por slot. O arquivo BepInEx `manus.silkthreadcrest.cfg` registra o atalho do menu. O catálogo de invocações vai para o bloco `SilkThreadCrest` do JSON nativo e para `Application.persistentDataPath/SilkThreadCrest/summons-slot-N.json`. A rota tipada usa o slot recebido explicitamente. **Não há slot 2 fixo no código.** A rota string e a gravação do sidecar ainda consultam `profileID`, com fallback 0; por isso todos os slots e a troca de contexto continuam no checklist de validação.

O save armazena ataques/nomes/ordens, não objetos Unity. Depois de reiniciar, uma invocação pode estar disponível para dano antes de seu template visual ser reconstituído ao encontrar outro inimigo correspondente na cena.

## O que não existe no baseline

Não há editor de pixel/arte equivalente ao FireAlpaca dentro do jogo, importador completo de projeto CustomKnight, skin Eclipse com todas as animações, segmentação anatômica perfeita por máscara manual, catálogo completo validado de todos os inimigos/golpes, seletor interativo dos modos de ordenação, parâmetros globais de balanceamento no `.cfg`, backup transacional de saves ou compatibilidade demonstrada com outras plataformas e todos os mods.

## Continuidade

Comece por [AGENTS.md](../AGENTS.md), [Arquitetura](ARQUITETURA.md) e [Contribuição](../CONTRIBUTING.md). Prioridades e critérios de aceite estão no [Roadmap](ROADMAP.md). O código atual deve permanecer disponível como baseline de rollback enquanto novos patches são avaliados.
