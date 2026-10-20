"""Evidence matching and version-scoped resolution history; no game automation."""
import datetime,hashlib,json,re,unicodedata
from pathlib import Path

def script_clean(text):
    text=re.sub(r'&[A-Za-z][A-Za-z0-9.,+\-]*#','',str(text or ''))
    return re.sub(r'\s+',' ',re.sub(r'<[^>]*>',' ',text.replace('/',' '))).strip()
def normalized(text):return ''.join(c for c in unicodedata.normalize('NFKC',script_clean(text)) if c.isalnum())
def template_matches(raw,rendered):
    raw=re.sub(r'&[A-Za-z][A-Za-z0-9.,+\-]*#','',str(raw or ''))
    if not re.search(r'<N\d+>',raw,re.I):return False
    parts=re.split(r'(<N\d+>)',raw,flags=re.I)
    literal=''.join(normalized(p) for p in parts if not re.fullmatch(r'<N\d+>',p,re.I))
    if len(literal)<3 or len(parts)>41:return False
    pattern=''.join('.{0,128}?' if re.fullmatch(r'<N\d+>',p,re.I) else re.escape(normalized(p)) for p in parts)
    return re.fullmatch(pattern,normalized(rendered)) is not None

def version_tuple(v):
    if not isinstance(v,str) or not re.fullmatch(r'\d+\.\d+\.\d+(?:\.\d+)?',v):return None
    return tuple(int(x) for x in v.split('.'))+(0,)*(4-len(v.split('.')))

class ResolutionRegistry:
    def __init__(self,path=None):
        p=Path(path) if path else Path(__file__).with_name('resolutions.json')
        self.data=json.loads(p.read_text(encoding='utf-8-sig')) if p.exists() else {'entries':[]}
    def matching(self,rule,game,key,row):
        for entry in self.data.get('entries',[]):
            m=entry['match']
            if rule not in m['rules'] or (m.get('game') and game!=m['game']):continue
            if m.get('units') and row.get('data',{}).get('unit') not in m['units']:continue
            if m.get('keyPrefix') and not key.startswith(m['keyPrefix']):continue
            return entry
        return None
    def historical(self,rule,game,key,row):
        entry=self.matching(rule,game,key,row)
        if entry is None:return None
        if entry.get('affectedHashes'):
            return entry if row.get('_hash') in entry['affectedHashes'] else None
        observed=version_tuple(row.get('_version'));fixed=version_tuple(entry.get('implementedIn'))
        return entry if observed is not None and fixed is not None and observed<fixed else None
    def hint_id(self,text):return self.data.get('protectedHintFingerprints',{}).get(hashlib.sha256(normalized(text).encode('utf-8')).hexdigest())
