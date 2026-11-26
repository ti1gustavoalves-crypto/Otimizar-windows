# Frankenstein TI

Central modular de suporte para Windows 10/11 x64. A versão 0.2.0 reúne 12 módulos, instalador por usuário, histórico local e cinco temas: Frankenstein, Minecraft, Gamer, Tecnologia e Claro.

[Baixar o instalador 0.2.0](https://github.com/ti1gustavoalves-crypto/Otimizar-windows/releases/download/frankenstein-v0.2.0/InstalarFrankensteinTI.exe) · [Página da versão](https://github.com/ti1gustavoalves-crypto/Otimizar-windows/releases/tag/frankenstein-v0.2.0)

## Instalar e atualizar

Execute `InstalarFrankensteinTI.exe`. A instalação fica em `%LOCALAPPDATA%\Programs\FrankensteinTI`, com atalho no menu Iniciar e opção de atalho na Área de Trabalho. A central aparece na lista de aplicativos instalados do Windows.

Escolha o tema no canto inferior esquerdo. O botão **Atualizações** consulta o canal exclusivo do Frankenstein. A abertura da central também verifica a disponibilidade, sem instalar silenciosamente. O download exige HTTPS, origem/versionamento esperados, tamanho e SHA-256 corretos antes de abrir o instalador.

O canal fica em `frankenstein/releases/update-manifest.json`. Ele não altera o canal do Otimizador de Desempenho. A versão atual do instalador não possui assinatura Authenticode; hashes não substituem assinatura digital. A verificação não ignora bloqueios de segurança do Windows.

## Dados e recuperação

- `Dados/Execucoes`: histórico, relatórios e backups da central.
- `Dados/interface.json`: tema escolhido.
- `App/modules/optimizer/Dados do Otimizador`: dados próprios do módulo original, copiados para a nova versão durante atualizações.
- `previous-*`: cópias da aplicação anterior. A troca malsucedida restaura a aplicação anterior.

A desinstalação remove a aplicação ativa e os atalhos, preserva os dados e as versões anteriores e arquiva os dados do otimizador em `Dados`. A desinstalação não desfaz reparos ou programas instalados pelos módulos. Antes de atualizar, conclua os módulos em execução e feche a central.

## Módulos

Diagnóstico inicial, otimizador completo, central de instalação, instalador de programas, reparo de apps, Navegador SOS, reparo avançado de navegadores, três ações de licença NX, instalação assistida do LabX e diagnóstico do vínculo corporativo.

Os programas legados mantêm interfaces próprias. O encerramento de um executável não comprova o sucesso de reparos. Procedimentos de instalação/reparo devem ser avaliados em máquinas de teste. A limpeza total de inscrições corporativas continua somente como referência.

## Compilar e testar

Na pasta `frankenstein`, usando Windows PowerShell 5.1 e .NET Framework 4.8:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-Release.ps1
```

O build usa `../src` para o otimizador e `Originais` para as ferramentas fornecidas. Executa os testes da central, dos módulos, do otimizador e de distribuição antes de gerar o instalador, manifesto e checksum em `releases`. Não distribua a pasta `build`, que contém fixtures e dados de teste.

## Publicar uma próxima versão

1. Atualize a versão da central e do instalador, além dos textos/notas correspondentes.
2. Execute `Build-Release.ps1` e revise os resultados.
3. Faça commit dos fontes e de `releases/update-manifest.json` e `releases/SHA256SUMS.txt`.
4. Crie a tag `frankenstein-vX.Y.Z` e uma Release com o ativo `InstalarFrankensteinTI.exe`, exatamente com esse nome.
5. Envie a branch `main` com o manifesto correspondente; confira publicamente a URL e o SHA-256 do ativo antes de anunciar a atualização.

Os binários de distribuição ficam como ativos da Release. Não reutilize uma tag publicada com conteúdo diferente. Não altere `releases/update-manifest.public.json` da raiz: ele pertence ao otimizador original.

## Validação

Testes da central e módulos: 30 verificações. Distribuição/interface: 28 verificações, incluindo instalação isolada, preservação de dados, rollback, remoção do aplicativo, extração confinada, manifesto, download limitado e contraste dos cinco temas. Também é executado o autoteste original do otimizador. A integração visual é revisada nos tamanhos 1440×900 e 1120×760.

Nenhuma suíte garante ausência de falhas em todas as máquinas. Os testes não instalam LabX nem executam reparos reais no Windows.
