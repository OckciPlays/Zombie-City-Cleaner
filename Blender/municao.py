# As caixas de munição (9mm, 5,56, calibre 12 e 7,62): os modelos, o Municao.fbx e os ícones e as fotos do inventário.
#
# Como rodar (Blender 5.2, numa cena qualquer):
#   import importlib, sys; sys.path.append(r"<repo>\Blender"); import municao; importlib.reload(municao)
#   municao.construir()    # refaz as 4 caixas na cena "Municao"
#   municao.exportar()     # grava Assets/_Game/Art/Characters/Jogador/Municao.fbx
#   municao.imagens()      # grava Icone_<nome>.png (128 x 128) e Foto_<nome>.png (600 x 500) em Assets/_Game/Art/UI/Icones
# Depois, na Unity: Zombie City Cleaner > Montar armas.
#
# O formato é o do Equipamentos.fbx (as peças e as cores vêm do equipamentos.py): cada caixa é um objeto girado 90° em X,
# com a malha nos eixos da Unity. A origem fica embaixo, no meio; a tampa aberta fica para trás (+Y no Blender) e as
# balas aparecem por cima. Cada calibre tem a sua cor, para se reconhecer de longe:
# - 9mm: papelão claro com a faixa azul, as balas curtas (latão com a ponta de cobre);
# - 5,56: verde-oliva militar com a faixa amarela, as balas finas e compridas;
# - calibre 12: vermelha, com os cartuchos vermelhos em pé (a boca escura por cima);
# - 7,62: papelão marrom com a faixa preta, as balas grossas e compridas.

import math
import os

import bpy

import equipamentos as eq

NOMES = ["Municao9mm", "Municao556", "Municao12", "Municao762"]

# as cores novas, (linha da imagem, coluna) na Paleta.png, como no equipamentos.py
eq.CORES.update({
    "papelao": (3, 9), "azul": (13, 2), "latao": (10, 6), "cobre": (6, 9),
    "verde_militar": (15, 2), "amarelo": (6, 3),
    "vermelho": (6, 0), "cartucho": (3, 10), "boca": (8, 13),
    "marrom": (12, 8), "preto_fosco": (8, 5),
})

_caixa = eq._caixa
_cilindro = eq._cilindro


def _caixa_aberta(cor, faixa, w, d, h, tampa=None):
    """O corpo da caixa (w x d x h, a origem embaixo, no meio), a faixa em volta e a tampa aberta, em pé atrás."""
    tampa = tampa or cor
    pecas = [
        (_caixa, cor, (w, d, h), (0, 0, h / 2), (0, 0, 0), 0.003),
        # a faixa: um pouco mais larga que a caixa, no meio da altura
        (_caixa, faixa, (w + 0.002, d + 0.002, h * 0.28), (0, 0, h * 0.5), (0, 0, 0), 0),
        # a tampa: articulada na borda de trás, aberta para trás (inclinada 15°)
        (_caixa, tampa, (w, 0.004, d), (0, d / 2 + math.sin(math.radians(15)) * d / 2, h + math.cos(math.radians(15)) * d / 2),
         (-15, 0, 0), 0),
    ]
    return pecas


def _balas(colunas, linhas, w, d, h, raio, sobe, cor, ponta, ponta_alt):
    """As balas em pé, numa grade, aparecendo 'sobe' acima da caixa, com a ponta de outra cor."""
    pecas = []
    dx, dy = w / colunas, d / linhas
    for i in range(colunas):
        for j in range(linhas):
            x = -w / 2 + dx * (i + 0.5)
            y = -d / 2 + dy * (j + 0.5)
            # o corpo vai de um pouco abaixo da borda até 'sobe' acima dela
            corpo = sobe + 0.01
            pecas.append((_cilindro, cor, raio, corpo, (x, y, h - 0.01 + corpo / 2), "Z", 6))
            if ponta:
                pecas.append((_cilindro, ponta, raio * 0.62, ponta_alt, (x, y, h + sobe + ponta_alt / 2), "Z", 6))
    return pecas


def _9mm():
    w, d, h = 0.11, 0.07, 0.05
    return _caixa_aberta("papelao", "azul", w, d, h) + _balas(5, 3, w * 0.88, d * 0.82, h, 0.0055, 0.006, "latao", "cobre", 0.006)


def _556():
    w, d, h = 0.12, 0.065, 0.07
    return _caixa_aberta("verde_militar", "amarelo", w, d, h) + _balas(6, 3, w * 0.88, d * 0.82, h, 0.0042, 0.01, "latao", "cobre", 0.012)


def _12():
    w, d, h = 0.115, 0.085, 0.07
    return _caixa_aberta("vermelho", "preto_fosco", w, d, h) + _balas(4, 3, w * 0.9, d * 0.86, h, 0.0105, 0.012, "cartucho", "boca", 0.002)


def _762():
    w, d, h = 0.13, 0.06, 0.08
    return _caixa_aberta("marrom", "preto_fosco", w, d, h) + _balas(5, 2, w * 0.86, d * 0.78, h, 0.0058, 0.012, "latao", "cobre", 0.016)


MODELOS = {"Municao9mm": _9mm, "Municao556": _556, "Municao12": _12, "Municao762": _762}


def _cena():
    cena = bpy.data.scenes.get("Municao") or bpy.data.scenes.new("Municao")
    bpy.context.window.scene = cena
    return cena


def construir():
    cena = _cena()
    for o in list(cena.objects):
        if o.name.split("_")[0] in NOMES:
            bpy.data.objects.remove(o, do_unlink=True)
    objs = []
    for i, nome in enumerate(NOMES):
        o = eq._objeto(nome, MODELOS[nome](), cena)
        o.location = (i * 0.25, 0, 0)
        objs.append(o)
    return objs


def exportar():
    cena = _cena()
    for o in cena.objects:
        o.select_set(o.name.split("_")[0] in NOMES)
    bpy.ops.export_scene.fbx(filepath=os.path.join(eq.UNITY, "Municao.fbx"), use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", bake_anim=False, mesh_smooth_type="FACE")


def imagens():
    """Os ícones (128 x 128, de cima e da frente, para as balas aparecerem) e as fotos (600 x 500, de 3/4), no estilo dos
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
    from mathutils import Vector
    for nome, o in objs.items():
        for outro in objs.values():
            outro.hide_render = outro is not o
        cantos = [o.matrix_world @ Vector(c) for c in o.bound_box]
        centro = sum(cantos, Vector()) / 8
        tam = max((max(c[i] for c in cantos) - min(c[i] for c in cantos)) for i in range(3))
        # a frente da caixa é -Y no Blender (a tampa aberta fica atrás)
        r.resolution_x, r.resolution_y = 128, 128
        eq._camera(cena, True, centro, (0.35, -1.0, 0.75), 3.0, tam * 1.3)
        r.filepath = os.path.join(eq.ICONES, f"Icone_{nome}.png")
        bpy.ops.render.render(write_still=True)
        r.resolution_x, r.resolution_y = 600, 500
        eq._camera(cena, False, centro, (0.8, -1.0, 0.75), tam * 2.6, tam)
        r.filepath = os.path.join(eq.ICONES, f"Foto_{nome}.png")
        bpy.ops.render.render(write_still=True)
    for o in objs.values():
        o.hide_render = False
