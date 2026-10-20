param([string]$GameRoot=$PSScriptRoot,[switch]$NoPause)
$ErrorActionPreference='Stop'
$backup=$null
$changed=New-Object 'System.Collections.Generic.List[object]'
function Set-IniValue([string]$Text,[string]$Section,[string]$Key,[string]$Value) {
    $pattern='(?ms)^\['+[regex]::Escape($Section)+'\][^\r\n]*\r?\n(?<body>.*?)(?=^\[|\z)'
    $m=[regex]::Match($Text,$pattern)
    if(!$m.Success) {return $Text+"`r`n[$Section]`r`n$Key = $Value`r`n"}
    $body=$m.Groups['body'].Value
    $keyPattern='(?m)^'+[regex]::Escape($Key)+'\s*=.*$'
    if([regex]::IsMatch($body,$keyPattern)) {$body=[regex]::Replace($body,$keyPattern,"$Key = $Value")}
    else {$body+="`r`n$Key = $Value`r`n"}
    return $Text.Substring(0,$m.Index)+"[$Section]`r`n"+$body+$Text.Substring($m.Index+$m.Length)
}
function Save-Original([string]$Relative) {
    $target=Join-Path $GameRoot $Relative
    $exists=Test-Path -LiteralPath $target -PathType Leaf
    if($exists) {
        $saved=Join-Path $backup $Relative
        New-Item -ItemType Directory -Path (Split-Path $saved) -Force | Out-Null
        Copy-Item -LiteralPath $target -Destination $saved -Force
    }
    $changed.Add([pscustomobject]@{Path=$Relative;Existed=$exists})
}
try {
    Write-Host 'PATAPON 1+2 REPLAY 朗讀模組 0.6.58 安裝程式'
    $GameRoot=[IO.Path]::GetFullPath($GameRoot)
    foreach($required in @('PATAPON12_REPLAY.exe','GameAssembly.dll','PATAPON12_REPLAY_Data\il2cpp_data\Metadata\global-metadata.dat')) {
        if(!(Test-Path -LiteralPath (Join-Path $GameRoot $required) -PathType Leaf)) {throw '請先將整個安裝包解壓到遊戲根目錄，也就是 PATAPON12_REPLAY.exe 所在資料夾，再執行安裝。'}
    }
    $knownVersion=(Get-FileHash -LiteralPath (Join-Path $GameRoot 'GameAssembly.dll') -Algorithm SHA256).Hash -eq '2C43F4078A3F2B470AF9ADEED91958C92B7C3A69D58DFA115A549560E03420FF'
    if(!$knownVersion) {
        Write-Host '這個遊戲版本尚未驗證，仍會備份並繼續安裝。模組相容性尚未確認。'
        Write-Host '首次啟動將由 BepInEx 產生對應遊戲版本的介面，可能需要較久或連線下載元件。'
    }
    if(Get-Process -Name PATAPON12_REPLAY -ErrorAction SilentlyContinue) {throw '請先關閉 PATAPON 遊戲，再重新執行安裝。'}
    $payload=Join-Path $PSScriptRoot 'payload'
    $manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') -Raw | ConvertFrom-Json
    if(!$knownVersion) {$manifest=@($manifest | Where-Object {$_.Path -notmatch '^BepInEx[\\/]interop[\\/]'})}
    Write-Host '正在檢查安裝檔案完整性……'
    foreach($entry in $manifest) {
        if([IO.Path]::IsPathRooted($entry.Path) -or $entry.Path -match '(^|[\\/])\.\.([\\/]|$)') {throw '安裝清單包含無效路徑。'}
        $src=Join-Path $payload $entry.Path
        if((Get-FileHash -LiteralPath $src -Algorithm SHA256).Hash -ne $entry.Sha256) {throw ('安裝檔案不完整，請重新解壓：'+$entry.Path)}
        $dest=Join-Path $GameRoot $entry.Path
        if(Test-Path -LiteralPath $dest -PathType Leaf) {
            $stream=[IO.File]::Open($dest,'Open','ReadWrite','None');$stream.Dispose()
        }
    }
    $backup=Join-Path $GameRoot ('PataponAccess-backups\'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    Write-Host '正在備份舊檔並安裝，請稍候……'
    foreach($entry in $manifest) {
        Save-Original $entry.Path
        $dest=Join-Path $GameRoot $entry.Path
        New-Item -ItemType Directory -Path (Split-Path $dest) -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $payload $entry.Path) -Destination $dest -Force
        if((Get-FileHash -LiteralPath $dest -Algorithm SHA256).Hash -ne $entry.Sha256) {throw ('安裝後檔案檢查失敗：'+$entry.Path)}
    }
    $cfgRel='BepInEx\config\BepInEx.cfg'
    Save-Original $cfgRel
    $cfg=Join-Path $GameRoot $cfgRel
    $text=if(Test-Path -LiteralPath $cfg) {[IO.File]::ReadAllText($cfg)} else {''}
    $text=Set-IniValue $text 'Logging.Console' 'Enabled' 'false'
    $text=Set-IniValue $text 'Logging.Disk' 'Enabled' 'true'
    if(!$knownVersion) {$text=Set-IniValue $text 'IL2CPP' 'UpdateInteropAssemblies' 'true'}
    New-Item -ItemType Directory -Path (Split-Path $cfg) -Force | Out-Null
    [IO.File]::WriteAllText($cfg,$text,(New-Object Text.UTF8Encoding($false)))
    $modRel='BepInEx\config\org.patapon.access.start.cfg'
    Save-Original $modRel
    $modCfg=Join-Path $GameRoot $modRel
    $modText=if(Test-Path -LiteralPath $modCfg) {[IO.File]::ReadAllText($modCfg)} else {''}
    $modText=Set-IniValue $modText 'Audio' 'AttackReadyCue' 'false'
    [IO.File]::WriteAllText($modCfg,$modText,(New-Object Text.UTF8Encoding($false)))
    $changed | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $backup 'changed-files.json') -Encoding UTF8
    Write-Host '安裝完成！請從 Steam 開啟遊戲。'
    Write-Host 'BepInEx 黑色視窗已隱藏，記錄器保留；存檔及按鍵設定不受影響。'
    Write-Host ('舊檔備份：'+$backup)
    $result=0
} catch {
    Write-Host ('安裝失敗：'+$_.Exception.Message) -ForegroundColor Red
    if($backup -and $changed.Count -gt 0) {
        $rollbackFailed=$false
        for($i=$changed.Count-1;$i -ge 0;$i--) {
            $item=$changed[$i];$dest=Join-Path $GameRoot $item.Path
            try {
                if($item.Existed) {Copy-Item -LiteralPath (Join-Path $backup $item.Path) -Destination $dest -Force}
                elseif(Test-Path -LiteralPath $dest -PathType Leaf) {Remove-Item -LiteralPath $dest -Force}
            } catch {$rollbackFailed=$true;Write-Host ('無法還原：'+$item.Path)}
        }
        if(!$rollbackFailed) {Write-Host '已還原此次變更的檔案。'}
        else {Write-Host ('部分檔案未能還原，請保留備份：'+$backup)}
    }
    $result=1
}
if(!$NoPause) {Read-Host '按 Enter 關閉此安裝視窗' | Out-Null}
exit $result
