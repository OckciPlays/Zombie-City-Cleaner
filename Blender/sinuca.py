# A sinuca: a caixa das bolas (cheia e vazia) e os 4 tacos: os modelos, o Sinuca.fbx e os ícones e as fotos do inventário.
#
# Como rodar (Blender 5.2, numa cena qualquer):
#   import importlib, sys; sys.path.append(r"<repo>\Blender"); import sinuca; importlib.reload(sinuca)
#   sinuca.construir()    # refaz os 6 modelos na cena "Sinuca"
#   sinuca.exportar()     # grava Assets/_Game/Art/Characters/Jogador/Sinuca.fbx
#   sinuca.imagens()      # grava Icone_<nome>.png e Foto_<nome>.png (600 x 500) em Assets/_Game/Art/UI/Icones
# Depois, na Unity: Zombie City Cleaner > Montar armas.
#
# O formato é o do Municao.fbx (as peças e as cores vêm do equipamentos.py): cada modelo é um objeto girado 90° em X, com
# a malha nos eixos da Unity, a origem embaixo, no meio. Só cores que já existem na Paleta.png.
# - CaixaDeBolas: o estojo de madeira (3 x 1 no inventário), aberto, com as 16 bolas em duas fileiras de 8 (a forração
#   verde por dentro, a tampa aberta atrás); CaixaDeBolasVazia: o mesmo, sem as bolas (os 16 berços vazios).
# - Os tacos (4 x 1 no inventário), deitados ao longo do X: a ponta (o couro azul e a virola branca), a haste de madeira
#   clara, a junta, o antebraço com os enfeites, a empunhadura e a base com o pé de borracha. O que muda de um para o
#   outro é a parte de trás (a base, o antebraço e os enfeites): o de madeira, o escuro (com os enfeites brancos), o
#   preto (com os anéis prateados) e o vermelho.

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

import equipamentos as eq

NOMES = ["CaixaDeBolas", "CaixaDeBolasVazia", "TacoMadeira", "TacoEscuro", "TacoPreto", "TacoVermelho"]
# as células de cada um no inventário (o ícone tem esta proporção, 128 px por célula)
CELULAS = {"CaixaDeBolas": (3, 1), "CaixaDeBolasVazia": (3, 1), "TacoMadeira": (4, 1), "TacoEscuro": (4, 1),
           "TacoPreto": (4, 1), "TacoVermelho": (4, 1)}

# as cores novas, (linha da imagem, coluna) na Paleta.png
eq.CORES.update({
    # as bolas: a branca, as 7 cores (lisas e listradas) e a preta
    "bola_branca": (13, 5), "bola_amarela": (9, 13), "bola_azul": (4, 8), "bola_vermelha": (3, 10), "bola_roxa": (5, 7),
    "bola_laranja": (6, 9), "bola_verde": (6, 2), "bola_vinho": (8, 13), "bola_preta": (8, 5),
    # o estojo: a madeira, a forração verde, os berços e o fecho
    "estojo": (6, 8), "estojo_escuro": (12, 1), "forro": (2, 9), "berco": (8, 14), "fecho": (8, 8),
    # os tacos
    "haste": (1, 6), "virola": (13, 5), "sola": (4, 8), "junta": (3, 9), "borracha_taco": (8, 5),
    "madeira_taco": (3, 7), "madeira_taco_escura": (12, 1), "empunhadura": (8, 14), "enfeite_branco": (13, 5),
    "taco_preto": (8, 5), "prata": (8, 2), "taco_vermelho": (3, 10),
})

_caixa = eq._caixa
_cilindro = eq._cilindro

RAIO_DA_BOLA = 0.0286
# as cores das bolas, da 0 (a branca) à 8 (a preta); de 9 a 15, as listradas, com as cores de 1 a 7
CORES_DAS_BOLAS = ["bola_branca", "bola_amarela", "bola_azul", "bola_vermelha", "bola_roxa", "bola_laranja", "bola_verde",
                   "bola_vinho", "bola_preta"]


def _esfera(bm, uv, n, centro, raio=RAIO_DA_BOLA):
    """A bola n: uma esfera baixa (8 lados), a cor dela e, nas listradas (9 a 15), a faixa colorida no meio e o resto branco."""
    r = bmesh.ops.create_uvsphere(bm, u_segments=8, v_segments=6, radius=raio)
    vs = r["verts"]
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(centro)), verts=vs)
    faces = list({f for v in vs for f in v.link_faces})
    cor = CORES_DAS_BOLAS[n if n <= 8 else n - 8]
    if n < 9:
        eq._pintar(bm, uv, faces, cor)
        return
    for f in faces:
        z = f.calc_center_median().z - centro[2]
        eq._pintar(bm, uv, [f], cor if abs(z) < raio * 0.5 else "bola_branca")


def _cone(bm, uv, cor, r1, r2, de, ate, lados=8):
    """Um tronco de cone ao longo do X, de 'de' a 'ate' (o raio r1 no começo e r2 no fim), com o meio em y = 0 e z = 0."""
    comp = ate - de
    r = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=lados, radius1=r1, radius2=r2, depth=comp)
    vs = r["verts"]
    # o cone nasce ao longo do Z (r1 embaixo): deitado no X, com o r1 no começo
    m = Matrix.Translation(Vector(((de + ate) / 2, 0, 0))) @ Matrix.Rotation(math.radians(90), 4, "Y")
    bmesh.ops.transform(bm, matrix=m, verts=vs)
    eq._pintar(bm, uv, list({f for v in vs for f in v.link_faces}), cor)


# ------------------------------------------------------------------ o estojo das bolas

def _estojo(cheio):
    """O estojo de madeira, aberto: o fundo e as paredes por fora, a forração verde por dentro, a tampa em pé atrás (com
    a forração dela), as dobradiças e o fecho na frente. Cheio, as 16 bolas em duas fileiras de 8 (a branca e a preta
    nas pontas da fileira da frente); vazio, os 16 berços escuros."""
    passo = RAIO_DA_BOLA * 2 + 0.003
    w, d = passo * 8 + 0.026, passo * 2 + 0.026
    h, e = 0.04, 0.008      # a altura das paredes e a grossura da madeira
    pecas = [
        (_caixa, "estojo", (w, d, e), (0, 0, e / 2), (0, 0, 0), 0.002),
        (_caixa, "forro", (w - 2 * e, d - 2 * e, 0.004), (0, 0, e + 0.002), (0, 0, 0), 0),
    ]
    for s in (-1, 1):
        pecas.append((_caixa, "estojo", (w, e, h), (0, s * (d / 2 - e / 2), h / 2), (0, 0, 0), 0.002))
        pecas.append((_caixa, "estojo", (e, d - 2 * e, h), (s * (w / 2 - e / 2), 0, h / 2), (0, 0, 0), 0.002))
    # a tampa: articulada na borda de trás, aberta para trás (inclinada 12°), com a forração dela
    a = math.radians(12)
    meio = Vector((0, d / 2 + math.sin(a) * d / 2, h + math.cos(a) * d / 2))
    pecas.append((_caixa, "estojo", (w, e, d), tuple(meio), (-12, 0, 0), 0.002))
    pecas.append((_caixa, "forro", (w - 2 * e, 0.003, d - 2 * e), tuple(meio + Vector((0, -e / 2 - 0.001, 0)).copy()), (-12, 0, 0), 0))
    # as dobradiças e o fecho
    for x in (-w * 0.32, w * 0.32):
        pecas.append((_cilindro, "fecho", 0.004, 0.03, (x, d / 2 + 0.001, h), "X", 6))
    pecas.append((_caixa, "fecho", (0.024, 0.004, 0.016), (0, -d / 2 - 0.002, h - 0.01), (0, 0, 0), 0))
    # as bolas (ou os berços vazios): a fileira da frente com a branca numa ponta e a preta na outra
    fileiras = [[0, 1, 2, 3, 4, 5, 6, 8], [9, 10, 11, 12, 13, 14, 15, 7]]
    for j, fileira in enumerate(fileiras):
        y = (j - 0.5) * passo
        for i, n in enumerate(fileira):
            x = (i - 3.5) * passo
            if cheio:
                pecas.append((_esfera, n, (x, y, e + 0.004 + RAIO_DA_BOLA * 0.9)))
            else:
                pecas.append((_cilindro, "berco", RAIO_DA_BOLA * 0.8, 0.002, (x, y, e + 0.005), "Z", 8))
    return pecas


def _caixa_de_bolas():
    return _estojo(True)


def _caixa_vazia():
    return _estojo(False)


# ------------------------------------------------------------------ os tacos

COMPRIMENTO = 1.45
# o desenho de trás de cada taco: a base, o antebraço, os enfeites e os anéis
TACOS = {
    "TacoMadeira": {"base": "madeira_taco", "antebraco": "madeira_taco", "enfeite": "madeira_taco_escura", "anel": "borracha_taco"},
    "TacoEscuro": {"base": "madeira_taco_escura", "antebraco": "madeira_taco_escura", "enfeite": "enfeite_branco", "anel": "enfeite_branco"},
    "TacoPreto": {"base": "taco_preto", "antebraco": "taco_preto", "enfeite": "prata", "anel": "prata"},
    "TacoVermelho": {"base": "taco_vermelho", "antebraco": "taco_vermelho", "enfeite": "borracha_taco", "anel": "borracha_taco"},
}


def _taco(desenho):
    """Um taco de 1,45 m deitado ao longo do X, a ponta em +X (vira -X na Unity), com o meio na origem e embaixo em z = 0
    (o raio da base, 1,45 cm)."""
    c = COMPRIMENTO
    x0 = -c / 2
    rb = 0.0145
    z = rb
    partes = [
        # (de, até, raio no começo, raio no fim, cor), da base para a ponta
        (0.000, 0.012, rb, rb, "borracha_taco"),
        (0.012, 0.022, rb, rb, desenho["anel"]),
        (0.022, 0.300, rb, 0.0137, desenho["base"]),
        (0.300, 0.308, 0.0137, 0.0137, desenho["anel"]),
        (0.308, 0.540, 0.0136, 0.0127, "empunhadura"),
        (0.540, 0.548, 0.0127, 0.0127, desenho["anel"]),
        (0.548, 0.700, 0.0126, 0.0118, desenho["antebraco"]),
        (0.700, 0.722, 0.0119, 0.0119, "junta"),
        (0.722, 1.425, 0.0112, 0.0068, "haste"),
        (1.425, 1.444, 0.0068, 0.0068, "virola"),
        (1.444, 1.450, 0.0066, 0.0064, "sola"),
    ]
    pecas = []
    for de, ate, r1, r2, cor in partes:
        pecas.append((_cone_no_taco, cor, r1, r2, x0 + de, x0 + ate, z))
    # os enfeites do antebraço: 4 filetes em volta
    for k in range(4):
        ang = k * 90
        pecas.append((_enfeite, desenho["enfeite"], x0 + 0.62, 0.0124, z, ang))
    return pecas


def _cone_no_taco(bm, uv, cor, r1, r2, de, ate, z):
    n0 = len(bm.verts)
    _cone(bm, uv, cor, r1, r2, de, ate)
    bm.verts.ensure_lookup_table()
    novos = bm.verts[n0:]
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector((0, 0, z))), verts=list(novos))


def _enfeite(bm, uv, cor, x, raio, z, ang):
    """Um filete (uma caixa achatada ao longo do taco) colado no antebraço, no ângulo 'ang' em volta do eixo."""
    a = math.radians(ang)
    centro = (x, math.cos(a) * raio, z + math.sin(a) * raio)
    _caixa(bm, uv, cor, (0.06, 0.0035, 0.007), centro, (ang, 0, 0), 0)


MODELOS = {
    "CaixaDeBolas": _caixa_de_bolas,
    "CaixaDeBolasVazia": _caixa_vazia,
    "TacoMadeira": lambda: _taco(TACOS["TacoMadeira"]),
    "TacoEscuro": lambda: _taco(TACOS["TacoEscuro"]),
    "TacoPreto": lambda: _taco(TACOS["TacoPreto"]),
    "TacoVermelho": lambda: _taco(TACOS["TacoVermelho"]),
}


def _cena():
    cena = bpy.data.scenes.get("Sinuca") or bpy.data.scenes.new("Sinuca")
    # logo depois de abrir um arquivo, o contexto do script fica sem janela: a primeira do gerenciador serve
    janela = bpy.context.window or bpy.context.window_manager.windows[0]
    janela.scene = cena
    return cena


def construir():
    cena = _cena()
    for o in list(cena.objects):
        if o.name.split("_")[0] in NOMES:
            bpy.data.objects.remove(o, do_unlink=True)
    objs = []
    for i, nome in enumerate(NOMES):
        o = eq._objeto(nome, MODELOS[nome](), cena)
        o.location = (0, i * 0.3, 0)
        objs.append(o)
    return objs


def exportar():
    cena = _cena()
    for o in cena.objects:
        o.select_set(o.name.split("_")[0] in NOMES)
    bpy.ops.export_scene.fbx(filepath=os.path.join(eq.UNITY, "Sinuca.fbx"), use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", bake_anim=False, mesh_smooth_type="FACE")


def imagens():
    """Os ícones (128 px por célula: o estojo de cima e da frente, as bolas aparecendo; o taco de lado, inteiro) e as fotos
    (600 x 500, de 3/4), no estilo dos outros: o Workbench com a textura, a cavidade, o contorno e o fundo transparente."""
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
        r.resolution_x, r.resolution_y = 128 * cx, 128 * cy
        if nome.startswith("Taco"):
            # de lado (a ponta para a direita) e um pouco de cima; o taco enche a largura
            eq._camera(cena, True, centro, (0.0, -1.0, 0.18), 3.0, tam * 1.04)
        else:
            # quase de cima, no meio do estojo (sem a tampa, que fica atrás): as bolas (ou os berços) enchem o ícone
            fundo = Vector((centro.x, min(c.y for c in cantos) + 0.07, 0.03))
            eq._camera(cena, True, fundo, (0.0, -0.3, 1.0), 3.0, tam * 1.02)
        r.filepath = os.path.join(eq.ICONES, f"Icone_{nome}.png")
        bpy.ops.render.render(write_still=True)
        r.resolution_x, r.resolution_y = 600, 500
        if nome.startswith("Taco"):
            eq._camera(cena, False, centro, (0.3, -1.0, 0.5), tam * 1.6, tam)
        else:
            eq._camera(cena, False, centro, (0.6, -1.0, 0.9), tam * 1.9, tam)
        r.filepath = os.path.join(eq.ICONES, f"Foto_{nome}.png")
        bpy.ops.render.render(write_still=True)
    for o in objs.values():
        o.hide_render = False
