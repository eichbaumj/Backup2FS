Write-Host "Current settings file content:"
Get-Content "$env:LOCALAPPDATA\Backup2FS\settings.config"

Write-Host "`nChecking file permissions and ownership:"
Get-Acl "$env:LOCALAPPDATA\Backup2FS\settings.config" | Format-List

$content = @'
<?xml version="1.0" encoding="utf-8"?>
<Settings>
  <HashAlgorithms>
    <md5>false</md5>
    <sha1>false</sha1>
    <sha256>true</sha256>
  </HashAlgorithms>
</Settings>
'@

Write-Host "`nUpdating settings file to only enable SHA-256..."
Set-Content -Path "$env:LOCALAPPDATA\Backup2FS\settings.config" -Value $content -Encoding UTF8 -Force

Write-Host "`nUpdated settings file content:"
Get-Content "$env:LOCALAPPDATA\Backup2FS\settings.config"

Write-Host "`nApplying read-only attribute to prevent other processes from changing it..."
Set-ItemProperty -Path "$env:LOCALAPPDATA\Backup2FS\settings.config" -Name IsReadOnly -Value $false 