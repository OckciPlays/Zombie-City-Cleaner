# As etapas das armas estilizadas (v2), num Blender em segundo plano:
#   blender -b --factory-startup --disable-autoexec --python Blender/Armas/rodar_v2.py -- <rifle|espingarda|sniper> <etapa>...
# As etapas: construir (da arma antiga até a malha nova; grava o <Arma>2.blend), exportar (o FBX e a paleta), fotos
# (Docs/Armas/capturas), imagens (o ícone e a foto do inventário), rascunho (as cores com as mãos de referência) ou tudo.
import importlib
import os
import sys
import traceback

import bpy

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import armas  # noqa: E402
import estilizadas  # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:]
# o nome do módulo (v3_mosin) ou só o da arma (rifle: o v2_rifle)
cfg = importlib.import_module(args[0] if os.path.exists(os.path.join(AQUI, args[0] + ".py")) else "v2_" + args[0])
etapas = args[1:] or ["tudo"]
if "tudo" in etapas:
    etapas = ["construir", "exportar", "fotos", "imagens"]
try:
    for etapa in etapas:
        print(f"== {cfg.NOME}: {etapa}")
        if etapa == "construir":
            bpy.ops.wm.read_factory_settings(use_empty=True)
            (cfg.construir if hasattr(cfg, "construir") else estilizadas.construir)(cfg)
            armas.referencias(cfg)
            armas.salvar(cfg)
            continue
        if os.path.normcase(bpy.data.filepath) != os.path.normcase(armas.blend(cfg)):
            bpy.ops.wm.open_mainfile(filepath=armas.blend(cfg), load_ui=False)
        if etapa == "exportar":
            estilizadas.exportar(cfg)
            armas.salvar(cfg)
        elif etapa == "fotos":
            armas.fotos(cfg, usar_final=False)
        elif etapa == "imagens":
            armas.imagens(cfg, usar_final=False)
        elif etapa == "rascunho":
            armas.rascunho(cfg)
        else:
            raise SystemExit("etapa desconhecida: " + etapa)
    print("ETAPAS OK")
except Exception:
    traceback.print_exc()
    print("ETAPA FALHOU")
    sys.exit(1)
