param (
    [string]$TargetPath
)

if ([string]::IsNullOrWhiteSpace($TargetPath) -or !(Test-Path $TargetPath)) {
    Write-Host "[Sign-Dev] TargetPath is invalid or missing: $TargetPath"
    exit 0
}

# 1. Ensure Zone.Identifier Mark-of-the-Web is unblocked
Unblock-File -Path $TargetPath -ErrorAction SilentlyContinue

$dir = Split-Path -Path $TargetPath
if (Test-Path $dir) {
    Get-ChildItem -Path $dir -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
        Unblock-File -Path $_.FullName -ErrorAction SilentlyContinue
    }
}

# 2. Check for existing "DriveAndGoDevCert" certificate in CurrentUser\My
$cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue | Where-Object { $_.Subject -like "*DriveAndGoDevCert*" } | Select-Object -First 1

if (-not $cert) {
    Write-Host "[Sign-Dev] Generating new self-signed Code Signing certificate (DriveAndGoDevCert)..."
    try {
        $cert = New-SelfSignedCertificate -Subject "CN=DriveAndGoDevCert" -Type CodeSigningCert -CertStoreLocation "Cert:\CurrentUser\My" -NotAfter (Get-Date).AddYears(5) -ErrorAction Stop
        
        # Trust certificate in CurrentUser\Root
        $rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", "CurrentUser")
        $rootStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $rootStore.Add($cert)
        $rootStore.Close()
        Write-Host "[Sign-Dev] Certificate successfully trusted in CurrentUser\Root."
    }
    catch {
        Write-Host "[Sign-Dev] Warning creating/trusting cert: $_"
        # Fallback to any existing code signing cert
        $cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue | Select-Object -First 1
    }
}

# 3. Sign the compiled output binaries
if ($cert) {
    Write-Host "[Sign-Dev] Signing target binary: $TargetPath"
    Set-AuthenticodeSignature -FilePath $TargetPath -Certificate $cert -ErrorAction SilentlyContinue | Out-Null
    
    # Also sign all DLLs and EXEs in the output directory
    if (Test-Path $dir) {
        Get-ChildItem -Path $dir -Include *.dll,*.exe -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
            Set-AuthenticodeSignature -FilePath $_.FullName -Certificate $cert -ErrorAction SilentlyContinue | Out-Null
        }
    }
} else {
    Write-Host "[Sign-Dev] No code signing certificate available."
}
