# Otimizador de Desempenho para Windows

Aplicativo desktop em C# e Windows Forms para acompanhar recursos do computador, aplicar perfis reversíveis de desempenho e executar manutenção controlada.

## Recursos principais

- Monitoramento em tempo real de CPU, memória, armazenamento e processos.
- Histórico diário, semanal e mensal com retenção local de 30 dias.
- Benchmark antes e depois, concluído após reiniciar o Windows.
- Diagnóstico de discos, estabilidade, inicialização e recomendações.
- Limpeza selecionável de arquivos temporários e análise por volume.
- Perfis de energia, tema escuro, efeitos visuais e aplicativos em segundo plano.
- Backup das configurações alteradas e restauração por seção.
- Quarentena reversível para arquivos duplicados.
- Testes de segurança executados em arquivos e Registro isolados.
- Instalador com atualização, reparo, troca atômica e rollback.

## Requisitos

- Windows 10 ou 11 de 64 bits.
- Windows PowerShell 5.1 ou mais recente.
- .NET Framework disponível no Windows.

Algumas operações exigem privilégios de administrador. As leituras de temperatura dependem dos sensores expostos pelo fabricante; o adaptador para `LibreHardwareMonitorLib.dll` é opcional e a biblioteca não é distribuída neste repositório.

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

O pipeline aceita um certificado de assinatura de código instalado no usuário atual e um endereço HTTPS para o canal de atualização:

```powershell
powershell -ExecutionPolicy Bypass -File .\src\build-release.ps1 `
  -OutputDirectory .\outputs `
  -UpdateBaseUrl "https://downloads.exemplo.com/otimizador" `
  -CertificateThumbprint "SEU_THUMBPRINT"
```

Sem esses parâmetros, a compilação continua funcional, mas os executáveis ficam sem assinatura e o canal público de atualização permanece desativado.

## Privacidade e segurança

- O histórico e os relatórios permanecem no computador do usuário.
- Logs de falha removem automaticamente nome da conta, máquina e caminho do perfil.
- Downloads de atualização exigem HTTPS e verificação SHA-256.
- Processos protegidos e ambientes corporativos são tratados de forma conservadora.
- As otimizações mantêm um backup para restauração.

Antes de distribuir publicamente, assine os executáveis com um certificado confiável e publique o manifesto somente em uma origem HTTPS controlada.

