# As peças do carro (a vela, a bateria, o radiador e o pneu): os modelos, o Pecas.fbx e os ícones e as fotos do inventário.
# As do motor se acham no capô dos carros da cidade e vão no capô do carro que se dirige; o pneu vai numa das rodas.
#
# Como rodar (Blender 5.2, numa cena qualquer):
#   import importlib, sys; sys.path.append(r"<repo>\Blender"); import pecas; importlib.reload(pecas)
#   pecas.construir()    # refaz as 4 peças na cena "Pecas"
#   pecas.exportar()     # grava Assets/_Game/Art/Characters/Jogador/Pecas.fbx
#   pecas.imagens()      # grava Icone_<nome>.png e Foto_<nome>.png (600 x 500) em Assets/_Game/Art/UI/Icones
# Depois, na Unity: Zombie City Cleaner > Montar armas.
#
# O formato é o do Municao.fbx (as peças e as cores vêm do equipamentos.py): cada uma é um objeto girado 90° em X, com a
# malha nos eixos da Unity, a origem embaixo, no meio.
# - Vela: em pé (o terminal em cima, o isolante de porcelana branca, o sextavado e a rosca embaixo).
# - Bateria: em pé (a caixa preta, a tampa, os dois polos, o vermelho é o positivo, e a etiqueta amarela na frente).
# - Radiador: deitado (as aletas de alumínio por cima, as caixas pretas dos lados, a tampa e os dois canos).
# - Pneu: a roda inteira deitada, com a calota para cima (as medidas da Carro_Roda do carro.py: 0,30 m de raio, 0,19 de largura).
# - Macaco: o macaco sanfona fechado, deitado (a base, os 4 braços em losango, o fuso com o olhal e o apoio de cima).
# Os ícones têm a proporção das células da peça no inventário (Itens.cs): a vela 1 x 1, a bateria 2 x 2, o radiador 3 x 2,
# o pneu 3 x 3 e o macaco 3 x 1.

import math
import os

import bpy
from mathutils import Vector

import equipamentos as eq

NOMES = ["Vela", "Bateria", "Radiador", "Pneu", "Macaco"]
# as células de cada uma no inventário (o ícone tem esta proporção, 128 px por célula)
CELULAS = {"Vela": (1, 1), "Bateria": (2, 2), "Radiador": (3, 2), "Pneu": (3, 3), "Macaco": (3, 1)}

# as cores novas, (linha da imagem, coluna) na Paleta.png
eq.CORES.update({
    "porcelana": (13, 5), "metal_claro": (8, 2), "aco": (8, 8), "aco_escuro": (8, 1),
    "preto_bateria": (11, 5), "tampa_bateria": (8, 5), "polo_mais": (6, 0), "polo_menos": (8, 0),
    "etiqueta": (10, 6), "aleta": (13, 3), "caixa_radiador": (8, 0), "cano": (8, 14), "tampa_radiador": (8, 2),
    # as do pneu, as mesmas da roda do carro.py
    "borracha": (8, 5), "aro": (8, 2), "calota": (8, 11), "furo_aro": (10, 14),
    # o macaco (os braços pintados de azul-acinzentado)
    "macaco": (8, 7),
})

_caixa = eq._caixa
_cilindro = eq._cilindro


def _vela():
    # uns 9 cm em pé: a rosca (com a ponta do eletrodo), o sextavado, o isolante branco com os anéis e o terminal
    pecas = [
        (_cilindro, "aco_escuro", 0.0068, 0.019, (0, 0, 0.0095), "Z", 10),
        (_caixa, "aco", (0.0025, 0.0025, 0.004), (0, 0.0035, -0.001), (0, 0, 0), 0),
        (_cilindro, "aco", 0.0105, 0.012, (0, 0, 0.025), "Z", 6),
        (_cilindro, "aco", 0.0085, 0.006, (0, 0, 0.034), "Z", 10),
        (_cilindro, "porcelana", 0.0062, 0.040, (0, 0, 0.057), "Z", 10),
        (_cilindro, "aco", 0.0035, 0.010, (0, 0, 0.082), "Z", 8),
        (_cilindro, "aco", 0.0045, 0.004, (0, 0, 0.089), "Z", 8),
    ]
    for z in (0.044, 0.050, 0.056, 0.062, 0.068):
        pecas.append((_cilindro, "porcelana", 0.0071, 0.0025, (0, 0, z), "Z", 10))
    return pecas


def _bateria():
    # 24 x 17 x 19 cm: a caixa, a tampa, os polos (o positivo vermelho), a alça e a etiqueta na frente (-Y)
    w, d, h = 0.24, 0.17, 0.175
    pecas = [
        (_caixa, "preto_bateria", (w, d, h), (0, 0, h / 2), (0, 0, 0), 0.006),
        (_caixa, "tampa_bateria", (w + 0.004, d + 0.004, 0.018), (0, 0, h + 0.006), (0, 0, 0), 0.004),
        (_caixa, "etiqueta", (w * 0.7, 0.003, 0.07), (0, -d / 2 - 0.001, h * 0.55), (0, 0, 0), 0),
        (_caixa, "polo_mais", (0.05, 0.004, 0.012), (-w * 0.18, -d / 2 - 0.002, h * 0.55), (0, 0, 0), 0),
        # os tampões das células, em fila
        (_caixa, "preto_bateria", (w * 0.6, 0.03, 0.006), (0, 0.02, h + 0.017), (0, 0, 0), 0),
        # a alça, deitada por cima
        (_caixa, "tampa_bateria", (0.012, d * 0.8, 0.008), (0, 0, h + 0.03), (0, 0, 0), 0),
    ]
    for s, cor in ((-1, "polo_mais"), (1, "polo_menos")):
        pecas.append((_cilindro, "aco", 0.011, 0.02, (s * w * 0.36, -d * 0.28, h + 0.024), "Z", 10))
        pecas.append((_cilindro, cor, 0.016, 0.006, (s * w * 0.36, -d * 0.28, h + 0.017), "Z", 10))
    return pecas


def _radiador():
    # deitado: 50 x 34 cm e 5 cm de grossura. O miolo de aletas (ripas finas por cima), as caixas pretas da esquerda e
    # da direita, a tampa e os canos de entrada e saída
    w, d, h = 0.50, 0.34, 0.05
    nucleo = w - 0.08
    pecas = [
        (_caixa, "aleta", (nucleo, d, h * 0.8), (0, 0, h * 0.4), (0, 0, 0), 0),
        (_caixa, "aco_escuro", (nucleo, d + 0.004, 0.008), (0, 0, h * 0.8 + 0.002), (0, 0, 0), 0),
    ]
    # as aletas: ripas claras em cima do fundo escuro
    n = 14
    for i in range(n):
        y = -d / 2 + d * (i + 0.5) / n
        pecas.append((_caixa, "metal_claro", (nucleo - 0.01, d / n * 0.55, 0.006), (0, y, h * 0.8 + 0.006), (0, 0, 0), 0))
    for s in (-1, 1):
        pecas.append((_caixa, "caixa_radiador", (0.045, d + 0.02, h), (s * (nucleo / 2 + 0.022), 0, h / 2), (0, 0, 0), 0.006))
    # a tampa em cima da caixa da esquerda e os canos (para fora, de lado)
    pecas.append((_cilindro, "tampa_radiador", 0.02, 0.012, (-(nucleo / 2 + 0.022), d * 0.32, h + 0.006), "Z", 12))
    pecas.append((_cilindro, "cano", 0.016, 0.06, (-(nucleo / 2 + 0.07), -d * 0.3, h * 0.5), "X", 10))
    pecas.append((_cilindro, "cano", 0.016, 0.06, ((nucleo / 2 + 0.07), d * 0.3, h * 0.5), "X", 10))
    return pecas


def _pneu():
    # a Carro_Roda do carro.py, deitada (o eixo em Z): o pneu, o ombro, o aro com a calota em cima e os 5 furos do aro
    r, l = 0.30, 0.19
    pecas = [
        (_cilindro, "borracha", r, l, (0, 0, l / 2), "Z", 18),
        (_cilindro, "borracha", r - 0.035, l + 0.02, (0, 0, l / 2), "Z", 18),
        (_cilindro, "aro", 0.19, 0.03, (0, 0, l + 0.002), "Z", 14),
        (_cilindro, "calota", 0.065, 0.03, (0, 0, l + 0.012), "Z", 10),
    ]
    for i in range(5):
        a = 72 * i
        t = math.radians(a)
        pecas.append((_caixa, "furo_aro", (0.05, 0.075, 0.012), (0.125 * math.sin(t), 0.125 * math.cos(t), l + 0.012), (0, 0, -a), 0))
    return pecas


def _barra(cor, p, q, largura, espessura):
    # uma caixa de p a q (no plano XZ), para os braços do macaco
    dx, dz = q[0] - p[0], q[2] - p[2]
    comp = math.hypot(dx, dz)
    giro = math.degrees(-math.atan2(dz, dx))
    centro = ((p[0] + q[0]) / 2, 0, (p[2] + q[2]) / 2)
    return (_caixa, cor, (comp, largura, espessura), centro, (0, giro, 0), 0)


def _macaco():
    # o macaco sanfona fechado, uns 50 cm com o fuso: a base, os braços de baixo e de cima até as juntas dos lados, o fuso
    # que atravessa as juntas (o olhal na ponta, para a manivela) e o apoio de cima, que encosta embaixo do carro
    pecas = [(_caixa, "aco_escuro", (0.20, 0.09, 0.012), (0, 0, 0.006), (0, 0, 0), 0)]
    for s in (-1, 1):
        junta = (s * 0.19, 0, 0.055)
        pecas.append(_barra("macaco", (s * 0.05, 0, 0.018), junta, 0.07, 0.014))
        pecas.append(_barra("macaco", junta, (s * 0.05, 0, 0.092), 0.07, 0.014))
        pecas.append((_caixa, "aco", (0.03, 0.08, 0.026), junta, (0, 0, 0), 0))
    pecas += [
        (_cilindro, "aco", 0.007, 0.46, (0.02, 0, 0.055), "X", 8),
        (_cilindro, "aco", 0.013, 0.03, (0.25, 0, 0.055), "X", 8),
        (_caixa, "aco_escuro", (0.12, 0.07, 0.02), (0, 0, 0.102), (0, 0, 0), 0),
        (_caixa, "aco", (0.012, 0.072, 0.008), (0, 0, 0.114), (0, 0, 0), 0),
    ]
    return pecas


MODELOS = {"Vela": _vela, "Bateria": _bateria, "Radiador": _radiador, "Pneu": _pneu, "Macaco": _macaco}


def _cena():
    cena = bpy.data.scenes.get("Pecas") or bpy.data.scenes.new("Pecas")
    bpy.context.window.scene = cena
    return cena


def construir():
    cena = _cena()
    for o in list(cena.objects):
        if o.name.split("_")[0] in NOMES:
            bpy.data.objects.remove(o, do_unlink=True)
    objs = []
    x = 0.0
    for nome in NOMES:
        o = eq._objeto(nome, MODELOS[nome](), cena)
        o.location = (x, 0, 0)
        x += 0.7
        objs.append(o)
    return objs


def exportar():
    cena = _cena()
    for o in cena.objects:
        o.select_set(o.name.split("_")[0] in NOMES)
    bpy.ops.export_scene.fbx(filepath=os.path.join(eq.UNITY, "Pecas.fbx"), use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", bake_anim=False, mesh_smooth_type="FACE")


def imagens():
    """Os ícones (128 px por célula, na proporção da peça no inventário) e as fotos (600 x 500, de 3/4), no estilo dos
    outros: o Workbench com a textura, a cavidade, o contorno e o fundo transparente."""
    cena = _cena()
    r = cena.render
    try:
        r.engine = "BLENDER_WORKBENCH"
    except TypeError:
        pass
    r.film_transparent = True
    r.image_settings.file_format = "PNG"
    r.image_settings.color_mode = "RGBA"
    s = cena.display.shading
    s.light = "STUDIO"
    s.color_type = "TEXTURE"
    s.show_cavity = True
    s.cavity_type = "BOTH"
    s.show_object_outline = True
    s.object_outline_color = (0.05, 0.05, 0.05)
    cena.view_settings.view_transform = "Standard"
    objs = {o.name.split("_")[0]: o for o in cena.objects if o.name.split("_")[0] in NOMES}
    for nome, o in objs.items():
        for outro in objs.values():
            outro.hide_render = outro is not o
        cantos = [o.matrix_world @ Vector(c) for c in o.bound_box]
        centro = sum(cantos, Vector()) / 8
        tam = max((max(c[i] for c in cantos) - min(c[i] for c in cantos)) for i in range(3))
        cx, cy = CELULAS[nome]
        # a frente é -Y no Blender. O radiador, deitado, é visto mais de cima (as aletas)
        icone = {"Vela": (0.6, -1.0, 0.45), "Bateria": (0.45, -1.0, 0.6), "Radiador": (0.0, -0.55, 1.0), "Pneu": (0.25, -0.6, 1.0), "Macaco": (0.0, -1.0, 0.3)}[nome]
        foto = {"Vela": (0.9, -1.0, 0.6), "Bateria": (0.8, -1.0, 0.75), "Radiador": (0.6, -0.8, 1.0), "Pneu": (0.6, -1.0, 0.9), "Macaco": (0.8, -1.0, 0.6)}[nome]
        r.resolution_x, r.resolution_y = 128 * cx, 128 * cy
        eq._camera(cena, True, centro, icone, 3.0, tam * 1.15)
        r.filepath = os.path.join(eq.ICONES, f"Icone_{nome}.png")
        bpy.ops.render.render(write_still=True)
        r.resolution_x, r.resolution_y = 600, 500
        eq._camera(cena, False, centro, foto, tam * (1.9 if nome in ("Radiador", "Pneu") else 1.5 if nome == "Macaco" else 2.6), tam)
        r.filepath = os.path.join(eq.ICONES, f"Foto_{nome}.png")
        bpy.ops.render.render(write_still=True)
    for o in objs.values():
        o.hide_render = False
