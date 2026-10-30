# A perua nova, versão 2 (leve), 29/10/2026: o mesmo carro da v1 (Blender/PeruaNova/perua_nova.py, que não muda), com
# bem menos faces. Os detalhes miúdos da v1 (frisos, barras da grade, gomos dos bancos, a madeira sobreposta, parafusos,
# aletas da calota, o refletor do farol...) saem da malha e entram nas texturas: a v1 inteira é o modelo "alto", e a
# cor, o relevo (normal), a oclusão, o metal e a aspereza dela são projetados na malha leve (bake "selected to active").
# O pneu é de 18 lados e chapado (como o da roda antiga). As peças, os nomes de peça (Perua2_*), as origens e as medidas
# são as da v1 (as do carro.py): a física e os pontos da Unity não mudam.
#
# Roda num Blender em segundo plano (não mexe no Blender aberto):
#   blender -b Blender/PeruaNova/PeruaNova.blend --python Blender/PeruaNova2/rodar.py -- construir
#   blender -b Blender/PeruaNova2/PeruaNova2.blend --python Blender/PeruaNova2/rodar.py -- assar cor
#   (... aspereza, metal, normal, ao; depois "compor_exportar")
# O resultado vai para Assets/_Game/Art/Veiculos/PeruaNova2/ e Blender/PeruaNova2/PeruaNova2.blend. Na Unity:
# Zombie City Cleaner > Veículos > Aplicar o visual novo da perua (v2, leve).

import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
PASTA_BLENDER = os.path.dirname(AQUI)
for p in (PASTA_BLENDER, os.path.join(PASTA_BLENDER, "PeruaNova")):
    if p not in sys.path:
        sys.path.append(p)
import perua_nova as v1  # noqa: E402  (a v1: as medidas, os construtores e os materiais de autoria)

REPO = os.path.dirname(PASTA_BLENDER)
DESTINO = os.path.join(REPO, "Assets", "_Game", "Art", "Veiculos", "PeruaNova2")
TEXTURAS = os.path.join(DESTINO, "Texturas")
BLEND = os.path.join(AQUI, "PeruaNova2.blend")

NOMES_V1 = ["Perua_Carroceria", "Perua_Porta_DE", "Perua_Porta_DD", "Perua_Porta_TE", "Perua_Porta_TD", "Perua_Roda",
            "Perua_Volante", "Perua_Capo", "Perua_Tampa"]
NOMES = [n.replace("Perua_", "Perua2_") for n in NOMES_V1]
RODA_ALTA = "Perua2_RodaAlta"          # o modelo alto da roda nova (18 lados, com as aletas e os parafusos)
ASSAR = "Perua2_Assar"                 # todas as peças leves juntas, só durante o bake
FINAIS = {"Perua_Lataria": "Perua2_Lataria", "Perua_Interior": "Perua2_Interior", "Perua_Vidro": "Perua2_Vidro",
          "Perua_Farol": "Perua2_Farol", "Perua_Lanterna": "Perua2_Lanterna"}
TAMANHO = 2048

# o que nunca vira malha na v2 (fica só na textura, projetado da v1)
SO_TEXTURA = {"madeira", "friso", "madeira_int", "tapete", "placa_f", "placa_t", "alto_falante"}
# o que nunca some, por menor que seja (os vidros, as lentes que acendem, os cintos e as borrachas soltas)
SEMPRE = {"lente_farol", "lente_lanterna", "vidro", "cinto", "borracha"}


def _miudo(sup, dims):
    """Um detalhe miúdo (fino e estreito): sai da malha e fica na textura."""
    if sup in SO_TEXTURA:
        return True
    if sup in SEMPRE:
        return False
    d = sorted(dims)
    return d[0] < 0.011 and d[1] < 0.06


class MalhaLeve(v1.Malha):
    """A Malha da v1, mas sem os detalhes miúdos, com chanfros só nas peças grandes (1 segmento) e menos lados."""

    def caixa(self, sup, tam, centro, giro=(0, 0, 0), chanfro=0.006, seg=1):
        if _miudo(sup, tam):
            return []
        ch = chanfro if (max(tam) >= 0.25 and chanfro >= 0.01) else 0.0
        return super().caixa(sup, tam, centro, giro, ch, 1)

    def ripa(self, sup, a, b, tx, ty, chanfro=0.0):
        if _miudo(sup, (tx, ty, (Vector(b) - Vector(a)).length)):
            return []
        return super().ripa(sup, a, b, tx, ty, 0.0)

    def perfil(self, sup, pontos, x0, x1, chanfro=0.0, seg=2):
        if sup in SO_TEXTURA:
            return []
        return super().perfil(sup, pontos, x0, x1, chanfro if chanfro >= 0.012 else 0.0, 1)

    def frente(self, sup, pontos, z0, z1, chanfro=0.0, seg=1):
        if sup in SO_TEXTURA:
            return []
        return super().frente(sup, pontos, z0, z1, 0.0, 1)

    def cilindro(self, sup, raio, comprimento, centro, eixo="x", lados=16, raio2=None, liso=True):
        # as hastes compridas (a antena, a do quebra-sol) ficam, mesmo finas
        if comprimento < 0.3 and _miudo(sup, (2 * raio, 2 * raio, comprimento)):
            return []
        lados = max(6, min(lados, 8 if raio < 0.03 else 12))
        return super().cilindro(sup, raio, comprimento, centro, eixo, lados, raio2, liso)

    def torno(self, sup, perfil, centro=(0, 0, 0), eixo="x", lados=32, fechar=(False, False), liso=True, escala=(1, 1)):
        return super().torno(sup, perfil, centro, eixo, 18 if lados >= 24 else max(8, lados // 2), fechar, liso, escala)

    def tubo(self, sup, pontos, raio, lados=8, liso=True):
        return super().tubo(sup, pontos, raio, 6, liso)

    def toro(self, sup, raio, espessura, centro=(0, 0, 0), lados=32, gomos=8, achatar=1.0):
        return super().toro(sup, raio, espessura, centro, 20, 6, achatar)


# ---------------------------------------------------------------------------------------------------- o que muda na v2
def _almofada_leve(m, lx, ly, lz, gomos, eixo, vinco, frente=False):
    """A almofada é um bloco só: os gomos e as laterais ficam na textura (o relevo vem da v1)."""
    m.caixa("vinil", (lx, ly, lz), (0, 0, 0), chanfro=min(0.03, min(lx, ly, lz) * 0.3), seg=1)


def _forro_do_arco_leve(m, zc, x0, x1, sup="escuro"):
    n = 7
    r = v1.R_ARCO - 0.004
    pts = [(zc + r * math.cos(math.pi * i / n), v1.R_RODA + r * math.sin(math.pi * i / n)) for i in range(n + 1)]
    for i in range(n):
        (za, ya), (zb, yb) = pts[i], pts[i + 1]
        m.ripa(sup, ((x0 + x1) / 2, ya, za), ((x0 + x1) / 2, yb, zb), abs(x1 - x0), 0.012)


def _farol_leve(m, x, y, z):
    """O farol leve: a caixa aberta, o bisel, a lente (que acende) e, no lugar do paraboloide, uma placa no meio da
    caixa que recebe a imagem do refletor da v1 (aparece pela lente transparente)."""
    lx, ly = 0.145, 0.125
    prof = z + 0.012 - v1.Z_RECUO
    zc = (z + 0.012 + v1.Z_RECUO) / 2
    for s in (-1, 1):
        m.caixa("grade", (0.006, ly + 0.012, prof), (x + s * (lx / 2 + 0.003), y, zc), chanfro=0)
        m.caixa("grade", (lx + 0.012, 0.006, prof), (x, y + s * (ly / 2 + 0.003), zc), chanfro=0)
    m.caixa("refletor", (lx, ly, 0.012), (x, y, 2.119), chanfro=0)
    for p, q in (((-1, -1), (1, -1)), ((-1, 1), (1, 1)), ((-1, -1), (-1, 1)), ((1, -1), (1, 1))):
        m.ripa("cromo", (x + p[0] * lx / 2, y + p[1] * ly / 2, z + 0.012), (x + q[0] * lx / 2, y + q[1] * ly / 2, z + 0.012), 0.012, 0.014)
    m.caixa("lente_farol", (lx - 0.008, ly - 0.008, 0.008), (x, y, z + 0.012), chanfro=0)


def _roda(m, alta):
    """A roda: o pneu de 18 lados, chapado e mais quadrado (como o da roda antiga), o aro, o tambor, a calota e o cubo.
    A alta tem também as aletas da calota e os 5 parafusos (que a leve recebe na textura). Raio 0,30, largura 0,21."""
    h, R = v1.MEIA_LARGURA_PNEU, v1.R_RODA
    pneu = [(0.200, -h + 0.012), (0.250, -h), (0.288, -h + 0.010), (R, -h + 0.032),
            (R, h - 0.032), (0.288, h - 0.010), (0.250, h), (0.200, h - 0.012)]
    m.torno("pneu", pneu, lados=18, liso=False)
    m.torno("aco", [(0.200, h - 0.012), (0.19, h - 0.02), (0.19, -h + 0.02), (0.200, -h + 0.012)], lados=18, liso=False)
    m.torno("aco", [(0.0, -0.04), (0.15, -0.04), (0.15, -0.085), (0.0, -0.085)], lados=12, liso=False)
    calota = [(0.205, h - 0.006), (0.198, h + 0.006), (0.150, h + 0.012), (0.100, h + 0.024), (0.088, h + 0.008)]
    m.torno("cromo", calota, lados=18, liso=False)
    m.torno("aco", [(0.088, h + 0.004), (0.0, h + 0.004)], lados=18, liso=False)
    m.torno("cromo", [(0.032, h + 0.004), (0.032, h + 0.018), (0.0, h + 0.028)], lados=10, liso=False)
    if alta:
        for i in range(12):
            a = 2 * math.pi * i / 12
            p0 = Vector((h + 0.024, 0.105 * math.cos(a), 0.105 * math.sin(a)))
            p1 = Vector((h + 0.014, 0.175 * math.cos(a), 0.175 * math.sin(a)))
            m.ripa("aco", tuple(p0), tuple(p1), 0.012, 0.010)
        for i in range(5):
            a = 2 * math.pi * i / 5 + math.pi / 10
            m.cilindro("cromo", 0.011, 0.022, (h + 0.014, 0.058 * math.cos(a), 0.058 * math.sin(a)), eixo="x", lados=6, liso=False)


# ---------------------------------------------------------------------------------------------------- montar
def _cena():
    return bpy.data.scenes["PeruaNova"]


def _apagar(nomes):
    for n in nomes:
        o = bpy.data.objects.get(n)
        if o is not None:
            me = o.data
            bpy.data.objects.remove(o, do_unlink=True)
            if me is not None and me.users == 0:
                bpy.data.meshes.remove(me)


def _renomear(o, nome):
    o.name = nome
    o.data.name = nome
    return o


def construir():
    """As peças leves (Perua2_*), no mesmo lugar das da v1, com os construtores da v1 e a MalhaLeve."""
    cena = _cena()
    _apagar(NOMES + [RODA_ALTA, ASSAR])
    salvos = (v1.Malha, v1._almofada, v1._forro_do_arco, v1._farol)
    v1.Malha, v1._almofada, v1._forro_do_arco, v1._farol = MalhaLeve, _almofada_leve, _forro_do_arco_leve, _farol_leve
    try:
        objs = [_renomear(v1._carroceria(cena), "Perua2_Carroceria")]
        for nome, s, fr in (("DE", -1, True), ("DD", 1, True), ("TE", -1, False), ("TD", 1, False)):
            objs.append(_renomear(v1._porta(cena, "Perua2_Porta_" + nome, s, fr), "Perua2_Porta_" + nome))
        vol = _renomear(v1._volante(cena), "Perua2_Volante")
        vol.location = v1._para_blender((-0.37, 0.86, 0.50))
        vol.rotation_euler = (math.radians(90 + 25), 0, 0)
        objs.append(vol)
        objs.append(_renomear(v1._capo(cena), "Perua2_Capo"))
        objs.append(_renomear(v1._tampa(cena), "Perua2_Tampa"))
    finally:
        v1.Malha, v1._almofada, v1._forro_do_arco, v1._farol = salvos
    for nome, alta in (("Perua2_Roda", False), (RODA_ALTA, True)):
        m = v1.Malha()
        _roda(m, alta)
        o = m.objeto(nome, cena)
        o.location = v1._para_blender(v1.LUGAR_DA_RODA_ASSAR)
        if not alta:
            objs.insert(5, o)
    # a ordem de NOMES
    objs = [bpy.data.objects[n] for n in NOMES]
    for o in objs:
        print(o.name, "tri", sum(len(p.vertices) - 2 for p in o.data.polygons))
    tot = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) * (4 if o.name.endswith("Roda") else 1) for o in objs)
    print("TOTAL v2 (com 4 rodas):", tot)
    return objs


# ---------------------------------------------------------------------------------------------------- materiais (polimento)
def polir_materiais():
    """Ajustes dos materiais de autoria, só neste arquivo (a v1 não muda): o forro mais claro e menos manchado, o vinil
    com menos granulado e a madeira com o veio na direção certa também nas faces de frente/trás (a tampa traseira)."""
    v1.CORES["forro"] = (196, 192, 176)
    v1.CORES["vinil"] = (58, 138, 146)
    v1.VERSAO_DOS_MATERIAIS = 100
    usados = sorted({m.name[4:] for m in bpy.data.materials if m.name.startswith("Aut_")})
    for s in usados:
        v1._material_de_autoria(s)
    for nome in ("Aut_madeira", "Aut_madeira_int"):
        mat = bpy.data.materials.get(nome)
        if mat is None:
            continue
        nt = mat.node_tree
        geo = next(n for n in nt.nodes if n.type == "NEW_GEOMETRY")
        sep = nt.nodes.new("ShaderNodeSeparateXYZ")
        nt.links.new(geo.outputs["Normal"], sep.inputs[0])
        absy = nt.nodes.new("ShaderNodeMath")
        absy.operation = "ABSOLUTE"
        nt.links.new(sep.outputs[1], absy.inputs[0])
        frente = nt.nodes.new("ShaderNodeMapRange")
        frente.inputs["From Min"].default_value = 0.55
        frente.inputs["From Max"].default_value = 0.8
        nt.links.new(absy.outputs[0], frente.inputs["Value"])
        for no in [n for n in nt.nodes if n.bl_idname == "ShaderNodeVectorMath" and n.operation == "MULTIPLY"]:
            k = tuple(no.inputs[1].default_value)
            if not (abs(k[0] - 1) < 1e-6 and k[1] < 0.5 and abs(k[2] - 1) < 1e-6):
                continue
            outro = nt.nodes.new("ShaderNodeVectorMath")
            outro.operation = "MULTIPLY"
            outro.inputs[1].default_value = (k[1], 1.0, 1.0)
            if no.inputs[0].links:
                nt.links.new(no.inputs[0].links[0].from_socket, outro.inputs[0])
            mix = nt.nodes.new("ShaderNodeMix")
            mix.data_type = "VECTOR"
            nt.links.new(frente.outputs["Result"], mix.inputs["Factor"])
            a = next(s for s in mix.inputs if s.identifier == "A_Vector")
            b = next(s for s in mix.inputs if s.identifier == "B_Vector")
            r = next(s for s in mix.outputs if s.identifier == "Result_Vector")
            destinos = [l.to_socket for l in no.outputs[0].links]
            nt.links.new(no.outputs[0], a)
            nt.links.new(outro.outputs[0], b)
            for d in destinos:
                nt.links.new(r, d)
    print("materiais polidos:", len(usados))


# ---------------------------------------------------------------------------------------------------- UV
# (a madeira e os frisos agora são projetados na pintura das laterais: ela ganha mais pixel)
PESO = dict(v1.PESO_NO_ATLAS, pintura=2.6, cromo=1.5, vinil=1.5, painel=1.3, plastico=1.1, lente_farol=2.0,
            lente_lanterna=1.6, escuro=0.35, grade=0.5, aco=0.6)
# a roda é uma malha só, usada 4 vezes: os pixels dela valem por 4
PESO_DA_RODA = 2.6


def desembrulhar(margem=0.002):
    cena = _cena()
    objs = [bpy.data.objects[n] for n in NOMES]
    vl = cena.view_layers[0]
    for o in cena.objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    vl.objects.active = objs[0]
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_mode(type="FACE")
    ts = cena.tool_settings
    ts.use_uv_select_sync = True
    for atlas in ("A", "B"):
        for o in objs:
            bm = bmesh.from_edit_mesh(o.data)
            sup = [m.name[4:] for m in o.data.materials]
            for f in bm.faces:
                f.select_set(v1.SUPERFICIES[sup[f.material_index]][0] == atlas)
            bm.select_flush_mode()
            bmesh.update_edit_mesh(o.data)
        bpy.ops.uv.smart_project(angle_limit=math.radians(70), island_margin=margem, area_weight=0.0,
                                 correct_aspect=True, scale_to_bounds=False)
        bpy.ops.uv.average_islands_scale()
        for o in objs:
            bm = bmesh.from_edit_mesh(o.data)
            uvl = bm.loops.layers.uv.active
            sup = [m.name[4:] for m in o.data.materials]
            for f in bm.faces:
                k = PESO.get(sup[f.material_index], 1.0) * (PESO_DA_RODA if o.name.endswith("Roda") else 1.0)
                if f.select and k != 1.0:
                    for l in f.loops:
                        l[uvl].uv = l[uvl].uv * k
            bmesh.update_edit_mesh(o.data)
        try:
            bpy.ops.uv.pack_islands(rotate=True, margin=margem, shape_method="CONCAVE")
        except TypeError:
            bpy.ops.uv.pack_islands(rotate=True, margin=margem)
    bpy.ops.object.mode_set(mode="OBJECT")
    print("UV ok")


# ---------------------------------------------------------------------------------------------------- bake (da v1 para a v2)
def _imagem(atlas, canal):
    nome = f"Perua2_{atlas}_{canal}"
    tam = TAMANHO if atlas in ("A", "B") else 64
    img = bpy.data.images.get(nome)
    if img is None or img.size[0] != tam:
        if img is not None:
            bpy.data.images.remove(img)
        img = bpy.data.images.new(nome, tam, tam, alpha=False, float_buffer=False)
        img.colorspace_settings.name = "sRGB" if canal == "cor" else "Non-Color"
    # sem usuário (nenhum material aponta para ela no fim da etapa), o Blender a jogaria fora ao salvar
    img.use_fake_user = True
    return img


def _alvo(canal):
    for mat in bpy.data.materials:
        if not mat.name.startswith("Aut_"):
            continue
        atlas = v1.SUPERFICIES[mat.name[4:]][0]
        no = mat.node_tree.nodes.get("BAKE")
        no.image = _imagem("V" if atlas == "-" else atlas, canal)
        mat.node_tree.nodes.active = no


def _juntar():
    """Uma cópia de todas as peças leves numa malha só (o alvo do bake), no lugar delas."""
    cena = _cena()
    _apagar([ASSAR])
    bm = bmesh.new()
    mats = []
    for n in NOMES:
        o = bpy.data.objects[n]
        me = o.data.copy()
        me.transform(o.matrix_world)
        mapa = []
        for m in me.materials:
            if m not in mats:
                mats.append(m)
            mapa.append(mats.index(m))
        idx = [p.material_index for p in me.polygons]
        for p, i in zip(me.polygons, idx):
            p.material_index = mapa[i]
        bm.from_mesh(me)
        bpy.data.meshes.remove(me)
    me = bpy.data.meshes.new(ASSAR)
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(m)
    o = bpy.data.objects.new(ASSAR, me)
    cena.collection.objects.link(o)
    return o


def assar(canais, amostras=8, margem=8, extrusao=0.012, distancia=0.03):
    """Projeta a v1 (e a roda alta) nas peças leves juntas, canal a canal (Cycles na CPU: com a Unity aberta, a GPU
    fica sem memória e o bake sai preto sem erro)."""
    import time
    cena = _cena()
    cena.render.engine = "CYCLES"
    cena.cycles.device = "CPU"
    cena.cycles.samples = amostras
    cena.cycles.use_denoising = False
    if cena.world is None:
        cena.world = bpy.data.worlds.new("MundoPerua")
    cena.world.light_settings.distance = 0.35
    alvo = _juntar()
    altos = [bpy.data.objects[n] for n in NOMES_V1 if n != "Perua_Roda"] + [bpy.data.objects[RODA_ALTA]]
    # o alvo não aparece para nenhum raio (senão, coincidindo com a v1, a oclusão das frestas ficaria preta)
    for attr in ("visible_camera", "visible_diffuse", "visible_glossy", "visible_transmission", "visible_volume_scatter", "visible_shadow"):
        setattr(alvo, attr, False)
    for o in cena.objects:
        o.hide_render = not (o in altos or o == alvo)
    vl = cena.view_layers[0]
    for o in cena.objects:
        o.select_set(o in altos or o == alvo)
    vl.objects.active = alvo
    b = cena.render.bake
    b.use_selected_to_active = True
    b.use_cage = False
    b.cage_extrusion = extrusao
    b.max_ray_distance = distancia
    b.margin = margem
    b.margin_type = "EXTEND"
    tipos = {"cor": "DIFFUSE", "aspereza": "ROUGHNESS", "metal": "EMIT", "normal": "NORMAL", "ao": "AO"}
    for canal in canais:
        t = time.time()
        _alvo(canal)
        v1._ligar_metal(canal == "metal")
        kw = dict(pass_filter={"COLOR"}) if canal == "cor" else {}
        if canal == "normal":
            b.normal_space = "TANGENT"
        bpy.ops.object.bake(type=tipos[canal], use_selected_to_active=True, cage_extrusion=extrusao,
                            max_ray_distance=distancia, margin=margem, use_clear=True, **kw)
        v1._ligar_metal(False)
        import numpy as np
        for atlas in ("A", "B"):
            img = _imagem(atlas, canal)
            a = np.empty(TAMANHO * TAMANHO * 4, np.float32)
            img.pixels.foreach_get(a)
            # (um bake preto sem erro acontece com a GPU sem memória: confira a média)
            print(f"assado {canal} {atlas}: média {a.reshape(-1, 4)[:, :3].mean():.3f}, {time.time() - t:.0f} s")
            img.pack()
    _alvo("cor")
    for o in cena.objects:
        o.hide_render = False
    _apagar([ASSAR])


def compor():
    import numpy as np
    os.makedirs(TEXTURAS, exist_ok=True)
    for atlas, final in (("A", "Perua2_Lataria"), ("B", "Perua2_Interior")):
        def ler(canal):
            a = np.empty(TAMANHO * TAMANHO * 4, dtype=np.float32)
            _imagem(atlas, canal).pixels.foreach_get(a)
            return a.reshape(TAMANHO, TAMANHO, 4)
        cor, asp, metal, nor, ao = ler("cor"), ler("aspereza"), ler("metal"), ler("normal"), ler("ao")
        oc = ao[..., 0:1]
        c = cor.copy()
        c[..., :3] = cor[..., :3] * (0.55 + 0.45 * oc)
        c[..., 3] = 1.0
        ms = np.zeros_like(cor)
        ms[..., 0] = np.clip(metal[..., 0], 0, 1)
        ms[..., 1] = oc[..., 0]
        ms[..., 3] = 1.0 - np.clip(asp[..., 0], 0, 1)
        n = nor.copy()
        n[..., 3] = 1.0
        for sufixo, dados, espaco in (("Cor", c, "sRGB"), ("MS", ms, "Non-Color"), ("Normal", n, "Non-Color")):
            nome = f"{final}_{sufixo}"
            img = bpy.data.images.get(nome)
            if img is None or img.size[0] != TAMANHO:
                if img:
                    bpy.data.images.remove(img)
                img = bpy.data.images.new(nome, TAMANHO, TAMANHO, alpha=True, float_buffer=False)
            img.colorspace_settings.name = espaco
            img.pixels.foreach_set(dados.ravel())
            img.filepath_raw = os.path.join(TEXTURAS, nome + ".png")
            img.file_format = "PNG"
            img.save()
        print("texturas:", final)


def exportar():
    """O FBX com as 9 peças leves e os 5 materiais finais (Perua2_*), no formato da v1 e do carro.py."""
    import numpy as np
    cena = _cena()
    os.makedirs(DESTINO, exist_ok=True)
    finais = {}
    for f1, f2 in FINAIS.items():
        m = bpy.data.materials.get(f2) or bpy.data.materials.new(f2)
        finais[f1] = m
    temporarios = []
    for n in NOMES:
        o = bpy.data.objects[n]
        me = o.data.copy()
        sup = [m.name[4:] for m in o.data.materials]
        ordem = list(FINAIS.keys())
        usados = [f for f in ordem if any(v1.SUPERFICIES[s][1] == f for s in sup)]
        mapa = np.array([usados.index(v1.SUPERFICIES[s][1]) for s in sup], dtype=np.int32)
        idx = np.empty(len(me.polygons), dtype=np.int32)
        me.polygons.foreach_get("material_index", idx)
        me.materials.clear()
        for f in usados:
            me.materials.append(finais[f])
        me.polygons.foreach_set("material_index", mapa[idx])
        me.update()
        o.name = n + "_aut"
        t = bpy.data.objects.new(n, me)
        cena.collection.objects.link(t)
        t.rotation_euler = (math.radians(90), 0, 0)
        temporarios.append((t, o, n))
    vl = cena.view_layers[0]
    for o in cena.objects:
        o.select_set(any(o is t for t, _, _ in temporarios))
    vl.objects.active = temporarios[0][0]
    try:
        bpy.ops.export_scene.fbx(filepath=os.path.join(DESTINO, "PeruaNova2.fbx"), use_selection=True,
                                 object_types={"MESH"}, apply_scale_options="FBX_SCALE_ALL", bake_anim=False,
                                 mesh_smooth_type="FACE", path_mode="STRIP")
    finally:
        for t, o, n in temporarios:
            me = t.data
            bpy.data.objects.remove(t, do_unlink=True)
            bpy.data.meshes.remove(me)
            o.name = n
    print("FBX:", os.path.join(DESTINO, "PeruaNova2.fbx"))


def salvar():
    for img in bpy.data.images:
        if img.name.startswith("Perua2_") and img.has_data and not img.packed_file:
            img.pack()
    bpy.ops.wm.save_as_mainfile(filepath=BLEND, copy=False)
    print("blend:", BLEND)
