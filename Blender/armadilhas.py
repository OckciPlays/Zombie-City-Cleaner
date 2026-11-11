# As armadilhas e as defesas (10/11/2026, a etapa 5 da árvore de habilidades: o ramo "Armadilhas e defesas", os nós t1
# a t7; o t5, Armadilheiro, e o t7, Fortaleza, são passivos e não têm modelo): os modelos postos no mundo, as versões
# danificadas da barricada, os vazios que a Unity usa e os itens de cada uma.
#
# Os objetos postos no mundo (Armadilhas.fbx), cada um Armadilha_<Nome>_corpo:
# - AlarmeLatas (t1): duas estacas finas (0,66 m) a 1,6 m uma da outra, o barbante esticado a ~0,55 m (com uma barriga) e
#   cinco latas penduradas de boca para baixo, juntas no meio para baterem uma na outra. O vazio Latas fica no meio do
#   barbante (de onde sai o som).
# - BarricadaPorta e BarricadaJanela (t2): as tábuas pregadas atravessadas no vão (a de porta: três deitadas e uma na
#   diagonal, por cima; a de janela: duas deitadas e a diagonal), com as pontas 13 cm para fora do vão, pregadas no
#   batente. A origem fica no meio da largura do vão, na face da parede de quem prega: no chão (a porta) ou no peitoril
#   (a janela); as tábuas ficam na frente da origem (+Z da Unity) e a parede atrás. As medidas do vão são as das casas e
#   dos prédios do jogo (Medidas.DoJogo, a Obra.cs: as de hoje x 1,15).
#   As _Danificada (para quando os zumbis batem): na porta, a de baixo quebrada no meio (a metade da direita no chão, na
#   frente) e a de cima despregada na ponta da direita, pendurada no prego da esquerda; na janela, a de cima pendurada e a
#   diagonal quebrada no meio. Sobram os pregos tortos onde as tábuas soltaram.
# - TapetePregos (t3): duas tábuas lado a lado (1,2 x 0,35 m) sobre três travessas, com 40 pregos de ponta para cima.
# - Estacas (t4): seis estacas afiadas (1,1 a 1,2 m) cruzadas duas a duas e inclinadas para a frente, amarradas numa tora
#   deitada (1,44 m), com uma vara atravessada por cima e duas pedras segurando os pés. A origem no meio da tora, no chão;
#   o vazio Ponta no meio da linha das pontas, na frente.
# - CercaFogo (t6): o galão de gasolina do jogo (o do loot.py, sem a tampa), com um pano enfiado no bico que desce pela
#   frente e corre no chão, e a mancha molhada da trilha até ~0,95 m. A origem no meio do fundo do galão; o vazio Pavio na
#   ponta do pano, no chão: dali a Unity acende a linha de fogo (~4 m) para a frente (+Z).
# Os itens (Loot_Armadilhas.fbx), no formato do Loot.fbx (Item_<Nome>_corpo, a origem embaixo, no meio): AlarmeLatas (o
# barbante enrolado e três latas amarradas nele), Tabuas (um feixe de quatro tábuas amarradas, com pregos), TapetePregos
# (um pedaço de 60 cm), Estacas (um feixe de cinco) e CercaFogo (o galão com o pano no bico).
#
# O formato é o do Loot.fbx (o loot.py e o sucata_itens.py): cada modelo é um objeto <Nome>_corpo girado 90° em X, com a
# malha nos eixos da Unity (o alto em +Y, a frente em +Z), feito aqui nos eixos do Blender (o alto em +Z, a frente em -Y);
# o material "Pistola", com a Paleta.png. Os vazios são filhos do objeto, na posição do modelo.
# Cores: as da Paleta.png (equipamentos.py: linha de cima para baixo, coluna), com as da sucata (o sucata_itens.py).
#
# Como rodar (um Blender em segundo plano, sem janela):
#   blender -b --factory-startup --disable-autoexec --python Blender/armadilhas.py -- <tudo|exportar|fotos> <pasta>
#   (exportar: grava <pasta>/Armadilhas.fbx, Loot_Armadilhas.fbx, Armadilhas_medidas.json e Armadilhas.blend; fotos: as
#   fotos em <pasta>/fotos, com o personagem do Roupas.fbx ao lado; tudo: os dois. Depois, a folha:
#   py Blender/armadilhas_folha.py <pasta>.) A pasta é obrigatória: os FBX do jogo ficam ao lado do Loot.fbx
#   (Assets/_Game/Art/Characters/Jogador), mas só com a Unity fechada.

import json
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import equipamentos as eq  # noqa: E402
import loot  # noqa: E402  (as peças C, Z, R e F, o _com_matriz, o _anel e o galão)
import sucata_itens as si  # noqa: E402  (as peças T e L e as cores da sucata)

C, Z, R, F = loot.C, loot.Z, loot.R, loot.F
T, L = si.T, si.L
ROUPAS = os.path.join(eq.REPO, "Assets", "_Game", "Art", "Characters", "Roupas")

eq.CORES.update({
    # as madeiras (a linha 3 de cima, a das armas, e as que já existiam)
    "a_madeira": (3, 8), "a_madeira_escura": (3, 7), "a_madeira_clara": (1, 4), "a_madeira_velha": (1, 1),
    "a_casca": (1, 3), "a_casca_escura": (7, 3), "a_cerne": (1, 2),
    # as latas, os pregos e o resto
    "a_lata": (8, 8), "a_lata_clara": (4, 6), "a_aco": (8, 2), "a_aco_escuro": (8, 1), "a_quase_preto": (3, 6),
    "a_rotulo_vermelho": (6, 4), "a_rotulo_oliva": (2, 8), "a_rotulo_azul": (4, 8), "a_creme": (3, 9),
    "a_pano": (3, 9), "a_pano_molhado": (6, 13), "a_mancha": (2, 5), "a_pedra": (10, 3), "a_pedra_clara": (8, 2),
    # as das fotos (o chão, a parede do vão, o batente e os marcadores dos vazios)
    "f_chao": (2, 13), "f_parede": (4, 4), "f_batente": (4, 5), "f_marcador": (11, 6),
})

# ------------------------------------------------------------------------------------------------ as medidas do jogo
# Medidas.DoJogo (Obra.cs): as casas de hoje x 1,15. A porta de 1,10 x 2,10 m vira 1,265 x 2,415; a janela de 1,20 m
# vira 1,38, do peitoril (0,90 m, não muda) ao topo (2,10 x 1,15 = 2,415): 1,515 de altura. A parede tem 0,20.
K = 1.15
PORTA_L, PORTA_A = 1.1 * K, 2.1 * K
JANELA_L, JANELA_P, JANELA_TOPO = 1.2 * K, 0.9, 2.1 * K
JANELA_A = JANELA_TOPO - JANELA_P
PAREDE = 0.20
PE_DIREITO = 2.8 * K

ESP, LARG = 0.028, 0.17       # a grossura e a largura das tábuas da barricada
SOBRA = 0.13                  # quanto cada tábua passa do vão, de cada lado (pregada no batente e na parede)


# ------------------------------------------------------------------------------------------------ as tábuas
def _dir(a, b):
    dx, dz = b[0] - a[0], b[1] - a[1]
    n = math.hypot(dx, dz)
    return dx / n, dz / n, n


def _ponto(a, b, d):
    """O ponto a d metros de a, indo para b (no plano da parede: x, z)."""
    ux, uz, _ = _dir(a, b)
    return (a[0] + ux * d, a[1] + uz * d)


def _horizontal(z, meia, ang):
    t = math.radians(ang)
    return (-meia * math.cos(t), z - meia * math.sin(t)), (meia * math.cos(t), z + meia * math.sin(t))


def _tabua(cor, a, b, camada=0):
    """Uma tábua pregada na parede, de a até b ((x, z) no plano dela): a camada 0 encosta na parede (y de 0 a -ESP), a 1
    fica por cima da 0."""
    y = -ESP * (camada + 0.5)
    return R(cor, (a[0], y, a[1]), (b[0], y, b[1]), LARG, ESP, (0, 0, 1))


def _prego(x, z, camada=0, cor="a_aco_escuro"):
    return Z(cor, 0.0065, 0.004, (x, -ESP * (camada + 1) - 0.002, z), 6, "Y")


def _pregos(a, b, ds, camada=0, abre=0.045, cor="a_aco_escuro"):
    """Dois pregos lado a lado (na largura da tábua) a cada distância ds de a, na tábua de a até b."""
    ux, uz, _ = _dir(a, b)
    nx, nz = -uz, ux
    p = []
    for d in ds:
        cx, cz = a[0] + ux * d, a[1] + uz * d
        for q in (-1, 1):
            p.append(_prego(cx + nx * abre * q, cz + nz * abre * q, camada, cor))
    return p


def _prego_torto(x, z, sentido=1):
    """Um prego que ficou na parede quando a tábua soltou: sai dela e entorta para baixo."""
    return [T("a_aco_escuro", (x, 0.004, z), (x, -0.022, z), 0.0022, 4),
            T("a_aco_escuro", (x, -0.022, z), (x + 0.006 * sentido, -0.03, z - 0.015), 0.0022, 4)]


def _lascas(cor, q, a, camada, semente, y=None):
    """A ponta quebrada de uma tábua em q (que vem de a): três lascas finas de comprimentos e direções diferentes."""
    rnd = random.Random(semente)
    ux, uz, _ = _dir(a, q)
    nx, nz = -uz, ux
    y = -ESP * (camada + 0.5) if y is None else y
    p = []
    for k, s in enumerate((-0.32, 0.03, 0.34)):
        comp = rnd.uniform(0.035, 0.11)
        desvio = rnd.uniform(-0.22, 0.22)
        larg = rnd.uniform(0.04, 0.06)
        ax, az = q[0] + nx * s * LARG - ux * 0.012, q[1] + nz * s * LARG - uz * 0.012
        dx, dz = ux + nx * desvio, uz + nz * desvio
        n = math.hypot(dx, dz)
        p.append(R(cor, (ax, y, az), (ax + dx / n * comp, y + rnd.uniform(-0.003, 0.003), az + dz / n * comp),
                   larg, ESP * rnd.uniform(0.5, 0.85), (nx, 0, nz)))
    return p


def _barricada(vao, danificada):
    """As tábuas no vão (vao: "porta" ou "janela"); z = 0 no chão (a porta) ou no peitoril (a janela)."""
    if vao == "porta":
        w = PORTA_L
        deitadas = [(0.42, 2.5, "a_madeira"), (1.25, -1.5, "a_madeira_velha"), (2.07, 1.5, "s_madeira_crua")]
        diag, cor_diag = ((-0.70, 0.22), (0.70, 2.26)), "a_madeira_clara"
        quebra, solta = 0, 2            # a de baixo quebra; a de cima fica pendurada
    else:
        w = JANELA_L
        deitadas = [(0.33, -2.0, "a_madeira"), (1.18, 1.5, "a_madeira_velha")]
        diag, cor_diag = ((-0.74, 0.12), (0.74, 1.40)), "s_madeira_crua"
        quebra, solta = None, 1         # a diagonal quebra; a de cima fica pendurada
    meia = w / 2 + SOBRA
    p = []
    for i, (z, ang, cor) in enumerate(deitadas):
        a, b = _horizontal(z, meia, ang)
        n = _dir(a, b)[2]
        if danificada and i == solta:
            # despregada na direita: um toco ficou nos pregos de lá, e o resto pende do prego da esquerda (o outro
            # prego da esquerda saiu junto), com a ponta solta saindo da parede
            corte = _ponto(a, b, n - 0.10)
            p.append(_tabua(cor, corte, b))
            p += _pregos(corte, b, (0.035,))
            piv = _ponto(a, b, 0.065)
            t = math.radians(-21)

            def gira(q):
                dx, dz = q[0] - piv[0], q[1] - piv[1]
                return (piv[0] + dx * math.cos(t) - dz * math.sin(t), piv[1] + dx * math.sin(t) + dz * math.cos(t))
            a2, b2 = gira(a), gira(corte)
            # (na camada 0, atrás da diagonal, que a segura)
            p.append(_tabua(cor, a2, b2))
            p.append(_prego(piv[0], piv[1] + 0.03))
            p += _lascas(cor, b2, a2, 0, 7 + i)
        elif danificada and i == quebra:
            # quebrada no meio: a metade da esquerda ficou, com as lascas; a da direita caiu na frente (os pregos tortos)
            q = _ponto(a, b, n * 0.55)
            p.append(_tabua(cor, a, q))
            p += _pregos(a, b, (0.065,))
            p += _lascas(cor, q, a, 0, 3 + i)
            c = _ponto(a, b, n - 0.065)
            p += _prego_torto(c[0], c[1] + 0.045) + _prego_torto(c[0] - 0.004, c[1] - 0.045, -1)
            # a metade caída, deitada no chão, com as lascas na ponta quebrada e um prego torto na outra
            ca, cb = Vector((0.06, -0.36, ESP / 2)), Vector((0.70, -0.52, ESP / 2))
            p.append(R(cor, tuple(ca), tuple(cb), LARG, ESP, (0, 1, 0)))
            u = (ca - cb).normalized()
            lado = Vector((-u.y, u.x, 0))
            for s, comp in ((-0.3, 0.07), (0.05, 0.035), (0.33, 0.09)):
                o = ca + lado * s * LARG
                p.append(R(cor, tuple(o - u * 0.01), tuple(o + u * comp), 0.045, ESP * 0.6, (u.y, -u.x, 0)))
            pn = cb + u * 0.065
            p.append(T("a_aco_escuro", tuple(pn + Vector((0, 0, ESP / 2))), tuple(pn + Vector((0.004, 0.0, ESP + 0.02))),
                       0.0022, 4))
        else:
            p.append(_tabua(cor, a, b))
            p += _pregos(a, b, (0.065, n - 0.065))
    # a diagonal, por cima das deitadas: pregada nas pontas e onde cruza com elas
    a, b = diag
    n = _dir(a, b)[2]
    cruza = [(z - a[1]) / (b[1] - a[1]) * n for z, _, _ in deitadas]
    if danificada and quebra is None:
        q = _ponto(a, b, n * 0.5)
        p.append(_tabua(cor_diag, a, q, 1))
        p += _pregos(a, b, [0.07] + [d for d in cruza if d < n * 0.5 - 0.1], 1)
        p += _lascas(cor_diag, q, a, 1, 11)
        c = _ponto(a, b, n - 0.07)
        p += _prego_torto(c[0] - 0.03, c[1] + 0.03) + _prego_torto(c[0] + 0.03, c[1] - 0.03, -1)
    else:
        p.append(_tabua(cor_diag, a, b, 1))
        p += _pregos(a, b, [0.07, n - 0.07] + cruza, 1)
    return {"pecas": p, "vazios": {}, "origem": (0.0, 0.0)}


# ------------------------------------------------------------------------------------------------ o alarme de latas
LATAS = [  # x, o fio, a cor do rótulo, o raio, a altura, a inclinação (graus, em X e em Y)
    (-0.30, 0.075, "a_rotulo_vermelho", 0.037, 0.11, 6, -4),
    (-0.17, 0.115, "a_rotulo_oliva", 0.040, 0.12, -5, 7),
    (-0.04, 0.060, "s_ferrugem", 0.034, 0.10, 3, 3),
    (0.09, 0.095, "a_rotulo_azul", 0.037, 0.11, -4, -6),
    (0.22, 0.080, "a_creme", 0.042, 0.075, 5, 2),
]
FIO_X, FIO_Z, BARRIGA = 0.765, 0.56, 0.035


def _fio_z(x):
    return FIO_Z - BARRIGA * (1 - (x / FIO_X) ** 2)


def _lata_aberta(cor, r, h, z0, lados=8):
    """Uma lata de conserva vazia, em pé, de z0 a z0 + h, com a boca (o fundo escuro) embaixo: o corpo, as bordas e o
    rótulo; a de rótulo "s_ferrugem" é a enferrujada, sem rótulo."""
    p = [L("a_lata", r, h, z0, lados),
         L("a_lata_clara", r + 0.0015, 0.005, z0 + h - 0.005, lados),
         L("a_lata_clara", r + 0.0015, 0.005, z0, lados),
         L("a_quase_preto", r - 0.004, 0.002, z0 - 0.0006, lados),
         L(cor, r + 0.0008, h * 0.62, z0 + h * 0.18, lados)]
    if cor != "s_ferrugem":
        p.append(C("s_ferrugem", (0.016, 0.0012, 0.02), (0.006, -(r + 0.0012) * math.cos(math.pi / lados), z0 + h * 0.12)))
    return p


def _alarme():
    p = []
    # as estacas (finas, quase em pé, a cabeça batida mais escura) e o barbante amarrado nelas, com a ponta que sobra
    for s in (-1, 1):
        a = Vector((s * 0.78, 0.0, 0.0))
        b = Vector((s * 0.772, 0.006, 0.66))
        u = (b - a).normalized()
        p.append(R("s_madeira_crua" if s < 0 else "a_madeira_velha", tuple(a), tuple(b), 0.035, 0.035, (1, 0, 0)))
        p.append(R("a_madeira_escura", tuple(b - u * 0.018), tuple(b + u * 0.002), 0.038, 0.038, (1, 0, 0)))
        no = a + u * FIO_Z
        p.append(R("s_corda", tuple(no - u * 0.01), tuple(no + u * 0.01), 0.043, 0.043, (1, 0, 0)))
        ponta = no + Vector((s * 0.024, -0.012, 0))
        p.append(T("s_corda", tuple(ponta), tuple(ponta + Vector((s * 0.012, -0.01, -0.1))), 0.0025, 3))
    # o barbante esticado, com a barriga no meio
    n = 12
    pts = [Vector((-FIO_X + 2 * FIO_X * k / n, 0.0, _fio_z(-FIO_X + 2 * FIO_X * k / n))) for k in range(n + 1)]
    for k in range(n):
        p.append(T("s_corda", tuple(pts[k]), tuple(pts[k + 1]), 0.0028, 4))
    # as latas, penduradas de boca para baixo por um fio curto amarrado no fundo
    for x, fio, cor, r, h, ax, ay in LATAS:
        pecas = [T("s_corda", (0, 0, 0.004), (0, 0, -fio - 0.003), 0.0018, 3),
                 C("s_corda", (0.012, 0.012, 0.008), (0, 0, -fio + 0.002))] + _lata_aberta(cor, r, h, -fio - h)
        M = Matrix.Translation((x, 0, _fio_z(x))) @ Matrix.Rotation(math.radians(ay), 4, "Y") @ \
            Matrix.Rotation(math.radians(ax), 4, "X")
        p.append((loot._com_matriz, M, pecas))
    return {"pecas": p, "vazios": {"Latas": (0.0, 0.0, _fio_z(0.0))}, "origem": (0.0, 0.0)}


# ------------------------------------------------------------------------------------------------ o tapete de pregos
def _pregos_para_cima(xs_linhas, z0, altura, semente):
    rnd = random.Random(semente)
    cores = ["a_aco", "a_aco_escuro", "s_ferrugem", "a_aco", "s_ferrugem_escura"]
    p = []
    for y, xs in xs_linhas:
        for x in xs:
            dx, dy = rnd.uniform(-0.007, 0.007), rnd.uniform(-0.007, 0.007)
            p.append(T(rnd.choice(cores), (x, y, z0 - 0.004), (x + dx, y + dy, z0 + altura * rnd.uniform(0.85, 1.0)),
                       0.0042, 4, r2=0.0005, fase=rnd.uniform(0, 90)))
    return p


def _tapete():
    p = []
    for x in (-0.55, 0.0, 0.55):
        p.append(C("a_madeira_escura", (0.06, 0.34, 0.02), (x, 0, 0.01)))
    p.append(C("s_madeira_crua", (1.2, 0.17, 0.03), (0, -0.0885, 0.035)))
    p.append(C("a_madeira", (1.2, 0.17, 0.03), (0.004, 0.0885, 0.035)))
    # a pintura velha (verde) que sobrou numa das tábuas e uma mancha de ferrugem
    p.append(C("s_pintura_verde", (0.34, 0.13, 0.0012), (0.33, 0.09, 0.0506)))
    p.append(C("s_pintura_verde", (0.12, 0.07, 0.0012), (0.09, 0.075, 0.0506), giro=(0, 0, 8)))
    p.append(C("s_ferrugem_escura", (0.10, 0.05, 0.0012), (-0.36, -0.1, 0.0506), giro=(0, 0, -12)))
    linhas = []
    for k, y in enumerate((-0.13, -0.05, 0.05, 0.13)):
        linhas.append((y, [-0.54 + 0.12 * i + (0.06 if k % 2 else 0.0) for i in range(10) if -0.54 + 0.12 * i + (0.06 if k % 2 else 0.0) < 0.58]))
    p += _pregos_para_cima(linhas, 0.05, 0.055, 31)
    return {"pecas": p, "vazios": {}, "origem": None}


# ------------------------------------------------------------------------------------------------ as estacas
def _estaca(cor, tras, base, d, comp, fase, raio=0.038, ponta=0.2):
    fim = base + d * comp
    meio = fim - d * ponta
    return [T(cor, tuple(tras), tuple(meio), raio, 6, fase=fase),
            T("s_madeira_crua", tuple(meio), tuple(fim), raio, 6, r2=0.003, fase=fase)]


def _estacas():
    p = []
    # a tora da base, com o cerne claro nas pontas cortadas
    p.append(T("a_casca_escura", (-0.72, 0, 0.068), (0.72, 0, 0.068), 0.07, 7))
    for s in (-1, 1):
        p.append(T("a_cerne", (s * 0.716, 0, 0.068), (s * 0.724, 0, 0.068), 0.062, 7))
    elev = math.radians(40)
    pontas = []
    comps = (1.12, 1.2, 1.08, 1.18, 1.1, 1.16)
    # três pares em X (vistos de frente): as duas de cada par abrem para lados contrários e se cruzam a CRUZA m da
    # tora; a segunda de cada par passa por cima da primeira (afastada na normal do par)
    abre = 0.28
    normal = Vector((0, math.sin(elev), math.cos(elev)))
    cruza = 0.1 / (abre / math.sqrt(1 + abre * abre))
    for i in range(6):
        x0 = -0.5 + i * 0.2
        d = Vector((abre if i % 2 == 0 else -abre, -math.cos(elev), math.sin(elev))).normalized()
        base = Vector((x0, 0.0, 0.13)) + (normal * 0.075 if i % 2 else Vector())
        tras = base - d * (0.3 if i % 2 else 0.2)
        p += _estaca("a_casca" if i % 2 == 0 else "a_madeira_escura", tras, base, d, comps[i], i * 13)
        pontas.append(base + d * comps[i])
        # a amarração na tora (as de baixo) e no cruzamento
        if i % 2 == 0:
            p.append(T("s_corda", tuple(base - d * 0.03), tuple(base + d * 0.03), 0.047, 6))
        q = base + d * cruza
        p.append(T("s_corda", tuple(q - d * 0.022), tuple(q + d * 0.022), 0.045, 6))
    # a vara atravessada por cima dos cruzamentos
    q = Vector((0, 0, 0.13)) + Vector((0, -math.cos(elev), math.sin(elev))) * cruza / math.sqrt(1 + abre * abre) + \
        normal * (0.075 + 0.038 + 0.026)
    p.append(T("a_casca", (-0.70, q.y, q.z), (0.70, q.y, q.z), 0.026, 6))
    # as pedras em cima dos pés das estacas (o peso que segura a frente) e dois tocos fincados na frente da tora
    p.append(C("a_pedra", (0.17, 0.13, 0.09), (-0.32, 0.17, 0.045), giro=(0, 4, 18), chanfro=0.02))
    p.append(C("a_pedra_clara", (0.14, 0.12, 0.08), (0.36, 0.16, 0.04), giro=(3, 0, -24), chanfro=0.02))
    for s in (-1, 1):
        p.append(T("a_casca", (s * 0.62, -0.1, 0.0), (s * 0.62, -0.098, 0.15), 0.022, 5))
        p.append(T("s_corda", (s * 0.62, -0.1, 0.1), (s * 0.62, -0.1, 0.125), 0.027, 5))
    meio = sum(pontas, Vector()) / len(pontas)
    return {"pecas": p, "vazios": {"Ponta": (0.0, min(v.y for v in pontas), meio.z)}, "origem": (0.0, 0.0)}


# ------------------------------------------------------------------------------------------------ a cerca de fogo
def _galao_sem_tampa():
    """O galão vermelho do loot.py (o bico na quina de +X), sem a tampa amarela do bico."""
    return [q for q in loot._galao() if not (q[0] is eq._cilindro and q[1] == "amarelo")]


def _mancha(cx, cy, rx, ry, z, semente, n=8):
    rnd = random.Random(semente)
    pts = []
    for k in range(n):
        t = 2 * math.pi * k / n + rnd.uniform(-0.12, 0.12)
        f = rnd.uniform(0.9, 1.0)
        pts.append((cx + rx * f * math.cos(t), cy + ry * f * math.sin(t)))
    return F("a_mancha", pts, z, z + 0.0015)


def _pano(caminho, larg=0.032, grossura=0.01, molhado_de=99):
    """Uma tira de pano pelos pontos do caminho; a partir do ponto molhado_de, a cor molhada (de gasolina)."""
    p = []
    for k in range(len(caminho) - 1):
        cor = "a_pano" if k < molhado_de else "a_pano_molhado"
        p.append(R(cor, caminho[k], caminho[k + 1], larg * (1.0 if k % 2 else 0.86), grossura, (1, 0, 0)))
    return p


def _cerca():
    # o galão girado: o bico para a frente (-Y)
    p = [(loot._com_matriz, Matrix.Rotation(math.radians(-90), 4, "Z"), _galao_sem_tampa())]
    # o pano: o bolo enfiado no bico, a tira que sai por cima, desce pela frente do galão e corre no chão
    p.append(C("a_pano", (0.042, 0.04, 0.034), (0.0, -0.112, 0.316), giro=(12, 8, 25)))
    caminho = [(0.0, -0.118, 0.325), (0.004, -0.148, 0.345), (0.007, -0.172, 0.315), (0.009, -0.172, 0.25),
               (0.011, -0.171, 0.17), (0.013, -0.172, 0.09), (0.015, -0.185, 0.02), (0.018, -0.25, 0.006),
               (0.02, -0.33, 0.006), (0.016, -0.40, 0.007), (0.02, -0.455, 0.007)]
    p += _pano(caminho, molhado_de=4)
    # a gasolina derramada: a poça na frente do galão e a trilha molhada que segue do pano para a frente
    p.append(_mancha(0.0, -0.21, 0.11, 0.08, 0.0004, 1))
    # (a faixa que afina até ~0,95 m, com as manchas tortas nas beiradas)
    p.append(F("a_mancha", [(-0.065, -0.27), (0.065, -0.27), (0.032, -0.9), (0.012, -0.955), (-0.012, -0.955),
                            (-0.032, -0.9)], 0.0005, 0.002))
    rnd = random.Random(5)
    for k in range(9):
        y = -0.32 - 0.07 * k
        lado = 1 if k % 2 else -1
        larg = 0.065 - 0.035 * k / 8
        p.append(_mancha(lado * larg * rnd.uniform(0.5, 0.9), y, rnd.uniform(0.025, 0.045), rnd.uniform(0.04, 0.07),
                         0.0007 + 0.0002 * (k % 2), 10 + k))
    return {"pecas": p, "vazios": {"Pavio": (0.02, -0.465, 0.008)}, "origem": (0.0, 0.0)}


# ------------------------------------------------------------------------------------------------ os itens
def _deitada(pecas, x, y, z, giro_z, giro_x=90):
    """As peças de uma lata (em pé, o meio na origem) deitadas de lado em (x, y, z), viradas giro_z graus."""
    M = Matrix.Translation((x, y, z)) @ Matrix.Rotation(math.radians(giro_z), 4, "Z") @ \
        Matrix.Rotation(math.radians(giro_x), 4, "X")
    return (loot._com_matriz, M, pecas)


def _item_alarme():
    # o barbante enrolado (três voltas) e três latas amarradas nele, deitadas, com o fio de uma a outra
    p = []
    for k, (r, z) in enumerate(((0.062, 0.006), (0.054, 0.017), (0.06, 0.028))):
        p += loot._anel("s_corda", r, 0.011, 0.011, z, 12)
    latas = [(0.115, -0.045, 0.037, 0.11, "a_rotulo_vermelho", 70), (0.135, 0.06, 0.04, 0.12, "a_rotulo_oliva", 100),
             (0.0, -0.115, 0.034, 0.10, "s_ferrugem", 15)]
    for x, y, r, h, cor, g in latas:
        p.append(_deitada(_lata_aberta(cor, r, h, -h / 2), x, y, r, g))
    p += [T("s_corda", (0.055, -0.03, 0.02), (0.07, -0.04, 0.035), 0.0025, 3),
          T("s_corda", (0.06, 0.03, 0.02), (0.09, 0.05, 0.04), 0.0025, 3),
          T("s_corda", (0.0, -0.06, 0.02), (0.0, -0.09, 0.03), 0.0025, 3),
          T("s_corda", (-0.06, 0.02, 0.03), (-0.10, 0.05, 0.004), 0.0025, 3)]
    return p


def _item_tabuas():
    p = [C("a_madeira", (0.6, 0.12, 0.022), (0, -0.062, 0.011)),
         C("s_madeira_crua", (0.6, 0.12, 0.022), (0.01, 0.062, 0.011)),
         C("a_madeira_velha", (0.6, 0.12, 0.022), (-0.012, -0.035, 0.033), giro=(0, 0, 4)),
         C("a_madeira_clara", (0.58, 0.12, 0.022), (0.015, 0.06, 0.033), giro=(0, 0, -3))]
    for x in (-0.19, 0.17):
        p.append(C("s_corda", (0.024, 0.262, 0.056), (x, 0, 0.026)))
    # pregos meio batidos nas pontas das de cima e uns soltos por cima
    for x, y in ((-0.27, -0.05), (-0.26, -0.015), (0.27, 0.075), (0.28, 0.04)):
        p.append(T("a_aco", (x, y, 0.042), (x, y, 0.06), 0.002, 4))
        p.append(Z("a_aco", 0.0055, 0.0025, (x, y, 0.061), 6))
    for x, y, a in ((0.02, -0.02, 20), (0.05, 0.01, -35), (-0.03, 0.03, 75)):
        t = math.radians(a)
        p.append(R("a_aco", (x - 0.025 * math.cos(t), y - 0.025 * math.sin(t), 0.0465),
                   (x + 0.025 * math.cos(t), y + 0.025 * math.sin(t), 0.0465), 0.0035, 0.0035))
        p.append(Z("a_aco", 0.0055, 0.0025, (x - 0.026 * math.cos(t), y - 0.026 * math.sin(t), 0.047), 6))
    return p


def _item_tapete():
    p = [C("a_madeira_escura", (0.05, 0.17, 0.02), (x, 0, 0.01)) for x in (-0.24, 0.24)]
    p.append(C("s_madeira_crua", (0.6, 0.17, 0.03), (0, 0, 0.035)))
    p.append(C("s_pintura_verde", (0.2, 0.1, 0.0012), (0.16, 0.02, 0.0506)))
    linhas = [(-0.045, [-0.25 + 0.1 * i for i in range(6)]), (0.045, [-0.2 + 0.1 * i for i in range(5)])]
    p += _pregos_para_cima(linhas, 0.05, 0.05, 41)
    return p


def _item_estacas():
    p = []
    rnd = random.Random(9)
    pos = [(-0.066, 0.032), (0.0, 0.032), (0.066, 0.032), (-0.033, 0.087), (0.033, 0.087)]
    for i, (y, z) in enumerate(pos):
        x0 = -0.45 + rnd.uniform(-0.03, 0.03)
        d = Vector((1, 0, 0))
        p += _estaca("a_casca" if i % 2 == 0 else "a_madeira_escura", Vector((x0, y, z)), Vector((x0, y, z)), d,
                     0.9 + rnd.uniform(-0.04, 0.04), i * 17, 0.03, 0.17)
    for x in (-0.22, 0.14):
        p.append(C("s_corda", (0.03, 0.21, 0.135), (x, 0, 0.062)))
    return p


def _item_cerca():
    p = _galao_sem_tampa()
    p.append(C("a_pano", (0.042, 0.04, 0.034), (0.112, 0.0, 0.318), giro=(10, 15, 20)))
    p += _pano([(0.125, 0.0, 0.33), (0.152, -0.008, 0.31), (0.158, -0.012, 0.25), (0.158, -0.016, 0.18),
                (0.157, -0.02, 0.11), (0.16, -0.018, 0.06)], molhado_de=3)
    # a fita prata que segura a tira no galão
    p.append(C("s_fita_prata", (0.008, 0.06, 0.04), (0.155, -0.015, 0.2)))
    return p


ARMADILHAS = {
    "Armadilha_AlarmeLatas": _alarme,
    "Armadilha_BarricadaPorta": lambda: _barricada("porta", False),
    "Armadilha_BarricadaPorta_Danificada": lambda: _barricada("porta", True),
    "Armadilha_BarricadaJanela": lambda: _barricada("janela", False),
    "Armadilha_BarricadaJanela_Danificada": lambda: _barricada("janela", True),
    "Armadilha_TapetePregos": _tapete,
    "Armadilha_Estacas": _estacas,
    "Armadilha_CercaFogo": _cerca,
}
ITENS = {
    "Item_AlarmeLatas": _item_alarme, "Item_Tabuas": _item_tabuas, "Item_TapetePregos": _item_tapete,
    "Item_Estacas": _item_estacas, "Item_CercaFogo": _item_cerca,
}
ORIGENS = {
    "Armadilha_AlarmeLatas": "no chão, no meio entre as duas estacas (o barbante ao longo do X)",
    "Armadilha_BarricadaPorta": "no meio da largura do vão, no chão, na face da parede de quem prega; as tábuas para +Z",
    "Armadilha_BarricadaJanela": "no meio da largura do vão, no peitoril, na face da parede de quem prega; as tábuas para +Z",
    "Armadilha_TapetePregos": "no chão, no meio (o comprimento ao longo do X)",
    "Armadilha_Estacas": "no chão, no meio da tora da base; as pontas para +Z",
    "Armadilha_CercaFogo": "no chão, no meio do fundo do galão; o pano e a trilha para +Z",
}
ORIGENS["Armadilha_BarricadaPorta_Danificada"] = ORIGENS["Armadilha_BarricadaPorta"]
ORIGENS["Armadilha_BarricadaJanela_Danificada"] = ORIGENS["Armadilha_BarricadaJanela"]


# ------------------------------------------------------------------------------------------------ a cena e os FBX
def _bm(pecas):
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    for p in pecas:
        p[0](bm, uv, *p[1:])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def objeto(nome, pecas, colecao, origem=None):
    """Como o sucata_itens.objeto (nome_corpo, a malha nos eixos da Unity, o objeto girado 90° em X). A origem: no chão
    (o z mais baixo) e, em x e y, no meio da caixa (origem None) ou no ponto (x, y) dado. Devolve o objeto e o quanto o
    modelo andou (para os vazios)."""
    me = bpy.data.meshes.new(nome + "_corpo")
    bm = _bm(pecas)
    vs = [v.co for v in bm.verts]
    if origem is None:
        origem = ((min(v.x for v in vs) + max(v.x for v in vs)) / 2, (min(v.y for v in vs) + max(v.y for v in vs)) / 2)
    desloc = Vector((origem[0], origem[1], min(v.z for v in vs)))
    if nome.startswith("Armadilha_Barricada"):
        desloc.z = 0.0     # a barricada: z = 0 no chão (a porta) ou no peitoril (a janela)
    bmesh.ops.translate(bm, vec=-desloc, verts=bm.verts)
    bm.to_mesh(me)
    bm.free()
    for f in me.polygons:
        f.use_smooth = False
    me.materials.append(eq._material())
    me.transform(Matrix.Rotation(math.radians(-90), 4, "X"))
    o = bpy.data.objects.new(nome + "_corpo", me)
    o.rotation_euler = (math.radians(90), 0, 0)
    colecao.objects.link(o)
    return o, desloc


def vazio(nome, dono, ponto, colecao):
    """Um vazio filho do objeto, no ponto (x, y, z) do modelo (nos eixos do Blender, já com a origem descontada)."""
    e = bpy.data.objects.new(nome, None)
    e.empty_display_type = "PLAIN_AXES"
    e.empty_display_size = 0.08
    colecao.objects.link(e)
    e.parent = dono
    e.matrix_parent_inverse = Matrix.Identity(4)
    x, y, z = ponto
    e.location = (x, z, -y)
    return e


def _unity(v):
    """Do modelo (Blender: x, y, z; a frente em -Y) para a Unity (o X espelhado, o alto em +Y, a frente em +Z)."""
    return [round(-v[0] + 0.0, 4), round(v[2] + 0.0, 4), round(-v[1] + 0.0, 4)]


def _limpar():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)


def construir():
    _limpar()
    cena = bpy.context.scene
    cols = {}
    for nome in ("Armadilhas", "Itens"):
        c = bpy.data.collections.get(nome) or bpy.data.collections.new(nome)
        if c.name not in cena.collection.children:
            cena.collection.children.link(c)
        cols[nome] = c
    info, arm, itens = {}, [], []
    for i, (nome, f) in enumerate(ARMADILHAS.items()):
        m = f()
        o, desloc = objeto(nome, m["pecas"], cols["Armadilhas"], m["origem"])
        o.location = (i * 2.2, 0, 0)
        vz = {}
        for vn, pt in m["vazios"].items():
            local = Vector(pt) - desloc
            vazio(vn, o, local, cols["Armadilhas"])
            vz[vn] = local
        arm.append(o)
        info[nome] = {"objeto": o, "vazios": vz}
    for i, (nome, f) in enumerate(ITENS.items()):
        o, _ = objeto(nome, f(), cols["Itens"])
        o.location = (i * 1.0, 3.0, 0)
        itens.append(o)
        info[nome] = {"objeto": o, "vazios": {}}
    bpy.context.view_layer.update()
    for nome, d in info.items():
        o = d["objeto"]
        vs = [Vector((v.co.x, -v.co.z, v.co.y)) for v in o.data.vertices]   # de volta aos eixos do modelo
        mn = Vector([min(v[k] for v in vs) for k in range(3)])
        mx = Vector([max(v[k] for v in vs) for k in range(3)])
        d["min"], d["max"] = mn, mx
        d["tri"] = sum(len(p.vertices) - 2 for p in o.data.polygons)
        print(f"  {o.name:44s} {mx.x - mn.x:.3f} x {mx.y - mn.y:.3f} x {mx.z - mn.z:.3f} m (larg x fundo x alto) | "
              f"{d['tri']} triângulos | vazios: {', '.join(f'{k} {tuple(round(c, 3) for c in v)}' for k, v in d['vazios'].items()) or '-'}")
    return arm, itens, info


def _medidas(info):
    j = {
        "unidade": "m",
        "eixos": "Unity: o alto em +Y, a frente em +Z, o X espelhado do Blender (min/max e vazios no espaço da malha)",
        "vaos_do_jogo": {
            "fonte": "Medidas.DoJogo (Assets/_Game/Scripts/Mundo/Construcoes/Obra.cs): as casas de hoje x 1,15, nas casas e nos prédios",
            "porta": {"largura": round(PORTA_L, 4), "altura": round(PORTA_A, 4)},
            "janela": {"largura": round(JANELA_L, 4), "altura": round(JANELA_A, 4), "peitoril": JANELA_P,
                       "topo": round(JANELA_TOPO, 4)},
            "janela_pequena_do_banheiro": {"largura": round(0.6 * K, 4), "peitoril": round(1.4 + 0.2 * (K - 1), 4),
                                           "nota": "sem barricada própria; a de janela passa dela"},
            "parede": PAREDE, "pe_direito": round(PE_DIREITO, 4),
            "nota": "a mobília (MedidasDaMobilia.Escala) fica em x 1,30, mas as portas e as janelas são das construções (x 1,15)",
        },
        "barricada": {
            "tabuas": {"largura": LARG, "grossura": ESP, "passa_do_vao_de_cada_lado": SOBRA},
            "porta": "3 deitadas + 1 diagonal por cima (4 tábuas, 1,525 m as deitadas)",
            "janela": "2 deitadas + 1 diagonal por cima (3 tábuas, 1,64 m as deitadas)",
            "origem": "no meio da largura do vão, na face da parede do lado de quem prega: no chão (a porta) ou no peitoril "
                      "(a janela); as tábuas ocupam de 0 a 0,056 m para +Z; do meio da parede até a face são 0,10 m. "
                      "O TratarJanela da Obra põe as tábuas dela a 0,05 m da face (o peitoril sobressai 5 cm): as nossas "
                      "começam acima do peitoril, então encostam.",
        },
        "modelos": {},
    }
    for nome, d in info.items():
        mn, mx = d["min"], d["max"]
        um, ux = _unity(mn), _unity(mx)
        j["modelos"][nome + "_corpo"] = {
            "caixa_larg_alto_fundo": [round(mx.x - mn.x, 4), round(mx.z - mn.z, 4), round(mx.y - mn.y, 4)],
            "min": [min(um[k], ux[k]) for k in range(3)], "max": [max(um[k], ux[k]) for k in range(3)],
            "origem": ORIGENS.get(nome, "embaixo, no meio (como os do Loot.fbx)"),
            "triangulos": d["tri"],
            "vazios": {k: _unity(v) for k, v in d["vazios"].items()},
        }
    j["modelos"]["Armadilha_CercaFogo_corpo"]["linha_de_fogo"] = {
        "comeca_no": "Pavio", "direcao": "+Z", "comprimento": 4.0,
        "nota": "a trilha molhada do modelo vai até ~0,95 m da origem; o fogo é da Unity"}
    return j


def exportar(pasta, arm, itens, info):
    os.makedirs(pasta, exist_ok=True)
    for arq, objs in (("Armadilhas.fbx", arm), ("Loot_Armadilhas.fbx", itens)):
        for o in bpy.context.scene.objects:
            o.select_set(False)
        for o in objs:
            o.select_set(True)
            for c in o.children:
                c.select_set(True)
        bpy.ops.export_scene.fbx(filepath=os.path.join(pasta, arq), use_selection=True, object_types={"MESH", "EMPTY"},
                                 apply_scale_options="FBX_SCALE_ALL", bake_anim=False, mesh_smooth_type="FACE")
        print("armadilhas: gravado", os.path.join(pasta, arq))
    with open(os.path.join(pasta, "Armadilhas_medidas.json"), "w", encoding="utf-8") as f:
        json.dump(_medidas(info), f, ensure_ascii=False, indent=2)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(pasta, "Armadilhas.blend"), copy=True)
    print("armadilhas: gravado o json e o blend em", pasta)


# ------------------------------------------------------------------------------------------------ as fotos
def _malha(nome, pecas, local=(0, 0, 0)):
    """Uma peça das fotos (o chão, a parede, os marcadores), nos eixos do Blender, sem ir para o FBX."""
    me = bpy.data.meshes.new(nome)
    bm = _bm(pecas)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(eq._material())
    o = bpy.data.objects.new(nome, me)
    o.location = local
    bpy.context.scene.collection.objects.link(o)
    return o


def _pessoa():
    """O NPC_Corpo do Roupas.fbx (só lido), com camiseta, calça e bota, os braços abaixados (ele vem em T)."""
    bpy.ops.import_scene.fbx(filepath=os.path.join(ROUPAS, "Roupas.fbx"))
    manter = {"NPC_Corpo", "Roupa_Camiseta", "Roupa_Calca_Trabalho", "Roupa_Bota_Trabalho", "Roupa_Cabelo_Curto"}
    rig = None
    for o in list(bpy.context.selected_objects):
        if o.type == "ARMATURE":
            rig = o
        elif o.name not in manter:
            bpy.data.objects.remove(o, do_unlink=True)
    for o in bpy.data.objects:
        if o.name in manter:
            for m in o.data.materials:
                for n in (m.node_tree.nodes if m and m.node_tree else []):
                    if n.type == "TEX_IMAGE" and n.image and not n.image.has_data:
                        novo = os.path.join(ROUPAS, "T_" + os.path.basename(n.image.filepath.replace("\\", "/")))
                        if os.path.exists(novo):
                            n.image.filepath = novo
                            n.image.reload()
    if rig is not None:
        for osso, ang in (("upper_arm.L", 74), ("upper_arm.R", -74)):
            pb = rig.pose.bones.get(osso)
            if pb is None:
                continue
            bpy.context.view_layer.update()
            mw = rig.matrix_world @ pb.matrix
            c = mw.to_translation()
            pb.matrix = rig.matrix_world.inverted() @ Matrix.Translation(c) @ Matrix.Rotation(math.radians(ang), 4, "Y") @ \
                Matrix.Translation(-c) @ mw
        bpy.context.view_layer.update()
    corpo = [o for o in bpy.data.objects if o.name in manter]
    return rig, corpo


def _render(cena, caminho, pontos, direcao, res=(900, 640), transparente=False, margem=1.1, lente=50):
    r = cena.render
    r.resolution_x, r.resolution_y = res
    r.resolution_percentage = 100
    r.film_transparent = transparente
    cam = cena.camera
    cam.data.lens = lente
    cam.data.sensor_fit = "AUTO"
    d = Vector(direcao).normalized()
    mn = Vector([min(p[k] for p in pontos) for k in range(3)])
    mx = Vector([max(p[k] for p in pontos) for k in range(3)])
    alvo = (mn + mx) / 2
    rot = (-d).to_track_quat("-Z", "Y").to_matrix()
    rt = rot.transposed()
    th = 18.0 / lente
    tv = th * res[1] / res[0]
    dist = 0.0
    for p in pontos:
        u = rt @ (Vector(p) - alvo)
        dist = max(dist, abs(u.x) * margem / th + u.z, abs(u.y) * margem / tv + u.z)
    cam.location = alvo + d * dist
    cam.rotation_euler = rot.to_euler()
    r.filepath = caminho
    bpy.ops.render.render(write_still=True)


def _cantos(o):
    return [o.matrix_world @ Vector(c) for c in o.bound_box]


def _caixa_pessoa(x, y):
    return [Vector((x + a, y + b, c)) for a in (-0.3, 0.3) for b in (-0.15, 0.15) for c in (0.0, 1.8)]


def fotos(pasta, info):
    cena = bpy.context.scene
    saida = os.path.join(pasta, "fotos")
    os.makedirs(saida, exist_ok=True)
    try:
        cena.render.engine = "BLENDER_WORKBENCH"
    except TypeError:
        pass
    cena.render.image_settings.file_format = "PNG"
    cena.render.image_settings.color_mode = "RGBA"
    s = cena.display.shading
    s.light = "STUDIO"
    s.color_type = "TEXTURE"
    s.show_cavity = True
    s.cavity_type = "BOTH"
    s.show_object_outline = True
    s.object_outline_color = (0.05, 0.05, 0.05)
    s.show_shadows = True
    s.shadow_intensity = 0.35
    cena.display.light_direction = (0.45, -0.55, 0.7)
    cena.view_settings.view_transform = "Standard"
    if cena.world is None:
        cena.world = bpy.data.worlds.new("Fundo")
    cena.world.color = (0.55, 0.6, 0.66)
    if cena.camera is None:
        cam = bpy.data.objects.new("CameraDasFotos", bpy.data.cameras.new("CameraDasFotos"))
        cena.collection.objects.link(cam)
        cena.camera = cam
    cena.camera.data.clip_end = 200

    rig, corpo = _pessoa()
    chao = _malha("Foto_Chao", [C("f_chao", (14, 14, 0.02), (0, 0, -0.01))])
    # as paredes com o vão da porta e da janela (a face de quem prega em y = 0, a parede para +Y)
    larg_parede = 4.4
    porta = _malha("Foto_ParedePorta", [
        C("f_parede", ((larg_parede - PORTA_L) / 2, PAREDE, PE_DIREITO), (-(larg_parede + PORTA_L) / 4, PAREDE / 2, PE_DIREITO / 2)),
        C("f_parede", ((larg_parede - PORTA_L) / 2, PAREDE, PE_DIREITO), ((larg_parede + PORTA_L) / 4, PAREDE / 2, PE_DIREITO / 2)),
        C("f_parede", (PORTA_L, PAREDE, PE_DIREITO - PORTA_A), (0, PAREDE / 2, (PE_DIREITO + PORTA_A) / 2)),
    ])
    janela = _malha("Foto_ParedeJanela", [
        C("f_parede", ((larg_parede - JANELA_L) / 2, PAREDE, PE_DIREITO), (-(larg_parede + JANELA_L) / 4, PAREDE / 2, PE_DIREITO / 2)),
        C("f_parede", ((larg_parede - JANELA_L) / 2, PAREDE, PE_DIREITO), ((larg_parede + JANELA_L) / 4, PAREDE / 2, PE_DIREITO / 2)),
        C("f_parede", (JANELA_L, PAREDE, PE_DIREITO - JANELA_TOPO), (0, PAREDE / 2, (PE_DIREITO + JANELA_TOPO) / 2)),
        C("f_parede", (JANELA_L, PAREDE, JANELA_P), (0, PAREDE / 2, JANELA_P / 2)),
        C("f_batente", (JANELA_L, PAREDE + 0.1, 0.06), (0, PAREDE / 2, JANELA_P - 0.03)),
    ])
    fixos = [chao, porta, janela] + corpo + ([rig] if rig else [])

    def mostrar(objs):
        for o in cena.objects:
            if o.type in {"MESH"}:
                o.hide_render = o not in objs

    def por_pessoa(x, y):
        if rig is not None:
            rig.location = (x, y, 0)
        bpy.context.view_layer.update()

    def marcadores(nome):
        ms = []
        for e in info[nome]["objeto"].children:
            p = e.matrix_world.translation
            ms.append(_malha("Foto_Marcador_" + e.name, [C("f_marcador", (0.045, 0.045, 0.045), (0, 0, 0), giro=(35, 45, 0))], p))
        return ms

    def tirar(nome, local, extra_vistas, pessoa_xy, parede=None):
        o = info[nome]["objeto"]
        o.location = local
        por_pessoa(*pessoa_xy)
        ms = marcadores(nome)
        base = [o, chao] + ms
        alvo = _cantos(o)
        com_pessoa = alvo + _caixa_pessoa(*pessoa_xy)
        curto = nome.replace("Armadilha_", "")
        for vista, (direcao, com_parede, com_gente, margem) in extra_vistas.items():
            objs = base + (corpo if com_gente else []) + ([parede] if (com_parede and parede) else [])
            mostrar(objs)
            _render(cena, os.path.join(saida, f"{curto}_{vista}.png"), com_pessoa if com_gente else alvo, direcao,
                    margem=margem)
        for m in ms:
            bpy.data.objects.remove(m, do_unlink=True)
        o.location = (0, 0, -50)

    for o in info:
        info[o]["objeto"].location = (0, 0, -50)
    vistas = {"34": ((0.85, -1.0, 0.62), False, True, 1.06), "lado": ((-1.0, -0.12, 0.3), False, True, 1.06),
              "perto": ((0.75, -1.0, 0.85), False, False, 1.12)}
    tirar("Armadilha_AlarmeLatas", (0, 0, 0), vistas, (1.2, 0.35))
    tirar("Armadilha_TapetePregos", (0, 0, 0), vistas, (0.95, 0.3))
    # (as estacas apontam para a frente e para cima: as vistas mais baixas, para os X aparecerem)
    tirar("Armadilha_Estacas", (0, 0, 0), {"34": ((0.6, -1.0, 0.3), False, True, 1.06), "lado": vistas["lado"],
                                           "perto": ((0.0, -1.0, 0.16), False, False, 1.1)}, (1.25, 0.3))
    tirar("Armadilha_CercaFogo", (0, 0, 0), vistas, (0.75, 0.3))
    vb = {"frente": ((0.18, -1.0, 0.16), True, True, 1.06), "34": ((0.95, -1.0, 0.4), True, True, 1.06),
          "lado": ((-1.0, -0.4, 0.22), False, True, 1.06)}
    for nome, z, parede in (("Armadilha_BarricadaPorta", 0.0, porta), ("Armadilha_BarricadaPorta_Danificada", 0.0, porta),
                            ("Armadilha_BarricadaJanela", JANELA_P, janela),
                            ("Armadilha_BarricadaJanela_Danificada", JANELA_P, janela)):
        tirar(nome, (0, 0, z), vb, (1.45, -0.45), parede)
    # os itens: de 3/4, sem o chão, o fundo transparente
    for nome in ITENS:
        o = info[nome]["objeto"]
        o.location = (0, 0, 0)
        bpy.context.view_layer.update()
        mostrar([o])
        _render(cena, os.path.join(saida, f"Foto_{nome}.png"), _cantos(o), (0.8, -1.0, 0.8), res=(600, 500),
                transparente=True, margem=1.15)
        o.location = (0, 0, -50)
    print("armadilhas: as fotos em", saida)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if len(args) < 2:
        print("armadilhas: diga a etapa e a pasta de saída: -- <tudo|exportar|fotos> <pasta>")
        sys.exit(1)
    etapa, pasta = args[0], args[1]
    arm, itens, info = construir()
    if etapa in ("tudo", "exportar"):
        exportar(pasta, arm, itens, info)
    if etapa in ("tudo", "fotos"):
        fotos(pasta, info)
