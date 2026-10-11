# SilkThreadCrest 0.7.25 — escala e sincronização da Silk Eclipse

A personalização funcional da 0.7.24 foi mantida. Esta atualização altera a apresentação da Silk Eclipse, sem mudar combate, dano, seda, ferramentas ou persistência de invocações.

## Correções

O visual agora acompanha o animator corporal real (`HeroController.AnimCtrl.animator`), usando o nome do clip e o `CurrentFrame` do tk2d. Foi removida a alternância independente por posição/temporizador e a tentativa incorreta de detectar ataque ou cura pelo nome do objeto de um Animator.

A escala é calibrada pela geometria do primeiro frame Idle nativo da Hornet, não pelo tamanho de um renderer arbitrário, de um golpe ou de um efeito. Essa referência fica fixa durante a utilização da skin. A largura corporal ignora parte da agulha da arte, e os pivôs de cada pose procuram alinhar o centro do corpo, não o centro do retângulo da arma/efeito. A posição acompanha o transform do sprite corporal, não um deslocamento fixo do GameObject raiz.

Os frames de caminhada são normalizados quanto à orientação: a folha mistura um personagem voltado para a esquerda e outro para a direita. A direção exibida usa `cState.facingRight` e o flip do sprite corporal. A altura não muda arbitrariamente quando o ataque aumenta os bounds do renderer.

Para ocultar o original, o mod usa `forceRenderingOff` apenas no renderer corporal, mantendo seu `enabled` e a atualização normal da animação/mesh. Efeitos nativos continuam ativos. Ao voltar para Original ou sair da skin, a flag anterior é restaurada.

## Cobertura e limite importante

A folha atual **continua tendo oito poses**, e não centenas de frames equivalentes ao jogo. Idle e caminhada escolhem as duas poses conforme o progresso do clip nativo; salto, queda e cura usam suas poses disponíveis. O ataque simples usa a pose de golpe somente na faixa central do clip; antecipação e recuperação voltam à animação nativa.

Dash, ataque vertical, pogo/downspike, parede, ferramentas, morte, dano, cutscenes e outras ações não cobertas usam a **arte original da Hornet**. Isso evita substituir uma animação importante por um idle ou ataque genérico, mas produz uma troca visual temporária entre a skin e a arte nativa. Portanto, esta versão não deve ser tratada como uma skin completa frame a frame. Para manter a Silk Eclipse em todas essas ações com fidelidade, ainda será necessário desenhar frames correspondentes e mapear os atlases completos.

Os pivôs e a proporção corporal foram ajustados a partir da folha existente. O alinhamento perfeito de cada pose precisa ser confirmado no jogo; ataques muito horizontais e efeitos de cura podem requerer ajuste artístico adicional.

## Validação e teste

Foram feitos 30 testes isolados do seletor de poses, incluindo clips reais encontrados na assembly do jogo, início/meio/fim do ataque, ações não cobertas e índices fora do intervalo. Compilação principal e compilação limpa do pacote de fonte são realizadas contra as assemblies fornecidas. A renderização no Silksong Windows não pode ser executada neste ambiente.

Após instalar, confirme **0.7.25** no título F8. Selecione Silk Eclipse e teste parado, caminhar para os dois lados, saltar, cair, atacar e curar. Confira também que dash/pogo/parede mostram a animação original, sem parar a mecânica. O log `Silk Eclipse sync` informa o clip, frame nativo, pose escolhida, modo skin/native fallback e altura Idle usada como referência. Se houver desalinhamento, envie uma captura e essas linhas do log.
