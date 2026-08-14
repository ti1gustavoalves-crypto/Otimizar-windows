# Otimizador de Desempenho para Windows

Aplicativo desktop em C# e Windows Forms para acompanhar recursos do computador, aplicar perfis reversíveis de desempenho e executar manutenção controlada.

A interface apresenta identidade visual própria, barra de título escura e cinco áreas organizadas para reduzir cliques durante a manutenção técnica.

A versão 5.3.0 simplifica a navegação durante o atendimento: remove identificação repetida, amplia a área útil e mantém somente contexto operacional relevante.

## Recursos principais

- Monitoramento em tempo real de CPU, memória, armazenamento e processos.
- Índice de saúde de 0 a 100 calculado a partir de recursos, estabilidade, armazenamento, inicialização e atualizações pendentes.
- Identificação da causa mais provável de lentidão com base nas métricas atuais e no histórico disponível.
- Atendimento guiado para manutenção preventiva, PC lento, pouco espaço e inicialização lenta.
- Atendimento técnico em quatro etapas: diagnóstico inicial, correções selecionadas, busca de atualizações e verificação final.
- Central de pendências com ações críticas, recomendadas, opcionais e informativas.
- Elevação administrativa antecipada e única para o conjunto selecionado.
- Comparação automática de CPU, memória e espaço antes e depois do atendimento.
- Diagnóstico acionável de discos, estabilidade, inicialização, energia e recomendações.
- Verificação rápida de espaço, saúde dos discos, estabilidade, reinicialização pendente, dispositivos e serviços essenciais.
- Verificação profunda somente para diagnóstico com DISM, SFC e CHKDSK, seguida de reparo opcional e confirmado pelos mecanismos oficiais do Windows.
- Inicialização completa com entradas do usuário, computador, pastas e aplicativos da Microsoft Store.
- Inventário de versões de vídeo, BIOS, firmware, chipset e demais drivers importantes.
- Busca e instalação de drivers oficiais pelo Windows Update.
- Atalhos seguros para o suporte oficial do fabricante de cada atualização encontrada.
- Correspondência por Hardware ID, classificação e comparação segura de versões.
- Backup e restauração de drivers, diagnóstico de dispositivos e proteção especial para BIOS/firmware.
- Central unificada de atualizações para Windows e drivers pelo Windows Update e aplicativos pelo WinGet, sempre com confirmação prévia.
- Limpeza selecionável de arquivos temporários e análise por volume.
- Exclusão selecionável de arquivos e pastas para a Lixeira, com proteção de locais críticos.
- Otimização automática por tipo de unidade, escolhendo o método adequado para SSD, HDD ou armazenamento em camadas.
- Limpeza do WinSxS sem `ResetBase` e diagnóstico energético oficial do Windows.
- Acesso direto ao Sensor de Armazenamento para manutenção automática de espaço.
- Relatórios técnicos gerados automaticamente pelos fluxos de manutenção.
- Pesquisa e filtros para inicialização, armazenamento, drivers e programas.
- Cache inteligente para hardware, diagnósticos, armazenamento, drivers e programas, invalidado após alterações relevantes.
- Perfis de energia, tema escuro, efeitos visuais e aplicativos em segundo plano.
- Recuperação centralizada das configurações alteradas, restauração por seção e quarentena reversível.
- Quarentena reversível para arquivos duplicados.
- Atualização rápida pelo GitHub, com cache SHA-256, progresso de download, troca atômica, reinicialização automática e rollback.
- Rota alternativa automática pela API oficial do GitHub quando o domínio de conteúdo bruto não estiver acessível pela rede.
- Executável portátil que mantém dados e relatórios na própria pasta.

## Requisitos

- Windows 10 ou 11 de 64 bits.
- Windows PowerShell 5.1 ou mais recente.
- .NET Framework disponível no Windows.
- Windows Package Manager (WinGet) para a área de atualização de programas.

Algumas operações exigem privilégios de administrador. As leituras de temperatura dependem dos sensores expostos pelo fabricante; o adaptador para `LibreHardwareMonitorLib.dll` é opcional e a biblioteca não é distribuída neste repositório.

## Download

[Baixar a versão mais recente do instalador](https://raw.githubusercontent.com/ti1gustavoalves-crypto/Otimizar-windows/main/releases/InstalarOtimizadorDeDesempenho.exe)

[Baixar a versão portátil](https://raw.githubusercontent.com/ti1gustavoalves-crypto/Otimizar-windows/main/releases/OtimizadorDeDesempenho-Portatil.exe)

No modo portátil, mantenha o executável em uma pasta gravável. O programa criará `Dados do Otimizador` ao lado dele para armazenar relatórios, logs, configurações, quarentena e backups.

O executável ainda não possui assinatura digital comercial, portanto o Windows SmartScreen pode solicitar confirmação na primeira execução.

## Compilar e testar

Abra o PowerShell na raiz do repositório e execute:

```powershell
powershell -ExecutionPolicy Bypass -File .\src\build-release.ps1 -OutputDirectory .\outputs
```

O processo compila a suíte de autotestes, exige aprovação de todos os testes e só então cria:

- `outputs/OtimizadorDeDesempenho.exe`
- `outputs/OtimizadorDeDesempenho-Portatil.exe`
- `outputs/InstalarOtimizadorDeDesempenho.exe`
- manifestos, notas e resumo SHA-256 da versão

Os testes incluem regras de saúde e diagnóstico, validade do cache e validação da interface real em 1024 × 680, 1260 × 760 e 1600 × 900.

Os artefatos gerados não são versionados.

## Publicação assinada

Por padrão, o pipeline publica um manifesto compatível com o canal oficial hospedado em `releases/` neste repositório. O endereço pode ser substituído, e um certificado de assinatura instalado no usuário atual pode ser informado:

```powershell
powershell -ExecutionPolicy Bypass -File .\src\build-release.ps1 `
  -OutputDirectory .\outputs `
  -UpdateBaseUrl "https://downloads.exemplo.com/otimizador" `
  -CertificateThumbprint "SEU_THUMBPRINT"
```

Sem o certificado, a compilação continua funcional e o canal do GitHub permanece ativo, mas os executáveis ficam sem assinatura digital.

## Privacidade e segurança

- Os relatórios permanecem no computador do usuário.
- Logs de falha removem automaticamente nome da conta, máquina e caminho do perfil.
- Downloads de atualização exigem HTTPS e verificação SHA-256.
- Pacotes já baixados somente são reutilizados quando o SHA-256 continua idêntico ao manifesto oficial.
- Processos protegidos e ambientes corporativos são tratados de forma conservadora.
- As otimizações mantêm um backup para restauração.

Antes de distribuir publicamente, assine os executáveis com um certificado confiável e publique o manifesto somente em uma origem HTTPS controlada.

As decisões técnicas e fontes oficiais usadas nas otimizações estão em [`docs/performance-research.md`](docs/performance-research.md).
