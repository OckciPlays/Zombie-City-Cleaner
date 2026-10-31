# O rifle v3 (30/10/2026, a pedido: "melhorar o rifle, seguindo o mesmo conceito da sniper (...) utilizar essas imagens
# como referências (...) próximo a ~3 mil triângulos"; das duas imagens, a escolhida foi a M4A1 com a alça de transporte):
# o AR-15 que o Pedro mandou (Fontes/cc_iur.blend), fiel, nas cores chapadas da paleta das estilizadas, com o que a M4A1
# da imagem tem e o AR-15 não: a alça de transporte (com a mira de trás, as orelhas, o tambor e os parafusos), o anel do
# guarda-mão (o "delta ring"), o guarda-mão redondo canelado, a tampa dele e a massa de mira em "A" (com as orelhas, a
# alça da baioneta), sobre o bloco de gases do cano. Do AR-15 saem as miras rebatíveis e o guarda-mão de trilhos (o
# receptor de cima é cortado na frente, em CORTE_X). As peças da fonte passam pelo Decimate (DECIMAR) antes de tudo.
#
# O encaixe é o da M4 v1 (arma_rifle.py): a escala 0,12 (1,2 vez o tamanho real), a boca do cano no ponto da antiga, a
# empunhadura na mão direita e o guarda-mão na esquerda. As peças: o corpo ("gun"), a alavanca de manejo ("slide") e o
# carregador ("mag").
#
# As peças modeladas aqui ficam no espaço do AR-15 (a boca em +X, a esquerda em +Y, o alto em +Z; 1 = 10 cm de verdade)
# e passam pela mesma transformação. O eixo do cano fica em z EIXO.
#
# Como rodar: blender -b --factory-startup --disable-autoexec --python Blender/Armas/rodar_v2.py -- v3_m4 tudo

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

import armas
import arma_rifle as v1
import estilizadas
from v3_mosin import _tubo_aros

NOME = "Rifle3"
FOTO = "rifle_v3"
MODELO = "Rifle"
FBX_ANTIGO = v1.FBX_ANTIGO
FONTE = v1.FONTE
PECAS = ["corpo", "ferrolho", "carregador"]
ICONE = v1.ICONE
# sem as miras rebatíveis (a alça e a massa de mira são modeladas), sem o quebra-chamas (1.294 triângulos: o daqui é
# um tubo de 8 lados) e sem a alavanca de manejo ambidestra (com o Decimate, a trava grande virava lascas: a daqui é o
# "T" clássico da M4A1)
OBJETOS = {k: v for k, v in v1.OBJETOS.items()
           if k not in ("iur iron sights", "iur flash hider", "iur charging handle")}
ANGULO_LISO = 37          # as peças modeladas aqui: lisas até 37° (o guarda-mão de 10 lados fica redondo)
# as peças do AR-15: quinas vivas acima de 12° (facetadas: com o Decimate forte, liso até 30° parecia massinha)
ANGULO_LISO_FONTE = 12
EIXO = 0.2198              # a altura do eixo do cano no AR-15
CORTE_X = -0.02            # o receptor de cima acaba aqui (na frente começava o guarda-mão de trilhos)
# a fração que fica de cada peça da fonte (o Decimate por colapso)
DECIMAR = {
    "iur lower": 0.3,
    "iur upper": 0.3,
    "iur 15,7in barrel": 0.4,
    "iur dust cover": 0.1,
    "iur bolt catch": 0.5,
    "iur mag catch": 0.2,
    "iur selector": 0.1,
    "iur trigga": 0.6,
    "iur grip": 0.25,
    "iur stock": 0.35,
    "ar15 30rnd mag empty (steel)": 0.25,
}
# o guarda-mão canelado: de X0 a X1, os gomos no raio R_GOMO e os sulcos (em V) no R_SULCO
GUARDA_X0, GUARDA_X1 = 0.12, 1.90
R_GOMO, R_SULCO = 0.300, 0.272
GOMOS = 8
LARGURA_SULCO = 0.06
LADOS = 10


def transformacao():
    return v1.transformacao()


def cor_da_face(obj, mat, c):
    """Os materiais do AR-15 (h-190: o preto; stell: o aço; polymer: o polímero) nas cores da paleta, como na M4A1 da
    imagem: o receptor e o metal pretos, o cano grafite, a coronha, o punho e o guarda-mão de polímero cinza."""
    if obj == "iur grip":
        return "polimero"
    if obj == "iur stock":
        return "polimero" if mat == "polymer" else "preto"
    if obj == "iur 15,7in barrel":
        return "grafite" if mat == "stell" else "preto"
    if obj == "ar15 30rnd mag empty (steel)":
        return "preto_fosco"
    if mat == "polymer":
        return "polimero"
    if mat == "stell":
        return "aco"
    return "preto"


def _perfil_y(m, pontos, y0, y1, cor, chanfro=0.0):
    """Um polígono de lado (pontos (x, z)) puxado em Y de y0 a y1."""
    a = [m.bm.verts.new((x, y0, z)) for x, z in pontos]
    b = [m.bm.verts.new((x, y1, z)) for x, z in pontos]
    n = len(pontos)
    m.bm.faces.new(a)
    m.bm.faces.new(list(reversed(b)))
    for i in range(n):
        j = (i + 1) % n
        m.bm.faces.new((a[i], a[j], b[j], b[i]))
    m._fim(a + b, cor, chanfro)


def _torno(m, perfil, cor, lados=LADOS):
    """Um sólido de revolução em volta do eixo do cano: o perfil é uma lista de (x, raio)."""
    aneis = []
    for x, r in perfil:
        aneis.append([m.bm.verts.new((x, r * math.sin(2 * math.pi * k / lados), EIXO - r * math.cos(2 * math.pi * k / lados)))
                      for k in range(lados)])
    faces = [m.bm.faces.new(list(reversed(aneis[0]))), m.bm.faces.new(aneis[-1])]
    for a, b in zip(aneis, aneis[1:]):
        for k in range(lados):
            j = (k + 1) % lados
            faces.append(m.bm.faces.new((a[k], a[j], b[j], b[k])))
    idx = m.nomes.index(cor)
    for f in faces:
        f.material_index = idx


def guarda_mao(m):
    """O anel (delta ring), o guarda-mão redondo com os gomos e a tampa da frente."""
    _tubo_aros(m, "x", (0.045, 0.0, EIXO), 0.315, 0.315, 0.15, "preto", LADOS, 0.010)
    passo = (GUARDA_X1 - GUARDA_X0) / GOMOS
    perfil = [(GUARDA_X0, R_GOMO)]
    for k in range(1, GOMOS):
        gx = GUARDA_X0 + k * passo
        perfil += [(gx - LARGURA_SULCO / 2, R_GOMO), (gx, R_SULCO), (gx + LARGURA_SULCO / 2, R_GOMO)]
    perfil.append((GUARDA_X1, R_GOMO))
    _torno(m, perfil, "polimero")
    _tubo_aros(m, "x", (1.935, 0.0, EIXO), 0.305, 0.305, 0.07, "preto", LADOS, 0.0)


def quebra_chamas(m):
    """O quebra-chamas (o "birdcage" da M4): um tubo de 8 lados com a ponta chanfrada, no lugar do da fonte."""
    _tubo_aros(m, "x", (3.995, 0.0, EIXO), 0.105, 0.105, 0.50, "preto", 8, 0.012)


def massa_de_mira(m):
    """A massa de mira em "A" sobre o bloco de gases: o colar no cano, as duas pernas, o alto com as orelhas e a massa,
    e a alça da baioneta embaixo."""
    m.caixa((1.97, -0.13, 0.06), (2.42, 0.13, 0.47), "preto", chanfro=0.010)
    _perfil_y(m, [(2.00, 0.46), (2.12, 0.46), (2.21, 0.86), (2.13, 0.86)], -0.065, 0.065, "preto")
    _perfil_y(m, [(2.30, 0.46), (2.42, 0.46), (2.29, 0.86), (2.21, 0.86)], -0.065, 0.065, "preto")
    m.caixa((2.11, -0.075, 0.84), (2.31, 0.075, 0.91), "preto", chanfro=0.0)
    for lado in (-1, 1):
        m.caixa((2.15, lado * 0.045, 0.90), (2.27, lado * 0.075, 0.98), "preto", chanfro=0.0)
    m.caixa((2.19, -0.013, 0.90), (2.23, 0.013, 0.965), "preto", chanfro=0.0)
    m.caixa((2.20, -0.03, 0.00), (2.40, 0.03, 0.08), "preto", chanfro=0.0)


def alca(m):
    """A alça de transporte no trilho de cima: a base, a caixa da mira de trás com as orelhas, a janela da mira e o tambor
    do lado direito, a barra da alça, a perna da frente (inclinada) e os dois parafusos do lado esquerdo."""
    m.caixa((-1.74, -0.105, 0.535), (-0.08, 0.105, 0.62), "preto", chanfro=0.0)
    m.caixa((-1.74, -0.105, 0.62), (-1.25, 0.105, 0.90), "preto", chanfro=0.010)
    for lado in (-1, 1):
        m.caixa((-1.68, lado * 0.05, 0.89), (-1.42, lado * 0.09, 1.00), "preto", chanfro=0.0)
    m.caixa((-1.58, -0.04, 0.89), (-1.52, 0.04, 0.96), "preto", chanfro=0.0)
    m.tubo("y", (-1.55, -0.125, 0.78), 0.055, 0.055, 0.04, "preto", lados=8)
    m.caixa((-1.25, -0.09, 0.87), (-0.28, 0.09, 0.96), "preto", chanfro=0.010)
    _perfil_y(m, [(-0.40, 0.62), (-0.08, 0.62), (-0.18, 0.96), (-0.30, 0.96)], -0.09, 0.09, "preto", chanfro=0.008)
    for x in (-1.05, -0.55):
        m.tubo("y", (x, 0.13, 0.58), 0.045, 0.045, 0.05, "preto", lados=6)


def alavanca(m):
    """A alavanca de manejo em "T" (a peça "ferrolho", que anda na recarga): a barra de trás, a trava do lado esquerdo
    e a haste que entra no receptor."""
    m.caixa((-1.99, -0.17, 0.41), (-1.87, 0.17, 0.50), "preto", chanfro=0.010)
    m.caixa((-1.96, 0.17, 0.42), (-1.87, 0.23, 0.49), "preto", chanfro=0.0)
    m.caixa((-1.87, -0.045, 0.42), (-1.70, 0.045, 0.49), "preto", chanfro=0.0)


def _cortar_receptor(bm):
    """O receptor de cima sem o guarda-mão de trilhos: tudo à frente de CORTE_X sai, e o buraco do corte é fechado."""
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    r = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(CORTE_X, 0, 0), plane_no=(1, 0, 0), clear_outer=True)
    corte = [e for e in r["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
    if corte:
        bmesh.ops.holes_fill(bm, edges=corte, sides=0)


def construir(cfg):
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    mats = estilizadas.materiais()
    nomes = estilizadas.NOMES
    G = cfg.transformacao()
    por_peca = {p: bmesh.new() for p in cfg.PECAS}
    ordem = list(cfg.OBJETOS) + ["(modeladas)"]

    # 1) as peças do AR-15: o Decimate, a cor de cada face, o corte do receptor
    with bpy.data.libraries.load(os.path.join(armas.FONTES, cfg.FONTE), link=False) as (src, dst):
        dst.objects = [n for n in src.objects if n in cfg.OBJETOS]
    tmp = bpy.data.meshes.new("_tmp")
    for o in [o for o in dst.objects if o is not None]:
        peca = cfg.OBJETOS[o.name]
        if hasattr(v1, "ajustar"):
            v1.ajustar(o.name, o)
        razao = cfg.DECIMAR.get(o.name)
        if razao:
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
        fontes = [mt.name if mt else "" for mt in me.materials]
        bm = bmesh.new()
        bm.from_mesh(me)
        if o.name == "iur upper":
            _cortar_receptor(bm)
        origem = bm.faces.layers.int.new("origem")
        for f in bm.faces:
            mf = fontes[f.material_index] if f.material_index < len(fontes) else ""
            f.material_index = nomes.index(cfg.cor_da_face(o.name, mf, f.calc_center_median()))
            f[origem] = ordem.index(o.name)
        bm.transform(G)
        bm.to_mesh(tmp)
        bm.free()
        por_peca[peca].from_mesh(tmp)
        bpy.data.meshes.remove(me)
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.meshes.remove(tmp)

    # 2) o que a M4A1 tem e o AR-15 não, modelado no espaço do AR-15 e levado pela mesma transformação
    tmp = bpy.data.meshes.new("_tmp")
    for peca, fazer in (("corpo", (guarda_mao, quebra_chamas, massa_de_mira, alca)), ("ferrolho", (alavanca,))):
        novas = bmesh.new()
        m = armas.Extras(novas, nomes)
        for f in fazer:
            f(m)
        origem = novas.faces.layers.int.new("origem")
        for f in novas.faces:
            f[origem] = ordem.index("(modeladas)")
        novas.transform(G)
        novas.to_mesh(tmp)
        novas.free()
        por_peca[peca].from_mesh(tmp)
    bpy.data.meshes.remove(tmp)

    # 3) as peças: o que ninguém vê sai, as faces num plano se juntam, lisas até 30°, o UV na paleta
    col = armas._colecao(cfg.NOME)
    objs = []
    contagem = {}
    for peca, bm in por_peca.items():
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
        n = armas.tirar_escondidas(bm)
        bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(1.0), verts=bm.verts, edges=bm.edges, delimit={"MATERIAL"})
        bmesh.ops.triangulate(bm, faces=bm.faces, quad_method="BEAUTY", ngon_method="BEAUTY")
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.normal_update()
        origem = bm.faces.layers.int.get("origem")
        modeladas = ordem.index("(modeladas)")
        for e in bm.edges:
            if len(e.link_faces) != 2:
                e.smooth = False
                continue
            f0, f1 = e.link_faces
            limite = cfg.ANGULO_LISO
            if origem is not None and (f0[origem] != modeladas or f1[origem] != modeladas):
                limite = cfg.ANGULO_LISO_FONTE
            e.smooth = f0.normal.angle(f1.normal, 0.0) <= math.radians(limite) + 1e-4
        if origem is not None:
            for f in bm.faces:
                contagem[ordem[f[origem]]] = contagem.get(ordem[f[origem]], 0) + 1
            bm.faces.layers.int.remove(origem)
        nome = f"{cfg.NOME}_{peca}"
        me = bpy.data.meshes.new(nome)
        bm.to_mesh(me)
        bm.free()
        for p in me.polygons:
            p.use_smooth = True
        for mat in mats:
            me.materials.append(mat)
        me.uv_layers.new(name="UVMap")
        estilizadas.uv_da_cor(me)
        o = bpy.data.objects.new(nome, me)
        col.objects.link(o)
        objs.append(o)
        print(f"  {nome}: {len(me.polygons)} triângulos ({n} faces escondidas tiradas)")
    for k in ordem:
        print(f"    {k}: {contagem.get(k, 0)}")
    print("  total:", sum(len(o.data.polygons) for o in objs), "triângulos")
    return objs
