# As etapas das armas novas, num Blender em segundo plano (uma arma e uma ou mais etapas por processo):
#   blender -b --factory-startup --disable-autoexec --python Blender/Armas/rodar.py -- <arma> <etapa> [<etapa>...]
# <arma>: pistola, rifle, sniper ou espingarda (o arma_<arma>.py). As etapas, na ordem: construir (do modelo de fora até
# o UV; grava o <Arma>.blend), assar (assa e grava as texturas), exportar (o FBX), fotos (Docs/Armas/capturas), rascunho
# (fotos rápidas com as superfícies em cores, para conferir), imagens (o ícone e a foto do inventário, em Art/UI/Icones) ou
# tudo (construir, assar, exportar, fotos e imagens).
import importlib
import os
import sys
import traceback

import bpy

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import armas  # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:]
cfg = importlib.import_module("arma_" + args[0])
etapas = args[1:] or ["tudo"]
if "tudo" in etapas:
    etapas = ["construir", "assar", "exportar", "fotos", "imagens"]
try:
    for etapa in etapas:
        print(f"== {cfg.NOME}: {etapa}")
        if etapa == "construir":
            bpy.ops.wm.read_factory_settings(use_empty=True)
            armas.construir(cfg)
            armas.desembrulhar(cfg)
            armas.referencias(cfg)
            armas.salvar(cfg)
            continue
        if os.path.normcase(bpy.data.filepath) != os.path.normcase(armas.blend(cfg)):
            bpy.ops.wm.open_mainfile(filepath=armas.blend(cfg), load_ui=False)
        if etapa == "assar":
            armas.assar(cfg)
            armas.compor(cfg)
            armas.salvar(cfg)
        elif etapa == "exportar":
            armas.exportar(cfg)
            armas.salvar(cfg)
        elif etapa == "fotos":
            armas.fotos(cfg)
        elif etapa == "rascunho":
            armas.rascunho(cfg)
        elif etapa == "imagens":
            armas.imagens(cfg)
        else:
            raise SystemExit("etapa desconhecida: " + etapa)
    print("ETAPAS OK")
except Exception:
    traceback.print_exc()
    print("ETAPA FALHOU")
    sys.exit(1)
