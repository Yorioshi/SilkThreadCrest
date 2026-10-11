# SilkThreadCrest — Brasão do Fio

Mod experimental para **Hollow Knight: Silksong (Windows)** com combate por turnos no cenário, brasão/item personalizados, ferramentas, invocações persistentes e customização visual de Hornet.

**Versão de referência: 0.7.25.** O código está disponível para continuidade por colaboradores e agentes de IA. O repositório foi criado privado; para outras pessoas acessarem, o proprietário precisa conceder acesso. A licença geral de redistribuição ainda não foi escolhida — veja [NOTICE.md](NOTICE.md).

> **Transparência:** há confirmação do usuário em testes pontuais para combate, persistência de invocações e aplicação visual na 0.7.24. A 0.7.25 foi compilada e passou pelos testes isolados, mas a correção de escala/animações da Silk Eclipse ainda não tem confirmação in-game neste baseline. Não há cobertura completa de inimigos, bosses, ferramentas, idiomas, resoluções ou outros mods.

## Funções e estágio atual

| Função | Como funciona | Estágio/evidência |
|---|---|---|
| **Brasão do Fio** | Registra `SilkThread` usando Hunter como template; ferrão descendente até 12 unidades, puxão até alvo e segundo golpe quando o primeiro hit foi capturado. | Implementado; herda configurações do brasão-base. Fio/ferrão e ícone de item são placeholders. Matriz completa de hazards/inimigos pendente. |
| **Sacrifício** | Converte dano ambiental elegível em pogo; normalmente perde 1 HP, preserva vida mínima normal; cooldown 0,6 s. Repetição até 2,5 s custa 1 seda. | Implementado; testes amplos de hazards, vida azul e sinergias pendentes. Não é uma cura direta. |
| **Combate por turnos** | TAUNT perto de alvo vivo, com Hornet no chão. Trava o duelo na sala atual e alterna ações reais. Restaura posição, física, input e brasão na saída. | Fluxo confirmado pelo usuário em testes pontuais. Não é uma arena separada nem uma pausa global permanente. |
| **Cura e defesa** | Cura base 9 seda/até 3 HP. Defender reduz 50% do dano elegível, arredondando para cima, mínimo 1; recompensa base 2 seda se houver perda real de HP. | Implementadas; custos podem receber sinergias. Cobertura de todas as rotas de dano pendente. |
| **Troca de brasões** | Menu próprio durante o turno; uma troca por turno, três por batalha; não consome ação. | Implementada na linha de combate funcional; equipamentos/slots especiais precisam de validação individual. |
| **Ferramentas e skills** | Skills acionam rotina nativa e usam seda. Ferramentas vermelhas equipadas usam estoque fabricado no banco e `ThrowTool` nativo. | Implementado; consumo efetivo de cada ferramenta, multi-hit e efeitos especiais ainda precisam de teste por item. |
| **Passivas e objetos persistentes** | Gate de dano por ferramenta azul/amarela ou ferramenta com objeto ativo: uma ativação por turno da Hornet. | Implementado por `RepresentingTool`; cobertura depende da identificação da fonte. |
| **Sinergias** | Brasões/ferramentas podem reduzir custos ou aumentar seda de golpe/defesa, via aliases internos/localizados. | Implementadas; matching por substring é heurístico, não catálogo completo. |
| **Invocações** | Captura dados antes da morte, aprende após vitória, oferece golpes catalogados; custo base 3 seda, uma ação. Visual isolado aparece quando disponível. | Uso/dano e persistência tiveram confirmação pontual. Catálogo heurístico não equivale a todos os ataques de todos os inimigos. |
| **Save das invocações** | Extensão JSON nativa + sidecar `summons-slot-N.json`; carregamento por overloads string e tipado. | Persistência confirmada na correção 0.7.15. Não é exclusiva do slot 2; troca de todos os slots/new game/corrupção ainda precisa da matriz manual. |
| **HUD temático** | Preto/vermelho/branco, turno em texto destacado, HP sobre o inimigo, brasão equipado, controles atuais e wipe só entrada/saída. | Aparência do HUD aprovada pelo usuário em etapas anteriores; responsividade/dispositivos não têm cobertura universal. |
| **Menu F8 e paletas** | Presets Original/Carmim/Vazio/Seda/Ocre; canais Capa/Cabeça/Sob a capa; cursor livre e F8 fecha. | Aplicação confirmada na 0.7.24. Máscaras aproximadas por cor, não anatomia perfeita por frame. |
| **Packs PNG externos** | Detecta pastas de `Skins` e substitui texturas por associação de nomes/coleções. | Importação parcial, sem validação de UV/pivô/dimensão. Não é o CustomKnight Creator inteiro embutido. |
| **Silk Eclipse** | Arte própria de oito poses; 0.7.25 segue clips/frames tk2d e escala do Idle nativo; ações não cobertas usam arte original. | Experimental. 30 testes lógicos passaram; escala, pivôs e animação final ainda pendentes de teste in-game. Não é skin frame a frame completa. |

Detalhes e evidências estão em [Estado do projeto](docs/ESTADO-DO-PROJETO.md).

## Instalar e usar

Baixe o arquivo `SilkThreadCrest-<versão>-Windows.zip` na área **Releases**. BepInEx e o jogo precisam estar instalados separadamente; eles não vêm no ZIP. Faça backup do save antes de testar alterações de persistência, feche o jogo e mantenha apenas uma cópia de `SilkThreadCrest.dll`.

Extraia o pacote para a pasta principal do jogo. Estrutura esperada:

```text
Hollow Knight Silksong/
└── BepInEx/
    └── plugins/
        └── SilkThreadCrest/
            ├── SilkThreadCrest.dll
            └── Skins/
                └── SilkEclipse/Texture2D/silk_eclipse_sheet.png
```

A folha Silk Eclipse também está embutida na DLL; a cópia externa tem prioridade quando existe. Os presets e a seleção ficam em `appearance.json` ao lado da DLL. Invocações ficam separadas por save/slot no diretório persistente do jogo. **Não apague esses arquivos para atualizar o plugin.**

### Controles

| Situação | Controle |
|---|---|
| Abrir/fechar aparência | F8 por padrão; configurável em `Appearance / MenuShortcut` |
| Iniciar combate | Ação TAUNT, perto do alvo e no chão; padrão mostrado V/clique analógico direito |
| Mudar ação | Cima/baixo ou esquerda/direita; HUD mostra ↑/↓ = MUDAR AÇÃO |
| Executar ação | Binding de salto do jogador, mostrada no HUD |
| Recuar | Binding de dash durante turno da Hornet |
| Abrir brasões | Binding de ataque durante turno da Hornet |
| Confirmar em seletor | Binding de salto |
| Fechar seletor | Binding de ataque ou dash |

As instruções internas de batalha leem bindings do jogo; o prompt alternativo de TAUNT ainda pode apresentar apenas o padrão. O seletor de brasões/invocações pausa temporariamente o cenário; a batalha normal não pausa todos os sistemas externos ao duelo.

## Documentação completa

| Documento | Conteúdo |
|---|---|
| [Estado do projeto](docs/ESTADO-DO-PROJETO.md) | Base, evidências, estágio por função e fronteiras do que foi testado |
| [Arquitetura](docs/ARQUITETURA.md) | Responsabilidade de arquivos, fluxos, hooks e mapa de alterações |
| [Combate e balanceamento](docs/COMBATE-E-BALANCEAMENTO.md) | Ações, estados, fórmulas, custos, ferramentas, sinergias, HUD e riscos |
| [Brasão e Sacrifício](docs/BRASAO-E-SACRIFICIO.md) | Registro, herança, fio, puxão, pogo, dano, hazards, cooldowns |
| [Invocações e save](docs/INVOCACOES-E-SAVE.md) | Captura/commit, blacklist, golpes, visual, ordenação, schema JSON e slots |
| [Personalização e assets](docs/PERSONALIZACAO-E-ASSETS.md) | F8, paletas, máscaras, materiais de frame, packs, Silk Eclipse e limites |
| [Testes e limitações](docs/TESTES-E-LIMITACOES.md) | Matriz P0/P1/P2, logs e diferença entre build, stubs e teste no jogo |
| [Registro de validação](docs/VALIDACAO-BASELINE.md) | Resultados realmente executados na organização do baseline |
| [Roadmap](docs/ROADMAP.md) | Correções possíveis e evolução proposta, não promessa de funcionalidade |
| [Referências e créditos](docs/REFERENCIAS.md) | Projetos/fontes usados como estudo e situação dos assets |
| [Changelog](CHANGELOG.md) | Evolução relevante e mudanças de comportamento |
| [Contribuição](CONTRIBUTING.md) | Build, testes, empacotamento, PRs, relato de bugs e rollback |
| [Instruções para IA](AGENTS.md) | Contexto de continuidade, contratos, armadilhas e prompt reutilizável |
| [Avisos e direitos](NOTICE.md) | Dependências externas, arte e licença ainda não definida |

## Desenvolver com ou sem IA

O agente ou colaborador deve ler [AGENTS.md](AGENTS.md) e [CONTRIBUTING.md](CONTRIBUTING.md) antes de editar. O repositório contém código C# `net472`, testes isolados `net8.0`, scripts Bash/PowerShell e CI limitada a testes sem Unity.

```bash
bash scripts/test.sh
# Build completa requer assemblies locais autorizadas:
bash scripts/build-release.sh '/caminho/Managed' '/caminho/BepInEx/core'
```

A execução inicial do repositório passou pelos **10 testes de binding/recurso + 30 de poses**, pela compilação contra as assemblies fornecidas e pelo empacotamento Bash. Não há prova de gameplay no GitHub Actions. O código principal permanece igual ao release 0.7.25; esta organização adiciona documentação/testes/scripts, não um novo patch de mecânicas.

## Limitações em destaque

Skills e ferramentas que não causam redução de HP podem receber fallback genérico de `2 × nailDamage`; isso precisa de revisão para ferramentas de efeito. O segundo golpe do fio na exploração marca `IgnoreInvulnerable = true`, diferente dos fallbacks do combate que verificam invulnerabilidade. Catálogo/blacklist/sinergias dependem de nomes. Sidecar e JSON nativo não são transacionais; a rota string ainda depende de `profileID` atual, enquanto a tipada usa o slot explícito. A Silk Eclipse volta à arte original em ações não cobertas.

Esses pontos estão documentados como comportamento/risco atual, não como bugs todos reproduzidos nem como correções já feitas. Consulte o [Roadmap](docs/ROADMAP.md) antes de planejar alterações.
