# Roda uma etapa da caminhonete num Blender em segundo plano (não mexe no Blender aberto), uma versão por vez:
#   blender -b --factory-startup --python rodar.py -- v1 construir                 (cria o Caminhonete_v1.blend, com o UV)
#   blender -b --factory-startup --python rodar.py -- v1 rascunho                  (só as malhas, para as fotos de forma)
#   blender -b --factory-startup Caminhonete_v1.blend --python rodar.py -- v1 assar cor A   (aspereza, metal, normal, ao; A e B)
#   blender -b --factory-startup Caminhonete_v1.blend --python rodar.py -- v1 compor_exportar
# Na CPU (a Unity costuma estar aberta: OCO_CPU=1).
import os
import sys
import traceback

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import caminhonete as cam  # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
try:
    cam.VERSAO = args[0]
    etapa = args[1]
    cam.preparar_catalogo()
    if etapa == "construir":
        cam.construir()
        cam.desembrulhar()
    elif etapa == "rascunho":
        cam.construir()
    elif etapa == "uv":
        cam.desembrulhar()
    elif etapa == "assar":
        cam.assar(args[2:3], atlas_do_passe=tuple(args[3:]) or ("A", "B"))
    elif etapa == "compor_exportar":
        cam.compor()
        cam.exportar()
    else:
        raise SystemExit("etapa desconhecida: " + etapa)
    cam.salvar()
    print("ETAPA OK:", " ".join(args))
except Exception:
    traceback.print_exc()
    print("ETAPA FALHOU")
    sys.exit(1)
