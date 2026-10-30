# Roda uma etapa do cupê num Blender em segundo plano (não mexe no Blender aberto):
#   blender -b --factory-startup --python Blender/Cupe/rodar.py -- construir          (cria o Cupe.blend)
#   blender -b --factory-startup Blender/Cupe/Cupe.blend --python Blender/Cupe/rodar.py -- assar cor   (aspereza, metal, normal, ao)
#   blender -b --factory-startup Blender/Cupe/Cupe.blend --python Blender/Cupe/rodar.py -- compor_exportar
# (uv: refaz só o desembrulhar.) Um canal por processo: ~1 min cada, na CPU.
import os
import sys
import traceback

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import cupe  # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
try:
    etapa = args[0]
    cupe.preparar_catalogo()
    if etapa == "construir":
        cupe.construir()
        cupe.desembrulhar()
    elif etapa == "uv":
        cupe.desembrulhar()
    elif etapa == "assar":
        # assar <canal> [A|B]: um atlas só por processo, se a memória estiver curta
        cupe.assar(args[1:2], atlas_do_passe=tuple(args[2:]) or ("A", "B"))
    elif etapa == "compor_exportar":
        cupe.compor()
        cupe.exportar()
    else:
        raise SystemExit("etapa desconhecida: " + etapa)
    cupe.salvar()
    print("ETAPA OK:", " ".join(args))
except Exception:
    traceback.print_exc()
    print("ETAPA FALHOU")
    sys.exit(1)
