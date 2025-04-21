$content = @'
<?xml version="1.0" encoding="utf-8"?>
<Settings>
  <HashAlgorithms>
    <md5>true</md5>
    <sha1>true</sha1>
    <sha256>true</sha256>
  </HashAlgorithms>
</Settings>
'@

$settingsPath = "$env:LOCALAPPDATA\Backup2FS\settings.config"
Set-Content -Path $settingsPath -Value $content -Encoding UTF8
Write-Host "Settings file updated to enable all algorithms:"
Get-Content $settingsPath 