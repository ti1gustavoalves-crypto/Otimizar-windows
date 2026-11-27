# Validação de alterações reais em máquina virtual

Os autotestes usam pastas isoladas, processos de teste, transferência simulada e validação de entradas. Eles não executam reparos no Windows da máquina de desenvolvimento. Não foi encontrado Hyper-V, VirtualBox ou VMware disponível neste ambiente; os cenários destrutivos abaixo permanecem pendentes de uma VM descartável.

Use Windows 10/11 x64, snapshot anterior à instalação e arquivos descartáveis. Teste como administrador e com a Proteção do Sistema habilitada. Não use a máquina corporativa para estes cenários.

| Cenário | Resultado esperado |
| --- | --- |
| Diagnóstico e cancelamento | Nenhuma configuração alterada; resultado completo ou cancelado, sem apresentar dados parciais como conclusão. |
| Proteção do Sistema desabilitada | Reparo bloqueado antes da alteração e motivo registrado. |
| Ponto recente / limite do Windows | Sem confirmação de novo ponto, reparo bloqueado; nenhum ajuste automático de política. |
| Reparo com novo ponto confirmado | Registro em Dados/Protecao precede a alteração. Conferir a existência do ponto no Windows. |
| Proxy com PAC/servidor fictícios | Revisão explícita; backup dos quatro valores; WinHTTP e servidores DNS preservados. |
| Desfazer proxy | Valores e ausência original restaurados. Alteração posterior, outro usuário ou outro computador bloqueiam o desfazer automático. |
| Reparo de navegadores interrompido por erro | Falha indicada; backups e lista das alterações já feitas preservados. |
| NX, drivers, quarentena e configurações | Comparar estado antes/depois e testar a restauração própria de cada ferramenta. |
| Instalação em lote cancelada | Aguardar o instalador corrente; não iniciar o próximo. Sites e instalações assistidas aparecem como pendência manual. |
| Fila e atualização | Apenas uma operação ativa; pedido de atualização cancela pendências e aguarda a ativa. |
| Atualização 0.3.3 para 0.4.0 | Dados, preferências e backups preservados; versão anterior mantida; falha na troca restaura a versão anterior. |
| Portátil | Dados permanecem junto ao EXE; atualização abre a versão ZIP no GitHub sem executar o instalador. |

Um ponto de restauração não substitui backup de arquivos. Limpeza definitiva, reset de Winsock e instaladores de terceiros não têm reversão granular universal. Revisar esses casos antes de executar; a central distingue restauração direta, ferramenta específica e orientação manual.
