"""Cruza packages.lock.json con el catálogo oficial NuGet, sin desactivar TLS."""
import datetime
import gzip
import hashlib
import io
import json
import pathlib
import re
import sys
import urllib.request
import urllib.parse

ROOT = pathlib.Path(__file__).resolve().parent.parent
INDEX = "https://api.nuget.org/v3/vulnerabilities/index.json"


def leer(url):
    parsed = urllib.parse.urlparse(url)
    if parsed.scheme != "https" or parsed.hostname != "api.nuget.org":
        raise ValueError("Origen de avisos no permitido")
    with urllib.request.urlopen(urllib.request.Request(url, headers={"Accept-Encoding": "identity"}), timeout=30) as response:
        if urllib.parse.urlparse(response.geturl()).hostname != "api.nuget.org":
            raise ValueError("Redirección de origen no permitida")
        data = response.read(20_000_001)
    if len(data) > 20_000_000:
        raise ValueError("Respuesta demasiado grande")
    if data[:2] == b"\x1f\x8b":
        with gzip.GzipFile(fileobj=io.BytesIO(data)) as stream:
            data = stream.read(50_000_001)
    if len(data) > 50_000_000:
        raise ValueError("Catálogo demasiado grande")
    return json.loads(data), hashlib.sha256(data).hexdigest()


def comparar(a, b):
    def partes(s):
        s = s.split("+", 1)[0]
        release, _, pre = s.partition("-")
        numbers = release.split(".")
        if len(numbers) > 4 or not all(x.isdigit() for x in numbers):
            raise ValueError("Versión NuGet no reconocida: " + s)
        return tuple(map(int, numbers + ["0"] * (4 - len(numbers)))), pre.lower().split(".") if pre else []
    x, px = partes(a)
    y, py = partes(b)
    if x != y:
        return (x > y) - (x < y)
    if not px or not py:
        return (not px) - (not py)
    for u, v in zip(px, py):
        if u == v:
            continue
        if u.isdigit() and v.isdigit():
            return (int(u) > int(v)) - (int(u) < int(v))
        if u.isdigit() != v.isdigit():
            return -1 if u.isdigit() else 1
        return (u > v) - (u < v)
    return (len(px) > len(py)) - (len(px) < len(py))


def contiene(version, intervalo):
    if intervalo.startswith("[") and intervalo.endswith("]") and "," not in intervalo:
        return comparar(version, intervalo[1:-1]) == 0
    m = re.fullmatch(r"([\[(])\s*([^,]*)\s*,\s*([^\])]*?)\s*([\])])", intervalo)
    if not m:
        raise ValueError("Rango de avisos no reconocido: " + intervalo)
    apertura, minimo, maximo, cierre = m.groups()
    if minimo and (comparar(version, minimo.strip()) < 0 or apertura == "(" and comparar(version, minimo.strip()) == 0):
        return False
    if maximo and (comparar(version, maximo.strip()) > 0 or cierre == ")" and comparar(version, maximo.strip()) == 0):
        return False
    return True


def auditar():
    paquetes = set()
    archivos = []
    for path in ROOT.rglob("packages.lock.json"):
        if any(p in {"bin", "obj", ".git"} for p in path.relative_to(ROOT).parts):
            continue
        archivos.append(str(path.relative_to(ROOT)))
        for framework in json.loads(path.read_text(encoding="utf-8-sig"))["dependencies"].values():
            for nombre, entrada in framework.items():
                if entrada.get("resolved") and entrada.get("type") != "Project":
                    paquetes.add((nombre.lower(), entrada["resolved"]))
    if not archivos or not paquetes:
        raise ValueError("Faltan archivos de bloqueo o dependencias")
    index, digest = leer(INDEX)
    hallazgos = []
    control_vulnerable = False
    coincidencias_catalogo = set()
    fuentes = [{"url": INDEX, "sha256": digest}]
    for entrada in index:
        datos, digest = leer(entrada["@id"])
        control_vulnerable |= any(contiene("9.0.11", a["versions"]) for a in datos.get("system.security.cryptography.pkcs", []))
        control_vulnerable |= any(contiene("8.0.0", a["versions"]) for a in datos.get("system.text.json", []))
        fuentes.append({"url": entrada["@id"], "sha256": digest, "actualizado": entrada.get("@updated")})
        for nombre, version in sorted(paquetes):
            if nombre in datos:
                coincidencias_catalogo.add(nombre)
            for aviso in datos.get(nombre, []):
                if contiene(version, aviso["versions"]):
                    hallazgos.append({"paquete": nombre, "version": version, **aviso})
    if not control_vulnerable or not coincidencias_catalogo:
        raise ValueError("No pasó el control de avisos conocidos; no se considera válida la auditoría")
    resultado = {"fechaUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "fuentes": fuentes,
                 "archivosBloqueo": archivos, "paquetesVersiones": len(paquetes), "hallazgos": hallazgos,
                 "controlVersionVulnerableDetectada": control_vulnerable,
                 "estado": "CON_HALLAZGOS" if hallazgos else "SIN_AVISOS_APLICABLES_EN_LA_INSTANTANEA"}
    (ROOT / "AUDITORIA-DEPENDENCIAS.json").write_text(json.dumps(resultado, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps(resultado, indent=2, ensure_ascii=False))
    return 2 if hallazgos else 0


if __name__ == "__main__":
    try:
        sys.exit(auditar())
    except Exception as error:
        print("AUDITORIA INCOMPLETA: " + str(error), file=sys.stderr)
        sys.exit(1)
