# PATAPON 無障礙紀錄掃描器

這是給開發與偵錯使用的離線工具。它批次閱讀已存在的紀錄，產生可交給 Codex 排查的清單，不操作遊戲，不改朗讀、存檔、設定或按鍵。

## 使用

在這台電腦執行「掃描紀錄.cmd」，掃描目前 Steam 遊戲的 accessibility-logs。可以在遊戲開著時執行；尚未寫完的最後半行會另列，掃描完成後再寫入的事件要下次才會看到。

預設報告放在工具旁的 reports/latest。工具使用這台電腦現有的 Python；其他電腦需要 Python 3.10 以上，不需要額外套件。

給 Codex 的命令：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/PataponAudit/Scan.ps1 -NoPause -Out outputs/PataponAudit-all
```

可用參數：`-Logs` 指定 accessibility-logs 資料夾；`-Out` 指定輸出；`-LatestRuns 3` 只掃最近三次執行。省略 LatestRuns 表示全部。

## 報告

- summary.md：優先處理項目與判讀限制。
- report.json：完整結構化報告，包含原始紀錄檔、行號、事件序號和前後文。
- findings.jsonl：逐項疑點，方便再篩選與查詢。
- missions.csv：實際觀察過的關卡索引。
- boss-profiles.json：依遊戲、怪物、動作、模組 DLL 雜湊整理起手／提示紀錄，方便比較版本。這不是全遊戲覆蓋率。

同類問題會合併，最多保留四組證據，每組至多十條前後文。聲音播放失敗和 NVDA API 拒絕是直接證據；畫面未配對與起手未辨識是待查候選，不會自動當成錯誤或修改模組。刻意過濾事件、數字計時和短標籤會另計數。

只掃紀錄中出現過的事件。沒有玩過的關卡、記錄器沒抓到的原生事件、沒記錄的怪物實例識別與被遮住的畫面，都不能靠這個工具證明正常。舊紀錄缺少 speech-dispatch 時，只能確認曾排入朗讀，不能確認成功送給 NVDA。新版模組的 accepted 也只代表 NVDA 控制器接受，不能證明聲音播完。

報告可能含玩家名稱、劇情和本機路徑，保留在本機即可；工具不會上傳。不要把私人報告混入公開 release。

## 測試

```powershell
python -m unittest discover -s tools/PataponAudit -p test_audit.py
```

測試涵蓋明確失敗、候選文字、刻意過濾、隱藏文字、跨任務隔離、音效靜音、不同怪物提示、分卷去重和未寫完的最後一行。

## 已處理與回歸判斷（1.1）

`resolutions.json` 記錄修正版本、精確適用範圍和驗證程度。舊版符合的項目移至 `handled-history.json`，仍保留證據；不再混在待查清單。已實作但未玩過的標為 implemented-awaiting-gameplay。

新版本再次出現相同事件，會留在待查清單並標記 possibleRegressionOf。版本未知、不在適用範圍、音效 API 失敗，以及新的模板解析失敗，都不會被舊修正紀錄掩蓋。

掃描器也會處理舊格式時間、模板替換差異，並在有原生視窗狀態時排除已關閉視窗。沒有依日文、中文或其他語言直接刪除文字。
