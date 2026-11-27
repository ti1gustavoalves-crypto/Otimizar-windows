# Assinatura de código

O suporte está preparado, mas as versões atuais continuam **sem assinatura digital**. Não há certificado de produção configurado. Nenhum certificado de teste deve ser distribuído como identidade confiável.

Quando houver um certificado de assinatura de código instalado com acesso à chave privada em `Cert:\CurrentUser\My`, use:

```powershell
.\Build-Release.ps1 -SigningThumbprint 'THUMBPRINT_DE_40_CARACTERES' -SignToolPath 'C:\caminho\signtool.exe'
```

Para certificado em `Cert:\LocalMachine\My`, acrescente `-SigningMachineStore`. Também são aceitas as variáveis `FRANKENSTEIN_SIGNING_THUMBPRINT` e `FRANKENSTEIN_SIGNTOOL`. O thumbprint identifica o certificado e não é uma senha. Não salve PFX, chaves privadas, PINs ou senhas no repositório. Tokens e provedores de chave podem solicitar autenticação própria.

Instale o SignTool pelo Windows SDK. Sem caminho explícito, o script procura no PATH e no SDK. A assinatura usa SHA-256 e carimbo de tempo RFC 3161 com SHA-256. `-TimestampUrl` permite escolher o serviço do emissor; o padrão é `http://timestamp.digicert.com`. A resposta de timestamp é assinada e verificada pelo Windows.

A ordem da geração é: compilar e testar, assinar e verificar o aplicativo, calcular hashes do payload, compilar o instalador, assinar e verificar o instalador e só então calcular seu SHA-256 e manifesto. Falhas de assinatura, identidade, validade, verificação ou carimbo de tempo interrompem a geração. Não publique artefatos parciais de uma execução com erro.

Sem thumbprint, o build continua explicitamente sem assinatura. Se um caminho do SignTool ou a opção de armazenamento da máquina for informado sem thumbprint, a configuração é rejeitada. A verificação de assinatura real de produção permanece pendente até existir um certificado confiável. Os testes com ferramentas simuladas verificam o fluxo de falha; não substituem essa validação.

A assinatura identifica o editor e detecta alterações, mas não garante a ausência de todos os alertas do SmartScreen. O atualizador continua conferindo origem, tamanho, versão e SHA-256; preparar o build para Authenticode não torna o manifesto do GitHub criptograficamente assinado.

Referências: [SignTool](https://learn.microsoft.com/en-us/windows/win32/seccrypto/signtool) e [verificação Authenticode](https://learn.microsoft.com/en-us/windows/win32/seccrypto/using-signtool-to-verify-a-file-signature).
