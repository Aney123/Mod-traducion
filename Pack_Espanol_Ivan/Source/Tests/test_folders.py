from pathlib import Path
import json, sys, zipfile
P=Path(__file__).resolve().parent;W=P.parent.parent
if (P.parent/'Tools').exists():sys.path.insert(0,str(P.parent/'Tools'))
from load_folders import selected_folders, component_folder
cases=[]
fixture=b'<loadFolders><v1.6><li>Common</li><li>1.6</li><li IfModActive="CETeam.CombatExtended">1.6/CE</li><li IfModNotActive="CETeam.CombatExtended">1.6/Vanilla</li></v1.6></loadFolders>'
for name in ['LoadFolders.xml','loadFolders.xml','loadfolders.xml','LOADFOLDERS.XML']:
    paths={'M/1/'+name:fixture,'M/1/1.6/CE/Defs/Helmet.xml':b'','M/1/1.6/Vanilla/Defs/Helmet.xml':b'','M/1/1.5/CE/Defs/Helmet.xml':b''}
    folders=selected_folders(paths,paths.__getitem__,'1')
    assert component_folder('M/1/1.6/CE/Defs/Helmet.xml',folders,'Defs')[1]=={'IfModActive':'CETeam.CombatExtended'}
    assert component_folder('M/1/1.6/Vanilla/Defs/Helmet.xml',folders,'Defs')[1]=={'IfModNotActive':'CETeam.CombatExtended'}
    assert component_folder('M/1/1.5/CE/Defs/Helmet.xml',folders,'Defs') is None
    cases.append({'test':'root filename '+name,'passed':True})
source=Path(sys.argv[1]) if len(sys.argv)>1 else W/'upload/RimWorld_Fuentes_Para_Traducir(2).zip'
z=zipfile.ZipFile(source);paths={n.replace('\\','/'):n for n in z.namelist() if not n.endswith(('\\','/'))}
read=lambda path:z.read(paths[path])
for mid in ['3469863391','3472989392','3480089301','3482266407']:
    folders=selected_folders(paths,read,mid)
    assert any(x[0]=='1.6/CE' for x in folders) and any(x[0]=='1.6/Vanilla' for x in folders)
    assert not any(x[0].startswith('1.5') for x in folders)
    cases.append({'test':'actual Tasty Armory '+mid,'passed':True})
helmet='M/3480089301/1.6/Vanilla/Defs/vanilla_NasalHelm.xml'
assert component_folder(helmet,selected_folders(paths,read,'3480089301'),'Defs')
assert b'TA_SpectacleHelm' in read(helmet) and b'spectacle helm' in read(helmet)
cases.append({'test':'actual reported helmet source recovered','passed':True})
(P/'folder_test_results.json').write_text(json.dumps({'tests':cases},indent=2))
print('PASS',len(cases),'folder selection checks, including actual CE/Vanilla sources')
