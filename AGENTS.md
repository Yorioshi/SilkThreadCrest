# Instruções para agentes de IA e colaboradores

Este arquivo é o ponto de entrada para **qualquer agente de IA** que vá continuar o SilkThreadCrest. Não depende de um produto específico. Se sua ferramenta não carrega `AGENTS.md` automaticamente, leia-o ou forneça seu conteúdo como contexto antes de pedir alterações.

## 1. Identidade e ponto de partida

- Jogo-alvo: **Hollow Knight: Silksong, Windows, runtime Unity gerenciado**, com BepInEx e Harmony.
- Plugin: `manus.silkthreadcrest`; assembly: `SilkThreadCrest.dll`; versão do código inicial deste repositório: **0.7.25**.
- Linguagem: C#, target **net472**. O SDK .NET 8 foi usado para compilar; isso não torna o plugin um aplicativo net8.
- Repositório inclui fonte, arte própria e testes isolados. **Não inclui DLLs do jogo/BepInEx, executável, saves pessoais ou credenciais.**
- É um protótipo avançado de combate por turnos no cenário do jogo, com brasão/item personalizados, invocações, ferramentas e customização visual. Não é uma engine de RPG separada.
- O código e os documentos versionados são a fonte de verdade. Não invente requisitos com base em lembranças de conversas indisponíveis.

## 2. Leia antes de mudar

1. `README.md`: visão geral, instalação e índice.
2. `docs/ESTADO-DO-PROJETO.md`: o que está implementado, confirmado pelo usuário e ainda não validado.
3. `docs/ARQUITETURA.md`: responsabilidade de cada arquivo.
4. `CONTRIBUTING.md`: compilação, testes e entrega.
5. Documento do módulo que será alterado em `docs/`.
6. `docs/TESTES-E-LIMITACOES.md`, `docs/ROADMAP.md` e `docs/CHANGELOG-0.7.25.md`.
7. Leia os métodos reais e os hooks antes de editar. Documentação pode ficar defasada: relate a divergência e corrija-a no mesmo patch.

## 3. Contratos funcionais a preservar

Não mude estes contratos sem uma solicitação explícita ou proposta aprovada:

- Batalhas por turnos só começam com Hornet no chão e um alvo elegível próximo. O duelo usa a sala atual; os demais sistemas não são uma arena totalmente pausada.
- Hornet e o alvo usam os sistemas reais de vida/dano; início, recuo e término devem restaurar estado físico, input, posição e equipamento.
- O wipe visual ocorre **somente no início e no fim** do combate. Não em cada troca de turno.
- HUD do mod sem prompts de ação/recuo flutuantes duplicados. Instruções de executar/recuar/brasões usam bindings atuais; navegação usa ↑/↓ = MUDAR AÇÃO.
- Turno indicado **somente por texto destacado**, sem clones/retratos. HP do inimigo fica acima do alvo; brasão equipado tem nome e ícone.
- Troca de brasão: no máximo uma por turno e três por batalha, sem gastar a ação; brasão anterior restaurado no fim.
- Defender-se reduz 50% do próximo dano elegível, com arredondamento nativo documentado; recompensa de seda somente se vida realmente diminuiu.
- Invocação aprendida na primeira vitória: aviso, catálogo persistente por slot, seleção de golpe, custo base 3 seda sujeito às sinergias, uma ação normal. Cancelar seleção não consome turno/recursos.
- Ferramentas de ataque equipadas respeitam estoque fabricado no banco e consumo nativo. Não fabricar/abastecer automaticamente nem consumir duas vezes.
- Passivas e ferramentas persistentes causam dano no máximo uma vez por ferramenta por turno da Hornet, segundo identificação atual do `RepresentingTool`. Não aplicar o gate indiscriminadamente a golpes normais.
- Fallbacks de dano não podem atravessar invulnerabilidade de script do alvo. Nunca escrever `enemy.hp -= ...` como atalho sem justificativa e preservação da sequência nativa de morte/drops.
- Sacrifício converte apenas perigos ambientais elegíveis, com cooldown e custo de repetição; ataques comuns de inimigos não são convertidos.
- Personalização visual é independente do combate/save. Uma falha do menu não deve impedir o plugin principal de funcionar.

## 4. Verdade sobre a validação

**Não confunda compilação com funcionamento no jogo.**

- Usuário confirmou combate em testes pontuais anteriores, persistência de invocações após a correção de carregamento tipado e aplicação de paleta/skin na **0.7.24**.
- A skin da 0.7.24 foi aplicada, mas tamanho e animações ficaram inadequados.
- **0.7.25** corrige sincronização tk2d, escala/pivô e fallback nativo. Foram feitas builds e 30 testes isolados de poses; ainda **não há confirmação in-game da 0.7.25** neste baseline.
- Os dez testes de binding/recurso usam modelos mínimos de materiais tk2d. Não renderizam Unity nem validam gameplay.
- Não existe matriz completa de todos os inimigos, bosses, ferramentas, idiomas, resoluções, outros mods, plataformas ou versões do jogo.
- Todo relatório de entrega deve separar: teste automatizado, inspeção estática/IL, teste manual realizado e teste manual pendente.

## 5. Caminho de trabalho seguro

1. Faça `git status`, identifique branch/commit/versão e registre o pedido atual.
2. Leia o módulo e reproduza a falha com log/captura quando disponíveis. Não faça uma série de releases especulativas sem evidência.
3. Crie uma branch curta. Defina hipótese, mudança mínima e regressões que precisam ser testadas.
4. Localize APIs reais nas assemblies locais. Para reflexão/Harmony, confirme nome, assinatura, overload e se o método é genérico fechado. Não adivinhe APIs.
5. Altere apenas o necessário. Evite refatoração ampla junto de um patch de balanceamento ou aparência.
6. Execute os testes isolados (`scripts/test.sh` ou `scripts/test.ps1`). Compile com as DLLs da instalação real. Se faltar dependência, declare a falta; não baixe DLL de jogo de terceiros nem use stubs para afirmar build de produção.
7. Faça diff e auditoria de efeitos colaterais. Se o patch é visual, confirme que as rotinas de dano/save/turno não mudaram.
8. Registre o resultado dos testes, alterações, limitações e checklist in-game. Atualize versão, documentação do módulo e changelog se há mudança de comportamento.
9. Empacote somente a DLL do mod, arte/instruções e fonte. Não inclua DLLs do jogo, BepInEx, saves, configs pessoais, logs com dados privados ou segredos.
10. Não sobrescreva a última build conhecida como funcional sem backup. Entregue rollback claro.

## 6. Armadilhas técnicas importantes

### Unity / GUI / renderização

- `GUI.skin`, estilos e desenhos IMGUI devem ser criados/usados em contexto `OnGUI`, nunca no `Awake` inicial do plugin.
- F8 abre/fecha; cursor é liberado enquanto menu está aberto e estado anterior restaurado ao fechar. Teste coexistência com pause/menu/batalha.
- tk2d usa `collectionInst.spriteDefinitions[id].materialInst`. Mudar só `collection.materials`, `materialInsts` ou `Renderer.sharedMaterial` pode ser desfeito por `UpdateMaterial`.
- Use `SilkThreadMaterialBinding` para capturar/vincular/restaurar materiais de cada frame e resolver a coleção de runtime da plataforma.
- Não modifique assets compartilhados sem backup. Destrua texturas/materiais temporários ao restaurar. Não processe atlas completo a cada frame ou a cada alteração de slider sem debouncing.
- A máscara da paleta é **aproximada por cor da arte original**, não uma segmentação anatômica perfeita. Não anuncie independência perfeita de cada parte em todas as poses.
- A Silk Eclipse tem **oito poses**. O seletor sincroniza poses aos clips/frame nativos, mas ações não cobertas usam arte original. Não a descreva como skin frame a frame completa.
- Não gere um único personagem estático e afirme que todas as animações nativas foram adaptadas. Uma skin completa exige frames/atlases equivalentes, pivôs, orientação, dimensões, UVs e efeitos consistentes.

### Clones / invocações / dano

- Não clonar Hornet ou inimigo ativo para HUD: `Instantiate` pode executar Awake/OnEnable, substituir singletons, registrar `HealthManager`, criar hitboxes ou disparar FSM.
- Nas invocações existentes, audite isolamento do snapshot visual: zero IA/vida/dano/física/colisão/FSM ativo. Se ampliar o visual, não reintroduza gameplay no clone.
- Capture dados antes da destruição nativa, confirme aprendizado apenas com derrota válida e preserve sequência de morte/drops.
- Um fallback não deve dobrar dano se um impacto tardio chega depois. A flag de invulnerabilidade deve ser validada antes de fallback. Teste ataques prolongados.
- Blacklist de hitboxes passivas é heurística: preserve-a e adicione aliases fundamentados em logs/inspeção, não por suposição.

### Saves

- `SilkThreadSavePatches` lida com overloads tipados e método genérico fechado. Um patch que instala sem exceção não prova que o jogo passou por ele.
- Sidecar: `Application.persistentDataPath/SilkThreadCrest/summons-slot-N.json`, com N recebido do save carregado. **Não é exclusivo do slot 2**.
- Não trocar por um arquivo global compartilhado entre slots. Teste carregar A → B → A, new game, save vazio, sidecar inválido, salvar/reiniciar, autosave e retorno ao título.
- Nunca apagar, mesclar indiscriminadamente ou converter um save pessoal sem backup e autorização. Não alterar schema sem migração/versionamento explícitos.
- Metadados salvos e prefab visual são coisas distintas: após reiniciar, os dados podem existir antes de o inimigo aparecer e o visual ser reidratado.

## 7. Entrega esperada de qualquer alteração

Inclua: pedido atendido, arquivos alterados, versão/commit, comportamento anterior/novo, custos/dano que mudaram, evidência de testes, o que não foi testado, limitações, instalação e rollback. Quando não houver teste no jogo, escreva **“compilado e testado isoladamente; validação in-game pendente”**.

## 8. Prompt reutilizável para outro agente

> Você vai continuar o SilkThreadCrest. Leia primeiro `AGENTS.md`, `README.md`, `CONTRIBUTING.md`, `docs/ESTADO-DO-PROJETO.md`, `docs/ARQUITETURA.md` e a documentação do módulo afetado. Preserve os contratos de combate, saves por slot e isolamento da aparência. Não trate build como teste in-game. Meu pedido é: [descrever alteração]. Minha versão do jogo/BepInEx é: [informar]. Estes são meus logs/capturas: [anexar, sem dados privados]. As assemblies autorizadas da minha instalação estão em: [GameManagedDir e BepInExCoreDir]. Trabalhe em uma branch, faça a menor mudança correta, teste, documente, empacote e indique limitações/rollback. Se faltar uma informação material, peça-a em vez de inventar compatibilidade.
