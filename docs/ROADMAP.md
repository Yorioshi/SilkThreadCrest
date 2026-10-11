# Roadmap e possíveis correções

Estas são **propostas de continuidade**, não funcionalidades já entregues nem compromissos de prazo. Prioridade é relativa ao risco de perda de progresso, regressão de gameplay e acabamento. Hipóteses encontradas por leitura estática não são bugs reproduzidos.

## P0 — segurança funcional e evidência

| Item | Motivo/estado atual | Critério de aceite sugerido |
|---|---|---|
| Validar Silk Eclipse 0.7.25 in-game | Sync/escala corrigidos e testes lógicos passaram; visual real ainda pendente | Capturas e log de Idle, walk ambos lados, jump/fall, hit/heal, fallback dash/pogo/parede e respawn, sem escala errada nem arte duplicada |
| Testes completos de slots/novo jogo | Typed load usa slot; string/write usam `profileID`, fallback 0 | A → B → A sem mistura, new game limpa só contexto certo, vazio/corrompido recupera com regra documentada |
| Escrita de sidecar resiliente | `WriteAllText` não é transacional, sem schema/migração robustos | Arquivo temporário/renomeação atômica onde suportado, backup anterior e recuperação após interrupção simulada |
| Política do segundo hit do fio | `IgnoreInvulnerable = true` na exploração; fallbacks do duelo têm proteção diferente | Decisão de gameplay explícita e testes em i-frames/fases de chefe; preservar drops/morte |
| Ferramentas de efeito vs fallback ofensivo | Ação vermelha sem dano pode cair em fallback 2× ferrão | Não criar dano arbitrário para ferramenta não ofensiva, sem reabastecer/consumir estoque duas vezes |
| Restauração após interrupção | FSM manipula física/input/time/brasão | Derrota, recuo, vitória, cena carregada/destruída e alvo removido não causam soft-lock |

## P1 — precisão mecânica e robustez

| Item | Trabalho proposto | Critério de aceite sugerido |
|---|---|---|
| IDs de invocação estáveis | Substituir nome truncado por identidade estável, com migração | Variante não colide; saves antigos mantêm catálogo sem duplicação |
| Catálogo de ataques curado | Complementar blacklist heurística com metadados/whitelist por inimigo | Dano de contato/aura excluído; ataques válidos identificados e documentados por caso |
| Visuais de invocação após restart | Fonte reidratável independente de inimigo vivo na cena | Invocação possui visual mesmo antes de reencontrar o inimigo, sem IA/vida/hitbox/pooling indevido |
| Isolamento de clone visual | Evitar Awake/OnEnable lógicos ao arquivar e criar visual | Nenhum registro de alvo/singleton/física/evento de gameplay, testes de inimigos com spawn-effects |
| Descontos de skill reais | Custo reduzido pode ser superado pelo gasto nativo; não há refund automático | Gasto líquido corresponde ao custo anunciado, sem energia criada nem cobrança dupla |
| Gate por turno só após dano aceito | Chave/registro atual dependem de ferramenta e callback | Hit rejeitado não esgota passiva indevidamente; ativo persistente não dá dano repetido nem bloqueia ataque normal |
| Ações multi-hit/prolongadas | Definir quantidade e janela antes de fallback | Nenhum dano duplicado por hit tardio; comportamento normal de skill preservado dentro do contrato de turno |
| Precisão de dano inimigo/defesa | Primeiro DamageHero usado como reserva; arredondamento mínimo 1 | Dano/reforço correspondem a golpe escolhido, vida antes/depois real e recompensa única |
| IDs de sinergia | Trocar aliases substring por dados validados da instalação | Nenhum efeito acidental por idioma/nome parecido, custos e bônus testados por item |
| Atualizar estoque pós-uso | Label pode ficar defasada até rebuild | Quantidade visível corresponde imediatamente ao estoque real |

## P2 — personalização e acabamento

A prioridade artística é uma **Silk Eclipse frame a frame completa**: desenhar arte correspondente aos clips nativos, preservar atlas/UV/pivôs/escala/direção e cobrir ferramentas, dash, parede, dano/morte, pogo e cutscenes. A folha atual de oito poses não substitui esse trabalho.

Para cores independentes precisas, criar máscaras manuais por atlas/frame para capa, cabeça e membros/roupa sob a capa; manter a heurística cromática apenas como fallback declarado. Para packs, adicionar manifesto de coleção/dimensão/PPU/pivô, associação exata de atlas e validação antes da aplicação. O menu pode oferecer preview, ferramentas de máscara e importação guiada, mas hoje não é um editor externo completo.

Outros aprimoramentos propostos: seletor visível de ordenação/nome/aquisição para invocações, busca/paginação, custos/estados/sinergias claros no HUD, vida azul separada, ícones finais próprios do item/brasão, labels de TAUNT dinâmicas no fallback, layout acessível em múltiplas resoluções e tradução dos menus.

## Engenharia e distribuição

Expor parâmetros de balanceamento com limites/defaults atuais e documentar migração, separar apresentação do domínio do combate gradualmente, criar testes de DTO/save/dano e integração Unity, medir stutter/memória de atlases e buscas globais, e registrar uma matriz real de versões do jogo/BepInEx/outros mods são trabalhos futuros.

Os scripts de teste e empacotamento agora são portáveis; o Bash foi executado. O PowerShell e o ciclo do GitHub Actions devem ter seus resultados registrados depois de execução. A licença MIT foi escolhida pelo proprietário e adicionada em `LICENSE`; dependências e assets de terceiros mantêm direitos próprios. O baseline foi criado privado originalmente.

## Sequência sugerida

Conservar `v0.7.25` como baseline; registrar testes in-game atuais; corrigir risco P0 reproduzido num patch pequeno; repetir os casos críticos; depois ampliar conteúdo artístico/mecânico. Não iniciar uma refatoração de tudo e uma skin completa no mesmo release. Cada mudança deve ter evidência, rollback e documentação do comportamento real.
