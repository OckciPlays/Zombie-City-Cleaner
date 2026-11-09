# A pegada certa das armas brancas (08/11/2026, a pedido: "no machado, por exemplo, a pegada está correta, porém o cabo do
# machado está ultrapassando entre o 2º e 3º dedo da mão esquerda, como se fosse um cigarro grande, e isso não deve
# acontecer. Quando se pega um machado, a mão do jogador deve recobrir todo o cabo, com os dedos fazendo uma espécie de
# 'colchão' sobre a madeira e não ultrapassando entre os dedos, pois aí não há estabilidade para gerar o golpe ... A mesma
# coisa ocorre com as outras armas brancas: a faca não está empunhada, e sim ultrapassando os dedos como no cigarro").
#
# Antes, a arma ia no "miolo" do punho fechado (o meio dos ossos dos dedos) e girada dentro dele para ficar em pé na tela: o
# cabo saía por entre os dedos. Agora, depois que o faca.py faz os clipes do jeito de antes (a arma presa ao punho direito,
# o golpe e a mão esquerda por IK), esta etapa refaz as mãos em volta do cabo, quadro a quadro. A arma fica onde estava em
# cada quadro (o desenho na tela, aprovado) e é a mão que muda:
# - o cabo atravessa a palma, perto da dobra da base dos dedos (PEGADA_Y), um pouco na diagonal (DIAGONAL graus: a mão
#   levemente inclinada, a cabeça para o lado do indicador e a ponta de baixo para a base do mínimo), a cabeça do lado do
#   polegar; a altura do cabo é a que deixa a pele da palma encostada nele, sem vão (08/11/2026, a pedido: "o cabo não
#   encosta na palma ... segurado só pela ponta dos dedos e pelo polegar, quase como uma pinça");
# - o giro da mão em volta do cabo é o que a deixa mais perto da mão de antes (a menor volta), no quadro 1 do Idle;
# - os dedos fecham em volta do cabo até a pele encostar nele (os pontos da mão nova, a do Maos_Novas_FP.fbx, presos a cada
#   osso; a primeira falange pelos pontos depois do nó, a segunda pelos depois da junta), e o polegar dá a volta no cabo
#   e deita sobre o indicador e o médio, do lado de fora, sem entrar no cabo nem nos dedos (_polegar: uma busca nos giros
#   dele; o polegar da mão nova é curto e a ponta chega até a lateral da falange do meio do indicador);
# - os braços seguem por IK de dois ossos (o do agua_animacoes.py: o cotovelo do lado onde estava e o antebraço com metade
#   do giro da mão); onde o braço esticado não alcança a pegada (o fim do soco), a arma recua o que faltar;
# - nas de duas mãos (o machado, o bastão e a lança), a esquerda pega o cabo do mesmo jeito, no mesmo lugar de antes (no y
#   -SEGUNDA_MAO da arma), nos clipes em que ela já estava no cabo (machado.CLIPES_COM_DUAS_MAOS); se o braço esquerdo
#   não alcança esse lugar (a lança), ela pega o cabo no ponto e no giro que deixam o pulso mais perto do de antes no
#   Idle (o antebraço vem de baixo, como antes), e fica nesse ponto em todos os quadros; a arma não sai do lugar; no fim
#   do soco (o braço esticado) a arma recua o que faltar, ou, com OMBRO_NO_ALCANCE na cfg (a lança), é o ombro que avança
#   (na estocada, até uns 18 cm o esquerdo e 5 cm o direito, fora da tela), e a arma fica exatamente onde estava.
# Os mesmos clipes, com os mesmos quadros (as durações e o momento do golpe não mudam).
#
# A mesma pegada (a arma no espaço do hand.R e os giros dos dedos) serve no corpo: o Blender/brancas_corpo.py usa
# pegada_da_arma().

import math
import os
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
BLENDER = os.path.dirname(AQUI)
if BLENDER not in sys.path:
    sys.path.insert(0, BLENDER)
import agua_animacoes as ag  # noqa: E402

MAOS_NOVAS = os.path.join(ag.JOGADOR, "Maos_Novas_FP.fbx")
DIAGONAL = 16.0         # o cabo na palma, da linha dos nós para os dedos (graus): a mão levemente inclinada (a mesma em
#                         todas as armas desde 09/11/2026; antes, cada cfg tinha as DIAGONAIS dela, de 35 a 60)
# (09/11/2026) a pegada canônica: os dedos fecham com ângulos fixos (a primeira e a segunda falange, em graus), escalados
# pelo raio do cabo (FECHA_FINO num cabo de RAIO_FINO, FECHA_GROSSO num de RAIO_GROSSO), o indicador um pouco mais aberto;
# depois cada dedo recua só o necessário para a pele não entrar mais que DENTRO_MAX no cabo
FECHA_FINO = (95.0, 90.0)
FECHA_GROSSO = (85.0, 80.0)
RAIO_FINO, RAIO_GROSSO = 0.012, 0.0165
FECHA_DEDO = {"index": -5.0, "middle": 0.0, "ring": 1.0, "pinky": 2.0}
DENTRO_MAX = 0.002
RECUO_MAX = 50              # (graus somados nas duas falanges: o dedo nunca abre mais que isso)
PEGADA_Y = 0.070        # o eixo do cabo, do pulso para os dedos (m, no espaço do hand: os nós ficam em 0,099)
PALMA_Z = 0.0165        # (sem os pontos da mão) a pele da palma embaixo da base dos dedos (m, abaixo do eixo do hand)
FOLGA = 0.0006          # a pele para no cabo com esta folga
RAIO_DEDO = 0.0097      # o raio dos dedos e do polegar da mão nova (m), para o polegar não entrar neles
RAIO_POLEGAR = 0.0108
POLEGAR_NO_DEDO = 0.008  # o polegar deita sobre a falange do meio do indicador e do médio, perto da junta (m no osso)
POLEGAR_PONTA = 0.030   # o ponto da ponta do polegar que deita ali (m no thumb2)
OMBRO_A_MAIS = 0.06     # o quanto o ombro pode avançar para a mão esquerda pegar perto de onde estava (OMBRO_NO_ALCANCE)
DEDOS = ("index", "middle", "ring", "pinky")


# ================================================================================================ a mão nova e o cabo

_PTS = None


def _pontos():
    if _PTS is None:
        pontos_da_mao_nova()
    return _PTS


def pontos_da_mao_nova():
    """Os pontos da pele da mão nova presos a cada osso (com metade do peso ou mais), no espaço do osso, no repouso:
    {osso: [Vector]} (lidos do Maos_Novas_FP.fbx, importado e apagado)."""
    global _PTS
    antes = set(bpy.data.objects)
    acoes = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=MAOS_NOVAS, automatic_bone_orientation=False, primary_bone_axis="Y",
                             secondary_bone_axis="X", ignore_leaf_bones=False)
    novos = [o for o in bpy.data.objects if o not in antes]
    rig = next(o for o in novos if o.type == "ARMATURE")
    pts = {}
    for o in novos:
        if o.type != "MESH":
            continue
        nomes = {g.index: g.name for g in o.vertex_groups}
        for v in o.data.vertices:
            for g in v.groups:
                if g.weight >= 0.5:
                    n = nomes[g.group]
                    B = rig.data.bones[n].matrix_local
                    pts.setdefault(n, []).append(B.inverted() @ (rig.matrix_world.inverted() @ o.matrix_world @ v.co))
    for o in novos:
        bpy.data.objects.remove(o, do_unlink=True)
    for a in list(bpy.data.actions):
        if a not in acoes:
            bpy.data.actions.remove(a)
    _PTS = pts
    return pts


def cabo(bm, y, meia=0.012):
    """O raio e o meio (x, z) do cabo da arma no y dela (no espaço da arma; bm: a malha da arma): o corte da malha pelo
    plano y (os pontos onde as arestas o atravessam), a metade da média da largura e da altura."""
    pts = []
    for e in bm.edges:
        a, b = e.verts[0].co, e.verts[1].co
        if (a.y - y) * (b.y - y) <= 0 and abs(a.y - b.y) > 1e-9:
            t = (y - a.y) / (b.y - a.y)
            pts.append(a.lerp(b, t))
    if not pts:
        return 0.014, (0.0, 0.0)
    x0, x1 = min(v.x for v in pts), max(v.x for v in pts)
    z0, z1 = min(v.z for v in pts), max(v.z for v in pts)
    return ((x1 - x0) + (z1 - z0)) / 4.0, ((x0 + x1) / 2.0, (z0 + z1) / 2.0)


_EIXOS = {}


def eixo_na_mao(rest, lado, raio):
    """O eixo do cabo no espaço do hand do lado: o ponto (no meio da mão) e a direção (para a cabeça). O cabo encosta na
    palma: a altura do eixo é a que deixa a pele da palma (os pontos do hand da mão nova) tocando o cabo, sem vão e sem
    entrar nele."""
    chave = (lado, round(raio, 5), DIAGONAL, PEGADA_Y)
    if chave in _EIXOS:
        return _EIXOS[chave][0].copy(), _EIXOS[chave][1].copy()
    H = rest["hand." + lado].inverted()
    idx = H @ rest["index1." + lado].translation
    pin = H @ rest["pinky1." + lado].translation
    t = Vector((idx.x - pin.x, 0.0, 0.0)).normalized()      # para o lado do polegar
    a = math.radians(DIAGONAL)
    d = (t * math.cos(a) + Vector((0.0, 1.0, 0.0)) * math.sin(a)).normalized()
    xm = sum((H @ rest[f + "1." + lado].translation).x for f in DEDOS) / 4.0
    z = -(PALMA_Z + raio)
    palma = [p for p in _pontos().get("hand." + lado, []) if p.z < 0.0]
    if palma:
        C0 = Vector((xm, PEGADA_Y, 0.0))
        n = d.cross(Vector((0.0, 0.0, 1.0))).normalized()
        R = raio + FOLGA
        zs = []
        for p in palma:
            v = p - C0
            if abs(v.dot(d)) > 0.05:
                continue
            u = v.dot(n)
            if abs(u) < R:
                zs.append(p.z - math.sqrt(R * R - u * u))
        if zs:
            z = min(zs)
    _EIXOS[chave] = (Vector((xm, PEGADA_Y, z)), d)
    return Vector((xm, PEGADA_Y, z)), d


def pegada(rest, lado, raio, meio, y_arma, phi):
    """G: os eixos da arma para o espaço do hand do lado, com o ponto do cabo em y_arma no eixo da pegada e o giro phi
    (graus) em volta do cabo (o -Y da arma é a cabeça, para o lado do polegar)."""
    C, d = eixo_na_mao(rest, lado, raio)
    Yw = -d
    z0 = Vector((0.0, 0.0, 1.0))
    z0 = (z0 - z0.dot(Yw) * Yw).normalized()
    p = math.radians(phi)
    Zw = (z0 * math.cos(p) + Yw.cross(z0) * math.sin(p)).normalized()
    Xw = Yw.cross(Zw)
    R = Matrix((Xw, Yw, Zw)).transposed().to_4x4()
    return Matrix.Translation(C) @ R @ Matrix.Translation(-Vector((meio[0], y_arma, meio[1])))


def melhor_giro(rest, lado, raio, meio, y_arma, W, H_antes):
    """O giro em volta do cabo que deixa a mão nova mais perto da de antes (W: a arma no mundo; H_antes: a mão de antes)."""
    q0 = H_antes.to_quaternion()
    melhor = None
    for phi in range(0, 360, 2):
        Hn = W @ pegada(rest, lado, raio, meio, y_arma, phi).inverted()
        ang = q0.rotation_difference(Hn.to_quaternion()).angle
        if melhor is None or ang < melhor[0]:
            melhor = (ang, phi)
    return melhor[1], math.degrees(melhor[0])


# ================================================================================================ os dedos em volta do cabo

def _dobra(a):
    return Matrix.Rotation(math.radians(-a), 4, "X")


def _no_segmento(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(1e-12, ab.dot(ab))))
    return (p - (a + ab * t)).length


def _polegar(rest, pai, lado, pts, C, d, R, posado, depois, rel):
    """O polegar fechado por cima: ele dá a volta no cabo (o lado do pulso) e deita sobre a falange do meio do indicador e
    do médio, do lado de fora (a 34.png do Pedro: "polegar envolvendo o cabo"), sem entrar no cabo nem nos dedos. Uma
    busca nos giros da base (dobra, abertura e torção) e na dobra da ponta. Devolve (o giro da base, a dobra da ponta)."""
    nt, nt2 = "thumb." + lado, "thumb2." + lado
    pt1, pt2 = depois(nt, 0.010)[::2], depois(nt2, 0.004)[::3]
    capsulas = []
    for dd in ("index", "middle", "ring"):
        n1, n2 = f"{dd}1.{lado}", f"{dd}2.{lado}"
        a0 = posado[n1].translation.copy()
        a1 = posado[n2].translation.copy()
        a2 = posado[n2] @ Vector((0.0, 0.040, 0.0))
        capsulas += [(a0, a1), (a1, a2)]
    # os alvos: as falanges do indicador e do médio por fora (longe do cabo), encostadas nelas; a ponta do polegar vai
    # para o mais perto que alcança
    alvos = []
    for t in (0.0, 0.008, 0.016, 0.024, 0.032, 0.040):
        Pi = posado["index2." + lado] @ Vector((0.0, t, 0.0))
        Pm = posado["middle2." + lado] @ Vector((0.0, t, 0.0))
        for meio in (Pi, Pi * 0.6 + Pm * 0.4):
            v = meio - C
            fora = (v - v.dot(d) * d).normalized()
            alvos.append(meio + fora * (RAIO_DEDO + RAIO_POLEGAR) * 0.92)
    # (08/11/2026) com o cabo bem na diagonal da palma (DIAGONAL grande), o indicador fica longe da ponta do polegar: ele
    # então dá a volta no cabo, encostado no lado de fora dele (o lado oposto à palma, perto do meio da mão), em vez de
    # ficar esticado ao longo do cabo
    if DIAGONAL > 20.0:
        longe_da_palma = Vector((0.0, 0.0, -1.0))
        longe_da_palma = (longe_da_palma - longe_da_palma.dot(d) * d).normalized()
        lado_polegar = d.cross(longe_da_palma).normalized()
        if lado_polegar.x * (1.0 if lado == "L" else -1.0) < 0:
            lado_polegar = -lado_polegar
        for s in (-0.015, 0.0, 0.015, 0.03):
            for giro in (0.0, 0.5):
                n = (longe_da_palma * math.cos(giro) + lado_polegar * math.sin(giro)).normalized()
                alvos.append(C + d * s + n * (R + RAIO_POLEGAR * 0.92))
    Pi = posado["index2." + lado] @ Vector((0.0, POLEGAR_NO_DEDO, 0.0))
    Pm = posado["middle2." + lado] @ Vector((0.0, POLEGAR_NO_DEDO, 0.0))
    alvo = alvos[0]
    Rt = rel(nt)
    Rt2 = rel(nt2)

    def avaliar(a, b, c, a3):
        q = Matrix.Rotation(math.radians(a), 4, "Z") @ _dobra(b) @ Matrix.Rotation(math.radians(c), 4, "Y")
        Mt = Rt @ q
        Mt2 = Mt @ Rt2 @ _dobra(a3)
        pen = 0.0
        vao = 1.0
        for M, ps in ((Mt, pt1), (Mt2, pt2)):
            for p in ps:
                w = M @ p
                u = w - C
                r = (u - u.dot(d) * d).length
                if r < R:
                    pen += R - r
                elif M is Mt2:
                    vao = min(vao, r - R)
                for s0, s1 in capsulas:
                    dd_ = _no_segmento(w, s0, s1)
                    if dd_ < RAIO_DEDO - 0.0015:
                        pen += (RAIO_DEDO - 0.0015) - dd_
        ponta = Mt2 @ Vector((0.0, POLEGAR_PONTA, 0.0))
        longe = min((ponta - t).length for t in alvos)
        return longe + 0.5 * vao + 4.0 * pen, q, pen

    melhor = None
    for a in range(-150, 151, 15):
        for b in range(-60, 121, 15):
            for c in range(-90, 91, 15):
                for a3 in range(0, 91, 15):
                    r = avaliar(a, b, c, a3)
                    if melhor is None or r[0] < melhor[0][0]:
                        melhor = (r, (a, b, c, a3))
    for passo in (4, 2):
        a0, b0, c0, d0 = melhor[1]
        for a in (a0 - passo * 2, a0 - passo, a0, a0 + passo, a0 + passo * 2):
            for b in (b0 - passo * 2, b0 - passo, b0, b0 + passo, b0 + passo * 2):
                for c in (c0 - passo * 2, c0, c0 + passo * 2):
                    for a3 in (d0 - passo * 2, d0 - passo, d0, d0 + passo, d0 + passo * 2):
                        if not 0 <= a3 <= 95:
                            continue
                        r = avaliar(a, b, c, a3)
                        if r[0] < melhor[0][0]:
                            melhor = (r, (a, b, c, a3))
    (custo, q, pen), (a, b, c, a3) = melhor
    Mt = Rt @ q
    Mt2 = Mt @ Rt2 @ _dobra(a3)
    _polegar.ultimo = {"giro": (a, b, c, a3), "longe_do_alvo": round(custo - 4.0 * pen, 4), "dentro": round(pen, 5),
                       "base": tuple(round(x, 3) for x in Rt.translation), "alvo": tuple(round(x, 3) for x in alvo),
                       "ponta": tuple(round(x, 3) for x in Mt2 @ Vector((0.0, POLEGAR_PONTA, 0.0))),
                       "Pi": tuple(round(x, 3) for x in Pi), "Pm": tuple(round(x, 3) for x in Pm),
                       "i2base": tuple(round(x, 3) for x in posado["index2." + lado].translation)}
    return q, a3


def dedos_no_cabo(rest, pai, lado, pts, raio):
    """Os giros dos dedos (os ossos dos dedos e do polegar do lado, como quatérnios locais) fechados em volta do cabo, no
    espaço do hand. A pegada canônica (09/11/2026, a 34.png do Pedro: "punho firme, sem abertura"): cada dedo fecha com
    ângulos fixos (FECHA_FINO/FECHA_GROSSO pelo raio do cabo, FECHA_DEDO por dedo: o indicador um pouco mais aberto), as
    pontas voltando para a palma, e recua só o necessário para a pele não entrar mais que DENTRO_MAX no cabo (antes cada
    falange fechava até o primeiro ponto encostar, e com a palma inclinada o indicador parava esticado, apontando). O
    polegar dá a volta no cabo e deita sobre o indicador e o médio (_polegar)."""
    C, d = eixo_na_mao(rest, lado, raio)
    R = raio + FOLGA

    def rel(n):
        return rest[pai[n]].inverted() @ rest[n]

    def dentro(M, ps):
        pior = -1e9
        for p in ps:
            v = (M @ p) - C
            pior = max(pior, raio - (v - v.dot(d) * d).length)
        return pior

    def depois(n, y):
        return [p for p in pts.get(n, []) if p.y > y]

    k = max(0.0, min(1.0, (raio - RAIO_FINO) / (RAIO_GROSSO - RAIO_FINO)))
    base = [FECHA_FINO[i] + (FECHA_GROSSO[i] - FECHA_FINO[i]) * k for i in (0, 1)]
    Mh = Matrix.Identity(4)
    giros, posado, info = {}, {}, {}
    for dd in DEDOS:
        n1, n2 = f"{dd}1.{lado}", f"{dd}2.{lado}"
        p1, p2 = depois(n1, 0.004), depois(n2, 0.002)
        b1, b2 = base[0] + FECHA_DEDO.get(dd, 0.0), base[1] + FECHA_DEDO.get(dd, 0.0)
        R1, R2 = rel(n1), rel(n2)
        escolha = None
        # o menor recuo (somado nas duas falanges) que deixa a pele no máximo DENTRO_MAX dentro do cabo; no mesmo recuo,
        # o que entra menos
        for s in range(0, RECUO_MAX + 1, 2):
            melhor = None
            for i in range(0, s + 1, 2):
                a1, a2 = b1 - i, b2 - (s - i)
                M1 = Mh @ R1 @ _dobra(a1)
                pen = max(dentro(M1, p1), dentro(M1 @ R2 @ _dobra(a2), p2))
                if melhor is None or pen < melhor[0]:
                    melhor = (pen, a1, a2)
            if melhor[0] <= DENTRO_MAX or s >= RECUO_MAX:
                escolha = melhor
                break
        pen, a1, a2 = escolha
        M1 = Mh @ R1 @ _dobra(a1)
        giros[n1], giros[n2] = _dobra(a1), _dobra(a2)
        posado[n1] = M1
        posado[n2] = M1 @ R2 @ _dobra(a2)
        info[dd] = (round(a1), round(a2), round(pen * 1000, 1))
    q, a3 = _polegar(rest, pai, lado, pts, C, d, R, posado, depois, rel)
    giros["thumb." + lado], giros["thumb2." + lado] = q, _dobra(a3)
    dedos_no_cabo.ultimo = info
    return {n: m.to_quaternion() for n, m in giros.items()}


# ================================================================================================ a pegada de uma arma

def pegada_da_arma(cfg, malha, rest, pai, pts, W_ref=None, H_ref=None, HL_ref=None):
    """Tudo da pegada de uma arma (cfg: o módulo dela; malha: o bmesh dela, no espaço da arma): na direita, o y do cabo, o
    raio e o meio, o giro, G (a arma no espaço do hand.R) e os giros dos dedos; na esquerda (se a arma tiver
    SEGUNDA_MAO), o mesmo. W_ref e H_ref (a arma e a mão de antes, num quadro de referência) escolhem o giro; sem eles,
    o giro de antes (GIRO_DA_PEGADA da cfg, ou 0)."""
    out = {}
    lados = [("R", -cfg.NO_PUNHO.y, H_ref)]
    if hasattr(cfg, "SEGUNDA_MAO"):
        lados.append(("L", -cfg.SEGUNDA_MAO, HL_ref))      # (como no machado.py: a SEGUNDA_MAO m da origem da arma)
    for lado, y, Hr in lados:
        raio, meio = cabo(malha, y)
        if W_ref is not None and Hr is not None:
            phi, ang = melhor_giro(rest, lado, raio, meio, y, W_ref, Hr)
        else:
            phi, ang = getattr(cfg, "GIRO_DA_PEGADA", {}).get(lado, 0.0), None
        G = pegada(rest, lado, raio, meio, y, phi)
        out[lado] = {"y": y, "raio": raio, "meio": meio, "phi": phi, "volta_da_mao": ang, "G": G,
                     "dedos": dedos_no_cabo(rest, pai, lado, pts, raio)}
    return out


# ================================================================================================ os clipes da primeira pessoa

def _avaliar(rig, acao, f):
    ad = rig.animation_data
    ad.action = acao
    if hasattr(ad, "action_slot") and len(acao.slots):
        ad.action_slot = acao.slots[0]
    bpy.context.scene.frame_set(f)
    return {pb.name: pb.matrix.copy() for pb in rig.pose.bones}


def _gravar(rig, esq, nome, quadros):
    """Uma ação nova com o nome dado, com as matrizes de cada quadro ({quadro: {osso: matriz}}) em todos os ossos."""
    ad = rig.animation_data
    acao = bpy.data.actions.new(nome)
    acao.use_fake_user = True
    ad.action = acao
    anteriores = {}
    for f in sorted(quadros):
        M = quadros[f]
        for n in esq.ordem:
            pb = rig.pose.bones[n]
            pb.rotation_mode = "QUATERNION"
            loc, rot, esc = esq.base(M, n).decompose()
            if n in anteriores and anteriores[n].dot(rot) < 0:
                rot = -rot
            anteriores[n] = rot
            pb.location, pb.rotation_quaternion, pb.scale = loc, rot, esc
            pb.keyframe_insert("location", frame=f, group=n)
            pb.keyframe_insert("rotation_quaternion", frame=f, group=n)
            pb.keyframe_insert("scale", frame=f, group=n)
    ad.action = None
    return acao


def _mao_no_cabo(esq, M, M_antes, lado, H, dedos, ombro=False):
    """Põe a mão do lado em H (por IK, o cotovelo do lado de antes) e os dedos fechados; com ombro, o ombro (a cabeça do
    upper_arm) avança o que faltar para o braço alcançar. Devolve o alcance do braço (1 = esticado), o quanto o pulso
    ficou longe do pedido (m) e o quanto o ombro avançou (m)."""
    S = M_antes["upper_arm." + lado].translation.copy()
    avancou = 0.0
    if ombro:
        maximo = 0.995 * (esq.comprimento("upper_arm." + lado, "forearm." + lado)
                          + esq.comprimento("forearm." + lado, "hand." + lado))
        v = H.translation - S
        if v.length > maximo:
            avancou = v.length - maximo
            S = S + v.normalized() * avancou
    polo = M_antes["forearm." + lado].translation - S
    alcance = ag.braco(esq, M, lado, S, H.translation.copy(), H.to_3x3(), polo)
    esq.refazer_abaixo(M, "hand." + lado, {n: q.to_matrix().to_4x4() for n, q in dedos.items()})
    return alcance, (M["hand." + lado].translation - H.translation).length, avancou


def _ao_alcance(esq, M_antes, W, peg, esquerda):
    """A arma recua (em direção ao ombro direito) o tanto que faltar para a mão direita alcançar a pegada (no soco do braço
    esticado, o pulso da pegada nova fica alguns centímetros mais longe que o de antes). A esquerda nunca mexe na arma:
    o lugar dela no cabo já é um que ela alcança (_esquerda_ao_alcance)."""
    for _ in range(3):
        mudou = False
        for lado in ["R"]:
            S = M_antes["upper_arm." + lado].translation
            H = W @ peg[lado]["G"].inverted()
            v = H.translation - S
            maximo = 0.995 * (esq.comprimento("upper_arm." + lado, "forearm." + lado)
                              + esq.comprimento("forearm." + lado, "hand." + lado))
            if v.length > maximo:
                W = Matrix.Translation(-v.normalized() * (v.length - maximo)) @ W
                mudou = True
        if not mudou:
            break
    return W


def _giro_perto_do_pulso(rest, lado, raio, meio, y_arma, W, H_antes, por_radiano=0.03):
    """O giro em volta do cabo que deixa o pulso novo mais perto do de antes (m), contando também a volta da mão
    (por_radiano m a cada radiano). Devolve (giro, custo)."""
    q0 = H_antes.to_quaternion()
    melhor = None
    for phi in range(0, 360, 4):
        Hn = W @ pegada(rest, lado, raio, meio, y_arma, phi).inverted()
        custo = (Hn.translation - H_antes.translation).length + por_radiano * q0.rotation_difference(
            Hn.to_quaternion()).angle
        if melhor is None or custo < melhor[1]:
            melhor = (phi, custo)
    return melhor


def _esquerda_ao_alcance(rig, esq, clipes, no_punho, cfg, malha, peg, pai, pts, duas_maos, W_ref, HL_ref):
    """Se o braço esquerdo não alcança o lugar dele no cabo em algum quadro dos clipes de duas mãos (a lança: 24 cm à
    frente da direita), a mão esquerda pega o cabo no ponto (fora dos 8 cm em volta da direita) e no giro que deixam o
    pulso mais perto do de antes no Idle (onde o IK de antes a deixou, embaixo do cabo: o antebraço continua vindo de
    baixo, do mesmo lado da tela), entre os que o braço alcança no Idle (com o ombro OMBRO_A_MAIS à frente, na
    OMBRO_NO_ALCANCE). Ela fica nesse ponto em todos os quadros: na estocada, é o ombro que avança. A arma não sai do
    lugar. Devolve a pegada."""
    maximo = 0.995 * (esq.comprimento("upper_arm.L", "forearm.L") + esq.comprimento("forearm.L", "hand.L"))
    limite = maximo + (OMBRO_A_MAIS if getattr(cfg, "OMBRO_NO_ALCANCE", False) else 0.0)
    quadros = []
    for acao in clipes:
        if acao.name.split("|", 1)[1] not in duas_maos:
            continue
        for f in range(int(acao.frame_range[0]), int(acao.frame_range[1]) + 1):
            M_antes = _avaliar(rig, acao, f)
            quadros.append((M_antes["hand.R"] @ no_punho, M_antes["upper_arm.L"].translation.copy()))
    G0 = peg["L"]["G"]
    if all(((W @ G0.inverted()).translation - S).length <= maximo for W, S in quadros):
        return peg
    y0, yR = peg["L"]["y"], peg["R"]["y"]
    ys = [v.co.y for v in malha.verts]
    tabela = []
    y = min(ys) + 0.02
    while y < max(ys) - 0.02:
        raio, meio = cabo(malha, y)
        if abs(y - yR) >= 0.08 and raio < 0.025:
            phi, custo = _giro_perto_do_pulso(esq.rest, "L", raio, meio, y, W_ref, HL_ref)
            G = pegada(esq.rest, "L", raio, meio, y, phi)
            tabela.append({"y": y, "raio": raio, "meio": meio, "phi": phi, "G": G, "Gi": G.inverted(), "custo": custo,
                           "longe": ((W_ref @ G.inverted()).translation - HL_ref.translation).length})
        y += 0.01
    idle = next(a for a in clipes if a.name.endswith("|Idle"))
    S_ref = _avaliar(rig, idle, int(idle.frame_range[0]))["upper_arm.L"].translation
    alcanca = [c for c in tabela if ((W_ref @ c["Gi"]).translation - S_ref).length <= limite]
    preferido = min(alcanca or tabela, key=lambda c: c["custo"])
    volta = math.degrees(HL_ref.to_quaternion().rotation_difference((W_ref @ preferido["Gi"]).to_quaternion()).angle)
    peg["L"] = dict(preferido, dedos=dedos_no_cabo(esq.rest, pai, "L", pts, preferido["raio"]), volta_da_mao=volta)
    print(f"  a esquerda não alcançava o cabo em y {y0:.3f} em todos os quadros: pega em y {preferido['y']:.3f}, giro "
          f"{preferido['phi']} (o pulso a {preferido['longe'] * 100:.1f} cm do de antes no Idle, a mão {volta:.0f} graus "
          f"virada)")
    return peg


def refazer_clipes(rig, clipes, no_punho, cfg, malha, duas_maos=()):
    """Refaz os clipes da primeira pessoa com a pegada certa (a arma onde estava em cada quadro). Devolve a lista nova dos
    clipes (as ações novas, com os mesmos nomes) e a pegada (pegada_da_arma)."""
    esq = ag.Esqueleto(rig)
    pai = {b.name: b.parent.name for b in rig.data.bones if b.parent}
    pts = pontos_da_mao_nova()
    idle = next(a for a in clipes if a.name.endswith("|Idle"))
    f_ref = int(idle.frame_range[0])
    M_ref = _avaliar(rig, idle, f_ref)
    W_ref = rig.matrix_world @ M_ref["hand.R"] @ no_punho
    peg = pegada_da_arma(cfg, malha, esq.rest, pai, pts, W_ref, M_ref["hand.R"], M_ref.get("hand.L"))
    if "L" in peg and duas_maos:
        peg = _esquerda_ao_alcance(rig, esq, clipes, no_punho, cfg, malha, peg, pai, pts, duas_maos, W_ref,
                                   M_ref.get("hand.L"))
    for lado, p in peg.items():
        print(f"  pegada {lado}: o cabo em y {p['y']:.3f}, raio {p['raio'] * 100:.2f} cm, giro {p['phi']}°, a mão "
              f"girou {p['volta_da_mao']:.0f}° da de antes; dedos (graus) " +
              ", ".join(f"{n.split('.')[0]} {math.degrees(q.angle):.0f}" for n, q in p["dedos"].items()))
    ombro = getattr(cfg, "OMBRO_NO_ALCANCE", False)
    novos = []
    for acao in clipes:
        nome = acao.name.split("|", 1)[1]
        f0, f1 = int(acao.frame_range[0]), int(acao.frame_range[1])
        esquerda = "L" in peg and nome in duas_maos
        quadros = {}
        pior = {"R": (0.0, 0.0, 0.0), "L": (0.0, 0.0, 0.0)}
        for f in range(f0, f1 + 1):
            M_antes = _avaliar(rig, acao, f)
            M = dict(M_antes)
            W = M_antes["hand.R"] @ no_punho
            if not ombro:
                W = _ao_alcance(esq, M_antes, W, peg, esquerda)
            r = _mao_no_cabo(esq, M, M_antes, "R", W @ peg["R"]["G"].inverted(), peg["R"]["dedos"], ombro)
            pior["R"] = (max(pior["R"][0], r[0]), max(pior["R"][1], r[1]), max(pior["R"][2], r[2]))
            if esquerda:
                r = _mao_no_cabo(esq, M, M_antes, "L", W @ peg["L"]["G"].inverted(), peg["L"]["dedos"], ombro)
                pior["L"] = (max(pior["L"][0], r[0]), max(pior["L"][1], r[1]), max(pior["L"][2], r[2]))
            quadros[f] = M
        velho = acao.name
        acao.name = velho + "_antes"
        nova = _gravar(rig, esq, velho, quadros)
        novos.append(nova)
        print(f"  {velho}: refeito ({f0}-{f1}){', as duas mãos' if esquerda else ''}; o maior alcance, a maior falta do "
              "pulso e o maior avanço do ombro: "
              + ", ".join(f"{k} {v[0]:.3f} / {v[1] * 100:.1f} cm / {v[2] * 100:.1f} cm" for k, v in pior.items() if v[0] > 0))
    for a in clipes:
        bpy.data.actions.remove(a)
    return novos, peg
