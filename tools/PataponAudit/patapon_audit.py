#!/usr/bin/env python3
"""Read-only, offline accessibility log triage. Python 3.10+, standard library only."""
from __future__ import annotations
import argparse,bisect,collections,csv,datetime,hashlib,json,re,sys,unicodedata,math
from pathlib import Path

from audit_rules import ResolutionRegistry,template_matches,script_clean
VERSION='1.1.0'
TAG=re.compile(r'<[^>]*>')
DEFAULT_LOGS=Path('D:/SteamLibrary/steamapps/common/PATAPON12_REPLAY/BepInEx/accessibility-logs')
SPEECH_KINDS={'dialogue-template-resolved','settings-popup-speech','dialogue-level-resolved','dialogue-queued','common-visible-text','selection-speech-ready','structured-ui-speech','inventory-result-reward-speech','minigame-reward-speech','speech-dispatch'}
FILTER_KINDS={'camp-hint-repeat-suppressed','battle-chant-suppressed','ambient-dialogue-suppressed','idle-chatter-repeat-suppressed','miracle-countdown-suppressed'}
CONTEXT_KINDS=SPEECH_KINDS|FILTER_KINDS|{'mission-start','mission-end','boss-script-motion','boss-attack-marker','boss-attack-cue','cue-playback','weather-request','miracle-ten-seconds','p2-unit-page-navigation','p2-gimmick-motion-request'}

def norm(value):
    s=unicodedata.normalize('NFKC',TAG.sub('',str(value or '')))
    return ''.join(c for c in s if c.isalnum())

def clean(value):return re.sub(r'\s+',' ',TAG.sub('',str(value or ''))).strip()
def texts(data):
    return [data[k] for k in ('text','fullText','body','label','name','title') if isinstance(data.get(k),str) and data[k]]
def minute(ms):
    seconds=max(0,int(ms)//1000);return f'{seconds//60}:{seconds%60:02d}'
def safe_float(value,default=0):
    try:
        result=float(value);return result if math.isfinite(result) else default
    except (ValueError,TypeError):return default

def load_run(paths):
    rows=[];issues=[];seen={};duplicates=0;origin=None
    for p in paths:
        try:
            with p.open('rb') as f:
                for line_no,raw in enumerate(f,1):
                    if not raw.strip():continue
                    try:
                        row=json.loads(raw.decode('utf-8-sig'))
                        if not isinstance(row,dict) or not isinstance(row.get('data',{}),dict):raise ValueError('invalid event object')
                    except (ValueError,UnicodeDecodeError) as e:
                        issues.append({'file':str(p),'line':line_no,'kind':'invalid-line' if raw.endswith(b'\n') else 'partial-live-tail','error':str(e)[:180]});continue
                    if not isinstance(row.get('kind'),str):continue
                    row.setdefault('data',{})
                    row['_file']=str(p);row['_line']=line_no
                    if not row.get('run'):row['run']=re.sub(r'-\d+$','',p.stem)
                    if row.get('elapsedMs') is not None:
                        row['_ms']=safe_float(row['elapsedMs']);row['_timeSource']='elapsedMs'
                    else:
                        try:
                            timestamp=datetime.datetime.fromisoformat(row.get('time','')).timestamp()
                            if origin is None:origin=timestamp
                            row['_ms']=(timestamp-origin)*1000;row['_timeSource']='wall-clock-derived'
                        except (ValueError,TypeError):row['_ms']=0;row['_timeSource']='unknown'
                    key=(row.get('run'),row.get('seq'))
                    if key[1] is not None:
                        fingerprint=json.dumps({k:v for k,v in row.items() if not k.startswith('_')},sort_keys=True,ensure_ascii=False)
                        if key in seen:
                            if seen[key]==fingerprint:duplicates+=1;continue
                            issues.append({'file':str(p),'line':line_no,'kind':'conflicting-sequence','seq':key[1]})
                        else:seen[key]=fingerprint
                    rows.append(row)
        except OSError as e:issues.append({'file':str(p),'kind':'unreadable','error':str(e)})
    rows.sort(key=lambda r:(r['_ms'],safe_float(r.get('seq'))))
    return rows,issues,duplicates

class Triage:
    def __init__(self):
        self.registry=ResolutionRegistry();self.handled={}
        self.findings={};self.runs=[];self.missions=[];self.issues=[];self.counts=collections.Counter();self.profiles={};self.next_id=1
    def evidence(self,row,rows,times):
        i=bisect.bisect_left(times,row['_ms']-1500);j=bisect.bisect_right(times,row['_ms']+2000)
        context=[]
        for other in rows[i:j]:
            if other['kind'] not in CONTEXT_KINDS:continue
            data=other.get('data',{})
            fields={k:data[k] for k in ('unit','motion','action','phase','attackEntry','played','sound','success','muted','accepted','reason','source') if k in data}
            if texts(data):fields['text']=' / '.join(texts(data))[:500]
            context.append({'seq':other.get('seq'),'elapsedMs':other['_ms'],'kind':other['kind'],'data':fields})
        if len(context)>10:
            context=sorted(context,key=lambda x:abs(x['elapsedMs']-row['_ms']))[:10];context.sort(key=lambda x:x['elapsedMs'])
        return {'run':row.get('run'),'mission':row.get('mission',0),'file':row['_file'],'line':row['_line'],'seq':row.get('seq'),'elapsedMs':row['_ms'],'kind':row['kind'],'data':row.get('data',{}),'context':context}
    def add(self,rule,level,confidence,title,detail,row,rows,times,game='',key='',action=''):
        resolution=self.registry.historical(rule,game,key,row)
        bucket=self.handled if resolution else self.findings
        signature='|'.join([rule,game,key]);group=bucket.get(signature)
        if group is None:
            group={'id':f'A{self.next_id:04d}','rule':rule,'severity':level,'confidence':confidence,'title':title,'detail':detail,'nextAction':action,'game':game,'key':key,'occurrences':0,'runs':[],'examples':[]}
            if resolution:group['resolution']={k:resolution[k] for k in ('id','implementedIn','status','evidence') if k in resolution}
            else:
                prior=self.registry.matching(rule,game,key,row)
                if prior:group['possibleRegressionOf']=prior['id']
            self.next_id+=1;bucket[signature]=group
        group['occurrences']+=1
        run=row.get('run','unknown')
        if run not in group['runs']:group['runs'].append(run)
        if len(group['examples'])<4:group['examples'].append(self.evidence(row,rows,times))
    def process(self,run,rows,issues,duplicates):
        self.issues.extend(issues)
        if not rows:return
        times=[r['_ms'] for r in rows];session=next((r['data'] for r in rows if r['kind']=='session'),{})
        for row in rows:row['_version']=session.get('plugin','unknown');row['_hash']=session.get('pluginSha256')
        template_events=[]
        for row in rows:
            if row['kind']=='game-event':
                for value in row['data'].get('args',[]):
                    if isinstance(value,str) and re.search(r'<N\d+>',value,re.I):template_events.append((row,value))
        template_times=[row['_ms'] for row,value in template_events]
        window_events=[r for r in rows if r['kind']=='native-window-state'];window_times=[r['_ms'] for r in window_events]
        def window_state(row):
            path=row['data'].get('path','');best=None;best_length=-1
            limit=bisect.bisect_right(window_times,row['_ms']+600)
            for w in window_events[:limit]:
                prefix=w['data'].get('windowPath','')
                if prefix and (path==prefix or path.startswith(prefix+'/')) and len(prefix)>=best_length:best=w;best_length=len(prefix)
            return None if best is None else best['data'].get('open')
        dispatch=[r for r in rows if r['kind']=='speech-dispatch'];dispatch_times=[r['_ms'] for r in dispatch]
        queued=[r for r in rows if r['kind'] in SPEECH_KINDS-{'speech-dispatch'}];queue_times=[r['_ms'] for r in queued]
        filters=[r for r in rows if r['kind'] in FILTER_KINDS];filter_times=[r['_ms'] for r in filters]
        all_speech=sorted(queued+dispatch,key=lambda r:r['_ms']);speech_times=[r['_ms'] for r in all_speech]
        audio_settings=next((r['data'] for r in rows if r['kind']=='audit-settings'),{})
        mission_rows={};mission_starts={};mission_ends={};games={};mission_titles={}
        for r in rows:
            mid=r.get('mission',0);d=r['data'];k=r['kind']
            self.counts[k]+=1
            if k=='mission-start':mission_starts[mid]=r['_ms'];games[mid]=d.get('game','')
            if k=='mission-end':mission_ends[mid]=r['_ms']
            mission_rows.setdefault(mid,[]).append(r)
        def during(r):
            mid=r.get('mission',0)
            return mid in mission_starts and mission_starts[mid]<=r['_ms']<=mission_ends.get(mid,float('inf'))
        def matches(row,candidates,ctimes,window=3000):
            ns=[norm(t) for t in texts(row['data']) if norm(t)]
            if not ns:return False
            lo=bisect.bisect_left(ctimes,row['_ms']-window);hi=bisect.bisect_right(ctimes,row['_ms']+window)
            for c in candidates[lo:hi]:
                if c.get('mission',0)!=row.get('mission',0):continue
                if c['kind']=='speech-dispatch' and not c['data'].get('accepted'):continue
                for t in texts(c['data']):
                    n=norm(t)
                    if any(v in n for v in ns):return True
            return False
        cue_rows=[r for r in rows if r['kind']=='boss-attack-cue'];cue_times=[r['_ms'] for r in cue_rows]
        def near_cue(r,before=0,after=700):
            lo=bisect.bisect_left(cue_times,r['_ms']-before);hi=bisect.bisect_right(cue_times,r['_ms']+after)
            return [c for c in cue_rows[lo:hi] if c.get('mission')==r.get('mission') and c['data'].get('unit')==r['data'].get('unit')]
        for mid,start in mission_starts.items():
            mr=[r for r in mission_rows[mid] if during(r)]
            title=next((clean(r['data'].get('text','')).split(' MAP ')[0] for r in mr if r['kind']=='dialogue-queued' and '～' in r['data'].get('text','')),'')
            mission_titles[mid]=title
            units=sorted({r['data']['unit'] for r in mr if r['data'].get('unit')})
            self.missions.append({'run':run,'mission':mid,'game':games.get(mid,''),'title':title,'startMs':start,'endMs':mission_ends.get(mid),'complete':mid in mission_ends,'units':units,'events':len(mr),'windupCandidates':sum(r['kind']=='boss-script-motion' and r['data'].get('action')==10 and r['data'].get('phase')==0 for r in mr),'bossCues':sum(r['kind']=='boss-attack-cue' and bool(r['data'].get('played')) for r in mr),'speechEvidence':'dispatch' if dispatch else 'queue-only'})
        # Sources without terminal speech telemetry cannot prove an actual missed utterance.
        self.runs.append({'run':run,'files':sorted({r['_file'] for r in rows}),'version':session.get('plugin','unknown'),'pluginSha256':session.get('pluginSha256'),'events':len(rows),'duplicatesIgnored':duplicates,'missionCount':len(mission_starts),'speechEvidence':'dispatch' if dispatch else 'queue-only','settingsKnown':bool(audio_settings),'invalidLines':len(issues),'lastElapsedMs':times[-1]})
        handled_text=set()
        for r in rows:
            d=r['data'];k=r['kind'];game=games.get(r.get('mission',0),d.get('game',str(d.get('source','')).split('.')[0] if str(d.get('source','')).startswith(('P1.','P2.')) else ''));unit=d.get('unit','')
            if k in FILTER_KINDS:
                self.counts['expected-filter-events']+=1
                hint_id=self.registry.hint_id(d.get('text','')) if game=='P2' and k=='ambient-dialogue-suppressed' else None
                if hint_id is not None:self.add('protected-hint-suppressed','high','confirmed-source-filter','遊戲原有提示被閒聊過濾','已核對原生村落台詞編號 '+str(hint_id)+'；不能把原有遊戲提示一律當普通閒聊。',r,rows,times,game,key='P2:colony:'+str(hint_id),action='檢查原生提示保護與當前語言的台詞快取；新版再出現應當作回歸。')
            if k=='dialogue-template-unresolved':
                self.add('template-resolution-failed','medium','confirmed-unresolved-template','對話變數在期限內未取得顯示值','已嘗試解析，但名字或數字可能仍缺漏；這個新失敗不會被已處理清單隱藏。',r,rows,times,game,key=norm(d.get('template',''))[:180],action='核對同一句的實際顯示元件與時間；不要猜玩家名字。')
            if k=='speech-dispatch' and d.get('accepted') is False:
                self.add('speech-delivery-failed','high','confirmed-api-result','文字未成功送入 NVDA','這是送出 API 的失敗結果；不是推測玩家有沒有聽見。',r,rows,times,game,key=d.get('reason','unknown'),action='檢查 NVDA 連線、控制器載入和回傳碼。')
            if k=='cue-playback' and not d.get('muted') and d.get('success') is False:
                self.add('cue-playback-failed','high','confirmed-api-result','提示音播放要求失敗','已排除玩家把此音效設成靜音的情況。',r,rows,times,game,key=d.get('sound',''),action='查音效檔案、路徑和播放 API；不要先改起手規則。')
            if k=='rate-limit':
                self.add('log-loss','high','confirmed-log','記錄器有丟棄事件','這段的負面判斷不完整，沒有事件不能當成沒有發生。',r,rows,times,game,key='rate-limit',action='縮小高頻記錄來源，再重測相關場景。')
            if 'error' in k or k in {'hook-failed','hook-missing'}:
                self.add('observer-error','high','confirmed-log','觀察器錯誤或掛接失敗','原生資料未必有被成功觀察。',r,rows,times,game,key=k+':'+str(d.get('method',d.get('type',d.get('error',''))))[:220],action='先修觀察器，再評估覆蓋情況。')
            if k=='boss-script-motion' and during(r) and d.get('action')==10 and d.get('phase')==0:
                cues=near_cue(r)
                profile=self.profiles.setdefault((game,unit,d.get('motion'),session.get('pluginSha256',session.get('plugin','?'))),{'game':game,'unit':unit,'motion':d.get('motion'),'version':session.get('plugin'),'pluginSha256':session.get('pluginSha256'),'starts':0,'recognized':0,'cueEvents':0,'played':0})
                profile['starts']+=1;profile['recognized']+=bool(d.get('attackEntry'));profile['cueEvents']+=bool(cues);profile['played']+=any(c['data'].get('played') for c in cues)
                if audio_settings.get('bossAttackCue') is False:
                    self.counts['disabled-boss-starts']+=1;continue
                if not d.get('attackEntry') and not cues:
                    self.add('windup-unrecognized','medium','needs-script-review','疑似攻擊起手未被預警辨識','觀察到 action 10 / phase 0，但未辨識起手且附近沒有同怪物的預警。這不是已驗證的傷害前置時間；舊版也可能關閉音效。',r,rows,times,game,key=f'{unit}:motion={d.get("motion")}',action='比較同腳本準備段與後續攻擊判定，確認提前量；不能直接用傷害幀補音。')
                elif d.get('attackEntry') and not cues:
                    self.add('recognized-without-cue','medium','needs-settings-review','已辨識起手但沒有預警送出紀錄','可能是音效設定、冷卻或未在前景；不能只靠這條就判斷播放壞掉。',r,rows,times,game,key=unit,action='核對設定、前景及同怪物的防重複冷卻。')
            if k=='boss-attack-marker' and during(r):
                mid=r.get('mission',0)
                nearby=[s for s in mission_rows.get(mid,[]) if s['kind']=='boss-script-motion' and s['data'].get('unit')==unit and s['data'].get('action')==10 and s['data'].get('phase')==0 and 0<=r['_ms']-s['_ms']<=15000]
                if not nearby and not near_cue(r,before=15000,after=200):
                    self.add('attack-without-observed-windup','low','needs-more-telemetry','有攻擊判定，但沒觀察到先前起手','攻擊判定前十五秒沒有同怪物的共通起手或預警，可能是特殊腳本、重複判定或記錄不完整。',r,rows,times,game,key=unit,action='查特殊腳本的狀態轉換；此事件只能用於倒推，不能直接當預警。')
            if k=='dialogue-queued' and dispatch:
                # Only assess periods after terminal telemetry first appears.
                if r['_ms']>=dispatch_times[0] and not matches(r,dispatch,dispatch_times,2500):
                    self.add('queued-not-dispatched','medium','needs-review','對話排隊後沒有對應送出紀錄','可能被後續對話覆蓋、取消或失去前景。需要檢查前後文，不代表應強制朗讀。',r,rows,times,game,key=norm(d.get('text',''))[:160],action='檢查覆蓋、取消與焦點紀錄，保留真正劇情和戰術提示。')
            if k!='text-change':continue
            value=clean(d.get('text',''));n=norm(value);path=d.get('path','')
            owner_open=window_state(r)
            if owner_open is False:self.counts['native-closed-window-skipped']+=1;continue
            if d.get('maxVisibleCharacters',1)==0 or d.get('visible') is False or safe_float(d.get('alpha',1),1)<.05:
                self.counts['hidden-text-skipped']+=1;continue
            if not n or n.isdigit() or len(n)<4:
                self.counts['numeric-or-short-text-skipped']+=1;continue
            if matches(r,filters,filter_times,1800):self.counts['text-matched-expected-filter']+=1;continue
            if matches(r,all_speech,speech_times,3500):self.counts['text-has-speech-pipeline']+=1;continue
            template_evidence=None
            lo=bisect.bisect_left(template_times,r['_ms']-3500);hi=bisect.bisect_right(template_times,r['_ms']+3500)
            for event,raw in template_events[lo:hi]:
                if event.get('mission',0)!=r.get('mission',0) or not template_matches(raw,value):continue
                proxy={**event,'data':{'text':script_clean(raw)}}
                if matches(proxy,filters,filter_times,1800):
                    template_evidence='expected-filter';break
                if matches(proxy,all_speech,speech_times,3500):template_evidence='partial-template';break
            if template_evidence=='expected-filter':self.counts['template-matched-expected-filter']+=1;continue
            if template_evidence=='partial-template':
                self.add('template-substitution-gap','medium','template-pipeline-confirmed','對話有擷取，但名字或數字不同','原始模板與畫面符合，並有排入朗讀證據；是變數內容完整度問題，不是整句沒抓到。',r,rows,times,game,key=re.sub(r'\d+','*',path)+':'+n[:200],action='檢查顯示文字解析；新版若仍缺漏，不能沿用舊問題的已處理狀態。');continue
            if owner_open is None and any(t in path for t in ('SaveDataManager/Canvas/Root/Information/','Setting/SettingWindow/Contents/ConfirmationWindows/')):
                self.add('window-state-unverified','low','old-visibility-incomplete','舊確認視窗缺少開啟狀態證據','文字元件存在不代表視窗正在顯示；新版會記錄原生開關狀態，不能按日文或其他語言排除。',r,rows,times,game,key=re.sub(r'\d+','*',path),action='以原生視窗狀態配對；真正開啟卻沒有朗讀仍要列為疑點。');continue
            # Fixed UI labels, layout grids and lookup-only localizations are not
            # automatically faults. Prioritize actual balloons and hint windows.
            if not any(token in path.lower() for token in ('balloon','message','dialog','hint','tips','description')):
                self.counts['unclassified-ui-text']+=1;continue
            signature=(r.get('mission',0),path,n)
            if signature in handled_text:continue
            handled_text.add(signature)
            self.add('visible-text-no-speech-evidence','medium','needs-visibility-review','畫面文字沒有對應朗讀證據','來源是對話或說明元件，但附近沒有朗讀管線或已知過濾紀錄。舊版只記文字與最大顯示字數，不能保證玩家真的看得到。',r,rows,times,game,key=re.sub(r'\d+','*',path)+':'+n[:200],action='先確認可見性及畫面角色，再判斷是否漏讀；不要直接把所有文字加入朗讀。')
    def report(self):
        ranks={'high':0,'medium':1,'low':2}
        findings=sorted(self.findings.values(),key=lambda f:(ranks[f['severity']],-len(f['runs']),-f['occurrences'],f['id']))
        return {'schema':1,'toolVersion':VERSION,'generatedAt':datetime.datetime.now().astimezone().isoformat(),'scope':'observed-runtime-events-only','limitations':['沒有紀錄的關卡、未出現的事件與畫面不能驗證。','沒有對應朗讀證據不等於確定漏讀；舊版通常缺少 NVDA 送出紀錄。','NVDA accepted 只表示控制器接受要求，不表示聲音播完或玩家聽見。','預警判定統計不是全遊戲覆蓋率，也不證明有足夠閃躲提前量。','同種類怪物可能同時出現；舊紀錄缺少實例識別時，時間比對可能混淆。','掃描器不修改遊戲、模組、存檔、按鍵或過濾規則。'],'runs':self.runs,'missions':self.missions,'counts':dict(self.counts),'handledHistory':list(self.handled.values()),'resolutionPolicy':self.registry.data.get('policy'),'findings':findings,'bossProfiles':list(self.profiles.values()),'inputIssues':self.issues}

def discover(folder,latest):
    groups=collections.defaultdict(list)
    for p in sorted(folder.glob('*.jsonl')):
        run=None
        try:
            with p.open(encoding='utf-8-sig') as f:
                first=f.readline();row=json.loads(first);run=row.get('run')
        except (ValueError,OSError,UnicodeDecodeError):pass
        groups[run or re.sub(r'-\d+$','',p.stem)].append(p)
    result=sorted(groups.items(),key=lambda item:max(p.stat().st_mtime for p in item[1]))
    return result[-latest:] if latest else result

def markdown(report):
    fs=report['findings'];lines=['# PATAPON 無障礙紀錄掃描','',f'掃描 {len(report["runs"])} 次執行、{len(report["missions"])} 場任務；合併為 {len(fs)} 類待查項目。','', '這是依據已記錄事件的排查清單，不是全遊戲覆蓋率或自動修復。','', '## 優先查看','']
    if not fs:lines.append('沒有符合目前規則的缺口；不代表所有資訊都已報讀。')
    for f in fs[:35]:
        e=f['examples'][0];text=' / '.join(texts(e['data']))[:200]
        lines.extend([f'### {f["id"]} [{f["severity"]}] {f["title"]}','',f'- 遊戲：{f["game"] or "未取得"}；出現 {f["occurrences"]} 次，跨 {len(f["runs"])} 次執行。',f'- 證據分類：{f["confidence"]}',f'- {f["detail"]}',f'- 位置：{Path(e["file"]).name}，第 {e["line"]} 行，任務 {e["mission"]}，執行後 {minute(e["elapsedMs"])}。',f'- 下一步：{f["nextAction"]}'])
        if text:lines.append(f'- 文字：{text}')
        if e['data'].get('unit'):lines.append(f'- 單位：{e["data"]["unit"]}；動作：{e["data"].get("motion","未取得")}')
        lines.append('')
    lines+=['## 已處理的歷史項目','',f'共 {len(report.get("handledHistory",[]))} 類，仍保留原始證據；已實作不代表已完成遊玩驗證。新版若再次發生，仍會列為疑點。','']
    for f in report.get('handledHistory',[]):lines.append(f'- {f["id"]}：{f["title"]}，{f["occurrences"]} 次；{f["resolution"]["status"]}。')
    lines+=['## 記錄完整性','',f'- 輸入問題：{len(report["inputIssues"])} 筆（可能包含遊戲仍在寫入的最後半行）。',f'- 刻意過濾事件：{report["counts"].get("expected-filter-events",0)} 筆，沒有直接列成錯誤。',f'- 僅有排隊證據的執行：{sum(r["speechEvidence"]=="queue-only" for r in report["runs"])} 次。','','## 判讀限制','']+['- '+s for s in report['limitations']]
    return '\n'.join(lines)+'\n'

def write_report(report,out):
    out.mkdir(parents=True,exist_ok=True)
    (out/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    (out/'summary.md').write_text(markdown(report),encoding='utf-8')
    (out/'handled-history.json').write_text(json.dumps(report.get('handledHistory',[]),ensure_ascii=False,indent=2),encoding='utf-8')
    with (out/'findings.jsonl').open('w',encoding='utf-8') as f:
        for x in report['findings']:f.write(json.dumps(x,ensure_ascii=False)+'\n')
    with (out/'missions.csv').open('w',encoding='utf-8-sig',newline='') as f:
        fields=['run','mission','game','title','startMs','endMs','complete','events','windupCandidates','bossCues','speechEvidence','units'];w=csv.DictWriter(f,fieldnames=fields);w.writeheader()
        for m in report['missions']:w.writerow({**m,'units':';'.join(m['units'])})
    (out/'boss-profiles.json').write_text(json.dumps(report['bossProfiles'],ensure_ascii=False,indent=2),encoding='utf-8')

def main(argv=None):
    ap=argparse.ArgumentParser(description='批次檢查 PATAPON 可及性紀錄；唯讀，不操作遊戲。')
    ap.add_argument('--logs',type=Path,default=DEFAULT_LOGS,help='accessibility-logs 資料夾')
    ap.add_argument('--out',type=Path,default=Path('outputs/PataponAudit-latest'),help='報告輸出資料夾')
    ap.add_argument('--latest-runs',type=int,default=0,help='只查最近 N 次執行，0 表示全部')
    args=ap.parse_args(argv)
    if args.latest_runs<0:ap.error('--latest-runs 必須大於等於 0')
    if not args.logs.is_dir():ap.error('找不到紀錄資料夾：'+str(args.logs))
    groups=discover(args.logs,args.latest_runs)
    if not groups:ap.error('找不到 .jsonl 紀錄；不會以空報告冒充掃描成功。')
    triage=Triage()
    for run,paths in groups:
        rows,issues,duplicates=load_run(paths);triage.process(run,rows,issues,duplicates)
    result=triage.report()
    if not result['runs']:ap.error('檔案存在，但沒有可解析事件；請確認紀錄內容或稍後重試。')
    write_report(result,args.out)
    print(f'完成：{len(result["runs"])} 次執行，{len(result["missions"])} 場任務，{len(result["findings"])} 類待查項目。')
    print(str((args.out/'summary.md').resolve()))
    return 0
if __name__=='__main__':raise SystemExit(main())
