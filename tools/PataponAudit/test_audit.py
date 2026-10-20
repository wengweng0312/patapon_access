import json,tempfile,unittest
from pathlib import Path
import patapon_audit as audit
_test_temp=(Path.cwd()/"work/audit-test-temp").resolve()
assert _test_temp.is_relative_to(Path.cwd().resolve())
_test_temp.mkdir(parents=True,exist_ok=True)
tempfile.tempdir=str(_test_temp)

def row(kind,ms=0,mission=1,**data):return {'schema':2,'run':'test','mission':mission,'seq':ms+1,'elapsedMs':ms,'kind':kind,'data':data,'_ms':ms,'_file':'test.jsonl','_line':ms+1}
def scan(events):
 t=audit.Triage();events.sort(key=lambda r:r['_ms']);t.process('test',events,[],0);return t.report()
def rules(report):return {f['rule'] for f in report['findings']}
class ScannerTests(unittest.TestCase):
 def base(self):return [row('session',plugin='test'),row('mission-start',1,game='P2')]
 def text(self,ms=10,**extra):return row('text-change',ms,text='危險！趕快退後！',path='TextManager/BalloonText_0',maxVisibleCharacters=20,**extra)
 def test_visible_candidate(self):self.assertIn('visible-text-no-speech-evidence',rules(scan(self.base()+[self.text()])))
 def test_queue_is_evidence_not_actual_delivery(self):
  r=scan(self.base()+[self.text(),row('dialogue-queued',15,text='危險！趕快退後！')]);self.assertNotIn('visible-text-no-speech-evidence',rules(r));self.assertEqual(r['runs'][0]['speechEvidence'],'queue-only')
 def test_cross_mission_not_matched(self):self.assertIn('visible-text-no-speech-evidence',rules(scan(self.base()+[self.text(),row('dialogue-queued',15,mission=2,text='危險！趕快退後！')])))
 def test_expected_filter(self):self.assertNotIn('visible-text-no-speech-evidence',rules(scan(self.base()+[self.text(),row('battle-chant-suppressed',15,text='危險！趕快退後！')])))
 def test_invisible(self):self.assertNotIn('visible-text-no-speech-evidence',rules(scan(self.base()+[self.text(visible=False)])))
 def test_numeric_not_global_fault(self):self.assertFalse(rules(scan(self.base()+[row('text-change',10,text='120',path='TextManager/DrumSysmteText_0')])))
 def test_boss_missed(self):self.assertIn('windup-unrecognized',rules(scan(self.base()+[row('boss-script-motion',20,unit='unit228_01_01',action=10,phase=0,motion=9,attackEntry=False)])))
 def test_boss_cue_same_unit(self):
  events=self.base()+[row('boss-script-motion',20,unit='unit228_01_01',action=10,phase=0,motion=9,attackEntry=True),row('boss-attack-cue',21,unit='unit228_01_01',played=True)]
  self.assertNotIn('recognized-without-cue',rules(scan(events)))
 def test_other_unit_cue_no_cover(self):self.assertIn('recognized-without-cue',rules(scan(self.base()+[row('boss-script-motion',20,unit='unit228_01_01',action=10,phase=0,motion=9,attackEntry=True),row('boss-attack-cue',21,unit='unit201_01_01',played=True)])))
 def test_disabled_cue(self):self.assertNotIn('windup-unrecognized',rules(scan(self.base()+[row('audit-settings',2,bossAttackCue=False),row('boss-script-motion',20,unit='unit228_01_01',action=10,phase=0,motion=9,attackEntry=False)])))
 def test_audio_mute_not_playback_failure(self):self.assertNotIn('cue-playback-failed',rules(scan(self.base()+[row('cue-playback',20,sound='boss.wav',success=False,muted=True)])))
 def test_backend_failure(self):self.assertIn('speech-delivery-failed',rules(scan(self.base()+[row('speech-dispatch',20,text='提示',accepted=False,reason='controller-unavailable')])))
 def test_missing_dispatch_after_telemetry(self):
  events=self.base()+[row('speech-dispatch',10,text='其他',accepted=True),row('dialogue-queued',30,text='危險！趕快退後！')]
  self.assertIn('queued-not-dispatched',rules(scan(events)))
 def test_multiple_parts_dedupe_and_partial_tail(self):
  with tempfile.TemporaryDirectory() as temp:
   p=Path(temp);r=row('session');r={k:v for k,v in r.items() if not k.startswith('_')}
   (p/'a.jsonl').write_text(json.dumps(r)+'\n',encoding='utf-8');(p/'b.jsonl').write_text(json.dumps(r)+'\n{"partial":',encoding='utf-8')
   rows,issues,dupes=audit.load_run([p/'a.jsonl',p/'b.jsonl']);self.assertEqual(len(rows),1);self.assertEqual(dupes,1);self.assertEqual(issues[0]['kind'],'partial-live-tail')
 def test_dedupe_across_runs_keeps_evidence(self):
  t=audit.Triage()
  for name in ['a','b']:
   events=self.base()+[self.text()]
   for r in events:r['run']=name
   t.process(name,events,[],0)
  f=t.report()['findings'][0];self.assertEqual(f['occurrences'],2);self.assertEqual(len(f['runs']),2)
 def test_no_logs_fails(self):
  with tempfile.TemporaryDirectory() as temp:
   with self.assertRaises(SystemExit):audit.main(['--logs',temp,'--out',temp+'/out'])
 def test_all_malformed_fails(self):
  with tempfile.TemporaryDirectory() as temp:
   (Path(temp)/'bad.jsonl').write_text('invalid\n',encoding='utf-8')
   with self.assertRaises(SystemExit):audit.main(['--logs',temp,'--out',temp+'/out'])
 def test_numeric_timestamp_nan_safe(self):self.assertEqual(audit.safe_float('NaN'),0)
 def test_no_data_event_accepted(self):
  with tempfile.TemporaryDirectory() as temp:
   p=Path(temp)/'a.jsonl';p.write_text('{"kind":"example","run":"a","seq":1}\n',encoding='utf-8')
   rows,issues,dupes=audit.load_run([p]);self.assertEqual(rows[0]['data'],{})
 def test_level_resolution_counts_as_pipeline(self):
  events=self.base()+[self.text(),row('dialogue-level-resolved',15,text='危險！趕快退後！')]
  self.assertNotIn('visible-text-no-speech-evidence',rules(scan(events)))
 def test_fixed_old_start_moves_to_history(self):
  events=self.base()+[row('boss-script-motion',20,unit='unit217_01_01',action=10,phase=0,motion=3,attackEntry=False)]
  events[0]['data']['plugin']='0.6.51';r=scan(events)
  self.assertNotIn('windup-unrecognized',rules(r));self.assertEqual(len(r['handledHistory']),1)
 def test_new_same_start_is_regression(self):
  events=self.base()+[row('boss-script-motion',20,unit='unit217_01_01',action=10,phase=0,motion=3,attackEntry=False)]
  events[0]['data']['plugin']='0.6.52';r=scan(events)
  self.assertIn('windup-unrecognized',rules(r));self.assertEqual(r['findings'][0]['possibleRegressionOf'],'p2-timer-helper-crab-starts')
 def test_unknown_version_not_hidden(self):
  events=self.base()+[row('boss-script-motion',20,unit='unit217_01_01',action=10,phase=0,motion=3,attackEntry=False)]
  self.assertIn('windup-unrecognized',rules(scan(events)))
 def test_new_template_failure_not_hidden(self):
  events=self.base()+[row('dialogue-template-unresolved',20,text='大人',template='<N0>大人')];events[0]['data']['plugin']='0.6.52'
  self.assertIn('template-resolution-failed',rules(scan(events)))
 def test_native_closed_japanese_window_not_fault(self):
  events=self.base()+[row('native-window-state',5,windowPath='Root/Dialog',open=False),row('text-change',10,path='Root/Dialog/Description',text='日本語を保持します',maxVisibleCharacters=20)]
  self.assertNotIn('visible-text-no-speech-evidence',rules(scan(events)))
 def test_native_open_japanese_window_stays_visible(self):
  events=self.base()+[row('native-window-state',5,windowPath='Root/Dialog',open=True),row('text-change',10,path='Root/Dialog/Description',text='日本語を保持します',maxVisibleCharacters=20)]
  self.assertIn('visible-text-no-speech-evidence',rules(scan(events)))
 def test_legacy_timestamps_are_relative(self):
  with tempfile.TemporaryDirectory() as temp:
   p=Path(temp)/'legacy-123-0.jsonl';p.write_text('{"kind":"session","time":"2026-10-20T10:00:00+08:00"}\n{"kind":"example","time":"2026-10-20T10:00:02+08:00"}\n',encoding='utf-8')
   rows,_,_=audit.load_run([p]);self.assertEqual(rows[1]['_ms'],2000);self.assertEqual(rows[0]['run'],'legacy-123')
 def test_filtered_template_is_expected(self):
  events=self.base()+[row('game-event',9,args=['&H14O0#<N0>大人/您好']),row('ambient-dialogue-suppressed',10,text='大人 您好'),row('text-change',12,path='TextManager/BalloonText_0',text='Kami大人 您好',maxVisibleCharacters=30)]
  self.assertNotIn('visible-text-no-speech-evidence',rules(scan(events)))
 def test_old_name_gap_handled_new_gap_active(self):
  for version,history in [('0.6.51',True),('0.6.52',False)]:
   events=self.base()+[row('game-event',9,args=['<N0>大人/請小心']),row('dialogue-queued',10,text='大人 請小心'),row('text-change',12,path='TextManager/BalloonText_0',text='Kami大人 請小心',maxVisibleCharacters=30)];events[0]['data']['plugin']=version;r=scan(events)
   self.assertEqual('template-substitution-gap' in rules(r),not history)
 def test_hint_filter_regression(self):
  for version,history in [('0.6.51',True),('0.6.52',False)]:
   events=self.base()+[row('ambient-dialogue-suppressed',10,text='大人… 【雨】有各式各樣的效果',source='P2.Game.Window.Message.openW')];events[0]['data']['plugin']=version;r=scan(events)
   self.assertEqual('protected-hint-suppressed' in rules(r),not history)
if __name__=='__main__':unittest.main()
