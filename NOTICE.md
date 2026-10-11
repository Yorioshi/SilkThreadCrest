# Avisos, dependências e direitos

SilkThreadCrest é um projeto de modificação não oficial. Hollow Knight, Silksong, Hornet e os assets nativos do jogo pertencem aos respectivos titulares, incluindo Team Cherry. Este repositório não distribui o jogo nem suas DLLs, texturas originais ou saves de usuários. Os exemplos de código fazem referência às APIs da instalação local do jogador.

## Licença geral ainda não definida

O proprietário ainda não escolheu uma licença geral para o código e a arte própria do projeto. **Não presumir MIT, GPL, domínio público ou autorização irrestrita de redistribuição.** O acesso privado permite colaborar nos termos autorizados pelo proprietário; para publicar forks, distribuir alterações ou tornar este conteúdo público, esclareça a licença e as permissões necessárias.

Este arquivo é um aviso de situação, não uma licença aberta. Nenhuma licença nova foi aplicada automaticamente nesta entrega.

## Dependências de terceiros

BepInEx, Harmony, Newtonsoft.Json, Unity, TeamCherry.TK2D/PlayMaker e outras assemblies são utilizadas por referência. Seus direitos/licenças são os dos respectivos projetos/produtos. As DLLs locais não são incluídas no código-fonte nem nos pacotes do mod aqui preparados. O NuGet restaura `Microsoft.NETFramework.ReferenceAssemblies` para build; esse pacote tem seus próprios termos.

GitHub Actions e os scripts são auxiliares de build/teste; não fornecem autorização para distribuir dependências proprietárias ou arquivos de contas.

## Assets próprios e referências

A folha `Skins/SilkEclipse/Texture2D/silk_eclipse_sheet.png` foi gerada para este projeto e teve ajustes determinísticos de recorte/orientação no desenvolvimento. Não foi copiada de um pack Nexus/Persona/CustomKnight. A folha contém oito poses; a origem por geração de imagem e a cobertura limitada são explicitadas, sem alegar arte manual completa ou exclusividade jurídica.

Os projetos/vídeos em [Referências](docs/REFERENCIAS.md) foram usados para estudo do fluxo de skins, interfaces e ferramentas. Isso não significa que seus assets, executáveis ou código integral foram incorporados nem que uma referência visual autoriza redistribuir recursos de terceiros.

Os ícones placeholder reutilizados pelo plugin vêm de recursos já existentes na instalação do jogo e são copiados em memória. Nenhum dump desses recursos foi adicionado ao repositório. Antes de incluir novos packs externos ou trechos licenciados, registre origem, licença específica, escopo e atribuição exigida.

## Dados que não pertencem ao repositório

Credenciais, variáveis de ambiente, tokens, arquivos de save, sidecars pessoais, `appearance.json`, configurações BepInEx pessoais e logs completos devem permanecer fora do Git. A `.gitignore` cobre padrões usuais, mas não substitui revisão dos arquivos antes de commit ou anexos de issue.
