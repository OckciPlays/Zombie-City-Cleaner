# As animações do equilíbrio sem uma perna do Oco (31/10/2026, a pedido: "vamos fazer as animações pelo blender, faça bem
# detalhadas"): o levantar de bruços com uma perna só, apoiando os braços no chão (LevantarE: sem a perna esquerda;
# LevantarD: o espelho, sem a direita). Feitas no esqueleto do próprio Oco (Assets/_Game/Art/Characters/Zombie/Oco/
# Oco_Original.fbx), saem num FBX só (Assets/_Game/Art/Characters/Oco/Oco_AnimacoesEquilibrio.fbx) que a Unity importa
# como Humanoid e grava no esqueleto de cada Oco (MontagemDosZumbis.Equilibrio.cs), como os outros clipes.
#
# Como é feita: pose a pose (as chaves de LEVANTAR, mais abaixo), cada uma dizendo só o que muda. Cada canal é
# interpolado sozinho (uma curva cúbica sem passar do ponto, como as alças automáticas do Blender) e, a cada quadro, a
# pose sai dos canais: o quadril (posição e giro), o giro de cada osso no mundo (a coluna, o pescoço, a cabeça, os
# ombros, o coto e o pé) e, para os braços e a perna que sobrou, um IK de dois ossos feito aqui: onde fica a mão (ou o
# tornozelo) e para onde aponta o cotovelo (ou o joelho). Assim a mão e o pé ficam plantados no chão de verdade entre as
# chaves, e o quadro é gravado inteiro (todos os ossos, todos os quadros). Por cima, um tremor de esforço no tronco e na
# cabeça nas horas de força.
#
# O mundo do Blender: o Oco olha para -Y; +X é a esquerda dele; Z para cima; o chão em Z = 0. Os giros são em graus e
# relativos à pose de repouso (a pose A), no mundo: (inclinar para a frente, tombar para a esquerda, virar para a
# esquerda), nessa ordem. O quadril termina de pé em cima da origem (o clipe é no lugar: o agente não anda).
#
# Como rodar (no Blender aberto):
#   import importlib, sys; sys.path.append(r"D:\Zombiegame\Zombie-City-Cleaner\Blender")
#   import equilibrio_oco as eq; importlib.reload(eq)
#   eq.preparar(); eq.construir(); eq.exportar()

import math
import os

import bpy
from mathutils import Matrix, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(AQUI)
FBX_DO_OCO = os.path.join(REPO, "Assets", "_Game", "Art", "Characters", "Zombie", "Oco", "Oco_Original.fbx")
FBX_SAIDA = os.path.join(REPO, "Assets", "_Game", "Art", "Characters", "Oco", "Oco_AnimacoesEquilibrio.fbx")
CENA = "EquilibrioOco"
FPS = 30
RIG = "OCO_Rig"


# ================================================================================================ a coreografia
#
# LevantarE: sem a perna esquerda, de bruços (a cabeça para -Y), até ficar de pé na direita. Canais:
#   hips (x, y, z) e hips.rot (p, r, y); <osso>: (p, r, y) no mundo (spine, spine_02, chest, neck, head, shoulder.L/.R,
#   thigh.L (o coto), foot.R); mao.L/.R (onde fica o pulso), dedos.L/.R (para onde vão os dedos), palma.L/.R (para onde a
#   palma olha), cotovelo.L/.R (para onde o cotovelo aponta); pe.R (o tornozelo) e joelho.R (para onde o joelho aponta).

def _coluna(sp, sp2, ch, nk, hd, r=0.0, y=0.0, rh=0.0, yh=0.0):
    """Os giros da coluna de uma vez: a inclinação de cada osso, com o mesmo tombo e a mesma virada (a cabeça à parte)."""
    return {"spine": (sp, r, y), "spine_02": (sp2, r, y), "chest": (ch, r, y), "neck": (nk, rh * 0.5, yh * 0.5),
            "head": (hd, rh, yh)}


CHAO = 0.045           # a altura do pulso com a mão espalmada no chão (os dedos ficam um pouco curvados no jogo)
JOELHO_MINIMO = 0.065  # a junta do joelho não desce disto (o joelho apoiado no chão)
BAIXO, CIMA = (0, 0, -1), (0, 0, 1)

# os apoios que ficam parados: cada um é repetido na chave em que o apoio começa e na em que termina (sem isso, a curva
# o levaria aos poucos até a próxima chave, escorregando pelo chão)
MAO_D_NO_CHAO = {"mao.R": (-0.29, -0.30, CHAO), "dedos.R": (0.15, -1, 0), "palma.R": BAIXO}
MAO_E_NO_CHAO = {"mao.L": (0.29, -0.28, CHAO), "dedos.L": (-0.15, -1, 0), "palma.L": BAIXO}
MAO_E_ESCORREGOU = {"mao.L": (0.43, -0.19, CHAO), "dedos.L": (0.5, -1, 0), "palma.L": BAIXO}
MAO_E_DE_NOVO = {"mao.L": (0.34, -0.25, CHAO), "dedos.L": (0.2, -1, 0), "palma.L": BAIXO}
PERNA_ESTICADA = {"pe.R": (-0.11, 1.19, 0.08), "joelho.R": BAIXO, "foot.R": (150, 0, 0)}
JOELHO_NO_CHAO = {"pe.R": (-0.11, 0.66, 0.07), "joelho.R": (-0.15, -0.3, -1), "foot.R": (155, 0, 0)}
PE_PLANTADO = {"pe.R": (-0.12, 0.06, 0.09), "joelho.R": (0, -1, 0.1), "foot.R": (0, 0, 4)}

LEVANTAR = [
    # 0: caído de bruços, o rosto virado para a direita (a bochecha esquerda no chão); o braço direito dobrado perto da
    # cabeça, o esquerdo largado ao lado do corpo com a palma para cima; a perna direita esticada para trás, o peito do
    # pé no chão; o coto largado
    (0, {"hips": (0.0, 0.33, 0.125), "hips.rot": (91, 0, 0),
         **_coluna(91, 92, 93, 86, 78, rh=72, yh=-6),
         "shoulder.L": (90, 0, 0), "shoulder.R": (90, 0, 0),
         "mao.R": (-0.30, -0.42, CHAO), "dedos.R": (0.25, -1, 0), "palma.R": BAIXO, "cotovelo.R": (-1, 0.3, 0.6),
         "mao.L": (0.24, 0.30, 0.05), "dedos.L": (0.1, 1, 0), "palma.L": CIMA, "cotovelo.L": (1, 0, 0.4),
         **PERNA_ESTICADA, "thigh.L": (95, 6, 10)}),
    # 7: um espasmo: a cabeça sai do chão, os ombros se encolhem
    (7, {**_coluna(91, 92, 92, 88, 80, rh=50, yh=-4), "shoulder.L": (85, 0, -6), "shoulder.R": (85, 0, 6)}),
    # 10: a mão esquerda sai de perto do quadril, girando no ar
    (10, {"mao.L": (0.38, 0.0, 0.10), "dedos.L": (0.9, -0.4, 0.1), "palma.L": (0.6, 0, -0.8), "cotovelo.L": (1, 0.5, 0.15)}),
    # 15: as mãos embaixo dos ombros (a direita veio arrastada), os cotovelos para cima; a cabeça de frente, rosnando
    # para o chão
    (15, {**MAO_D_NO_CHAO, "cotovelo.R": (-0.6, 1, 1), **MAO_E_NO_CHAO, "cotovelo.L": (0.6, 1, 1),
          **_coluna(90, 90, 89, 70, 62, rh=4), "shoulder.L": (88, 0, -10), "shoulder.R": (88, 0, 10),
          "thigh.L": (92, 2, 4)}),
    # 25: empurra: o peito sobe (a coluna em arco, o quadril no chão), a cabeça pendurada
    (25, {"hips": (0.0, 0.335, 0.13), "hips.rot": (88, 0, 0), **_coluna(80, 70, 60, 70, 75),
          "cotovelo.R": (-0.8, 1, 0.4), "cotovelo.L": (0.8, 1, 0.4)}),
    # 30: os braços falham: o peito despenca uns centímetros, a cabeça bate para baixo
    (30, {**_coluna(85, 80, 74, 84, 92, r=-3), "cotovelo.R": (-0.8, 1, 0.8), "cotovelo.L": (0.8, 1, 0.8)}),
    # 38: a perna ainda esticada para trás (só sai depois do empurrão)
    (38, {**PERNA_ESTICADA}),
    # 40: empurra de novo, até o fim: os braços quase retos, o tronco em arco, a cabeça erguida para a frente
    (40, {"hips": (0.0, 0.33, 0.14), "hips.rot": (84, 0, 0), **_coluna(72, 58, 48, 40, 30, r=2),
          "cotovelo.R": (-0.6, 1, 0.1), "cotovelo.L": (0.6, 1, 0.1), "shoulder.L": (80, 0, 0), "shoulder.R": (80, 0, 0)}),
    # 45: a perna de sapo: o joelho escorrega pelo chão para fora e para a frente (de bruços, não dá para dobrar o joelho
    # para baixo); o quadril começa a sair do chão
    (45, {"hips": (-0.01, 0.32, 0.20), "hips.rot": (84, -6, 0),
          "pe.R": (-0.26, 0.92, 0.07), "joelho.R": (-1, -0.5, -0.25), "foot.R": (150, 0, -25)}),
    # 52: o quadril sobe e vai para trás, o joelho direito vem para baixo do corpo (de quatro: as duas mãos e um joelho);
    # o coto balança no ar
    (52, {"hips": (-0.03, 0.24, 0.43), "hips.rot": (78, -4, 0), **_coluna(76, 72, 66, 58, 50, r=-3), **JOELHO_NO_CHAO,
          "thigh.L": (20, 10, 0), "cotovelo.R": (-0.5, 1, 0.1), "cotovelo.L": (0.5, 1, 0.1)}),
    (53, {**MAO_E_NO_CHAO}),
    # 58: sem o joelho esquerdo, o corpo tomba para a esquerda: a mão esquerda escorrega para fora e segura; a cabeça
    # chicoteia
    (58, {"hips": (0.02, 0.25, 0.41), "hips.rot": (80, 12, -4), **_coluna(78, 74, 70, 56, 40, r=14, rh=18),
          **MAO_E_ESCORREGOU, "cotovelo.L": (1, 0.6, 0.2), "thigh.L": (-10, 30, 0)}),
    (62, {**MAO_E_ESCORREGOU}),
    # 65: recuperado, o peso de volta para a direita
    (65, {"hips": (-0.04, 0.23, 0.44), "hips.rot": (79, -6, 2), **_coluna(78, 73, 66, 54, 44, r=-6, rh=-6), **JOELHO_NO_CHAO,
          "thigh.L": (25, 6, 0)}),
    # 68: a mão esquerda volta para baixo do ombro (sai um pouco do chão)
    (68, {"mao.L": (0.38, -0.23, CHAO + 0.05), "dedos.L": (0.3, -1, 0.1)}),
    (71, {**MAO_E_DE_NOVO, "cotovelo.L": (0.8, 0.8, 0.1)}),
    # 74: o peso nas mãos: o quadril sobe, o joelho sai do chão e o pé vem arrastando para a frente
    (74, {"hips": (-0.04, 0.22, 0.55), "hips.rot": (74, -4, 0), **_coluna(94, 98, 102, 76, 56, r=-4),
          "pe.R": (-0.11, 0.40, 0.15), "joelho.R": (0, -1, -0.6), "foot.R": (115, 0, 0), "thigh.L": (0, 4, 0)}),
    # 78: o pé passa raspando, a ponta para baixo
    (78, {"pe.R": (-0.12, 0.20, 0.19), "joelho.R": (0, -1, -0.1), "foot.R": (30, 0, 0)}),
    # 82: o pé plantado embaixo do corpo: agachado fundo numa perna, as mãos ainda no chão
    (82, {"hips": (-0.05, 0.20, 0.46), "hips.rot": (62, -6, 0), **_coluna(82, 80, 76, 56, 40, r=-5), **PE_PLANTADO,
          "thigh.L": (-20, 8, 0)}),
    (84, {**MAO_D_NO_CHAO}),
    # 90: empurra o chão: a mão direita sai primeiro e sobe para equilibrar; o tronco começa a subir
    (90, {"hips": (-0.05, 0.17, 0.52), "hips.rot": (58, -4, 0), **_coluna(84, 82, 78, 52, 36, r=-8),
          "mao.R": (-0.40, -0.26, 0.32), "dedos.R": (-0.4, -1, -0.2), "palma.R": (0, 0, -1), "cotovelo.R": (-1, 0.5, -0.2),
          **MAO_E_DE_NOVO, "joelho.R": (0, -1, 0.15)}),
    # 100: a mão esquerda empurra por último e sai; o quadril passa de meio caminho
    (100, {"hips": (-0.04, 0.10, 0.74), "hips.rot": (34, -3, 0), **_coluna(50, 46, 42, 30, 20, r=-4),
          "mao.L": (0.36, -0.22, 0.42), "dedos.L": (0.4, -1, 0.2), "palma.L": (-0.3, 0, -1), "cotovelo.L": (1, 0.6, -0.3),
          "mao.R": (-0.46, -0.10, 0.70), "dedos.R": (-0.7, -0.6, 0.2), "palma.R": (0, 0.2, -1), "cotovelo.R": (-0.6, 1, -0.6)}),
    # 111: quase de pé, os braços abertos
    (111, {"hips": (-0.03, 0.04, 0.88), "hips.rot": (16, -2, 0), **_coluna(26, 26, 26, 18, 10, r=-2),
           "mao.L": (0.48, -0.12, 0.92), "dedos.L": (0.8, -0.4, 0), "palma.L": (0, 0, -1), "cotovelo.L": (0.5, 1, -0.5),
           "mao.R": (-0.46, -0.08, 0.86), "dedos.R": (-0.8, -0.4, 0), "palma.R": (0, 0, -1), "cotovelo.R": (-0.5, 1, -0.5),
           "joelho.R": (0, -1, 0.05), "thigh.L": (-15, 6, 0)}),
    # 118: tomba para a esquerda (o lado do coto): o tronco se contorce para o outro lado, o braço esquerdo sobe
    (118, {"hips": (-0.01, 0.03, 0.90), "hips.rot": (12, 7, 3), **_coluna(20, 18, 14, 8, 4, r=-10, rh=-12, yh=8),
           "mao.L": (0.52, -0.05, 1.12), "dedos.L": (0.9, -0.2, 0.3), "cotovelo.L": (0.3, 1, -0.6),
           "mao.R": (-0.40, -0.18, 0.80), "thigh.L": (-5, 22, 0)}),
    # 125: volta, passa um pouco para a direita
    (125, {"hips": (-0.04, 0.02, 0.915), "hips.rot": (10, -4, -2), **_coluna(16, 18, 20, 12, 8, r=4, rh=8, yh=-6),
           "mao.L": (0.36, -0.14, 0.92), "mao.R": (-0.34, -0.20, 0.86), "thigh.L": (-12, 8, 0)}),
    # 134: firme, na postura de zumbi: curvado, os braços caídos para a frente, a cabeça projetada
    (134, {"hips": (-0.03, 0.02, 0.92), "hips.rot": (8, -1, 0), **_coluna(14, 20, 24, 10, 6, r=1, rh=3),
           "mao.L": (0.25, -0.14, 0.84), "dedos.L": (-0.1, -0.5, -1), "palma.L": (-1, 0, 0), "cotovelo.L": (0.4, 1, 0),
           "mao.R": (-0.25, -0.16, 0.84), "dedos.R": (0.1, -0.5, -1), "palma.R": (1, 0, 0), "cotovelo.R": (-0.4, 1, 0),
           "shoulder.L": (6, 0, 0), "shoulder.R": (6, 0, 0), "thigh.L": (-8, 5, 0), **PE_PLANTADO}),
]

# o tremor do esforço (graus): (quadro do começo, do pico, do fim, amplitude)
TREMOR = [(18, 26, 31, 1.6), (33, 38, 44, 2.2), (66, 74, 86, 1.4), (86, 93, 101, 1.8)]


# ================================================================================================ geometria

def rot_mundo(p=0.0, r=0.0, y=0.0):
    """Inclinar para a frente (em volta de +X: o alto vai para -Y), tombar para a esquerda (em volta de +Y: o alto vai
    para +X) e virar para a esquerda (em volta de +Z: a frente, -Y, vai para +X), nessa ordem."""
    return (Matrix.Rotation(math.radians(y), 3, "Z") @ Matrix.Rotation(math.radians(r), 3, "Y")
            @ Matrix.Rotation(math.radians(p), 3, "X"))


def moldura(direcao, segundo):
    """Uma base ortonormal: Y na direção, Z no 'segundo' (tirado o que vai na direção), X = Y × Z."""
    yv = Vector(direcao).normalized()
    zv = Vector(segundo)
    zv = zv - yv * zv.dot(yv)
    if zv.length < 1e-6:
        zv = Vector((0, 0, 1)) if abs(yv.z) < 0.9 else Vector((0, 1, 0))
        zv = zv - yv * zv.dot(yv)
    zv.normalize()
    xv = yv.cross(zv)
    return Matrix((xv, yv, zv)).transposed()


def ik2(a, alvo, polo, l1, l2):
    """O IK de dois ossos: onde fica a junta do meio e o lado para onde ela dobra (para o lado do polo)."""
    d_vec = alvo - a
    d = max(min(d_vec.length, (l1 + l2) * 0.9995), abs(l1 - l2) * 1.001 + 1e-4)
    n = d_vec.normalized()
    cosa = max(-1.0, min(1.0, (l1 * l1 + d * d - l2 * l2) / (2 * l1 * d)))
    u = Vector(polo) - n * n.dot(polo)
    if u.length < 1e-6:
        u = Vector((0, 0, 1)) - n * n.z
    u.normalize()
    return a + n * (l1 * cosa) + u * (l1 * math.sqrt(max(0.0, 1 - cosa * cosa))), u, d_vec.length - (l1 + l2)


class Esqueleto:
    """O repouso dos ossos no mundo (a armadura fica na origem, sem giro nem escala)."""

    def __init__(self, arm):
        self.arm = arm
        ossos = arm.data.bones
        self.repouso = {b.name: arm.matrix_world @ b.matrix_local for b in ossos}
        self.pai = {b.name: (b.parent.name if b.parent else None) for b in ossos}
        prof = {b.name: len(b.parent_recursive) for b in ossos}
        self.ordem = sorted((b.name for b in ossos), key=lambda n: prof[n])
        self.comp = {}
        for b in ossos:
            filho = next((c for c in b.children if c.name.split(".")[0] in ("forearm", "hand", "shin", "foot")), None)
            self.comp[b.name] = ((self.repouso[filho.name].translation - self.repouso[b.name].translation).length
                                 if filho else b.length)
        # o lado para onde cada cadeia dobra no repouso (o cotovelo um pouco para trás, o joelho um pouco para a frente)
        self.dobra0 = {}
        for lado in "LR":
            for c, b, p in (("upper_arm", "forearm", "hand"), ("thigh", "shin", "foot")):
                a = self.repouso[f"{c}.{lado}"].translation
                m = self.repouso[f"{b}.{lado}"].translation
                f = self.repouso[f"{p}.{lado}"].translation
                n = (f - a).normalized()
                u = (m - a) - n * (m - a).dot(n)
                padrao = Vector((0, 1, 0)) if c == "upper_arm" else Vector((0, -1, 0))
                self.dobra0[c + "." + lado] = u.normalized() if u.length > 0.004 else padrao

    def eixo(self, nome, i):
        return self.repouso[nome].to_3x3().col[i].normalized()


# ================================================================================================ a pose de um quadro

def posar(esq, canais):
    """Os canais de um quadro viram a matriz de cada osso no mundo (e quanto a mão ou o pé ficaram longe do alvo)."""
    mundo, erros = {}, {}
    for nome in esq.ordem:
        if nome in mundo:
            continue
        rep = esq.repouso[nome]
        pai = esq.pai[nome]
        if pai is None:
            mundo[nome] = rep.copy()
            continue
        segue = mundo[pai] @ esq.repouso[pai].inverted() @ rep
        cab = segue.translation.copy()
        if nome == "hips":
            mundo[nome] = Matrix.Translation(Vector(canais["hips"])) @ (rot_mundo(*canais["hips.rot"]) @ rep.to_3x3()).to_4x4()
        elif nome in canais:
            mundo[nome] = Matrix.Translation(cab) @ (rot_mundo(*canais[nome]) @ rep.to_3x3()).to_4x4()
        else:
            mundo[nome] = segue
        # os braços e a perna: o IK resolve os dois ossos e põe a mão (o pé) no lugar
        base, lado = (nome.split(".") + [""])[:2]
        if base in ("upper_arm", "thigh"):
            alvo_canal = ("mao." if base == "upper_arm" else "pe.") + lado
            if alvo_canal not in canais:
                continue
            baixo, ponta = ("forearm", "hand") if base == "upper_arm" else ("shin", "foot")
            polo = Vector(canais[("cotovelo." if base == "upper_arm" else "joelho.") + lado])
            alvo = Vector(canais[alvo_canal])
            l1, l2 = esq.comp[nome], esq.comp[f"{baixo}.{lado}"]
            meio, u, erro = ik2(cab, alvo, polo, l1, l2)
            if base == "thigh" and meio.z < JOELHO_MINIMO:
                # o joelho não entra no chão: o plano da perna gira para fora (a perna de sapo) até ele caber
                eixo = (alvo - cab).normalized()
                fora = Vector((1 if lado == "L" else -1, 0, 0))
                for passo in range(1, 31):
                    for sinal in (1, -1):
                        g = Matrix.Rotation(math.radians(3 * passo * sinal), 3, eixo)
                        m2, u2, _ = ik2(cab, alvo, g @ u, l1, l2)
                        if m2.z >= JOELHO_MINIMO and (m2 - cab).dot(fora) > -0.02:
                            meio, u = m2, u2
                            break
                    else:
                        continue
                    break
            erros[alvo_canal] = erro
            rc, rb, rp = esq.repouso[nome], esq.repouso[f"{baixo}.{lado}"], esq.repouso[f"{ponta}.{lado}"]
            u0 = esq.dobra0[nome]
            d_c0 = rb.translation - rc.translation
            d_b0 = rp.translation - rb.translation
            # o osso de baixo termina onde o IK chegou (alvo, ou o mais perto que dá)
            fim = meio + (alvo - meio).normalized() * l2
            g_c = moldura(meio - cab, u) @ moldura(d_c0, u0).inverted()
            g_b = moldura(fim - meio, u) @ moldura(d_b0, u0).inverted()
            mundo[nome] = Matrix.Translation(cab) @ (g_c @ rc.to_3x3()).to_4x4()
            mundo[f"{baixo}.{lado}"] = Matrix.Translation(meio) @ (g_b @ rb.to_3x3()).to_4x4()
            nome_p = f"{ponta}.{lado}"
            if base == "upper_arm":
                # a mão: os dedos e a palma (no repouso, a palma olha para dentro, para o corpo)
                dedos0 = esq.eixo(nome_p, 1)
                palma0 = esq.eixo(nome_p, 0) * (1 if lado == "L" else -1)
                g_p = moldura(canais["dedos." + lado], canais["palma." + lado]) @ moldura(dedos0, palma0).inverted()
            elif nome_p in canais:
                g_p = rot_mundo(*canais[nome_p])
            else:
                g_p = g_b
            mundo[nome_p] = Matrix.Translation(fim) @ (g_p @ rp.to_3x3()).to_4x4()
    return mundo, erros


def aplicar(esq, mundo, quadro, anteriores):
    """As matrizes no mundo viram os giros locais dos ossos (e a posição do quadril), com chave no quadro."""
    arm = esq.arm
    for nome in esq.ordem:
        pai = esq.pai[nome]
        if pai is None:
            continue
        rep = esq.repouso[nome]
        local = (mundo[pai] @ esq.repouso[pai].inverted() @ rep).inverted() @ mundo[nome]
        pb = arm.pose.bones[nome]
        pb.rotation_mode = "QUATERNION"
        q = local.to_quaternion()
        ant = anteriores.get(nome)
        if ant is not None and ant.dot(q) < 0:
            q.negate()   # no mesmo hemisfério da chave anterior (a interpolação não dá a volta comprida)
        anteriores[nome] = q.copy()
        pb.rotation_quaternion = q
        pb.keyframe_insert("rotation_quaternion", frame=quadro, group=nome)
        if nome == "hips":
            pb.location = local.translation
            pb.keyframe_insert("location", frame=quadro, group=nome)


# ================================================================================================ os canais no tempo

def _tangentes(ts, vs):
    """As tangentes de uma curva cúbica que não passa do ponto (Fritsch-Butland): zero nas pontas e nos extremos."""
    n = len(ts)
    m = [0.0] * n
    for k in range(1, n - 1):
        h0, h1 = ts[k] - ts[k - 1], ts[k + 1] - ts[k]
        s0, s1 = (vs[k] - vs[k - 1]) / h0, (vs[k + 1] - vs[k]) / h1
        if s0 * s1 <= 0:
            continue
        m[k] = 3 * (h0 + h1) / ((2 * h1 + h0) / s0 + (h1 + 2 * h0) / s1)
    return m


def _curva(ts, vs, ms, f):
    if f <= ts[0]:
        return vs[0]
    if f >= ts[-1]:
        return vs[-1]
    k = max(i for i in range(len(ts) - 1) if ts[i] <= f)
    h = ts[k + 1] - ts[k]
    t = (f - ts[k]) / h
    t2, t3 = t * t, t * t * t
    return ((2 * t3 - 3 * t2 + 1) * vs[k] + (t3 - 2 * t2 + t) * h * ms[k] + (-2 * t3 + 3 * t2) * vs[k + 1]
            + (t3 - t2) * h * ms[k + 1])


class Linha:
    """Os canais de uma animação: cada um com as suas chaves, interpolado sozinho."""

    def __init__(self, chaves):
        porCanal = {}
        for quadro, poses in chaves:
            for canal, valor in poses.items():
                porCanal.setdefault(canal, []).append((quadro, tuple(float(v) for v in valor)))
        self.curvas = {}
        for canal, ks in porCanal.items():
            ts = [k[0] for k in ks]
            comps = []
            for i in range(len(ks[0][1])):
                vs = [k[1][i] for k in ks]
                comps.append((vs, _tangentes(ts, vs)))
            self.curvas[canal] = (ts, comps)
        self.fim = max(q for q, _ in chaves)

    def no_quadro(self, f):
        saida = {}
        for canal, (ts, comps) in self.curvas.items():
            saida[canal] = tuple(_curva(ts, vs, ms, f) for vs, ms in comps)
        return saida


def tremor(canais, f):
    """O tremor do esforço: o tronco, o pescoço e a cabeça tremem (duas senoides) nas horas de força."""
    amp = 0.0
    for ini, pico, fim, a in TREMOR:
        if ini <= f <= fim:
            k = (f - ini) / max(1e-3, pico - ini) if f <= pico else (fim - f) / max(1e-3, fim - pico)
            amp = max(amp, a * (k * k * (3 - 2 * k)))
    if amp <= 0:
        return canais
    t = f / FPS
    for i, osso in enumerate(("chest", "neck", "head")):
        if osso in canais:
            p, r, y = canais[osso]
            fase = 1.7 * i
            canais[osso] = (p + amp * (math.sin(2 * math.pi * 9.0 * t + fase) + 0.5 * math.sin(2 * math.pi * 13.7 * t + 2 * fase)),
                            r + amp * 0.6 * math.sin(2 * math.pi * 7.3 * t + 0.7 + fase), y)
    return canais


def espelhar(canais):
    """O espelho em X (a esquerda vira a direita): as posições e as direções trocam o sinal do X; os giros, o do tombo e
    o da virada; os nomes .L e .R se trocam."""
    saida = {}
    for canal, v in canais.items():
        nome = canal[:-2] + (".R" if canal.endswith(".L") else ".L") if canal[-2:] in (".L", ".R") else canal
        if canal in ("hips",) or canal.split(".")[0] in ("mao", "dedos", "palma", "cotovelo", "pe", "joelho"):
            saida[nome] = (-v[0], v[1], v[2])
        else:
            saida[nome] = (v[0], -v[1], -v[2])
    return saida


# ================================================================================================ a cena

def preparar():
    """O Oco importado numa cena própria (a de antes, se houver, é refeita)."""
    if CENA in bpy.data.scenes:
        velha = bpy.data.scenes[CENA]
        for o in list(velha.objects):
            bpy.data.objects.remove(o, do_unlink=True)
    cena = bpy.data.scenes.get(CENA) or bpy.data.scenes.new(CENA)
    bpy.context.window.scene = cena
    bpy.ops.import_scene.fbx(filepath=FBX_DO_OCO, automatic_bone_orientation=False)
    cena.render.fps = FPS
    arm = armadura()
    arm.name = RIG
    arm.animation_data_create()
    return arm


def armadura():
    return next(o for o in bpy.data.scenes[CENA].objects if o.type == "ARMATURE")


def malha():
    return next(o for o in bpy.data.scenes[CENA].objects if o.type == "MESH" and o.parent and o.parent.type == "ARMATURE")


def construir(so=None):
    """As ações Zombie_LevantarE e Zombie_LevantarD, gravadas quadro a quadro, nas faixas do NLA (como a exportação quer)."""
    arm = armadura()
    esq = Esqueleto(arm)
    linha = Linha(LEVANTAR)
    ad = arm.animation_data or arm.animation_data_create()
    for t in list(ad.nla_tracks):
        ad.nla_tracks.remove(t)
    relatorio = {}
    for nome, espelho in (("LevantarE", False), ("LevantarD", True)):
        if so and nome != so:
            continue
        acao_nome = "Zombie_" + nome
        if acao_nome in bpy.data.actions:
            bpy.data.actions.remove(bpy.data.actions[acao_nome])
        acao = bpy.data.actions.new(acao_nome)
        acao.use_fake_user = True
        ad.action = acao
        anteriores, piores = {}, {}
        for f in range(0, linha.fim + 1):
            canais = tremor(linha.no_quadro(f), f)
            if espelho:
                canais = espelhar(canais)
            mundo, erros = posar(esq, canais)
            for k, e in erros.items():
                if e > piores.get(k, (-9, 0))[0]:
                    piores[k] = (e, f)
            aplicar(esq, mundo, f, anteriores)
        relatorio[nome] = {k: (round(e, 3), f) for k, (e, f) in piores.items()}
    # as faixas do NLA, com o nome que a Unity procura ("...|Zombie_LevantarE")
    ad.action = None
    for nome in ("LevantarE", "LevantarD"):
        acao = bpy.data.actions.get("Zombie_" + nome)
        if acao is None:
            continue
        tr = ad.nla_tracks.new()
        tr.name = nome
        s = tr.strips.new(RIG + "|Zombie_" + nome, 0, acao)
        s.name = RIG + "|Zombie_" + nome
        if hasattr(s, "action_slot") and len(acao.slots):
            s.action_slot = acao.slots[0]
        tr.mute = True
    cena = bpy.data.scenes[CENA]
    cena.frame_start, cena.frame_end = 0, linha.fim
    mostrar(so or "LevantarE")
    return relatorio


def mostrar(nome="LevantarE"):
    """Deixa uma ação ativa na armadura para olhar (as faixas do NLA ficam mudas)."""
    arm = armadura()
    arm.animation_data.action = bpy.data.actions["Zombie_" + nome]
    acao = arm.animation_data.action
    if hasattr(arm.animation_data, "action_slot") and len(acao.slots):
        arm.animation_data.action_slot = acao.slots[0]


def contatos(quadros, nome="LevantarE"):
    """A altura mínima de cada parte da malha em alguns quadros (o que encosta no chão e o que afunda nele)."""
    mostrar(nome)
    me = malha()
    grupos = {g.index: g.name for g in me.vertex_groups}
    dono = []
    for v in me.data.vertices:
        melhor = max(v.groups, key=lambda g: g.weight, default=None)
        dono.append(grupos[melhor.group] if melhor else "")
    partes = {"cabeca": ("head", "neck"), "peito": ("chest", "spine_02", "spine"), "quadril": ("hips",),
              "maoE": ("hand.L",), "maoD": ("hand.R",), "joelhoD": ("shin.R",), "peD": ("foot.R", "toe.R"),
              "coxaD": ("thigh.R",), "coto": ("thigh.L",), "antebracoE": ("forearm.L",), "antebracoD": ("forearm.R",)}
    cena = bpy.data.scenes[CENA]
    saida = {}
    # (a máscara da prévia, que esconde a perna cortada, muda os índices dos vértices)
    mascaras = [m for m in me.modifiers if m.type == "MASK" and m.show_viewport]
    for m in mascaras:
        m.show_viewport = False
    for f in quadros:
        cena.frame_set(f)
        dg = bpy.context.evaluated_depsgraph_get()
        ev = me.evaluated_get(dg)
        m = ev.to_mesh()
        mw = me.matrix_world
        menor = {}
        for i, v in enumerate(m.vertices):
            z = (mw @ v.co).z
            for p, ossos in partes.items():
                if dono[i] in ossos and z < menor.get(p, 9):
                    menor[p] = z
        ev.to_mesh_clear()
        saida[f] = {p: round(z, 3) for p, z in menor.items()}
    for m in mascaras:
        m.show_viewport = True
    return saida


def exportar():
    """O FBX com a armadura, a malha e as duas faixas (a Unity faz o avatar Humanoid dele)."""
    cena = bpy.data.scenes[CENA]
    arm, me = armadura(), malha()
    # a prévia (a perna cortada escondida) não vai junto
    for m in [m for m in me.modifiers if m.type != "ARMATURE"]:
        me.modifiers.remove(m)
    for g in [g for g in me.vertex_groups if g.name.startswith("_")]:
        me.vertex_groups.remove(g)
    ad = arm.animation_data
    ad.action = None
    for t in ad.nla_tracks:
        t.mute = False
    bpy.context.window.scene = cena
    cena.frame_set(0)
    bpy.context.view_layer.update()
    for o in list(cena.objects):
        o.select_set(False)
    arm.select_set(True)
    me.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(filepath=FBX_SAIDA, use_selection=True, object_types={"ARMATURE", "MESH"},
                             apply_scale_options="FBX_SCALE_ALL", add_leaf_bones=False, bake_anim=True,
                             bake_anim_use_nla_strips=True, bake_anim_use_all_actions=False,
                             bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)
    for t in ad.nla_tracks:
        t.mute = True
    return FBX_SAIDA
