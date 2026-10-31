# A M4 nova (29/10/2026): a partir do AR-15 que o Pedro mandou (Fontes/cc_iur.blend, com o cano de 15,7", o guarda-mão
# de trilhos, as miras rebatíveis e a coronha regulável): o receptor de cima e o de baixo em preto, o cano e o quebra-chamas
# grafite, a empunhadura de polímero pontilhada, a coronha de polímero com a soleira de borracha, o carregador de aço, o
# seletor, o retém, o gatilho e a tampa da janela de ejeção.
#
# O encaixe na antiga (a do Armas.fbx): a boca no mesmo ponto (y -0,732, z 0,105) e a escala 0,12 (1,2 vez o tamanho real,
# como a pistola), com que a empunhadura cai na mão direita e o guarda-mão na esquerda. As peças são as da antiga: o corpo
# ("gun"), o ferrolho ("slide": a alavanca de manejo) e o carregador ("mag").

from mathutils import Matrix, Vector

NOME = "Rifle"
MODELO = "Rifle"
FBX_ANTIGO = "Armas.fbx"
FONTE = "cc_iur.blend"
OBJETOS = {
    "iur lower": "corpo",
    "iur upper": "corpo",
    "iur 15,7in barrel": "corpo",
    "iur flash hider": "corpo",
    "iur dust cover": "corpo",
    "iur bolt catch": "corpo",
    "iur mag catch": "corpo",
    "iur selector": "corpo",
    "iur trigga": "corpo",
    "iur grip": "corpo",
    "iur iron sights": "corpo",
    "iur stock": "corpo",
    "iur charging handle": "ferrolho",
    "ar15 30rnd mag empty (steel)": "carregador",
}
PECAS = ["corpo", "ferrolho", "carregador"]
# o ferrolho do AR-15 fica dentro do receptor: sai, e a tampa da janela de ejeção vai fechada (no modelo, aberta)
TIRAR_ESCONDIDAS = True
DISSOLVER = 1.0          # graus: as faces quase no mesmo plano viram uma (menos triângulos, a mesma forma)
# as peças miúdas com muitas faces (a fração que fica): de longe e na mão não se vê a diferença
DECIMAR = {"iur iron sights": 0.45, "iur selector": 0.5, "iur mag catch": 0.6, "iur trigga": 0.6, "iur bolt catch": 0.6,
           "iur flash hider": 0.7}

CHANFRO = 0.0010
TAMANHO = 2048
MARGEM_UV = 0.003
DISTANCIA_AO = 0.02
AO_NA_COR = 0.45
ICONE = (640, 256)      # o tamanho do ícone do inventário (o de antes)

ESCALA = 0.12
LARGURA = 1.0
BOCA_FONTE = Vector((4.2463, 0.0, 0.2198))   # a boca do quebra-chamas no AR-15
BOCA = Vector((0.0, -0.732, 0.105))          # a boca da antiga (Armas.cs: (0, 0,105, 0,732))


def transformacao():
    """Do AR-15 (a boca em +X, a esquerda em +Y, o alto em +Z) para o espaço da arma (a boca em -Y, a esquerda em +X)."""
    s = ESCALA
    G = Matrix(((0, s * LARGURA, 0, 0), (-s, 0, 0, 0), (0, 0, s, 0), (0, 0, 0, 1)))
    return Matrix.Translation(BOCA - G @ BOCA_FONTE) @ G


def ajustar(nome, o):
    if nome == "iur dust cover":
        o.rotation_euler = (0.0, 0.0, 0.0)


def superficie(obj, mat, c, n):
    """Os materiais do AR-15: h-190 (o preto de Cerakote), stell (o aço), polymer (o polímero)."""
    if obj == "iur grip":
        return "texturizado"
    if obj == "iur stock":
        if mat == "polymer":
            return "borracha" if c.x < -4.16 else "polimero"
        return "pintado"
    if obj in ("iur 15,7in barrel", "iur flash hider"):
        return "exposto" if mat == "stell" else "grafite"
    if obj == "iur bolt":
        return "grafite"
    if obj == "ar15 30rnd mag empty (steel)":
        return "grafite"
    if mat == "polymer":
        return "polimero"
    if mat == "stell":
        return "exposto"
    return "pintado"
