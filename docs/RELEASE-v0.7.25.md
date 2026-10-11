# SilkThreadCrest 0.7.25 — baseline de continuidade

Baseline do mod organizado com fonte completa, arte Silk Eclipse, documentação de todos os módulos, instruções para colaboradores/agentes de IA, testes isolados e scripts portáveis. **Nenhuma mudança de mecânica foi feita para criar o repositório.**

## Baixar

- `SilkThreadCrest-0.7.25-Windows.zip`: pacote instalável em uma instalação BepInEx já preparada.
- `SilkThreadCrest-0.7.25.dll`: DLL avulsa com a folha Silk Eclipse embutida.
- `SilkThreadCrest-0.7.25-Source.zip`: fonte, documentos, arte e testes; sem assemblies proprietárias.

Leia `README.md` para instalação, `AGENTS.md` para continuar com qualquer agente de IA, e `CONTRIBUTING.md` para build/testes. Os downloads automáticos “Source code” do GitHub são fonte, não instaláveis.

## Funcionalidades

Combate por turnos na sala atual, início somente no chão, ações/skills/ferramentas com estoque nativo, defesa/cura, sinergias, troca limitada de brasões, invocações com golpes catalogados e save nativo/sidecar por slot, HUD temático, Brasão do Fio/Sacrifício, menu F8/paletas/packs e Silk Eclipse experimental.

## Evidência e limitações

Build passou com zero avisos/erros, dez verificações de binding/recurso e trinta de poses passaram, ZIPs foram validados. Os testes são isolados, sem jogo/Unity renderizado. Aplicação de paletas/skin foi confirmada na 0.7.24; a correção de escala/sincronização da Silk Eclipse **0.7.25 ainda aguarda confirmação in-game**. A folha tem oito poses; ações não cobertas usam arte original.

Catálogo de ataques, sinergias e packs usam heurísticas de nomes/cores; não há validação de todos os inimigos/bosses/ferramentas. Slots/novo jogo/corrupção precisam da matriz manual. Consulte `docs/ESTADO-DO-PROJETO.md`, `docs/TESTES-E-LIMITACOES.md` e `docs/ROADMAP.md`.

## Dados e rollback

Feche o jogo, faça backup dos saves antes de testar persistência e mantenha uma única DLL do mesmo GUID. Não apague `appearance.json` ou `summons-slot-N.json` para atualizar. Rollback normal é restaurar a DLL anterior conhecida como funcional; mudanças de schema exigem migração/backup específico.

O código original, a documentação e os scripts estão sob MIT; consulte `LICENSE` e `NOTICE.md` para direitos e dependências não incluídas. Mantenha os avisos nas redistribuições.

## Atualização de distribuição e licença

Repositório tornado público em 11 de outubro de 2026 a pedido do proprietário. `LICENSE` MIT e `NOTICE.md` estão incluídos nos ZIPs atualizados de instalação/fonte; a licença também pode ser baixada separadamente nos assets deste release. O código de gameplay continua sendo 0.7.25 e a tag original de baseline foi preservada. O branch `main` contém as atualizações de documentação/licença posteriores ao baseline.
