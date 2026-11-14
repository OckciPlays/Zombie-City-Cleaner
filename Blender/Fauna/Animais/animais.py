# Os animais: monta cada bicho (a malha no tamanho do jogo, o esqueleto, os pesos e as ações) num .blend.
#
#   blender -b --factory-startup --python animais.py -- <saida.blend> <Nome> [<Nome> ...] [--sem-acoes]
#   (<Nome> = os de animais_dados.MODELOS; "todos" = todos)
#
# Cada bicho fica na coleção dele: a malha <Nome>, o esqueleto <Nome>_Esqueleto e as ações <Nome>_<Ação> (uma faixa de
# NLA muda por ação; a ativa é a <Nome>_Parado).

import bpy
import os
import sys

AQUI = r"C:\Users\gusta\OneDrive\Documentos\Projetos\Projeto Zombie City Cleaner\Modelo 3D\Animais"
ns = {"__name__": "animais"}
ARQUIVOS = ["animais_kit.py", "animais_quadrupede.py", "animais_aves.py", "animais_dados.py"]
ARQUIVOS += sorted(f for f in os.listdir(AQUI) if f.startswith("animais_dados_") and f.endswith(".py"))
ARQUIVOS += ["animais_poses.py", "animais_acoes.py"] + sorted(f for f in os.listdir(AQUI) if f.startswith("animais_acoes_") and f.endswith(".py"))
for arq in ARQUIVOS:
    p = os.path.join(AQUI, arq)
    if os.path.isfile(p):
        exec(compile(open(p, encoding="utf-8").read(), p, "exec"), ns)

from mathutils import Matrix, Vector  # noqa: E402


def montar(nome, acoes=True):
    d = ns["MODELOS"][nome]
    ns["apagar_colecao"](nome)
    ob, _ = ns["importar"](d["fbx"], nome)
    V = ns["vertices"](ob)
    tipo = d["escala"]
    k = tipo[2] / tipo[1] if tipo[0] == "lombo" else tipo[1]
    if d["grupo"] in ("ave", "sapo"):
        y0 = d["P"].get("y0", 0.0)
    elif "y0" in d["P"]:
        y0 = d["P"]["y0"]
    else:
        # o pivô no meio das patas: o meio do pé da frente (do boleto à ponta) e o do de trás
        P = d["P"]
        yd = 0.5 * (P["d"][4][1] + P["d"][5][1])
        yt = 0.5 * (P["t"][3][1] + P["t"][4][1])
        y0 = 0.5 * (yd + yt)
    zmin = V[:, 2].min()
    ob.data.transform(Matrix.Scale(k, 4) @ Matrix.Translation((0, -y0, -zmin)))
    ob.data.update()
    if d["grupo"] in ("ave", "sapo"):
        ossos = ns["ossos_" + d["grupo"]](d["P"], k, y0)
    else:
        ossos = ns["ossos_quadrupede"](d["P"], k, y0)
    rig = ns["esqueleto"](nome, ossos, ns["colecao"](nome))
    rig["escala"] = k
    rig["grupo"] = d["grupo"]
    sem = ns["pesar"](ob, rig)
    if "ajustar_pesos" in ns:
        ns["ajustar_pesos"](ob, rig, d)
    rig["escala"] = k
    rig["grupo"] = d["grupo"]
    print("@@@ montado", nome, "escala %.3f" % k, "ossos", len(rig.data.bones), "sem peso", sem)
    if acoes and "acoes_do_bicho" in ns:
        feitas = ns["acoes_do_bicho"](nome, ob, rig, d)
        print("@@@ acoes", nome, len(feitas))
    return ob, rig


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    saida = args[0]
    nomes = [a for a in args[1:] if not a.startswith("--")]
    if nomes == ["todos"]:
        nomes = list(ns["MODELOS"])
    if not bpy.data.filepath:
        for o in list(bpy.data.objects):
            bpy.data.objects.remove(o, do_unlink=True)
    for n in nomes:
        montar(n, acoes="--sem-acoes" not in args)
    bpy.context.scene.render.fps = ns["FPS"]
    bpy.ops.wm.save_as_mainfile(filepath=saida)


if __name__ == "__main__" or ns.get("__name__") == "animais":
    if "--" in sys.argv:
        main()
