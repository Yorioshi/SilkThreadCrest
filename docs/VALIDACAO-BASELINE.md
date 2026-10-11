# Registro de validação do baseline GitHub

**Data: 11 de outubro de 2026. Versão de gameplay: 0.7.25.** A preparação do repositório não altera o código C# do baseline já entregue. Os arquivos C# foram comparados byte a byte com a fonte original 0.7.25.

| Verificação realizada | Resultado | Limite |
|---|---|---|
| Comparação dos arquivos C# | Idênticos à fonte 0.7.25 | Não substitui teste de gameplay |
| Material binding e recurso | 10 verificações passaram | Stubs mínimos, sem renderização Unity |
| Seleção de poses | 30 verificações passaram | Sem animator, renderer, escala ou cena real |
| Build do plugin | Sucesso, 0 avisos e 0 erros | Contra assemblies locais fornecidas; não prova execução Windows |
| Script Bash de release | Produziu DLL, ZIP Windows e ZIP fonte | PowerShell ainda não executado em Windows |
| Integridade dos ZIPs | Teste de compressão sem erros | Não confirma instalação no jogo |
| Links/documentos | Verificação de arquivos locais e revisão de caminhos | Fontes externas históricas não foram todas revalidadas nesta entrega |

Os resultados do GitHub Actions ficam registrados na aba Actions, separadamente. A CI executa só os dois projetos isolados; não há DLLs proprietárias/Unity para executar ou compilar o jogo no runner.

## Pendências explícitas

Silksong Windows não foi executado no ambiente de preparação. A aparência final da Silk Eclipse 0.7.25, pivôs/escala/transições e a repetição completa da matriz P0 no jogo continuam pendentes. Também estão pendentes: execução dos scripts PowerShell em Windows, validação universal de ferramentas/skills/inimigos/bosses/hazards, todos os slots/rotas de save, compatibilidade com outros mods, benchmarks e múltiplas resoluções/dispositivos.

Confirmações pontuais do usuário estão em [Estado do projeto](ESTADO-DO-PROJETO.md). Para repetir a validação, siga [Contribuição](../CONTRIBUTING.md) e [Testes e limitações](TESTES-E-LIMITACOES.md). Ao testar no jogo, acrescente versão do executável/BepInEx/Harmony, cenário, passos, log recortado/captura e resultado, sem dados pessoais.
