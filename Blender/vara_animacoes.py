# A vara de pesca nova (07/11/2026, a pedido: "a animação de segurar a vara de pesca, hoje ele segura a vara de uma forma
# bem irregular que não tem lógica, deveria segurar a carretilha com a mão esquerda e a base da vara de pesca com a mão
# direita"): a vara com a carretilha, os braços de primeira pessoa e as animações do corpo, feitos por script num Blender
# sem janela, a partir dos FBX que já estão no projeto (que só são lidos, nunca gravados de novo), em três FBX novos:
#
#   Assets/_Game/Art/Pesca/Vara.fbx
#       a vara sozinha (para a mão do corpo, na terceira pessoa), no formato do Loot.fbx e da Boia.fbx (o objeto girado
#       90° em X, a malha nos eixos da Unity, o material Paleta): Vara (a malha; a origem é a pegada da mão direita, o
#       meio do cabo), Manivela (filha da Vara, a origem no eixo de giro; gira em volta do X dela) e Ponta (um vazio, filho
#       da Vara, onde a linha sai). Na Unity: a ponta para +Z, a carretilha em cima (+Y), a manivela à esquerda (-X).
#       A vara tem 1,80 m: a tampa e o cabo de cortiça (30 cm), o porta-carretilha com o gatilho embaixo, a carretilha
#       de perfil baixo (baitcasting) em cima, logo à frente do cabo, com a manivela dupla do lado esquerdo e a estrela da
#       fricção, o cabo da frente, 8 passadores em cima (cada vez menores, com as amarrações) e a ponteira.
#   Assets/_Game/Art/Characters/Jogador/Jogador_FP_Vara.fbx
#       os braços de primeira pessoa com a vara: FP_Rig_Vara (o rig do Jogador_FP_Maos.fbx com os ossos "vara", filho do
#       root, "manivela", filho da vara, e "ponta", filho da vara), FP_Bracos_Vara (os braços e a vara numa malha só: a
#       vara pesa 100% no osso "vara" e a manivela no "manivela") e Boca_FP_Vara, com os clipes Idle (2,0 s), Walk
#       (1,0 s), Sacar (0,3 s), Lancar (0,9 s), Recolher (0,5 s) e Puxar (1,2 s).
#   Assets/_Game/Art/Characters/Personagens/Personagem_PrincipalBloco_Pesca.fbx
#       o esqueleto do corpo (PrincipalBloco_Rig, o do Personagem_PrincipalBloco.fbx: os mesmos nomes, a mesma hierarquia
#       e o mesmo repouso) com três ossos a mais, que não deformam nada: "vara" (filho do hand.R, parado na mão: segue a
#       vara em todos os quadros), "manivela" e "ponta" (filhos dele); com Vara_Segurar (2,0 s), Vara_Lancar (0,9 s),
#       Vara_Recolher (0,5 s) e Vara_Puxar (1,2 s). Só o tronco e os braços se mexem (as pernas ficam no repouso: na
#       Unity os clipes vão na camada dos braços, com a máscara do tronco). Cada clipe grava todos os ossos em todos os
#       quadros (posição, giro e escala).
#
# Os ossos da vara (no corpo e na primeira pessoa): "vara" com a cabeça na pegada da mão direita (a origem da Vara.fbx),
# o Y dele ao longo da vara, para a ponta, e o Z para cima da vara (o lado da carretilha); "ponta" na ponta (onde a linha
# sai), com os mesmos eixos; "manivela" no eixo da manivela, com o Y dele ao longo do eixo (para a esquerda, para fora
# da carretilha) e o Z para cima da vara: a manivela gira em volta do Y do osso.
#
# Os clipes são a 30 quadros por segundo; os laços terminam na mesma pose do começo (o último quadro é o primeiro).
#
# Os eixos da vara (onde ela é feita, em pé): z ao longo dela (para a ponta), y para cima (o lado da carretilha) e x para
# a esquerda de quem segura (o lado da manivela); a origem na pegada. São os eixos da Unity com o x trocado (a Unity
# espelha o x): na Unity, a ponta para +Z, a carretilha para +Y, a manivela em -X. DEITAR leva a vara para o mundo do
# Blender (o personagem olha para -Y, +X é a esquerda dele, Z para cima): a ponta para -Y, a carretilha para +Z.
#
# A pegada: a mão direita fecha no meio do cabo (o punho do Jogador_FP_Maos, como no remo: o eixo do cabo 7,2 cm à frente
# do pulso e 3 cm abaixo da palma), o polegar para o lado da ponta, as costas da mão para a direita e o cabo um pouco
# inclinado na palma (INCLINACAO_NA_PALMA, como na mão de verdade: o lado do polegar mais para a frente); o pulso fica
# um pouco acima e atrás do cabo (o pulso dobrado para baixo, como quem segura uma vara de pesca). A mão esquerda fecha
# por cima no punho de baixo da manivela (a bolota), a palma para baixo, os dedos para a ponta e o polegar para o lado
# da carretilha; ela segue o punho quando a manivela gira (para a frente: o alto do braço vai para a ponta da vara). No
# corpo, a vara fica parada no espaço da mão direita (hand.R) em todos os quadros (o osso "vara", e o lugar sai no
# "conferir"). Para o braço esquerdo não torcer demais (o IK põe metade da torção no antebraço), o cotovelo esquerdo vai
# para fora e a vara gira um pouco em volta dela: no corpo, a carretilha deitada 20° para a esquerda (e mais 20° no
# Puxar); no lançamento, mais 45° enquanto a vara vai para trás e chicoteia.
#
# Como as poses são feitas: a vara a cada quadro (a pegada, a elevação acima do horizonte, o giro para os lados, o tombo
# e o ângulo da manivela) por chaves misturadas por curvas (a Linha do agua_animacoes.py) ou por senos nos laços; as mãos
# saem da vara; os braços por IK de dois ossos (o do agua_animacoes.py). Daqui são reaproveitadas as funções dele (o IK,
# as curvas, os braços de primeira pessoa, a gravação, a exportação, as conferências, as fotos e as folhas), e as peças
# e o material do loot.py e do pesca.py.
#
# O lançamento (Lancar e Vara_Lancar, 0,9 s): a vara vai para trás por cima do ombro direito (o alto em 0,35 s, a ponta
# atrás da cabeça), chicoteia para a frente (passa da vertical em ~0,41 s) até 10° acima do horizonte (a soltura, em
# 0,50 s) e volta ao Idle até 0,9 s. No Recolher (0,5 s) a manivela dá uma volta; no Puxar (1,2 s), duas, com a vara mais
# alta (~55°) e dois trancos para trás.
#
# Como rodar (Blender 5.2, em segundo plano; um Blender por vez):
#   blender --background --factory-startup --python Blender/vara_animacoes.py -- tudo [pasta das fotos]
#   (as etapas, que também podem ir sozinhas e nesta ordem: vara, fp, corpo, conferir, fotos; "medir" só mostra os
#   alcances dos braços, sem gravar nada; "fotos:Vara,FP,Perto,Corpo" faz só algumas fotos)
# As folhas das fotos (fora do Blender, com o Pillow):
#   py Blender/vara_animacoes.py folhas <pasta das fotos>

import math
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
if AQUI not in sys.path:
    sys.path.insert(0, AQUI)
import agua_animacoes as ag  # noqa: E402

try:
    import bpy
    from mathutils import Matrix, Vector
    import loot as lt      # noqa: E402  (as peças: o torno, as caixas, os cilindros, as ripas, os anéis)
    import pesca as pc     # noqa: E402  (o objeto no formato do Loot.fbx, com o material Paleta)
except ImportError:        # fora do Blender: só as folhas das fotos
    bpy = None
    Matrix = Vector = ag.Vector

ARTE = ag.ARTE
SAIDA_VARA = os.path.join(ARTE, "Pesca", "Vara.fbx")
SAIDA_FP = os.path.join(ag.JOGADOR, "Jogador_FP_Vara.fbx")
SAIDA_CORPO = os.path.join(ag.PERSONAGENS, "Personagem_PrincipalBloco_Pesca.fbx")

FPS = ag.FPS
RIG_CORPO = ag.RIG_CORPO

# os clipes: (nome, quadros, laço)
CLIPES_FP = [("Idle", 60, True), ("Walk", 30, True), ("Sacar", 9, False), ("Lancar", 27, False),
             ("Recolher", 15, True), ("Puxar", 36, True)]
CLIPES_CORPO = [("Vara_Segurar", 60, True), ("Vara_Lancar", 27, False), ("Vara_Recolher", 15, True),
                ("Vara_Puxar", 36, True)]
# o mesmo movimento nos dois: o clipe do corpo de cada clipe da primeira pessoa
DO_CORPO = {"Vara_Segurar": "Idle", "Vara_Lancar": "Lancar", "Vara_Recolher": "Recolher", "Vara_Puxar": "Puxar"}

# ================================================================================================ as medidas da vara
# (m, nos eixos da vara: z para a ponta, y para cima, x para a esquerda de quem segura; a origem na pegada)
FIM = -0.15                 # o fim do cabo (a tampa)
PORTA = (0.15, 0.255)       # o porta-carretilha (o cabo de cortiça, com a tampa, vai do FIM até aqui: 30 cm)
FRENTE = (0.262, 0.305)     # o cabo da frente
INICIO_DO_BLANK = 0.312
PONTA_Z = 1.65              # a ponta (a vara tem 1,80 m)
R_CABO = 0.0153
Z_CARRETILHA = 0.205        # o meio da carretilha
Y_CARRETEL = 0.036          # o eixo do carretel, acima do eixo da vara
PIVO = (0.047, 0.031, Z_CARRETILHA - 0.006)   # o eixo da manivela, no plano do braço dela (a origem da Manivela)
BRACO_MANIVELA = 0.043      # do eixo ao meio de cada punho (a manivela é dupla)
ANGULO_DO_PUNHO = 250.0     # o braço do punho da mão esquerda no ângulo 0 (graus, do alto para a ponta): para trás e
                            # um pouco para baixo
PUNHO = (0.004, 0.033)      # o punho (a bolota) da manivela: de x a x, a partir do eixo
R_PUNHO = 0.0105
PONTA = (0.0, 0.0035, 1.646)   # onde a linha sai (o meio do anel da ponteira)
# os passadores, em cima da vara: onde (z), o raio do anel e a altura do meio do anel acima do eixo
PASSADORES = [(0.50, 0.0085, 0.024), (0.69, 0.0072, 0.020), (0.87, 0.0061, 0.017), (1.04, 0.0052, 0.014),
              (1.20, 0.0045, 0.012), (1.34, 0.0039, 0.0102), (1.46, 0.0034, 0.0090), (1.56, 0.0030, 0.0080)]

# ================================================================================================ as pegadas
INCLINACAO_NA_PALMA = 20.0  # o cabo na palma da mão direita: o lado do polegar inclinado para os dedos (graus)
TOMBO_DA_DIREITA = 8.0      # as costas da mão direita, da direita para cima (graus, em volta do cabo)
DEDOS_DA_ESQUERDA = 90.0    # os dedos da mão esquerda, de cima (0) para a ponta (90): para a ponta, a palma para baixo
FORA_DO_PUNHO = 0.012       # o meio do punho fechado da mão esquerda, para fora do meio da bolota (o indicador perto
                            # do braço da manivela)
PEGADA_DA_MANIVELA = (0.0, 0.066, -0.024)   # o eixo da bolota no espaço da mão esquerda (mais fechada que no cabo)
DOBRA_NA_MANIVELA = (100.0, 112.0)          # as falanges em volta da bolota (graus)

DEDOS = ag.DEDOS


def _rad(g):
    return math.radians(g)


def _rot(eixo, graus):
    return Matrix.Rotation(_rad(graus), 4, eixo)


def _deitar():
    """Os eixos da vara para o mundo do Blender (o z da vara vai para -Y, o y para +Z)."""
    return _rot("X", 90.0)


def osso_vara():
    """O quadro do osso "vara" nos eixos da vara: a cabeça na pegada, o Y para a ponta, o Z para cima (X = Y x Z, a
    direita de quem segura)."""
    return Matrix(((-1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))


def osso_ponta():
    m = osso_vara()
    m.translation = Vector(PONTA)
    return m


def osso_manivela():
    """O osso "manivela": a cabeça no eixo, o Y ao longo do eixo (para fora, +x), o Z para cima (X = Y x Z = +z)."""
    m = Matrix(((0, 1, 0, 0), (0, 0, 1, 0), (1, 0, 0, 0), (0, 0, 0, 1)))
    m.translation = Vector(PIVO)
    return m


def giro_da_manivela(theta):
    """A manivela girada theta graus (para a frente) em volta do eixo dela, nos eixos da vara."""
    p = Vector(PIVO)
    return Matrix.Translation(p) @ _rot("X", theta) @ Matrix.Translation(-p)


def punho_da_manivela(theta):
    """O meio do punho de baixo (o da mão esquerda), nos eixos da vara, com a manivela girada theta graus."""
    a = _rad(ANGULO_DO_PUNHO)
    c = Vector(((PUNHO[0] + PUNHO[1]) / 2, BRACO_MANIVELA * math.cos(a), BRACO_MANIVELA * math.sin(a)))
    return giro_da_manivela(theta) @ (Vector(PIVO) + c)


def mao_direita():
    """O quadro da mão direita (o osso hand.R: o giro e o pulso) nos eixos da vara."""
    t, r = _rad(INCLINACAO_NA_PALMA), _rad(TOMBO_DA_DIREITA)
    # no espaço da mão: o cabo vai para o lado do polegar (-X na mão direita) e um pouco para os dedos (+Y)
    d_h = Vector((-math.cos(t), math.sin(t), 0.0))
    z_h = Vector((0.0, 0.0, 1.0))
    # na vara: o cabo para a ponta (+z) e as costas da mão para a direita (-x), tombadas para cima
    d_v = Vector((0.0, 0.0, 1.0))
    z_v = Vector((-math.cos(r), math.sin(r), 0.0))
    A = Matrix((d_v, d_v.cross(z_v), z_v)).transposed()
    B = Matrix((d_h, d_h.cross(z_h), z_h)).transposed()
    R = A @ B.inverted()
    H = R.to_4x4()
    H.translation = -(R @ Vector(ag.PEGADA))
    return H


def mao_esquerda(theta):
    """O quadro da mão esquerda (hand.L) nos eixos da vara, no punho da manivela girada theta graus: o polegar para a
    carretilha (-x) e os dedos de cima para a ponta (DEDOS_DA_ESQUERDA; com 90, para a ponta, a palma para baixo)."""
    b = _rad(DEDOS_DA_ESQUERDA)
    X = Vector((-1.0, 0.0, 0.0))
    Y = Vector((0.0, math.cos(b), math.sin(b)))
    R = Matrix((X, Y, X.cross(Y))).transposed()
    p = punho_da_manivela(theta) + Vector((FORA_DO_PUNHO, 0.0, 0.0))
    H = R.to_4x4()
    H.translation = p - R @ Vector(PEGADA_DA_MANIVELA)
    return H


def dedos_na_manivela(lado):
    """Os dedos fechados em volta da bolota da manivela (mais que no cabo), o polegar como no punho."""
    q = {}
    for d in DEDOS:
        q[f"{d}1.{lado}"] = ag.Dedos._dobra(DOBRA_NA_MANIVELA[0])
        q[f"{d}2.{lado}"] = ag.Dedos._dobra(DOBRA_NA_MANIVELA[1])
    return q


def quadro(G, e, psi=0.0, phi=0.0, lam=0.0):
    """A vara no mundo (os eixos do modelo do Blender para o mundo): a pegada em G, a ponta e graus acima do horizonte,
    psi graus para a esquerda (em volta de Z), tombada phi graus em volta dela mesma (> 0: a carretilha para a esquerda)
    e lam graus para os lados (em volta do Y do mundo; < 0: o alto da vara para a direita)."""
    R = _rot("Y", lam) @ _rot("Z", psi) @ _rot("X", -e) @ _rot("Y", phi)
    R.translation = Vector(G)
    return R


# ================================================================================================ a vara (o modelo)

def _raio_do_blank(z):
    return 0.0068 + (0.0019 - 0.0068) * (z - INICIO_DO_BLANK) / (PONTA_Z - 0.006 - INICIO_DO_BLANK)


def _perfil():
    """O perfil da vara em pé (o raio, a altura e a cor do trecho até o próximo ponto), do fim do cabo à ponta."""
    f0, p0, p1 = FIM, PORTA[0], PORTA[1]
    p = [(0.0, f0, "preto"), (0.0128, f0, "preto"), (0.0158, f0 + 0.006, "preto"), (0.0158, f0 + 0.016, "preto"),  # a tampa
         (0.0147, f0 + 0.016, "filtro"), (0.0152, -0.07, "filtro"), (R_CABO, 0.0, "filtro"), (0.0150, 0.07, "filtro"),
         (0.0142, p0 - 0.010, "aco"), (0.0142, p0, "grafite"),                                  # o cabo e o anel
         (0.0118, p0 + 0.002, "grafite"), (0.0118, p1 - 0.001, "aco"),                           # o porta-carretilha
         (0.0127, p1 + 0.001, "aco"), (0.0127, FRENTE[0], "filtro"),                             # a rosca
         (0.0122, FRENTE[0], "filtro"), (0.0108, FRENTE[1], "aco"),                              # o cabo da frente
         (0.0086, FRENTE[1], "aco"), (0.0086, INICIO_DO_BLANK, "grafite"),                       # o anel do fim
         (0.0068, INICIO_DO_BLANK, "grafite"), (_raio_do_blank(0.33), 0.33, "vermelho_escuro"),  # o enfeite
         (_raio_do_blank(0.345), 0.345, "grafite")]
    p += [(_raio_do_blank(PONTA_Z - 0.006), PONTA_Z - 0.006, "aco"), (0.0024, PONTA_Z - 0.006, "aco"),
          (0.0024, PONTA_Z - 0.001, "aco"), (0.0, PONTA_Z, "aco")]                               # a ponteira
    return p


def _passador(z, r, h):
    """Um passador em cima da vara: o anel de aço com o miolo escuro, as duas pernas até a vara e as amarrações."""
    rb = _raio_do_blank(z)
    M = Matrix.Translation((0.0, h, 0.0))
    p = [(lt._com_matriz, M, lt._anel("aco", r, 0.0015, 0.0022, z, 8)),
         (lt._com_matriz, M, lt._anel("grafite", r - 0.0013, 0.0010, 0.0024, z, 8))]
    for s in (-1, 1):
        p.append(lt.R("aco", (0, h - r + 0.0004, z), (0, rb - 0.0004, z + s * (0.005 + h * 0.35)), 0.0022, 0.0013,
                      lado=(1, 0, 0)))
        p.append(lt.Z("vermelho_escuro", rb + 0.0006, 0.007, (0, 0, z + s * (0.007 + h * 0.35)), 8))
    return p


def _estrela(x0, x1, centro, raio, dentro, n=5, cor="vermelho"):
    """A estrela da fricção no plano yz (em pé em x0..x1): o miolo redondo e as n pontas (cada uma um triângulo)."""
    yc, zc = centro
    YZ = Matrix(((0, 0, 1, 0), (0, 1, 0, 0), (1, 0, 0, 0), (0, 0, 0, 1)))   # (u, v, w) da placa -> (w, v, u)
    p = [lt.Z(cor, dentro, x1 - x0, ((x0 + x1) / 2, yc, zc), 10, "X")]
    for k in range(n):
        a = 2 * math.pi * k / n
        b1, b2 = a - math.pi / n * 0.8, a + math.pi / n * 0.8
        pts = [(zc + dentro * 0.9 * math.sin(b1), yc + dentro * 0.9 * math.cos(b1)),
               (zc + raio * math.sin(a), yc + raio * math.cos(a)),
               (zc + dentro * 0.9 * math.sin(b2), yc + dentro * 0.9 * math.cos(b2))]
        p.append((lt._com_matriz, YZ, [lt.F(cor, pts, x0, x1)]))
    return p


def _carretilha():
    """A carretilha de perfil baixo em cima do porta-carretilha: o pé, a base, as duas tampas arredondadas (vinho), o
    carretel com a linha amarela, a barra do polegar atrás, o guia da linha na frente, a caixa das engrenagens e a
    estrela da fricção do lado da manivela (+x) e o freio do lado de lá."""
    zc, yc = Z_CARRETILHA, Y_CARRETEL
    YZ = Matrix(((0, 0, 1, 0), (0, 1, 0, 0), (1, 0, 0, 0), (0, 0, 0, 1)))
    p = [lt.C("grafite", (0.014, 0.006, 0.058), (0, 0.0138, zc), chanfro=0.001),          # o pé
         lt.C("grafite", (0.044, 0.007, 0.056), (0, 0.0185, zc - 0.001), chanfro=0.0015)]  # a base
    # as tampas: uma superelipse em pé no plano yz
    pts = []
    for k in range(18):
        a = 2 * math.pi * k / 18
        c, s = math.cos(a), math.sin(a)
        pts.append((zc - 0.002 + 0.032 * math.copysign(abs(s) ** 0.75, s),
                    yc + 0.0205 * math.copysign(abs(c) ** 0.75, c)))
    for x0, x1 in ((0.020, 0.033), (-0.033, -0.020)):
        p.append((lt._com_matriz, YZ, [lt.F("vermelho_escuro", pts, x0, x1)]))
    # o carretel: a linha e as abas
    p += [lt.Z("amarelo", 0.0138, 0.036, (0, yc, zc + 0.003), 14, "X")]
    for s in (-1, 1):
        p.append(lt.Z("prata", 0.0168, 0.0025, (s * 0.0185, yc, zc + 0.003), 14, "X"))
    # a barra do polegar (atrás, em cima) e a tampa de trás, até a base
    p += [lt.C("grafite", (0.041, 0.006, 0.016), (0, yc + 0.0175, zc - 0.021), giro=(-18, 0, 0), chanfro=0.0012),
          lt.C("grafite", (0.041, 0.020, 0.008), (0, yc + 0.004, zc - 0.030), giro=(-10, 0, 0), chanfro=0.0012)]
    # na frente: o guia da linha (a barra, o tubo do sem-fim e a janelinha)
    p += [lt.C("grafite", (0.040, 0.008, 0.010), (0, yc - 0.004, zc + 0.030), chanfro=0.001),
          lt.Z("aco", 0.0028, 0.040, (0, yc - 0.012, zc + 0.031), 8, "X"),
          lt.C("preto", (0.008, 0.006, 0.011), (0.004, yc - 0.002, zc + 0.032))]
    # o lado da manivela: a caixa das engrenagens, a estrela e o eixo
    px, py, pz = PIVO
    p += [lt.Z("vermelho_escuro", 0.018, 0.005, (0.0355, py, pz), 14, "X"),
          lt.Z("prata", 0.0185, 0.0012, (0.0335, py, pz), 14, "X")]
    p += _estrela(0.038, 0.0425, (py, pz), 0.0165, 0.0090)
    p += [lt.Z("aco", 0.0042, 0.006, (0.0440, py, pz), 8, "X")]
    # o outro lado: o freio
    p += [lt.Z("prata", 0.0105, 0.004, (-0.035, yc, zc - 0.002), 12, "X"),
          lt.Z("preto", 0.0060, 0.003, (-0.0385, yc, zc - 0.002), 10, "X")]
    return p


def _pecas_da_vara():
    p = [(lt._torno, _perfil(), 10)]
    for z, r, h in PASSADORES:
        p += _passador(z, r, h)
    # a ponteira: o anelzinho em cima da ponta
    p.append((lt._com_matriz, Matrix.Translation((0, PONTA[1], 0)), lt._anel("aco", 0.0022, 0.0011, 0.0026, PONTA[2], 8)))
    # o gatilho embaixo do porta-carretilha
    p += [lt.R("grafite", (0, -0.010, 0.180), (0, -0.026, 0.171), 0.006, 0.004, lado=(1, 0, 0)),
          lt.R("grafite", (0, -0.026, 0.171), (0, -0.030, 0.158), 0.006, 0.004, lado=(1, 0, 0))]
    p += _carretilha()
    return p


def _pecas_da_manivela():
    """A manivela dupla, a partir do eixo (a origem): o miolo, o braço (do lado de fora da estrela) e as duas bolotas
    pretas, cada uma com a tampinha de aço. O punho de baixo (o da mão esquerda) fica no ANGULO_DO_PUNHO."""
    a = _rad(ANGULO_DO_PUNHO)
    d = Vector((0.0, math.cos(a), math.sin(a))) * BRACO_MANIVELA
    p = [lt.Z("prata", 0.0062, 0.006, (0.002, 0, 0), 10, "X"),
         lt.R("prata", (0.0015, d.y, d.z), (0.0015, -d.y, -d.z), 0.0035, 0.0085, lado=(1, 0, 0)),
         lt.Z("aco", 0.0035, 0.004, (0.0050, 0, 0), 8, "X")]
    for s in (1, -1):
        c = d * s
        p.append(lt.Z("preto", R_PUNHO, PUNHO[1] - PUNHO[0], ((PUNHO[0] + PUNHO[1]) / 2, c.y, c.z), 10, "X"))
        p.append(lt.Z("aco", 0.0042, 0.003, (PUNHO[1] + 0.0012, c.y, c.z), 8, "X"))
    return p


def construir_vara(cena):
    """A Vara (com a origem na pegada), a Manivela (filha dela, a origem no eixo) e a Ponta (o vazio), no formato do
    Loot.fbx: as peças nos eixos da vara, deitadas no modelo do Blender e guardadas de novo nos eixos da Unity."""
    deitar = _deitar()
    vara = pc.objeto("Vara", [(lt._com_matriz, deitar, _pecas_da_vara())], cena)
    man = pc.objeto("Manivela", [(lt._com_matriz, deitar, _pecas_da_manivela())], cena)
    # as filhas: no espaço da Vara (os eixos da vara), sem giro
    man.rotation_euler = (0, 0, 0)
    man.parent = vara
    man.matrix_parent_inverse = Matrix.Identity(4)
    man.location = PIVO
    ponta = bpy.data.objects.new("Ponta", None)
    ponta.empty_display_type = "ARROWS"
    ponta.empty_display_size = 0.03
    cena.collection.objects.link(ponta)
    ponta.parent = vara
    ponta.matrix_parent_inverse = Matrix.Identity(4)
    ponta.location = PONTA
    bpy.context.view_layer.update()
    print(f"vara: {len(vara.data.polygons)} faces na vara, {len(man.data.polygons)} na manivela")
    return vara, man, ponta


def etapa_vara():
    cena = ag.nova_cena()
    vara, man, ponta = construir_vara(cena)
    os.makedirs(os.path.dirname(SAIDA_VARA), exist_ok=True)
    for o in cena.objects:
        o.select_set(o in (vara, man, ponta))
    bpy.context.view_layer.objects.active = vara
    bpy.ops.export_scene.fbx(filepath=SAIDA_VARA, use_selection=True, object_types={"MESH", "EMPTY"},
                             apply_scale_options="FBX_SCALE_ALL", bake_anim=False, mesh_smooth_type="FACE")
    print("vara: gravado", SAIDA_VARA)


def importar_vara():
    """A Vara.fbx importada de volta, com as malhas no modelo do Blender (a Vara e a Manivela soltas, na origem, cada uma
    nos eixos do modelo: a manivela com o eixo no PIVO). Devolve (vara, manivela, ponta)."""
    novos = ag.importar(SAIDA_VARA)
    vara = [o for o in novos if o.name.startswith("Vara")][0]
    man = [o for o in novos if o.name.startswith("Manivela")][0]
    ponta = [o for o in novos if o.name.startswith("Ponta")][0]
    bpy.context.view_layer.update()
    for o in (man, vara):
        m = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = m
        o.data.transform(m)
        o.matrix_world = Matrix.Identity(4)
    return vara, man, ponta


# ================================================================================================ os movimentos
# Cada movimento devolve, para t de 0 a 1: dG (a pegada, somada à base de cada conjunto, nos eixos do mundo; dG_fp, quando
# há, é a da primeira pessoa, que vai menos para o olho e sai da tela pela direita no lançamento), e
# (graus acima do horizonte), psi, phi, lam (os giros da vara, somados aos da base), theta (a manivela, graus) e, para o
# corpo, o tronco (coluna, peito, pescoco, cabeca: incl, giro, lado) e os ombros (sobe, frente); para a primeira pessoa,
# o quanto os ombros sobem e vão (dOmbro.X).

E_IDLE = 33.0
TRONCO = {"coluna": (2.0, -5.0, 0.0), "peito": (3.0, -10.0, 0.0), "pescoco": (0.0, 7.0, 0.0),
          "cabeca": (-3.0, 8.0, 0.0), "ombro.L": (0.0, 20.0), "ombro.R": (0.0, 0.0)}


def _v(*a):
    return Vector(a)


def segurar(t):
    """Segurando, com a respiração: a vara sobe e desce de leve e balança um pouco."""
    a = 2 * math.pi * t
    m = dict(TRONCO)
    m.update({"dG": _v(0.0, 0.0, 0.006 * math.sin(a)), "e": E_IDLE + 0.8 * math.sin(a + 0.5),
              "psi": 0.5 * math.sin(a + 1.0), "theta": 0.0, "peito": (3.0 + 0.8 * math.sin(a), -10.0, 0.0),
              "dOmbro.L": _v(0, 0, 0.004 * math.sin(a)), "dOmbro.R": _v(0, 0, 0.004 * math.sin(a))})
    return m


def andar(t):
    """O balanço do andar (um passo para cada lado): a vara desce em cada passo, vai de um lado para o outro e a ponta
    atrasa um pouco."""
    a = 2 * math.pi * t
    m = segurar(0.0)
    m.update({"dG": _v(0.007 * math.sin(a), 0.004 * math.sin(2 * a + 0.5), 0.008 * math.cos(2 * a)),
              "e": E_IDLE + 0.8 * math.sin(0.5) + 1.4 * math.cos(2 * a + 0.7), "psi": 0.5 * math.sin(1.0) + 1.2 * math.sin(a + 0.3),
              "lam": 1.5 * math.sin(a)})
    return m


def sacar(t):
    """Do fundo da tela até o Idle: a vara vem de baixo, apontada para baixo, e sobe."""
    s = ag.sai_devagar(t)
    m = segurar(0.0)
    m["dG"] = m["dG"] + _v(0.0, 0.08, -0.24) * (1 - s)
    m["e"] = m["e"] - 63.0 * (1 - s)
    return m


SOLTURA = 0.50      # o momento em que a linha sai, no lançamento (s)
ALTO_DO_LANCE = 0.35


def _chaves_do_lance():
    """O lançamento (0,9 s): para trás por cima do ombro direito, o chicote para a frente e a volta."""
    T0 = dict(TRONCO)
    T0.update({"dOmbro.L": (0, 0, 0), "dOmbro.R": (0, 0, 0)})
    k = [
        (0.00, dict(T0, dG=(0, 0, 0), dG_fp=(0, 0, 0), e=E_IDLE + 0.8 * math.sin(0.5), lam=0.0, psi=0.5 * math.sin(1.0))),
        (0.12, dict(dG=(-0.03, 0.01, 0.10), dG_fp=(-0.07, 0.03, 0.06), e=62.0, lam=-4.0, peito=(0.0, -15.0, 0.0), coluna=(0.0, -7.0, 0.0),
                    **{"ombro.R": (5.0, -2.0), "dOmbro.R": (0, 0, 0.04), "dOmbro.L": (0, 0, 0.05)})),
        (0.24, dict(dG=(-0.11, -0.01, 0.19), dG_fp=(-0.16, 0.07, 0.11), e=100.0, lam=-11.0)),
        (ALTO_DO_LANCE, dict(dG=(-0.125, -0.035, 0.22), dG_fp=(-0.20, 0.08, 0.12), e=122.0, lam=-14.0, psi=-2.0, peito=(-4.0, -20.0, 0.0),
                             coluna=(-2.0, -9.0, 0.0), pescoco=(2.0, 12.0, 0.0), cabeca=(0.0, 10.0, 0.0),
                             **{"ombro.R": (10.0, -4.0), "ombro.L": (4.0, 18.0), "dOmbro.R": (-0.02, 0.0, 0.10),
                                "dOmbro.L": (-0.08, -0.06, 0.10)})),
        (0.40, dict(dG=(-0.115, -0.04, 0.21), dG_fp=(-0.18, 0.04, 0.12), e=104.0, lam=-12.0)),
        (0.45, dict(dG=(-0.05, -0.06, 0.15), dG_fp=(-0.10, -0.03, 0.09), e=60.0, lam=-5.0, psi=1.0)),
        (SOLTURA, dict(dG=(-0.02, -0.11, 0.07), dG_fp=(-0.03, -0.08, 0.05), e=10.0, lam=-1.0, psi=1.0, peito=(9.0, -6.0, 0.0), coluna=(5.0, -3.0, 0.0),
                       pescoco=(-4.0, 4.0, 0.0), cabeca=(-6.0, 4.0, 0.0),
                       **{"ombro.R": (2.0, 8.0), "ombro.L": (0.0, 26.0), "dOmbro.R": (0.0, -0.04, 0.02),
                          "dOmbro.L": (0.0, -0.08, 0.02)})),
        (0.58, dict(dG=(-0.01, -0.12, 0.04), dG_fp=(-0.01, -0.09, 0.03), e=6.0, lam=0.0)),
        (0.72, dict(dG=(0.0, -0.06, 0.01), dG_fp=(0.0, -0.05, 0.01), e=20.0, lam=0.0, psi=0.5)),
        (0.90, dict(T0, dG=(0, 0, 0), dG_fp=(0, 0, 0), e=E_IDLE + 0.8 * math.sin(0.5), lam=0.0, psi=0.5 * math.sin(1.0))),
    ]
    return [(s / 0.9, d) for s, d in k]


def _chaves_do_puxao():
    """Brigando com o peixe (1,2 s): a vara alta, dois trancos para trás e o peixe puxando de volta."""
    k = [
        (0.00, dict(dG=(0.0, 0.035, 0.075), dG_fp=(0.0, 0.015, 0.045), e=54.0, peito=(3.0, -10.0, 0.0), coluna=(2.0, -5.0, 0.0),
                    **{"dOmbro.R": (0, 0, 0.03), "dOmbro.L": (0, 0, 0.03)})),
        (0.10, dict(dG=(0.005, 0.085, 0.105), dG_fp=(0.005, 0.035, 0.06), e=67.0, peito=(-4.0, -12.0, 0.0), coluna=(-2.0, -6.0, 0.0),
                    **{"dOmbro.R": (0, 0.02, 0.05), "dOmbro.L": (0, 0.02, 0.05)})),
        (0.24, dict(dG=(0.0, 0.07, 0.10), dG_fp=(0.0, 0.03, 0.06), e=63.0)),
        (0.50, dict(dG=(-0.005, 0.025, 0.07), dG_fp=(-0.005, 0.01, 0.04), e=50.0, peito=(4.0, -9.0, 0.0), coluna=(3.0, -5.0, 0.0),
                    **{"dOmbro.R": (0, 0, 0.03), "dOmbro.L": (0, 0, 0.03)})),
        (0.60, dict(dG=(0.0, 0.035, 0.075), dG_fp=(0.0, 0.015, 0.045), e=54.0)),
        (0.70, dict(dG=(0.01, 0.09, 0.11), dG_fp=(0.01, 0.04, 0.065), e=69.0, peito=(-5.0, -13.0, 0.0), coluna=(-2.0, -6.0, 0.0),
                    **{"dOmbro.R": (0, 0.02, 0.05), "dOmbro.L": (0, 0.02, 0.05)})),
        (0.84, dict(dG=(0.005, 0.07, 0.10), dG_fp=(0.005, 0.03, 0.06), e=64.0)),
        (1.10, dict(dG=(0.0, 0.03, 0.07), dG_fp=(0.0, 0.012, 0.04), e=51.0, peito=(4.0, -9.0, 0.0), coluna=(3.0, -5.0, 0.0),
                    **{"dOmbro.R": (0, 0, 0.03), "dOmbro.L": (0, 0, 0.03)})),
    ]
    return [(s / 1.2, d) for s, d in k]


class _Movimentos:
    """As curvas dos movimentos por chaves (feitas uma vez, quando o Blender está aberto)."""
    lance = None
    puxao = None

    @classmethod
    def preparar(cls):
        if cls.lance is None:
            cls.lance = ag.Linha(_chaves_do_lance(), laco=False)
            cls.puxao = ag.Linha(_chaves_do_puxao(), laco=True)


ROLO_NO_LANCE = 45.0   # o quanto a vara gira em volta dela no lançamento (a carretilha deita para a esquerda)


def _rolo_do_lance(t):
    """De 0 a 1: a vara gira em volta dela enquanto vai para trás (até 0,2 s), fica até o chicote passar (0,47 s) e volta
    até 0,75 s (assim o braço esquerdo, que cruza para o ombro direito, não torce demais)."""
    s = t * 0.9
    if s <= 0.0 or s >= 0.75:
        return 0.0
    if s < 0.20:
        return ag.suave(s / 0.20)
    if s < 0.47:
        return 1.0
    return 1.0 - ag.suave((s - 0.47) / 0.28)


def lancar(t):
    _Movimentos.preparar()
    m = _Movimentos.lance(t)
    m["theta"] = 0.0
    m["phi"] = ROLO_NO_LANCE * _rolo_do_lance(t)
    return m


def recolher(t):
    """Recolhendo a linha: a manivela dá uma volta, a vara quase parada (um tremidinho com a força da mão)."""
    th = 360.0 * t
    a = _rad(th)
    m = segurar(0.0)
    m.update({"dG": _v(0.002 * math.sin(a), 0.0, 0.003 * math.sin(a + 1.0)), "e": E_IDLE - 1.5 + 0.6 * math.sin(a),
              "psi": 0.5 * math.sin(1.0) + 0.3 * math.sin(a), "theta": th, "peito": (3.0, -10.0 + 0.6 * math.sin(a), 0.0),
              "dOmbro.L": _v(0.0, -0.006 * math.sin(a), 0.004 * math.cos(a))})
    return m


def puxar(t):
    _Movimentos.preparar()
    m = _Movimentos.puxao(t)
    a = 2 * math.pi * t
    m["e"] = m["e"] + 1.2 * math.sin(6 * a)
    m["psi"] = 1.4 * math.sin(4 * a + 0.7)
    m["lam"] = 1.0 * math.sin(3 * a)
    m["phi"] = 20.0          # a carretilha mais deitada para a esquerda (o braço esquerdo torce menos)
    m["theta"] = 720.0 * t
    return m


FONTES = {"Idle": segurar, "Walk": andar, "Sacar": sacar, "Lancar": lancar, "Recolher": recolher, "Puxar": puxar}


# ================================================================================================ a primeira pessoa

# a base da primeira pessoa (no espaço do olho: olhando para -Y): a pegada embaixo, à direita, e a vara para a frente
# e para cima, um pouco para o meio; os ombros, como os do remo, um pouco mais para dentro (a mão esquerda cruza até a
# carretilha)
G_FP = (-0.25, -0.40, -0.21)
PSI_FP = 10.0
PHI_FP = 0.0
OMBRO_FP = {"L": (0.06, -0.04, -0.34), "R": (-0.20, 0.05, -0.31)}
POLO_FP = {"L": (0.9, 0.0, -0.5), "R": (-0.8, 0.2, -0.6)}


def q_fp(m):
    G = Vector(G_FP) + Vector(m.get("dG_fp", m.get("dG", (0, 0, 0))))
    V = quadro(G, m["e"], PSI_FP + m.get("psi", 0.0), PHI_FP + m.get("phi", 0.0), m.get("lam", 0.0))
    q = {"V": V, "theta": m.get("theta", 0.0)}
    for lado in "LR":
        q["ombro." + lado] = Vector(OMBRO_FP[lado]) + Vector(m.get("dOmbro." + lado, (0, 0, 0)))
        q["polo." + lado] = POLO_FP[lado]
    return q


def maos_na_vara(V, theta):
    """Os quadros das duas mãos (hand.R e hand.L) no mundo, com a vara em V (o modelo para o mundo)."""
    VS = V @ _deitar()
    return {"R": VS @ mao_direita(), "L": VS @ mao_esquerda(theta)}


def pose_fp_vara(esq, dedos, q, ossos):
    """As matrizes dos braços de primeira pessoa e dos ossos da vara. ossos: o repouso de cada osso da vara no modelo
    (da vara na pose de repouso, a do Idle no quadro 0)."""
    V = q["V"]
    maos = maos_na_vara(V, q["theta"])
    p = {}
    for lado in "LR":
        H = maos[lado]
        p["ombro." + lado] = q["ombro." + lado]
        p["mao." + lado] = H.translation
        p["giro_mao." + lado] = H.to_3x3()
        p["polo." + lado] = q["polo." + lado]
    p["dedos_q.R"] = dedos.remo("R")
    p["dedos_q.L"] = dedos_na_manivela("L")
    M = ag.pose_fp(esq, p, dedos)
    deitar = _deitar()
    M["vara"] = V @ ossos["vara"]
    M["ponta"] = V @ ossos["ponta"]
    M["manivela"] = V @ deitar @ giro_da_manivela(q["theta"]) @ deitar.inverted() @ ossos["manivela"]
    # o alcance dos braços (1 = esticado), para as medidas
    alc = {}
    for lado in "LR":
        S = Vector(p["ombro." + lado])
        a = esq.comprimento("upper_arm." + lado, "forearm." + lado)
        b = esq.comprimento("forearm." + lado, "hand." + lado)
        alc[lado] = (Vector(p["mao." + lado]) - S).length / (a + b)
    pose_fp_vara.alcance = alc
    return M


pose_fp_vara.alcance = {}


def _novo_osso(rig, nome, pai, M, comprimento, deforma):
    eb = rig.data.edit_bones.new(nome)
    eb.parent = rig.data.edit_bones[pai]
    eb.head = M.translation
    eb.tail = M.translation + M.to_3x3().col[1] * comprimento
    eb.align_roll(M.to_3x3().col[2])
    eb.use_deform = deforma
    return eb


def criar_ossos_da_vara(rig, pai, M_vara_std):
    """Os ossos "vara" (filho de pai), "manivela" e "ponta" (filhos da vara). M_vara_std leva os eixos da vara para o
    espaço da armadura, no repouso."""
    bpy.context.view_layer.objects.active = rig
    for o in bpy.context.scene.objects:
        o.select_set(o == rig)
    bpy.ops.object.mode_set(mode="EDIT")
    _novo_osso(rig, "vara", pai, M_vara_std @ osso_vara(), PONTA[2], True)
    _novo_osso(rig, "manivela", "vara", M_vara_std @ osso_manivela(), 0.03, True)
    _novo_osso(rig, "ponta", "vara", M_vara_std @ osso_ponta(), 0.05, False)
    bpy.ops.object.mode_set(mode="OBJECT")


def preparar_vara_fp(rig, malha):
    """Os ossos da vara no rig de primeira pessoa e a malha da vara (da Vara.fbx) junto com os braços, na pose do Idle no
    quadro 0: a vara 100% no osso "vara", a manivela no "manivela". Devolve (esqueleto, ossos no modelo)."""
    V0 = q_fp(segurar(0.0))["V"]
    criar_ossos_da_vara(rig, "root", V0 @ _deitar())
    vara, man, ponta = importar_vara()
    bpy.data.objects.remove(ponta, do_unlink=True)
    for o, grupo in ((vara, "vara"), (man, "manivela")):
        o.data.transform(V0)
        g = o.vertex_groups.new(name=grupo)
        g.add(list(range(len(o.data.vertices))), 1.0, "REPLACE")
    with bpy.context.temp_override(active_object=malha, object=malha, selected_objects=[malha, vara, man],
                                   selected_editable_objects=[malha, vara, man]):
        bpy.ops.object.join()
    esq = ag.Esqueleto(rig)
    ossos = {n: V0.inverted() @ esq.rest[n] for n in ("vara", "manivela", "ponta")}
    return esq, ossos


def animar_fp(esq, dedos, ossos):
    acoes = []
    for nome, quadros, laco in CLIPES_FP:
        f = FONTES[nome]
        acoes.append((nome, ag.gravar(esq, nome, quadros, lambda t, f=f: pose_fp_vara(esq, dedos, q_fp(f(t)), ossos), laco)))
    return acoes


def etapa_fp():
    ag.nova_cena()
    rig, malha, boca, esq, dedos = ag.conjunto_fp("Vara")
    esq, ossos = preparar_vara_fp(rig, malha)
    acoes = animar_fp(esq, dedos, ossos)
    rep = ag.acao_de_repouso(esq)
    ag.faixas(rig, acoes)
    ag.exportar(SAIDA_FP, [o for o in (rig, malha, boca) if o], rig, {"ARMATURE", "MESH", "EMPTY"}, rig, rep)
    return rig


# ================================================================================================ o corpo

# a base do corpo: a pegada na frente da barriga, um pouco à direita; o tronco virado um pouco para a direita (o ombro
# esquerdo para a frente, para a mão esquerda chegar à carretilha) e a carretilha deitada 20° para a esquerda (o braço
# esquerdo torce menos)
G_CORPO = (-0.075, -0.265, 1.10)
PSI_CORPO = 2.0
PHI_CORPO = 20.0
POLO_CORPO = {"L": (0.9, 0.0, -0.4), "R": (-0.7, 0.3, -0.7)}
PERNAS = ("thigh.L", "shin.L", "foot.L", "toe.L", "thigh.R", "shin.R", "foot.R", "toe.R")


def q_corpo(m):
    G = Vector(G_CORPO) + Vector(m.get("dG", (0, 0, 0)))
    V = quadro(G, m["e"], PSI_CORPO + m.get("psi", 0.0), PHI_CORPO + m.get("phi", 0.0), m.get("lam", 0.0))
    q = {"V": V, "theta": m.get("theta", 0.0)}
    for k, v in TRONCO.items():
        q[k] = m.get(k, v)
    return q


def pose_corpo_vara(esq, dedos, q):
    """As matrizes do corpo: o quadril e as pernas no repouso, o tronco das chaves, as mãos na vara (por IK) e os ossos
    da vara: "vara" e "ponta" parados na mão direita e "manivela" girada theta graus."""
    maos = maos_na_vara(q["V"], q["theta"])
    p = {"quadril": esq.rest["hips"].translation.copy(), "quadril.rot": (0, 0, 0)}
    for k in ("coluna", "peito", "pescoco", "cabeca", "ombro.L", "ombro.R"):
        p[k] = tuple(q[k])
    for lado in "LR":
        H = maos[lado]
        p["mao." + lado] = H.translation
        p["giro_mao." + lado] = H.to_3x3()
        p["polo." + lado] = POLO_CORPO[lado]
        # as pernas: qualquer coisa (refeitas no repouso logo abaixo)
        r = esq.rest["foot." + lado]
        p["pe." + lado], p["pe_y." + lado], p["pe_z." + lado] = r.translation, r.col[1].xyz, r.col[2].xyz
        p["joelho." + lado] = (0, -1, 0)
    p["dedos_q.R"] = dedos.remo("R")
    p["dedos_q.L"] = dedos_na_manivela("L")
    M = ag.pose_corpo(esq, p, dedos)
    for n in PERNAS:
        M.pop(n, None)
    esq.completar(M, {})
    # a manivela: girada em volta do Y dela (o eixo da manivela)
    M["manivela"] = M["vara"] @ esq.rel["manivela"] @ Matrix.Rotation(_rad(q["theta"]), 4, "Y")
    return M


def preparar_corpo_vara():
    """O PrincipalBloco_Rig do Personagem_PrincipalBloco.fbx com os ossos da vara (filhos do hand.R, no repouso: a vara
    na mão do repouso, como ela fica na mão em todos os quadros)."""
    rig, esq = ag.preparar_corpo()
    na_mao = esq.rest["hand.R"] @ mao_direita().inverted()      # os eixos da vara para a armadura, no repouso
    criar_ossos_da_vara(rig, "hand.R", na_mao)
    return rig, ag.Esqueleto(rig)


def animar_corpo(esq, dedos):
    acoes = []
    for nome, quadros, laco in CLIPES_CORPO:
        f = FONTES[DO_CORPO[nome]]
        acoes.append((nome, ag.gravar(esq, nome, quadros, lambda t, f=f: pose_corpo_vara(esq, dedos, q_corpo(f(t))), laco)))
    return acoes


def etapa_corpo():
    dedos = ag.dedos_do_fp()
    ag.nova_cena()
    rig, esq = preparar_corpo_vara()
    acoes = animar_corpo(esq, dedos)
    rep = ag.acao_de_repouso(esq)
    ag.faixas(rig, acoes)
    ag.exportar(SAIDA_CORPO, [rig], rig, {"ARMATURE"}, rig, rep)
    return rig


# ================================================================================================ medir e conferir

def medir():
    """O maior alcance dos braços (1 = esticado) em cada clipe, na primeira pessoa e no corpo."""
    rel = {"fp": {}, "corpo": {}}
    ag.nova_cena()
    rig, malha, boca, esq, dedos = ag.conjunto_fp("Vara")
    esq, ossos = preparar_vara_fp(rig, malha)
    for nome, quadros, _ in CLIPES_FP:
        mx = {"L": (0, 0), "R": (0, 0)}
        for i in range(quadros + 1):
            pose_fp_vara(esq, dedos, q_fp(FONTES[nome](i / quadros)), ossos)
            for lado, v in pose_fp_vara.alcance.items():
                if v > mx[lado][0]:
                    mx[lado] = (round(v, 3), i)
        rel["fp"][nome] = mx
    dedos = ag.dedos_do_fp()
    ag.nova_cena()
    rig, esq = preparar_corpo_vara()
    for nome, quadros, _ in CLIPES_CORPO:
        mx = {}
        for i in range(quadros + 1):
            pose_corpo_vara(esq, dedos, q_corpo(FONTES[DO_CORPO[nome]](i / quadros)))
            for k, v in ag.pose_corpo.alcance.items():
                if k.startswith("braco") and v > mx.get(k, (0, 0))[0]:
                    mx[k] = (round(v, 3), i)
        rel["corpo"][nome] = mx
    return rel


class _Caminhos:
    """Troca, por um tempo, os caminhos e os clipes do agua_animacoes pelos daqui (para usar as conferências dele)."""

    def __init__(self, **novos):
        self.novos = novos

    def __enter__(self):
        self.antes = {k: getattr(ag, k) for k in self.novos}
        for k, v in self.novos.items():
            setattr(ag, k, v)

    def __exit__(self, *a):
        for k, v in self.antes.items():
            setattr(ag, k, v)


OSSOS_DA_VARA = ("vara", "manivela", "ponta")


def conferir_repouso():
    """Compara o esqueleto do FBX novo do corpo com o do Personagem_PrincipalBloco.fbx (como o conferir_repouso do
    agua_animacoes.py, menos os três ossos da vara, que só existem no novo): no arquivo (a matriz local de cada nó e a
    global do novo com a de ligação do original), no Blender (o matrix_local de cada osso) e na pose (cada clipe novo
    tocado no esqueleto original dá a mesma pose nos ossos dele)."""
    rel = {}
    no_o, lig_o = ag._ler_fbx(ag.FBX_CORPO)
    no_n, _ = ag._ler_fbx(SAIDA_CORPO)
    ossos_o = {n for n, (t, m, p) in no_o.items() if t == "LimbNode"}
    ossos_n = {n for n, (t, m, p) in no_n.items() if t == "LimbNode"}
    rel["ossos_original"], rel["ossos_novo"] = len(ossos_o), len(ossos_n)
    rel["faltam_no_novo"] = sorted(ossos_o - ossos_n)
    rel["sobram_no_novo"] = sorted(ossos_n - ossos_o)
    rel["pais_dos_ossos_da_vara"] = {n: no_n[n][2] for n in OSSOS_DA_VARA if n in no_n}
    rel["pais_diferentes"] = sorted(n for n in ossos_o & ossos_n if no_o[n][2] != no_n[n][2])
    rel["fbx_lcl_max"] = max(ag._dif(no_o[n][1], no_n[n][1]) for n in ossos_o & ossos_n)
    rel["fbx_global_novo_vs_ligacao_original_max"] = max(((ag._dif(ag._global(no_n, n), lig_o[n]), n) for n in lig_o
                                                          if n in no_n), default=(None, None))
    ag.nova_cena()
    ag.importar(ag.FBX_CORPO)
    ro = bpy.data.objects[RIG_CORPO]
    ro.name = "Original"
    for o in list(bpy.data.objects):
        if o.type != "ARMATURE":
            bpy.data.objects.remove(o, do_unlink=True)
    ag.importar(SAIDA_CORPO)
    rn = bpy.data.objects[RIG_CORPO]
    rel["objeto_igual"] = ag._dif(ro.matrix_world, rn.matrix_world)
    pior = (0.0, None)
    for b in ro.data.bones:
        bn = rn.data.bones.get(b.name)
        if bn is not None:
            d = ag._dif(b.matrix_local, bn.matrix_local)
            if d > pior[0]:
                pior = (d, b.name)
    rel["blender_matrix_local_max"] = pior
    piores = {}
    for nome, quadros, _ in CLIPES_CORPO:
        acao = bpy.data.actions.get(RIG_CORPO + "|" + nome)
        if acao is None:
            piores[nome] = "sem a ação"
            continue
        ag.usar_acao(rn, acao)
        ag.usar_acao(ro, acao)
        pior = 0.0
        for f in range(1, quadros + 2, 3):
            bpy.context.scene.frame_set(f)
            for po in ro.pose.bones:
                pior = max(pior, ag._dif(ro.matrix_world @ po.matrix, rn.matrix_world @ rn.pose.bones[po.name].matrix))
        piores[nome] = pior
    rel["pose_no_original_max"] = piores
    rel["takes"] = sorted(a.name for a in bpy.data.actions if a.name.startswith(RIG_CORPO))
    return rel


def conferir_clipes():
    """Os clipes, os quadros, o laço e o maior giro por quadro nos dois FBX novos com rig (pelo conferir_clipes do
    agua_animacoes.py; o da primeira pessoa vai duas vezes, no lugar do nado e do remo)."""
    with _Caminhos(SAIDA_NADAR=SAIDA_FP, SAIDA_REMO=SAIDA_FP, SAIDA_CORPO=SAIDA_CORPO):
        return ag.conferir_clipes()


def _unity(v):
    return [round(-v.x, 5), round(v.y, 5), round(v.z, 5)]


def _blender(v):
    return [round(v.x, 5), round(v.y, 5), round(v.z, 5)]


def _giro_unity(xu, yu, zu):
    """O giro da Unity (xyzw e os ângulos de Euler, ZXY) de um objeto com os eixos xu, yu, zu (já nos eixos da Unity)."""
    R = Matrix((Vector(xu), Vector(yu), Vector(zu))).transposed()
    q = R.to_quaternion()
    e = R.to_euler("ZXY")
    return {"localRotation_xyzw": [round(q.x, 5), round(q.y, 5), round(q.z, 5), round(q.w, 5)],
            "localEulerAngles": [round(math.degrees(a), 3) for a in e]}


def _vara_unity_em(H):
    """A Vara.fbx (os eixos dela na Unity: X = -x da vara, Y = y, Z = z) num espaço da Unity, dado H: os eixos da vara
    para o espaço do Blender desse osso. Devolve a posição e o giro locais na Unity."""
    R = H.to_3x3()
    xu = _unity(R @ Vector((-1, 0, 0)))
    yu = _unity(R @ Vector((0, 1, 0)))
    zu = _unity(R @ Vector((0, 0, 1)))
    d = {"localPosition": _unity(H.translation)}
    d.update(_giro_unity(xu, yu, zu))
    return d


def vara_na_mao():
    """A vara no espaço da mão direita do corpo, medida nas poses de todos os clipes (que deve ser a mesma em todos os
    quadros): o lugar e o giro da Vara.fbx como filha do hand.R (como o PegadaNaMao/GiroNaMao do remo no Caiaque.cs) e
    como filha do osso "vara", a ponta, o eixo da manivela e os momentos do lançamento."""
    dedos = ag.dedos_do_fp()
    ag.nova_cena()
    rig, esq = preparar_corpo_vara()
    deitar = _deitar()
    medidas = []
    for nome, quadros, _ in CLIPES_CORPO:
        for i in range(0, quadros + 1, 3):
            q = q_corpo(FONTES[DO_CORPO[nome]](i / quadros))
            M = pose_corpo_vara(esq, dedos, q)
            # a vara de verdade (a da pose) e a que o osso vara diz
            medidas.append((M["hand.R"].inverted() @ q["V"] @ deitar, M["vara"] @ osso_vara().inverted(), M["hand.R"]))
    H0 = medidas[0][0]
    var_mao = max(ag._dif(m[0], H0) for m in medidas)
    var_osso = max(ag._dif(m[2] @ m[0], m[1]) for m in medidas)
    rel = {"na_mao_hand.R": _vara_unity_em(H0), "variacao_entre_quadros": var_mao,
           "osso_vara_menos_vara_max": var_osso}
    rel["na_mao_hand.R"]["blender"] = {"pegada": _blender(H0.translation),
                                       "ponta_para": _blender(H0.to_3x3() @ Vector((0, 0, 1))),
                                       "carretilha_para": _blender(H0.to_3x3() @ Vector((0, 1, 0)))}
    # a Vara.fbx como filha do osso "vara" (a cabeça na pegada, Y para a ponta, Z para cima)
    rel["sob_o_osso_vara"] = _vara_unity_em(osso_vara().inverted())
    # o osso vara, no hand.R (o rel do repouso, o mesmo em todos os quadros), na Unity
    rv = esq.rel["vara"]
    rel["osso_vara_no_hand.R"] = {"localPosition": _unity(rv.translation),
                                  **_giro_unity(_unity(rv.to_3x3() @ Vector((-1, 0, 0))), _unity(rv.to_3x3().col[1]),
                                                _unity(rv.to_3x3().col[2]))}
    # (os eixos da vara são os do FBX: na Unity, só o x trocado)
    rel["ponta"] = {"na_Vara_fbx_unity": _unity(Vector(PONTA)),
                    "no_osso_vara_unity": _unity(osso_vara().inverted() @ Vector(PONTA)),
                    "osso_ponta_no_osso_vara_unity": _unity(esq.rel["ponta"].translation)}
    rel["manivela"] = {"pivo_na_Vara_fbx_unity": _unity(Vector(PIVO)),
                       "eixo_na_Vara_fbx_unity": "X local da Manivela (Vector3.right)",
                       "girar_para_recolher": "Manivela.localRotation = Quaternion.AngleAxis(theta, Vector3.right), theta "
                                              "crescendo (o alto do braço vai para a ponta da vara)",
                       "theta_dos_clipes": "Vara_Recolher: 360 * t (uma volta em 0,5 s); Vara_Puxar: 720 * t (duas em 1,2 s);"
                                           " Vara_Segurar e Vara_Lancar: 0 (t de 0 a 1 no clipe)",
                       "punho_da_mao_esquerda_em_theta_0_unity": _unity(punho_da_manivela(0.0)),
                       "osso_manivela_no_osso_vara_unity": _unity(esq.rel["manivela"].translation)}
    # o lançamento: os momentos pela elevação da vara
    passos = [i / 900 for i in range(901)]
    es = [(t * 0.9, lancar(t)["e"]) for t in passos]
    alto = max(es, key=lambda x: x[1])
    vertical = next((s for s, e in es if s > alto[0] and e <= 90.0), None)
    quarenta = next((s for s, e in es if s > alto[0] and e <= 45.0), None)
    baixo = min((x for x in es if x[0] > alto[0]), key=lambda x: x[1])
    rel["lancamento_s"] = {"comeca_para_tras": 0.0, "alto_atras": round(alto[0], 3), "elevacao_no_alto": round(alto[1], 1),
                           "passa_da_vertical_para_a_frente": round(vertical, 3),
                           "passa_de_45_graus": round(quarenta, 3), "soltura": SOLTURA,
                           "mais_baixo": (round(baixo[0], 3), round(baixo[1], 1)), "volta_ao_idle": 0.9}
    return rel


def eixos_da_vara_fbx():
    """Lê a Vara.fbx de volta (o parser do Blender): os nós, o lugar das filhas e o tamanho da malha nos eixos do FBX
    (a Unity troca o x): para onde a ponta, a carretilha e a manivela vão."""
    from io_scene_fbx import parse_fbx
    nos, _ = ag._ler_fbx(SAIDA_VARA)
    raiz, _ = parse_fbx.parse(SAIDA_VARA)
    objs = next(e for e in raiz.elems if e.id == b"Objects")
    con = next(e for e in raiz.elems if e.id == b"Connections")
    geo = {}
    for e in objs.elems:
        if e.id == b"Geometry":
            vs = next(c for c in e.elems if c.id == b"Vertices").props[0]
            pts = [Vector(vs[i:i + 3]) for i in range(0, len(vs), 3)]
            geo[e.props[0]] = pts
    nome_do_modelo = {e.props[0]: e.props[1].split(b"\x00\x01")[0].decode() for e in objs.elems if e.id == b"Model"}
    rel = {"nos": {n: {"tipo": t, "pai": p, "Lcl_translacao": _blender(m.translation),
                       "Lcl_giro_euler": [round(math.degrees(a), 3) for a in m.to_euler()]} for n, (t, m, p) in nos.items()}}
    for c in con.elems:
        if c.props[0] == b"OO" and c.props[1] in geo and c.props[2] in nome_do_modelo:
            pts = geo[c.props[1]]
            lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
            hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
            # na Unity: x trocado (o mínimo vira o máximo)
            rel["malha_" + nome_do_modelo[c.props[2]]] = {"fbx_min": _blender(lo), "fbx_max": _blender(hi),
                                                         "unity_min": [round(-hi.x, 4), round(lo.y, 4), round(lo.z, 4)],
                                                         "unity_max": [round(-lo.x, 4), round(hi.y, 4), round(hi.z, 4)],
                                                         "vertices": len(pts)}
    try:
        gs = next(e for e in raiz.elems if e.id == b"GlobalSettings")
        p70 = next(c for c in gs.elems if c.id == b"Properties70")
        rel["eixos_do_arquivo"] = {p.props[0].decode(): p.props[4] for p in p70.elems
                                   if p.props[0] in (b"UpAxis", b"UpAxisSign", b"FrontAxis", b"FrontAxisSign",
                                                     b"CoordAxis", b"CoordAxisSign", b"UnitScaleFactor")}
    except StopIteration:
        pass
    return rel


def _distancia_ao_segmento(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(1e-12, ab.dot(ab))))
    return (p - (a + ab * t)).length, t


def _raio_em(s):
    """O raio da vara na distância s da pegada (a carretilha conta à parte)."""
    if s < PORTA[1]:
        return 0.0158
    if s < FRENTE[1]:
        return 0.0127
    return _raio_do_blank(max(s, INICIO_DO_BLANK))


def _folga_da_vara(pontos, Mv):
    """A menor folga (m; < 0 é atravessar) dos pontos (nome, posição) até a vara, com a vara no quadro Mv (os eixos da
    vara para o mundo): o eixo da vara do fim do cabo até a ponta, com o raio dela, e a carretilha (uma bola de 4 cm em
    volta do meio dela)."""
    a = Mv @ Vector((0, 0, FIM))
    b = Mv @ Vector((0, 0, PONTA_Z))
    carr = Mv @ Vector((0, Y_CARRETEL, Z_CARRETILHA))
    pior = (9.0, None, None)
    for nome, p in pontos:
        d, t = _distancia_ao_segmento(p, a, b)
        s = FIM + t * (PONTA_Z - FIM)
        f = d - _raio_em(s)
        f2 = (p - carr).length - 0.040
        if f2 < f:
            f, s = f2, "carretilha"
        if f < pior[0]:
            pior = (f, nome, s)
    return pior


MAOS_E_DEDOS = {f"{d}{i}.{l}" for d in DEDOS for i in (1, 2) for l in "LR"} | \
               {f"{n}.{l}" for n in ("thumb", "thumb2", "hand") for l in "LR"}


def maos_na_vara_dos_fbx():
    """Nos dois FBX novos (importados de volta), a cada quadro de cada clipe: a distância (cm) do eixo do cabo na mão
    direita até a pegada da vara (o osso "vara") e do meio da bolota na mão esquerda até o punho de baixo da manivela (o
    osso "manivela"). As duas devem ser quase zero: as mãos seguram a vara e a esquerda segue a manivela."""
    na_direita = Vector(ag.PEGADA)
    na_esquerda = Vector(PEGADA_DA_MANIVELA) + Vector((FORA_DO_PUNHO, 0.0, 0.0))
    # o punho de baixo no espaço do osso manivela (X = z da vara, Y = x da vara, Z = y da vara), no ângulo 0
    punho = osso_manivela().inverted() @ punho_da_manivela(0.0)
    rel = {}
    for caminho, clipes in ((SAIDA_FP, CLIPES_FP), (SAIDA_CORPO, CLIPES_CORPO)):
        ag.nova_cena()
        rig = [o for o in ag.importar(caminho) if o.type == "ARMATURE"][0]
        W = rig.matrix_world
        por_clipe = {}
        for nome, quadros, _ in clipes:
            ag.usar_acao(rig, bpy.data.actions[rig.name + "|" + nome])
            pior_d, pior_e = 0.0, 0.0
            for f in range(1, quadros + 2):
                bpy.context.scene.frame_set(f)
                pb = rig.pose.bones
                d = (W @ pb["hand.R"].matrix @ na_direita - W @ pb["vara"].matrix @ Vector((0, 0, 0))).length
                e = (W @ pb["hand.L"].matrix @ na_esquerda - W @ pb["manivela"].matrix @ punho).length
                pior_d, pior_e = max(pior_d, d), max(pior_e, e)
            por_clipe[nome] = {"mao_direita_cm": round(pior_d * 100, 2), "mao_esquerda_cm": round(pior_e * 100, 2)}
        rel[os.path.basename(caminho)] = por_clipe
    return rel


def folgas(passo=2):
    """Se algo atravessa a vara: no corpo (o Jogador_Corpo do original com as ações novas; a vara no osso "vara" do FBX
    novo), a menor folga de cada parte do corpo até a vara, menos as mãos (que seguram); na primeira pessoa, a menor
    distância do olho até a vara e a folga dos braços (menos as mãos)."""
    rel = {"corpo": {}, "fp": {}}
    ag.nova_cena()
    ag.importar(ag.FBX_CORPO)
    ro = bpy.data.objects[RIG_CORPO]
    corpo = bpy.data.objects["Jogador_Corpo"]
    for o in list(bpy.data.objects):
        if o not in (ro, corpo):
            bpy.data.objects.remove(o, do_unlink=True)
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    ro.name = "Original"
    rn = [o for o in ag.importar(SAIDA_CORPO) if o.type == "ARMATURE"][0]
    nomes = {g.index: g.name for g in corpo.vertex_groups}
    grupo = {}
    for v in corpo.data.vertices:
        if v.groups:
            g = max(v.groups, key=lambda g: g.weight)
            grupo[v.index] = nomes[g.group]
    inv = osso_vara().inverted()
    for nome, quadros, _ in CLIPES_CORPO:
        acao = bpy.data.actions[RIG_CORPO + "|" + nome]
        ag.usar_acao(ro, acao)
        ag.usar_acao(rn, acao)
        piores = {}
        for f in range(1, quadros + 2, passo):
            bpy.context.scene.frame_set(f)
            Mv = rn.matrix_world @ rn.pose.bones["vara"].matrix @ inv
            dg = bpy.context.evaluated_depsgraph_get()
            ev = corpo.evaluated_get(dg)
            me = ev.to_mesh()
            pts = [(grupo.get(v.index, "?"), ev.matrix_world @ v.co) for v in me.vertices
                   if grupo.get(v.index) not in MAOS_E_DEDOS]
            ev.to_mesh_clear()
            por_parte = {}
            for g, p in pts:
                por_parte.setdefault(g, []).append((g, p))
            for g, lst in por_parte.items():
                fo = _folga_da_vara(lst, Mv)
                if fo[0] < piores.get(g, (9.0,))[0]:
                    piores[g] = (round(fo[0] * 100, 1), f, fo[2] if isinstance(fo[2], str) else round(fo[2], 2))
        rel["corpo"][nome] = {g: v for g, v in sorted(piores.items(), key=lambda x: x[1][0])[:6]}
    # a primeira pessoa
    ag.nova_cena()
    novos = ag.importar(SAIDA_FP)
    rig = [o for o in novos if o.type == "ARMATURE"][0]
    malha = [o for o in novos if o.type == "MESH"][0]
    nomes = {g.index: g.name for g in malha.vertex_groups}
    grupo = {}
    for v in malha.data.vertices:
        if v.groups:
            grupo[v.index] = nomes[max(v.groups, key=lambda g: g.weight).group]
    for nome, quadros, _ in CLIPES_FP:
        ag.usar_acao(rig, bpy.data.actions[rig.name + "|" + nome])
        olho, braco_ = (9.0, None), {}
        for f in range(1, quadros + 2, passo):
            bpy.context.scene.frame_set(f)
            Mv = rig.matrix_world @ rig.pose.bones["vara"].matrix @ inv
            d, t = _distancia_ao_segmento(Vector((0, 0, 0)), Mv @ Vector((0, 0, FIM)), Mv @ Vector((0, 0, PONTA_Z)))
            if d < olho[0]:
                olho = (round(d, 3), f)
            dg = bpy.context.evaluated_depsgraph_get()
            ev = malha.evaluated_get(dg)
            me = ev.to_mesh()
            pts = [(grupo.get(v.index), ev.matrix_world @ v.co) for v in me.vertices
                   if grupo.get(v.index) in ("upper_arm.L", "forearm.L", "upper_arm.R", "forearm.R")]
            ev.to_mesh_clear()
            for g in ("upper_arm.L", "forearm.L", "upper_arm.R", "forearm.R"):
                fo = _folga_da_vara([x for x in pts if x[0] == g], Mv)
                if fo[1] and fo[0] < braco_.get(g, (9.0,))[0]:
                    braco_[g] = (round(fo[0] * 100, 1), f, fo[2] if isinstance(fo[2], str) else round(fo[2], 2))
        rel["fp"][nome] = {"olho_ate_a_vara_m": olho, "bracos_folga_cm": braco_}
    return rel


# ================================================================================================ as fotos

def _paleta_e_textura():
    ag._textura_no_material("PrincipalBloco", ag.TEXTURA)
    for nome in ("Paleta", "Pistola"):
        ag._textura_no_material(nome, ag.PALETA)


def _bolinha(nome, raio, pos, cor=(1.0, 0.85, 0.1, 1)):
    import bmesh
    me = bpy.data.meshes.new(nome)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=2, radius=raio)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(nome, me)
    ob.location = pos
    bpy.context.scene.collection.objects.link(ob)
    mat = bpy.data.materials.new(nome)
    mat.diffuse_color = cor
    me.materials.append(mat)
    return ob


def fotos_vara(pasta):
    """A vara sozinha (a Vara.fbx importada de volta): inteira e a carretilha de perto, dos dois lados e de cima."""
    ag.nova_cena()
    vara, man, ponta = importar_vara()
    _paleta_e_textura()
    ag._render_base(960, 540)
    bpy.context.scene.display.shading.show_cavity = True
    zc = Z_CARRETILHA
    vistas = {"inteira": ((1.3, 0.6, 0.9), (0.0, -0.75, 0.0), 30),
              "carretilha_esquerda": ((0.42, -0.08, 0.16), (0.02, -zc, 0.03), 50),
              "carretilha_direita": ((-0.42, -0.10, 0.16), (-0.01, -zc, 0.03), 50),
              "carretilha_cima": ((0.10, 0.10, 0.45), (0.01, -zc, 0.03), 50),
              "ponta": ((0.25, -1.45, 0.12), (0.0, -1.62, 0.0), 50)}
    saidas = []
    for nome, (pos, alvo, lente) in vistas.items():
        ag._camera("Foto", pos, alvo=alvo, lente=lente)
        c = os.path.join(pasta, f"Vara_{nome}.png")
        ag._salvar(c)
        saidas.append(c)
    return saidas


def _quadros(nome, quadros):
    return ag.quadros_das_fotos(quadros, 9 if "Lancar" in nome else 6)


def fotos_fp(pasta):
    """Os clipes da primeira pessoa (o FBX novo, importado de volta) pelo olho, a 55 graus, e de lado (do lado direito,
    com uma bolinha amarela no olho)."""
    ag.nova_cena()
    novos = ag.importar(SAIDA_FP)
    rig = [o for o in novos if o.type == "ARMATURE"][0]
    _paleta_e_textura()
    ag._render_base(480, 270)
    olho = _bolinha("MarcaDoOlho", 0.025, (0, 0, 0))
    saidas = []
    for nome, quadros, _ in CLIPES_FP:
        ag.usar_acao(rig, bpy.data.actions[rig.name + "|" + nome])
        for vista in ("olho", "lado"):
            if vista == "olho":
                ag._camera("Olho", (0, 0, 0), rot=(math.radians(90), 0, math.radians(180)), angulo_y=55)
            else:
                ag._camera("Lado", (-3.6, -0.8, 0.25), alvo=(0, -0.8, 0.25), lente=40)
            olho.hide_render = vista == "olho"
            for f in _quadros(nome, quadros):
                bpy.context.scene.frame_set(f)
                c = os.path.join(pasta, f"FPVara_{nome}_{vista}_{f:03d}.png")
                ag._salvar(c)
                saidas.append(c)
    return saidas


def fotos_perto(pasta):
    """As mãos na vara de perto (a primeira pessoa): a direita no cabo e a esquerda na manivela, no Idle e no Recolher."""
    ag.nova_cena()
    novos = ag.importar(SAIDA_FP)
    rig = [o for o in novos if o.type == "ARMATURE"][0]
    _paleta_e_textura()
    ag._render_base(480, 360)
    saidas = []
    for nome, quadros in (("Idle", 60), ("Recolher", 15)):
        ag.usar_acao(rig, bpy.data.actions[rig.name + "|" + nome])
        for f in ag.quadros_das_fotos(quadros, 6 if nome == "Recolher" else 1):
            bpy.context.scene.frame_set(f)
            Mv = rig.matrix_world @ rig.pose.bones["vara"].matrix @ osso_vara().inverted()
            carr = Mv @ Vector((0.03, Y_CARRETEL, Z_CARRETILHA - 0.03))
            pegada = Mv @ Vector((0, 0, 0))
            for vista, pos in (("34", carr + Vector((0.30, 0.10, 0.10))), ("frente", carr + Vector((0.05, -0.36, 0.05))),
                               ("lado", pegada + Vector((-0.34, 0.02, 0.06)))):
                alvo = carr if vista != "lado" else pegada + Vector((0, -0.05, 0.02))
                ag._camera("Perto", pos, alvo=alvo, lente=50)
                c = os.path.join(pasta, f"FPVara_{nome}Perto_{vista}_{f:03d}.png")
                ag._salvar(c)
                saidas.append(c)
    return saidas


def fotos_corpo(pasta):
    """Os clipes do corpo (o Jogador_Corpo do original com as ações novas, importadas de volta) de frente, de lado (do
    lado direito) e de 3/4, com a vara (a Vara.fbx) no osso "vara" do FBX novo e a manivela no osso "manivela"."""
    ag.nova_cena()
    ag.importar(ag.FBX_CORPO)
    ro = bpy.data.objects[RIG_CORPO]
    for o in list(bpy.data.objects):
        if o not in (ro,) and o.name != "Jogador_Corpo":
            bpy.data.objects.remove(o, do_unlink=True)
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    ro.name = "Original"
    rn = [o for o in ag.importar(SAIDA_CORPO) if o.type == "ARMATURE"][0]
    vara, man, ponta = importar_vara()
    bpy.data.objects.remove(ponta, do_unlink=True)
    # as malhas de volta nos eixos da vara (a Vara) e do osso da manivela (a Manivela)
    deitar = _deitar()
    vara.data.transform(deitar.inverted())
    man.data.transform((deitar @ osso_manivela()).inverted())
    _paleta_e_textura()
    ag._render_base(480, 360)
    inv_v, inv_m = osso_vara().inverted(), Matrix.Identity(4)
    # as vistas (o nome, a câmera, o alvo e a lente): o corpo inteiro e, de perto, o tronco e as mãos (de 3/4 pela
    # direita e pela esquerda, o lado da manivela); as de perto vão numa folha à parte (o clipe com "Perto"). A "Mao"
    # segue a carretilha (a câmera a este tanto dela, pela esquerda e pela frente): a mão esquerda na manivela.
    vistas = {("frente", ""): ((0.9, -5.2, 1.7), (-0.15, -0.5, 1.55), 40),
              ("lado", ""): ((-5.4, -0.6, 1.7), (0, -0.6, 1.55), 40),
              ("34", ""): ((-3.4, -4.0, 2.3), (-0.1, -0.5, 1.5), 40),
              ("34", "Perto"): ((-1.5, -1.7, 1.75), (-0.06, -0.25, 1.32), 45),
              ("frente", "Perto"): ((1.3, -1.9, 1.7), (-0.06, -0.25, 1.32), 45),
              ("frente", "Mao"): ((0.42, -0.30, 0.16), None, 50)}
    saidas = []
    for nome, quadros, _ in CLIPES_CORPO:
        acao = bpy.data.actions[RIG_CORPO + "|" + nome]
        ag.usar_acao(ro, acao)
        ag.usar_acao(rn, acao)
        for (vista, perto), (pos, alvo, lente) in vistas.items():
            if alvo is not None:
                ag._camera("Foto", pos, alvo=alvo, lente=lente)
            for f in _quadros(nome, quadros):
                bpy.context.scene.frame_set(f)
                vara.matrix_world = rn.matrix_world @ rn.pose.bones["vara"].matrix @ inv_v
                man.matrix_world = rn.matrix_world @ rn.pose.bones["manivela"].matrix @ inv_m
                bpy.context.view_layer.update()
                if alvo is None:
                    carr = vara.matrix_world @ Vector((0.03, Y_CARRETEL, Z_CARRETILHA))
                    ag._camera("Foto", carr + Vector(pos), alvo=carr, lente=lente)
                c = os.path.join(pasta, f"Corpo_{nome}{perto}_{vista}_{f:03d}.png")
                ag._salvar(c)
                saidas.append(c)
    return saidas


def etapa_fotos(pasta, quais=("Vara", "FP", "Perto", "Corpo")):
    os.makedirs(pasta, exist_ok=True)
    if "Vara" in quais:
        fotos_vara(pasta)
    if "FP" in quais:
        fotos_fp(pasta)
    if "Perto" in quais:
        fotos_perto(pasta)
    if "Corpo" in quais:
        fotos_corpo(pasta)


# ================================================================================================ main

def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["tudo"]
    etapa = args[0] if args else "tudo"
    pasta = args[1] if len(args) > 1 else None
    import json
    if etapa == "medir":
        print("MEDIR " + json.dumps(medir(), indent=1, default=str, ensure_ascii=False))
    if etapa in ("vara", "tudo"):
        etapa_vara()
    if etapa in ("fp", "tudo"):
        etapa_fp()
    if etapa in ("corpo", "tudo"):
        etapa_corpo()
    if etapa in ("conferir", "tudo"):
        rel = {"repouso": conferir_repouso(), "clipes": conferir_clipes(), "vara_na_mao": vara_na_mao(),
               "vara_fbx": eixos_da_vara_fbx(), "maos_na_vara": maos_na_vara_dos_fbx(), "alcances": medir(),
               "folgas": folgas()}
        print("CONFERIR " + json.dumps(rel, indent=1, default=str, ensure_ascii=False))
    if etapa in ("fotos", "tudo") and pasta:
        etapa_fotos(pasta)
    if etapa.startswith("fotos:") and pasta:     # fotos:Vara,FP,Perto,Corpo
        etapa_fotos(pasta, etapa.split(":", 1)[1].split(","))


if __name__ == "__main__":
    if bpy is None:
        if len(sys.argv) > 2 and sys.argv[1] == "folhas":
            ag.folhas(sys.argv[2])
        else:
            print("uso: py vara_animacoes.py folhas <pasta>")
    else:
        main()
