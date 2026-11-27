$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '..\Signing.ps1')
$script:passed=0
function Assert($Condition,[string]$Name) { if(!$Condition){throw $Name};$script:passed++;Write-Output ('OK: '+$Name) }
function Reject([scriptblock]$Action) { try { & $Action | Out-Null; return $false } catch { return $true } }
Assert ($null -eq (Get-FrankensteinSigningConfiguration)) 'Ausencia de certificado permite build explicitamente sem assinatura'
Assert (Reject {Get-FrankensteinSigningConfiguration -Thumbprint 'invalid'}) 'Thumbprint malformado bloqueado'
Assert (Reject {Get-FrankensteinSigningConfiguration -SignToolPath 'signtool.exe'}) 'Ferramenta sem certificado nao ativa modo sem assinatura silenciosamente'
Assert (Reject {Get-FrankensteinSigningConfiguration -MachineStore}) 'Armazenamento sem certificado bloqueado'

# Test doubles exercise the control flow, never install a certificate or trust root.
$script:certificate=[pscustomobject]@{HasPrivateKey=$true;NotBefore=(Get-Date).AddDays(-1);NotAfter=(Get-Date).AddDays(1);EnhancedKeyUsageList=@([pscustomobject]@{ObjectId='1.3.6.1.5.5.7.3.3'})}
function Get-Item { param([string]$LiteralPath,[string]$ErrorAction) if($LiteralPath.StartsWith('Cert:')){return $script:certificate};Microsoft.PowerShell.Management\Get-Item -LiteralPath $LiteralPath }
$resolved=Get-FrankensteinSigningConfiguration -Thumbprint ('a'*40) -SignToolPath $PSCommandPath
Assert ($resolved.Thumbprint -ceq ('A'*40)) 'Certificado com EKU de codigo aceito e thumbprint normalizado'
$script:certificate.HasPrivateKey=$false
Assert (Reject {Get-FrankensteinSigningConfiguration -Thumbprint ('A'*40) -SignToolPath $PSCommandPath}) 'Certificado sem chave privada bloqueado'
$script:certificate.HasPrivateKey=$true;$script:certificate.NotAfter=(Get-Date).AddDays(-1)
Assert (Reject {Get-FrankensteinSigningConfiguration -Thumbprint ('A'*40) -SignToolPath $PSCommandPath}) 'Certificado vencido bloqueado'
$script:certificate.NotAfter=(Get-Date).AddDays(1);$script:certificate.EnhancedKeyUsageList=@([pscustomobject]@{ObjectId='1.3.6.1.5.5.7.3.2'})
Assert (Reject {Get-FrankensteinSigningConfiguration -Thumbprint ('A'*40) -SignToolPath $PSCommandPath}) 'Certificado de autenticacao nao substitui assinatura de codigo'
$script:certificate.EnhancedKeyUsageList=@([pscustomobject]@{ObjectId='1.3.6.1.5.5.7.3.3'})
Assert (Reject {Get-FrankensteinSigningConfiguration -Thumbprint ('A'*40) -SignToolPath $PSCommandPath -TimestampUrl 'file:///C:/timestamp'}) 'Timestamp fora de HTTP ou HTTPS bloqueado'
$script:calls=New-Object 'System.Collections.Generic.List[string]'
$script:signExit=0;$script:verifyExit=0
$script:identity='A'*40;$script:timestamp=[pscustomobject]@{Subject='Test timestamp'};$script:status='Valid'
function Test-SignTool {
    $script:calls.Add(($args -join '|'))
    $global:LASTEXITCODE=if($args[0] -eq 'sign'){$script:signExit}else{$script:verifyExit}
}
function Get-AuthenticodeSignature {
    param([string]$LiteralPath)
    [pscustomobject]@{Status=$script:status;SignerCertificate=[pscustomobject]@{Thumbprint=$script:identity};TimeStamperCertificate=$script:timestamp}
}
$configuration=[pscustomobject]@{Thumbprint='A'*40;SignToolPath='Test-SignTool';TimestampUrl='https://timestamp.example.test';MachineStore=$false}
$sample=Join-Path $PSScriptRoot 'sample with spaces.exe'
Invoke-FrankensteinSigning -Path $sample -Configuration $configuration | Out-Null
Assert ($script:calls.Count -eq 2 -and $script:calls[0].StartsWith('sign|') -and $script:calls[1].StartsWith('verify|/pa|/all|/v|')) 'Assinatura seguida de verificacao Authenticode'
Assert ($script:calls[0].Contains('/fd|SHA256|/tr|https://timestamp.example.test|/td|SHA256|')) 'Hash e carimbo de tempo SHA256 obrigatorios'
$script:calls.Clear();$configuration.MachineStore=$true
Invoke-FrankensteinSigning -Path $sample -Configuration $configuration | Out-Null
Assert ($script:calls[0].Contains('|/sm|')) 'Armazenamento LocalMachine encaminhado ao SignTool'
$script:calls.Clear();$script:signExit=1
Assert (Reject {Invoke-FrankensteinSigning -Path $sample -Configuration $configuration}) 'Falha do assinador interrompe a geracao'
Assert ($script:calls.Count -eq 1) 'Falha de assinatura nao avanca para verificacao'
$script:signExit=0;$script:verifyExit=1
Assert (Reject {Invoke-FrankensteinSigning -Path $sample -Configuration $configuration}) 'Falha de verificacao interrompe a geracao'
$script:verifyExit=0;$script:identity='B'*40
Assert (Reject {Invoke-FrankensteinSigning -Path $sample -Configuration $configuration}) 'Certificado divergente bloqueado'
$script:identity='A'*40;$script:timestamp=$null
Assert (Reject {Invoke-FrankensteinSigning -Path $sample -Configuration $configuration}) 'Ausencia de carimbo de tempo bloqueada'
$script:timestamp=[pscustomobject]@{Subject='Test timestamp'};$script:status='NotTrusted'
Assert (Reject {Invoke-FrankensteinSigning -Path $sample -Configuration $configuration}) 'Assinatura nao confiavel bloqueada'
$script:calls.Clear()
$result=Invoke-FrankensteinSigning -Path $sample -Configuration $null
Assert ($script:calls.Count -eq 0 -and $result -like 'SEM ASSINATURA DIGITAL:*') 'Build sem certificado declara o estado e nao invoca o assinador'
Write-Output ("$script:passed testes de suporte a assinatura aprovados; assinatura real nao exercitada.")
