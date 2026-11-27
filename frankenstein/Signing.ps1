# Dot-source this file from the release pipeline. No private key or password is read from the repository.
function Get-FrankensteinSigningConfiguration {
    param([string]$Thumbprint, [string]$SignToolPath, [string]$TimestampUrl='http://timestamp.digicert.com', [switch]$MachineStore)
    if ([string]::IsNullOrWhiteSpace($Thumbprint)) {
        if ($SignToolPath -or $MachineStore) { throw 'Informe SigningThumbprint para ativar a assinatura.' }
        return $null
    }
    $Thumbprint=$Thumbprint.Replace(' ','').ToUpperInvariant()
    if ($Thumbprint -notmatch '\A[0-9A-F]{40}\z') { throw 'Thumbprint de certificado invalido.' }
    $store=if($MachineStore){'LocalMachine'}else{'CurrentUser'}
    $cert=Get-Item -LiteralPath ("Cert:\$store\My\$Thumbprint") -ErrorAction Stop
    if (!$cert.HasPrivateKey -or $cert.NotBefore -gt (Get-Date) -or $cert.NotAfter -le (Get-Date)) { throw 'Certificado sem chave privada ou fora da validade.' }
    if (@($cert.EnhancedKeyUsageList | Where-Object {$_.ObjectId -eq '1.3.6.1.5.5.7.3.3'}).Count -eq 0) { throw 'O certificado nao permite assinatura de codigo.' }
    $timestamp=$null
    if (![Uri]::TryCreate($TimestampUrl,[UriKind]::Absolute,[ref]$timestamp) -or $timestamp.Scheme -notin @('http','https') -or $timestamp.UserInfo -or $timestamp.Fragment) { throw 'Servidor RFC 3161 invalido.' }
    if (!$SignToolPath) {
        $command=Get-Command signtool.exe -ErrorAction SilentlyContinue
        if ($command) { $SignToolPath=$command.Source }
        else {
            $sdk=Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
            if(Test-Path -LiteralPath $sdk){$SignToolPath=Get-ChildItem -LiteralPath $sdk -Directory | Sort-Object Name -Descending | ForEach-Object {Join-Path $_.FullName 'x64\signtool.exe'} | Where-Object {Test-Path -LiteralPath $_} | Select-Object -First 1}
        }
    }
    if (!$SignToolPath -or !(Test-Path -LiteralPath $SignToolPath -PathType Leaf)) { throw 'SignTool nao encontrado. Instale as ferramentas de assinatura do Windows SDK ou informe SignToolPath.' }
    [pscustomobject]@{Thumbprint=$Thumbprint;SignToolPath=[IO.Path]::GetFullPath($SignToolPath);TimestampUrl=$TimestampUrl;MachineStore=[bool]$MachineStore}
}

function Invoke-FrankensteinSigning {
    param([Parameter(Mandatory=$true)][string]$Path, $Configuration)
    if (!$Configuration) { Write-Output ('SEM ASSINATURA DIGITAL: '+[IO.Path]::GetFileName($Path)); return }
    $arguments=@('sign','/sha1',$Configuration.Thumbprint,'/s','My','/fd','SHA256','/tr',$Configuration.TimestampUrl,'/td','SHA256')
    if ($Configuration.MachineStore) { $arguments+='/sm' }
    $arguments+=[IO.Path]::GetFullPath($Path)
    & $Configuration.SignToolPath @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Falha na assinatura. Distribuicao interrompida.' }
    & $Configuration.SignToolPath verify /pa /all /v ([IO.Path]::GetFullPath($Path))
    if ($LASTEXITCODE -ne 0) { throw 'Falha na verificacao Authenticode. Distribuicao interrompida.' }
    $signature=Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid' -or !$signature.SignerCertificate -or $signature.SignerCertificate.Thumbprint -ne $Configuration.Thumbprint -or !$signature.TimeStamperCertificate) { throw 'Assinatura, identidade ou carimbo de tempo invalidos. Distribuicao interrompida.' }
    Write-Output ('ASSINATURA VALIDADA: '+[IO.Path]::GetFileName($Path))
}
