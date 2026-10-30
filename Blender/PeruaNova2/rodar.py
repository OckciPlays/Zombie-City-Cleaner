# Roda uma etapa da perua v2 num Blender em segundo plano (não mexe no Blender aberto):
#   blender -b Blender/PeruaNova/PeruaNova.blend  --python Blender/PeruaNova2/rodar.py -- construir
#   blender -b Blender/PeruaNova2/PeruaNova2.blend --python Blender/PeruaNova2/rodar.py -- assar cor   (aspereza, metal, normal, ao)
#   blender -b Blender/PeruaNova2/PeruaNova2.blend --python Blender/PeruaNova2/rodar.py -- compor_exportar
# "construir" parte do .blend da v1 (só lê) e grava o PeruaNova2.blend; as outras etapas abrem e gravam o PeruaNova2.blend.
import os
import sys
import traceback

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import perua_nova2 as v2  # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
try:
    etapa = args[0]
    if etapa == "construir":
        v2.polir_materiais()
        v2.construir()
        v2.desembrulhar()
    elif etapa == "uv":
        v2.desembrulhar()
    elif etapa == "assar":
        v2.assar(args[1:])
    elif etapa == "compor_exportar":
        v2.compor()
        v2.exportar()
    else:
        raise SystemExit("etapa desconhecida: " + etapa)
    v2.salvar()
    print("ETAPA OK:", " ".join(args))
except Exception:
    traceback.print_exc()
    print("ETAPA FALHOU")
    sys.exit(1)
