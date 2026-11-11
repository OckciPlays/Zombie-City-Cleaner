# O lampião (10/11/2026, a etapa 4 das armas de sucata: o fogo): o lampião de querosene, na mão direita como a tocha
# (tocha.py, faca.py; o "golpe" é o soco direito, com o lampião junto). A chama e a luz são da Unity, no vazio Chama
# (PONTA): a boca dos braços vai para lá.
#
# - O modelo (no espaço da arma solta: a ponta em -Y, o alto em +Z, a origem onde a mão segura): a mão pega o cabo de
#   madeira da alça, deitado ao longo do Y, e o lampião fica pendurado embaixo dele (o alto do lampião em +Z): o tanque
#   pintado de vermelho, o queimador de latão com o botão do pavio, o vidro com as quatro grades de arame, o chapéu com o
#   respiro, os dois tubos dos lados e a alça de arame presa neles.
# - Na primeira pessoa (10/11/2026, a pedido: "erguido na mão direita, um pouco à frente"): o cabo na tela em (0,24;
#   0,36) a 0,5 m, o lampião sempre de pé (pendurado; o cabo atravessado na tela, a ponta para a esquerda e 20 graus para a
#   câmera: o pulso fica dentro dos limites, com o desvio em 14 graus), o ombro 7 cm mais para a direita (o antebraço vem
#   da direita, ao lado do lampião, sem tampar o vidro), andando com o punho dos braços dos punhos (o balanço do passo, o
#   saque, o soco) e balançando um pouco como um pêndulo (arma_fp, a função que o duas_maos.refazer chama em cada quadro).
#
# Como rodar (os braços de primeira pessoa; a malha solta e os itens são do Blender/fogo.py):
#   ARMAS_SEM_BLEND=1 ARMAS_SAIDA=<pasta> blender -b --factory-startup --disable-autoexec --python Blender/Armas/lampiao.py -- bracos
#   (bracos: o Jogador_FP_Lampiao.fbx na pasta ARMAS_SAIDA, ou na do jogador; conferir: as fotos, na pasta ARMAS_FOTOS)

import math
import os
import sys

from mathutils import Matrix, Quaternion, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import faca  # noqa: E402

NOME = "Lampiao"
FOTO = "lampiao"
MODELO = "Lampiao"
PECAS = ["corpo"]
ICONE = (128, 128)
ANGULO_LISO = 35
CORES = {
    "pintura": (3, 10),       # 8a2e26 (a tinta vermelha do tanque e do chapéu)
    "metal": (3, 4),          # 6c7075
    "metal_escuro": (3, 3),   # 3f4246
    "vidro": (11, 12),        # 8ab8d8
    "latao": (3, 11),         # a88a4a
    "madeira": (4, 12),       # a77d4f (a madeira crua do cabo)
    "ferrugem": (3, 12),      # 9a4f27
    "pavio": (3, 9),          # d2cbb2
}
METAL = {"metal", "latao"}
# (só para o faca._no_punho: o lugar de antes no punho; aqui a arma_fp põe o lampião no lugar dele em cada quadro)
GIRO_PARA_A_FRENTE = 78.0
GIRO_NO_EIXO = 0.0
NO_PUNHO = Vector((0.0, 0.0, 0.0))
PEGADA_Y = 0.0
LIMITE_TORCAO = 90.0
DIAGONAIS = {"R": (16, 25, 35)}
# o cabo (o raio e o meio x, z no corte do y da pegada): o corte da malha toda pegaria o lampião pendurado embaixo
CABO_FP = (0.012, (0.0, 0.0))
ORTHO_CONFERIR = 0.8
# a chama (no espaço da arma): no meio do vidro, logo acima do pavio
PONTA = Vector((0.0, 0.0, -0.205))
CHAMA = PONTA

# a pose na tela (a câmera dos braços na origem, olhando -Y; a tela de -1 a 1): o cabo em (u, v) a d m
NA_TELA = (0.24, 0.36, 0.50)
GUINADA = 20.0          # o cabo atravessado, a ponta (o lado do polegar) para a esquerda e 20 graus para a câmera (com 50
#                         o desvio do pulso ia a 38 graus; com 80, a 57)
# o ombro um pouco para a direita e para cima (fora da tela): o antebraço chega ao cabo mais deitado
OMBRO_FP = {"R": (-0.07, 0.0, 0.04)}
PENDULO = (0.35, 14.0)  # o quanto o lampião balança com a velocidade da mão (graus por m/s) e o máximo (graus)
GIRO_DOS_PUNHOS = 0.0   # o giro do punho dos braços dos punhos que o lampião segue (0: fica sempre de pé)


def _tubo_y(m, y0, y1, r0, r1, sup, lados=12):
    m.tubo("y", (0.0, (y0 + y1) / 2, 0.0), r0, r1, abs(y1 - y0), sup, lados=lados)


def _anel_z(m, z0, z1, r0, r1, sup, lados=12):
    m.tubo("z", (0.0, 0.0, (z0 + z1) / 2), r0, r1, abs(z1 - z0), sup, lados=lados)


def _arame(m, pts, raio, sup):
    """Um arame pela polilinha (cilindros finos de 4 lados entre os pontos)."""
    import bmesh
    for a, b in zip(pts, pts[1:]):
        a, b = Vector(a), Vector(b)
        d = b - a
        r = bmesh.ops.create_cone(m.bm, cap_ends=True, cap_tris=False, segments=4, radius1=raio, radius2=raio,
                                  depth=d.length + raio)
        M = Matrix.Translation((a + b) / 2) @ d.normalized().to_track_quat("Z", "Y").to_matrix().to_4x4()
        bmesh.ops.transform(m.bm, matrix=M, verts=r["verts"])
        m._fim(r["verts"], sup, 0.0)


def modelo(m):
    """O lampião pendurado no cabo (o cabo no eixo Y, na origem; o lampião embaixo, o alto em +Z)."""
    # o cabo de madeira e as pontas de metal da alça
    _tubo_y(m, -0.037, 0.037, 0.012, 0.012, "madeira", lados=8)
    for s in (-1, 1):
        _tubo_y(m, s * 0.037, s * 0.043, 0.006, 0.006, "metal", lados=6)
    # a alça de arame: do cabo até os pinos nos tubos dos lados
    for s in (-1, 1):
        _arame(m, [(0, s * 0.042, 0.0), (0, s * 0.052, -0.03), (0, s * 0.058, -0.08), (0, s * 0.060, -0.135)], 0.0028, "metal")
        _tubo_y(m, s * 0.052, s * 0.064, 0.007, 0.007, "metal_escuro", lados=6)       # o pino
    # o respiro e o chapéu (pintados), com a aba
    _anel_z(m, -0.075, -0.062, 0.016, 0.014, "pintura", lados=10)
    _anel_z(m, -0.079, -0.075, 0.022, 0.022, "metal", lados=10)
    _anel_z(m, -0.108, -0.079, 0.042, 0.024, "pintura", lados=12)
    _anel_z(m, -0.114, -0.108, 0.046, 0.046, "metal", lados=12)
    # o vidro (bojudo) e as quatro grades de arame em volta dele
    _anel_z(m, -0.150, -0.114, 0.044, 0.032, "vidro", lados=12)
    _anel_z(m, -0.200, -0.150, 0.047, 0.044, "vidro", lados=12)
    _anel_z(m, -0.232, -0.200, 0.034, 0.047, "vidro", lados=12)
    for k in range(4):
        a = math.radians(45 + 90 * k)
        c, s_ = math.cos(a), math.sin(a)
        _arame(m, [(c * 0.036, s_ * 0.036, -0.112), (c * 0.053, s_ * 0.053, -0.15), (c * 0.054, s_ * 0.054, -0.2),
                   (c * 0.04, s_ * 0.04, -0.234)], 0.0018, "metal")
    _anel_z(m, -0.236, -0.232, 0.042, 0.042, "metal", lados=12)
    # o pavio e o queimador de latão, com o botão de girar o pavio
    _anel_z(m, -0.226, -0.214, 0.006, 0.004, "pavio", lados=6)
    _anel_z(m, -0.250, -0.236, 0.030, 0.026, "latao", lados=12)
    m.tubo("x", (0.040, 0.0, -0.243), 0.004, 0.004, 0.022, "latao", lados=6)
    m.tubo("x", (0.054, 0.0, -0.243), 0.009, 0.009, 0.005, "latao", lados=8)
    # o tanque pintado (com as bordas de metal e uma mancha de ferrugem) e a tampa de encher
    _anel_z(m, -0.254, -0.250, 0.060, 0.060, "metal", lados=14)
    _anel_z(m, -0.290, -0.254, 0.058, 0.058, "pintura", lados=14)
    _anel_z(m, -0.294, -0.290, 0.060, 0.060, "metal", lados=14)
    m.caixa((-0.0595, -0.018, -0.282), (-0.0575, 0.012, -0.262), "ferrugem", chanfro=0.0)
    m.tubo("z", (-0.032, 0.026, -0.246), 0.008, 0.008, 0.01, "latao", lados=6)
    # os dois tubos dos lados (do tanque ao chapéu), com a dobra de cima
    for s in (-1, 1):
        m.tubo("z", (0.0, s * 0.058, -0.178), 0.0055, 0.0055, 0.15, "pintura", lados=6)
        _tubo_y(m, s * 0.058, s * 0.030, 0.0055, 0.0055, "pintura", lados=6)
        m.tubo("z", (0.0, s * 0.03, -0.104), 0.0055, 0.0055, 0.01, "pintura", lados=6)


# ------------------------------------------------------------------------------------------------ a pose na tela
def _rot_pendurado():
    """O giro do lampião na tela: o alto (+Z da arma) para cima (+Z), a ponta (-Y da arma) para a esquerda da tela (+X),
    girada GUINADA graus para a câmera (+Y)."""
    g = math.radians(GUINADA)
    ponta = Vector((math.cos(g), math.sin(g), 0.0))
    y = -ponta
    z = Vector((0.0, 0.0, 1.0))
    x = y.cross(z)
    return Matrix((x, y, z)).transposed()


_CACHE = {}


def _mao(rig, acao, f):
    """A posição (no mundo) e o giro da mão direita dos braços dos punhos no quadro f da ação (guardados)."""
    chave = (acao.name, f)
    if chave not in _CACHE:
        import duas_maos
        M = duas_maos._avaliar(rig, acao, f)["hand.R"]
        Mw = rig.matrix_world @ M
        _CACHE[chave] = (Mw.translation.copy(), Mw.to_quaternion())
    return _CACHE[chave]


def _base(rig, acao):
    """A mão no quadro 1 do Idle (o Idle com o mesmo prefixo da ação: as ações dos punhos, "FP_Rig_Maos|...")."""
    import bpy
    idle = bpy.data.actions[acao.name.split("|", 1)[0] + "|Idle"]
    return _mao(rig, idle, int(idle.frame_range[0]))


def arma_fp(nome, acao, f, W, M_antes, rig, cfg=None):
    """O lugar do lampião (no espaço do esqueleto) no quadro f do clipe nome: o cabo no lugar da tela, mais o quanto a mão
    dos punhos andou desde o quadro 1 do Idle; de pé, balançando com a velocidade da mão."""
    import duas_maos
    p0, q0 = _base(rig, acao)
    p, q = _mao(rig, acao, f)
    f0, f1 = int(acao.frame_range[0]), int(acao.frame_range[1])
    u, v, d = NA_TELA
    alvo = duas_maos.na_tela(u, v, d) + (p - p0)
    # o pêndulo: a velocidade da mão (diferença central; no laço, as pontas se juntam)
    fa, fb = max(f0, f - 1), min(f1, f + 1)
    vel = (_mao(rig, acao, fb)[0] - _mao(rig, acao, fa)[0]) * (24.0 / max(1, fb - fa))
    k, mx = PENDULO
    R = _rot_pendurado()
    # para a frente (-Y) a mão anda e o fundo do lampião fica para trás: gira em volta do X da tela; para o lado, do Y
    ang_x = max(-mx, min(mx, -vel.y * k * 10.0))
    ang_y = max(-mx, min(mx, vel.x * k * 10.0))
    balanco = (Matrix.Rotation(math.radians(ang_x), 3, "X") @ Matrix.Rotation(math.radians(ang_y), 3, "Y"))
    giro = Quaternion().slerp(q @ q0.inverted(), GIRO_DOS_PUNHOS).to_matrix()
    Wm = Matrix.Translation(alvo) @ (giro @ balanco @ R).to_4x4()
    return rig.matrix_world.inverted() @ Wm


if __name__ == "__main__" and "--" in sys.argv:
    faca.rodar(sys.modules[__name__], sys.argv[sys.argv.index("--") + 1:] or ["bracos"])
