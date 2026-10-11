# Changelog

Este histórico resume a linha atual do mod a partir dos registros de desenvolvimento; não recria commits anteriores nem inclui experimentos descartados. A primeira revisão Git deste repositório contém o baseline **0.7.25**, não uma sequência de commits de todas as versões.

## 0.7.25 — sincronização e escala da Silk Eclipse

Seleção de poses baseada no animator corporal real, clip e frame tk2d. Referência de escala no primeiro frame Idle nativo, pivôs/âncoras e orientação das poses de caminhada ajustados. Ações sem pose adequada usam arte original. A folha continua com oito poses, não todas as animações. Trinta verificações isoladas do seletor e build passaram; confirmação in-game visual pendente. [Nota detalhada](docs/CHANGELOG-0.7.25.md).

## 0.7.24 — aplicação visual confirmada

Binding dos materiais de cada definição tk2d (`material` e `materialInst`), preferência pela coleção de runtime, máscaras aproximadas por cor, leitura externa/embutida testável e diagnóstico da assembly/renderers. O usuário confirmou que a personalização passou a ser aplicada. A skin Eclipse foi aplicada, mas tamanho/animações ainda ficaram incorretos. Dez verificações isoladas de binding/recurso. [Nota detalhada](docs/CHANGELOG-0.7.24.md).

## 0.7.16–0.7.23 — customizador e pipeline de aparência

Menu interno F8, presets, paleta personalizada, packs PNG por nomes e tema preto/vermelho/branco. Correções de inicialização segura de GUI, isolamento de falhas visuais, grade/rolagem, controle de cursor e paleta por partes. Integração da folha própria Silk Eclipse e recurso embutido. Várias tentativas intermediárias de aparência não alteravam o renderer ativo; não devem ser tratadas como builds visuais validadas. O marco de aplicação confirmada é 0.7.24.

## 0.7.12–0.7.15 — persistência de invocações

Correções do hook do caminho real de gravação JSON, método genérico fechado, sidecar por slot, commit antecipado no golpe fatal e carregamento por overload tipado com slot explícito. O usuário confirmou persistência na **0.7.15**. O código atual não fixa o slot 2; todos os slots e rotas ainda merecem a matriz de testes.

## 0.7.11 — ferramentas e passivas

Ferramentas vermelhas equipadas na lista de ações, estoque fabricado lido do item e uso pela rotina nativa. Gate de dano para passivas/ferramentas persistentes: uma ativação por ferramenta por turno da Hornet.

## 0.7.9–0.7.10 — balanceamento e carregamento

Defesa corta 50% do dano com arredondamento/mínimo definidos no código; sinergias de custo e seda, custo de repetição do Sacrifício, ordenação por dano, blacklist de hitboxes passivas e proteção de fallback contra invulnerabilidade. Correção de instalação tolerante por classe de patch e busca direta do alvo no comando, evitando que falha opcional de save impedisse o combate.

## 0.7.6–0.7.8 — invocações

Indicador de turno só texto destacado. Inimigos derrotados passam a fornecer golpes catalogados, menu Invocar, custo base 3 seda, aparição visual e ação normal. Snapshot antes da destruição nativa e confirmação na vitória. Fallback de dano da invocação quando `Hit` é rejeitado, com a proteção atual detalhada na documentação.

## 0.7.0–0.7.5 — interface e linha estável de combate

HUD preto/vermelho/branco, janelas de mensagens, transições e estado de vida/seda. Wipe restrito a entrada/saída, mensagem de golpe fatal, prompts integrados, brasão equipado, início apenas no chão e bindings reais. HP do inimigo acima dele e composição superior central. O indicador de turno atual é texto, não retratos.

## Linha 0.6.x — origem

Base Brasão do Fio e Sacrifício com duelo por turnos. O seletor de brasões passou a ser próprio do mod após o inventário nativo não abrir durante o duelo. O registro atual ainda herda configurações de Hunter e tem placeholders visuais.

## Organização inicial no GitHub

Sem alterar os arquivos C# do baseline: documentação completa por módulo, `AGENTS.md`, contribuição, mapa de arquitetura, estágios/limitações, roadmap, avisos de direitos, scripts de teste/build, testes portáveis e CI isolada. Release do baseline e snapshot de fonte para continuidade. Não é um patch adicional de gameplay.
