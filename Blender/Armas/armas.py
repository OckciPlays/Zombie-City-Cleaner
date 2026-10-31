# As armas novas (29/10/2026, a pedido: "reformulação visual das armas 3D"): o caminho comum das 4 armas (a pistola, o
# rifle, a sniper e a espingarda). Cada arma parte de um modelo pronto que o Pedro mandou (em Blender/Armas/Fontes) e é
# adaptada ao jogo: posta no espaço da arma antiga (a boca do cano e o cabo onde as mãos das animações seguram), dividida
# nas peças que mexem (o corpo, o ferrolho, o carregador...), com os materiais PBR da biblioteca daqui (preto, grafite,
# polímero, borracha, lente...) assados num atlas por arma: a cor, o metal/AO/lisura e o normal (as quinas arredondadas
# e o pontilhado do punho). A configuração de cada arma fica no arma_<nome>.py.
#
# O espaço da arma (o mesmo do Armas.fbx do Gustavo e do Blender/Pistola/pistola.py): o cano para -Y, o alto em +Z, a
# direita da arma em -X e a origem no cabo. A exportação gira para os eixos da Unity, como o Armas.fbx.
#
# Como rodar (num Blender em segundo plano: não mexe no Blender aberto; os modelos de fora abrem sem os scripts deles):
#   blender -b --factory-startup --disable-autoexec --python Blender/Armas/rodar.py -- pistola tudo
# As etapas: construir (do modelo de fora até o UV), assar, exportar, fotos (ou tudo). Grava o Blender/Armas/<Arma>.blend
# (o editável), o Assets/_Game/Art/Armas/Modelos/<Arma>.fbx, as texturas em Assets/_Game/Art/Armas/Texturas e as fotos em
# Docs/Armas/capturas.

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(AQUI))
FONTES = os.path.join(AQUI, "Fontes")
ARTE = os.path.join(REPO, "Assets", "_Game", "Art", "Armas")
MODELOS = os.path.join(ARTE, "Modelos")
TEXTURAS = os.path.join(ARTE, "Texturas")
CAPTURAS = os.path.join(REPO, "Docs", "Armas", "capturas")

# ------------------------------------------------------------------------------------------------ a biblioteca
# As superfícies (cores lineares). var: quanto a cor varia em manchas grandes (de leve); desgaste: o metal claro nas
# quinas vivas, só em partes e de leve; relevo: o normal fino (o "fino" do metal pintado, o "poroso" do polímero, o
# "pontilhado" do punho, o "serrilhado" das ranhuras).
SUPERFICIES = {
    # o metal pintado de preto (as peças pequenas: alavancas, gatilho, botões)
    "pintado": dict(cor=(0.020, 0.020, 0.022), var=0.10, metal=0.15, aspereza=0.50, var_asp=0.06, desgaste=0.35, relevo="fino"),
    # o metal grafite (ferrolhos, canos por fora, receptores): escuro, com reflexo discreto
    "grafite": dict(cor=(0.075, 0.076, 0.080), var=0.08, metal=0.78, aspereza=0.40, var_asp=0.06, desgaste=0.25, relevo="fino"),
    # o metal exposto (o cano na janela de ejeção, pinos, o ferrolho por dentro): aço mais claro e liso
    "exposto": dict(cor=(0.30, 0.30, 0.31), var=0.06, metal=1.00, aspereza=0.30, var_asp=0.05, desgaste=0.0, relevo="fino"),
    # o polímero preto (armações, coronhas, guarda-mãos)
    "polimero": dict(cor=(0.018, 0.018, 0.019), var=0.08, metal=0.0, aspereza=0.62, var_asp=0.05, desgaste=0.0, relevo="poroso"),
    # o polímero texturizado (a empunhadura pontilhada)
    "texturizado": dict(cor=(0.014, 0.014, 0.015), var=0.06, metal=0.0, aspereza=0.80, var_asp=0.04, desgaste=0.0, relevo="pontilhado"),
    # a borracha (a soleira da coronha, os anéis de vedação)
    "borracha": dict(cor=(0.011, 0.011, 0.011), var=0.05, metal=0.0, aspereza=0.90, var_asp=0.03, desgaste=0.0, relevo="poroso"),
    # as lentes da luneta (vidro escuro com o tratamento antirreflexo; sem emissão)
    "lente": dict(cor=(0.006, 0.012, 0.016), var=0.0, metal=0.0, aspereza=0.04, var_asp=0.0, desgaste=0.0, relevo=None),
    # o vidro (miras e janelas)
    "vidro": dict(cor=(0.030, 0.035, 0.040), var=0.0, metal=0.0, aspereza=0.06, var_asp=0.0, desgaste=0.0, relevo=None),
    # os componentes internos visíveis (a câmara, as molas, o fundo das janelas)
    "interno": dict(cor=(0.012, 0.012, 0.012), var=0.10, metal=0.60, aspereza=0.60, var_asp=0.05, desgaste=0.0, relevo="fino"),
    # o latão dos cartuchos
    "latao": dict(cor=(0.45, 0.30, 0.09), var=0.08, metal=1.00, aspereza=0.32, var_asp=0.05, desgaste=0.0, relevo=None),
    # a tinta branca dos pontos de mira (fosca, sem emissão)
    "branco": dict(cor=(0.62, 0.62, 0.58), var=0.03, metal=0.0, aspereza=0.50, var_asp=0.0, desgaste=0.0, relevo=None),
    # o plástico do cartucho de espingarda (vermelho escuro, discreto)
    "cartucho": dict(cor=(0.16, 0.025, 0.02), var=0.05, metal=0.0, aspereza=0.45, var_asp=0.04, desgaste=0.0, relevo=None),
}
NOMES_SUPERFICIES = list(SUPERFICIES)
COR_GASTA = (0.20, 0.20, 0.21)


def _no(nt, tipo, x, y, **props):
    n = nt.nodes.new(tipo)
    n.location = (x, y)
    for k, v in props.items():
        setattr(n, k, v)
    return n


def _faixa(nt, entrada, a, b, x, y, saida_a=0.0, saida_b=1.0):
    n = _no(nt, "ShaderNodeMapRange", x, y, clamp=True)
    n.inputs["From Min"].default_value = a
    n.inputs["From Max"].default_value = b
    n.inputs["To Min"].default_value = saida_a
    n.inputs["To Max"].default_value = saida_b
    nt.links.new(entrada, n.inputs["Value"])
    return n.outputs["Result"]


def _ruido(nt, coord, escala, detalhe, x, y):
    n = _no(nt, "ShaderNodeTexNoise", x, y)
    n.inputs["Scale"].default_value = escala
    n.inputs["Detail"].default_value = detalhe
    n.inputs["Roughness"].default_value = 0.55
    nt.links.new(coord, n.inputs["Vector"])
    return n.outputs["Fac"]


def _mistura_cor(nt, a, b, fator, x, y):
    n = _no(nt, "ShaderNodeMix", x, y)
    n.data_type = "RGBA"
    nt.links.new(fator, n.inputs["Factor"])
    for s, v in ((n.inputs[6], a), (n.inputs[7], b)):
        if isinstance(v, (tuple, list)):
            s.default_value = (*v, 1.0)
        else:
            nt.links.new(v, s)
    return n.outputs[2]


def _mistura(nt, a, b, fator, x, y):
    n = _no(nt, "ShaderNodeMix", x, y)
    n.data_type = "FLOAT"
    nt.links.new(fator, n.inputs["Factor"])
    for s, v in ((n.inputs[2], a), (n.inputs[3], b)):
        if isinstance(v, (int, float)):
            s.default_value = v
        else:
            nt.links.new(v, s)
    return n.outputs[0]


def _vezes(nt, a, b, x, y):
    n = _no(nt, "ShaderNodeMath", x, y, operation="MULTIPLY")
    for s, v in ((n.inputs[0], a), (n.inputs[1], b)):
        if isinstance(v, (int, float)):
            s.default_value = v
        else:
            nt.links.new(v, s)
    return n.outputs[0]


def _regiao(nt, coord, planos, x, y):
    """1 dentro da região (o lado de dentro de todos os planos: n . p < d, no espaço da arma), 0 fora, com a borda de
    meio milímetro."""
    dentro = None
    for k, (n, d) in enumerate(planos):
        dot = _no(nt, "ShaderNodeVectorMath", x, y - k * 160, operation="DOT_PRODUCT")
        nt.links.new(coord, dot.inputs[0])
        dot.inputs[1].default_value = n
        lado = _faixa(nt, dot.outputs["Value"], d - 0.00025, d + 0.00025, x + 200, y - k * 160, 1.0, 0.0)
        dentro = lado if dentro is None else _vezes(nt, dentro, lado, x + 400, y - k * 160)
    return dentro


def _altura_do_relevo(nt, coord, rel, x, y):
    """A altura do relevo e a força e a distância do Bump para ela."""
    if rel == "pontilhado":
        vor = _no(nt, "ShaderNodeTexVoronoi", x, y)
        vor.inputs["Scale"].default_value = 1150.0
        vor.inputs["Randomness"].default_value = 0.85
        nt.links.new(coord, vor.inputs["Vector"])
        return _faixa(nt, vor.outputs["Distance"], 0.15, 0.55, x + 200, y, 1.0, 0.0), 0.55, 0.0004
    if rel == "poroso":
        return _ruido(nt, coord, 1800.0, 2.0, x, y), 0.10, 0.0002
    return _ruido(nt, coord, 3200.0, 2.0, x, y), 0.035, 0.0002   # fino


def material(nome, chanfro, pontilhado=None):
    """O material de autoria de uma superfície: a cor, o metal e a aspereza em nós nomeados ("cor", "metal",
    "aspereza", para assar cada um por emissão) e o normal (as quinas arredondadas pelo Bevel e o relevo) no BSDF.
    <pontilhado>: no polímero, a região (os planos de _regiao) onde ele vira o texturizado (a empunhadura)."""
    mat = bpy.data.materials.get("Arma_" + nome)
    if mat is not None:
        bpy.data.materials.remove(mat)
    sp = SUPERFICIES[nome]
    mat = bpy.data.materials.new("Arma_" + nome)
    mat.use_nodes = True
    mat.diffuse_color = (*[min(1.0, c * 3) for c in sp["cor"]], 1.0)
    nt = mat.node_tree
    nt.nodes.clear()
    saida = _no(nt, "ShaderNodeOutputMaterial", 1400, 0)
    bsdf = _no(nt, "ShaderNodeBsdfPrincipled", 1100, 0)
    bsdf.name = "BSDF"
    nt.links.new(bsdf.outputs[0], saida.inputs["Surface"])
    coord = _no(nt, "ShaderNodeTexCoord", -1600, 0).outputs["Object"]
    geo = _no(nt, "ShaderNodeNewGeometry", -1600, 400)
    bev = _no(nt, "ShaderNodeBevel", -1600, 250, samples=8)
    bev.inputs["Radius"].default_value = chanfro
    dot = _no(nt, "ShaderNodeVectorMath", -1400, 300, operation="DOT_PRODUCT")
    nt.links.new(geo.outputs["Normal"], dot.inputs[0])
    nt.links.new(bev.outputs["Normal"], dot.inputs[1])
    quina = _faixa(nt, dot.outputs["Value"], 0.995, 0.94, -1200, 300)

    # a cor e a aspereza, com a variação grande (de leve)
    manchas = _ruido(nt, coord, 14.0, 3.0, -1200, 600)
    c0 = tuple(c * (1 - sp["var"]) for c in sp["cor"])
    c1 = tuple(c * (1 + sp["var"]) for c in sp["cor"])
    cor = _mistura_cor(nt, c0, c1, manchas, -900, 600)
    medio = _ruido(nt, coord, 60.0, 4.0, -1200, 0)
    aspereza = _faixa(nt, medio, 0.3, 0.7, -900, 0, sp["aspereza"] - sp["var_asp"], sp["aspereza"] + sp["var_asp"])
    metal = sp["metal"]
    if sp["desgaste"] > 0:
        quebra = _faixa(nt, _ruido(nt, coord, 420.0, 4.0, -1200, -300), 0.50, 0.66, -1000, -300)
        fator = _vezes(nt, _vezes(nt, quina, quebra, -800, -300), sp["desgaste"], -650, -300)
        cor = _mistura_cor(nt, cor, COR_GASTA, fator, -500, 500)
        metal = _mistura(nt, metal, 0.95, fator, -500, -500)
        aspereza = _mistura(nt, aspereza, 0.30, fator, -500, -200)

    # o normal: as quinas arredondadas e o relevo
    normal = bev.outputs["Normal"]
    rel = sp["relevo"]
    if rel is not None:
        bump = _no(nt, "ShaderNodeBump", 800, -400)
        nt.links.new(bev.outputs["Normal"], bump.inputs["Normal"])
        altura, forca, dist = _altura_do_relevo(nt, coord, rel, -400, -700)
        if pontilhado and nome == "polimero":
            # a empunhadura: o polímero texturizado (mais escuro, áspero e pontilhado) dentro da região
            tx = SUPERFICIES["texturizado"]
            dentro = _regiao(nt, coord, pontilhado, -1400, -1100)
            cor = _mistura_cor(nt, cor, tx["cor"], dentro, -300, 650)
            aspereza = _mistura(nt, aspereza, tx["aspereza"], dentro, -300, -150)
            a2, f2, d2 = _altura_do_relevo(nt, coord, "pontilhado", -400, -900)
            altura = _mistura(nt, altura, a2, dentro, 200, -750)
            nt.links.new(_mistura(nt, forca, f2, dentro, 400, -500), bump.inputs["Strength"])
            nt.links.new(_mistura(nt, dist, d2, dentro, 400, -600), bump.inputs["Distance"])
        else:
            bump.inputs["Strength"].default_value = forca
            bump.inputs["Distance"].default_value = dist
        nt.links.new(altura, bump.inputs["Height"])
        normal = bump.outputs["Normal"]
    nt.links.new(normal, bsdf.inputs["Normal"])

    for rotulo, valor, y in (("cor", cor, 300), ("metal", metal, -300), ("aspereza", aspereza, -450)):
        r = _no(nt, "NodeReroute", 900, y)
        r.name = rotulo
        r.label = rotulo
        if isinstance(valor, (int, float)):
            v = _no(nt, "ShaderNodeValue", 700, y)
            v.outputs[0].default_value = valor
            valor = v.outputs[0]
        nt.links.new(valor, r.inputs[0])
    nt.links.new(nt.nodes["cor"].outputs[0], bsdf.inputs["Base Color"])
    nt.links.new(nt.nodes["metal"].outputs[0], bsdf.inputs["Metallic"])
    nt.links.new(nt.nodes["aspereza"].outputs[0], bsdf.inputs["Roughness"])
    return mat


# ------------------------------------------------------------------------------------------------ construir
def _colecao(nome):
    col = bpy.data.collections.get(nome)
    if col is None:
        col = bpy.data.collections.new(nome)
        bpy.context.scene.collection.children.link(col)
    return col


def objetos(cfg):
    return [bpy.data.objects[f"{cfg.NOME}_{p}"] for p in cfg.PECAS]


def construir(cfg):
    """Do modelo de fora às peças do jogo: carrega os objetos pedidos (sem os scripts do arquivo), põe cada face na
    superfície dela, junta por peça, leva ao espaço da arma (o giro, a escala e o deslocamento da cfg), triangula e
    acerta as normais (lisas até 35°, quinas vivas acima)."""
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    fonte = os.path.join(FONTES, cfg.FONTE)
    with bpy.data.libraries.load(fonte, link=False) as (src, dst):
        dst.objects = [n for n in src.objects if n in cfg.OBJETOS]
    carregados = {o.name: o for o in dst.objects if o is not None}
    faltam = set(cfg.OBJETOS) - set(carregados)
    if faltam:
        raise RuntimeError(f"Faltam no {cfg.FONTE}: {sorted(faltam)}")
    mats = [material(n, cfg.CHANFRO, getattr(cfg, "PONTILHADO", None)) for n in NOMES_SUPERFICIES]
    G = cfg.transformacao()
    col = _colecao(cfg.NOME)
    por_peca = {p: bmesh.new() for p in cfg.PECAS}
    for nome, o in carregados.items():
        peca = cfg.OBJETOS[nome]
        if hasattr(cfg, "ajustar"):
            cfg.ajustar(nome, o)
        razao = getattr(cfg, "DECIMAR", {}).get(nome)
        if razao:
            # as peças miúdas e cheias de faces: o Decimate (por colapso) na fração pedida
            bpy.context.scene.collection.objects.link(o)
            mod = o.modifiers.new("Decimar", "DECIMATE")
            mod.ratio = razao
            mod.use_collapse_triangulate = True
            dg = bpy.context.evaluated_depsgraph_get()
            me = bpy.data.meshes.new_from_object(o.evaluated_get(dg))
            bpy.context.scene.collection.objects.unlink(o)
        else:
            me = o.data.copy()
        me.transform(o.matrix_basis.copy())
        fontes_mat = [m.name if m else "" for m in me.materials]
        bm = bmesh.new()
        bm.from_mesh(me)
        for f in bm.faces:
            mf = fontes_mat[f.material_index] if f.material_index < len(fontes_mat) else ""
            sup = cfg.superficie(nome, mf, f.calc_center_median(), f.normal)
            if sup is None:
                f.tag = True   # a cfg tira esta face
            else:
                f.material_index = NOMES_SUPERFICIES.index(sup)
        tirar = [f for f in bm.faces if f.tag]
        if tirar:
            bmesh.ops.delete(bm, geom=tirar, context="FACES")
        bm.transform(cfg.transformacao_do(nome) if hasattr(cfg, "transformacao_do") else G)
        # as faces que a cfg manda para outra peça (por exemplo, a caixa do carregador que vem no receptor)
        destinos = {}
        if hasattr(cfg, "peca_da_face"):
            inv = (cfg.transformacao_do(nome) if hasattr(cfg, "transformacao_do") else G).inverted()
            for f in bm.faces:
                outra = cfg.peca_da_face(nome, inv @ f.calc_center_median())
                if outra and outra != peca:
                    destinos.setdefault(outra, set()).add(f.index)
        bm.faces.ensure_lookup_table()
        tmp = bpy.data.meshes.new("_tmp")
        for outra, indices in destinos.items():
            copia = bm.copy()
            copia.faces.ensure_lookup_table()
            bmesh.ops.delete(copia, geom=[f for f in copia.faces if f.index not in indices], context="FACES")
            copia.to_mesh(tmp)
            copia.free()
            por_peca[outra].from_mesh(tmp)
        if destinos:
            todas = set().union(*destinos.values())
            bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.index in todas], context="FACES")
        bm.to_mesh(tmp)
        bm.free()
        por_peca[peca].from_mesh(tmp)
        bpy.data.meshes.remove(tmp)
        bpy.data.meshes.remove(me)
        bpy.data.objects.remove(o, do_unlink=True)
    if hasattr(cfg, "extras"):
        cfg.extras({p: Extras(bm) for p, bm in por_peca.items()})
    objs = []
    for peca, bm in por_peca.items():
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
        if getattr(cfg, "TIRAR_ESCONDIDAS", False):
            n = tirar_escondidas(bm)
            print(f"  {peca}: {n} faces escondidas tiradas")
        if getattr(cfg, "DISSOLVER", 0) > 0:
            # as faces no mesmo plano (e no mesmo material) viram uma só: a forma não muda, os triângulos caem
            bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(cfg.DISSOLVER), verts=bm.verts, edges=bm.edges,
                                     delimit={"MATERIAL"})
        bmesh.ops.triangulate(bm, faces=bm.faces, quad_method="BEAUTY", ngon_method="BEAUTY")
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        nome = f"{cfg.NOME}_{peca}"
        velho = bpy.data.objects.get(nome)
        if velho is not None:
            bpy.data.objects.remove(velho, do_unlink=True)
        me = bpy.data.meshes.new(nome)
        bm.to_mesh(me)
        bm.free()
        for p in me.polygons:
            p.use_smooth = True
        me.set_sharp_from_angle(angle=math.radians(35))
        for m in mats:
            me.materials.append(m)
        o = bpy.data.objects.new(nome, me)
        col.objects.link(o)
        objs.append(o)
    cfg.depois_de_construir(objs) if hasattr(cfg, "depois_de_construir") else None
    for o in objs:
        t = sum(len(p.vertices) - 2 for p in o.data.polygons)
        print(f"  {o.name}: {t} triângulos, {len(o.data.vertices)} vértices")
    print("  total:", sum(len(o.data.polygons) for o in objs), "triângulos")
    return objs


def tirar_escondidas(bm, raios=64, alcance=2.0):
    """Tira as faces que ninguém vê: de cada face saem raios para todos os lados, e se todos batem na própria peça (que é
    rígida: o que a esconde anda junto), ela some. As que se veem por uma fresta ficam."""
    from mathutils.bvhtree import BVHTree
    arvore = BVHTree.FromBMesh(bm)
    ouro = math.pi * (3 - math.sqrt(5))
    direcoes = []
    for k in range(raios * 2):
        z = 1 - (k + 0.5) / raios
        if z < -1:
            break
        r = math.sqrt(max(0.0, 1 - z * z))
        direcoes.append(Vector((math.cos(ouro * k) * r, math.sin(ouro * k) * r, z)))
    tirar = []
    bm.faces.ensure_lookup_table()
    for f in bm.faces:
        n = f.normal
        if n.length < 0.5:
            continue
        # de vários pontos da face (o miolo e perto de cada canto: uma face comprida pode entrar debaixo de outra peça só
        # pela metade) e para os dois lados (há malhas com a normal virada para dentro)
        miolo = f.calc_center_median()
        pontos = [miolo] + [v.co.lerp(miolo, 0.15) for v in f.verts]
        visto = False
        for p0 in pontos:
            for d in direcoes:
                lado = 1.0 if d.dot(n) >= 0 else -1.0
                if abs(d.dot(n)) < 0.05:
                    continue
                ponto = p0 + n * (1e-4 * lado)
                # as batidas a menos de 1 mm não contam (superfícies sobrepostas: uma esconderia a outra)
                for _ in range(6):
                    hit = arvore.ray_cast(ponto, d, alcance)
                    if hit[0] is None or hit[3] >= 0.001:
                        break
                    ponto = hit[0] + d * 0.0011
                if hit[0] is None:
                    visto = True
                    break
            if visto:
                break
        if not visto:
            tirar.append(f)
    if tirar:
        bmesh.ops.delete(bm, geom=tirar, context="FACES")
        soltos = [v for v in bm.verts if not v.link_faces]
        bmesh.ops.delete(bm, geom=soltos, context="VERTS")
    return len(tirar)


class Extras:
    """Peças modeladas aqui (a luneta, uma empunhadura...), direto no espaço da arma (metros; o cano em -Y, o alto em +Z),
    cada face na superfície dela e com as quinas chanfradas."""

    def __init__(self, bm, nomes=None):
        self.bm = bm
        self.nomes = nomes or NOMES_SUPERFICIES   # as superfícies (as armas v1) ou as cores (as estilizadas)

    def _fim(self, vs, sup, chanfro, seg=1):
        faces = list({f for v in vs for f in v.link_faces})
        idx = self.nomes.index(sup)
        for f in faces:
            f.material_index = idx
        if chanfro > 0:
            arestas = list({e for f in faces for e in f.edges})
            r = bmesh.ops.bevel(self.bm, geom=arestas, offset=chanfro, segments=seg, affect="EDGES", profile=0.5,
                                clamp_overlap=True)
            for f in r.get("faces", []):
                f.material_index = idx

    def caixa(self, a, b, sup, chanfro=0.0006):
        """Uma caixa de um canto a outro."""
        vs = bmesh.ops.create_cube(self.bm, size=1.0)["verts"]
        c = [(a[i] + b[i]) / 2 for i in range(3)]
        t = [abs(b[i] - a[i]) for i in range(3)]
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation(Vector(c)) @ Matrix.Diagonal((*t, 1.0)), verts=vs)
        self._fim(vs, sup, chanfro if min(t) > chanfro * 2.6 else 0.0)

    def perfil_x(self, perfil, x0, x1, sup, chanfro=0.0008, seg=1):
        """Um polígono de lado (pontos (y, z)) puxado em X de x0 a x1."""
        a = [self.bm.verts.new((x0, y, z)) for y, z in perfil]
        b = [self.bm.verts.new((x1, y, z)) for y, z in perfil]
        n = len(perfil)
        self.bm.faces.new(a)
        self.bm.faces.new(list(reversed(b)))
        for i in range(n):
            j = (i + 1) % n
            self.bm.faces.new((a[i], a[j], b[j], b[i]))
        self._fim(a + b, sup, chanfro, seg)

    def tubo(self, eixo, centro, raio0, raio1, comp, sup, lados=20, chanfro=0.0):
        """Um cilindro (ou tronco de cone, de raio0 a raio1) no eixo "x", "y" ou "z", com o miolo em <centro>."""
        r = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=lados, radius1=raio0, radius2=raio1,
                                  depth=comp)
        vs = r["verts"]
        giro = {"y": Matrix.Rotation(math.radians(90), 4, "X"), "x": Matrix.Rotation(math.radians(90), 4, "Y"),
                "z": Matrix.Identity(4)}[eixo]
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation(Vector(centro)) @ giro, verts=vs)
        self._fim(vs, sup, chanfro)


def _selecionar(objs, ativo=None):
    for o in bpy.context.scene.objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = ativo or objs[0]


def desembrulhar(cfg):
    """Um atlas só para a arma: as ilhas de todas as peças juntas, na mesma escala, sem sobrepor."""
    objs = objetos(cfg)
    for o in objs:
        while o.data.uv_layers:
            o.data.uv_layers.remove(o.data.uv_layers[0])
        o.data.uv_layers.new(name="UVMap")
    _selecionar(objs)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(50), island_margin=0.0, area_weight=0.0, scale_to_bounds=False)
    bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.uv.average_islands_scale()
    bpy.ops.uv.pack_islands(rotate=True, margin=cfg.MARGEM_UV, shape_method="CONCAVE")
    bpy.ops.object.mode_set(mode="OBJECT")


# ------------------------------------------------------------------------------------------------ assar
def _imagem(nome, tam, cor):
    img = bpy.data.images.get(nome)
    if img is not None:
        bpy.data.images.remove(img)
    img = bpy.data.images.new(nome, tam, tam, alpha=False, float_buffer=False)
    if not cor:
        img.colorspace_settings.name = "Non-Color"
    img.use_fake_user = True
    return img


def _ligar_para_assar(mats, img, canal):
    """Em cada material: a imagem ativa (o alvo) e, para os canais por emissão, o valor do canal na saída."""
    for mat in mats:
        nt = mat.node_tree
        for n in [n for n in nt.nodes if n.name.startswith("_assar")]:
            nt.nodes.remove(n)
        saida = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
        if canal in ("cor", "metal", "aspereza"):
            em = _no(nt, "ShaderNodeEmission", 1100, -700)
            em.name = "_assar_emissao"
            nt.links.new(nt.nodes[canal].outputs[0], em.inputs["Color"])
            nt.links.new(em.outputs[0], saida.inputs["Surface"])
        else:
            nt.links.new(nt.nodes["BSDF"].outputs[0], saida.inputs["Surface"])
        tex = _no(nt, "ShaderNodeTexImage", 1100, -900)
        tex.name = "_assar_imagem"
        tex.image = img
        nt.nodes.active = tex


def _desligar(mats):
    for mat in mats:
        nt = mat.node_tree
        for n in [n for n in nt.nodes if n.name.startswith("_assar")]:
            nt.nodes.remove(n)
        saida = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
        nt.links.new(nt.nodes["BSDF"].outputs[0], saida.inputs["Surface"])


def assar(cfg, canais=("cor", "metal", "aspereza", "ao", "normal")):
    cena = bpy.context.scene
    cena.render.engine = "CYCLES"
    cena.cycles.device = "CPU"
    cena.render.bake.margin = 8
    cena.render.bake.margin_type = "EXTEND"
    cena.render.bake.use_clear = True
    if cena.world is None:
        cena.world = bpy.data.worlds.new("Mundo")
    cena.world.light_settings.distance = cfg.DISTANCIA_AO
    objs = objetos(cfg)
    mats = [bpy.data.materials["Arma_" + n] for n in NOMES_SUPERFICIES]
    tam = cfg.TAMANHO
    for canal in canais:
        img = _imagem(f"{cfg.NOME}_assado_{canal}", tam, canal == "cor")
        _ligar_para_assar(mats, img, canal)
        _selecionar(objs)
        if canal == "ao":
            cena.cycles.samples = 128
            bpy.ops.object.bake(type="AO", margin=8)
        elif canal == "normal":
            cena.cycles.samples = 48
            bpy.ops.object.bake(type="NORMAL", normal_space="TANGENT", margin=8)
        else:
            cena.cycles.samples = 32
            bpy.ops.object.bake(type="EMIT", margin=8)
        px = img.pixels[:]
        media = sum(px[0::97]) / len(px[0::97])
        print(f"  assado: {canal} (média {media:.3f})")
        if media < 0.002:
            raise RuntimeError(f"O passe {canal} saiu vazio.")
    _desligar(mats)


def _srgb_para_linear(c):
    import numpy as np
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def _linear_para_srgb(c):
    import numpy as np
    c = np.clip(c, 0.0, 1.0)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1 / 2.4) - 0.055)


def compor(cfg):
    """As três texturas da Unity: <Arma>_Cor (a cor com um pouco do AO), <Arma>_MS (o metal no R, o AO no G e a lisura
    no A, como o URP Lit lê) e <Arma>_Normal."""
    import numpy as np
    os.makedirs(TEXTURAS, exist_ok=True)
    tam = cfg.TAMANHO
    n = tam * tam

    def ler(canal):
        return np.array(bpy.data.images[f"{cfg.NOME}_assado_{canal}"].pixels[:], dtype=np.float32).reshape(n, 4)

    ao = ler("ao")[:, 0]
    cor = ler("cor")
    lin = _srgb_para_linear(cor[:, :3]) * (1.0 - cfg.AO_NA_COR + cfg.AO_NA_COR * ao)[:, None]
    saida = np.ones((n, 4), dtype=np.float32)
    saida[:, :3] = _linear_para_srgb(lin)
    _gravar(f"{cfg.NOME}_Cor", tam, saida, True)
    ms = np.zeros((n, 4), dtype=np.float32)
    ms[:, 0] = ler("metal")[:, 0]
    ms[:, 1] = ao
    ms[:, 3] = 1.0 - ler("aspereza")[:, 0]
    _gravar(f"{cfg.NOME}_MS", tam, ms, False, alfa=True)
    _gravar(f"{cfg.NOME}_Normal", tam, ler("normal"), False)


def _gravar(nome, tam, pixels, cor, alfa=False):
    img = bpy.data.images.get(nome)
    if img is not None:
        bpy.data.images.remove(img)
    img = bpy.data.images.new(nome, tam, tam, alpha=alfa, float_buffer=False)
    if not cor:
        img.colorspace_settings.name = "Non-Color"
    img.pixels.foreach_set(pixels.ravel())
    img.filepath_raw = os.path.join(TEXTURAS, nome + ".png")
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    print("  gravada:", nome)


# ------------------------------------------------------------------------------------------------ o material final, exportar
def material_final(cfg):
    """O material que vai para o FBX (<Arma>_Textura): as texturas gravadas num BSDF, como a Unity vai mostrar."""
    nome = f"{cfg.NOME}_Textura"
    mat = bpy.data.materials.get(nome) or bpy.data.materials.new(nome)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    saida = _no(nt, "ShaderNodeOutputMaterial", 700, 0)
    bsdf = _no(nt, "ShaderNodeBsdfPrincipled", 400, 0)
    nt.links.new(bsdf.outputs[0], saida.inputs["Surface"])

    def tex(arquivo, cor, y):
        t = _no(nt, "ShaderNodeTexImage", -400, y)
        caminho = os.path.join(TEXTURAS, arquivo)
        t.image = bpy.data.images.load(caminho, check_existing=True)
        t.image.reload()
        if not cor:
            t.image.colorspace_settings.name = "Non-Color"
        return t

    c = tex(f"{cfg.NOME}_Cor.png", True, 300)
    nt.links.new(c.outputs["Color"], bsdf.inputs["Base Color"])
    ms = tex(f"{cfg.NOME}_MS.png", False, 0)
    sep = _no(nt, "ShaderNodeSeparateColor", -100, 0)
    nt.links.new(ms.outputs["Color"], sep.inputs[0])
    nt.links.new(sep.outputs[0], bsdf.inputs["Metallic"])
    inv = _no(nt, "ShaderNodeMath", 100, -150, operation="SUBTRACT")
    inv.inputs[0].default_value = 1.0
    nt.links.new(ms.outputs["Alpha"], inv.inputs[1])
    nt.links.new(inv.outputs[0], bsdf.inputs["Roughness"])
    nm = tex(f"{cfg.NOME}_Normal.png", False, -350)
    mapa = _no(nt, "ShaderNodeNormalMap", 100, -400)
    nt.links.new(nm.outputs["Color"], mapa.inputs["Color"])
    nt.links.new(mapa.outputs["Normal"], bsdf.inputs["Normal"])
    nt.nodes.active = c
    return mat


def exportar(cfg):
    """O <Arma>.fbx: as peças com o material final, no formato do Armas.fbx (cada uma girada 90° em X, com a malha nos
    eixos da Unity: o cano em +Z, o alto em +Y)."""
    os.makedirs(MODELOS, exist_ok=True)
    mat = material_final(cfg)
    giro = Matrix.Rotation(math.radians(-90), 4, "X")
    originais = objetos(cfg)
    copias = []
    for o in originais:
        me = o.data.copy()
        me.transform(giro)
        me.materials.clear()
        me.materials.append(mat)
        c = bpy.data.objects.new(o.name + "_exportar", me)
        c.rotation_euler = (math.radians(90), 0, 0)
        bpy.context.scene.collection.objects.link(c)
        copias.append(c)
    for o in originais:
        o.name = o.name + "_autoria"
    for c in copias:
        c.name = c.name.replace("_exportar", "")
    _selecionar(copias)
    caminho = os.path.join(MODELOS, f"{cfg.NOME}.fbx")
    bpy.ops.export_scene.fbx(filepath=caminho, use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", bake_anim=False, mesh_smooth_type="OFF",
                             use_tspace=False, path_mode="STRIP")
    for c in copias:
        me = c.data
        bpy.data.objects.remove(c, do_unlink=True)
        bpy.data.meshes.remove(me)
    for o in originais:
        o.name = o.name.replace("_autoria", "")
    print("  exportado:", caminho)


# ------------------------------------------------------------------------------------------------ as fotos
def fotos(cfg, vistas=None, amostras=96, largura=1400, altura=900, luz_fator=0.22, usar_final=True):
    """As fotos de conferência (Cycles, a luz de estúdio e o fundo cinza-escuro) com o material final (ou, nas
    estilizadas, os materiais de cor de cada face), em Docs/Armas."""
    os.makedirs(CAPTURAS, exist_ok=True)
    cena = bpy.context.scene
    objs = objetos(cfg)
    for o in bpy.data.objects:
        if o.type == "MESH" and o not in objs:
            o.hide_render = True
    salvos = {o.name: list(o.data.materials) for o in objs}
    if usar_final:
        mat = material_final(cfg)
        for o in objs:
            o.data.materials.clear()
            o.data.materials.append(mat)
    r = cena.render
    r.engine = "CYCLES"
    cena.cycles.device = "CPU"
    cena.cycles.samples = amostras
    cena.cycles.use_denoising = True
    r.resolution_x, r.resolution_y = largura, altura
    r.film_transparent = False
    r.image_settings.file_format = "PNG"
    r.image_settings.color_mode = "RGB"
    cena.view_settings.view_transform = "AgX"
    cena.view_settings.look = "AgX - Medium High Contrast"
    if cena.world is None:
        cena.world = bpy.data.worlds.new("Mundo")
    w = cena.world
    w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (0.022, 0.022, 0.023, 1)
    bg.inputs["Strength"].default_value = 1.0

    cantos = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    mn = Vector([min(c[i] for c in cantos) for i in range(3)])
    mx = Vector([max(c[i] for c in cantos) for i in range(3)])
    centro = (mn + mx) / 2
    tam = (mx - mn).length

    luzes = []

    def luz(nome, pos, energia, tamanho, cor=(1, 1, 1)):
        l = bpy.data.lights.new(nome, "AREA")
        l.energy = energia
        l.size = tamanho
        l.color = cor
        o = bpy.data.objects.new(nome, l)
        o.location = centro + Vector(pos) * tam
        o.rotation_euler = (centro - o.location).to_track_quat("-Z", "Y").to_euler()
        cena.collection.objects.link(o)
        luzes.append(o)

    k = tam * tam * luz_fator
    luz("Chave", (-1.2, -0.6, 1.4), 260 * k, tam * 1.2)
    luz("Recorte", (1.3, 1.2, 0.9), 220 * k, tam * 0.8, (0.92, 0.95, 1.0))
    luz("Enchimento", (-0.6, 1.5, -0.4), 60 * k, tam * 1.5)
    luz("Topo", (0.0, 0.0, 2.0), 90 * k, tam * 1.5)

    cam = bpy.data.objects.get("CameraDasFotos")
    if cam is None:
        cam = bpy.data.objects.new("CameraDasFotos", bpy.data.cameras.new("CameraDasFotos"))
        cena.collection.objects.link(cam)
    cena.camera = cam
    cam.data.lens = 85
    cam.data.clip_start = 0.005
    cam.data.clip_end = 50
    vistas = vistas or {
        # (direção da câmera a partir do centro, folga)
        "lado_direito": ((-1.0, 0.0, 0.08), 1.0),
        "lado_esquerdo": ((1.0, 0.0, 0.08), 1.0),
        "frente": ((0.0, -1.0, 0.05), 1.0),
        "tres_quartos": ((-0.85, -0.75, 0.45), 1.0),
        "tres_quartos_tras": ((0.8, 0.9, 0.55), 1.0),
        "cima": ((-0.15, 0.25, 1.0), 1.0),
    }
    for nome, (d, folga) in vistas.items():
        d = Vector(d).normalized()
        # a distância que cabe a arma no quadro (a lente de 85 mm, o lado mais largo)
        dist = tam * 1.6 * folga / (36.0 / 85.0) * 0.80
        cam.location = centro + d * dist
        cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
        r.filepath = os.path.join(CAPTURAS, f"{getattr(cfg, 'FOTO', cfg.NOME).lower()}_{nome}.png")
        bpy.ops.render.render(write_still=True)
        print("  foto:", nome)
    for o in luzes:
        l = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.lights.remove(l)
    if usar_final:
        for o in objs:
            o.data.materials.clear()
            for m in salvos[o.name]:
                o.data.materials.append(m)


def blend(cfg):
    return os.path.join(AQUI, f"{cfg.NOME}.blend")


def salvar(cfg):
    bpy.ops.wm.save_as_mainfile(filepath=blend(cfg), relative_remap=True)
    print("  salvo:", blend(cfg))


# ------------------------------------------------------------------------------------------------ o rascunho
RASCUNHO = {
    "pintado": (0.85, 0.15, 0.15), "grafite": (0.25, 0.45, 0.95), "exposto": (0.85, 0.85, 0.9), "polimero": (0.2, 0.75, 0.25),
    "texturizado": (0.95, 0.85, 0.1), "borracha": (0.55, 0.3, 0.1), "lente": (0.1, 0.9, 0.9), "vidro": (0.6, 0.9, 1.0),
    "interno": (0.5, 0.1, 0.6), "latao": (0.9, 0.6, 0.2), "branco": (1, 1, 1), "cartucho": (1.0, 0.4, 0.6),
}


def rascunho(cfg):
    """Fotos rápidas (Workbench) com cada superfície numa cor forte, para conferir a divisão (em Docs/Armas/rascunho)."""
    pasta = os.path.join(REPO, "Docs", "Armas", "rascunho")
    os.makedirs(pasta, exist_ok=True)
    for n, c in RASCUNHO.items():
        m = bpy.data.materials.get("Arma_" + n)
        if m is not None:
            m.diffuse_color = (*c, 1.0)
    cena = bpy.context.scene
    r = cena.render
    r.engine = "BLENDER_WORKBENCH"
    s = cena.display.shading
    s.light = "STUDIO"
    s.color_type = "MATERIAL"
    s.show_cavity = True
    cena.view_settings.view_transform = "Standard"
    r.resolution_x, r.resolution_y = 1200, 760
    objs = objetos(cfg)
    for o in bpy.data.objects:
        if o.type == "MESH" and o not in objs:
            o.hide_render = not o.name.startswith("Ref_Maos")
    cantos = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    mn = Vector([min(c[i] for c in cantos) for i in range(3)])
    mx = Vector([max(c[i] for c in cantos) for i in range(3)])
    centro = (mn + mx) / 2
    tam = max(mx - mn)
    cam = bpy.data.objects.get("CameraDoRascunho") or bpy.data.objects.new("CameraDoRascunho", bpy.data.cameras.new("CameraDoRascunho"))
    if cam.name not in cena.collection.objects:
        cena.collection.objects.link(cam)
    cena.camera = cam
    cam.data.type = "ORTHO"
    for nome, d in (("direita", (-1, 0, 0.05)), ("esquerda", (1, 0, 0.05)), ("34", (-0.8, -0.7, 0.5)), ("tras", (0.7, 0.9, 0.6)),
                    ("baixo", (-0.3, 0.1, -1.0)), ("cima", (-0.2, 0.2, 1.0))):
        d = Vector(d).normalized()
        cam.location = centro + d * tam * 3
        cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
        cam.data.ortho_scale = tam * 1.08
        cam.data.clip_end = tam * 10
        r.filepath = os.path.join(pasta, f"{cfg.NOME.lower()}_{nome}.png")
        bpy.ops.render.render(write_still=True)
    print("  rascunho em", pasta)


# ------------------------------------------------------------------------------------------------ as referências
JOGADOR = os.path.join(REPO, "Assets", "_Game", "Art", "Characters", "Jogador")


def _caixa(vs):
    return (Vector([min(v[i] for v in vs) for i in range(3)]), Vector([max(v[i] for v in vs) for i in range(3)]))


def encaixe(destino, origem):
    """O giro de 90 em 90 graus (sem espelho) e o deslocamento que levam a forma <origem> para <destino> (pela caixa e
    pelo miolo, como a MontagemDaPistola da Unity). Devolve a matriz e o erro (m)."""
    import itertools

    def medir(vs):
        mn, mx = _caixa(vs)
        c = (mn + mx) / 2
        return mn, mx, c, sum(vs, Vector()) / len(vs) - c

    mnd, mxd, cd, pd = medir(destino)
    mno, mxo, co, po = medir(origem)
    melhor = None
    for perm in itertools.permutations(range(3)):
        for sinais in itertools.product((1, -1), repeat=3):
            R = Matrix.Identity(3)
            R.zero()
            for j in range(3):
                R[perm[j]][j] = sinais[j]
            if R.determinant() < 0:
                continue
            tam = Vector([abs(x) for x in R @ (mxo - mno)])
            erro = (tam - (mxd - mnd)).length + (R @ po - pd).length
            if melhor is None or erro < melhor[0]:
                melhor = (erro, R)
    erro, R = melhor
    return Matrix.Translation(cd - R @ co) @ R.to_4x4(), erro


def referencias(cfg):
    """Os braços de primeira pessoa (a pose de repouso, que é a de segurar) e as peças da arma antiga levados para o
    espaço da arma, na coleção "Referências" (fora do FBX e das fotos): para conferir o cabo, a mão de apoio e onde cada
    peça que mexe estava. Devolve as caixas das peças antigas (por osso) e o miolo de cada mão."""
    col = _colecao("Referências")
    for o in list(col.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(JOGADOR, f"Jogador_FP_{cfg.MODELO}.fbx"))
    fp = [o for o in bpy.data.objects if o not in antes]
    bracos = next(o for o in fp if o.type == "MESH" and o.name.startswith("FP_Bracos"))
    me = bracos.data
    grupos = {g.index: g.name for g in bracos.vertex_groups}

    def osso(v):
        return grupos[max(v.groups, key=lambda g: g.weight).group] if len(v.groups) else ""

    mw = bracos.matrix_world.copy()
    # o encaixe é medido na malha crua (a pose de repouso, como a Unity faz); as mãos vêm da malha deformada pela armadura
    # (a pose de segurar), levadas de volta pelo giro do osso "gun" (a arma inteira é rígida nele)
    dg = bpy.context.evaluated_depsgraph_get()
    pose = bracos.evaluated_get(dg).to_mesh()
    co_pose = [mw @ v.co for v in pose.vertices]
    bracos.evaluated_get(dg).to_mesh_clear()
    arm = next(o for o in fp if o.type == "ARMATURE")
    pb = arm.pose.bones["gun"]
    D = arm.matrix_world @ pb.matrix @ pb.bone.matrix_local.inverted() @ arm.matrix_world.inverted()
    Dinv = D.inverted()
    da_arma = {i for p in me.polygons if p.material_index == 1 for i in p.vertices}
    destino = [mw @ me.vertices[i].co for i in da_arma if osso(me.vertices[i]) != "shell"]
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(JOGADOR, cfg.FBX_ANTIGO))
    soltas = [o for o in bpy.data.objects if o not in antes]
    antigas = [o for o in soltas if o.type == "MESH" and o.name.startswith(cfg.MODELO + "_")]
    org = next(o for o in antigas if o.name.startswith(cfg.MODELO + "_corpo")).matrix_world.translation.copy()
    origem = [o.matrix_world @ v.co - org for o in antigas for v in o.data.vertices]
    M, erro = encaixe(destino, origem)
    para_arma = M.inverted()
    print(f"  encaixe da antiga nos braços: {erro * 1000:.2f} mm")
    # as mãos (e tudo da pose) no espaço da arma; as peças antigas, no repouso
    co = [para_arma @ Dinv @ c for c in co_pose]
    cru = [para_arma @ mw @ v.co for v in me.vertices]

    def malha(nome, faces, cor, fonte):
        bm = bmesh.new()
        mapa = {}
        for p in faces:
            vs = []
            for i in p.vertices:
                if i not in mapa:
                    mapa[i] = bm.verts.new(fonte[i])
                vs.append(mapa[i])
            try:
                bm.faces.new(vs)
            except ValueError:
                pass
        m = bpy.data.meshes.new(nome)
        bm.to_mesh(m)
        bm.free()
        mat = bpy.data.materials.get(nome) or bpy.data.materials.new(nome)
        mat.diffuse_color = (*cor, 1.0)
        m.materials.append(mat)
        o = bpy.data.objects.new(nome, m)
        col.objects.link(o)
        return o

    malha("Ref_Maos", [p for p in me.polygons if p.material_index == 0], (0.85, 0.55, 0.40), co)
    caixas = {}
    por_osso = {}
    for p in me.polygons:
        if p.material_index == 1:
            por_osso.setdefault(osso(me.vertices[p.vertices[0]]), []).append(p)
    for nome_osso, faces in por_osso.items():
        o = malha(f"Ref_Antiga_{nome_osso}", faces, (0.9, 0.2, 0.2), cru)
        o.hide_render = True
        vs = [v.co for v in o.data.vertices]
        caixas[nome_osso] = _caixa(vs)
    # o miolo de cada mão (a palma e os dedos)
    maos = {}
    for lado in ("R", "L"):
        vs = [co[v.index] for v in me.vertices if osso(v).endswith("." + lado) and osso(v).split(".")[0] in
              ("hand", "index1", "index2", "middle1", "middle2", "ring1", "ring2", "pinky1", "pinky2", "thumb", "thumb2")]
        if vs:
            maos[lado] = (sum(vs, Vector()) / len(vs), _caixa(vs))
    for o in fp + soltas:
        if o.name in bpy.data.objects:
            bpy.data.objects.remove(o, do_unlink=True)
    for k, (mn, mx) in caixas.items():
        print(f"  antiga, {k}: y {mn.y:.3f}..{mx.y:.3f}  z {mn.z:.3f}..{mx.z:.3f}  x {mn.x:.3f}..{mx.x:.3f}")
    for lado, (c, (mn, mx)) in maos.items():
        print(f"  mão {lado}: miolo ({c.x:.3f}, {c.y:.3f}, {c.z:.3f})  y {mn.y:.3f}..{mx.y:.3f}  z {mn.z:.3f}..{mx.z:.3f}")
    return caixas, maos


# ------------------------------------------------------------------------------------------------ o ícone e a foto do inventário
ICONES = os.path.join(REPO, "Assets", "_Game", "Art", "UI", "Icones")


def imagens(cfg, usar_final=True):
    """O ícone (de lado, o cano para a direita, no tamanho do de antes: cfg.ICONE) e a foto (600 x 500, de 3/4) do
    inventário, no estilo dos outros (o Workbench com a textura, a cavidade, o contorno e o fundo transparente). O
    cartucho da espingarda fica de fora. Nas estilizadas, as cores dos materiais de cada face."""
    cena = bpy.context.scene
    objs = [o for o in objetos(cfg) if not o.name.endswith("_cartucho")]
    for o in bpy.data.objects:
        if o.type == "MESH":
            o.hide_render = o not in objs
    salvos = {o.name: list(o.data.materials) for o in objs}
    if usar_final:
        mat = material_final(cfg)
        for o in objs:
            o.data.materials.clear()
            o.data.materials.append(mat)
    r = cena.render
    r.engine = "BLENDER_WORKBENCH"
    r.film_transparent = True
    r.image_settings.file_format = "PNG"
    r.image_settings.color_mode = "RGBA"
    s = cena.display.shading
    s.light = "STUDIO"
    s.color_type = "TEXTURE" if usar_final else "MATERIAL"
    s.show_cavity = True
    s.cavity_type = "BOTH"
    s.show_object_outline = True
    s.object_outline_color = (0.05, 0.05, 0.05)
    cena.view_settings.view_transform = "Standard"
    cena.view_settings.look = "None"
    cantos = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    mn = Vector([min(c[i] for c in cantos) for i in range(3)])
    mx = Vector([max(c[i] for c in cantos) for i in range(3)])
    centro = (mn + mx) / 2
    tam = max(mx - mn)
    cam = bpy.data.objects.get("CameraDoIcone")
    if cam is None:
        cam = bpy.data.objects.new("CameraDoIcone", bpy.data.cameras.new("CameraDoIcone"))
        cena.collection.objects.link(cam)
    cena.camera = cam
    cam.data.clip_start = 0.01
    cam.data.clip_end = 50

    def apontar(d, dist):
        d = Vector(d).normalized()
        cam.location = centro + d * dist
        cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()

    # o ícone: de lado, pelo lado direito da arma (-X), o cano (-Y) para a direita da imagem
    w, h = cfg.ICONE
    r.resolution_x, r.resolution_y = w, h
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = max(mx.y - mn.y, (mx.z - mn.z) * w / h) * 1.06
    apontar((-1.0, 0.0, 0.05), tam * 3)
    r.filepath = os.path.join(ICONES, f"Icone_{cfg.MODELO}.png")
    bpy.ops.render.render(write_still=True)
    # a foto: de 3/4 (as longas, mais de lado, como a antiga do rifle), em perspectiva, com a arma enchendo o quadro
    r.resolution_x, r.resolution_y = 600, 500
    cam.data.type = "PERSP"
    cam.data.lens = 50
    from bpy_extras.object_utils import world_to_camera_view
    d = (-1.0, -0.45, 0.35) if tam < 0.4 else (-1.0, -0.22, 0.30)
    dist = tam * 1.5
    for _ in range(6):
        apontar(d, dist)
        bpy.context.view_layer.update()
        proj = [world_to_camera_view(cena, cam, c) for c in cantos]
        folga = max(max(abs(p.x - 0.5) for p in proj) / 0.45, max(abs(p.y - 0.5) for p in proj) / 0.42)
        dist *= folga
    apontar(d, dist)
    r.filepath = os.path.join(ICONES, f"Foto_{cfg.MODELO}.png")
    bpy.ops.render.render(write_still=True)
    if usar_final:
        for o in objs:
            o.data.materials.clear()
            for m in salvos[o.name]:
                o.data.materials.append(m)
    print("  ícone e foto:", cfg.MODELO)
