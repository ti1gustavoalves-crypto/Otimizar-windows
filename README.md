# Otimizador de Desempenho para Windows

Aplicativo desktop em C# e Windows Forms para acompanhar recursos do computador, aplicar perfis reversíveis de desempenho e executar manutenção controlada.

## Recursos principais

- Monitoramento em tempo real de CPU, memória, armazenamento e processos.
- Histórico diário, semanal e mensal com retenção local de 30 dias.
- Benchmark antes e depois, concluído após reiniciar o Windows.
- Diagnóstico de discos, estabilidade, inicialização e recomendações.
- Limpeza selecionável de arquivos temporários e análise por volume.
- Otimização automática por tipo de unidade, escolhendo o método adequado para SSD, HDD ou armazenamento em camadas.
- Limpeza do WinSxS sem `ResetBase` e diagnóstico energético oficial do Windows.
- Acesso direto ao Sensor de Armazenamento para manutenção automática de espaço.
- Perfis de energia, tema escuro, efeitos visuais e aplicativos em segundo plano.
- Backup das configurações alteradas e restauração por seção.
- Quarentena reversível para arquivos duplicados.
- Testes de segurança executados em arquivos e Registro isolados.
- Instalador com atualização pelo GitHub, reparo, troca atômica e rollback.

## Requisitos

- Windows 10 ou 11 de 64 bits.
- Windows PowerShell 5.1 ou mais recente.
- .NET Framework disponível no Windows.

Algumas operações exigem privilégios de administrador. As leituras de temperatura dependem dos sensores expostos pelo fabricante; o adaptador para `LibreHardwareMonitorLib.dll` é opcional e a biblioteca não é distribuída neste repositório.

## Download

[Baixar a versão mais recente do instalador](https://raw.githubusercontent.com/ti1gustavoalves-crypto/Otimizar-windows/main/releases/InstalarOtimizadorDeDesempenho.exe)

O executável ainda não possui assinatura digital comercial, portanto o Windows SmartScreen pode solicitar confirmação na primeira execução.

## Compilar e testar

Abra o PowerShell na raiz do repositório e execute:

```powershell
powershell -ExecutionPolicy Bypass -File .\src\build-release.ps1 -OutputDirectory .\outputs
```

O processo compila a suíte de autotestes, exige aprovação de todos os testes e só então cria:

- `outputs/OtimizadorDeDesempenho.exe`
- `outputs/InstalarOtimizadorDeDesempenho.exe`
- manifestos, notas e resumo SHA-256 da versão

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

- O histórico e os relatórios permanecem no computador do usuário.
- Logs de falha removem automaticamente nome da conta, máquina e caminho do perfil.
- Downloads de atualização exigem HTTPS e verificação SHA-256.
- Processos protegidos e ambientes corporativos são tratados de forma conservadora.
- As otimizações mantêm um backup para restauração.

Antes de distribuir publicamente, assine os executáveis com um certificado confiável e publique o manifesto somente em uma origem HTTPS controlada.

As decisões técnicas e fontes oficiais usadas nas otimizações estão em [`docs/performance-research.md`](docs/performance-research.md).
