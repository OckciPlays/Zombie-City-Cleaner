# O braço esticado com o cotovelo à frente (pegada_fogo_esticar.py) (19/11/2026, a pedido do Pedro, na espingarda: "ao invés de quebrar esse pulso,
# leva o cotovelo mais pra frente, com isso o braço estica um pouco"), agora no rifle e na sniper: lê o FBX dos braços como
# está no jogo (as pegadas aprovadas em 12/11) e, em cada quadro de cada clipe, deixa a mão (e os dedos) exatamente onde
# estão e refaz só o braço do lado pedido: o cotovelo e o ombro escolhidos pela mesma busca da espingarda
# (cotovelo_por_quadro: o antebraço na linha da mão, o pulso bom até `dobra_boa` e cortado em `dobra_corte`, a torção
# baixa, o braço de cima fora da tela, o cotovelo perto do de antes e sem saltos de um quadro para o outro).
#   blender -b --factory-startup --disable-autoexec --python Blender/Armas/pegada_fogo_esticar.py -- <entrada.fbx> <saida.fbx>
#       <lados: L, R ou LR> [chave=valor ...]   (os parâmetros da busca; ex.: dobra_boa=12 tor_boa=20)
# O que foi para o jogo em 19/11/2026 (a entrada: o FBX que o pegada_fogo.py gera para a arma, o de 12/11):
#   Rifle:        -- Jogador_FP_Rifle.fbx <saida> L                        (a esquerda: dobra 52 -> 13, torção 38 -> 0)
#   Sniper:       -- Jogador_FP_Sniper.fbx <saida> LR tor_boa=15 pq_tor=1.0 (as duas: torção 41 -> 0, dobra 24 -> 11 na esquerda)
#   Sucata_Rifle: -- Jogador_FP_Sucata_Rifle.fbx <saida> LR tor_boa=15 pq_tor=1.0
#   (a espingarda não passa por aqui: o motor dela já tem a busca; os pesos novos estão no _PARAMS_ESPINGARDA)
# A conferência: pegada_fogo_conferir.py -- <rotulo> <Arma> fbx=<saida> medir fotos <pasta>.
import ast
import importlib
import json
import os
import sys
import time

import bpy
from mathutils import Vector

REPO = r"D:\Zombiegame\Zombie-City-Cleaner"
for p in (os.path.join(REPO, "Blender"), os.path.join(REPO, "Blender", "Armas")):
    if p not in sys.path:
        sys.path.insert(0, p)
import agua_animacoes as ag  # noqa: E402
import duas_maos as dm  # noqa: E402
import pegada as pg  # noqa: E402
import pegada_fogo as pf  # noqa: E402

if not any(isinstance(f, pf._Motores) for f in sys.meta_path):
    sys.meta_path.insert(0, pf._Motores())
esp = importlib.import_module("fogo_espingarda_esp")

args = sys.argv[sys.argv.index("--") + 1:]
entrada, saida, lados = args[0], os.path.abspath(args[1]), args[2]
# os parâmetros da busca: os da esquerda da espingarda (19/11), mais o que vier na linha de comando
P = {"dobra_boa": 12, "dobra_corte": 30, "desvio_corte": 24, "pq_tela": 60, "pq_meio": 0, "pq_pref": 150, "pq_ombro": 150,
     "pq_cont": 150, "pq_tor": 0.3, "tor_boa": 50, "pq_gira_ombro": 1}
for a in args[3:]:
    k, v = a.split("=", 1)
    P[k] = ast.literal_eval(v)
print("ESTICAR", entrada, "->", saida, "lados", lados, json.dumps(P))
t0 = time.time()

ag.nova_cena()
objs = ag.importar(entrada)
fps = bpy.context.scene.render.fps
rig = next(o for o in objs if o.type == "ARMATURE")
bracos = next(o for o in objs if o.type == "MESH" and o.name.startswith("FP_Bracos"))
acoes = {a.name.split("|", 1)[1]: a for a in bpy.data.actions if a.name.startswith(rig.name + "|")}
rig.animation_data_create()
esq = ag.Esqueleto(rig)

novas = {}
for nome, a in sorted(acoes.items()):
    f0, f1 = int(a.frame_range[0]), int(a.frame_range[1])
    quadros = {}
    prev = {l: None for l in lados}
    pior = {l: [0.0, 0.0, 0.0, 0.0] for l in lados}
    antes = {l: [0.0, 0.0, 0.0] for l in lados}
    for f in range(f0, f1 + 1):
        M0 = pf._matrizes(rig, a, f)
        M = dict(M0)
        for lado in lados:
            ua, fa, mao = "upper_arm." + lado, "forearm." + lado, "hand." + lado
            d0, dv0, t0_ = dm.pulso(esq, M0, lado)
            antes[lado] = [max(antes[lado][0], abs(d0)), max(antes[lado][1], abs(dv0)), max(antes[lado][2], abs(t0_))]
            H = M0[mao].copy()
            S0, E0 = M0[ua].translation.copy(), M0[fa].translation.copy()
            Ec, S = esp.cotovelo_por_quadro(esq, lado, H, S0, E0, E0, P, 0.0, prev[lado], W0=H.translation.copy())
            prev[lado] = Ec
            Mas = dict(M)
            m = Mas[ua].copy()
            m.translation = S
            Mas[ua] = m
            av = dm.braco_no_lugar(esq, M, Mas, lado, H, Ec - S, None)
            d, dv, t = dm.pulso(esq, M, lado)
            pior[lado] = [max(pior[lado][0], abs(d)), max(pior[lado][1], abs(dv)), max(pior[lado][2], abs(t)), max(pior[lado][3], av * 1000)]
            erro_mao = (M[mao].translation - H.translation).length * 1000
            if erro_mao > 0.5:
                print(f"  AVISO {nome} q{f} {lado}: a mão saiu {erro_mao:.1f} mm do lugar")
        quadros[f] = M
    for lado in lados:
        print(f"  CLIPE {nome:8s} {lado}: pulso antes dobra/desvio/torção {antes[lado][0]:.0f}/{antes[lado][1]:.0f}/{antes[lado][2]:.0f}"
              f" -> {pior[lado][0]:.0f}/{pior[lado][1]:.0f}/{pior[lado][2]:.0f}; o ombro avançou {pior[lado][3]:.0f} mm")
    novas[nome] = (a, quadros)

for nome, (a, quadros) in novas.items():
    nome_acao = a.name
    nova = pg._gravar(rig, esq, nome_acao + "_nova", quadros)
    bpy.data.actions.remove(a)
    nova.name = nome_acao
    acoes[nome] = nova
boca = next(o for o in bpy.data.objects if o.name.startswith("Boca_FP"))
bpy.context.scene.render.fps, bpy.context.scene.render.fps_base = fps, 1.0
os.makedirs(os.path.dirname(saida), exist_ok=True)
pf.exportar(rig, bracos, boca, acoes, saida)
print(f"ESTICAR OK {time.time() - t0:.1f} s")
