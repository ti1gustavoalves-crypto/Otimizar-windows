# Pesquisa de desempenho e critérios de segurança

Este documento registra as fontes oficiais e os critérios usados para selecionar otimizações. O objetivo é melhorar responsividade e espaço livre sem desativar segurança, atualizações ou recursos essenciais do Windows.

## Ações implementadas

### Otimização por tipo de unidade

O comando `defrag <unidade> /O` deixa o Windows escolher a operação adequada ao tipo de mídia. Em SSDs, o sistema pode executar retrim; em HDDs, análise e desfragmentação; em armazenamento em camadas, a operação correspondente.

Fonte: [defrag — Microsoft Learn](https://learn.microsoft.com/windows-server/administration/windows-commands/defrag)

### Limpeza do repositório de componentes

`DISM /Online /Cleanup-Image /StartComponentCleanup` remove componentes substituídos e pode recuperar espaço no disco do sistema. O projeto não usa `/ResetBase`, pois essa opção impede a desinstalação de atualizações instaladas.

Fonte: [DISM servicing command-line options — Microsoft Learn](https://learn.microsoft.com/windows-hardware/manufacture/desktop/dism-operating-system-package-servicing-command-line-options?view=windows-11)

### Diagnóstico energético

`powercfg /energy` observa o sistema e cria um relatório com problemas de energia, drivers, temporizadores e atividade que podem afetar eficiência ou desempenho. A versão atual usa uma observação de 15 segundos e mantém o relatório apenas no computador.

Fonte: [Powercfg command-line options — Microsoft Learn](https://learn.microsoft.com/windows-hardware/design/device-experiences/powercfg-command-line-options)

### Sensor de Armazenamento

O Sensor de Armazenamento automatiza a remoção de arquivos desnecessários e ajuda a evitar pouco espaço livre, condição que pode prejudicar desempenho e impedir atualizações. O projeto abre a configuração oficial do Windows e não impõe uma política silenciosamente.

Fonte: [Configure Storage Sense in Windows — Microsoft Learn](https://learn.microsoft.com/windows/configuration/storage/storage-sense)

### Práticas gerais

Inicialização, aplicativos em segundo plano, espaço livre, efeitos visuais, modo de energia e monitoramento de processos seguem as recomendações gerais da Microsoft para Windows 10 e 11.

Fonte: [Dicas para melhorar o desempenho do PC no Windows — Suporte Microsoft](https://support.microsoft.com/pt-br/windows/experience/performance-optimization/tips-to-improve-pc-performance-in-windows)

## Ajustes não aplicados automaticamente

- Desativar Microsoft Defender, Windows Update, firewall ou isolamento de núcleo.
- Desativar indiscriminadamente SysMain, Pesquisa do Windows ou serviços do sistema.
- Remover ou limitar OneDrive, Intune, Veeam e componentes corporativos.
- Fixar arquivo de paginação, desativar compactação de memória ou alterar parâmetros de rede sem diagnóstico.
- Usar `/ResetBase` na limpeza do WinSxS.
- Aplicar prioridade alta a processos ou alterar afinidade de CPU de forma permanente.

Essas alterações podem reduzir segurança, estabilidade, capacidade de recuperação ou desempenho em cargas específicas. Por isso, não fazem parte do perfil automático.

## Possível evolução

O Windows oferece EcoQoS por processo por meio de `SetProcessInformation`. Uma futura implementação deve ser temporária, explícita e limitada a processos escolhidos pelo usuário; ela não deve atingir aplicações visíveis, áudio ou processos protegidos.

Fonte: [Quality of Service — Microsoft Learn](https://learn.microsoft.com/windows/win32/procthread/quality-of-service)

