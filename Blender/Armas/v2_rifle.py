# A M4 estilizada (v2, 30/10/2026): a arma antiga do jogo melhorada, low poly como ela e com a mesma identidade
# (o fuzil preto, o guarda-mão de anéis, a alça de mira e o quebra-chamas): as quinas chanfradas, alguns detalhes no mesmo estilo de blocos e as cores da paleta estilizada (veja o estilizadas.py).
# As peças saem dos ossos da antiga nos braços: o corpo (gun), a alavanca (slide) e o carregador (mag).

NOME = "Rifle2"
FOTO = "rifle_v2"            # o prefixo das fotos em Docs/Armas/capturas
MODELO = "Rifle"
FBX_ANTIGO = "Armas.fbx"
PECAS = ["corpo", "ferrolho", "carregador"]
OSSOS = {"gun": "corpo", "slide": "ferrolho", "mag": "carregador"}
CHANFRO = 0.0016          # a largura do chanfro nas quinas (m)
ANGULO_CHANFRO = 40       # só nas quinas mais fechadas que isto (graus)
ICONE = (640, 256)


def cor_da_face(peca, c, n, cor):
    # a soleira de borracha (a face de trás da coronha)
    if peca == "corpo" and c.y > 0.352 and n.y > 0.6:
        return "borracha"
    return cor


def extras(pecas):
    m = pecas["corpo"]
    # a tampa da janela de ejeção e o botão de avanço, do lado direito (-X) do receptor
    m.caixa((-0.0188, -0.150, 0.104), (-0.0162, -0.080, 0.126), "grafite", chanfro=0.0006)
    m.caixa((-0.0192, -0.146, 0.123), (-0.0168, -0.084, 0.125), "metal", chanfro=0.0)
    m.tubo("y", (-0.0205, -0.060, 0.119), 0.0055, 0.0055, 0.022, "metal", lados=8, chanfro=0.0006)
    # os pinos de desmontagem, atravessando o receptor de baixo
    for y in (-0.232, -0.018):
        m.tubo("x", (-0.0005, y, 0.050), 0.0032, 0.0032, 0.0385, "aco", lados=8)
    # o carregador: as nervuras dos dois lados e a base
    c = pecas["carregador"]
    for lado in (-1, 1):
        x = -0.0008 + lado * 0.0140
        for y in (-0.172, -0.128):
            c.caixa((x - 0.0012, y - 0.0025, -0.112), (x + 0.0012, y + 0.0025, 0.020), "preto_fosco", chanfro=0.0)
    c.caixa((-0.0160, -0.1990, -0.1395), (0.0145, -0.1010, -0.1285), "grafite", chanfro=0.0012)
