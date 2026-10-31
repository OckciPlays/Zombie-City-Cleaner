# A espingarda estilizada (v2, 30/10/2026): a espingarda antiga do jogo melhorada, low poly como ela e com a mesma identidade
# (a de bomba robusta, a coronha e a telha de madeira, o receptor e os dois tubos): as quinas chanfradas, alguns detalhes no mesmo estilo de blocos e as cores da paleta estilizada (veja o estilizadas.py).
# As peças saem dos ossos da antiga nos braços: o corpo (gun), a telha (pump) e o cartucho (shell).

NOME = "Espingarda2"
FOTO = "espingarda_v2"            # o prefixo das fotos em Docs/Armas/capturas
MODELO = "Espingarda"
FBX_ANTIGO = "Armas.fbx"
PECAS = ["corpo", "telha", "cartucho"]
OSSOS = {"gun": "corpo", "pump": "telha", "shell": "cartucho"}
CHANFRO = 0.0016          # a largura do chanfro nas quinas (m)
ANGULO_CHANFRO = 40       # só nas quinas mais fechadas que isto (graus)
ICONE = (768, 128)


def cor_da_face(peca, c, n, cor):
    # a soleira de borracha (a face de trás da coronha)
    if peca == "corpo" and c.y > 0.315 and n.y > 0.6:
        return "borracha"
    return cor


def extras(pecas):
    m = pecas["corpo"]
    # o porta-cartuchos do lado esquerdo (+X) do receptor, com 4 cartuchos em pé (a base de latão embaixo)
    m.caixa((0.0172, -0.212, 0.016), (0.0212, -0.103, 0.064), "preto_fosco", chanfro=0.0010)
    for y in (-0.1945, -0.1695, -0.1445, -0.1195):
        m.tubo("z", (0.0300, y, 0.0430), 0.0094, 0.0094, 0.046, "vermelho", lados=10)
        m.tubo("z", (0.0300, y, 0.0170), 0.0098, 0.0098, 0.008, "latao", lados=10)
    # os pinos do receptor
    for y in (-0.205, -0.085):
        m.tubo("x", (-0.0005, y, 0.012), 0.0028, 0.0028, 0.0400, "aco", lados=8)
