# Os curativos no corpo do jogador (05/11/2026, a pedido: o sistema de vida por membro; a faixa e a tala que o
# CurativosNoCorpo.cs fazia por código, "modeladas no Blender"): a faixa enrolada num membro, a da testa, a do peito e a
# tala SAM presa por duas tiras, o Resources/Curativos.fbx e uma prévia de conferência.
#
# Como rodar (Blender 5.2, em segundo plano):
#   blender -b --factory-startup --disable-autoexec --python Blender/curativos.py -- tudo [<prévia.png>]
#   (as etapas: exportar; previa <prévia.png>; previa_escala <prévia.png>, com a cabeça e o peito escalados como na
#   Unity; tudo = exportar e a prévia, no caminho dado ou na pasta temporária)
# Depois, na Unity: o CurativosNoCorpo carrega as malhas do Resources/Curativos.fbx.
#
# As faixas são uma tira só enrolada em espiral (cada volta por cima de parte da de baixo, com a borda de baixo e a
# listra creme dela aparecendo) que acaba na ponta solta; a tala é a SAM dobrada em U, presa por duas tiras.
#
# O formato é o do Itens.fbx e do Loot.fbx (as funções do equipamentos.py): cada peça é um objeto girado 90° em X, com a
# malha nos eixos da Unity. Aqui o modelo é feito nos eixos do Blender: o eixo do membro em +Z (vira o +Y da Unity), a
# frente em -Y (vira o +Z da Unity) e o +X vira o -X da Unity (a Unity espelha o X). Os objetos têm os nomes que a Unity
# procura (Faixa_Membro, Faixa_Cabeca, Faixa_Tronco, Tala), sem o "_corpo" dos itens.
#
# As medidas: o membro é um cilindro de raio NOMINAL 0,05 m (a Unity escala a seção, o plano XY daqui, pela medida de
# cada membro dividida por 0,05) e a origem fica no meio do comprimento (os objetos todos na origem); a face de dentro das faixas e da tala encosta
# nesse raio (o polígono por fora do círculo: o meio de cada face a 0,05 e os vértices um pouco além); nada passa de
# 0,075 m do eixo. Como a seção escala, o pano da cabeça (a Unity escala ~2x) e o do peito (~3x) são mais finos aqui,
# para ficarem com uns 3 mm no corpo, como o do membro (e o grampo do peito, estreito na seção).
# Cores: os blocos da Paleta.png, as mesmas dos itens do loot.py (a atadura branca e creme, a tala laranja e azul).

import math
import os
import sys
import tempfile

import bpy
from mathutils import Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import equipamentos as eq  # noqa: E402
from loot import C, Z  # noqa: E402  (e as cores do saque, postas no eq.CORES)

eq.CORES.update({
    "tira": (8, 10),          # o bege das tiras da tala (o caqui, 9c8e70)
    "tira_escura": (6, 11),   # a ponta de velcro das tiras (7a6646)
    "manequim": (2, 13),      # o cinza do cilindro da prévia (6c6a64; não vai no FBX)
})

SAIDA = os.path.join(eq.UNITY, "Resources", "Curativos.fbx")
NOMES = ["Faixa_Membro", "Faixa_Cabeca", "Faixa_Tronco", "Tala"]

R = 0.05            # o raio nominal do membro
LIMITE = 0.075      # nada além disto do eixo


def _acaso(*n):
    """Um número de -0,5 a 0,5 que só depende de n (o mesmo a cada vez que o script roda)."""
    x = math.sin(sum(v * c for v, c in zip(n, (12.9898, 78.233, 37.719)))) * 43758.5453
    return x - math.floor(x) - 0.5


# ------------------------------------------------------------------ a lâmina curva (o pano e a tala)

def _casca(bm, uv, cor, pontos, fechada, cor_dentro=None, cor_borda=None, listra=None):
    """Uma lâmina em volta do eixo Z. pontos = [(a, z_baixo, z_cima, r_dentro_baixo, r_dentro_cima, r_fora_baixo,
    r_fora_cima)], com o ângulo a em graus a partir da frente (-Y) para o +X; os raios são as distâncias das faces ao
    eixo (os vértices ficam a r / cos(meio passo): o polígono por fora do círculo). Fechada: a volta inteira; aberta,
    com as pontas tampadas. listra = (altura, cor): uma faixa de outra cor na face de fora, junto da borda de baixo."""
    cor_dentro = cor_dentro or cor
    cor_borda = cor_borda or cor
    n = len(pontos)
    vs = []
    for i, (a, zb, zt, rib, rit, rob, rot) in enumerate(pontos):
        passos = []
        if fechada or i > 0:
            passos.append(abs(pontos[i][0] - pontos[i - 1][0]) % 360)
        if fechada or i < n - 1:
            passos.append(abs(pontos[(i + 1) % n][0] - pontos[i][0]) % 360)
        passo = min(p if p <= 180 else 360 - p for p in passos)
        k = 1.0 / math.cos(math.radians(passo) / 2)
        s, c = math.sin(math.radians(a)), math.cos(math.radians(a))

        def v(r, z):
            return bm.verts.new((r * k * s, -r * k * c, z))

        if listra:
            f = min(listra[0], (zt - zb) * 0.45) / (zt - zb)
            vs.append((v(rib, zb), v(rit, zt), v(rot, zt), v(rob, zb), v(rob + (rot - rob) * f, zb + (zt - zb) * f)))
        else:
            vs.append((v(rib, zb), v(rit, zt), v(rot, zt), v(rob, zb)))
    cores = {}

    def face(cf, *q):
        cores.setdefault(cf, []).append(bm.faces.new(q))

    for i in range(n if fechada else n - 1):
        ib, it, ot, ob = vs[i][:4]
        jb, jt, jo, jd = vs[(i + 1) % n][:4]
        face(cor_dentro, ib, jb, jt, it)
        if listra:
            om, jm = vs[i][4], vs[(i + 1) % n][4]
            face(listra[1], ob, om, jm, jd)
            face(cor, om, ot, jo, jm)
        else:
            face(cor, ob, ot, jo, jd)
        face(cor_borda, it, jt, jo, ot)
        face(cor_borda, ib, ob, jd, jb)
    if not fechada:
        for q, volta in ((vs[0], False), (vs[-1], True)):
            # a tampa: dentro embaixo, fora embaixo, (a listra), fora em cima, dentro em cima
            ib, it, ot, ob = q[:4]
            tampa = [ib, ob] + ([q[4]] if listra else []) + [ot, it]
            face(cor_borda, *(tampa[::-1] if volta else tampa))
    for cf, fs in cores.items():
        eq._pintar(bm, uv, fs, cf)


def L(cor, pontos, fechada=True, dentro=None, borda=None, listra=None):
    return (_casca, cor, pontos, fechada, dentro, borda, listra)


def _no_raio(a, r, z):
    """O ponto a r do eixo no ângulo a (graus, da frente -Y para o +X), na altura z."""
    return (r * math.sin(math.radians(a)), -r * math.cos(math.radians(a)), z)


def _placa(cor, a, r, tangente, grossura, altura, z):
    """Uma plaquinha deitada no pano, no ângulo a: o comprido na tangente, a grossura para fora (de r a r + grossura)."""
    return C(cor, (tangente, grossura, altura), _no_raio(a, r + grossura / 2, z), giro=(0, 0, a))


# ------------------------------------------------------------------ as faixas

def _espiral(lados, comeco, voltas, comprimento, largura, esp, degrau, cone, tremor, semente,
             pedacos_da_ponta, ponta, levanta, listra, cor="branco"):
    """Uma faixa enrolada em espiral: uma tira só, de baixo para cima, que começa no ângulo `comeco` (graus, da frente
    -Y para o +X) e dá `voltas` voltas, subindo a cada volta o que falta para o comprimento (cada volta cobre parte da
    de baixo); continua na ponta solta, por cima da volta de baixo, afinando até uma pontinha (meia largura `ponta`) um
    pouco levantada. O pano fica `degrau` mais para fora a cada volta e um pouco mais alto embaixo (`cone`): a borda de
    baixo de cada volta aparece, um degrau de degrau + cone/2) e tem uma listra creme de `listra` m junto da borda de
    baixo (como nas ataduras de crepe: a espiral aparece de longe). As bordas tremem para dentro (enrolada à mão), sem
    mudar o comprimento. Devolve a peça, a função (ângulo) -> (z do meio, r de fora no meio) da tira, para o grampo, e o
    ângulo da pontinha."""
    passo_a = 360.0 / lados
    n = int(round(voltas * lados))
    sobe = (comprimento - largura) * lados / n          # (por volta: as voltas inteiras de pedaços)
    z0 = -comprimento / 2 + largura / 2

    def meio(th):
        return z0 + sobe * (th - comeco) / 360.0

    def fora(th):
        cima = R + esp + degrau * (th - comeco) / 360.0
        return cima + cone, cima                       # (r de fora embaixo, em cima)

    pts = []
    for i in range(n + 1):
        th = comeco + i * passo_a
        z = meio(th)
        rob, rot = fora(th)
        db = tremor * (_acaso(semente, i, 1) + 0.5) if i > 0 else 0.0     # (o começo e o fim de cada lado: o
        dt = tremor * (_acaso(semente, i, 2) + 0.5) if i < n else 0.0     # comprimento exato)
        pts.append((th, z - largura / 2 + db, z + largura / 2 - dt, R, R, rob, rot))
    # a ponta solta: uma lâmina só (a grossura do pano), saindo da volta de baixo e se levantando
    th_fim = comeco + n * passo_a
    for j in range(1, pedacos_da_ponta + 1):
        f = j / pedacos_da_ponta
        th = th_fim + j * passo_a
        z = meio(th)
        meia = (largura / 2 - tremor) * (1 - f) + ponta * f
        rob, rot = fora(th)
        sobe_r = levanta * f * f
        pts.append((th, z - meia, z + meia, rob - esp + sobe_r, rot - esp + sobe_r, rob + sobe_r, rot + sobe_r))

    def no_pano(th):
        rob, rot = fora(th)
        f = max(0.0, (th - th_fim) / (pedacos_da_ponta * passo_a))
        return meio(th), (rob + rot) / 2 + levanta * f * f

    return L(cor, pts, False, borda="creme", listra=(listra, "creme")), no_pano, th_fim + pedacos_da_ponta * passo_a


def _faixa_membro():
    # uma atadura de 4 cm enrolada em espiral, quase 3 voltas (2,7: cada uma cobre ~45% da de baixo), 0,10 m de
    # comprimento; começa atrás (135°) e acaba na frente, à direita (+X daqui, o -X da Unity), onde a ponta solta fica
    # por cima, afinando até o lado, presa por um grampo de metal (o das ataduras elásticas)
    lados = 14
    faixa, no_pano, th = _espiral(lados, 135.0, 2.7, 0.10, 0.04, esp=0.0022, degrau=0.0012, cone=0.0018,
                                  tremor=0.0012, semente=1, pedacos_da_ponta=2, ponta=0.0065, levanta=0.0012,
                                  listra=0.004)
    passo = 360.0 / lados
    a = th - passo * 0.75
    z, r = no_pano(a)
    return [faixa, _placa("prata", a, r, 0.016, 0.0018, 0.012, z)]


def _faixa_cabeca():
    # uma volta e meia na testa, 0,045 m de largura (a tira de 3,2 cm subindo 8,7 mm por volta): começa atrás à
    # esquerda (-142°, -X daqui), passa pela testa, dá a volta e na segunda passada, por cima da primeira, atravessa a
    # testa de novo e acaba do lado (+X daqui, o -X da Unity, a 105°) na pontinha solta
    faixa, _, _ = _espiral(16, -142.5, 1.5, 0.045, 0.032, esp=0.0013, degrau=0.0013, cone=0.0006,
                           tremor=0.0008, semente=2, pedacos_da_ponta=3, ponta=0.004, levanta=0.0016,
                           listra=0.0035)
    return [faixa]                    # (fina: a Unity escala a seção ~2x na cabeça)


def _faixa_tronco():
    # a faixa larga do peito, 0,13 m: a tira de 8,5 cm em espiral, 2 voltas e um quarto, começando nas costas, à
    # esquerda (215°), e acabando na frente, um pouco à esquerda (-X daqui, o +X da Unity), na ponta presa por um grampo
    lados = 16
    faixa, no_pano, th = _espiral(lados, 215.0, 2.25, 0.13, 0.085, esp=0.0011, degrau=0.0006, cone=0.0006,
                                  tremor=0.002, semente=3, pedacos_da_ponta=2, ponta=0.014, levanta=0.0005,
                                  listra=0.006)
    passo = 360.0 / lados
    a = th - passo * 0.75
    z, r = no_pano(a)
    # o grampo: estreito na seção (que a Unity escala ~3x no peito), o alto de verdade
    return [faixa, _placa("prata", a, r, 0.0065, 0.0007, 0.016, z)]


# ------------------------------------------------------------------ a tala

def _tala():
    # a tala SAM dobrada em U, abraçando o membro pela frente (-Y daqui, +Z da Unity) num meio tubo de 160°: laranja por
    # fora e azul por dentro (3 mm cada, como a do loot.py), 0,30 m de comprimento com os cantos aparados e as abas um
    # pouco abertas; presa por 2 tiras bege que dão a volta inteira no membro a 0,09 m do meio, cada uma com a ponta de
    # velcro sobreposta do lado
    comp, esp = 0.30, 0.003
    arco = 80.0
    angs = [-arco + 20.0 * i for i in range(9)]
    pecas = []
    for cor, r0 in (("azul", R), ("laranja_vivo", R + esp)):
        pts = []
        for a in angs:
            apara = 0.012 if abs(a) > 70 else 0.003 if abs(a) > 50 else 0.0
            abre = 0.0015 if abs(a) > 70 else 0.0
            z = comp / 2 - apara
            pts.append((a, -z, z, r0 + abre, r0 + abre, r0 + esp + abre, r0 + esp + abre))
        pecas.append(L(cor, pts, False))
    larg, esp_t = 0.03, 0.0025
    for zc in (-0.09, 0.09):
        pts = []
        for i in range(18):
            a = -180.0 + 20.0 * i
            # por cima da tala (e da aba aberta) no meio tubo; no membro do outro lado
            r0 = R + 2 * esp + (0.002 if abs(a) > 70 else 0.0005) if abs(a) <= arco + 0.1 else R
            pts.append((a, zc - larg / 2, zc + larg / 2, r0, r0, r0 + esp_t, r0 + esp_t))
        pecas.append(L("tira", pts, True))
        # a ponta de velcro: por cima da tira, do lado (+X daqui), afinando
        vel = []
        for j, a in enumerate((100.0, 120.0, 140.0, 160.0)):
            m = larg / 2 - 0.0015 if j < 3 else larg / 2 - 0.006
            r0 = R + esp_t
            vel.append((a, zc - m, zc + m, r0, r0, r0 + 0.002, r0 + 0.002))
        pecas.append(L("tira_escura", vel, False))
    return pecas


MODELOS = {"Faixa_Membro": _faixa_membro, "Faixa_Cabeca": _faixa_cabeca, "Faixa_Tronco": _faixa_tronco, "Tala": _tala}


# ------------------------------------------------------------------ montar, exportar e conferir

def _cena():
    return bpy.context.scene


def construir():
    cena = _cena()
    for o in list(cena.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    objs = []
    for i, nome in enumerate(NOMES):
        o = eq._objeto(nome, MODELOS[nome](), cena)
        o.name = nome
        o.data.name = nome
        objs.append(o)
    for o in objs:
        # a malha já está nos eixos da Unity (o eixo do membro em +Y, a frente em +Z), só falta espelhar o X
        vs = [v.co for v in o.data.vertices]
        lo = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
        hi = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
        raio = max(math.hypot(v.x, v.z) for v in vs)
        dentro = min(math.hypot(v.x, v.z) for v in vs)
        print(f"curativos: {o.name}: {len(o.data.polygons)} faces, {len(vs)} vértices; na Unity X {-hi.x:+.4f}..{-lo.x:+.4f}, "
              f"Y {lo.y:+.4f}..{hi.y:+.4f}, Z {lo.z:+.4f}..{hi.z:+.4f}; do eixo {dentro:.4f}..{raio:.4f}"
              + ("  (PASSOU DO LIMITE)" if raio > LIMITE else ""))
    return objs


def exportar():
    cena = _cena()
    os.makedirs(os.path.dirname(SAIDA), exist_ok=True)
    for o in cena.objects:
        o.select_set(o.name in NOMES)
    bpy.ops.export_scene.fbx(filepath=SAIDA, use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", bake_anim=False, mesh_smooth_type="FACE")
    print("curativos: gravado", SAIDA)


def previa(caminho, escala=False):
    """As 4 peças lado a lado, cada uma num cilindro cinza de raio 0,05 m, de 3/4 pela frente (em cima) e por trás
    (embaixo), no Workbench com a textura, a cavidade e o contorno. Com escala: a cabeça 2x e o peito 3 x 2,2 na seção,
    como a Unity faz (para ver a grossura do pano no corpo)."""
    cena = _cena()
    cilindros = {"Faixa_Membro": 0.22, "Faixa_Cabeca": 0.12, "Faixa_Tronco": 0.22, "Tala": 0.40}
    secao = {"Faixa_Cabeca": (2.0, 2.0), "Faixa_Tronco": (3.0, 2.2)} if escala else {}
    largura = 0.48 if escala else 0.3
    for fileira, (dz, giro) in enumerate(((0.0, 0.0), (-0.55, 180.0))):
        x = 0.0
        for i, nome in enumerate(NOMES):
            sx, sz = secao.get(nome, (1.0, 1.0))
            o = bpy.data.objects[nome]
            if fileira:
                o = o.copy()
                o.name = nome + "_costas"
                cena.collection.objects.link(o)
            o.location = (x, 0, dz)
            o.rotation_euler = (math.radians(90), 0, math.radians(giro))
            o.scale = (sx, 1.0, sz)          # (a malha nos eixos da Unity: a seção é o X e o Z do objeto)
            c = eq._objeto(f"Cilindro{fileira}{i}", [Z("manequim", R, cilindros[nome], (0, 0, 0), 32)], cena)
            c.location, c.rotation_euler, c.scale = o.location, o.rotation_euler, o.scale
            x += largura if escala and nome != "Faixa_Membro" else 0.3
    bpy.context.view_layer.update()
    r = cena.render
    try:
        r.engine = "BLENDER_WORKBENCH"
    except TypeError:
        pass
    r.film_transparent = False
    r.image_settings.file_format = "PNG"
    r.image_settings.color_mode = "RGB"
    s = cena.display.shading
    s.light = "STUDIO"
    s.color_type = "TEXTURE"
    s.show_cavity = True
    s.cavity_type = "BOTH"
    s.show_object_outline = True
    s.object_outline_color = (0.05, 0.05, 0.05)
    cena.view_settings.view_transform = "Standard"
    if cena.world is None:
        cena.world = bpy.data.worlds.new("Fundo")
    cena.world.color = (0.62, 0.64, 0.68)
    r.resolution_x, r.resolution_y = 1800, 1300
    # a câmera de 3/4 (da frente-direita, um pouco de cima), enquadrando tudo pela projeção dos cantos
    cantos = [o.matrix_world @ Vector(c) for o in cena.objects if o.type == "MESH" for c in o.bound_box]
    meio = sum(cantos, Vector()) / len(cantos)
    cam = eq._camera(cena, True, meio, (0.55, -1.0, 0.45), 5.0, 1.0)
    bpy.context.view_layer.update()
    inv = cam.matrix_world.inverted()
    q = [inv @ c for c in cantos]
    x0, x1 = min(p.x for p in q), max(p.x for p in q)
    y0, y1 = min(p.y for p in q), max(p.y for p in q)
    cam.location = cam.matrix_world @ Vector(((x0 + x1) / 2, (y0 + y1) / 2, 0.0))
    cam.data.ortho_scale = max(x1 - x0, (y1 - y0) * r.resolution_x / r.resolution_y) * 1.06
    r.filepath = caminho
    bpy.ops.render.render(write_still=True)
    print("curativos: prévia em", caminho)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["tudo"]
    etapa = args[0] if args else "tudo"
    construir()
    if etapa in ("exportar", "tudo"):
        exportar()
    if etapa in ("previa", "previa_escala", "tudo"):
        previa(args[1] if len(args) > 1 else os.path.join(tempfile.gettempdir(), "curativos_previa.png"),
               escala=etapa == "previa_escala")
