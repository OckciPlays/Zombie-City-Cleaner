# O coquetel molotov (10/11/2026, a etapa 4 das armas de sucata: o fogo): a garrafa de vidro com o pano enfiado no
# gargalo, segurada pelo gargalo na mão direita (como a tocha: tocha.py, faca.py). O fogo é da Unity, no vazio Pavio
# (PONTA): a boca dos braços vai para lá.
#
# - O modelo (no espaço da arma solta: a ponta, o pano, em -Y; o alto em +Z; a origem no meio do gargalo, onde a mão
#   segura): a garrafa verde com o rótulo, o gargalo, a boca, o pano enfiado e caído para um lado, e a fita prata que o
#   prende na boca.
# - Na primeira pessoa: parada como a tocha (embaixo, à direita, em pé: o pano para cima e um pouco para a frente),
#   andando com o punho dos braços dos punhos; o "SocoDireito" vira o arremesso (ARREMESSO: o braço sobe por trás da cabeça e joga para a
#   frente; a garrafa sai da mão no quadro SOLTA). A garrafa vai num osso próprio, "garrafa" (filho do hand.R, no repouso
#   dele), que vira escala 0 do quadro SOLTA até o fim do arremesso: a Unity cria a garrafa voando nesse quadro, no lugar
#   do vazio da boca (preso ao osso "garrafa").
#
# Como rodar (os braços de primeira pessoa; a malha solta e os itens são do Blender/fogo.py):
#   ARMAS_SEM_BLEND=1 ARMAS_SAIDA=<pasta> blender -b --factory-startup --disable-autoexec --python Blender/Armas/molotov.py -- bracos
#   (bracos: o Jogador_FP_Molotov.fbx; conferir: as fotos do Idle e de cada quadro do arremesso, na pasta ARMAS_FOTOS)

import math
import os
import sys

from mathutils import Matrix, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import faca  # noqa: E402

NOME = "Molotov"
FOTO = "molotov"
MODELO = "Molotov"
PECAS = ["corpo"]
ICONE = (128, 128)
ANGULO_LISO = 40
CORES = {
    "vidro": (2, 10),         # 4d6a34 (o vidro verde)
    "vidro_escuro": (2, 9),   # 3a4a2c (o fundo e a boca)
    "rotulo": (4, 4),         # b8b2a0
    "faixa": (3, 10),         # 8a2e26 (a faixa do rótulo)
    "pano": (3, 9),           # d2cbb2
    "pano_sujo": (3, 12),     # 9a4f27 (o pano molhado de combustível)
    "fita": (3, 14),          # a3a6a4
}
METAL = set()
# parada como a tocha (tocha.py: em pé na mão, um pouco para a frente; o mesmo ajuste no punho, que deixa o pulso natural
# com a pegada canônica): o gargalo no lugar do cabo, o pano no lugar da chama
GIRO_PARA_A_FRENTE = 78.0
GIRO_NO_EIXO = 0.0
NO_PUNHO = Vector((0.0, 0.0, 0.0))
PEGADA_Y = 0.0
LIMITE_TORCAO = 90.0
AJUSTE_FP = (-35.0, -27.5, 0.03, -0.015, -0.015)
DIAGONAIS = {"R": (16, 20, 25, 30, 35), "L": (16, 20, 25, 30, 35)}
ORTHO_CONFERIR = 0.8
# o pano (onde o fogo nasce), no espaço da arma
PONTA = Vector((0.0, -0.088, 0.004))
PAVIO = PONTA
# as fotos de conferência: os quadros do arremesso
QUADROS_DO_GOLPE = (1, 3, 5, 6, 7, 8, 9, 11)
GIRO_DOS_PUNHOS = 1.0
# parada, a garrafa sobe um pouco do lugar da tocha (no espaço da câmera, m): o corpo dela, embaixo da mão, aparece
SOBE = Vector((0.015, -0.02, 0.10))
# o arremesso (o "SocoDireito", quadros 1 a 11, 24 por segundo): (quadro, o meio do gargalo no espaço da câmera dos
# braços (x para a esquerda da tela, -y para a frente, z para cima; m), para onde o pano aponta). O quadro 1 é a pose
# parada. O braço sobe pela direita até atrás da cabeça (fora da tela, a 30 cm do olho), vem por cima e solta na frente,
# com o braço esticado; o pano fica para cima (o pulso como no martelo) e deita para a frente na hora de soltar.
SOLTA = 8
ARREMESSO = [
    (1, None, None),
    (3, (-0.30, -0.25, 0.08), (0.0, 0.45, 1.0)),
    (5, (-0.30, 0.02, 0.17), (0.0, 0.9, 0.6)),
    (6, (-0.27, -0.10, 0.21), (0.0, 0.25, 1.0)),
    (7, (-0.18, -0.32, 0.18), (0.0, -0.3, 1.0)),
    (8, (-0.08, -0.50, 0.08), (0.0, -0.65, 1.0)),
    (9, (-0.03, -0.48, -0.10), (0.15, -0.6, 0.7)),
    (11, (-0.12, -0.42, -0.20), (0.1, -0.2, 1.0)),
]


def _torno(m, perfil, sup, lados=12):
    """Um sólido de revolução em volta do Y: o perfil [(y, raio)], fechado nas pontas."""
    bm = m.bm
    aneis = []
    for y, r in perfil:
        aneis.append([bm.verts.new((r * math.cos(2 * math.pi * k / lados), y, r * math.sin(2 * math.pi * k / lados)))
                      for k in range(lados)])
    vs = [v for a in aneis for v in a]
    for a, b in zip(aneis, aneis[1:]):
        for k in range(lados):
            k2 = (k + 1) % lados
            bm.faces.new((a[k], a[k2], b[k2], b[k]))
    bm.faces.new(aneis[0][::-1])
    bm.faces.new(aneis[-1])
    m._fim(vs, sup, 0.0)


def modelo(m):
    """A garrafa (o gargalo na origem, ao longo do Y; o fundo em +Y, a boca em -Y) e o pano."""
    # o vidro: a boca, o gargalo, o ombro e o corpo
    _torno(m, [(-0.044, 0.0125), (-0.044, 0.0165), (-0.036, 0.0165), (-0.036, 0.0135), (0.030, 0.0135), (0.048, 0.022),
               (0.066, 0.031), (0.078, 0.033), (0.214, 0.033), (0.219, 0.030), (0.219, 0.012)], "vidro", lados=12)
    # o fundo mais escuro e o rótulo com a faixa
    m.tubo("y", (0.0, 0.2185, 0.0), 0.0305, 0.0305, 0.002, "vidro_escuro", lados=12)
    m.tubo("y", (0.0, 0.142, 0.0), 0.0338, 0.0338, 0.085, "rotulo", lados=12)
    m.tubo("y", (0.0, 0.128, 0.0), 0.0342, 0.0342, 0.016, "faixa", lados=12)
    # o pano: o rolo enfiado na boca, a ponta para fora caída para um lado e um pedaço solto
    m.tubo("y", (0.0, -0.052, 0.0), 0.0118, 0.0105, 0.022, "pano", lados=8)
    m.caixa((-0.013, -0.098, -0.006), (0.011, -0.060, 0.010), "pano", chanfro=0.002)
    m.caixa((-0.008, -0.112, 0.002), (0.016, -0.090, 0.016), "pano_sujo", chanfro=0.002)
    m.caixa((0.009, -0.074, -0.004), (0.017, -0.046, 0.008), "pano", chanfro=0.0015)
    # a fita prata em volta da boca, prendendo o pano
    m.tubo("y", (0.0, -0.040, 0.0), 0.0175, 0.0175, 0.008, "fita", lados=12)


# ------------------------------------------------------------------------------------------------ a pose na tela
def _catmull(p0, p1, p2, p3, t):
    t2, t3 = t * t, t * t * t
    return 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3)


def _arremesso(f, W1):
    """O lugar da garrafa (no mundo, já com o ajuste no punho) no quadro f do arremesso: W1 é a pose do quadro 1; cada
    chave gira a garrafa o mínimo para o pano apontar para onde ela manda (o giro em volta do gargalo continua o do
    quadro 1: a mão não roda no gargalo), e as chaves se ligam por uma curva suave e pelo giro mais curto."""
    pano1 = (W1.to_3x3() @ Vector((0.0, -1.0, 0.0))).normalized()
    chaves = []
    for q, lugar, pano in ARREMESSO:
        if lugar is None:
            chaves.append((q, W1))
            continue
        R = pano1.rotation_difference(Vector(pano).normalized()).to_matrix() @ W1.to_3x3()
        chaves.append((q, Matrix.Translation(Vector(lugar)) @ R.to_4x4()))
    qs = [q for q, _ in chaves]
    if f <= qs[0]:
        return chaves[0][1]
    if f >= qs[-1]:
        return chaves[-1][1]
    i = max(k for k in range(len(qs) - 1) if qs[k] <= f)
    t = (f - qs[i]) / (qs[i + 1] - qs[i])
    P = [c[1].translation for c in chaves]
    p = _catmull(P[max(0, i - 1)], P[i], P[i + 1], P[min(len(P) - 1, i + 2)], t)
    qa, qb = chaves[i][1].to_quaternion(), chaves[i + 1][1].to_quaternion()
    if qa.dot(qb) < 0:
        qb = -qb
    return Matrix.Translation(p) @ qa.slerp(qb, t).to_matrix().to_4x4()


def arma_fp(nome, acao, f, W, M_antes, rig, cfg=None):
    """O lugar da garrafa (no espaço do esqueleto) no quadro f do clipe nome: parada, onde a tocha estaria (W, o punho dos
    braços dos punhos); no SocoDireito, o arremesso. O duas_maos ainda põe o AJUSTE_FP depois (W @ A): o arremesso, feito
    já com ele, volta sem ele (@ A⁻¹)."""
    sobe = rig.matrix_world.inverted().to_3x3() @ SOBE
    if nome != "SocoDireito":
        return Matrix.Translation(sobe) @ W
    import duas_maos
    import pegada
    A = duas_maos._ajuste(AJUSTE_FP, Vector((0.0, PEGADA_Y, 0.0)))
    no_punho = M_antes["hand.R"].inverted() @ W
    M1 = pegada._avaliar(rig, acao, int(acao.frame_range[0]))
    W1 = rig.matrix_world @ Matrix.Translation(sobe) @ M1["hand.R"] @ no_punho @ A
    pegada._avaliar(rig, acao, f)
    Wm = _arremesso(f, W1)
    return rig.matrix_world.inverted() @ Wm @ A.inverted()


def depois_dos_bracos(rig, corpo, boca, clipes, n_verts_arma):
    """A garrafa num osso próprio ("garrafa", filho do hand.R, no repouso dele): os vértices dela passam para ele, a boca
    (o pano) vai presa nele, e no SocoDireito ele vira escala 0 do quadro SOLTA até o fim (a garrafa saiu da mão)."""
    import bpy
    bpy.context.view_layer.objects.active = rig
    for o in bpy.context.view_layer.objects:
        o.select_set(o is rig)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = rig.data.edit_bones
    mao = eb["hand.R"]
    g = eb.new("garrafa")
    g.head, g.tail, g.roll = mao.head.copy(), mao.tail.copy(), mao.roll
    g.parent = mao
    g.use_connect = False
    g.use_deform = True
    bpy.ops.object.mode_set(mode="OBJECT")
    # os vértices da garrafa (os últimos da malha dos braços, postos pelo faca.bracos) passam do hand.R para o osso novo
    vg_mao = corpo.vertex_groups["hand.R"]
    vg = corpo.vertex_groups.get("garrafa") or corpo.vertex_groups.new(name="garrafa")
    n = len(corpo.data.vertices)
    idx = list(range(n - n_verts_arma, n))
    vg_mao.remove(idx)
    vg.add(idx, 1.0, "REPLACE")
    # a boca presa ao osso novo, no mesmo lugar
    Mb = boca.matrix_world.copy()
    boca.parent = rig
    boca.parent_type = "BONE"
    boca.parent_bone = "garrafa"
    bpy.context.view_layer.update()
    boca.matrix_world = Mb
    # as chaves do osso: parado (escala 1) em todos os clipes; no SocoDireito, escala ~0 a partir do quadro SOLTA
    pb = rig.pose.bones["garrafa"]
    pb.rotation_mode = "QUATERNION"
    ad = rig.animation_data
    for a in clipes:
        faca._usar(rig, a)
        f0, f1 = int(a.frame_range[0]), int(a.frame_range[1])
        fim = a.name.endswith("|SocoDireito")
        for f in range(f0, f1 + 1):
            pb.location = (0.0, 0.0, 0.0)
            pb.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
            pb.scale = (0.001,) * 3 if (fim and f >= SOLTA) else (1.0,) * 3
            for caminho in ("location", "rotation_quaternion", "scale"):
                pb.keyframe_insert(caminho, frame=f, group="garrafa")
    ad.action = None
    print(f"  molotov: o osso garrafa com {len(idx)} vértices; some no quadro {SOLTA} do SocoDireito")


if __name__ == "__main__" and "--" in sys.argv:
    faca.rodar(sys.modules[__name__], sys.argv[sys.argv.index("--") + 1:] or ["bracos"])
