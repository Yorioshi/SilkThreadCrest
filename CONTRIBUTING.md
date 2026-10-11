# Como contribuir e continuar o desenvolvimento

Leia primeiro [AGENTS.md](AGENTS.md), mesmo se você não usa IA. Ele registra os contratos que evitam regressões de combate, saves e renderização. Para pedir uma alteração a outro agente, use o prompt reutilizável no final daquele arquivo.

## Preparar o ambiente

Você precisa de uma instalação autorizada de Silksong Windows, BepInEx compatível com esse runtime gerenciado e SDK .NET 8 (usado na validação). O projeto principal gera uma DLL **net472** e restaura `Microsoft.NETFramework.ReferenceAssemblies` pelo NuGet.

O repositório não contém DLLs do jogo nem BepInEx. As referências exigidas estão listadas em [`src/SilkThreadCrest.csproj`](src/SilkThreadCrest.csproj). Aponte `GameManagedDir` para `Hollow Knight Silksong_Data/Managed` e `BepInExCoreDir` para `BepInEx/core`. Não copie essas DLLs para GitHub.

```powershell
# Windows / PowerShell — substitua pelos caminhos locais reais
 dotnet build src/SilkThreadCrest.csproj -c Release `
   '-p:GameManagedDir=C:\Jogos\Hollow Knight Silksong\Hollow Knight Silksong_Data\Managed' `
   '-p:BepInExCoreDir=C:\Jogos\Hollow Knight Silksong\BepInEx\core'
```

```bash
# Linux — compilação apenas, com assemblies locais autorizadas
 dotnet build src/SilkThreadCrest.csproj -c Release \
   '-p:GameManagedDir=/caminho/Managed' \
   '-p:BepInExCoreDir=/caminho/BepInEx/core'
```

**Compilar no Linux não significa que o mod foi testado no jogo Linux/Proton.** O jogo Windows não foi executado no ambiente de desenvolvimento deste baseline. A versão exata do executável do jogo e a combinação exata de versões BepInEx/Harmony precisam ser registradas por cada testador; não há garantia genérica de compatibilidade.

## Testes que não exigem o jogo

```bash
bash scripts/test.sh
```

```powershell
./scripts/test.ps1
```

Há dez verificações do helper real de binding/leitura de recurso com modelos mínimos de materiais tk2d, e trinta verificações do seletor de poses. Elas comprovam lógica de referências, recurso embutido e seleção de poses, não shader, renderização, gameplay, persistência no processo real do jogo ou compatibilidade com todos os inimigos. O GitHub Actions executa **apenas esses testes isolados**.

## Criar pacotes

```bash
bash scripts/build-release.sh '/caminho/Managed' '/caminho/BepInEx/core'
```

```powershell
./scripts/build-release.ps1 -GameManagedDir 'C:\...\Managed' -BepInExCoreDir 'C:\...\BepInEx\core'
```

A saída é `artifacts/<versão>/`, contendo DLL, ZIP Windows e ZIP de fonte. As dependências proprietárias não são incluídas. O script Bash foi executado no ambiente de desenvolvimento; **os scripts PowerShell devem ser validados em Windows**. O ZIP Windows é o instalável; o ZIP automático “Source code” do GitHub é fonte, não um pacote BepInEx pronto.

## Procedimento de mudança

Trabalhe numa branch: `git switch -c fix/nome-curto`. Indique no PR o problema, hipótese, comportamento anterior/novo, os arquivos alterados e a evidência obtida. Evite misturar refatoração, rebalanceamento e skins no mesmo patch.

Sempre que mudar custo/dano/estado de turno, atualize [combate e balanceamento](docs/COMBATE-E-BALANCEAMENTO.md). Se mudar schema de save, documente migração e recuperação em [invocações e save](docs/INVOCACOES-E-SAVE.md). Alteração de aparência deve preservar os arquivos de gameplay quando não houver pedido explícito para mudá-los.

Use o checklist de [testes e limitações](docs/TESTES-E-LIMITACOES.md). Teste pelo menos uma instalação limpa e a atualização de uma instalação existente com config/sidecar. Guarde cópia de segurança do save antes de testar alterações de persistência. Não automatize operações destrutivas sobre saves pessoais.

## Versão, documentação e release

A versão funcional está em `SilkThreadCrestPlugin.PluginVersion`. Neste baseline não há versão separada de pacote NuGet. Atualize a versão quando houver mudança de comportamento, registre no `CHANGELOG.md` e documente a situação real: “implementado”, “testado isoladamente”, “confirmado pelo usuário em teste pontual”, “pendente de validação in-game”. Não use percentuais de conclusão sem critérios.

Antes da entrega, confira que o título F8 e o log `Appearance runtime` correspondem à DLL instalada. Mantenha uma única DLL do mesmo GUID. Duplicatas podem causar registro/patch duplo e dificultar diagnóstico.

Para compartilhar arte de terceiros, confira licença e autorização de redistribuição específicas; links de referência não significam que seus assets estão incluídos. [NOTICE.md](NOTICE.md) registra a situação atual. O código original do projeto está sob [MIT](LICENSE); mantenha o aviso de copyright e a licença nas redistribuições. Não relicencie contribuições ou assets de terceiros automaticamente.

## Relatar falhas

Informe versão do plugin, jogo/BepInEx, outros mods, slot envolvido (sem publicar o save), passos de reprodução, resultado esperado/obtido, frequência e capturas. Recorte as linhas relevantes do log e remova nomes de usuário, caminhos pessoais e segredos. Para aparência, inclua `Appearance runtime`, `Appearance binding` e `Silk Eclipse sync`. Para invocações, inclua `learned summon`, `wrote the summon sidecar`, `restored` e erros de patches de save.

## Rollback

Feche o jogo e restaure a DLL anterior conhecida como funcional. Não apague `appearance.json`, sidecars ou saves como procedimento padrão. Se um schema novo foi gravado, use a cópia prévia adequada e siga a migração documentada; não presuma que trocar só a DLL reverte dados de save.
