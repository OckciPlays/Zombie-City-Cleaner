# A pistola 9 mm nova (29/10/2026): a partir da Fort-17 que o Pedro mandou (Fontes/Fort-17.blend), uma semiautomática
# de cão externo: o ferrolho grafite com o serrilhado atrás, a janela de ejeção com o cano de aço aparecendo, as miras
# com os pontos brancos, a armação de polímero preto com a empunhadura pontilhada, o guarda-mato, o gatilho, a trava, a
# alavanca do ferrolho e o carregador com a base de polímero.
#
# O encaixe na arma antiga (a do Armas.fbx, onde as animações seguram): a boca do cano no mesmo ponto (y -0,188, z 0,093)
# e a escala 0,115 (1,2 vez o tamanho real, como a antiga), com que o cabo cai na mão direita dos braços de primeira
# pessoa (conferido de lado e de baixo). A largura vai 20% a mais (o cabo da antiga era mais grosso, e os dedos fecham
# nele). As peças são as da antiga: o corpo (no osso "gun"), o ferrolho ("slide", corre no tiro) e o carregador ("mag").

from mathutils import Matrix, Vector

NOME = "Pistola"
MODELO = "Pistola"        # o nome dela nos FBX do jogador (Jogador_FP_Pistola, Pistola_* no Armas.fbx)
FBX_ANTIGO = "Armas.fbx"  # onde está a antiga solta
FONTE = "Fort-17.blend"
# os objetos da Fort-17 que entram, e a peça de cada um (o carregador solto e o cartucho solto ficam de fora)
OBJETOS = {
    "gun": "corpo",
    "hammer": "corpo",
    "mode": "corpo",
    "trigga": "corpo",
    "bolt release": "corpo",
    "bolt": "ferrolho",
    "918 mag empty": "carregador",
}
PECAS = ["corpo", "ferrolho", "carregador"]

CHANFRO = 0.0007        # o raio das quinas arredondadas no normal (m)
TAMANHO = 2048          # o atlas
MARGEM_UV = 0.004
DISTANCIA_AO = 0.012
AO_NA_COR = 0.45        # quanto do AO escurece a cor (o resto vai só no G do MS)
ICONE = (256, 128)      # o tamanho do ícone do inventário (o de antes)

ESCALA = 0.115
LARGURA = 1.2
BOCA_FORT = Vector((0.941, 0.0, 0.559))   # o centro da boca do cano na Fort-17
BOCA = Vector((0.0, -0.188, 0.093))       # a boca da antiga (Armas.cs: (0, 0,093, 0,188) na Unity)


def transformacao():
    """Da Fort-17 (a boca em +X, a esquerda em +Y, o alto em +Z) para o espaço da arma (a boca em -Y, a esquerda em +X)."""
    s = ESCALA
    sx = s * LARGURA
    G = Matrix(((0, sx, 0, 0), (-s, 0, 0, 0), (0, 0, s, 0), (0, 0, 0, 1)))
    return Matrix.Translation(BOCA - G @ BOCA_FORT) @ G


# a empunhadura pontilhada, no espaço da arma (planos n . p < d): abaixo da armação (z 0,040), acima da base (z -0,046)
# e atrás da frente do cabo, que inclina para trás descendo (18°, 4 mm à frente da face da frente)
PONTILHADO = [((0.0, 0.0, 1.0), 0.040), ((0.0, 0.0, -1.0), 0.046), ((0.0, -1.0, -0.33), 0.0308)]


def superficie(obj, mat, c, n):
    """A superfície de cada face (c e n no espaço da Fort-17). Os materiais dela: 1b o metal preto, 3b a armação, 1w o
    aço claro (o cano e o cão), 1n os pontos da mira (com emissão: aqui viram tinta branca), 2b a base do carregador."""
    if obj == "bolt":
        return {"1n": "branco", "1w": "exposto"}.get(mat, "grafite")
    if obj == "gun":
        if mat == "1w":
            return "exposto"
        if mat == "1b":
            return "pintado"
        return "polimero"   # a empunhadura pontilhada sai da região PONTILHADO, no material
    if obj == "hammer":
        return "grafite"
    if obj == "918 mag empty":
        return "polimero" if mat == "2b" else "pintado"
    return "pintado"
