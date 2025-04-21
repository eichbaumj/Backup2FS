$content = @'
<?xml version="1.0" encoding="utf-8"?>
<Settings>
  <HashAlgorithms>
    <md5>false</md5>
    <sha1>false</sha1>
    <sha256>false</sha256>
  </HashAlgorithms>
</Settings>
'@

$path = "$env:LOCALAPPDATA\Backup2FS\settings.config"
Set-Content -Path $path -Value $content -Encoding UTF8
Write-Host "Settings file updated to disable all algorithms"
Get-Content $path 