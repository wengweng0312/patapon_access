param([string]$Logs='D:\SteamLibrary\steamapps\common\PATAPON12_REPLAY\BepInEx\accessibility-logs',[string]$Out='',[int]$LatestRuns=0,[switch]$NoPause)
$ErrorActionPreference='Stop'
$env:PYTHONIOENCODING='utf-8'
try {
    if(!$Out){$Out=Join-Path $PSScriptRoot 'reports\latest'}
    $python=Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
    $prefix=@()
    if(!(Test-Path -LiteralPath $python)){
        $found=Get-Command python.exe -ErrorAction SilentlyContinue
        if($found){$python=$found.Source}
        else {$found=Get-Command py.exe -ErrorAction SilentlyContinue;if(!$found){throw '找不到 Python 3.10 以上版本。此工具不需要額外 Python 套件。'};$python=$found.Source;$prefix=@('-3')}
    }
    & $python @prefix (Join-Path $PSScriptRoot 'patapon_audit.py') --logs $Logs --out $Out --latest-runs $LatestRuns
    if($LASTEXITCODE -ne 0){throw '掃描未完成，請查看上方錯誤。'}
    Write-Host '報告只寫到輸出資料夾，不會修改遊戲或紀錄。'
    $result=0
} catch {Write-Host ('掃描失敗：'+$_.Exception.Message);$result=1}
if(!$NoPause){Read-Host '按 Enter 關閉' | Out-Null}
exit $result
