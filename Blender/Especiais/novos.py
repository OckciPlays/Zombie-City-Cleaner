# Os zumbis novos (04/11/2026, a pedido): os modelos que o Pedro e o Gustavo fizeram do Brutamonte e do Corredor
# (Blender/Especiais/Novos/NovoBrutamonte.fbx e NovoCorredor.fbx: malhas únicas do Tripo, com 1 m de altura, sem
# esqueleto e só com a cor) ganham o mesmo tratamento dos especiais (especiais.py), sem mexer nos que já estão no jogo:
#  - a altura do jogo (o Brutamonte na escala em que o olho dele fica do tamanho do olho antigo, R = 0,10; o Corredor com
#    a altura do Corredor atual), centrado entre os pés e virado para -Y;
#  - o Brutamonte troca o olho do Tripo (um globo aberto e dois aros finos, que não fecham) pelo olho antigo (o globo e as
#    duas pálpebras do "O Brutamonte/Brutamonte_Jogo.blend"), no mesmo lugar; a íris e as pálpebras entram numa faixa de
#    256 px embaixo do atlas (2048 x 2304), como nos especiais;
#  - o esqueleto do Oco (os nomes do jogo), encaixado pelas medidas da malha; o Corredor está em T, então a linha do
#    braço é medida na horizontal; os dois lados são medidos separados (o Corredor não é simétrico);
#  - os ossos do olho (olho, palpebra_sup, palpebra_inf, filhos do chest, com a cabeça no centro do globo) e as peças do
#    olho presas inteiras a eles; a carne em volta do olho presa ao chest (não escorrega quando o tronco dobra);
#  - os pesos pelo calor dos ossos numa casca de voxels (como nos especiais).
# Sai Assets/_Game/Art/Characters/Zombie/Especiais/<Nome>.fbx com <Nome>_BaseColor/_Normal/_MetalLiso.png e uma cópia
# para conferir em Backups/Novos_Conferir/<Nome>_Rig.blend (05/11/2026: era "D:\teste blender\Novos", a pasta do O OCO,
# que não deve ser mexida; a variável NOVOS_CONFERIR troca a pasta).
#  - (05/11/2026, a pedido: "algumas animações de correr no corredor novo faz com que algumas peças da sua cabeça se mexam
#    que não fazem sentido") nada da cabeça segue os braços: os pesos pelo calor, com o modelo em T (o Corredor) ou com a
#    cabeça entre os ombros (o Brutamonte), punham parte das peças da cabeça no braço, no antebraço e no ombro; na corrida,
#    os braços balançando as arrastavam (até 5 cm em relação ao crânio). Veja cabeca_sem_bracos.
#
# Rodar (Blender em segundo plano): blender -b --factory-startup --python novos.py -- NovoBrutamonte NovoCorredor

import math
import os
import sys

import bpy
import bmesh
import numpy as np
from mathutils import Matrix, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import especiais as E  # noqa: E402

FONTES = os.path.join(AQUI, "Novos")
ARTE_OLHO = r"D:\teste blender\O Brutamonte\Brutamonte_Jogo.blend"
IRIS = r"D:\teste blender\O Brutamonte\textures\Brutamonte_Olho_Iris.png"
CONFERIR = os.environ.get("NOVOS_CONFERIR") or os.path.join(os.path.dirname(os.path.dirname(AQUI)), "Backups", "Novos_Conferir")
R_OLHO = 0.10                     # o raio do globo antigo (as pálpebras antigas foram feitas para ele)
FAIXA = 256

MODELOS = {
    "NovoBrutamonte": dict(tipo="Brutamonte", braco="pendurado", voxel=0.03, simetrico=True,
                           ref=(r"D:\teste blender\O Brutamonte\export\Brutamonte.fbx", "Cabeca")),
    "NovoCorredor": dict(tipo="Corredor", braco="T", voxel=0.016, altura=2.234,
                         ref=(r"D:\teste blender\O Corredor v2\export\Corredor.fbx", "Cabeca")),
}


def log(*a):
    print("[novos]", *a, flush=True)


# ================================================================================================ a malha
def ilhas(me):
    """O índice da ilha (peça solta) de cada vértice."""
    pai = np.arange(len(me.vertices))
    def raiz(i):
        while pai[i] != i:
            pai[i] = pai[pai[i]]
            i = pai[i]
        return i
    for e in me.edges:
        a, b = raiz(e.vertices[0]), raiz(e.vertices[1])
        if a != b:
            pai[a] = b
    return np.array([raiz(i) for i in range(len(me.vertices))])


def esfera(P):
    """O centro e o raio da esfera que melhor passa pelos pontos (mínimos quadrados)."""
    A = np.c_[2 * P, np.ones(len(P))]
    b = (P ** 2).sum(1)
    x = np.linalg.lstsq(A, b, rcond=None)[0]
    c = x[:3]
    return c, float(np.sqrt(x[3] + c @ c))


def coords(ob):
    V = np.empty(len(ob.data.vertices) * 3, np.float32)
    ob.data.vertices.foreach_get("co", V)
    return V.reshape(-1, 3).astype(np.float64)


def por_coords(ob, P):
    ob.data.vertices.foreach_set("co", P.astype(np.float32).ravel())
    ob.data.update()


def apagar_vertices(ob, mascara):
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.verts[i] for i in np.nonzero(mascara)[0]], context="VERTS")
    bm.to_mesh(ob.data)
    bm.free()


def sem_soltos(ob):
    """Os vértices sem face (sobras da separação das peças)."""
    usados = np.zeros(len(ob.data.vertices), bool)
    for p in ob.data.polygons:
        usados[list(p.vertices)] = True
    if (~usados).any():
        apagar_vertices(ob, ~usados)


def preparar(nome, cfg):
    """Importa, deixa em pé no tamanho do jogo, centrado entre os pés. Para o Brutamonte, tira o olho do Tripo e devolve o
    centro do globo (C)."""
    E.limpar_cena()
    novos = E.importar(os.path.join(FONTES, nome + ".fbx"))
    ob = next(o for o in novos if o.type == "MESH")
    for o in novos:
        if o is not ob:
            bpy.data.objects.remove(o, do_unlink=True)
    for o in bpy.context.selected_objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    ob.name = nome + "_Corpo"
    P = coords(ob)
    H = P[:, 2].max() - P[:, 2].min()
    C = None
    if cfg["tipo"] == "Brutamonte":
        isl = ilhas(ob.data)
        olho = np.zeros(len(P), bool)
        globo = None
        for r_ in np.unique(isl):
            m = isl == r_
            c = P[m].mean(0)
            if abs(c[0]) < 0.03 * H and c[1] > 0.06 * H and 0.65 * H < c[2] < 0.85 * H and m.sum() < 400:
                olho |= m
                cc, rr = esfera(P[m])
                resid = np.abs(np.linalg.norm(P[m] - cc, axis=1) - rr).mean()
                if globo is None or resid < globo[2]:
                    globo = (cc, rr, resid, int(m.sum()))
        if globo is None:
            raise RuntimeError("não achei o olho do Tripo nas costas")
        log(f"  olho do Tripo: {olho.sum()} vértices; globo centro {np.round(globo[0], 4).tolist()} raio {globo[1]:.4f} "
            f"(erro {globo[2] * 1000:.1f} mm, {globo[3]} vértices)")
        s = R_OLHO / globo[1]
        C = globo[0] * s
        apagar_vertices(ob, olho)
        P = coords(ob)
    else:
        s = cfg["altura"] / H
    P = P * s
    # centrado: x no meio do tronco (o Corredor não é simétrico), y no meio dos pés; o chão em z = 0
    z0 = P[:, 2].min()
    P[:, 2] -= z0
    Hs = P[:, 2].max()
    tronco = P[(P[:, 2] > 0.48 * Hs) & (P[:, 2] < 0.6 * Hs) & (np.abs(P[:, 0]) < 0.2 * Hs)]
    pes = P[P[:, 2] < 0.05 * Hs]
    dx = -float(np.median(tronco[:, 0]))
    dy = -float(pes[:, 1].mean())
    P[:, 0] += dx
    P[:, 1] += dy
    por_coords(ob, P)
    if C is not None:
        C = C + np.array([dx, dy, -z0])
    log(f"  escala {s:.4f}, altura {Hs:.3f} m, deslocado x {dx:+.3f} y {dy:+.3f}")
    return ob, s, (Vector(C.tolist()) if C is not None else None)


# ================================================================================================ o olho antigo
def olho_antigo(C):
    """O globo e as pálpebras do Brutamonte_Jogo.blend (malhas relativas ao pivô do olho), postos com o centro em C."""
    nomes = ["Brutamonte_Olho_Globo", "Brutamonte_Palpebra_Sup", "Brutamonte_Palpebra_Inf"]
    with bpy.data.libraries.load(ARTE_OLHO, link=False) as (de, para):
        para.objects = [n for n in de.objects if n in nomes]
    pecas = {}
    for o in para.objects:
        bpy.context.scene.collection.objects.link(o)
        o.parent = None
        o.matrix_world = Matrix.Translation(C)
        pecas[o.name.split("_")[-1]] = o           # Globo, Sup, Inf
    for o in bpy.context.selected_objects:
        o.select_set(False)
    for o in pecas.values():
        o.select_set(True)
    bpy.context.view_layer.objects.active = pecas["Globo"]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in pecas.values():
        sem_soltos(o)
    # os caminhos das imagens do arquivo de arte (relativos a ele)
    base = os.path.dirname(ARTE_OLHO)
    for img in bpy.data.images:
        if img.filepath.startswith("//"):
            img.filepath = os.path.join(base, img.filepath[2:])
        try:
            img.reload()
        except Exception:
            pass
    log("  olho antigo:", {k: len(o.data.polygons) for k, o in pecas.items()})
    return pecas


def assar_palpebra(ob, largura, altura, caminho):
    """A cor que a pálpebra tem hoje (UV antigo + atlas antigo) assada numa imagem nova com UV próprio (UVNovo)."""
    me = ob.data
    antigo = me.uv_layers[0].name
    novo = me.uv_layers.new(name="UVNovo")
    nome_novo = novo.name                   # (a referência à camada não vale mais depois do modo de edição)
    me.uv_layers.active = novo
    for o in bpy.context.selected_objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")
    mat = me.materials[0].copy()
    me.materials[0] = mat
    nt = mat.node_tree
    for n in nt.nodes:                      # as texturas do material antigo leem o UV antigo
        if n.type == "TEX_IMAGE" and not n.inputs["Vector"].links:
            uvn = nt.nodes.new("ShaderNodeUVMap")
            uvn.uv_map = antigo
            nt.links.new(uvn.outputs["UV"], n.inputs["Vector"])
    img = bpy.data.images.new(ob.name + "_Assada", largura, altura, alpha=False)
    alvo = nt.nodes.new("ShaderNodeTexImage")
    alvo.image = img
    for n in nt.nodes:
        n.select = False
    alvo.select = True
    nt.nodes.active = alvo
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 4
    sc.render.bake.use_pass_direct = False
    sc.render.bake.use_pass_indirect = False
    sc.render.bake.use_pass_color = True
    sc.render.bake.margin = 6
    bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, use_selected_to_active=False)
    img.filepath_raw = caminho
    img.file_format = "PNG"
    img.save()
    nt.nodes.remove(alvo)
    return img, nome_novo


def pixels(img):
    w, h = img.size
    a = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(a)
    return a.reshape(h, w, 4)         # linha 0 = embaixo


def reduzir(a, w, h):
    """Redimensiona (vizinho com média por blocos quando for múltiplo; senão amostra)."""
    H0, W0 = a.shape[:2]
    if W0 % w == 0 and H0 % h == 0:
        return a.reshape(h, H0 // h, w, W0 // w, a.shape[2]).mean((1, 3))
    ys = (np.arange(h) + 0.5) * H0 / h
    xs = (np.arange(w) + 0.5) * W0 / w
    return a[ys.astype(int)][:, xs.astype(int)]


def salvar_png(nome, a, caminho):
    h, w = a.shape[:2]
    img = bpy.data.images.new(nome, w, h, alpha=True)
    img.pixels.foreach_set(np.clip(a, 0, 1).astype(np.float32).ravel())
    img.filepath_raw = caminho
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def texturas(nome, corpo, pecas):
    """<Nome>_BaseColor/_Normal/_MetalLiso.png. No Brutamonte: o atlas do Tripo em cima e a faixa (íris + pálpebras)
    embaixo; os UVs vão junto."""
    fbm = os.path.join(FONTES, nome + ".fbm")
    jpg = next(os.path.join(fbm, f) for f in os.listdir(fbm) if f.lower().endswith((".jpg", ".png")))
    tri = bpy.data.images.load(jpg, check_existing=True)
    cor = pixels(tri)
    H0, W0 = cor.shape[:2]
    rug = np.full((H0, W0), 0.85, np.float32)
    if pecas:
        NH = H0 + FAIXA
        faixa = np.zeros((FAIXA, W0, 4), np.float32)
        faixa[..., :3] = np.array([30, 6, 6]) / 255.0
        faixa[..., 3] = 1
        rug_f = np.full((FAIXA, W0), 0.35, np.float32)
        iris = pixels(bpy.data.images.load(IRIS, check_existing=True))
        faixa[:, :FAIXA] = reduzir(iris, FAIXA, FAIXA)
        rug_f[:, :FAIXA] = 40 / 255.0                # o olho molhado brilha
        larg = (W0 - FAIXA) // 2
        uvs = {}
        for i, k in enumerate(("Sup", "Inf")):
            x0 = FAIXA + i * larg
            caminho = os.path.join(E.SAIDA, f"_{nome}_{k}_assada.png")
            img, uvnovo = assar_palpebra(pecas[k], larg, FAIXA, caminho)
            faixa[:, x0:x0 + larg] = pixels(img)
            uvs[k] = (x0, larg, uvnovo)
            try:
                os.remove(caminho)
            except OSError:
                pass
        cor = np.concatenate([faixa, cor], 0)        # linha 0 = embaixo: a faixa fica no v de 0 a 256/NH
        rug = np.concatenate([rug_f, rug], 0)
        # os UVs: o corpo sobe; o globo vai para o quadrado da íris; cada pálpebra para o seu pedaço da faixa
        def remapear(ob, f, fonte=None):
            me = ob.data
            src = me.uv_layers[fonte] if fonte else me.uv_layers[0]
            dst = me.uv_layers[0]
            A = np.empty(len(me.loops) * 2, np.float32)
            src.data.foreach_get("uv", A)
            A = A.reshape(-1, 2)
            dst.data.foreach_set("uv", f(A).astype(np.float32).ravel())
            for l in list(me.uv_layers):
                if l.name != dst.name:
                    me.uv_layers.remove(l)
        remapear(corpo, lambda A: np.c_[A[:, 0], (FAIXA + A[:, 1] * H0) / NH])
        remapear(pecas["Globo"], lambda A: np.c_[A[:, 0] * FAIXA / W0, A[:, 1] * FAIXA / NH])
        for k in ("Sup", "Inf"):
            x0, larg_, uvnovo = uvs[k]
            remapear(pecas[k], lambda A, x0=x0, larg_=larg_: np.c_[(x0 + A[:, 0] * larg_) / W0, A[:, 1] * FAIXA / NH], uvnovo)
    nor = np.zeros(cor.shape, np.float32)
    nor[..., 0] = 0.5
    nor[..., 1] = 0.5
    nor[..., 2] = 1.0
    nor[..., 3] = 1.0
    ml = np.zeros(cor.shape, np.float32)
    ml[..., 3] = 1.0 - rug
    cor[..., 3] = 1.0
    os.makedirs(E.SAIDA, exist_ok=True)
    salvar_png(nome + "_BaseColor", cor, os.path.join(E.SAIDA, nome + "_BaseColor.png"))
    salvar_png(nome + "_Normal", nor, os.path.join(E.SAIDA, nome + "_Normal.png"))
    salvar_png(nome + "_MetalLiso", ml, os.path.join(E.SAIDA, nome + "_MetalLiso.png"))
    for ob in [corpo] + list((pecas or {}).values()):
        ob.data.uv_layers[0].name = "UVMap"
    log(f"  texturas {cor.shape[1]}x{cor.shape[0]}")


# ================================================================================================ as medidas
def queixo_relativo(fbx, padrao):
    """O queixo / alto do especial atual (a peça Cabeca, como no especiais.py): a mesma proporção serve ao novo."""
    E.limpar_cena()
    novos = E.importar(fbx)
    malhas = [o for o in novos if o.type == "MESH"]
    bpy.context.view_layer.update()
    zs_cab, zs = [], []
    for o in malhas:
        mw = o.matrix_world
        # só os vértices das faces (as peças trazem vértices soltos, sobras da separação)
        z = [(mw @ o.data.vertices[i].co).z for p in o.data.polygons for i in p.vertices]
        zs += z
        if padrao in o.name:
            zs_cab += z
    r = float(np.percentile(zs_cab, 2)) / max(zs)
    E.limpar_cena()
    return r


class MedidasT(E.Medidas):
    """As medidas com os braços abertos (em T): a axila é o fundo do braço junto ao tronco e a linha do braço é medida
    na horizontal, do ombro até a ponta dos dedos. O resto é como no especiais.py."""

    def __init__(self, P, queixo, passo=0.01):
        self.P = P
        self.topo = P[:, 2].max()
        self.queixo = queixo
        esp = passo * 0.6
        def fatia(z):
            return P[np.abs(P[:, 2] - z) < esp]
        z = 0.25 * self.topo
        while z < 0.7 * self.topo:
            S = fatia(z)
            if len(S) and np.any(np.abs(S[:, 0]) < 0.012):
                break
            z += passo * 0.5
        self.virilha = z
        # a largura do tronco logo abaixo dos braços: o grupo do meio, subindo da cintura até os braços aparecerem
        def meio(S):
            gs = E.grupos_em_x(S[:, 0], 0.018)
            m = [g for g in gs if g[0] <= 0.0 <= g[1]]
            return m[0] if m else None
        # no T, longe do tronco só há braço: a altura do braço é a dos pontos com x bem para fora
        fora = P[P[:, 0] > 0.13 * self.topo]
        z_bracos = float(np.median(fora[:, 2])) if len(fora) else 0.7 * self.topo
        # a borda do tronco: logo abaixo do braço (ali só há tronco)
        # (pelos dois lados juntos: um rasgo na camisa não encurta a borda)
        S = fatia(z_bracos - 0.15)
        S = S[np.abs(S[:, 0]) < 0.18 * self.topo]
        borda = float(np.percentile(np.abs(S[:, 0]), 97)) if len(S) else 0.15
        g = (-borda, borda)
        # a axila: o ponto mais baixo do braço esquerdo perto do tronco
        perto = P[(P[:, 0] > borda + 0.03) & (P[:, 0] < borda + 0.12) & (P[:, 2] > z_bracos - 0.25) & (P[:, 2] < self.queixo)]
        self.axila = float(np.percentile(perto[:, 2], 3)) if len(perto) else z_bracos - 0.08
        # a base do pescoço: subindo acima do braço, o grupo do meio fica estreito
        larg_tronco = (g[1] - g[0]) if g else 0.3
        z = self.axila + 0.05
        while z < self.queixo - 0.01:
            gm = meio(fatia(z))
            if gm and (gm[1] - gm[0]) < 0.5 * larg_tronco:
                break
            z += passo * 0.5
        self.pescoco = z
        # a linha do braço esquerdo, na horizontal (começa onde aparecem pontos do braço, perto do tronco)
        braco = []
        x = borda + 0.02
        z_lo, z_hi = self.axila - 0.12, self.pescoco + 0.15
        while x < borda + 0.2 and len(P[(np.abs(P[:, 0] - x) < esp) & (P[:, 2] > z_lo) & (P[:, 2] < z_hi)]) < 3:
            x += passo * 0.5
        while True:
            S = P[(np.abs(P[:, 0] - x) < esp) & (P[:, 2] > z_lo) & (P[:, 2] < z_hi)]
            if len(S) < 3:
                break
            c = S.mean(0)
            if braco and abs(c[2] - braco[-1][0]) > 0.06:
                break
            braco.append((c[2], x, c[1]))
            x += passo
        log(f"    (em T) braços a partir de z {z_bracos:.3f}, borda do tronco x {borda:.3f}, axila {self.axila:.3f}, "
            f"pescoço {self.pescoco:.3f}, braço {len(braco)} pontos de x {braco[0][1] if braco else 0:.3f}")
        if len(braco) < 5:
            raise RuntimeError("não achei a linha do braço (em T)")
        self.braco = np.array(braco)
        self.ponta = self.braco[-1, 0]
        perna = []
        z = 0.03
        while z < self.virilha - 0.02:
            S = fatia(z)
            S = S[S[:, 0] > 0.004]
            gs = E.grupos_em_x(S[:, 0], 0.018)
            if gs:
                g2 = max(gs, key=lambda g: len(g[2]))
                pts = S[g2[2]]
                perna.append((z, pts[:, 0].mean(), pts[:, 1].mean(), pts[:, 1].min(), pts[:, 1].max()))
            z += passo
        self.perna = np.array(perna)
        pe = P[(P[:, 2] < 0.12) & (P[:, 0] > 0.0)]
        self.frente_do_pe = pe[:, 1].min()
        tronco = []
        z = self.virilha
        while z < self.topo:
            S = fatia(z)
            S = S[np.abs(S[:, 0]) < 0.05]
            if len(S) > 4:
                tronco.append((z, 0.5 * (S[:, 1].min() + S[:, 1].max()), S[:, 1].max() - S[:, 1].min()))
            z += passo
        self.tronco = np.array(tronco)


def juntas_dos_dois_lados(oco, rep, caudas, P, queixo, braco_em_t, simetrico=False):
    """As juntas com cada lado medido sozinho (o lado direito é medido no espelho e volta espelhado). Num modelo simétrico
    o lado esquerdo vale para os dois (como no especiais.py)."""
    Med = MedidasT if braco_em_t else E.Medidas
    M = Med(P, queixo)
    Mr = M if simetrico else Med(P * np.array([-1.0, 1.0, 1.0]), queixo)
    J = E.juntas(oco, rep, caudas, M)
    Jr = E.juntas(oco, rep, caudas, Mr)
    if braco_em_t:
        # no T, o desvio do braço do Oco (pendurado) não serve: as juntas ficam na linha do braço
        for JJ, MM in ((J, M), (Jr, Mr)):
            for nome in ("forearm.L", "hand.L"):
                p = np.array(rep[nome].translation)
                melhor, sm = 1e9, 0.0
                for s in np.linspace(-0.4, 1.2, 321):
                    d = np.linalg.norm(oco.ponto_do_braco(s) - p)
                    if d < melhor:
                        melhor, sm = d, s
                JJ[nome][0] = Vector(MM.ponto_do_braco(sm).tolist())
            JJ["upper_arm.L"][1] = JJ["forearm.L"][0].copy()
            JJ["forearm.L"][1] = JJ["hand.L"][0].copy()
    for n in list(J):
        if n.endswith(".R"):
            base = n[:-2] + ".L"
            h, t = Jr[base]
            J[n] = [Vector((-h.x, h.y, h.z)), Vector((-t.x, t.y, t.z))]
    for nm, MM in (("esquerdo", M), ("direito", Mr)):
        log(f"  medidas ({nm}): virilha {MM.virilha:.3f} axila {MM.axila:.3f} pescoço {MM.pescoco:.3f} queixo {MM.queixo:.3f} "
            f"topo {MM.topo:.3f} braço {len(MM.braco)} pontos até z {MM.ponta:.3f}")
    return J, M


# ================================================================================================ os pesos
def ilhas_rigidas(ob, max_v=80):
    """As peças soltas pequenas (remendos no joelho, esporões, cadarços, espinhos) com o mesmo peso em todos os vértices:
    cada uma anda inteira. Repartida entre dois ossos (o remendo do joelho entre a coxa e a canela), ela esticaria numa
    vareta quando a junta dobra."""
    isl = ilhas(ob.data)
    nomes = [g.name for g in ob.vertex_groups]
    n = 0
    for r_ in np.unique(isl):
        idx = np.nonzero(isl == r_)[0]
        if len(idx) > max_v:
            continue
        soma = {}
        for i in idx:
            for g in ob.data.vertices[int(i)].groups:
                soma[g.group] = soma.get(g.group, 0.0) + g.weight
        tot = sum(soma.values())
        if tot <= 0:
            continue
        media = {k: v / tot for k, v in soma.items() if v / tot >= 0.05}
        t2 = sum(media.values())
        lista = [int(i) for i in idx]
        for k in range(len(nomes)):
            ob.vertex_groups[k].remove(lista)
        for k, w in media.items():
            ob.vertex_groups[k].add(lista, w / t2, "REPLACE")
        n += 1
    log(f"  {n} peças soltas pequenas presas inteiras")


def cabeca_sem_bracos(ob):
    """Nada da cabeça segue os braços (05/11/2026). O vértice com pelo menos 20% do peso na cabeça perde o peso do braço,
    do antebraço e da mão; com metade ou mais na cabeça, também o do ombro (a clavícula). O resto é redividido entre os
    ossos que ficaram (a cabeça, o pescoço, o peito). Antes de ilhas_rigidas: as peças soltas da cabeça (os espinhos, as
    placas) saem presas inteiras ao crânio."""
    nomes = [g.name for g in ob.vertex_groups]
    if "head" not in nomes:
        return
    cab = nomes.index("head")
    braco = {i for i, n in enumerate(nomes) if n.split(".")[0] in ("upper_arm", "forearm", "hand")}
    ombro = {i for i, n in enumerate(nomes) if n.split(".")[0] == "shoulder"}
    mudados = 0
    for v in ob.data.vertices:
        pesos = {g.group: g.weight for g in v.groups if g.weight > 0.0}
        tot = sum(pesos.values())
        if tot <= 0.0:
            continue
        na_cabeca = pesos.get(cab, 0.0) / tot
        if na_cabeca < 0.2:
            continue
        tirar = [k for k in pesos if k in braco or (na_cabeca >= 0.5 and k in ombro)]
        if not tirar:
            continue
        resto = {k: w for k, w in pesos.items() if k not in tirar}
        soma = sum(resto.values())
        for k in tirar:
            ob.vertex_groups[k].remove([v.index])
        for k, w in resto.items():
            ob.vertex_groups[k].add([v.index], w / soma, "REPLACE")
        mudados += 1
    log(f"  {mudados} vértices da cabeça soltos dos braços")


def carne_no_peito(ob, arm, C, R):
    """A carne em volta do olho presa sobretudo ao chest (o olho é rígido no chest; assim a órbita não escorrega)."""
    gs = {g.name: g for g in ob.vertex_groups}
    peito = gs.get("chest") or ob.vertex_groups.new(name="chest")
    n = 0
    for v in ob.data.vertices:
        d = (v.co - C).length
        a = max(0.0, min(1.0, (1.8 * R - d) / (0.8 * R)))
        if a <= 0:
            continue
        pesos = {ob.vertex_groups[g.group].name: g.weight for g in v.groups}
        for nome, w in pesos.items():
            ob.vertex_groups[nome].add([v.index], w * (1 - a), "REPLACE")
        peito.add([v.index], pesos.get("chest", 0.0) * (1 - a) + a, "REPLACE")
        n += 1
    log(f"  {n} vértices em volta do olho presos ao chest")


# ================================================================================================ do T para a pose do Oco
def bracos_como_o_oco(arm, ob, rep):
    """O modelo em T vira a pose de repouso do Oco (braços pendurados): as animações do jogo são retargetizadas a partir do
    repouso de cada modelo, então um repouso em T sai com todos os movimentos dos braços deslocados para cima. Cada osso do
    braço gira (em volta da cabeça dele) até ficar na direção do osso do Oco; a malha vai junto (pelos pesos) e essa pose
    vira o repouso novo."""
    for o in bpy.context.selected_objects:
        o.select_set(False)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    for lado in (".L", ".R"):
        for base in ("upper_arm", "forearm", "hand"):
            n = base + lado
            pb = arm.pose.bones[n]
            bpy.context.view_layer.update()
            d_atual = (pb.tail - pb.head).normalized()
            d_oco = rep[n].to_3x3().col[1].normalized()
            q = d_atual.rotation_difference(d_oco)
            h = pb.head.copy()
            pb.matrix = Matrix.Translation(h) @ q.to_matrix().to_4x4() @ Matrix.Translation(-h) @ pb.matrix
            bpy.context.view_layer.update()
            log(f"    {n}: {math.degrees(d_atual.angle(d_oco)):.0f} graus até a pose do Oco")
    bpy.ops.object.mode_set(mode="OBJECT")
    # a malha recebe a pose; a pose vira o repouso
    for o in bpy.context.selected_objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    mod = next(m for m in ob.modifiers if m.type == "ARMATURE")
    bpy.ops.object.modifier_apply(modifier=mod.name)
    for o in bpy.context.selected_objects:
        o.select_set(False)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    mod = ob.modifiers.new("Armature", "ARMATURE")
    mod.object = arm


# ================================================================================================ um modelo
def fazer(nome, oco, rep, caudas):
    cfg = MODELOS[nome]
    log("==", nome)
    q_rel = queixo_relativo(*cfg["ref"])
    corpo, s, C = preparar(nome, cfg)
    pecas = olho_antigo(C) if C is not None else None
    P = E.pontos_da_superficie([corpo] + list((pecas or {}).values()))
    queixo = q_rel * P[:, 2].max()
    J, M = juntas_dos_dois_lados(oco, rep, caudas, P, queixo, cfg["braco"] == "T", cfg.get("simetrico", False))
    extras = []
    if C is not None:
        extras += [("olho", C, C + Vector((0, 0.12, 0)), "chest", (0, 0, 1)),
                   ("palpebra_sup", C, C + Vector((0, 0.10, 0.06)), "chest", (0, 0, 1)),
                   ("palpebra_inf", C, C + Vector((0, 0.10, -0.06)), "chest", (0, 0, 1))]
    arm = E.montar_armadura(nome, J, rep, extras)
    texturas(nome, corpo, pecas)
    mat = bpy.data.materials.new(nome + "_Mat")
    for o in [corpo] + list((pecas or {}).values()):
        o.data.materials.clear()
        o.data.materials.append(mat)
    for b in arm.data.bones:
        b.use_deform = b.name in E.ESQUELETO
    for tentativa in range(4):
        if E.pesos_pela_casca(corpo, arm, cfg["voxel"] * (1.0, 1.2, 0.85, 1.45)[tentativa]):
            break
    E.corrigir_sem_peso(corpo, arm)
    cabeca_sem_bracos(corpo)
    ilhas_rigidas(corpo)
    if C is not None:
        carne_no_peito(corpo, arm, C, R_OLHO)
        for k, osso in (("Globo", "olho"), ("Sup", "palpebra_sup"), ("Inf", "palpebra_inf")):
            g = pecas[k].vertex_groups.new(name=osso)
            g.add(list(range(len(pecas[k].data.vertices))), 1.0, "REPLACE")
    for b in arm.data.bones:
        b.use_deform = b.name != "root"
    for o in bpy.context.selected_objects:
        o.select_set(False)
    juntar = [corpo] + list((pecas or {}).values())
    for o in juntar:
        o.select_set(True)
    bpy.context.view_layer.objects.active = corpo
    if len(juntar) > 1:
        bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = nome + "_Jogo"
    ob.data.name = nome + "_Jogo"
    for m in list(ob.modifiers):
        ob.modifiers.remove(m)
    mod = ob.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    ob.parent = arm
    ob.matrix_parent_inverse = arm.matrix_world.inverted()
    E.limitar_pesos(ob)
    if cfg["braco"] == "T":
        bracos_como_o_oco(arm, ob, rep)
    tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    log(f"  {nome}: {tris} triângulos, {len(arm.data.bones)} ossos, altura {M.topo:.3f} m")
    E.exportar(nome, arm, ob)
    os.makedirs(CONFERIR, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(CONFERIR, nome + "_Rig.blend"), compress=True)
    return arm, ob


def principal(nomes):
    oco, rep, caudas = E.medir_oco()
    for n in nomes:
        fazer(n, oco, rep, caudas)
    log("feitos:", ", ".join(nomes))


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else list(MODELOS)
    principal([a for a in args if a in MODELOS] or list(MODELOS))
