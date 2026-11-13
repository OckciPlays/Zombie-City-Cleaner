# A fauna: a família de cervos low-poly (o macho, a fêmea e o filhote).
#
# O pedido do Pedro (12/11/2026): low-poly com realismo estilizado, as faces planas à mostra e as transições angulosas,
# os materiais separados por região e a variação de tom de face para face. A referência é a imagem da família que ele
# mandou (D:\Zombiegame\Trabalho-Claude\fauna\refs\familia_lowpoly.webp): as medidas abaixo saíram dela, parte por parte
# (o tronco fundo, a garupa redonda com a cauda em aba no alto, as pernas grossas com o joelho, o boleto e o jarrete
# marcados, os cascos grandes e fendidos, a cabeça curta e funda com o nariz grande e as narinas, o olho amendoado com a
# pálpebra, as orelhas grandes e a galhada grossa e facetada).
#
# As medidas são as do macho (~1 m na cernelha), em (f, s, z) (veja o formas.py). A fêmea e o filhote saem do macho
# pelas proporções de cada um.

import math

import numpy as np

from formas import Anel, P, fbm, tubo, elipsoide

# o tronco: (f, alto, baixo, meia largura, quilha), do peito à garupa
TRONCO = [
    (0.585, 0.885, 0.680, 0.098, 0.30),
    (0.48, 0.972, 0.585, 0.148, 0.42),
    (0.32, 1.000, 0.560, 0.162, 0.45),
    (0.15, 0.982, 0.565, 0.172, 0.42),
    (0.00, 0.968, 0.585, 0.176, 0.34),
    (-0.15, 0.965, 0.605, 0.170, 0.28),
    (-0.28, 0.972, 0.635, 0.160, 0.20),
    (-0.40, 0.985, 0.655, 0.154, 0.12),
    (-0.50, 0.975, 0.675, 0.142, 0.05),
    (-0.58, 0.935, 0.705, 0.112, 0.0),
    (-0.632, 0.875, 0.735, 0.062, 0.0),
]

# As proporções de cada animal (relativas ao macho). tris = os triângulos do corpo (sem as peças à parte).
VARIANTES = {
    "macho": dict(A=1.00, pernas=1.00, corpo=0.90, corpo_r=1.00, pescoco=1.24, pescoco_comp=0.93, cabeca=1.10,
                  orelha=1.12, olho=1.00, pernas_r=1.00, galhada=True, manchas=False, tris=7000, focinho=0.86,
                  testa=1.0),
    "femea": dict(A=0.87, pernas=1.03, corpo=0.88, corpo_r=0.95, pescoco=0.80, pescoco_comp=1.05, cabeca=1.10,
                  orelha=1.15, olho=1.05, pernas_r=0.92, galhada=False, manchas=False, tris=6000, focinho=0.94,
                  testa=1.0),
    "filhote": dict(A=0.58, pernas=0.96, corpo=0.82, corpo_r=0.97, pescoco=0.92, pescoco_comp=0.86, cabeca=1.42,
                    orelha=1.40, olho=1.32, pernas_r=1.00, galhada=False, manchas=True, tris=5000, focinho=0.82,
                    testa=1.10),
}

# Os materiais (o nome no Blender, a cor sRGB, a aspereza): os mesmos para os três; o principal do filhote é mais claro
MATERIAIS = [
    ("Cervo_Pelagem_Principal", (0.50, 0.275, 0.135), 0.92),
    ("Cervo_Pelagem_Escura", (0.33, 0.17, 0.085), 0.92),
    ("Cervo_Pelagem_Clara", (0.74, 0.55, 0.35), 0.92),
    ("Cervo_Regiao_Inferior", (0.90, 0.82, 0.69), 0.92),
    ("Cervo_Focinho", (0.17, 0.10, 0.10), 0.45),
    ("Cervo_Olhos", (0.085, 0.045, 0.028), 0.12),
    ("Cervo_Cascos", (0.11, 0.07, 0.05), 0.60),
    ("Cervo_Galhadas", (0.68, 0.52, 0.36), 0.75),
    ("Cervo_Manchas_Filhote", (0.90, 0.82, 0.68), 0.92),
    ("Cervo_Filhote_Pelagem", (0.64, 0.39, 0.20), 0.92),
]
M = {nome: i for i, (nome, _, _) in enumerate(MATERIAIS)}

ZB = 0.55            # a altura da "dobra" das pernas: o que está abaixo estica com as pernas, o resto sobe junto
POLO = (0.71, 1.31)  # a nuca: o centro da escala da cabeça
BASE_PESCOCO = (0.46, 0.84)
OLHO_F = 0.78        # à frente disto é o focinho (encolhe no filhote)
CABECA_DESLOCA = (0.030, -0.070)   # (f, z): a cabeça e o alto do pescoço
INCLINA_CABECA = math.radians(8.0)  # o nariz para baixo, em volta da nuca


class Cervo:
    def __init__(self, variante="macho"):
        self.nome = variante
        self.v = VARIANTES[variante]

    # -- as transformações da variante --
    def _pescoco_curto(self, f, z, grupo):
        pc = self.v["pescoco_comp"]
        if pc == 1.0 or grupo not in ("pescoco", "cabeca"):
            return f, z
        bf, bz = BASE_PESCOCO
        if grupo == "pescoco":
            if z > bz:
                return bf + (f - bf) * pc, bz + (z - bz) * pc
            return f, z
        return f + (POLO[0] - bf) * (pc - 1.0), z + (POLO[1] - bz) * (pc - 1.0)

    def _focinho(self, f, z):
        """A cara à frente dos olhos encolhe (o filhote) ao longo do eixo da cabeça."""
        fo = self.v["focinho"]
        if f > OLHO_F and fo != 1.0:
            k = (f - OLHO_F) * (fo - 1.0)
            return f + k, z - k * 0.60
        return f, z

    def ponto(self, f, s, z, grupo="corpo"):
        v = self.v
        if grupo == "cabeca":
            f, z = self._focinho(f, z)
            f = POLO[0] + (f - POLO[0]) * v["cabeca"]
            z = POLO[1] + (z - POLO[1]) * v["cabeca"]
            s = s * v["cabeca"]
            # a cabeça inclinada com o nariz um pouco mais para baixo (como na referência)
            df, dz = f - POLO[0], z - POLO[1]
            ca, sa = math.cos(INCLINA_CABECA), math.sin(INCLINA_CABECA)
            f, z = POLO[0] + df * ca + dz * sa, POLO[1] - df * sa + dz * ca
        f, z = self._pescoco_curto(f, z, grupo)
        # a cabeça mais baixa e à frente, como na referência (a nuca a ~1,27 da cernelha): o alto do pescoço acompanha
        if grupo == "cabeca":
            f, z = f + CABECA_DESLOCA[0], z + CABECA_DESLOCA[1]
        elif grupo == "pescoco":
            t = min(max((z - BASE_PESCOCO[1]) / (POLO[1] - BASE_PESCOCO[1]), 0.0), 1.0)
            f, z = f + CABECA_DESLOCA[0] * t, z + CABECA_DESLOCA[1] * t
        if grupo in ("cabeca", "pescoco"):
            f = f + (v["corpo"] - 1.0) * 0.46
        else:
            f = f * v["corpo"]
        if grupo in ("corpo", "cauda", "perna"):
            s = s * v["corpo_r"]
        z = z * v["pernas"] if z < ZB else z + ZB * (v["pernas"] - 1.0)
        return P(f * v["A"], s * v["A"], z * v["A"])

    def raio(self, r, grupo="corpo"):
        v = self.v
        fator = {"corpo": v["corpo_r"], "pescoco": v["pescoco"], "cabeca": v["cabeca"], "perna": v["pernas_r"] * 1.08,
                 "perna_baixa": v["pernas_r"] * 1.12, "cauda": v["corpo_r"], "orelha": v["orelha"]}.get(grupo, 1.0)
        return r * v["A"] * fator

    def _testa(self, f, w, h):
        """A testa mais redonda do filhote (o crânio atrás dos olhos)."""
        te = self.v["testa"]
        return (w * te, h * te) if f <= OLHO_F else (w, h)

    # -- o esqueleto --
    def ossos(self):
        """(nome, cabeça, ponta, pai) de cada osso, no espaço do Blender."""
        p = self.ponto
        o = [
            ("Raiz", P(0, 0, 0), P(0, 0, 0) + (0, -0.30 * self.v["A"], 0), None),
            ("Quadril", p(-0.20, 0, 0.86), p(-0.06, 0, 0.87), "Raiz"),
            ("Pelve", p(-0.20, 0, 0.86), p(-0.52, 0, 0.88), "Quadril"),
            ("Coluna_1", p(-0.06, 0, 0.87), p(0.12, 0, 0.88), "Quadril"),
            ("Coluna_2", p(0.12, 0, 0.88), p(0.31, 0, 0.88), "Coluna_1"),
            ("Peito", p(0.31, 0, 0.88), p(0.48, 0, 0.86), "Coluna_2"),
            ("Pescoco_1", p(0.48, 0, 0.86, "pescoco"), p(0.60, 0, 1.07, "pescoco"), "Peito"),
            ("Pescoco_2", p(0.60, 0, 1.07, "pescoco"), p(0.705, 0, 1.29, "pescoco"), "Pescoco_1"),
            ("Cabeca", p(0.705, 0, 1.31, "cabeca"), p(0.975, 0, 1.17, "cabeca"), "Pescoco_2"),
            ("Cauda_1", p(-0.575, 0, 0.895, "cauda"), p(-0.628, 0, 0.835, "cauda"), "Pelve"),
            ("Cauda_2", p(-0.628, 0, 0.835, "cauda"), p(-0.700, 0, 0.655, "cauda"), "Cauda_1"),
        ]
        for lado, sx in (("L", 1.0), ("R", -1.0)):
            def q(f, s, z, g="perna"):
                return p(f, s * sx, z, g)
            o += [
                ("Orelha." + lado, q(0.712, 0.058, 1.372, "cabeca"), q(0.672, 0.180, 1.475, "cabeca"), "Cabeca"),
                ("Omoplata." + lado, q(0.38, 0.09, 0.94), q(0.45, 0.112, 0.75), "Peito"),
                ("Braco." + lado, q(0.45, 0.112, 0.75), q(0.30, 0.112, 0.60), "Omoplata." + lado),
                ("Antebraco." + lado, q(0.30, 0.112, 0.60), q(0.31, 0.111, 0.355), "Braco." + lado),
                ("Canela.D." + lado, q(0.31, 0.111, 0.355), q(0.322, 0.111, 0.108), "Antebraco." + lado),
                ("Casco.D." + lado, q(0.322, 0.111, 0.108), q(0.39, 0.111, 0.01), "Canela.D." + lado),
                ("Coxa." + lado, q(-0.40, 0.10, 0.86), q(-0.285, 0.116, 0.62), "Pelve"),
                ("Perna." + lado, q(-0.285, 0.116, 0.62), q(-0.525, 0.109, 0.448), "Coxa." + lado),
                ("Canela.T." + lado, q(-0.525, 0.109, 0.448), q(-0.455, 0.109, 0.108), "Perna." + lado),
                ("Casco.T." + lado, q(-0.452, 0.109, 0.108), q(-0.385, 0.109, 0.01), "Canela.T." + lado),
            ]
        return o

    # -- as peças do campo --
    def pecas(self):
        """Cada peça: dict(nome, malha=(verts, quads, tris), k=a união suave, ossos=[(osso, a, b)], menos=recorte)."""
        p, r = self.ponto, self.raio
        pecas = []

        def add(nome, malha, k, ossos, menos=False):
            pecas.append(dict(nome=nome, malha=malha, k=k, ossos=ossos, menos=menos))

        # o tronco
        aneis = [Anel(p(f, 0, (a + b) / 2), r(w * 0.96), r((a - b) / 2), p=2.3, qb=q) for f, a, b, w, q in TRONCO]
        add("tronco", tubo(aneis, lados=32, sub=4), 0.0,
            [("Pelve", p(-0.64, 0, 0.82), p(-0.20, 0, 0.84)), ("Coluna_1", p(-0.20, 0, 0.84), p(0.05, 0, 0.84)),
             ("Coluna_2", p(0.05, 0, 0.84), p(0.31, 0, 0.84)), ("Peito", p(0.31, 0, 0.84), p(0.64, 0, 0.82))])
        # o peito saliente entre as pernas da frente (a ponta do esterno)
        c = p(0.555, 0, 0.68)
        add("peito", elipsoide(c, r(0.070), r(0.065), r(0.075)), 0.06, [("Peito", c, c)])

        # o pescoço: (f, z, meia largura, meia fundura)
        g = "pescoco"
        pesc = [(0.44, 0.82, 0.140, 0.170), (0.52, 0.93, 0.126, 0.165), (0.59, 1.05, 0.108, 0.148),
                (0.645, 1.16, 0.094, 0.125), (0.69, 1.25, 0.080, 0.104), (0.715, 1.305, 0.068, 0.086)]
        aneis = [Anel(p(f, 0, z, g), r(w, g), r(d, g), p=2.1, qb=0.10) for f, z, w, d in pesc]
        add("pescoco", tubo(aneis, lados=26, sub=4), 0.075,
            [("Pescoco_1", p(0.44, 0, 0.82, g), p(0.60, 0, 1.07, g)),
             ("Pescoco_2", p(0.60, 0, 1.07, g), p(0.72, 0, 1.31, g))])

        # a cabeça curta e funda: (f, z, meia largura, meia altura)
        g = "cabeca"
        cab = [(0.688, 1.332, 0.054, 0.058), (0.728, 1.328, 0.071, 0.079), (0.772, 1.308, 0.074, 0.082),
               (0.818, 1.278, 0.064, 0.076), (0.858, 1.248, 0.055, 0.069), (0.894, 1.222, 0.049, 0.063),
               (0.922, 1.203, 0.045, 0.058), (0.940, 1.192, 0.039, 0.050)]
        aneis = []
        for f, z, w, h in cab:
            w, h = self._testa(f, w, h)
            aneis.append(Anel(p(f, 0, z, g), r(w, g), r(h, g), p=2.25, qb=0.28 if f < 0.86 else 0.12))
        add("cabeca", tubo(aneis, lados=26, sub=4), 0.03, [("Cabeca", p(0.688, 0, 1.33, g), p(0.98, 0, 1.17, g))])
        cab_o = [("Cabeca", p(0.70, 0, 1.33, g), p(0.98, 0, 1.17, g))]
        # o nariz grande e arredondado (o focinho preto), o lábio de baixo e o queixo
        c = p(0.952, 0, 1.196, g)
        add("nariz", elipsoide(c, r(0.041, g), r(0.036, g), r(0.039, g)), 0.016, cab_o)
        c = p(0.915, 0, 1.156, g)
        add("queixo", elipsoide(c, r(0.031, g), r(0.040, g), r(0.021, g)), 0.016, cab_o)
        # a mandíbula (a linha de baixo da cara)
        mand = [(0.735, 1.258, 0.050, 0.030), (0.80, 1.215, 0.042, 0.026), (0.86, 1.180, 0.034, 0.021),
                (0.905, 1.158, 0.026, 0.017)]
        aneis = [Anel(p(f, 0, z, g), r(w, g), r(h, g), p=2.0) for f, z, w, h in mand]
        add("mandibula", tubo(aneis, lados=16, sub=3), 0.02, cab_o)
        for sx in (1, -1):
            c = p(0.752, 0.046 * sx, 1.274, g)      # a bochecha
            add("bochecha", elipsoide(c, r(0.030, g), r(0.042, g), r(0.036, g)), 0.02, cab_o)
            c = p(0.785, 0.050 * sx, 1.345, g)      # a sobrancelha
            add("sobrancelha", elipsoide(c, r(0.024, g), r(0.034, g), r(0.015, g)), 0.016, cab_o)
            c = p(0.768, 0.0705 * sx, 1.316, g)     # a órbita do olho (um recorte)
            add("orbita", elipsoide(c, r(0.016, g), r(0.024, g), r(0.014, g)), 0.008, cab_o, menos=True)
            c = p(0.985, 0.0195 * sx, 1.190, g)     # a narina (um recorte)
            add("narina", elipsoide(c, r(0.007, g), r(0.012, g), r(0.006, g)), 0.004, cab_o, menos=True)

        # a cauda em aba, larga e chata, saindo do alto da garupa
        g = "cauda"
        cau = [(-0.575, 0.905, 0.056, 0.034), (-0.622, 0.868, 0.074, 0.034), (-0.660, 0.810, 0.066, 0.030),
               (-0.684, 0.748, 0.048, 0.024), (-0.696, 0.690, 0.030, 0.016), (-0.700, 0.655, 0.012, 0.010)]
        aneis = [Anel(p(f, 0, z, g), r(w, g), r(d, g), p=2.0) for f, z, w, d in cau]
        add("cauda", tubo(aneis, lados=18, sub=3), 0.022,
            [("Cauda_1", p(-0.575, 0, 0.895, g), p(-0.628, 0, 0.835, g)),
             ("Cauda_2", p(-0.628, 0, 0.835, g), p(-0.700, 0, 0.655, g))])

        # as pernas
        g = "perna"
        gb = "perna_baixa"
        for lado, sx in (("L", 1.0), ("R", -1.0)):
            def q(f, s, z):
                return p(f, s * sx, z, g)

            # a frente: o ombro, o antebraço, o joelho, a canela, o boleto e a quartela
            om = [(0.38, 0.082, 0.92, 0.040, 0.072), (0.425, 0.096, 0.83, 0.054, 0.086),
                  (0.435, 0.104, 0.74, 0.060, 0.084), (0.37, 0.108, 0.66, 0.060, 0.080),
                  (0.33, 0.110, 0.61, 0.056, 0.070)]
            aneis = [Anel(q(f, s, z), r(a, g), r(b, g), p=2.1) for f, s, z, a, b in om]
            add("ombro", tubo(aneis, lados=22, sub=3), 0.075,
                [("Omoplata." + lado, q(0.38, 0.09, 0.95), q(0.45, 0.112, 0.75)),
                 ("Braco." + lado, q(0.45, 0.112, 0.75), q(0.30, 0.112, 0.60))])
            ab = [(0.318, 0.111, 0.625, 0.070, 0.084), (0.311, 0.112, 0.52, 0.056, 0.068),
                  (0.309, 0.111, 0.41, 0.038, 0.044), (0.310, 0.111, 0.355, 0.040, 0.045),
                  (0.312, 0.111, 0.31, 0.032, 0.037), (0.316, 0.111, 0.21, 0.027, 0.032),
                  (0.320, 0.111, 0.14, 0.027, 0.032), (0.324, 0.111, 0.108, 0.032, 0.037),
                  (0.338, 0.111, 0.074, 0.028, 0.030)]
            aneis = [Anel(q(f, s, z), r(a, gb), r(b, gb), p=2.0) for f, s, z, a, b in ab]
            add("mao", tubo(aneis, lados=18, sub=3), 0.018,
                [("Antebraco." + lado, q(0.30, 0.112, 0.63), q(0.31, 0.111, 0.355)),
                 ("Canela.D." + lado, q(0.31, 0.111, 0.355), q(0.322, 0.111, 0.108)),
                 ("Casco.D." + lado, q(0.322, 0.111, 0.108), q(0.35, 0.111, 0.05))])
            c = q(0.268, 0.112, 0.600)  # o cotovelo
            add("cotovelo", elipsoide(c, r(0.032, gb), r(0.034, gb), r(0.036, gb)), 0.015, [("Braco." + lado, c, c)])
            c = q(0.313, 0.111, 0.358)  # o joelho da frente (o carpo)
            add("joelho", elipsoide(c, r(0.040, gb), r(0.044, gb), r(0.034, gb)), 0.010,
                [("Antebraco." + lado, c, c)])

            # atrás: o pernil, a perna, o jarrete (bem marcado e para trás), a canela, o boleto e a quartela
            cx = [(-0.41, 0.076, 0.94, 0.052, 0.120), (-0.42, 0.094, 0.86, 0.080, 0.165),
                  (-0.40, 0.104, 0.77, 0.084, 0.160), (-0.355, 0.110, 0.69, 0.074, 0.125),
                  (-0.30, 0.114, 0.625, 0.062, 0.088)]
            aneis = [Anel(q(f, s, z), r(a, g), r(b, g), p=2.1) for f, s, z, a, b in cx]
            add("coxa", tubo(aneis, lados=22, sub=3), 0.06,
                [("Coxa." + lado, q(-0.41, 0.10, 0.95), q(-0.285, 0.116, 0.62))])
            pt = [(-0.30, 0.116, 0.630, 0.074, 0.104), (-0.38, 0.116, 0.555, 0.064, 0.086),
                  (-0.47, 0.111, 0.485, 0.045, 0.058), (-0.525, 0.109, 0.448, 0.040, 0.051),
                  (-0.522, 0.109, 0.408, 0.032, 0.039), (-0.492, 0.109, 0.26, 0.027, 0.032),
                  (-0.462, 0.109, 0.14, 0.027, 0.032), (-0.455, 0.109, 0.108, 0.032, 0.037),
                  (-0.437, 0.109, 0.074, 0.028, 0.030)]
            aneis = [Anel(q(f, s, z), r(a, gb), r(b, gb), p=2.0) for f, s, z, a, b in pt]
            add("pe", tubo(aneis, lados=18, sub=3), 0.022,
                [("Perna." + lado, q(-0.285, 0.116, 0.62), q(-0.525, 0.109, 0.448)),
                 ("Canela.T." + lado, q(-0.525, 0.109, 0.448), q(-0.455, 0.109, 0.108)),
                 ("Casco.T." + lado, q(-0.455, 0.109, 0.108), q(-0.43, 0.109, 0.05))])
            c = q(-0.560, 0.109, 0.466)  # a ponta do jarrete (o calcâneo)
            add("jarrete", elipsoide(c, r(0.022, gb), r(0.034, gb), r(0.030, gb)), 0.012, [("Perna." + lado, c, c)])
            for c, osso in ((q(0.324, 0.111, 0.108), "Canela.D." + lado), (q(-0.455, 0.109, 0.108), "Canela.T." + lado)):
                add("boleto", elipsoide(c, r(0.035, gb), r(0.040, gb), r(0.030, gb)), 0.008, [(osso, c, c)])
        return pecas

    # -- as peças à parte (malhas baixas) --
    def olhos(self):
        """O olho amendoado: o meio, os eixos (o comprido ao longo da cabeça, com o canto de dentro mais baixo; o de
        cima; o de fora) e os raios."""
        g = "cabeca"
        out = []
        a = self.ponto(0.70, 0, 1.33, g)
        b = self.ponto(0.98, 0, 1.17, g)
        T = (b - a) / np.linalg.norm(b - a)
        for sx in (1, -1):
            c = self.ponto(0.770, 0.0655 * sx, 1.317, g)
            fora = np.array((sx * 0.92, -0.32, 0.18))
            fora /= np.linalg.norm(fora)
            longo = T + np.array((0, 0, -0.18))
            longo -= fora * np.dot(longo, fora)
            longo /= np.linalg.norm(longo)
            cima = np.cross(fora, longo)
            if cima[2] < 0:
                cima = -cima
            k = self.v["olho"]
            out.append(dict(c=c, eixos=(longo, cima, fora), osso="Cabeca",
                            raios=(self.raio(0.0225 * k, g), self.raio(0.0140 * k, g), self.raio(0.0120 * k, g))))
        return out

    def orelhas(self):
        out = []
        for lado, sx in (("L", 1.0), ("R", -1.0)):
            base = self.ponto(0.710, 0.058 * sx, 1.372, "cabeca")
            d = np.array((0.60 * sx, 0.22, 0.77))
            out.append(dict(osso="Orelha." + lado, base=base, dir=d / np.linalg.norm(d),
                            comp=self.raio(0.180, "orelha"), larg=self.raio(0.124, "orelha"), lado=sx))
        return out

    def cascos(self):
        """Os cascos grandes: o talão, a ponta e a largura de cada dedo; e o boleto (para os esporões de trás)."""
        out = []
        for lado, sx in (("L", 1.0), ("R", -1.0)):
            for f0, s0, osso, fb in ((0.318, 0.112, "Casco.D." + lado, 0.324),
                                     (-0.461, 0.110, "Casco.T." + lado, -0.455)):
                talao = self.ponto(f0 - 0.006, s0 * sx, 0.070, "perna")
                ponta = self.ponto(f0 + 0.108, s0 * sx, 0.0, "perna")
                boleto = self.ponto(fb - 0.036, s0 * sx, 0.086, "perna")
                out.append(dict(osso=osso, talao=talao, ponta=ponta, larg=self.raio(0.0215, "perna_baixa"),
                                boleto=boleto, sx=sx))
        return out

    def galhada(self):
        """A galhada grossa e facetada: a haste sobe para fora e vira para a frente; a ponta da testa e três que sobem
        da haste, mais a ponta da haste (10 pontas)."""
        if not self.v["galhada"]:
            return []
        g = "cabeca"
        A = self.v["A"] * self.v["cabeca"]
        out = []
        for sx in (1.0, -1.0):
            def q(f, s, z):
                return self.ponto(f, s * sx, z, g)
            haste = [q(0.735, 0.050, 1.385), q(0.726, 0.118, 1.440), q(0.728, 0.190, 1.500), q(0.745, 0.255, 1.560),
                     q(0.780, 0.300, 1.610), q(0.832, 0.318, 1.642), q(0.892, 0.308, 1.660)]
            pontas = []
            for t, d, comp in ((0.12, (0.75, -0.10, 1.0), 0.085), (0.44, (0.15, -0.10, 1.0), 0.150),
                               (0.72, (0.28, -0.10, 1.0), 0.135)):
                v = np.array((d[1] * sx, -d[0], d[2]))
                pontas.append((t, v / np.linalg.norm(v), comp * A))
            out.append(dict(haste=haste, r0=0.028 * A, r1=0.016 * A, roseta=0.037 * A, pontas=pontas, sx=sx))
        return out

    # -- os materiais de cada face --
    def canonico(self, pos):
        v = self.v
        A = v["A"]
        f1, s1, z1 = -pos[:, 1] / A, pos[:, 0] / A, pos[:, 2] / A
        zc = np.where(z1 < ZB * v["pernas"], z1 / v["pernas"], z1 - ZB * (v["pernas"] - 1.0))
        return f1 / v["corpo"], s1 / v["corpo_r"], zc

    def _eixo(self, cabeca_osso, ponta_osso=None):
        o = {n: (np.asarray(a), np.asarray(b)) for n, a, b, _ in self.ossos()}
        a = o[cabeca_osso][0]
        b = o[ponta_osso][1] if ponta_osso else o[cabeca_osso][1]
        T = (b - a) / np.linalg.norm(b - a)
        L = np.array((1.0, 0, 0))
        L = L - T * np.dot(L, T)
        L /= np.linalg.norm(L)
        U = np.cross(T, L)
        return a, b, T, U / np.linalg.norm(U)

    def materiais_das_faces(self, c, n, parte, regiao, ext):
        """O material de cada face pelo centro (c), a normal (n), a peça do corpo mais perto, a região (o corpo ou as
        peças à parte) e os atributos das peças à parte (ext: u ao longo, v de lado a lado, w > 0 por dentro da orelha).
        As fronteiras seguem as arestas: as transições angulosas do low-poly."""
        A = self.v["A"]
        nf = len(c)
        principal = M["Cervo_Filhote_Pelagem"] if self.v["manchas"] else M["Cervo_Pelagem_Principal"]
        mat = np.full(nf, principal)
        fc, sc, zc = self.canonico(c)
        frente = -n[:, 1]
        dentro = -np.sign(c[:, 0] + 1e-9) * n[:, 0]
        corpo = regiao == 0
        fs = np.array([t[0] for t in TRONCO])[::-1]
        alto = np.interp(fc, fs, np.array([t[1] for t in TRONCO])[::-1])
        baixo = np.interp(fc, fs, np.array([t[2] for t in TRONCO])[::-1])
        h = (zc - baixo) / np.maximum(alto - baixo, 1e-3)
        tr = corpo & np.isin(parte, ("tronco", "ombro", "coxa", "peito"))
        pernas = corpo & np.isin(parte, ("mao", "pe", "cotovelo", "jarrete", "joelho", "boleto"))
        mancha = fbm(c / A, 5.0, 2, 21)

        def por(mascara, nome):
            mat[mascara] = M[nome]

        # o pelo mais escuro no lombo e em manchas grandes no alto do corpo
        por(tr & (mancha > 0.70) & (h > 0.50), "Cervo_Pelagem_Escura")
        por(tr & (h > 0.88) & (np.abs(sc) < 0.08), "Cervo_Pelagem_Escura")
        # o peito, a barriga, a parte de dentro das coxas e o branco embaixo da cauda
        por(tr & (fc > 0.48) & (frente > 0.60) & (np.abs(sc) < 0.085) & (h < 0.72), "Cervo_Pelagem_Clara")
        por(corpo & (parte == "peito") & (frente > 0.2), "Cervo_Pelagem_Clara")
        por(tr & (h < 0.14) & (n[:, 2] < -0.40) & (fc > -0.30), "Cervo_Pelagem_Clara")
        por(tr & (h < 0.08) & (n[:, 2] < -0.65) & (fc > -0.25), "Cervo_Regiao_Inferior")
        por(corpo & np.isin(parte, ("coxa", "pe")) & (fc < -0.2) & (dentro > 0.80) & (zc > 0.50),
            "Cervo_Pelagem_Clara")

        # as pernas: o lado de dentro claro e a quartela escura sobre o casco

        por(pernas & (zc < 0.118), "Cervo_Pelagem_Escura")
        # o pescoço: a frente (a garganta) clara
        a, b, T, U = self._eixo("Pescoco_1", "Pescoco_2")
        dors = n @ U
        pc = corpo & (parte == "pescoco")
        por(pc & (dors < -0.55), "Cervo_Pelagem_Clara")
        por(pc & (dors > 0.6) & (mancha > 0.5), "Cervo_Pelagem_Escura")
        # a cabeça: a testa e o alto do focinho mais escuros, a cara de baixo clara, o nariz preto
        a, b, T, U = self._eixo("Cabeca")
        comp = np.linalg.norm(b - a)
        t = ((c - a) @ T) / comp
        dors = n @ U
        cb = corpo & np.isin(parte, ("cabeca", "bochecha", "sobrancelha", "mandibula", "queixo", "nariz"))
        por(cb & (dors > 0.62) & (t > 0.25) & (t < 0.85), "Cervo_Pelagem_Escura")
        if self.v["galhada"]:
            por(cb & (dors > 0.45) & (t < 0.30), "Cervo_Pelagem_Escura")
        por(cb & (dors < -0.25) & (t > 0.15), "Cervo_Pelagem_Clara")
        por(corpo & np.isin(parte, ("mandibula", "queixo")), "Cervo_Pelagem_Clara")
        por(cb & (t > 0.62) & (np.abs(dors) < 0.35) & (np.abs(c[:, 0]) > 0.025 * A), "Cervo_Pelagem_Clara")
        # o contorno dos olhos: a pálpebra escura e o anel claro em volta
        for o in self.olhos():
            d = c - o["c"]
            e = np.sqrt(sum((d @ ax / rr) ** 2 for ax, rr in zip(o["eixos"][:2], o["raios"][:2])))
            perto = np.abs(d @ o["eixos"][2]) < o["raios"][2] * 3.0
            por(cb & perto & (e < 2.1), "Cervo_Pelagem_Clara")
            por(cb & perto & (e < 1.25), "Cervo_Pelagem_Escura")
        nc = self.ponto(0.952, 0, 1.196, "cabeca")
        dn = np.linalg.norm(c - nc, axis=1) / self.raio(0.041, "cabeca")
        por(cb & (dn < 1.25) & ((n @ T) > -0.15) & (dors > -0.55), "Cervo_Focinho")
        # a cauda: em cima clara, embaixo e nas beiradas branca
        cd = corpo & (parte == "cauda")
        por(cd, "Cervo_Pelagem_Clara")
        por(cd & (np.abs(c[:, 0]) < 0.026 * A) & (n[:, 1] > 0.2), "Cervo_Regiao_Inferior")
        por(cd & (n[:, 1] < -0.05), "Cervo_Pelagem_Clara")
        # as peças à parte
        por(regiao == 1, "Cervo_Olhos")
        orelha = regiao == 2
        por(orelha, "Cervo_Filhote_Pelagem" if self.v["manchas"] else "Cervo_Pelagem_Principal")
        por(orelha & (ext["w"] > 0.2), "Cervo_Pelagem_Clara")
        por(orelha & ((np.abs(ext["v"]) > 0.80) | (ext["u"] > 0.84)), "Cervo_Pelagem_Escura")
        por(regiao == 3, "Cervo_Cascos")
        por(regiao == 4, "Cervo_Galhadas")
        return mat, h, fc, sc

    def manchas_do_filhote(self):
        """As manchas brancas do filhote: três fileiras de cada lado do lombo, do ombro à garupa, e outras soltas nos
        lados do corpo. Cada uma: (f, a altura no tronco de 0 a 1, o lado, o raio relativo)."""
        if not self.v["manchas"]:
            return []
        rng = np.random.default_rng(11)
        out = []
        for sx in (1, -1):
            for hh, passo, desl in ((0.88, 0.066, 0.0), (0.76, 0.074, 0.03)):
                for f in np.arange(0.36 - desl, -0.52, -passo):
                    out.append((f + rng.uniform(-0.012, 0.012), hh + rng.uniform(-0.025, 0.025), sx,
                                rng.uniform(0.85, 1.2)))
            for _ in range(8):
                out.append((rng.uniform(-0.42, 0.25), rng.uniform(0.52, 0.66), sx, rng.uniform(0.6, 0.9)))
        return out

    def ponto_do_tronco(self, f, h):
        fs = np.array([t[0] for t in TRONCO])[::-1]
        alto = np.interp(f, fs, np.array([t[1] for t in TRONCO])[::-1])
        baixo = np.interp(f, fs, np.array([t[2] for t in TRONCO])[::-1])
        return self.ponto(f, 0.0, baixo + (alto - baixo) * h, "corpo")
