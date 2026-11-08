# Nadar e remar (07/11/2026, a pedido: o nado e o caiaque): as animações feitas por script num Blender sem janela, a
# partir dos FBX que já estão no projeto (que só são lidos, nunca gravados de novo), em três FBX novos:
#
#   Assets/_Game/Art/Characters/Jogador/Jogador_FP_Nadar.fbx
#       os braços de primeira pessoa nadando: FP_Rig_Nadar, FP_Bracos_Nadar e Boca_FP_Nadar (os do
#       Jogador_FP_Maos.fbx, com as mãos abertas), com os clipes Idle (boiando, 2,0 s), Walk (a braçada de peito,
#       1,2 s), Run (a braçada rápida, 0,8 s) e Sacar (0,3 s, os braços sobem de baixo da tela).
#   Assets/_Game/Art/Characters/Jogador/Jogador_FP_Remo.fbx
#       os braços de primeira pessoa remando: FP_Rig_Remo (com o osso "remo", filho do root), FP_Bracos_Remo (os braços
#       e o remo do Remo.fbx numa malha só; o remo pesa 100% no osso "remo") e Boca_FP_Remo, com Idle (2,0 s),
#       Walk (a remada, 1,6 s) e Sacar (0,3 s).
#   Assets/_Game/Art/Characters/Personagens/Personagem_PrincipalBloco_Agua.fbx
#       só o esqueleto do corpo (PrincipalBloco_Rig, o mesmo do Personagem_PrincipalBloco.fbx: os mesmos nomes, a
#       mesma hierarquia e o mesmo repouso), com Boiar (2,0 s), Nadar (1,2 s), Remar (1,6 s) e Remar_Parado (2,0 s).
#       Cada clipe grava todos os ossos em todos os quadros (posição, giro e escala).
#
# Os clipes são a 30 quadros por segundo; os laços terminam na mesma pose do começo (o último quadro é o primeiro).
#
# O mundo do Blender (o dos FBX importados): o personagem olha para -Y, +X é a esquerda dele e Z é para cima; os pés
# na origem. Na primeira pessoa, a origem é o olho, olhando para -Y (55 graus na vertical, a câmera dos braços).
#
# Como as poses são feitas: chaves (as mãos, para onde os dedos apontam, para onde a palma olha, para onde o cotovelo
# vai, os pés, os joelhos, o quadril e a coluna) misturadas por curvas suaves (Hermite, sem passar do ponto nas
# viradas) e, a cada quadro, os braços e as pernas por IK de dois ossos (o cotovelo e o joelho no plano do polo, o
# antebraço com metade do giro da mão). As chaves vão uma por quadro, em todos os ossos.
#
# O remo é o do jogo (Assets/_Game/Art/Veiculos/Remo.fbx, do caiaque.py): o cabo ao longo do X, as pás em pé, a face
# de puxar para trás. As duas mãos o seguram por cima (os nós dos dedos para cima e para a frente), a 60 cm uma da
# outra, e o eixo do cabo passa a 7,2 cm à frente do pulso e 3 cm abaixo da palma (PEGADA). No corpo, o remo fica
# parado no espaço da mão direita (hand.R) em todos os quadros do Remar e do Remar_Parado: a Unity pode prendê-lo
# nela (o lugar e o giro saem no "conferir", em remo_na_mao).
#
# Como rodar (Blender 5.2, em segundo plano; um Blender por vez):
#   blender --background --factory-startup --python Blender/agua_animacoes.py -- tudo [pasta das fotos]
#   (as etapas, que também podem ir sozinhas: nadar, remo, corpo, conferir, fotos)
# As folhas das fotos (fora do Blender, com o Pillow):
#   py Blender/agua_animacoes.py folhas <pasta das fotos>

import math
import os
import sys

try:
    import bpy
    from mathutils import Matrix, Quaternion, Vector
except ImportError:          # fora do Blender: só as folhas das fotos (as constantes do Blender ficam vazias)
    bpy = None

    def Vector(*a, **k):
        return None
    Matrix = Quaternion = Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(AQUI)
ARTE = os.path.join(REPO, "Assets", "_Game", "Art")
JOGADOR = os.path.join(ARTE, "Characters", "Jogador")
PERSONAGENS = os.path.join(ARTE, "Characters", "Personagens")
FBX_MAOS = os.path.join(JOGADOR, "Jogador_FP_Maos.fbx")
FBX_CORPO = os.path.join(PERSONAGENS, "Personagem_PrincipalBloco.fbx")
FBX_REMO = os.path.join(ARTE, "Veiculos", "Remo.fbx")
FBX_CAIAQUE = os.path.join(ARTE, "Veiculos", "Caiaque.fbx")
TEXTURA = os.path.join(PERSONAGENS, "Personagem_PrincipalBloco.png")
PALETA = os.path.join(ARTE, "Paleta.png")

SAIDA_NADAR = os.path.join(JOGADOR, "Jogador_FP_Nadar.fbx")
SAIDA_REMO = os.path.join(JOGADOR, "Jogador_FP_Remo.fbx")
SAIDA_CORPO = os.path.join(PERSONAGENS, "Personagem_PrincipalBloco_Agua.fbx")

FPS = 30
RIG_CORPO = "PrincipalBloco_Rig"

# os clipes: (nome, quadros, laço)
CLIPES_NADAR = [("Idle", 60, True), ("Walk", 36, True), ("Run", 24, True), ("Sacar", 9, False)]
CLIPES_REMO = [("Idle", 60, True), ("Walk", 48, True), ("Sacar", 9, False)]
CLIPES_CORPO = [("Boiar", 60, True), ("Nadar", 36, True), ("Remar", 48, True), ("Remar_Parado", 60, True)]

# o remo
MEIO_DAS_MAOS = 0.30        # cada mão a 30 cm do meio do cabo (as pegas pretas do remo)
PEGADA = (0.0, 0.072, -0.030)     # o eixo do cabo no espaço da mão (7,2 cm à frente do pulso, 3 cm abaixo da palma)
GIRO_DA_PEGADA = 15.0       # os dedos da mão no remo: para a frente e 15 graus para baixo
DOBRA_NO_REMO = (88.0, 100.0)   # as falanges em volta do cabo (graus)
# a água
AGUA_NADANDO = 1.42         # a superfície, acima da origem (os pés), nadando e boiando
AGUA_CAIAQUE = -0.06        # no caiaque, a origem é o assento (Assento_0), 6 cm acima da linha d'água

DEDOS = ("index", "middle", "ring", "pinky")


# ================================================================================================ contas

def v3(*a):
    if len(a) == 1:
        a = a[0]
    return Vector((float(a[0]), float(a[1]), float(a[2])))


def espelho(v):
    """O lado de lá (x trocado)."""
    return Vector((-v[0], v[1], v[2]))


def suave(t):
    t = min(1.0, max(0.0, t))
    return t * t * (3 - 2 * t)


def sai_devagar(t):
    """Começa rápido e chega devagar (o fim do Sacar)."""
    t = min(1.0, max(0.0, t))
    return 1 - (1 - t) ** 3


def quadro_yz(y, z, pos):
    """Matriz com o Y na direção y, o Z o mais perto de z e o X = Y x Z (mão destra), na posição pos."""
    y = Vector(y).normalized()
    z = Vector(z)
    z = (z - z.dot(y) * y)
    if z.length < 1e-8:
        z = y.orthogonal()
    z.normalize()
    x = y.cross(z)
    m = Matrix((x, y, z)).transposed().to_4x4()
    m.translation = pos
    return m


def quadro_yx(y, x, pos):
    """Matriz com o Y na direção y, o X o mais perto de x e o Z = X x Y."""
    y = Vector(y).normalized()
    x = Vector(x)
    x = (x - x.dot(y) * y)
    if x.length < 1e-8:
        x = y.orthogonal()
    x.normalize()
    z = x.cross(y)
    m = Matrix((x, y, z)).transposed().to_4x4()
    m.translation = pos
    return m


def girar(eixo, graus):
    return Matrix.Rotation(math.radians(graus), 4, Vector(eixo).normalized())


def rot_local(incl=0.0, giro=0.0, lado=0.0):
    """Um giro no espaço de um osso da coluna (X para a esquerda, Y ao longo do osso, Z para a frente), em graus:
    incl > 0 dobra para a frente, giro > 0 vira para a esquerda, lado > 0 tomba para a esquerda."""
    return (Matrix.Rotation(math.radians(giro), 4, "Y") @ Matrix.Rotation(math.radians(incl), 4, "X")
            @ Matrix.Rotation(math.radians(-lado), 4, "Z"))


class _Curva:
    """Um canal: chaves (t, valor), Hermite cúbica. A tangente é a do Catmull-Rom (com os tempos), zerada nas
    viradas e limitada para não passar do ponto (como a monotônica), componente por componente."""

    def __init__(self, pontos, laco):
        pontos = sorted(pontos, key=lambda p: p[0])
        self.vetor = hasattr(pontos[0][1], "__len__")
        vals = [tuple(float(x) for x in (v if self.vetor else (v,))) for _, v in pontos]
        ts = [float(t) for t, _ in pontos]
        if laco and len(ts) > 1:
            ts = [ts[-2] - 1.0, ts[-1] - 1.0] + ts + [ts[0] + 1.0, ts[1] + 1.0]
            vals = [vals[-2], vals[-1]] + vals + [vals[0], vals[1]]
        self.ts, self.vals, self.laco = ts, vals, laco
        n = len(ts)
        dim = len(vals[0])
        self.tang = []
        for i in range(n):
            m = []
            for c in range(dim):
                if i == 0 or i == n - 1:
                    m.append(0.0)
                    continue
                d1 = (vals[i][c] - vals[i - 1][c]) / max(1e-9, ts[i] - ts[i - 1])
                d2 = (vals[i + 1][c] - vals[i][c]) / max(1e-9, ts[i + 1] - ts[i])
                if d1 * d2 <= 0:
                    m.append(0.0)
                    continue
                mc = (vals[i + 1][c] - vals[i - 1][c]) / max(1e-9, ts[i + 1] - ts[i - 1])
                lim = 3.0 * min(abs(d1), abs(d2))
                m.append(max(-lim, min(lim, mc)))
            self.tang.append(m)

    def __call__(self, t):
        ts, vals = self.ts, self.vals
        if len(ts) == 1:
            r = vals[0]
        else:
            if self.laco:
                t = t % 1.0
            if t <= ts[0]:
                r = vals[0]
            elif t >= ts[-1]:
                r = vals[-1]
            else:
                i = 0
                while ts[i + 1] < t:
                    i += 1
                h = ts[i + 1] - ts[i]
                s = (t - ts[i]) / h
                h00 = 2 * s ** 3 - 3 * s ** 2 + 1
                h10 = s ** 3 - 2 * s ** 2 + s
                h01 = -2 * s ** 3 + 3 * s ** 2
                h11 = s ** 3 - s ** 2
                r = tuple(h00 * vals[i][c] + h10 * h * self.tang[i][c] + h01 * vals[i + 1][c] + h11 * h * self.tang[i + 1][c]
                          for c in range(len(vals[0])))
        return Vector(r) if self.vetor else r[0]


# os giros que vão como giro (e não como duas direções soltas): (direção Y, direção Z ou o contrário dela, canal novo)
GIROS = (("dedos", "palma", "giro_mao"), ("pe_y", "pe_z", "giro_pe"))


def _giro_de(nome, a, b):
    if nome == "giro_mao":
        return rot_da_mao(v3(a), v3(b)).to_quaternion()
    return quadro_yz(v3(a), v3(b), Vector()).to_quaternion()


class Linha:
    """Chaves [(t de 0 a 1, {canal: valor})]; cada canal é misturado só pelas chaves que o têm.
    O giro da mão (dedos.X e palma.X) e o do pé (pe_y.X e pe_z.X) viram um giro só em cada chave e são misturados
    como vetores de rotação em volta de um giro médio (misturar as duas direções separadas dava giros de 60 graus
    num quadro onde uma componente passava pelo zero); saem em giro_mao.X e giro_pe.X (matrizes 3x3)."""

    def __init__(self, chaves, laco=True):
        self.refs = {}
        chaves = [(t, dict(d)) for t, d in chaves]
        for a, b, novo in GIROS:
            lados = {k.split(".", 1)[1] for _, d in chaves for k in d if k.startswith(a + ".")}
            for lado in lados:
                ka, kb, kn = a + "." + lado, b + "." + lado, novo + "." + lado
                qs = [_giro_de(novo, d[ka], d[kb]) for _, d in chaves if ka in d and kb in d]
                if not qs:
                    continue
                soma, ant = [0.0] * 4, qs[0]
                for q in qs:
                    if q.dot(ant) < 0:
                        q = Quaternion((-q.w, -q.x, -q.y, -q.z))
                    soma = [s + c for s, c in zip(soma, q)]
                    ant = q
                ref = Quaternion(soma).normalized()
                self.refs[kn] = ref
                for _, d in chaves:
                    if ka in d and kb in d:
                        r = ref.inverted() @ _giro_de(novo, d.pop(ka), d.pop(kb))
                        if r.w < 0:
                            r = Quaternion((-r.w, -r.x, -r.y, -r.z))
                        eixo, ang = r.to_axis_angle()
                        d[kn] = tuple(eixo * ang)
        canais = {}
        for t, d in chaves:
            for k, v in d.items():
                canais.setdefault(k, []).append((t, v))
        self.curvas = {k: _Curva(lst, laco) for k, lst in canais.items()}

    def __call__(self, t):
        p = {k: c(t) for k, c in self.curvas.items()}
        for k, ref in self.refs.items():
            rv = p[k]
            q = ref @ Quaternion(rv.normalized(), rv.length) if rv.length > 1e-9 else ref.copy()
            p[k] = q.to_matrix()
        return p


def com_espelho(chaves):
    """As chaves do lado esquerdo (".L") ganham as do direito, espelhadas (os vetores com o x trocado)."""
    saida = []
    for t, d in chaves:
        n = dict(d)
        for k, v in d.items():
            if k.endswith(".L"):
                k2 = k[:-2] + ".R"
                if k2 not in n:
                    # os pontos e as direções (3 números) trocam o x; o resto (os ombros, os números) fica igual
                    n[k2] = tuple(espelho(v)) if (hasattr(v, "__len__") and len(v) == 3) else v
        saida.append((t, n))
    return saida


# ================================================================================================ o esqueleto

class Esqueleto:
    """O repouso de cada osso (no espaço da armadura), os pais, a ordem da hierarquia e as contas de FK."""

    def __init__(self, rig):
        self.rig = rig
        ossos = rig.data.bones
        self.rest = {b.name: b.matrix_local.copy() for b in ossos}
        self.pai = {b.name: (b.parent.name if b.parent else None) for b in ossos}
        self.filhos = {b.name: [c.name for c in b.children] for b in ossos}
        self.ordem = []

        def desce(n):
            self.ordem.append(n)
            for c in self.filhos[n]:
                desce(c)
        for b in ossos:
            if b.parent is None:
                desce(b.name)
        self.rel = {n: (self.rest[p].inverted() @ self.rest[n]) if p else self.rest[n].copy() for n, p in self.pai.items()}
        # a torção de repouso da coxa e da canela: no repouso, o X delas não fica bem no eixo do joelho (a perna é um
        # pouco dobrada, com o joelho um pouco virado); a perna por IK usa o eixo do joelho girado por essa diferença
        self.corr = {}
        for lado in "LR":
            th, sh, ft = "thigh." + lado, "shin." + lado, "foot." + lado
            if th not in self.rest or ft not in self.rest:
                continue
            H, K, A = self.rest[th].translation, self.rest[sh].translation, self.rest[ft].translation
            u = (A - H).normalized()
            e = (K - H) - (K - H).dot(u) * u
            x = u.cross(e.normalized())
            for osso, (a, b) in ((th, (H, K)), (sh, (K, A))):
                F = quadro_yx(b - a, x, a)
                self.corr[osso] = (F.inverted() @ self.rest[osso]).to_3x3().to_4x4()

    def comprimento(self, osso, filho):
        """A distância de repouso da cabeça do osso à cabeça do filho (o tamanho do segmento no IK)."""
        return (self.rest[filho].translation - self.rest[osso].translation).length

    def fk(self, M, nome, base=None):
        p = self.pai[nome]
        m = (M[p] @ self.rel[nome]) if p else self.rel[nome].copy()
        return m @ base if base is not None else m

    def completar(self, M, bases):
        for n in self.ordem:
            if n not in M:
                M[n] = self.fk(M, n, bases.get(n))

    def descendentes(self, nome):
        r = []
        for c in self.filhos[nome]:
            r.append(c)
            r += self.descendentes(c)
        return r

    def refazer_abaixo(self, M, nome, bases):
        for d in self.descendentes(nome):
            M.pop(d, None)
        self.completar(M, bases)

    def base(self, M, nome):
        """A matriz local (a do pose bone) que leva o osso à matriz M[nome] do espaço da armadura."""
        p = self.pai[nome]
        if p:
            return self.rel[nome].inverted() @ M[p].inverted() @ M[nome]
        return self.rest[nome].inverted() @ M[nome]


def dois_ossos(S, W, a, b, polo):
    """O IK de dois ossos: de S (o ombro ou o quadril) até W (o pulso ou o tornozelo), com o cotovelo para o lado do
    polo. Devolve o cotovelo, o pulso alcançado, a direção S->W, o polo no plano e quanto do comprimento foi usado."""
    d_vec = W - S
    d = max(1e-6, d_vec.length)
    u = d_vec / d
    e = Vector(polo) - Vector(polo).dot(u) * u
    if e.length < 1e-6:
        e = u.orthogonal()
    e.normalize()
    dd = min(max(d, abs(a - b) + 1e-4), (a + b) * 0.9995)
    x = (a * a - b * b + dd * dd) / (2 * dd)
    h = math.sqrt(max(0.0, a * a - x * x))
    E = S + u * x + e * h
    return E, S + u * dd, u, e, d / (a + b)


def rot_da_mao(dedos, palma):
    """O giro da mão: o Y para os dedos e o Z para as costas da mão (a palma olha para -Z)."""
    return quadro_yz(dedos, -Vector(palma), Vector()).to_3x3()


def braco(esq, M, lado, S, punho, rot_mao, polo, gira=0.5):
    """O braço por IK: o ombro em S, o pulso no punho, a mão com o giro rot_mao (3x3) e o cotovelo para o polo.
    O antebraço leva metade do giro da mão em volta dele (como nos braços do jogo). Devolve o alcance (1 = esticado)."""
    ua, fa, mao = "upper_arm." + lado, "forearm." + lado, "hand." + lado
    a, b = esq.comprimento(ua, fa), esq.comprimento(fa, mao)
    E, W, u, e, alcance = dois_ossos(S, punho, a, b, polo)
    z = u.cross(e) if lado == "L" else -u.cross(e)
    Mu = quadro_yz(E - S, z, S)
    Mf = quadro_yz(W - E, z, E)
    rel = esq.rest[fa].to_3x3().inverted() @ esq.rest[mao].to_3x3()
    D = (Mf.to_3x3() @ rel).inverted() @ rot_mao
    q = D.to_quaternion()
    tor = 2 * math.atan2(q.y, q.w)
    if tor > math.pi:
        tor -= 2 * math.pi
    if tor < -math.pi:
        tor += 2 * math.pi
    Mf = Mf @ Matrix.Rotation(tor * gira, 4, "Y")
    Mh = rot_mao.to_4x4()
    Mh.translation = W
    M[ua], M[fa], M[mao] = Mu, Mf, Mh
    return alcance


def perna(esq, M, lado, tornozelo, pe_y, pe_z, polo):
    """A perna por IK: o tornozelo no ponto, o pé com o Y para os dedos (pe_y) e o Z para o peito do pé (pe_z) e o
    joelho para o polo. Devolve o alcance."""
    th, sh, ft = "thigh." + lado, "shin." + lado, "foot." + lado
    H = M[th].translation.copy()
    a, b = esq.comprimento(th, sh), esq.comprimento(sh, ft)
    K, W, u, e, alcance = dois_ossos(H, tornozelo, a, b, polo)
    x = u.cross(e)
    M[th] = quadro_yx(K - H, x, H) @ esq.corr.get(th, Matrix.Identity(4))
    M[sh] = quadro_yx(W - K, x, K) @ esq.corr.get(sh, Matrix.Identity(4))
    M[ft] = quadro_yz(pe_y, pe_z, W)
    return alcance


# ================================================================================================ os dedos

class Dedos:
    """As poses dos dedos (os giros locais de cada falange, como quatérnios): o punho fechado vem do Idle do
    Jogador_FP_Maos.fbx; a mão aberta e a pegada do remo são feitas aqui (as falanges dobram em volta do X local, para
    a palma, com o ângulo negativo)."""

    def __init__(self, punho):
        self.punho = punho     # {osso: Quaternion}

    @staticmethod
    def _dobra(graus):
        return Quaternion((1, 0, 0), math.radians(-graus))

    def aberta(self, lado, concha=0.0):
        """A mão aberta, os dedos juntos e quase retos (concha: 0 a 1, um pouco mais dobrados, para puxar a água)."""
        q = {}
        for d in DEDOS:
            q[f"{d}1.{lado}"] = self._dobra(5 + 12 * concha)
            q[f"{d}2.{lado}"] = self._dobra(6 + 14 * concha)
        # o polegar encostado no indicador (uma parte do caminho até o punho)
        q["thumb." + lado] = Quaternion().slerp(self.punho["thumb." + lado], 0.45 + 0.1 * concha)
        q["thumb2." + lado] = Quaternion().slerp(self.punho["thumb2." + lado], 0.25)
        return q

    def remo(self, lado):
        """Fechada em volta do cabo do remo (3 cm), o polegar por baixo, como no punho."""
        q = {}
        for d in DEDOS:
            q[f"{d}1.{lado}"] = self._dobra(DOBRA_NO_REMO[0])
            q[f"{d}2.{lado}"] = self._dobra(DOBRA_NO_REMO[1])
        q["thumb." + lado] = self.punho["thumb." + lado].copy()
        q["thumb2." + lado] = self.punho["thumb2." + lado].copy()
        return q

    @staticmethod
    def misturar(a, b, t):
        return {k: a[k].slerp(b[k], t) for k in a}


def bases_dos_dedos(qs):
    return {k: q.to_matrix().to_4x4() for k, q in qs.items()}


# ================================================================================================ a gravação

def _limpar_animacao(rig):
    ad = rig.animation_data or rig.animation_data_create()
    ad.action = None
    for t in list(ad.nla_tracks):
        ad.nla_tracks.remove(t)
    return ad


def gravar(esq, nome, quadros, pose, laco):
    """Uma ação com uma chave por quadro (do quadro 1 ao quadros + 1) em todos os ossos: posição, giro e escala.
    pose(t) devolve as matrizes de todos os ossos no espaço da armadura (t de 0 a 1). Devolve a ação."""
    rig = esq.rig
    ad = rig.animation_data or rig.animation_data_create()
    if nome in bpy.data.actions:
        bpy.data.actions.remove(bpy.data.actions[nome])
    acao = bpy.data.actions.new(nome)
    acao.use_fake_user = True
    ad.action = acao
    anteriores = {}
    for f in range(quadros + 1):
        # no laço, o último quadro é o primeiro (fecha sem salto)
        M = pose(0.0 if (laco and f == quadros) else f / quadros)
        for n in esq.ordem:
            pb = rig.pose.bones[n]
            loc, rot, esc = esq.base(M, n).decompose()
            if n in anteriores and anteriores[n].dot(rot) < 0:
                rot = -rot
            anteriores[n] = rot
            pb.location, pb.rotation_quaternion, pb.scale = loc, rot, esc
            pb.keyframe_insert("location", frame=f + 1, group=n)
            pb.keyframe_insert("rotation_quaternion", frame=f + 1, group=n)
            pb.keyframe_insert("scale", frame=f + 1, group=n)
    ad.action = None
    return acao


def acao_de_repouso(esq, nome="Repouso"):
    """Uma ação de um quadro com todos os ossos no repouso. Fica ativa no export: as faixas são assadas uma por uma e,
    no fim, o FBX grava os nós na pose do quadro atual (com ela, a de repouso)."""
    rig = esq.rig
    ad = rig.animation_data or rig.animation_data_create()
    if nome in bpy.data.actions:
        bpy.data.actions.remove(bpy.data.actions[nome])
    acao = bpy.data.actions.new(nome)
    ad.action = acao
    for n in esq.ordem:
        pb = rig.pose.bones[n]
        pb.location, pb.rotation_quaternion, pb.scale = (0, 0, 0), (1, 0, 0, 0), (1, 1, 1)
        for p in ("location", "rotation_quaternion", "scale"):
            pb.keyframe_insert(p, frame=0, group=n)
    ad.action = None
    return acao


def faixas(rig, acoes):
    """Uma faixa de NLA por clipe, com o nome do clipe (o take do FBX, o clipe da Unity), sem extrapolação."""
    ad = _limpar_animacao(rig)
    for nome, acao in acoes:
        tr = ad.nla_tracks.new()
        tr.name = nome
        s = tr.strips.new(nome, 1, acao)
        s.name = nome
        if hasattr(s, "action_slot") and len(acao.slots):
            s.action_slot = acao.slots[0]
        s.extrapolation = "NOTHING"
    return ad


def usar_acao(rig, acao):
    """Liga só a ação (para as fotos e as medidas), sem as faixas."""
    ad = rig.animation_data or rig.animation_data_create()
    ad.use_nla = False
    ad.action = acao
    if acao is not None and hasattr(ad, "action_slot") and len(acao.slots):
        ad.action_slot = acao.slots[0]


def exportar(caminho, objetos, ativo, tipos, rig, repouso):
    cena = bpy.context.scene
    cena.render.fps, cena.render.fps_base = FPS, 1.0
    ad = rig.animation_data
    ad.use_nla = True
    ad.action = repouso
    if hasattr(ad, "action_slot") and len(repouso.slots):
        ad.action_slot = repouso.slots[0]
    cena.frame_set(0)
    for o in cena.objects:
        o.select_set(o in objetos)
    bpy.context.view_layer.objects.active = ativo
    bpy.ops.export_scene.fbx(filepath=caminho, use_selection=True, object_types=tipos,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=False, add_leaf_bones=False,
                             primary_bone_axis="Y", secondary_bone_axis="X", use_armature_deform_only=False,
                             bake_anim=True, bake_anim_use_nla_strips=True, bake_anim_use_all_actions=False,
                             bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
                             mesh_smooth_type="FACE", path_mode="AUTO", embed_textures=False)
    print("agua: gravado", caminho)


def importar(caminho):
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=caminho, automatic_bone_orientation=False,
                             primary_bone_axis="Y", secondary_bone_axis="X", ignore_leaf_bones=False)
    return [o for o in bpy.data.objects if o not in antes]


def nova_cena():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    cena = bpy.context.scene
    cena.render.fps, cena.render.fps_base = FPS, 1.0
    return cena


def punho_do_fp():
    """Os giros dos dedos no punho fechado (o quadro 1 do Idle do Jogador_FP_Maos.fbx)."""
    rig = bpy.data.objects["FP_Rig_Maos"]
    usar_acao(rig, bpy.data.actions["FP_Rig_Maos|Idle"])
    bpy.context.scene.frame_set(1)
    q = {}
    for lado in "LR":
        for d in DEDOS:
            for i in (1, 2):
                n = f"{d}{i}.{lado}"
                q[n] = rig.pose.bones[n].rotation_quaternion.copy()
        for n in ("thumb." + lado, "thumb2." + lado):
            q[n] = rig.pose.bones[n].rotation_quaternion.copy()
    usar_acao(rig, None)
    return q


def conjunto_fp(nome):
    """Importa o Jogador_FP_Maos.fbx e troca os nomes para o conjunto novo (FP_Rig_<nome>, FP_Bracos_<nome>,
    Boca_FP_<nome>). Devolve (rig, malha, boca, esqueleto, dedos)."""
    importar(FBX_MAOS)
    punho = punho_do_fp()
    rig = bpy.data.objects["FP_Rig_Maos"]
    malha = bpy.data.objects["FP_Bracos_Maos"]
    boca = bpy.data.objects.get("Boca_FP_Maos")
    rig.name = rig.data.name = "FP_Rig_" + nome
    malha.name = malha.data.name = "FP_Bracos_" + nome
    if boca:
        boca.name = "Boca_FP_" + nome
    # as ações importadas (as dos punhos) saem: o arquivo novo só leva as dele
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    _limpar_animacao(rig)
    for o in (malha, boca):
        if o and o.animation_data:
            o.animation_data_clear()
    return rig, malha, boca, Esqueleto(rig), Dedos(punho)


# ================================================================================================ os braços de primeira pessoa

def pose_fp(esq, p, dedos):
    """As matrizes dos braços de primeira pessoa a partir de um dicionário de chaves:
    ombro.X, mao.X (o pulso), dedos.X, palma.X, polo.X, fecha.X (0 aberta .. 1 punho; ou dedos_q.X) e, no remo, remo
    (a matriz do osso)."""
    M = {}
    esq.completar(M, {})
    bases = {}
    for lado in "LR":
        S = v3(p["ombro." + lado])
        M["upper_arm." + lado] = Matrix.Translation(S)   # só a posição, o IK refaz
        rot = p.get("giro_mao." + lado)
        if rot is None:
            rot = rot_da_mao(p["dedos." + lado], p["palma." + lado])
        braco(esq, M, lado, S, v3(p["mao." + lado]), rot, v3(p["polo." + lado]))
        qs = p.get("dedos_q." + lado)
        if qs is None:
            qs = Dedos.misturar(dedos.aberta(lado, p.get("concha." + lado, 0.0)),
                                dedos.remo(lado), p.get("fecha." + lado, 0.0))
        bases.update(bases_dos_dedos(qs))
        esq.refazer_abaixo(M, "hand." + lado, bases)
    if "remo" in p:
        M["remo"] = p["remo"]
    return M


# ---------------------------------------------------------------------------------------------- nadar (primeira pessoa)

OMBRO_NADO = (0.18, 0.02, -0.33)


def chaves_da_bracada(largura=1.0, fundo=0.0):
    """A braçada de peito vista pelos olhos (o lado esquerdo; o direito é o espelho). t = 0: as mãos juntas embaixo do
    queixo; empurram para a frente até quase esticar no meio da tela (as palmas para baixo); abrem num arco (as palmas
    para fora), puxam para trás (as palmas para trás, o cotovelo alto), fecham por baixo do peito e voltam juntas."""
    L = largura
    k = [
        (0.00, {"mao.L": (0.078, -0.30, -0.155), "dedos.L": (-0.03, -1, 0.14), "palma.L": (-0.35, 0, -0.94),
                "polo.L": (1, 0.2, -0.8), "fecha.L": 0.0, "concha.L": 0.15, "ombro.L": (0.18, 0.025, -0.33)}),
        (0.14, {"mao.L": (0.06, -0.42, -0.135), "dedos.L": (-0.04, -1, 0.05), "palma.L": (-0.2, 0, -1),
                "polo.L": (1, 0.2, -0.8), "concha.L": 0.0}),
        (0.28, {"mao.L": (0.065, -0.52, -0.12), "dedos.L": (0.0, -1, 0.0), "palma.L": (-0.1, 0, -1),
                "polo.L": (1, 0.0, -0.6), "concha.L": 0.0, "ombro.L": (0.17, -0.045, -0.29)}),
        (0.40, {"mao.L": (0.11 * L, -0.52, -0.12), "dedos.L": (0.15, -1, 0.0), "palma.L": (0.4, 0, -0.92),
                "polo.L": (1, 0.0, -0.6), "concha.L": 0.1, "ombro.L": (0.17, -0.04, -0.29)}),
        (0.52, {"mao.L": (0.28 * L, -0.46, -0.14 - 0.3 * fundo), "dedos.L": (0.4, -1, -0.05), "palma.L": (0.6, 0.15, -0.78),
                "polo.L": (1, 0.3, -0.3), "concha.L": 0.15}),
        (0.64, {"mao.L": (0.33 * L, -0.36, -0.20 - fundo), "dedos.L": (0.3, -0.85, -0.45), "palma.L": (0.5, 0.8, -0.3),
                "polo.L": (1, 0.3, 0.4), "concha.L": 0.25, "ombro.L": (0.18, 0.0, -0.31)}),
        (0.76, {"mao.L": (0.17 * L, -0.32, -0.19 - fundo), "dedos.L": (-0.2, -0.8, -0.55), "palma.L": (-0.3, 0.9, 0.1),
                "polo.L": (1, 0.4, -0.2), "concha.L": 0.2}),
        (0.88, {"mao.L": (0.08, -0.28, -0.165), "dedos.L": (-0.2, -0.9, 0.2), "palma.L": (-0.85, 0.2, -0.45),
                "polo.L": (1, 0.2, -0.7), "concha.L": 0.2, "ombro.L": (0.18, 0.03, -0.33)}),
    ]
    return com_espelho(k)


def boiando_fp(t):
    """Boiando: as mãos abertas embaixo da tela, remexendo a água de um lado para o outro (dois vaivéns por laço,
    a palma inclinada para o lado em que a mão vai) e subindo e descendo de leve com a respiração."""
    a = 2 * math.pi * 2 * t
    p = {}
    for lado, s in (("L", 1), ("R", -1)):
        x = s * (0.17 + 0.05 * math.sin(a))
        incl = 26 * math.cos(a)              # para fora quando a mão vai para fora
        palma = girar((0, -1, 0), s * incl) @ Vector((0, 0, -1))
        p["mao." + lado] = (x, -0.31 + 0.012 * math.cos(a), -0.155 + 0.008 * math.sin(2 * math.pi * t))
        p["dedos." + lado] = (s * (0.15 + 0.06 * math.sin(a)), -1, 0.02)
        p["palma." + lado] = tuple(palma)
        p["polo." + lado] = (s * 1, 0.3, -0.7)
        p["ombro." + lado] = (s * 0.18, 0.03, -0.34)
        p["concha." + lado] = 0.1
        p["fecha." + lado] = 0.0
    return p


def animar_nadar(esq, dedos):
    walk = Linha(chaves_da_bracada())
    run = Linha(chaves_da_bracada(largura=1.18, fundo=0.03))
    idle0 = boiando_fp(0.0)

    def sacar(t):
        # do fundo da tela até o Idle
        s = sai_devagar(t)
        p = dict(idle0)
        for lado in "LR":
            m = v3(idle0["mao." + lado])
            p["mao." + lado] = tuple(m + Vector((0, 0.07, -0.24)) * (1 - s))
            d = v3(idle0["dedos." + lado])
            p["dedos." + lado] = tuple(d.lerp(Vector((d.x, -0.6, -0.8)), 1 - s))
        return p

    fontes = {"Idle": boiando_fp, "Walk": walk, "Run": run, "Sacar": sacar}
    acoes = []
    for nome, quadros, laco in CLIPES_NADAR:
        f = fontes[nome]
        acoes.append((nome, gravar(esq, nome, quadros, lambda t, f=f: pose_fp(esq, f(t), dedos), laco)))
    return acoes


# ---------------------------------------------------------------------------------------------- o remo

def quadro_do_remo(pega_l, pega_r, costas=(0.0, 1.0, 0.0)):
    """O remo a partir dos dois pontos das mãos no cabo: o meio, a direção do cabo (para a pá esquerda) e a face de
    puxar olhando para trás. Devolve (matriz 4x4 do remo: X o cabo, Y a face de puxar, Z para cima; o meio; a
    direção)."""
    l, r = v3(pega_l), v3(pega_r)
    meio = (l + r) / 2
    d = (l - r).normalized()
    n = Vector(costas) - Vector(costas).dot(d) * d
    n.normalize()
    z = d.cross(n)
    m = Matrix((d, n, z)).transposed().to_4x4()
    m.translation = meio
    return m


def maos_no_remo(Mremo):
    """As duas mãos no cabo, por cima: os dedos para a frente e um pouco para baixo, as costas da mão para cima, o
    eixo do cabo no ponto da PEGADA. Devolve {lado: (pulso, giro 3x3)}."""
    b = math.radians(GIRO_DA_PEGADA)
    R = Mremo.to_3x3()
    dedos = R @ Vector((0, -math.cos(b), -math.sin(b)))
    costas = R @ Vector((0, -math.sin(b), math.cos(b)))
    rot = quadro_yz(dedos, costas, Vector()).to_3x3()
    saida = {}
    for lado, s in (("L", 1), ("R", -1)):
        pega = Mremo @ Vector((s * MEIO_DAS_MAOS, 0, 0))
        pulso = pega - rot @ Vector(PEGADA)
        saida[lado] = (pulso, rot)
    return saida


def remada(fase_l, ao_contrario=False):
    """As chaves da remada no corpo (o assento na origem; o lado esquerdo e, na outra metade, o espelho): os pontos
    das mãos no cabo (pega.L, pega.R), o giro do tronco (giro, graus: > 0 vira para a esquerda) e os ombros."""
    # a pá esquerda entra na água lá na frente (t = 0), puxa até o quadril (0,24), sai (0,32), o remo sobe e vira
    # (0,42) e a direita entra (0,5)
    k = [
        (0.00, {"pega.L": (0.32, -0.50, 0.24), "pega.R": (-0.10, -0.36, 0.52), "giro": -22.0}),
        (0.12, {"pega.L": (0.36, -0.32, 0.20), "pega.R": (-0.04, -0.44, 0.52), "giro": -10.0}),
        (0.24, {"pega.L": (0.37, -0.09, 0.20), "pega.R": (0.04, -0.48, 0.48), "giro": 8.0}),
        (0.30, {"pega.L": (0.37, -0.03, 0.23), "pega.R": (0.05, -0.49, 0.45), "giro": 14.0}),
        (0.36, {"pega.L": (0.33, -0.10, 0.33), "pega.R": (-0.04, -0.50, 0.38), "giro": 14.0}),
        (0.43, {"pega.L": (0.24, -0.28, 0.45), "pega.R": (-0.22, -0.46, 0.33), "giro": 8.0}),
    ]
    saida = []
    for t, d in k:
        saida.append((t, d))
    for t, d in k:
        saida.append((t + 0.5, {"pega.L": tuple(espelho(v3(d["pega.R"]))), "pega.R": tuple(espelho(v3(d["pega.L"]))),
                                "giro": -d["giro"]}))
    return saida


def remo_normalizado(p):
    """Os dois pontos a 60 cm um do outro (o remo não estica), em volta do meio deles."""
    l, r = v3(p["pega.L"]), v3(p["pega.R"])
    m, d = (l + r) / 2, (l - r).normalized()
    return m + d * MEIO_DAS_MAOS, m - d * MEIO_DAS_MAOS


# a primeira pessoa no caiaque: o olho no corpo sentado e o truque de primeira pessoa (as mãos um pouco mais para a
# frente e para cima, para o remo aparecer na metade de baixo da tela)
OLHO_SENTADO = Vector((0.0, -0.06, 0.70))
TRUQUE_FP = Vector((0.0, -0.17, -0.01))
OMBRO_REMO = (0.20, 0.07, -0.33)
EIXO_DO_TRONCO_FP = Vector((0.0, 0.12, 0.0))


def no_olho(v):
    return v3(v) - OLHO_SENTADO + TRUQUE_FP


def ombros_fp(giro, base=OMBRO_REMO, sobe=0.0):
    """Os ombros de primeira pessoa girados com o tronco (em volta de um eixo atrás do olho)."""
    R = girar((0, 0, 1), giro)
    o = {}
    for lado, s in (("L", 1), ("R", -1)):
        b = Vector((s * base[0], base[1], base[2]))
        o[lado] = (R @ (b - EIXO_DO_TRONCO_FP)) + EIXO_DO_TRONCO_FP + Vector((0, 0, sobe * s))
    return o


def pose_fp_remo(esq, dedos, pega_l, pega_r, giro, sobe=0.0):
    Mr = quadro_do_remo(pega_l, pega_r)
    maos = maos_no_remo(Mr)
    ombros = ombros_fp(giro, sobe=sobe)
    p = {}
    for lado, s in (("L", 1), ("R", -1)):
        pulso, rot = maos[lado]
        p["ombro." + lado] = ombros[lado]
        p["mao." + lado] = pulso
        p["dedos." + lado] = rot.col[1]
        p["palma." + lado] = -rot.col[2]
        p["polo." + lado] = (s * 0.8, 0.3, -0.6)
        p["dedos_q." + lado] = dedos.remo(lado)
    # o osso do remo: o repouso dele é o remo na pose do Idle (sem giro); a matriz é a do remo vezes o giro do osso
    p["remo"] = Mr @ REMO_NO_OSSO
    return pose_fp(esq, p, dedos)


REMO_IDLE = (Vector((0.30, -0.46, -0.175)), Vector((-0.30, -0.46, -0.175)))
# o osso "remo": Y ao longo do cabo (para a pá esquerda), Z para cima; no espaço do remo, o X do osso é -Y do remo
REMO_NO_OSSO = Matrix(((0, 1, 0, 0), (-1, 0, 0, 0), (0, 0, 1, 0), (0, 0, 0, 1)))


def remo_parado_fp(t):
    """O remo atravessado na frente, embaixo da tela, subindo e descendo com a respiração e balançando de leve."""
    a = 2 * math.pi * t
    l, r = REMO_IDLE
    sobe = Vector((0, 0, 0.009 * math.sin(a)))
    balanco = 0.012 * math.sin(a + 0.8)
    return l + sobe + Vector((0, 0, balanco)), r + sobe - Vector((0, 0, balanco))


def preparar_remo_fp(rig, malha):
    """O osso "remo" (filho do root) e a malha do remo (do Remo.fbx) junto com os braços, 100% no osso."""
    bpy.context.view_layer.objects.active = rig
    for o in bpy.context.scene.objects:
        o.select_set(o == rig)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = rig.data.edit_bones.new("remo")
    eb.parent = rig.data.edit_bones["root"]
    meio = (REMO_IDLE[0] + REMO_IDLE[1]) / 2
    eb.head = meio
    eb.tail = meio + Vector((0.10, 0, 0))
    eb.align_roll(Vector((0, 0, 1)))
    eb.use_deform = True
    bpy.ops.object.mode_set(mode="OBJECT")
    # a malha do remo: os vértices no lugar do Idle
    novos = importar(FBX_REMO)
    remo = [o for o in novos if o.type == "MESH"][0]
    for o in novos:
        if o != remo:
            bpy.data.objects.remove(o, do_unlink=True)
    remo.data.transform(remo.matrix_world)
    remo.matrix_world = Matrix.Identity(4)
    remo.data.transform(Matrix.Translation(meio))
    g = remo.vertex_groups.new(name="remo")
    g.add(list(range(len(remo.data.vertices))), 1.0, "REPLACE")
    with bpy.context.temp_override(active_object=malha, object=malha, selected_objects=[malha, remo],
                                   selected_editable_objects=[malha, remo]):
        bpy.ops.object.join()
    return Esqueleto(rig)


def animar_remo_fp(esq, dedos):
    linha = Linha(remada(0.0))

    def remando(t):
        p = linha(t)
        l, r = remo_normalizado(p)
        # o lado da mão de cima sobe um pouco (o ombro dela)
        return pose_fp_remo(esq, dedos, no_olho(l), no_olho(r), p["giro"], sobe=0.0)

    def parado(t):
        l, r = remo_parado_fp(t)
        return pose_fp_remo(esq, dedos, l, r, 2.0 * math.sin(2 * math.pi * t))

    def sacar(t):
        s = sai_devagar(t)
        l, r = remo_parado_fp(0.0)
        baixo = Vector((0, 0.10, -0.26)) * (1 - s)
        return pose_fp_remo(esq, dedos, l + baixo, r + baixo + Vector((0, 0, -0.04)) * (1 - s), 0.0)

    fontes = {"Idle": parado, "Walk": remando, "Sacar": sacar}
    acoes = []
    for nome, quadros, laco in CLIPES_REMO:
        acoes.append((nome, gravar(esq, nome, quadros, fontes[nome], laco)))
    return acoes


# ================================================================================================ o corpo

OSSOS_DAS_PECAS = ("weapon.R", "slide", "mag", "pump", "shell", "maco", "tampa", "cigarro", "brasa", "isqueiro",
                   "chama", "prato", "colher", "bocado", "caixa", "livro")


def pose_corpo(esq, p, dedos):
    """As matrizes do corpo a partir das chaves:
    quadril (a posição da cabeça do hips), quadril.rot (incl, giro, lado em graus, no mundo), coluna, peito, pescoco,
    cabeca (incl, giro, lado locais), ombro.X (sobe, frente em graus), mao.X/dedos.X/palma.X/polo.X (no mundo, ou no
    espaço do peito com "no_peito"), fecha.X/concha.X ou dedos_q.X, pe.X/pe_y.X/pe_z.X/joelho.X (no mundo, ou no
    espaço do quadril com "no_quadril"). As peças (as armas, o fumo, os móveis) ficam no repouso."""
    rig_rest = esq.rest
    bases = {}
    incl, giro, lado = p.get("quadril.rot", (0, 0, 0))
    Rq = (girar((0, 0, 1), giro) @ girar((1, 0, 0), incl) @ girar((0, 1, 0), lado)).to_3x3()
    Mh = (Rq @ rig_rest["hips"].to_3x3()).to_4x4()
    Mh.translation = v3(p["quadril"])
    for osso, canal in (("spine", "coluna"), ("chest", "peito"), ("neck", "pescoco"), ("head", "cabeca")):
        bases[osso] = rot_local(*p.get(canal, (0, 0, 0)))
    M = {"root": rig_rest["root"].copy(), "hips": Mh}
    esq.completar(M, bases)
    alcance = {}
    # os ombros (as clavículas): sobem e vão para a frente em volta da cabeça deles, nos eixos do peito
    Rp = M["chest"].to_3x3()
    cima, frente = Rp @ Vector((0, 1, 0)), Rp @ Vector((0, 0, 1))
    for lado_, s in (("L", 1), ("R", -1)):
        sobe, avanca = p.get("ombro." + lado_, (0.0, 0.0))
        osso = "shoulder." + lado_
        piv = M[osso].translation.copy()
        R = girar(frente, s * sobe) @ girar(cima, -s * avanca)
        M[osso] = Matrix.Translation(piv) @ R @ Matrix.Translation(-piv) @ M[osso]
        esq.refazer_abaixo(M, osso, bases)
    Mpeito = M["chest"]
    # os braços
    for lado_ in "LR":
        mao, po = v3(p["mao." + lado_]), v3(p["polo." + lado_])
        rot = p.get("giro_mao." + lado_)
        if rot is None:
            rot = rot_da_mao(p["dedos." + lado_], p["palma." + lado_])
        if p.get("no_peito"):
            R3 = Mpeito.to_3x3()
            mao, po, rot = Mpeito @ mao, R3 @ po, R3 @ rot
        S = M["upper_arm." + lado_].translation.copy()
        alcance["braco." + lado_] = braco(esq, M, lado_, S, mao, rot, po)
        qs = p.get("dedos_q." + lado_)
        if qs is None:
            qs = Dedos.misturar(dedos.aberta(lado_, p.get("concha." + lado_, 0.0)), dedos.remo(lado_),
                                p.get("fecha." + lado_, 0.0))
        bases.update(bases_dos_dedos(qs))
        esq.refazer_abaixo(M, "hand." + lado_, bases)
    # as pernas
    Mq = M["hips"]
    for lado_ in "LR":
        pe, jo = v3(p["pe." + lado_]), v3(p["joelho." + lado_])
        rp = p.get("giro_pe." + lado_)
        if rp is not None:
            py, pz = rp.col[1].copy(), rp.col[2].copy()
        else:
            py, pz = v3(p["pe_y." + lado_]), v3(p["pe_z." + lado_])
        if p.get("no_quadril"):
            R3 = Mq.to_3x3()
            pe, py, pz, jo = Mq @ pe, R3 @ py, R3 @ pz, R3 @ jo
        alcance["perna." + lado_] = perna(esq, M, lado_, pe, py, pz, jo)
        bases["toe." + lado_] = rot_local(p.get("dedo_do_pe", 0.0))
        esq.refazer_abaixo(M, "foot." + lado_, bases)
    pose_corpo.alcance = alcance
    return M


pose_corpo.alcance = {}


# ---------------------------------------------------------------------------------------------- boiar

def boiar(t):
    """Boiando em pé: o corpo um pouco deitado para trás, a cabeça e os ombros fora d'água, as pernas no batedor
    (cada pé dando voltas, alternados) e os braços remexendo a água na altura do peito (como na primeira pessoa)."""
    a = 2 * math.pi * 2 * t
    p = {"quadril": (0, 0.02, 1.02 + 0.016 * math.sin(a - 0.6)), "quadril.rot": (-7, 0, 0),
         "coluna": (-2, 0, 0), "peito": (-2 + 1.2 * math.sin(2 * math.pi * t), 0, 0),
         "pescoco": (6, 0, 0), "cabeca": (5 + 1.5 * math.sin(a), 3 * math.sin(2 * math.pi * t), 0)}
    for lado, s in (("L", 1), ("R", -1)):
        # os braços: o mesmo vaivém da primeira pessoa
        x = s * (0.30 + 0.06 * math.sin(a))
        incl = 28 * math.cos(a)
        palma = girar((0, -1, 0), s * incl) @ Vector((0, 0, -1))
        p["mao." + lado] = (x, -0.25 + 0.015 * math.cos(a), 1.23 + 0.012 * math.sin(a))
        p["dedos." + lado] = (s * 0.45, -1, -0.05)
        p["palma." + lado] = tuple(palma)
        p["polo." + lado] = (s * 1, 0.6, -0.4)
        p["ombro." + lado] = (2.0, 2.0)
        p["concha." + lado] = 0.15
        # as pernas: o pé dá uma volta na horizontal por vaivém dos braços (o esquerdo num sentido, o direito no
        # outro, meia volta atrás)
        f = a + (0 if lado == "L" else math.pi)
        cx, cy, cz = s * 0.36, 0.10, 0.58
        p["pe." + lado] = (cx + s * 0.12 * math.cos(f), cy + 0.13 * math.sin(f), cz + 0.05 * math.sin(f + 0.9))
        p["pe_y." + lado] = (s * (0.5 + 0.2 * math.cos(f)), -0.85, 0.2 + 0.2 * math.sin(f))
        p["pe_z." + lado] = (0, 0.2, 1)
        p["joelho." + lado] = (s * 1.0, -0.8, 0.1)
    return p


# ---------------------------------------------------------------------------------------------- nadar (o corpo)

def chaves_do_nado():
    """A braçada de peito no corpo deitado (o mesmo tempo do Walk da primeira pessoa). As mãos no espaço do peito
    (X para a esquerda, Y para a cabeça, Z para a frente do peito, que nadando é para o fundo); as pernas (a pernada
    de sapo) no espaço do quadril. A pernada empurra enquanto os braços vão para a frente (0 a 0,25), as pernas
    deslizam esticadas enquanto os braços puxam e encolhem no fim da puxada (0,7 a 1), com a cabeça subindo para
    respirar."""
    k = [
        (0.00, {"mao.L": (0.07, 0.30, 0.30), "dedos.L": (-0.2, 1, -0.15), "palma.L": (-0.6, 0, 0.8),
                "polo.L": (0.4, -0.6, 0.7), "concha.L": 0.15, "ombro.L": (4.0, 4.0),
                "pe.L": (0.24, -0.46, -0.30), "pe_y.L": (0.8, 0.0, 0.6), "pe_z.L": (0, 1, 0), "joelho.L": (0.7, -0.1, 0.7),
                "quadril.rot": (72, 0, 0), "quadril": (0, 0.34, 1.20), "coluna": (-6, 0, 0), "peito": (-5, 0, 0),
                "pescoco": (-28, 0, 0), "cabeca": (-24, 0, 0)}),
        (0.10, {"pe.L": (0.34, -0.64, -0.17), "pe_y.L": (0.75, -0.2, 0.6), "pe_z.L": (-0.3, 0.9, 0.2),
                "joelho.L": (0.7, -0.2, 0.6)}),
        (0.12, {"mao.L": (0.07, 0.48, 0.20), "dedos.L": (-0.05, 1, -0.05), "palma.L": (-0.3, 0, 1),
                "polo.L": (0.6, -0.4, 0.4), "concha.L": 0.0}),
        (0.20, {"pe.L": (0.17, -0.79, -0.05), "pe_y.L": (0.2, -0.85, 0.4), "pe_z.L": (0, 0.3, 1),
                "joelho.L": (0.4, 0, 1)}),
        (0.25, {"mao.L": (0.07, 0.65, 0.10), "dedos.L": (0, 1, 0.05), "palma.L": (-0.15, 0, 1), "polo.L": (0.9, 0.0, 0.0),
                "ombro.L": (14.0, 8.0), "quadril.rot": (78, 0, 0), "quadril": (0, 0.36, 1.22), "coluna": (-3, 0, 0),
                "peito": (-2, 0, 0), "pescoco": (-24, 0, 0), "cabeca": (-16, 0, 0)}),
        (0.30, {"pe.L": (0.07, -0.82, 0.0), "pe_y.L": (0, -0.95, 0.3), "pe_z.L": (0, 0.3, 0.95), "joelho.L": (0.3, 0, 1)}),
        (0.40, {"mao.L": (0.09, 0.65, 0.10), "dedos.L": (0.15, 1, 0.05), "palma.L": (0.4, 0, 0.9), "polo.L": (0.9, 0.0, -0.35),
                "concha.L": 0.1, "ombro.L": (14.0, 8.0)}),
        (0.50, {"mao.L": (0.30, 0.58, 0.10), "dedos.L": (0.3, 1, 0.1), "palma.L": (0.8, -0.1, 0.55), "polo.L": (0.75, 0, -0.65),
                "concha.L": 0.35, "ombro.L": (10.0, 4.0)}),
        (0.62, {"mao.L": (0.33, 0.46, 0.24), "dedos.L": (0.3, 0.6, 0.75), "palma.L": (0.3, -0.9, 0.3),
                "polo.L": (0.8, 0.2, -0.6), "concha.L": 0.5, "quadril.rot": (74, 0, 0), "quadril": (0, 0.35, 1.21),
                "pescoco": (-28, 0, 0), "cabeca": (-22, 0, 0)}),
        (0.65, {"pe.L": (0.07, -0.82, 0.0), "pe_y.L": (0, -0.95, 0.3), "pe_z.L": (0, 0.3, 0.95), "joelho.L": (0.3, 0, 1)}),
        (0.74, {"mao.L": (0.18, 0.30, 0.34), "dedos.L": (-0.4, 0.4, 0.8), "palma.L": (-0.7, -0.6, 0.3),
                "polo.L": (0.9, 0.0, -0.2), "concha.L": 0.4, "ombro.L": (2.0, 0.0)}),
        (0.80, {"pe.L": (0.12, -0.62, -0.22), "pe_y.L": (0.1, -0.6, 0.8), "pe_z.L": (0, 0.8, 0.6), "joelho.L": (0.5, -0.1, 0.9)}),
        (0.82, {"quadril.rot": (66, 0, 0), "quadril": (0, 0.33, 1.18), "coluna": (-8, 0, 0), "peito": (-7, 0, 0),
                "pescoco": (-32, 0, 0), "cabeca": (-30, 0, 0)}),
        (0.86, {"mao.L": (0.06, 0.26, 0.30), "dedos.L": (-0.4, 0.8, 0.4), "palma.L": (-0.9, 0, 0.4),
                "polo.L": (0.6, -0.6, 0.5), "concha.L": 0.2}),
        (0.92, {"pe.L": (0.21, -0.48, -0.30), "pe_y.L": (0.6, -0.2, 0.75), "pe_z.L": (0, 0.95, 0.3), "joelho.L": (0.7, -0.1, 0.7)}),
    ]
    return com_espelho(k)


def nadar_corpo():
    linha = Linha(chaves_do_nado())

    def f(t):
        p = linha(t)
        p["no_peito"] = True
        p["no_quadril"] = True
        p["dedo_do_pe"] = -10.0
        return p
    return f


# ---------------------------------------------------------------------------------------------- remar (o corpo)

QUADRIL_SENTADO = (0.0, 0.0, 0.02)


def sentado(p):
    """A pose sentada no caiaque: a bacia no assento, as pernas esticadas para a frente, um pouco dobradas, os
    joelhos para cima e para fora, os pés em pé apoiados no fundo."""
    p.setdefault("quadril", QUADRIL_SENTADO)
    for lado, s in (("L", 1), ("R", -1)):
        p["pe." + lado] = (s * 0.115, -0.79, -0.01)
        p["pe_y." + lado] = (s * 0.15, -0.8, 0.55)
        p["pe_z." + lado] = (0, 0.55, 0.8)
        p["joelho." + lado] = (s * 0.02, -0.1, 1)
    return p


def remar_corpo():
    linha = Linha(remada(0.0))

    def f(t):
        k = linha(t)
        l, r = remo_normalizado(k)
        g = k["giro"]
        p = sentado({"quadril.rot": (-4, 0.15 * g, 0), "coluna": (6, 0.35 * g, 0), "peito": (5, 0.45 * g, 0),
                     "pescoco": (-2, -0.45 * g, 0), "cabeca": (-3, -0.35 * g, 0)})
        Mr = quadro_do_remo(l, r)
        maos = maos_no_remo(Mr)
        for lado, s in (("L", 1), ("R", -1)):
            pulso, rot = maos[lado]
            p["mao." + lado] = pulso
            p["dedos." + lado] = rot.col[1]
            p["palma." + lado] = -rot.col[2]
            p["polo." + lado] = (s * 0.8, 0.4, -0.6)
            p["dedos_q." + lado] = None
            # o ombro do lado da mão de cima sobe; o da mão que entra na água vai para a frente
            cima = (v3(l if lado == "L" else r).z - 0.40) / 0.25
            p["ombro." + lado] = (6.0 * cima, 6.0 * max(0.0, -cima))
        f.remo = Mr
        return p
    f.remo = None
    return f


def remar_parado_corpo():
    def f(t):
        a = 2 * math.pi * t
        p = sentado({"quadril.rot": (-4, 0, 0), "coluna": (5 + 1.0 * math.sin(a), 0, 0), "peito": (4 + 1.2 * math.sin(a), 0, 0),
                     "pescoco": (-1, 6 * math.sin(a * 1.0 + 0.5), 0), "cabeca": (-2 - 1.0 * math.sin(a), 6 * math.sin(a + 0.5), 0)})
        sobe = 0.008 * math.sin(a)
        l = Vector((0.30, -0.33, 0.30 + sobe))
        r = Vector((-0.30, -0.33, 0.30 + sobe))
        Mr = quadro_do_remo(l, r)
        maos = maos_no_remo(Mr)
        for lado, s in (("L", 1), ("R", -1)):
            pulso, rot = maos[lado]
            p["mao." + lado] = pulso
            p["dedos." + lado] = rot.col[1]
            p["palma." + lado] = -rot.col[2]
            p["polo." + lado] = (s * 0.8, 0.5, -0.6)
            p["ombro." + lado] = (0.0, 2.0)
        f.remo = Mr
        return p
    f.remo = None
    return f


def animar_corpo(esq, dedos):
    fontes = {"Boiar": boiar, "Nadar": nadar_corpo(), "Remar": remar_corpo(), "Remar_Parado": remar_parado_corpo()}
    acoes = []
    for nome, quadros, laco in CLIPES_CORPO:
        fonte = fontes[nome]

        def pose(t, fonte=fonte):
            p = fonte(t)
            for lado in "LR":
                if p.get("dedos_q." + lado) is None and nome.startswith("Remar"):
                    p["dedos_q." + lado] = dedos.remo(lado)
            return pose_corpo(esq, p, dedos)
        acoes.append((nome, gravar(esq, nome, quadros, pose, laco)))
    return acoes, fontes


# ================================================================================================ as etapas

def etapa_nadar():
    nova_cena()
    rig, malha, boca, esq, dedos = conjunto_fp("Nadar")
    acoes = animar_nadar(esq, dedos)
    rep = acao_de_repouso(esq)
    faixas(rig, acoes)
    exportar(SAIDA_NADAR, [o for o in (rig, malha, boca) if o], rig, {"ARMATURE", "MESH", "EMPTY"}, rig, rep)
    return rig


def etapa_remo():
    nova_cena()
    rig, malha, boca, esq, dedos = conjunto_fp("Remo")
    esq = preparar_remo_fp(rig, malha)
    acoes = animar_remo_fp(esq, dedos)
    rep = acao_de_repouso(esq)
    faixas(rig, acoes)
    exportar(SAIDA_REMO, [o for o in (rig, malha, boca) if o], rig, {"ARMATURE", "MESH", "EMPTY"}, rig, rep)
    return rig


def preparar_corpo():
    """O PrincipalBloco_Rig do Personagem_PrincipalBloco.fbx (sem as ações dele) e, só para as fotos, o
    Jogador_Corpo."""
    importar(FBX_CORPO)
    rig = bpy.data.objects[RIG_CORPO]
    for o in list(bpy.data.objects):
        if o not in (rig,) and o.name != "Jogador_Corpo":
            bpy.data.objects.remove(o, do_unlink=True)
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    _limpar_animacao(rig)
    return rig, Esqueleto(rig)


def dedos_do_fp():
    """O punho do Jogador_FP_Maos.fbx, importado numa cena à parte e apagado."""
    nova_cena()
    importar(FBX_MAOS)
    q = punho_do_fp()
    return Dedos(q)


def etapa_corpo():
    dedos = dedos_do_fp()
    nova_cena()
    rig, esq = preparar_corpo()
    acoes, _ = animar_corpo(esq, dedos)
    rep = acao_de_repouso(esq)
    faixas(rig, acoes)
    exportar(SAIDA_CORPO, [rig], rig, {"ARMATURE"}, rig, rep)
    return rig


# ================================================================================================ conferir

def _ler_fbx(caminho):
    """Os nós do FBX (pelo parser do próprio Blender): {nome: (tipo, matriz local de Lcl, pai)} e as matrizes de
    ligação (TransformLink) dos clusters, por nome de osso."""
    from io_scene_fbx import parse_fbx
    raiz, _versao = parse_fbx.parse(caminho)

    def filho(e, nome):
        for c in e.elems:
            if c.id == nome:
                return c
        return None

    def props(e):
        p70 = filho(e, b"Properties70")
        d = {}
        if p70:
            for p in p70.elems:
                d[p.props[0].decode()] = p.props[4:]
        return d

    objs = filho(raiz, b"Objects")
    con = filho(raiz, b"Connections")
    por_id, nomes = {}, {}
    links = {}
    for e in objs.elems:
        uid = e.props[0]
        nome = e.props[1].split(b"\x00\x01")[0].decode()
        por_id[uid] = e
        nomes[uid] = nome
    pai = {}
    for c in con.elems:
        if c.props[0] == b"OO":
            pai.setdefault(c.props[1], []).append(c.props[2])
    nos = {}
    for uid, e in por_id.items():
        if e.id != b"Model":
            continue
        pr = props(e)
        t = Vector(pr.get("Lcl Translation", (0, 0, 0)))
        r = pr.get("Lcl Rotation", (0, 0, 0))
        s = Vector(pr.get("Lcl Scaling", (1, 1, 1)))
        pre = pr.get("PreRotation", (0, 0, 0))
        from mathutils import Euler
        R = Euler([math.radians(x) for x in pre], "XYZ").to_matrix() @ Euler([math.radians(x) for x in r], "XYZ").to_matrix()
        m = Matrix.Translation(t) @ R.to_4x4() @ Matrix.Diagonal((s.x, s.y, s.z, 1))
        ps = [p for p in pai.get(uid, []) if p in por_id and por_id[p].id == b"Model"]
        nos[nomes[uid]] = (e.props[2].decode(), m, nomes[ps[0]] if ps else None)
    ligacao = {}
    for uid, e in por_id.items():
        if e.id == b"Deformer" and e.props[2] == b"Cluster":
            tl = filho(e, b"TransformLink")
            if tl is None:
                continue
            vals = list(tl.props[0])
            m = Matrix([vals[0:4], vals[4:8], vals[8:12], vals[12:16]]).transposed()
            # o osso ligado a este cluster
            for p in [c.props for c in con.elems]:
                if p[0] == b"OO" and p[2] == uid and p[1] in por_id and por_id[p[1]].id == b"Model":
                    ligacao[nomes[p[1]]] = m
    return nos, ligacao


def _global(nos, nome):
    tipo, m, p = nos[nome]
    return (_global(nos, p) @ m) if p else m


def _dif(a, b):
    return max(abs(a[i][j] - b[i][j]) for i in range(4) for j in range(4))


def conferir_repouso():
    """Compara o esqueleto do FBX novo com o do Personagem_PrincipalBloco.fbx:
    1) no arquivo: a matriz local (Lcl) de cada nó de osso do novo com a do original e a matriz global do novo com a
       de ligação (TransformLink, o repouso da pele) do original;
    2) no Blender: os dois importados com as mesmas opções, o bone.matrix_local de cada osso;
    3) a pose: cada clipe novo tocado no esqueleto original (pelos nomes dos ossos) dá a mesma pose que no novo."""
    rel = {}
    no_o, lig_o = _ler_fbx(FBX_CORPO)
    no_n, _ = _ler_fbx(SAIDA_CORPO)
    ossos_o = {n for n, (t, m, p) in no_o.items() if t == "LimbNode"}
    ossos_n = {n for n, (t, m, p) in no_n.items() if t == "LimbNode"}
    rel["ossos_original"] = len(ossos_o)
    rel["ossos_novo"] = len(ossos_n)
    rel["faltam_no_novo"] = sorted(ossos_o - ossos_n)
    rel["sobram_no_novo"] = sorted(ossos_n - ossos_o)
    rel["pais_diferentes"] = sorted(n for n in ossos_o & ossos_n if no_o[n][2] != no_n[n][2])
    rel["fbx_lcl_max"] = max(_dif(no_o[n][1], no_n[n][1]) for n in ossos_o & ossos_n)
    pior = max(((_dif(_global(no_n, n), lig_o[n]), n) for n in lig_o if n in no_n), default=(None, None))
    rel["fbx_global_novo_vs_ligacao_original_max"] = pior
    rel["no_raiz"] = {n: (no_o[n][0], [round(x, 5) for x in no_o[n][1].to_euler()], [round(x, 5) for x in no_n[n][1].to_euler()])
                      for n in (RIG_CORPO,) if n in no_o and n in no_n}
    # no Blender
    nova_cena()
    importar(FBX_CORPO)
    ro = bpy.data.objects[RIG_CORPO]
    ro.name = "Original"
    for o in list(bpy.data.objects):
        if o.type != "ARMATURE":
            bpy.data.objects.remove(o, do_unlink=True)
    importar(SAIDA_CORPO)
    rn = bpy.data.objects[RIG_CORPO]
    rel["objeto_original"] = [list(map(lambda x: round(x, 6), r)) for r in ro.matrix_world]
    rel["objeto_novo"] = [list(map(lambda x: round(x, 6), r)) for r in rn.matrix_world]
    pior = (0.0, None)
    for b in ro.data.bones:
        bn = rn.data.bones.get(b.name)
        if bn is None:
            continue
        d = _dif(b.matrix_local, bn.matrix_local)
        if d > pior[0]:
            pior = (d, b.name)
    rel["blender_matrix_local_max"] = pior
    # 3) a pose: as ações novas (importadas) no esqueleto original e no novo
    piores = {}
    for nome, quadros, _ in CLIPES_CORPO:
        acao = bpy.data.actions.get(RIG_CORPO + "|" + nome)
        if acao is None:
            piores[nome] = "sem a ação"
            continue
        usar_acao(rn, acao)
        usar_acao(ro, acao)
        pior = 0.0
        for f in range(1, quadros + 2, 3):
            bpy.context.scene.frame_set(f)
            for pb in rn.pose.bones:
                po = ro.pose.bones[pb.name]
                pior = max(pior, _dif(ro.matrix_world @ po.matrix, rn.matrix_world @ pb.matrix))
        piores[nome] = pior
    rel["pose_no_original_max"] = piores
    rel["takes"] = sorted(a.name for a in bpy.data.actions if a.name.startswith(RIG_CORPO))
    return rel


def conferir_clipes():
    """Mede nos três FBX novos (importados de volta): os clipes, os quadros, o laço (o primeiro e o último quadro
    iguais) e o maior giro de um osso de um quadro para o outro."""
    rel = {}
    for caminho in (SAIDA_NADAR, SAIDA_REMO, SAIDA_CORPO):
        nova_cena()
        novos = importar(caminho)
        rig = [o for o in novos if o.type == "ARMATURE"][0]
        info = {"armadura": rig.name, "malhas": [o.name for o in novos if o.type == "MESH"],
                "vazios": [o.name for o in novos if o.type == "EMPTY"], "ossos": len(rig.data.bones),
                "fps": bpy.context.scene.render.fps, "clipes": {}}
        for m in [o for o in novos if o.type == "MESH"]:
            info["materiais_" + m.name] = [s.name for s in m.data.materials]
            info["grupos_" + m.name] = len(m.vertex_groups)
        for a in sorted(bpy.data.actions, key=lambda a: a.name):
            if not a.name.startswith(rig.name + "|"):
                continue
            usar_acao(rig, a)
            f0, f1 = int(a.frame_range[0]), int(a.frame_range[1])
            ant, salto, laco = None, (0.0, None, None), 0.0
            prim = None
            for f in range(f0, f1 + 1):
                bpy.context.scene.frame_set(f)
                mats = {pb.name: pb.matrix.copy() for pb in rig.pose.bones}
                if prim is None:
                    prim = mats
                if ant:
                    for n, m in mats.items():
                        ang = math.degrees(ant[n].to_quaternion().rotation_difference(m.to_quaternion()).angle)
                        ang = min(ang, 360.0 - ang)
                        if ang > salto[0]:
                            salto = (round(ang, 2), n, f)
                ant = mats
            laco = max(_dif(prim[n], ant[n]) for n in prim)
            info["clipes"][a.name.split("|", 1)[1]] = {"quadros": f1 - f0, "segundos": round((f1 - f0) / FPS, 3),
                                                       "fim_menos_comeco": round(laco, 6), "maior_giro_por_quadro": salto}
        rel[os.path.basename(caminho)] = info
    return rel


def remo_na_mao():
    """O remo no espaço da mão direita (e do weapon.R) no Remar: no Blender e nos eixos da Unity (o X trocado)."""
    dedos = dedos_do_fp()
    nova_cena()
    rig, esq = preparar_corpo()
    f = remar_corpo()
    saida = {}
    for t in (0.0, 0.25, 0.5, 0.75):
        p = f(t)
        for lado in "LR":
            p["dedos_q." + lado] = dedos.remo(lado)
        M = pose_corpo(esq, p, dedos)
        rel_ = M["hand.R"].inverted() @ f.remo
        rel_w = M["weapon.R"].inverted() @ f.remo
        saida[t] = (rel_, rel_w)
    rel_, rel_w = saida[0.0]
    var = max(_dif(saida[t][0], rel_) for t in saida)

    def unity(m):
        cabo = m.col[0].xyz       # a direção do cabo (para a pá esquerda)
        face = m.col[1].xyz       # a face de puxar
        cima = m.col[2].xyz
        pos = m.translation
        U = lambda v: [round(-v.x, 4), round(v.y, 4), round(v.z, 4)]
        B = lambda v: [round(v.x, 4), round(v.y, 4), round(v.z, 4)]
        # o Remo.fbx na Unity, filho do osso: os eixos dele (X o cabo, Y para cima, Z a face das costas da pá) no
        # espaço do osso da Unity. No Blender, a malha importada fica com X = -X da Unity, Y = -Z e Z = Y.
        xu, yu, zu = Vector(U(-cabo)), Vector(U(cima)), Vector(U(-face))
        R = Matrix((xu, yu, zu)).transposed()
        q = R.to_quaternion()
        e = R.to_euler("ZXY")
        return {"blender": {"meio_do_cabo": B(pos), "cabo_para_pa_esquerda": B(cabo), "face_de_puxar": B(face), "cima_da_pa": B(cima)},
                "unity": {"meio_do_cabo": U(pos), "cabo_para_pa_esquerda": U(cabo), "face_de_puxar": U(face), "cima_da_pa": U(cima),
                          "Remo_fbx_localPosition": U(pos), "Remo_fbx_eixo_X": B(xu), "Remo_fbx_eixo_Y": B(yu), "Remo_fbx_eixo_Z": B(zu),
                          "Remo_fbx_localRotation_xyzw": [round(q.x, 5), round(q.y, 5), round(q.z, 5), round(q.w, 5)],
                          "Remo_fbx_localEulerAngles": [round(math.degrees(a), 3) for a in e]}}
    return {"hand.R": unity(rel_), "weapon.R": unity(rel_w), "variacao_entre_quadros": var}


def folga_no_caiaque(passo=3):
    """Mede, no Remar e no Remar_Parado (o corpo original com as ações novas, o assento da frente na origem), se as
    pernas passam pelo convés fora da abertura ou se algo passa pelo fundo do casco (as formas do caiaque.py, nos
    eixos da Unity). Devolve o pior de cada (em cm; > 0 é passar)."""
    if AQUI not in sys.path:
        sys.path.insert(0, AQUI)
    import caiaque as cq
    nova_cena()
    importar(FBX_CORPO)
    ro = bpy.data.objects[RIG_CORPO]
    corpo = bpy.data.objects["Jogador_Corpo"]
    for o in list(bpy.data.objects):
        if o not in (ro, corpo):
            bpy.data.objects.remove(o, do_unlink=True)
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    ro.name = "Original"
    importar(SAIDA_CORPO)
    perna = {}
    nomes = {g.index: g.name for g in corpo.vertex_groups}
    for v in corpo.data.vertices:
        for g in v.groups:
            if g.weight > 0.5 and nomes[g.group].split(".")[0] in ("thigh", "shin", "foot", "toe"):
                perna[v.index] = nomes[g.group]
    ax, ay, az = cq.ASSENTOS[0]
    rel = {}
    for nome, quadros, _ in CLIPES_CORPO:
        if not nome.startswith("Remar"):
            continue
        usar_acao(ro, bpy.data.actions[RIG_CORPO + "|" + nome])
        conves, fundo_ = (-9.0, None), (-9.0, None)
        for f in range(1, quadros + 1, passo):
            bpy.context.scene.frame_set(f)
            dg = bpy.context.evaluated_depsgraph_get()
            ev = corpo.evaluated_get(dg)
            me = ev.to_mesh()
            for v in me.vertices:
                w = ev.matrix_world @ v.co
                x, y, z = -w.x + ax, w.z + ay, -w.y + az
                if abs(z) > cq.COMP / 2 or abs(x) > cq.largura(z):
                    continue
                baixo = cq.fundo(z) - y
                if baixo > fundo_[0]:
                    fundo_ = (baixo, f)
                if v.index in perna:
                    dz = abs(z - cq.ABERTURAS[0]) / cq.A_ABERTURA
                    dx = abs(x) / cq.B_ABERTURA
                    if dz ** cq.EXPOENTE_ABERTURA + dx ** cq.EXPOENTE_ABERTURA > 1.0:
                        acima = y - cq.altura_do_conves(x, z)
                        if acima > conves[0]:
                            conves = (acima, f, perna[v.index], round(x, 2), round(z - az, 2))
            ev.to_mesh_clear()
        rel[nome] = {"pernas_acima_do_conves_cm": (round(conves[0] * 100, 1),) + tuple(conves[1:]),
                     "abaixo_do_fundo_cm": (round(fundo_[0] * 100, 1), fundo_[1])}
    return rel


def alcances():
    """O alcance dos braços e das pernas em cada clipe do corpo (1 = esticado)."""
    dedos = dedos_do_fp()
    nova_cena()
    rig, esq = preparar_corpo()
    fontes = {"Boiar": boiar, "Nadar": nadar_corpo(), "Remar": remar_corpo(), "Remar_Parado": remar_parado_corpo()}
    rel = {}
    for nome, quadros, _ in CLIPES_CORPO:
        mx = {}
        for i in range(quadros):
            p = fontes[nome](i / quadros)
            for lado in "LR":
                if p.get("dedos_q." + lado) is None and nome.startswith("Remar"):
                    p["dedos_q." + lado] = dedos.remo(lado)
            pose_corpo(esq, p, dedos)
            for k, v in pose_corpo.alcance.items():
                mx[k] = max(mx.get(k, 0), round(v, 3))
        rel[nome] = mx
    return rel


# ================================================================================================ as fotos

def _textura_no_material(nome, imagem):
    for m in [m for m in bpy.data.materials if m.name == nome or m.name.startswith(nome + ".")]:
        _textura_em(m, imagem)


def _textura_em(m, imagem):
    if not os.path.exists(imagem):
        return
    m.use_nodes = True
    nt = m.node_tree
    bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
    # só a textura da cor (a que entra no Base Color); as outras (o mapa de normais do FBX) saem
    tex = None
    if bsdf and bsdf.inputs["Base Color"].is_linked:
        n = bsdf.inputs["Base Color"].links[0].from_node
        tex = n if n.type == "TEX_IMAGE" else None
    for n in [n for n in nt.nodes if n.type == "TEX_IMAGE" and n != tex]:
        nt.nodes.remove(n)
    if tex is None:
        tex = nt.nodes.new("ShaderNodeTexImage")
        if bsdf:
            nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    img = bpy.data.images.load(imagem, check_existing=True)   # pelo caminho (o do FBX aponta para outro lugar)
    img.alpha_mode = "NONE"
    tex.image = img
    tex.interpolation = "Closest"
    nt.nodes.active = tex


def _render_base(w, h):
    cena = bpy.context.scene
    r = cena.render
    r.engine = "BLENDER_WORKBENCH"
    r.resolution_x, r.resolution_y, r.resolution_percentage = w, h, 100
    r.film_transparent = False
    r.image_settings.file_format = "PNG"
    s = cena.display.shading
    s.light = "STUDIO"
    s.color_type = "TEXTURE"
    s.show_object_outline = True
    s.object_outline_color = (0.05, 0.05, 0.05)
    s.background_type = "VIEWPORT"
    s.background_color = (0.36, 0.46, 0.52)
    cena.view_settings.view_transform = "Standard"
    return cena


def _camera(nome, pos, alvo=None, rot=None, lente=None, angulo_y=None):
    cena = bpy.context.scene
    cam = bpy.data.objects.get(nome)
    if cam is None:
        cam = bpy.data.objects.new(nome, bpy.data.cameras.new(nome))
        cena.collection.objects.link(cam)
    cam.data.clip_start = 0.01
    cam.data.clip_end = 100
    if angulo_y:
        cam.data.sensor_fit = "VERTICAL"
        cam.data.angle_y = math.radians(angulo_y)
    if lente:
        cam.data.sensor_fit = "AUTO"
        cam.data.lens = lente
    cam.location = pos
    if rot is not None:
        cam.rotation_euler = rot
    else:
        d = (Vector(alvo) - Vector(pos)).normalized()
        cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    cena.camera = cam
    return cam


def _moldura_da_agua(z, lado=2.6, nome="Agua"):
    """Um quadrado fino na altura da água (para ver onde ela passa) e um plano bem claro e fino embaixo dele."""
    import bmesh
    me = bpy.data.meshes.new(nome)
    bm = bmesh.new()
    h = lado / 2
    for (x0, y0, x1, y1) in ((-h, -h, h, -h), (h, -h, h, h), (h, h, -h, h), (-h, h, -h, -h)):
        c = Vector(((x0 + x1) / 2, (y0 + y1) / 2, z))
        sx = max(abs(x1 - x0), 0.012)
        sy = max(abs(y1 - y0), 0.012)
        r = bmesh.ops.create_cube(bm, size=1.0)
        bmesh.ops.transform(bm, matrix=Matrix.Translation(c) @ Matrix.Diagonal((sx, sy, 0.006, 1)), verts=r["verts"])
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(nome, me)
    bpy.context.scene.collection.objects.link(ob)
    mat = bpy.data.materials.new(nome)
    mat.diffuse_color = (0.1, 0.35, 0.9, 1)
    me.materials.append(mat)
    return ob


def _salvar(caminho):
    bpy.context.scene.render.filepath = caminho
    bpy.ops.render.render(write_still=True)


def quadros_das_fotos(quadros, n=6):
    return [1 + round(quadros * i / n) for i in range(n)]


def fotos_fp(pasta, conjunto, clipes, extra=None):
    """Os clipes de um conjunto de primeira pessoa (importado de volta do FBX novo) pelo olho, a 55 graus."""
    nova_cena()
    novos = importar({"Nadar": SAIDA_NADAR, "Remo": SAIDA_REMO, "Maos": FBX_MAOS}[conjunto])
    rig = [o for o in novos if o.type == "ARMATURE"][0]
    _textura_no_material("PrincipalBloco", TEXTURA)
    _textura_no_material("Paleta", PALETA)
    _render_base(480, 270)
    _camera("Olho", (0, 0, 0), rot=(math.radians(90), 0, math.radians(180)), angulo_y=55)
    saidas = []
    for nome, quadros in clipes:
        acao = bpy.data.actions[rig.name + "|" + nome]
        usar_acao(rig, acao)
        for f in quadros_das_fotos(quadros):
            bpy.context.scene.frame_set(f)
            c = os.path.join(pasta, f"FP{conjunto}_{nome}_olho_{f:03d}.png")
            _salvar(c)
            saidas.append(c)
    return saidas


def fotos_corpo(pasta):
    """Os clipes do corpo (o Jogador_Corpo do original com as ações novas, importadas de volta), de lado e de 3/4,
    com a altura da água e, no caiaque, o caiaque (o Assento_0 na origem) e o remo na mão direita."""
    nova_cena()
    importar(FBX_CORPO)
    ro = bpy.data.objects[RIG_CORPO]
    for o in list(bpy.data.objects):
        if o not in (ro,) and o.name != "Jogador_Corpo":
            bpy.data.objects.remove(o, do_unlink=True)
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    ro.name = "Original"
    novos = importar(SAIDA_CORPO)
    rn = [o for o in novos if o.type == "ARMATURE"][0]
    _textura_no_material("PrincipalBloco", TEXTURA)
    # o caiaque e o remo (só nas fotos)
    cai = importar(FBX_CAIAQUE)
    casco = [o for o in cai if o.name.startswith("Caiaque")][0]
    assento = [o for o in cai if o.name.startswith("Assento_0")][0]
    bpy.context.view_layer.update()
    casco.location = casco.location - assento.matrix_world.translation
    remo = [o for o in importar(FBX_REMO) if o.type == "MESH"][0]
    remo.data.transform(remo.matrix_world)
    remo.matrix_world = Matrix.Identity(4)
    _textura_no_material("Paleta", PALETA)
    # o remo preso à mão direita, no lugar medido (remo_na_mao)
    rel = remo_na_mao_matriz()
    remo.parent = ro
    remo.parent_type = "BONE"
    remo.parent_bone = "hand.R"
    bpy.context.view_layer.update()
    # o pai de osso fica na ponta do osso: matrix_world = rig @ pose(hand.R) @ T(0, comp, 0) @ local
    comp = ro.data.bones["hand.R"].length
    remo.matrix_parent_inverse = Matrix.Identity(4)
    remo.matrix_basis = Matrix.Translation((0, -comp, 0)) @ rel
    agua_nado = _moldura_da_agua(AGUA_NADANDO, nome="AguaNado")
    agua_cai = _moldura_da_agua(AGUA_CAIAQUE, lado=5.4, nome="AguaCaiaque")
    _render_base(480, 300)
    vistas = {"Nadar": {"lado": ((3.6, -0.05, 1.30), (0, -0.05, 1.20)), "34": ((2.4, -2.8, 2.2), (0, -0.05, 1.20))},
              "Boiar": {"lado": ((3.6, 0.0, 1.0), (0, 0.0, 0.95)), "34": ((2.2, -2.6, 1.7), (0, 0.0, 0.95))},
              "Remar": {"lado": ((3.4, -0.25, 0.40), (0, -0.25, 0.25)), "34": ((2.3, -2.6, 1.5), (0, -0.2, 0.25)),
                        "frente": ((0.0, -3.4, 0.9), (0, -0.2, 0.30))},
              "Remar_Parado": {"lado": ((3.4, -0.25, 0.40), (0, -0.25, 0.25)), "34": ((2.3, -2.6, 1.5), (0, -0.2, 0.25))}}
    saidas = []
    for nome, quadros, _ in CLIPES_CORPO:
        acao = bpy.data.actions[RIG_CORPO + "|" + nome]
        # o corpo é o original (o Jogador_Corpo está preso a ele): a ação nova tocada nele, pelos nomes dos ossos
        usar_acao(ro, acao)
        usar_acao(rn, acao)
        no_caiaque = nome.startswith("Remar")
        for o in (casco, remo, agua_cai):
            o.hide_render = not no_caiaque
        agua_nado.hide_render = no_caiaque
        for vista, (pos, alvo) in vistas[nome].items():
            _camera("Foto", pos, alvo=alvo, lente=40)
            for f in quadros_das_fotos(quadros):
                bpy.context.scene.frame_set(f)
                c = os.path.join(pasta, f"Corpo_{nome}_{vista}_{f:03d}.png")
                _salvar(c)
                saidas.append(c)
    return saidas


def remo_na_mao_matriz():
    """O remo no espaço da mão direita (o mesmo em todos os quadros do Remar e do Remar_Parado)."""
    b = math.radians(GIRO_DA_PEGADA)
    Mr = quadro_do_remo(Vector((0.3, 0, 0)), Vector((-0.3, 0, 0)))
    maos = maos_no_remo(Mr)
    pulso, rot = maos["R"]
    Mh = rot.to_4x4()
    Mh.translation = pulso
    return Mh.inverted() @ Mr


def etapa_fotos(pasta, quais=("Maos", "Nadar", "Remo", "Corpo")):
    os.makedirs(pasta, exist_ok=True)
    if "Maos" in quais:
        fotos_fp(pasta, "Maos", [("Idle", 48), ("Walk", 18)])
    if "Nadar" in quais:
        fotos_fp(pasta, "Nadar", [(n, q) for n, q, _ in CLIPES_NADAR])
    if "Remo" in quais:
        fotos_fp(pasta, "Remo", [(n, q) for n, q, _ in CLIPES_REMO])
    if "Corpo" in quais:
        fotos_corpo(pasta)


# ================================================================================================ as folhas (Pillow)

def folhas(pasta):
    """Junta as fotos de cada clipe numa folha: uma linha por vista, os quadros lado a lado."""
    from PIL import Image, ImageDraw
    import re
    grupos = {}
    for f in sorted(os.listdir(pasta)):
        m = re.match(r"(FP\w+?|Corpo)_(\w+)_(olho|lado|34|frente)_(\d+)\.png$", f)
        if not m:
            continue
        grupos.setdefault((m.group(1), m.group(2)), {}).setdefault(m.group(3), []).append((int(m.group(4)), f))
    saidas = []
    for (conj, clipe), vistas in grupos.items():
        linhas = [sorted(v) for _, v in sorted(vistas.items())]
        if len(linhas) == 1 and len(linhas[0]) > 3:      # uma vista só (a do olho): três por linha
            linhas = [linhas[0][i:i + 3] for i in range(0, len(linhas[0]), 3)]
        w, h = Image.open(os.path.join(pasta, linhas[0][0][1])).size
        ncol = max(len(l) for l in linhas)
        folha = Image.new("RGB", (w * ncol, h * len(linhas) + 22), (30, 30, 30))
        d = ImageDraw.Draw(folha)
        d.text((6, 4), f"{conj} {clipe}", fill=(255, 255, 255))
        for i, linha in enumerate(linhas):
            for j, (q, f) in enumerate(linha):
                im = Image.open(os.path.join(pasta, f)).convert("RGB")
                folha.paste(im, (j * w, 22 + i * h))
                ImageDraw.Draw(folha).text((j * w + 6, 22 + i * h + 4), f"q{q}", fill=(255, 255, 255))
        c = os.path.join(pasta, f"Folha_{conj}_{clipe}.png")
        folha.save(c)
        saidas.append(c)
    print("\n".join(saidas))
    return saidas


# ================================================================================================ main

def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["tudo"]
    etapa = args[0] if args else "tudo"
    pasta = args[1] if len(args) > 1 else None
    if etapa in ("nadar", "tudo"):
        etapa_nadar()
    if etapa in ("remo", "tudo"):
        etapa_remo()
    if etapa in ("corpo", "tudo"):
        etapa_corpo()
    if etapa in ("conferir", "tudo"):
        import json
        rel = {"repouso": conferir_repouso(), "clipes": conferir_clipes(), "remo_na_mao": remo_na_mao(),
               "alcances": alcances(), "caiaque": folga_no_caiaque()}
        print("CONFERIR " + json.dumps(rel, indent=1, default=str, ensure_ascii=False))
    if etapa in ("fotos", "tudo") and pasta:
        etapa_fotos(pasta)
    if etapa.startswith("fotos:") and pasta:     # fotos:Nadar,Remo,Corpo,Maos
        etapa_fotos(pasta, etapa.split(":", 1)[1].split(","))


if __name__ == "__main__":
    if bpy is None:
        if len(sys.argv) > 2 and sys.argv[1] == "folhas":
            folhas(sys.argv[2])
        else:
            print(__doc__ or "uso: py agua_animacoes.py folhas <pasta>")
    else:
        main()
