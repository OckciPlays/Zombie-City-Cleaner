# A sniper estilizada (v2, 30/10/2026): a sniper antiga do jogo melhorada, low poly como ela e com a mesma identidade
# (o fuzil de ferrolho de caça, a coronha de madeira com a empunhadura e a luneta): as quinas chanfradas, alguns detalhes no mesmo estilo de blocos e as cores da paleta estilizada (veja o estilizadas.py).
# As peças saem dos ossos da antiga nos braços: o corpo (gun), o ferrolho (slide) e o carregador (mag).

NOME = "Sniper2"
FOTO = "sniper_v2"            # o prefixo das fotos em Docs/Armas/capturas
MODELO = "Sniper"
FBX_ANTIGO = "Armas_Sniper.fbx"
PECAS = ["corpo", "ferrolho", "carregador"]
OSSOS = {"gun": "corpo", "slide": "ferrolho", "mag": "carregador"}
CHANFRO = 0.0016          # a largura do chanfro nas quinas (m)
ANGULO_CHANFRO = 40       # só nas quinas mais fechadas que isto (graus)
ICONE = (896, 256)


def cor_da_face(peca, c, n, cor):
    # a soleira de borracha (a face de trás da coronha)
    if peca == "corpo" and c.y > 0.436 and n.y > 0.6:
        return "borracha"
    return cor


def _ponta(bm, frente):
    """O miolo e o raio da ponta da luneta (a objetiva, na frente, ou a ocular, atrás)."""
    from mathutils import Vector
    vs = [v.co.copy() for v in bm.verts if v.co.z > 0.150 and -0.32 < v.co.y < 0.20]
    y = min(v.y for v in vs) if frente else max(v.y for v in vs)
    ponta = [v for v in vs if abs(v.y - y) < 0.004]
    zs = [v.z for v in ponta]
    xs = [v.x for v in ponta]
    c = Vector(((min(xs) + max(xs)) / 2, y, (min(zs) + max(zs)) / 2))
    raio = max(max(zs) - min(zs), max(xs) - min(xs)) / 2
    return c, raio


def extras(pecas):
    m = pecas["corpo"]
    # as lentes da luneta (um disco escuro e liso um pouco para dentro de cada ponta) e a ocular de borracha
    c, r = _ponta(m.bm, True)
    m.tubo("y", (c.x, c.y + 0.0012, c.z), r * 0.82, r * 0.82, 0.0010, "lente", lados=12)
    c, r = _ponta(m.bm, False)
    m.tubo("y", (c.x, c.y - 0.0012, c.z), r * 0.78, r * 0.78, 0.0010, "lente", lados=12)
    m.tubo("y", (c.x, c.y - 0.0080, c.z), r * 1.04, r * 1.04, 0.012, "borracha", lados=12)
    # a bola na ponta da alavanca do ferrolho
    f = pecas["ferrolho"]
    from mathutils import Vector
    vs = sorted((v.co.copy() for v in f.bm.verts), key=lambda v: v.x)[:6]
    p = sum(vs, Vector()) / len(vs)
    f.tubo("x", (p.x - 0.0030, p.y, p.z), 0.0085, 0.0085, 0.0130, "aco", lados=8, chanfro=0.0020)
