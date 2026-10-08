"""Read root LoadFolders.xml case-insensitively; keep source spelling and conditions.

Conditional alternatives are inventoried together. The translation mod applies
only to actual loaded definitions whose values still match the recovered source.
"""
import xml.etree.ElementTree as E

def selected_folders(paths, read, mid, version='1.6'):
    prefix='M/'+str(mid)+'/'
    candidates=[p for p in paths if p.casefold()==(prefix+'LoadFolders.xml').casefold()]
    if len(candidates)>1:
        raise ValueError('Ambiguous root LoadFolders.xml: '+str(mid))
    if candidates:
        root=E.fromstring(read(candidates[0]));branch=root.find('v'+version)
        if branch is None:
            return [('',{},'Root fallback; no exact version branch')]
        return [(x.text.strip().replace('\\','/').strip('/'),dict(x.attrib),'LoadFolders v'+version)
                for x in branch if x.text and x.text.strip()]
    result=[('',{},'Root without LoadFolders')]
    if any(p.startswith(prefix+version+'/') for p in paths):result.append((version,{},'Version folder without LoadFolders'))
    return result

def component_folder(path, folders, component):
    tail='/'.join(path.split('/')[2:])
    return next((item for item in folders if tail.startswith((item[0]+'/' if item[0] else '')+component+'/')),None)
