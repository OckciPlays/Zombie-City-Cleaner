# O machado (31/10/2026, a pedido: "vamos criar o machado agora", com a imagem de um machado de cabo comprido): a segunda
# arma branca, como a faca (faca.py: as funções são as dela, com esta cfg).
#
# - O modelo (no espaço da arma solta: a cabeça em -Y, o alto da cabeça em +Z, o fio para baixo, a origem onde a mão
#   segura, perto do fim do cabo): a cabeça preta (o olho em cima e a lâmina que abre para baixo), o fio de aço e o cabo
#   de madeira, mais grosso perto da cabeça, fino no meio e com o gancho no fim. Nas cores da Paleta.png dos itens.
# - Os braços de primeira pessoa: os dos punhos com o machado no punho direito, e o clipe "Golpe" (golpe()): a machadada
#   de cima para baixo. A mão direita (um alvo de IK, como no maos_sniper.py) sobe e vai para trás, desce para a frente
#   girando o machado e volta à guarda; o braço segue, e tudo é assado em quadros-chave. O golpe pega no quadro 8.
#
# Como rodar: blender -b --factory-startup --disable-autoexec --python Blender/Armas/machado.py -- tudo
#   (as etapas: construir, exportar, imagens, fotos, bracos, conferir)

import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import faca  # noqa: E402

NOME = "Machado"
FOTO = "machado"
MODELO = "Machado"
PECAS = ["corpo"]
ICONE = (512, 128)
ANGULO_LISO = 35
CORES = {
    "cabeca": (3, 2),    # 1b1c1f
    "fio": (4, 6),       # c4c2b8
    "cabo": (3, 8),      # 7e5b44
}
METAL = {"fio"}
# o machado no punho direito: o cabo atravessa a mão (do mindinho ao indicador), a cabeça para o lado do polegar e o fio
# para a frente. Na câmera, um machado que aponta para a frente fica encurtado (e o golpe de cima para baixo, na direção
# do olhar, não aparece): ele fica deitado na diagonal, atravessando a tela (GIRO_DE_LADO, em volta do dorso), e o golpe é
# na diagonal, de cima à direita para baixo à esquerda
GIRO_DE_LADO = -10.0
GIRO_PARA_A_FRENTE = 62.0
GIRO_NO_EIXO = -50.0
# a mão direita mais alta no cabo, 18 cm da ponta de baixo (a esquerda vai embaixo dela: SEGUNDA_MAO)
NO_PUNHO = Vector((0.0, 0.18, 0.0))
ORTHO_CONFERIR = 1.2
QUADROS_DO_GOLPE = (1, 4, 7, 8, 9, 11)
# o golpe: (quadro, o deslocamento da mão (m, no espaço dos braços: +X para a esquerda, -Y para a frente, +Z para cima),
# o giro em volta do eixo do olhar (graus) e o giro em volta do eixo esquerda-direita (graus: positivo deita a cabeça
# para a frente))
CHAVES_DO_GOLPE = [
    (1, (0.0, 0.0, 0.0), 0.0, 0.0),
    (4, (-0.06, 0.0, 0.0), -15.0, -8.0),
    (7, (0.06, -0.08, 0.01), 40.0, 8.0),
    (8, (0.10, -0.10, 0.0), 65.0, 10.0),
    (11, (0.13, -0.06, -0.03), 80.0, 18.0),
    (15, (0.0, 0.0, 0.0), 0.0, 0.0),
]


def _cunha(m, perfil, z0, t0, z1, t1, sup, chanfro=0.0):
    """Um polígono de lado (pontos (y, z)) puxado em X para os dois lados, com a grossura (a metade) indo de t0 em z0 a
    t1 em z1: a lâmina afina para o fio. As duas faces de lado continuam planas."""
    def t(z):
        f = min(1.0, max(0.0, (z - z0) / (z1 - z0)))
        return t0 + (t1 - t0) * f
    a = [m.bm.verts.new((-t(z), y, z)) for y, z in perfil]
    b = [m.bm.verts.new((t(z), y, z)) for y, z in perfil]
    n = len(perfil)
    m.bm.faces.new(a)
    m.bm.faces.new(list(reversed(b)))
    for i in range(n):
        j = (i + 1) % n
        m.bm.faces.new((a[i], a[j], b[j], b[i]))
    m._fim(a + b, sup, chanfro)


def modelo(m):
    """O machado no espaço da arma solta (m): a cabeça (o olho em volta da ponta do cabo, a lâmina que abre para baixo e
    afina e o fio de aço) e o cabo curvo com o gancho."""
    # a cabeça refeita em 31/10/2026, a pedido ("a parte de metal está muito bugada e não forma um padrão de machado ...
    # o cabo entra dentro da parte de metal"; a referência é um machado Vonder): o olho, um bloco preto que envolve a ponta
    # do cabo (o cabo, de 3,7 cm, some dentro dele), mais grosso que o cabo
    m.caixa((-0.019, -0.572, -0.028), (0.019, -0.500, 0.032), "cabeca", chanfro=0.004)
    # a lâmina: um trapézio que abre para baixo (mais para a frente que para trás) e afina até o fio
    lamina = [(-0.570, -0.020), (-0.502, -0.020), (-0.497, -0.046), (-0.486, -0.071), (-0.470, -0.092),
              (-0.602, -0.092), (-0.591, -0.071), (-0.579, -0.046)]
    _cunha(m, lamina, -0.020, 0.0165, -0.092, 0.0045, "cabeca", chanfro=0.0015)
    # o fio de aço, levemente curvo
    fio = [(-0.470, -0.092), (-0.602, -0.092), (-0.599, -0.105), (-0.582, -0.112), (-0.536, -0.1145), (-0.490, -0.112),
           (-0.473, -0.105)]
    _cunha(m, fio, -0.092, 0.0045, -0.1145, 0.0012, "fio")
    cima = [(-0.535, 0.020), (-0.45, 0.019), (-0.30, 0.013), (-0.15, 0.011), (-0.02, 0.012), (0.030, 0.010), (0.046, 0.004)]
    baixo = [(0.046, -0.016), (0.036, -0.032), (0.020, -0.026), (0.0, -0.016), (-0.15, -0.012), (-0.30, -0.013),
             (-0.45, -0.016), (-0.535, -0.017)]
    m.perfil_x(cima + baixo, -0.012, 0.012, "cabo", chanfro=0.004)


def golpe(rig, idle, prefixo):
    """A machadada (o clipe Golpe, quadros 1 a 15): a mão direita por IK nas CHAVES_DO_GOLPE, o resto como no Idle."""
    cena = bpy.context.scene
    faca._usar(rig, idle)
    pb = rig.pose.bones
    M = rig.matrix_world
    f0, f1 = CHAVES_DO_GOLPE[0][0], CHAVES_DO_GOLPE[-1][0]
    cena.frame_set(f0)
    H0 = M @ pb["hand.R"].matrix
    cot0 = (M @ pb["forearm.R"].head).copy()
    omb0 = (M @ pb["upper_arm.R"].head).copy()
    giro0 = H0.to_quaternion().to_matrix().to_4x4()

    def vazio(nome):
        o = bpy.data.objects.new(nome, None)
        cena.collection.objects.link(o)
        o.rotation_mode = "QUATERNION"
        return o

    alvo, polo = vazio("alvo.R"), vazio("polo.R")
    fora = (cot0 - (omb0 + H0.translation) / 2).normalized() * 0.3
    for f, d, gy, gx in CHAVES_DO_GOLPE:
        giro = Matrix.Rotation(math.radians(gy), 4, "Y") @ Matrix.Rotation(math.radians(gx), 4, "X")
        T = Matrix.Translation(H0.translation + Vector(d)) @ giro @ giro0
        alvo.location = T.translation
        alvo.rotation_quaternion = T.to_quaternion()
        alvo.keyframe_insert("location", frame=f)
        alvo.keyframe_insert("rotation_quaternion", frame=f)
        polo.location = cot0 + fora + Vector(d)
        polo.keyframe_insert("location", frame=f)
    ante, mao = pb["forearm.R"], pb["hand.R"]
    ik = ante.constraints.new("IK")
    ik.target = alvo
    ik.pole_target = polo
    ik.chain_count = 2
    ik.use_tail = True
    cp = mao.constraints.new("COPY_TRANSFORMS")
    cp.target = alvo
    melhor, erro_min = 0.0, 1e9
    for g in range(-180, 180, 3):
        ik.pole_angle = math.radians(g)
        bpy.context.view_layer.update()
        e = ((M @ pb["forearm.R"].head) - cot0).length
        if e < erro_min:
            erro_min, melhor = e, g
    ik.pole_angle = math.radians(melhor)
    bpy.context.view_layer.objects.active = rig
    for o in bpy.context.view_layer.objects:
        o.select_set(o == rig)
    bpy.ops.object.mode_set(mode="POSE")
    for p in pb:
        if hasattr(p, "select"):
            p.select = True
        else:
            p.bone.select = True
    bpy.ops.nla.bake(frame_start=f0, frame_end=f1, only_selected=True, visual_keying=True, clear_constraints=True,
                     use_current_action=False, bake_types={"POSE"})
    bpy.ops.object.mode_set(mode="OBJECT")
    nova = rig.animation_data.action
    nova.name = f"{prefixo}|Golpe"
    for o in (alvo, polo):
        bpy.data.objects.remove(o, do_unlink=True)
    faca._usar(rig, idle)
    print(f"  Golpe: quadros {f0}-{f1}, polo {melhor}°, cotovelo a {erro_min * 100:.1f} cm do Idle")
    return nova


# a pegada com as duas mãos (31/10/2026, a pedido: "o machado pode fazer a pegada em 2 mãos"): a mão esquerda no cabo, a
# SEGUNDA_MAO m da direita, para o lado da cabeça, fechada em volta dele como a direita (os nós dos dedos ao longo do cabo,
# o pulso do mesmo lado que o da direita), em todos os clipes que o machado usa; ela segue o cabo a cada quadro
SEGUNDA_MAO = 0.05
CLIPES_COM_DUAS_MAOS = ("Idle", "Walk", "Run", "Sacar", "Golpe")


def _quadro(eixo, outro):
    """Uma base ortonormal: X no eixo, Y no outro (sem a parte no eixo), Z o terceiro."""
    x = eixo.normalized()
    y = (outro - x * outro.dot(x)).normalized()
    return Matrix((x, y, x.cross(y))).transposed()


def _mao(rig, lado):
    """O miolo do punho, a linha dos nós (do mindinho ao indicador) e a direção do pulso, no mundo, no quadro atual."""
    pb = rig.pose.bones
    M = rig.matrix_world
    pts = []
    for d in ("index", "middle", "ring", "pinky"):
        for k in ("1", "2"):
            b = pb[f"{d}{k}.{lado}"]
            pts += [M @ b.head, M @ b.tail]
    miolo = sum(pts, Vector()) / len(pts)
    nos = ((M @ pb[f"index1.{lado}"].head) - (M @ pb[f"pinky1.{lado}"].head)).normalized()
    pulso = ((M @ pb[f"hand.{lado}"].head) - (M @ pb[f"hand.{lado}"].tail)).normalized()
    return miolo, nos, pulso


def segunda_mao(rig, clipes, P, idle):
    """A mão esquerda no cabo (IK no antebraço e a mão copiando o alvo, assado em quadros-chave), clipe a clipe."""
    cena = bpy.context.scene
    pb = rig.pose.bones
    M = rig.matrix_world
    faca._usar(rig, idle)
    cena.frame_set(int(idle.frame_range[0]))
    bpy.context.view_layer.update()
    no_punho = (M @ pb["hand.R"].matrix).inverted() @ P   # o machado preso à mão direita

    def vazio(nome):
        o = bpy.data.objects.new(nome, None)
        cena.collection.objects.link(o)
        o.rotation_mode = "QUATERNION"
        return o

    for acao in [c for c in clipes if c.name.split("|", 1)[1] in CLIPES_COM_DUAS_MAOS]:
        faca._usar(rig, acao)
        f0, f1 = int(acao.frame_range[0]), int(acao.frame_range[1])
        alvo, polo = vazio("alvo.L"), vazio("polo.L")
        cot0 = None
        for f in range(f0, f1 + 1):
            cena.frame_set(f)
            A = (M @ pb["hand.R"].matrix) @ no_punho
            cabo = (A.to_3x3() @ Vector((0.0, -1.0, 0.0))).normalized()
            ponto = A @ Vector((0.0, -SEGUNDA_MAO, 0.0))
            _, _, pulso_r = _mao(rig, "R")
            miolo_l, nos_l, pulso_l = _mao(rig, "L")
            HL = M @ pb["hand.L"].matrix
            R = (_quadro(cabo, pulso_r) @ _quadro(nos_l, pulso_l).transposed()).to_4x4()
            T = Matrix.Translation(ponto) @ R @ Matrix.Translation(-miolo_l) @ HL
            alvo.location = T.translation
            alvo.rotation_quaternion = T.to_quaternion()
            alvo.keyframe_insert("location", frame=f)
            alvo.keyframe_insert("rotation_quaternion", frame=f)
            cot = M @ pb["forearm.L"].head
            omb = M @ pb["upper_arm.L"].head
            if cot0 is None:
                cot0 = cot.copy()
            polo.location = cot + (cot - (omb + HL.translation) / 2).normalized() * 0.3 + (T.translation - HL.translation)
            polo.keyframe_insert("location", frame=f)
        ante, mao = pb["forearm.L"], pb["hand.L"]
        ik = ante.constraints.new("IK")
        ik.target = alvo
        ik.pole_target = polo
        ik.chain_count = 2
        ik.use_tail = True
        cp = mao.constraints.new("COPY_TRANSFORMS")
        cp.target = alvo
        cena.frame_set(f0)
        melhor, erro_min = 0.0, 1e9
        for g in range(-180, 180, 3):
            ik.pole_angle = math.radians(g)
            bpy.context.view_layer.update()
            e = ((M @ pb["forearm.L"].head) - cot0).length
            if e < erro_min:
                erro_min, melhor = e, g
        ik.pole_angle = math.radians(melhor)
        bpy.context.view_layer.objects.active = rig
        for o in bpy.context.view_layer.objects:
            o.select_set(o == rig)
        bpy.ops.object.mode_set(mode="POSE")
        for p in pb:
            if hasattr(p, "select"):
                p.select = True
            else:
                p.bone.select = True
        bpy.ops.nla.bake(frame_start=f0, frame_end=f1, only_selected=True, visual_keying=True, clear_constraints=True,
                         use_current_action=True, bake_types={"POSE"})
        bpy.ops.object.mode_set(mode="OBJECT")
        for o in (alvo, polo):
            bpy.data.objects.remove(o, do_unlink=True)
        print(f"  {acao.name}: a mão esquerda no cabo (quadros {f0}-{f1}), polo {melhor}°")
    faca._usar(rig, idle)


if __name__ == "__main__" and "--" in sys.argv:
    faca.rodar(sys.modules[__name__], sys.argv[sys.argv.index("--") + 1:] or ["tudo"])
