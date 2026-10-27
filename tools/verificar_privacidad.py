"""Revisa fuentes versionadas sin imprimir valores de posibles datos privados."""
import pathlib,re,subprocess,sys
ROOT=pathlib.Path(__file__).resolve().parents[1]
BINARY={'.pdf','.png','.jpg','.jpeg','.gif','.ico','.dll','.exe','.pdb','.zip','.nupkg'}

def revisar():
    cmd=['git','-c','safe.directory='+ROOT.as_posix(),'-C',str(ROOT)]
    files=subprocess.check_output(cmd+['ls-files','-z']).decode('utf-8').split('\0')
    hallazgos=[]
    for rel in filter(None,files):
        path=ROOT/rel
        if path.suffix.lower() in BINARY or not path.is_file():continue
        raw=path.read_bytes()
        if b'\x00' in raw[:4096]:continue
        for n,line in enumerate(raw.decode('utf-8-sig',errors='replace').splitlines(),1):
            for match in re.finditer(r'(?i)[a-z]:[/\\]+Users[/\\]+([^/\\\s"\']+)',line):
                if match.group(1).lower() not in {'public','default','usuario','...','<usuario>'}:
                    hallazgos.append((rel,n,'ruta de perfil Windows'))
            for email in re.findall(r'[A-Za-z0-9_.+%-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}',line):
                dominio=email.split('@',1)[1].lower()
                if dominio not in {'example.com','example.org','example.net','example.invalid','users.noreply.github.com'} and not dominio.endswith('.invalid'):
                    hallazgos.append((rel,n,'correo literal que requiere revisión'))
    for archivo,linea,tipo in sorted(set(hallazgos)):print(f'{archivo}:{linea}: {tipo}; valor omitido')
    if hallazgos:print('Revisar candidatos. No se publicaron sus valores.');return 1
    print('Fuentes versionadas: sin correos literales ajenos a dominios de ejemplo ni rutas de perfiles detectadas.')
    print('No analiza imágenes/PDF ni reemplaza Gitleaks o una revisión del historial.')
    return 0
if __name__=='__main__':sys.exit(revisar())
