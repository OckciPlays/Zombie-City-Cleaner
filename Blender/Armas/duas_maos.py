# As armas brancas na primeira pessoa: a arma no lugar dela na tela e as mãos nas pegadas (08/11/2026, a pedido do Pedro,
# com as referências dele: a lança "Segura Inferior" e o guerreiro de lança; o machado "FPP Axe"; o taco de beisebol na
# posição de rebater).
#
# Antes, a arma ia presa ao punho direito dos braços dos punhos, na pose deles, e a mão esquerda era levada até o cabo
# depois. Agora a arma tem o lugar dela na tela, e as mãos vão até ela:
# - a pose da arma no Idle (cfg.IDLE_FP): um ponto da arma num lugar da tela (u, v: -1 a 1, da esquerda para a direita e
#   de baixo para cima, pela câmera dos braços, de 55 graus) e a uma distância da câmera, outro ponto da arma na direção
#   de outro lugar da tela, e o "alto" da arma (+Z) para um lado; na faca e na tocha (sem IDLE_FP), a arma fica onde
#   estava em cada quadro dos clipes dos punhos (presa ao punho direito de antes) e só a mão direita pega o cabo (no y
#   cfg.PEGADA_Y da arma);
# - os pontos de pegada (MainGrip e SupportGrip, os vazios Pegada_Direita e Pegada_Esquerda, filhos do vazio da arma):
#   a mão direita no cabo em cfg.MAO_DIREITA e a esquerda em cfg.MAO_ESQUERDA (o y da arma), fechadas como no pegada.py
#   (a palma encostada no cabo, os dedos em volta e o polegar dando a volta). O giro de cada mão em volta do cabo não muda
#   (as mãos não escorregam), e é o que deixa os braços melhores nos quadros que o jogo usa (escolher_giro);
# - os braços seguem as pegadas por IK de dois ossos (o do agua_animacoes.py: o antebraço leva metade da torção, e o pulso
#   a outra metade); o lado do cotovelo (o polo girado em volta da linha do ombro ao pulso: o braço de cima girando no
#   ombro) é escolhido quadro a quadro, sem saltos (_cotovelos), pelo mesmo custo; onde o braço não alcança, o ombro
#   avança o que faltar (fora da tela);
# - o custo (custo_do_braco, 08/11/2026, a pedido: "os braços longe da câmera", "os pulsos naturais" e "o punho está um
#   pouco torto e irregular, não segue o padrão anatômico"): o pulso alinhado com o antebraço (o desvio para o lado até
#   ~15 graus, a dobra para a palma ou para as costas moderada, até ~45, nunca além de 60) e a torção do antebraço até ~90
#   a partir do polegar para cima (no pulso, a metade: a malha do pulso não afina), a distância do braço à câmera (nenhum
#   ponto a menos de 25 cm, a mão a 35 cm ou mais), o cotovelo fora da tela (abaixo da borda de baixo ou além da do lado)
#   e baixo, e o avanço do ombro;
# - o Walk, o Run, o Sacar e os socos: a arma faz, em volta da pegada direita, o movimento que o punho direito fazia no
#   clipe dos punhos (o balanço do passo, a subida do saque), a partir da pose do Idle;
# - o Golpe: as chaves de cfg.GOLPE_FP (cada uma, uma pose da arma como a do Idle), uma curva suave entre elas, com o
#   acerto no mesmo quadro de antes; uma chave pode trazer "direita" (o y da pegada direita naquele momento: a mão que
#   escorrega pelo cabo no machado, cfg.ESCORREGA_NO_GOLPE); a arma então vai presa à mão esquerda (cfg.MAO_DA_ARMA);
# - a checagem de cada quadro (_medir): o pulso, a menor distância do braço à câmera, a fração da tela coberta pelos
#   braços, a distância entre as mãos e os antebraços se cruzando, impressos por clipe (e por quadro no Golpe).
# Tudo é assado nos clipes de sempre (os mesmos nomes e quadros).

import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
BLENDER = os.path.dirname(AQUI)
if BLENDER not in sys.path:
    sys.path.insert(0, BLENDER)
import agua_animacoes as ag  # noqa: E402
import pegada  # noqa: E402

TY = math.tan(math.radians(27.5))      # a câmera dos braços: 55 graus na vertical, 16:9
TX = TY * 16.0 / 9.0
# os limites (graus e m)
DOBRA_BOA = 45.0          # a dobra do pulso para a palma ou para as costas (moderada)
LIMITE_DOBRA = 60.0       # (nunca além)
LIMITE_DESVIO = 15.0      # o desvio do pulso para o lado do polegar ou do mínimo (o pulso alinhado com o antebraço)
LIMITE_TORCAO = 90.0      # a torção do antebraço a partir do polegar para cima (no pulso, a metade)
ULNAR_AGORA = 0.0         # (no Golpe, cfg.LIMITE_ULNAR_GOLPE: o desvio para o mínimo até onde vai no golpe)
PERTO_MIN = 0.25          # nenhum ponto do braço mais perto da câmera
MAO_MIN = 0.35            # o meio da palma longe do olho
COBRE_MAX = 0.30          # a fração da tela coberta pelos braços
GIROS_DO_COTOVELO = list(range(-180, 180, 10))
ROLAR_TUDO = 0.0          # (cfg.ROLAR_FP: graus em volta do cabo em todas as chaves de pose)
PASSO_MAX = 30.0          # o quanto o cotovelo e a torção dos antebraços podem mudar de um quadro para o outro (graus)


def na_tela(u, v, d):
    """O ponto (no espaço dos braços: a câmera na origem, olhando para -Y) no lugar (u, v) da tela, a d m da câmera."""
    return Vector((-u * TX * d, -d, v * TY * d))


def da_tela(p):
    d = -p.y
    return (-p.x / d / TX, p.z / d / TY, d)


def _na_reta(P, u, v, L, perto=False):
    """O ponto na reta da câmera pelo lugar (u, v) da tela que fica a L m de P (o mais longe da câmera; com perto, o mais
    perto: a arma vem para trás, na direção da câmera)."""
    r = Vector((-u * TX, -1.0, v * TY))
    a, b, c = r.dot(r), -2.0 * r.dot(P), P.dot(P) - L * L
    disc = max(0.0, b * b - 4 * a * c)
    return r * ((-b + (-1.0 if perto else 1.0) * math.sqrt(disc)) / (2 * a))


def pose(chave, base=None):
    """A arma no espaço dos braços (W: o espaço da arma para o dos braços) de uma chave:
    {"ponto": (y da arma, u, v, d), "mira": (y da arma, u, v), "alto": (x, y, z)}; ou relativa à base (a pose do Idle):
    {"avanco": m para a ponta (ao longo do cabo), "desloca": (x, y, z) m, "giro": ((eixo), graus), "pivo_y": y da arma
    em volta do qual gira, "de": a chave base, se não for a do Idle}."""
    if "ponto" not in chave:
        # (09/11/2026) "de": a pose base desta chave (outra chave), em vez da do Idle
        W0 = pose(chave["de"], base) if "de" in chave else pose(base)
        Y = W0.to_3x3() @ Vector((0.0, 1.0, 0.0))
        T = Matrix.Translation(-Y * chave.get("avanco", 0.0) + Vector(chave.get("desloca", (0.0, 0.0, 0.0))))
        eixo, graus = chave.get("giro", ((1.0, 0.0, 0.0), 0.0))
        p = W0 @ Vector((0.0, chave.get("pivo_y", 0.0), 0.0))
        R = Matrix.Translation(p) @ Matrix.Rotation(math.radians(graus), 4, Vector(eixo)) @ Matrix.Translation(-p)
        return T @ R @ W0
    y0, u0, v0, d0 = chave["ponto"]
    y1, u1, v1 = chave["mira"]
    P0 = na_tela(u0, v0, d0)
    P1 = _na_reta(P0, u1, v1, abs(y1 - y0), chave.get("perto", False))
    Y = ((P0 - P1) / (y0 - y1)).normalized()
    Z = Vector(chave.get("alto", (0.0, 0.0, 1.0)))
    Z = (Z - Z.dot(Y) * Y).normalized()
    rg = float(os.environ.get("DM_ROLAR", chave.get("rolar", ROLAR_TUDO)))
    if rg:
        # (a arma girada em volta do cabo: todas as chaves, cfg.ROLAR_FP; ou só esta chave, "rolar")
        Z = (Matrix.Rotation(math.radians(rg), 3, Y) @ Z).normalized()
    X = Y.cross(Z)
    W = Matrix((X, Y, Z)).transposed().to_4x4()
    W.translation = P0 - Y * y0
    return W


# ================================================================================================ a curva entre as chaves

def _catmull(p0, p1, p2, p3, t):
    t2, t3 = t * t, t * t * t
    return 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3)


def curva(chaves, pivo):
    """As poses da arma quadro a quadro ({quadro: W}) entre as chaves [(quadro, W)]: o pivô (um ponto da arma) numa curva
    de Catmull-Rom, e o giro também (quatérnios no mesmo hemisfério, normalizados)."""
    ks = sorted(chaves, key=lambda k: k[0])
    ps = [W @ pivo for _, W in ks]
    qs = []
    for _, W in ks:
        q = W.to_quaternion()
        if qs and qs[-1].dot(q) < 0:
            q = -q
        qs.append(q)
    out = {}
    for i in range(len(ks) - 1):
        f0, f1 = ks[i][0], ks[i + 1][0]
        a, b = max(0, i - 1), min(len(ks) - 1, i + 2)
        for f in range(f0, f1 + (1 if i == len(ks) - 2 else 0)):
            t = (f - f0) / (f1 - f0)
            p = _catmull(ps[a], ps[i], ps[i + 1], ps[b], t)
            qv = _catmull(Vector(qs[a]), Vector(qs[i]), Vector(qs[i + 1]), Vector(qs[b]), t)
            q = Quaternion(qv).normalized()
            W = q.to_matrix().to_4x4()
            W.translation = p - q.to_matrix() @ pivo
            out[f] = W
    return out


def curva_escalar(chaves):
    """Um número quadro a quadro ({quadro: valor}) entre as chaves [(quadro, valor)], por Catmull-Rom, sem passar dos
    valores das duas chaves de cada trecho (a mão não escorrega além do ponto marcado)."""
    ks = sorted(chaves)
    out = {}
    for i in range(len(ks) - 1):
        f0, f1 = ks[i][0], ks[i + 1][0]
        a, b = max(0, i - 1), min(len(ks) - 1, i + 2)
        lo, hi = min(ks[i][1], ks[i + 1][1]), max(ks[i][1], ks[i + 1][1])
        for f in range(f0, f1 + (1 if i == len(ks) - 2 else 0)):
            t = (f - f0) / (f1 - f0)
            out[f] = min(hi, max(lo, _catmull(ks[a][1], ks[i][1], ks[i + 1][1], ks[b][1], t)))
    return out


# ================================================================================================ as medidas do braço

def pulso(esq, M, lado):
    """O pulso do lado na pose M: (a dobra: + para as costas da mão, - para a palma; o desvio: + para o lado do polegar,
    - para o do mínimo; a torção do antebraço: 0 com as costas da mão para fora do plano do braço, o polegar para cima
    com o cotovelo para baixo), em graus. O antebraço é o do IK (o eixo do cotovelo ao pulso, o Z na normal do plano do
    braço), e a mão é comparada com o lugar dela no repouso (os braços abertos, o pulso reto). Na malha, o pulso leva a
    metade da torção (o ag.braco dá a outra metade ao antebraço)."""
    ua, fa, mao = "upper_arm." + lado, "forearm." + lado, "hand." + lado
    S, E, W = M[ua].translation, M[fa].translation, M[mao].translation
    u = (W - S).normalized()
    e = (E - S) - (E - S).dot(u) * u
    e = e.normalized() if e.length > 1e-6 else u.orthogonal().normalized()
    z = u.cross(e) if lado == "L" else -u.cross(e)
    Mf0 = ag.quadro_yz(W - E, z, E).to_3x3()
    rel = esq.rest[fa].to_3x3().inverted() @ esq.rest[mao].to_3x3()
    q = ((Mf0 @ rel).inverted() @ M[mao].to_3x3()).to_quaternion()
    if q.w < 0:
        q = -q
    n = math.hypot(q.w, q.y)
    tw = Quaternion((q.w / n, 0.0, q.y / n, 0.0)) if n > 1e-9 else Quaternion()
    tor = math.degrees(2 * math.atan2(tw.y, tw.w))
    tor = (tor + 180.0) % 360.0 - 180.0
    s = tw.inverted() @ q
    if s.w < 0:
        s = -s
    ang = s.angle
    eixo = s.axis if ang > 1e-9 else Vector((1.0, 0.0, 0.0))
    dobra = math.degrees(ang * eixo.x)
    desvio = math.degrees(ang * eixo.z) * (1.0 if lado == "R" else -1.0)
    return dobra, desvio, tor


def capsulas(M, lado):
    """O braço em cápsulas (o ponto de começo, o de fim e os raios): o braço, o antebraço e a mão fechada."""
    S, E = M["upper_arm." + lado].translation, M["forearm." + lado].translation
    H = M["hand." + lado]
    W = H.translation
    K = H @ Vector((0.0, 0.095, -0.012))
    return [(S, E, 0.050, 0.045), (E, W, 0.045, 0.032), (W, K, 0.036, 0.046)]


def _amostras(caps, passo=0.012):
    out = []
    for A, B, ra, rb in caps:
        n = max(2, int((B - A).length / passo) + 1)
        for i in range(n):
            t = i / (n - 1)
            out.append((A.lerp(B, t), ra + (rb - ra) * t))
    return out


def perto(caps):
    """A menor distância da pele (as cápsulas) à câmera (m)."""
    return min(p.length - r for p, r in _amostras(caps, 0.02))


def meio_da_mao(M, lado):
    return M["hand." + lado] @ Vector((0.0, 0.06, -0.015))


def cotovelo_na_tela(E):
    """Quanto o cotovelo entra na tela (m: > 0 à vista, < 0 fora): a menor folga até a borda de baixo e as dos lados."""
    d = -E.y
    if d <= 0.02:
        return -0.1
    baixo = E.z + TY * d
    lados = TX * d - abs(E.x)
    cima = TY * d - E.z
    return min(baixo, lados, cima)


_DIRS = {}


def _direcoes(nx, ny):
    if (nx, ny) not in _DIRS:
        u = (np.arange(nx) + 0.5) / nx * 2 - 1
        v = (np.arange(ny) + 0.5) / ny * 2 - 1
        U, V = np.meshgrid(u, v)
        d = np.stack([-U * TX, -np.ones_like(U), V * TY], -1).reshape(-1, 3)
        _DIRS[(nx, ny)] = d / np.linalg.norm(d, axis=1, keepdims=True)
    return _DIRS[(nx, ny)]


def cobertura(caps, nx=96, ny=54):
    """A fração da tela coberta pelas cápsulas (esferas ao longo delas, projetadas)."""
    am = _amostras(caps)
    C = np.array([tuple(p) for p, _ in am])
    R = np.array([r for _, r in am])
    keep = -C[:, 1] > -R
    C, R = C[keep], R[keep]
    if not len(C):
        return 0.0
    dist = np.linalg.norm(C, axis=1)
    if (dist <= R).any():
        return 1.0
    cosmax = np.sqrt(1 - (R / dist) ** 2)
    dots = _direcoes(nx, ny) @ (C / dist[:, None]).T
    return float((dots > cosmax[None, :]).any(axis=1).mean())


def cruzam(M):
    """Quanto os antebraços entram um no outro (m; 0 se não se tocam): as cápsulas do cotovelo ao pulso."""
    a = _amostras([capsulas(M, "L")[1]], 0.01)
    b = _amostras([capsulas(M, "R")[1]], 0.01)
    return max(0.0, max(ra + rb - (p - q).length for p, ra in a for q, rb in b))


def saida_do_antebraco(M, lado):
    """Por onde o antebraço sai da tela, do pulso para o cotovelo: ("baixo", "esquerda", "direita", "cima" ou None se o
    pulso está fora da tela) e a altura da saída (v, -1 a 1) ou o u, na borda de baixo."""
    W, E = M["hand." + lado].translation, M["forearm." + lado].translation
    if -W.y < 0.03:
        return None, 0.0
    uw, vw, _ = da_tela(W)
    if abs(uw) > 1.0 or abs(vw) > 1.0:
        return None, 0.0
    if -E.y < 0.03:
        t = (-W.y - 0.03) / max(1e-6, (E.y - W.y))
        E = W.lerp(E, min(1.0, t))
    ue, ve, _ = da_tela(E)
    melhor = (9.0, None, 0.0)
    for borda, a, b, lim in (("esquerda", uw, ue, -1.0), ("direita", uw, ue, 1.0), ("baixo", vw, ve, -1.0),
                             ("cima", vw, ve, 1.0)):
        if (b - a) * (lim - a) > 0 and abs(b - a) > 1e-9:
            t = (lim - a) / (b - a)
            if 0.0 <= t < melhor[0]:
                outro = (vw + (ve - vw) * t) if borda in ("esquerda", "direita") else (uw + (ue - uw) * t)
                melhor = (t, borda, outro)
    return melhor[1], melhor[2]


def custo_do_braco(esq, M, lado, avancou, th):
    """O custo de um braço posto (graus, mais ou menos): o pulso alinhado e perto do natural, dentro dos limites, o braço
    longe da câmera, o cotovelo fora da tela e baixo, o ombro sem avançar. Devolve (o custo, (dobra, desvio, torção,
    perto))."""
    dobra, desvio, tor = pulso(esq, M, lado)
    c = 0.12 * (abs(dobra) + abs(desvio) + abs(tor))
    c += 4.0 * max(0.0, abs(dobra) - (DOBRA_BOA - 5.0)) + 10.0 * max(0.0, abs(dobra) - LIMITE_DOBRA)
    lim = LIMITE_DESVIO if desvio > 0 else max(LIMITE_DESVIO, ULNAR_AGORA)
    c += 8.0 * max(0.0, abs(desvio) - (lim - 3.0))
    c += 4.0 * max(0.0, abs(tor) - (LIMITE_TORCAO - 10.0)) + 8.0 * max(0.0, abs(tor) - (LIMITE_TORCAO + 10.0))
    caps = capsulas(M, lado)
    p = perto(caps)
    c += 4000.0 * max(0.0, PERTO_MIN + 0.03 - p)
    S = M["upper_arm." + lado].translation
    E = M["forearm." + lado].translation
    W = M["hand." + lado].translation
    c += 600.0 * max(0.0, cotovelo_na_tela(E) + 0.03)
    c += 2000.0 * max(0.0, E.z - (S.z + W.z) / 2.0)      # o cotovelo para cima é feio
    # o antebraço entra pela borda de baixo (ou pela do lado de fora, mais embaixo): nunca pela de cima nem pela de dentro
    borda, onde = saida_do_antebraco(M, lado)
    fora = "direita" if lado == "R" else "esquerda"
    if borda == "cima" or (borda is not None and borda not in ("baixo", fora)):
        c += 300.0
    elif borda == fora:
        c += 40.0 + 400.0 * (1.0 + onde)
    c += 200.0 * avancou + 0.03 * _volta(th, 0.0)
    return c, (dobra, desvio, tor, p)


# ================================================================================================ as mãos nas pegadas

def _polo(cfg, lado):
    p = getattr(cfg, "POLO_FP", {}).get(lado)
    if p:
        return Vector(p)
    s = 1.0 if lado == "L" else -1.0           # para fora: +X na esquerda, -X na direita
    return Vector((0.45 * s, 0.25, -1.0))


def _alcance_max(esq, lado):
    return 0.995 * (esq.comprimento("upper_arm." + lado, "forearm." + lado)
                    + esq.comprimento("forearm." + lado, "hand." + lado))


def _polo_girado(M_antes, lado, H, polo0, th):
    S = M_antes["upper_arm." + lado].translation
    u = (H.translation - S).normalized()
    return Matrix.Rotation(math.radians(th), 3, u) @ polo0


def braco_no_lugar(esq, M, M_antes, lado, H, polo, dedos=None):
    """A mão do lado em H (a matriz do hand no espaço dos braços), o braço por IK com o cotovelo para o polo e o ombro
    avançando o que faltar; os dedos fechados (dedos), se vierem. Devolve o avanço do ombro (m)."""
    S = M_antes["upper_arm." + lado].translation.copy()
    v = H.translation - S
    maximo = _alcance_max(esq, lado)
    avancou = 0.0
    if v.length > maximo:
        avancou = v.length - maximo
        S = S + v.normalized() * avancou
    ag.braco(esq, M, lado, S, H.translation.copy(), H.to_3x3(), polo)
    if dedos is not None:
        esq.refazer_abaixo(M, "hand." + lado, {n: q.to_matrix().to_4x4() for n, q in dedos.items()})
    return avancou


def _melhor_cotovelo(esq, M_antes, lado, H, polo0, ths):
    melhor = None
    for th in ths:
        M = dict(M_antes)
        av = braco_no_lugar(esq, M, M_antes, lado, H, _polo_girado(M_antes, lado, H, polo0, th))
        c, info = custo_do_braco(esq, M, lado, av, th)
        if melhor is None or c < melhor[0]:
            melhor = (c, th, info)
    return melhor


def escolher_giro(esq, lado, amostras, grips, polo0, costas=None):
    """O giro da mão em volta do cabo (graus, fixo em todos os quadros) que deixa o braço melhor nas amostras (cada uma:
    (M_antes, W: a arma, y: o lugar da pegada no cabo, peso)), cada uma com o melhor lado do cotovelo. grips(y, phi): a
    pegada G. Devolve (o giro, o custo)."""
    global ULNAR_AGORA

    def total(phi, ths):
        global ULNAR_AGORA
        soma, pior, infos = 0.0, 0.0, []
        for M_antes, W, y, peso, ulnar in amostras:
            ULNAR_AGORA = ulnar
            H = W @ grips(y, phi).inverted()
            c, th, info = _melhor_cotovelo(esq, M_antes, lado, H, polo0, ths)
            if costas is not None:
                zc = (H.to_3x3() @ Vector((0.0, 0.0, 1.0))).normalized()
                c += 0.5 * math.degrees(zc.angle(Vector(costas).normalized()))
            soma += peso * c
            pior = max(pior, c)
            infos.append(info)
        return soma / sum(a[3] for a in amostras) + 0.5 * pior, infos

    grosso = list(range(-180, 180, 20))
    res = [(total(phi, grosso)[0], phi) for phi in range(0, 360, 10)]
    _, phi0 = min(res)
    fino = []
    for phi in range(phi0 - 10, phi0 + 11, 2):
        c, infos = total(phi % 360, GIROS_DO_COTOVELO)
        fino.append((c, phi % 360, infos))
    c, phi, infos = min(fino, key=lambda r: r[0])
    pior = tuple(max(abs(i[k]) for i in infos) for k in range(3)) + (min(i[3] for i in infos),)
    print(f"    {lado}: giro {phi}°, custo {c:.0f}; o pior nas amostras: a dobra {pior[0]:.0f}°, o desvio {pior[1]:.0f}°, "
          f"a torção {pior[2]:.0f}°, o braço a {pior[3] * 100:.0f} cm da câmera")
    return phi, c


def _cotovelos(esq, quadros, lado, Hs, polo0, th_ponta=None, laco=False):
    """O lado do cotovelo (graus em volta da linha do ombro ao pulso) quadro a quadro, sem saltos: a menor soma do custo
    de cada quadro e da mudança de um quadro para o outro (Viterbi). quadros: [(f, M_antes)]; Hs: {f: H}; th_ponta: o
    lado do Idle, puxado no primeiro e no último quadro. Devolve {f: graus}."""
    ths = GIROS_DO_COTOVELO
    tabela = []
    for f, M_antes in quadros:
        linha = []
        for th in ths:
            M = dict(M_antes)
            av = braco_no_lugar(esq, M, M_antes, lado, Hs[f], _polo_girado(M_antes, lado, Hs[f], polo0, th))
            linha.append(custo_do_braco(esq, M, lado, av, th)[0])
        tabela.append(linha)
    if th_ponta is not None:
        for k in (0, -1):
            tabela[k] = [c + 2.0 * _volta(th, th_ponta) for c, th in zip(tabela[k], ths)]
    acum = list(tabela[0])
    volta = []
    for linha in tabela[1:]:
        novo, de = [], []
        for j, th in enumerate(ths):
            k = min(range(len(ths)), key=lambda k: acum[k] + _passo(th, ths[k]))
            novo.append(acum[k] + _passo(th, ths[k]) + linha[j])
            de.append(k)
        acum = novo
        volta.append(de)
    j = min(range(len(ths)), key=lambda j: acum[j])
    escolha = [j]
    for de in reversed(volta):
        j = de[j]
        escolha.append(j)
    escolha.reverse()
    return {f: ths[j] for (f, _), j in zip(quadros, escolha)}


def _rolar(W, graus, p_arma):
    """A arma W girada em volta do eixo do cabo (o Y dela), pelo ponto p_arma (no espaço da arma)."""
    if not graus:
        return W
    p = W @ p_arma
    a = (W.to_3x3() @ Vector((0.0, 1.0, 0.0))).normalized()
    return Matrix.Translation(p) @ Matrix.Rotation(math.radians(graus), 4, a) @ Matrix.Translation(-p) @ W


def _depurar(esq, nome, qs, lados, Hs, peg):
    """(para ajustar) o custo de cada lado do cotovelo em alguns quadros do clipe"""
    for f, M_antes, _, _ in qs[::max(1, len(qs) // 4)]:
        for lado in lados:
            linha = []
            for th in GIROS_DO_COTOVELO[::2]:
                M = dict(M_antes)
                av = braco_no_lugar(esq, M, M_antes, lado, Hs[lado][f], _polo_girado(M_antes, lado, Hs[lado][f],
                                                                                      peg[lado]["polo"], th))
                c, (d, dv, t, p) = custo_do_braco(esq, M, lado, av, th)
                E = M["forearm." + lado].translation
                linha.append(f"{th:+d}:{c:.0f}({d:+.0f}/{dv:+.0f}/{t:+.0f} {p * 100:.0f} e{cotovelo_na_tela(E) * 100:+.0f})")
            print(f"    [{nome} {f} {lado}] " + " ".join(linha))


def _mapa(esq, lados, amostra, grips, cfg, ys, diagonal, diagonal_antes):
    """(para ajustar) no Idle, as combinações de diagonal, giro no cabo e cotovelo com o pulso dentro dos limites"""
    M_antes, W, yR = amostra[:3]
    for lado in lados:
        y = yR if lado == "R" else ys["L"]
        boas = []
        for dg in [int(x) for x in os.environ.get("DM_MAPA_DG", "15,30,45,60").split(",")]:
            diagonal[lado] = dg
            for phi in range(0, 360, 15):
                H = W @ grips(lado)(y, phi).inverted()
                for th in range(-180, 180, 15):
                    M = dict(M_antes)
                    av = braco_no_lugar(esq, M, M_antes, lado, H, _polo_girado(M_antes, lado, H, _polo(cfg, lado), th))
                    d, dv, t = pulso(esq, M, lado)
                    E = M["forearm." + lado].translation
                    if abs(d) <= 60 and abs(t) <= 120 and cotovelo_na_tela(E) < 0 and perto(capsulas(M, lado)) > 0.27:
                        boas.append((abs(dv) + abs(d) * 0.5 + abs(t) * 0.5, dg, phi, th, d, dv, t, cotovelo_na_tela(E), av))
        boas.sort()
        print(f"  [mapa {lado}] {len(boas)} boas; " + " | ".join(
            f"dg{b[1]} phi{b[2]} th{b[3]}: {b[4]:+.0f}/{b[5]:+.0f}/{b[6]:+.0f} e{b[7] * 100:+.0f} av{b[8] * 100:.0f}" for b in boas[:12]))
        diagonal.pop(lado, None)


def _ajuste(v, pivo):
    """O ajuste do lugar da faca e da tocha no punho (09/11/2026): v = (inclinação em volta do X da arma, em volta do Z,
    em graus; o movimento x, y, z em m), no espaço da arma, girando pelo ponto da pegada (pivo)."""
    rx, rz, tx, ty, tz = v
    return (Matrix.Translation(Vector((tx, ty, tz)) + pivo) @ Matrix.Rotation(math.radians(rx), 4, "X")
            @ Matrix.Rotation(math.radians(rz), 4, "Z") @ Matrix.Translation(-pivo))


def _custo_rapido(esq, amostras, g, polo0, ths):
    """O custo do braço direito nas amostras com o melhor giro da mão no cabo (de 15 em 15 graus), sem imprimir nada."""
    global ULNAR_AGORA
    melhor = None
    for phi in range(0, 360, 15):
        soma, pior = 0.0, 0.0
        for M_antes, W, y, peso, ulnar in amostras:
            ULNAR_AGORA = ulnar
            c = _melhor_cotovelo(esq, M_antes, "R", W @ g(y, phi).inverted(), polo0, ths)[0]
            soma += peso * c
            pior = max(pior, c)
        t = soma / sum(a[3] for a in amostras) + 0.5 * pior
        if melhor is None or t < melhor:
            melhor = t
    ULNAR_AGORA = 0.0
    return melhor


def _buscar_ajuste(esq, amostras, g, polo0, pivo, limite):
    """O ajuste (_ajuste) que deixa o braço direito melhor, dentro do limite (graus de inclinação, m de movimento): uma
    descida coordenada a coordenada, de passos grandes para pequenos."""
    am = amostras[::2] + [amostras[-1]]
    ths = GIROS_DO_COTOVELO[::3]
    v = [0.0] * 5
    melhor = _custo_rapido(esq, [(Ma, W @ _ajuste(v, pivo), y, p, u) for Ma, W, y, p, u in am], g, polo0, ths)
    custo0 = melhor
    for passo_g, passo_m in ((5.0, 0.015), (2.5, 0.0075)):
        mudou = True
        while mudou:
            mudou = False
            for i in range(5):
                for s in (-1.0, 1.0):
                    w = list(v)
                    w[i] += s * (passo_g if i < 2 else passo_m)
                    if math.hypot(w[0], w[1]) > limite[0] + 1e-6 or Vector(w[2:]).length > limite[1] + 1e-6:
                        continue
                    c = _custo_rapido(esq, [(Ma, W @ _ajuste(w, pivo), y, p, u) for Ma, W, y, p, u in am], g, polo0,
                                      ths)
                    if c < melhor - 0.5:
                        melhor, v, mudou = c, w, True
    print(f"  o ajuste da arma no punho: inclinada {v[0]:+.1f}° (X) e {v[1]:+.1f}° (Z), movida ({v[2] * 100:+.1f}, "
          f"{v[3] * 100:+.1f}, {v[4] * 100:+.1f}) cm; o custo rápido de {custo0:.0f} para {melhor:.0f}")
    return tuple(round(x, 4) for x in v)


def _passo(a, b):
    """O custo de o cotovelo mudar de a para b (graus) de um quadro para o outro: nunca mais de PASSO_MAX graus por quadro
    (09/11/2026: 30; antes, 60, e no bastão a torção dos antebraços saltava mais de 140 graus de um quadro para o outro)."""
    d = _volta(a, b)
    return 0.6 * d + (1e4 if d > PASSO_MAX else 0.0)


def _volta(a, b):
    """A diferença entre dois ângulos (graus), pelo menor lado."""
    return abs((a - b + 180.0) % 360.0 - 180.0)


def _avaliar(rig, acao, f):
    return pegada._avaliar(rig, acao, f)


# ================================================================================================ a checagem de cada quadro

def _medir(esq, M, lados, pior, f=None, por_quadro=None):
    """As medidas de um quadro pronto (M), acumuladas no pior do clipe; com por_quadro, também a linha do quadro."""
    linha = {}
    caps = []
    for lado in lados:
        dobra, desvio, tor = pulso(esq, M, lado)
        cl = capsulas(M, lado)
        caps += cl
        linha[lado] = (dobra, desvio, tor, perto(cl), meio_da_mao(M, lado).length,
                       cotovelo_na_tela(M["forearm." + lado].translation))
    cobre = cobertura(caps)
    linha["cobre"] = cobre
    if len(lados) == 2:
        linha["cruzam"] = cruzam(M)
        linha["entre"] = (meio_da_mao(M, "R") - meio_da_mao(M, "L")).length
    for lado in lados:
        d, dv, t, p, m, e = linha[lado]
        q = pior.setdefault(lado, [0.0, 0.0, 0.0, 9.0, 9.0, -9.0])
        pior[lado] = [max(q[0], abs(d)), max(q[1], abs(dv)), max(q[2], abs(t)), min(q[3], p), min(q[4], m), max(q[5], e)]
    pior["cobre"] = max(pior.get("cobre", 0.0), cobre)
    if "cruzam" in linha:
        pior["cruzam"] = max(pior.get("cruzam", 0.0), linha["cruzam"])
    if por_quadro is not None:
        por_quadro.append((f, linha))
    return linha


def _texto_lado(lado, v):
    return (f"{lado} pulso {v[0]:+.0f}/{v[1]:+.0f}/{v[2]:+.0f}° braço {v[3] * 100:.0f} cm mão {v[4] * 100:.0f} cm "
            f"cotovelo {'NA TELA ' if v[5] > 0 else 'fora '}{v[5] * 100:+.0f} cm")


DIAGONAIS_ESCOLHIDAS = os.path.join(AQUI, "diagonais_fp.json")


def _guardar_diagonal(cfg, por_lado):
    """(09/11/2026) a diagonal do cabo na palma escolhida para cada mão (das DIAGONAIS da cfg), guardada para as fotos do
    detalhe (brancas_corpo.fotos_detalhe) mostrarem a mão como ela fica no jogo."""
    import json
    try:
        with open(DIAGONAIS_ESCOLHIDAS, encoding="utf-8") as arq:
            tudo = json.load(arq)
    except (OSError, ValueError):
        tudo = {}
    tudo[cfg.MODELO] = por_lado
    with open(DIAGONAIS_ESCOLHIDAS, "w", encoding="utf-8") as arq:
        json.dump(tudo, arq, indent=1, sort_keys=True)


def diagonal_escolhida(modelo, lado="R"):
    """A diagonal guardada por _guardar_diagonal (ou a da pegada canônica, pegada.DIAGONAL)."""
    import json
    try:
        with open(DIAGONAIS_ESCOLHIDAS, encoding="utf-8") as arq:
            return float(json.load(arq)[modelo][lado])
    except (OSError, ValueError, KeyError):
        return pegada.DIAGONAL


def _excesso(v):
    """O quanto o pulso (dobra, desvio, torção) chega perto dos limites da conferência (09/11/2026: 60/25/90 graus), o
    maior dos três, em fração do limite: para achar o pior quadro de cada clipe."""
    return max(abs(v[0]) / 60.0, abs(v[1]) / 25.0, abs(v[2]) / 90.0)


def _relatorio(nome, f0, f1, pior, avanco, por_quadro=None, todos=True):
    """O pior do clipe; e todos os quadros (todos) ou, nos outros clipes, o pior quadro de cada mão (09/11/2026)."""
    piores = []
    if not todos and por_quadro:
        for lado in [lado for lado in ("R", "L") if lado in por_quadro[0][1]]:
            f, linha = max(por_quadro, key=lambda q: _excesso(q[1][lado]))
            piores.append(f"      o pior quadro {lado}: {f:2d}: " + _texto_lado(lado, linha[lado]))
        por_quadro = por_quadro[:1]
    partes = []
    for lado in ("R", "L"):
        if lado in pior:
            v = pior[lado]
            partes.append(f"{lado} pulso {v[0]:.0f}/{v[1]:.0f}/{v[2]:.0f}° braço {v[3] * 100:.0f} cm mão {v[4] * 100:.0f} cm "
                          f"cotovelo {v[5] * 100:+.0f} cm ombro {avanco.get(lado, 0.0) * 100:.0f} cm")
    extra = f" cobre {pior['cobre'] * 100:.0f}%"
    if "cruzam" in pior:
        extra += f" cruzam {pior['cruzam'] * 1000:.0f} mm"
    print(f"  {nome} ({f0}-{f1}): o pior: " + "; ".join(partes) + extra)
    for t in piores:
        print(t)
    for f, linha in (por_quadro or []):
        t = [_texto_lado(lado, linha[lado]) for lado in ("R", "L") if lado in linha]
        x = f" cobre {linha['cobre'] * 100:.0f}%"
        if "cruzam" in linha:
            x += f" cruzam {linha['cruzam'] * 1000:.0f} mm entre {linha['entre'] * 100:.0f} cm"
        print(f"      {f:2d}: " + "; ".join(t) + x)


# ================================================================================================ os clipes

def refazer(rig, clipes, cfg, malha, no_punho):
    """Os clipes da primeira pessoa (as ações dos punhos: Idle, Walk, Run, Sacar e os socos) refeitos com a arma no lugar
    dela e as mãos nas pegadas, e o Golpe feito das chaves (nas de duas mãos). Devolve (os clipes novos, a pegada)."""
    cena = bpy.context.scene
    esq = ag.Esqueleto(rig)
    pai = {b.name: b.parent.name for b in rig.data.bones if b.parent}
    pts = pegada.pontos_da_mao_nova()
    Mw = rig.matrix_world.inverted()
    idle = next(a for a in clipes if a.name.endswith("|Idle"))
    f_idle = int(idle.frame_range[0])
    M_idle = _avaliar(rig, idle, f_idle)
    global LIMITE_TORCAO, ULNAR_AGORA, ROLAR_TUDO
    ROLAR_TUDO = getattr(cfg, "ROLAR_FP", 0.0)
    torcao_antes = LIMITE_TORCAO
    LIMITE_TORCAO = getattr(cfg, "LIMITE_TORCAO", LIMITE_TORCAO)
    duas = hasattr(cfg, "IDLE_FP")
    lados = ("R", "L") if duas else ("R",)
    ys = {"R": cfg.MAO_DIREITA, "L": cfg.MAO_ESQUERDA} if duas else {"R": getattr(cfg, "PEGADA_Y", -cfg.NO_PUNHO.y)}
    for lado in ("R", "L"):
        d0, dv0, t0 = pulso(esq, M_idle, lado)
        print(f"  (os punhos de antes, {lado}: a dobra do pulso {d0:+.0f}°, o desvio {dv0:+.0f}°, a torção {t0:+.0f}°)")

    # as poses da arma em cada quadro de cada clipe (e o y da pegada direita, que escorrega no golpe do machado)
    W_idle = Mw @ pose(cfg.IDLE_FP) if duas else None
    cabos = {}

    def cabo(y):
        k = round(y, 4)
        if k not in cabos:
            cabos[k] = pegada.cabo(malha, y)
        return cabos[k]

    diagonal = {}
    diagonal_antes = pegada.DIAGONAL

    def grips(lado):
        def g(y, phi):
            raio, meio = cabo(y)
            pegada.DIAGONAL = diagonal.get(lado, diagonal_antes)
            return pegada.pegada(esq.rest, lado, raio, meio, y, phi)
        return g

    raio_R, meio_R = cabo(ys["R"])
    pivo_R = Vector((meio_R[0], ys["R"], meio_R[1]))
    H0 = M_idle["hand.R"]
    # o ombro (a cabeça do upper_arm) deslocado, fora da tela (cfg.OMBRO_FP: {lado: (x, y, z) m}): o antebraço chega à
    # pegada por outro ângulo, e o pulso fica reto onde a arma não pode sair do lugar (a tocha)
    ombro = dict(getattr(cfg, "OMBRO_FP", {}))
    if os.environ.get("DM_OMBRO"):
        for parte in os.environ["DM_OMBRO"].split(";"):
            lado, xyz = parte.split(":")
            ombro[lado] = tuple(float(c) for c in xyz.split(","))

    def com_ombro(M_antes):
        if not ombro:
            return M_antes
        M = dict(M_antes)
        for lado, off in ombro.items():
            M["upper_arm." + lado] = Matrix.Translation(Vector(off)) @ M["upper_arm." + lado]
        return M

    clipe_poses = []           # [(nome, acao, [(f, M_antes, W, yR)])]
    for acao in clipes:
        nome = acao.name.split("|", 1)[1]
        f0, f1 = int(acao.frame_range[0]), int(acao.frame_range[1])
        qs = []
        for f in range(f0, f1 + 1):
            M_antes = com_ombro(_avaliar(rig, acao, f))
            if duas:
                Hf = M_antes["hand.R"]
                giro = (Hf.to_3x3() @ H0.to_3x3().inverted()).to_4x4()
                escala = getattr(cfg, "GIRO_DOS_PUNHOS", 1.0)
                if escala != 1.0:
                    giro = Quaternion().slerp(giro.to_quaternion(), escala).to_matrix().to_4x4()
                p = W_idle @ pivo_R
                W = Matrix.Translation(p + (Hf.translation - H0.translation)) @ giro @ Matrix.Translation(-p) @ W_idle
            else:
                W = M_antes["hand.R"] @ no_punho          # a faca e a tocha: onde estavam
            qs.append((f, M_antes, W, ys["R"]))
        clipe_poses.append((nome, acao, qs))
    # (09/11/2026) GIRAR_DIREITA_NO_GOLPE: a arma não rola em volta do cabo (o "alto" das chaves fica), mas a mão direita,
    # a que escorrega pelo cabo, pode girar nele (o mesmo caminho do ROLAR_GOLPE, com o giro da arma sempre 0)
    rolar_livre = duas and getattr(cfg, "ROLAR_GOLPE", False)
    rolar_golpe = rolar_livre or (duas and getattr(cfg, "GIRAR_DIREITA_NO_GOLPE", False))
    if duas and hasattr(cfg, "GOLPE_FP"):
        raio_L, meio_L = cabo(ys["L"])
        pivo = (pivo_R + Vector((meio_L[0], ys["L"], meio_L[1]))) / 2.0
        idle_M = com_ombro(_avaliar(rig, idle, f_idle))

        def golpe_poses(rolagens):
            chaves = [(f, _rolar(Mw @ pose(c, cfg.IDLE_FP), rolagens.get(f, 0.0), pivo)) for f, c in cfg.GOLPE_FP]
            Ws = curva(chaves, pivo)
            yR = curva_escalar([(f, c.get("direita", ys["R"])) for f, c in cfg.GOLPE_FP])
            return ("Golpe", None, [(f, idle_M, Ws[f], yR[f]) for f in sorted(Ws)]), yR

        g, yR = golpe_poses({})
        clipe_poses.append(g)
        if any("direita" in c for _, c in cfg.GOLPE_FP):
            print("  o golpe: a pegada direita em y " + ", ".join(f"{f}:{yR[f]:.3f}" for f in sorted(yR)))

    # o giro de cada mão no cabo (fixo): o melhor nas amostras (o Idle pesa mais; os socos das de duas mãos não contam; o
    # Golpe só conta se as chaves não giram depois, ROLAR_GOLPE)
    usados = ({"Idle", "Walk", "Run", "Sacar"} | (set() if rolar_golpe else {"Golpe"})) if duas else         {"Idle", "Walk", "Run", "Sacar", "SocoDireito"}
    amostras = []
    for nome, _, qs in clipe_poses:
        if nome not in usados:
            continue
        passo = 2 if nome == "Golpe" else max(1, len(qs) // 3)
        ulnar = getattr(cfg, "LIMITE_ULNAR_GOLPE", 0.0) if nome == "Golpe" else 0.0
        for i in range(0, len(qs), passo):
            f, M_antes, W, yR_f = qs[i]
            amostras.append((M_antes, W, yR_f, 4.0 if (nome == "Idle" and i == 0) else (0.3 if nome == "Golpe" else 1.0),
                             ulnar))
        f, M_antes, W, yR_f = qs[-1]
        amostras.append((M_antes, W, yR_f, 1.0, ulnar))
    # (09/11/2026) a mão esquerda do machado (a arma não rola no golpe, só a direita gira no cabo: GIRAR_DIREITA_NO_GOLPE)
    # fica no Golpe como as chaves mandam: os quadros dele também contam na diagonal e no giro dela (antes, com a diagonal
    # boa só para o Idle, o pulso esquerdo dobrava 80 graus no golpe)
    golpe_L = []
    if duas and rolar_golpe and not rolar_livre:
        for nome, _, qs in clipe_poses:
            if nome == "Golpe":
                ulnar = getattr(cfg, "LIMITE_ULNAR_GOLPE", 0.0)
                golpe_L = [(M_antes, W, ys["L"], 0.3, ulnar) for f, M_antes, W, _ in qs]
    ajuste_v = getattr(cfg, "AJUSTE_FP", None)
    limite = getattr(cfg, "AJUSTE_FP_LIMITE", None)
    if os.environ.get("DM_AJUSTE_LIMITE"):          # (para testar outro limite: "graus,m")
        limite = tuple(float(x) for x in os.environ["DM_AJUSTE_LIMITE"].split(","))
        ajuste_v = None
    buscar = not duas and ajuste_v is None and limite
    if os.environ.get("DM_MAPA"):
        nm, fq = (os.environ["DM_MAPA"].split(":") + ["1"])[:2] if ":" in os.environ["DM_MAPA"] else ("Idle", "1")
        qa = next(q for n, _, qq in clipe_poses if n == nm for q in qq if q[0] == int(fq))
        _mapa(esq, lados, (qa[1], qa[2], qa[3]), grips, cfg, ys, diagonal, diagonal_antes)
    peg, peg_phi = {}, {}
    for lado in lados:
        costas = getattr(cfg, "COSTAS", {}).get(lado)
        am = amostras if lado == "R" else [(a, W, ys["L"], p, u) for a, W, _, p, u in amostras] + golpe_L
        # a diagonal do cabo na palma (graus: a mão inclinada; as DIAGONAIS da cfg, a melhor delas para o pulso, ou a da
        # pegada canônica, pegada.DIAGONAL) e o giro da mão no cabo; na faca e na tocha, com cada diagonal, também o
        # ajuste do lugar da arma no punho (buscado, se a cfg não traz o AJUSTE_FP)
        melhor = None
        for dg in getattr(cfg, "DIAGONAIS", {}).get(lado, (diagonal_antes,)):
            diagonal[lado] = dg
            print(f"    {lado}: a diagonal {dg}°")
            aj = ajuste_v
            if lado == "R" and buscar:
                aj = _buscar_ajuste(esq, am, grips("R"), _polo(cfg, "R"), pivo_R, limite)
            am_dg = am
            if aj is not None and not duas:
                A = _ajuste(aj, pivo_R)
                am_dg = [(Ma, W @ A, y, p, u) for Ma, W, y, p, u in am]
            phi = getattr(cfg, "GIRO_NO_CABO", {}).get(lado)
            c = 0.0
            if phi is None:
                phi, c = escolher_giro(esq, lado, am_dg, grips(lado), _polo(cfg, lado), costas)
            if melhor is None or c < melhor[0]:
                melhor = (c, dg, phi, aj)
        _, diagonal[lado], phi, aj = melhor
        if lado == "R" and not duas:
            ajuste_v = aj
        raio, meio = cabo(ys[lado])
        G = grips(lado)(ys[lado], phi)
        dedos = pegada.dedos_no_cabo(esq.rest, pai, lado, pts, raio + getattr(cfg, "DEDOS_RAIO_A_MAIS", 0.0))
        peg[lado] = {"y": ys[lado], "raio": raio, "meio": meio, "phi": phi, "G": G, "dedos": dedos,
                     "polo": _polo(cfg, lado), "polegar": dict(getattr(pegada._polegar, "ultimo", {}))}
        peg[lado]["diagonal"] = diagonal[lado]
        peg_phi[lado] = phi
        print(f"  pegada {lado}: o cabo em y {ys[lado]:.3f}, raio {raio * 100:.2f} cm, a diagonal {diagonal[lado]}°, giro {phi}°; polegar a "
              f"{peg[lado]['polegar'].get('longe_do_alvo', 0) * 100:.1f} cm do indicador")
    # (09/11/2026) a faca e a tocha: o lugar da arma no punho com um ajuste pequeno (AJUSTE_FP na cfg: a inclinação e o
    # movimento, _ajuste; ou AJUSTE_FP_LIMITE: buscado pelo custo do braço, com a diagonal), para o pulso ficar natural
    ajuste = _ajuste(ajuste_v, pivo_R) if (ajuste_v is not None and not duas) else Matrix.Identity(4)
    if ajuste_v is not None and not duas:
        print(f"  o ajuste da arma no punho (AJUSTE_FP): {ajuste_v}")
        clipe_poses = [(n, a, [(f, Ma, W @ ajuste, y) for f, Ma, W, y in qs]) for n, a, qs in clipe_poses]
    for lado in lados:
        peg[lado]["ajuste"] = ajuste if lado == "R" else Matrix.Identity(4)
    _guardar_diagonal(cfg, {lado: diagonal[lado] for lado in lados})
    phi_golpe = {}
    if rolar_golpe and hasattr(cfg, "GOLPE_FP"):
        # cada chave do golpe gira em volta do cabo o que deixa os braços melhores (a dobra do pulso vira para a palma ou
        # para as costas em vez de ir para o lado; no machado, o fio sem virar para o lado oposto ao "alto" da chave); nas
        # chaves em que a mão direita escorrega pelo cabo ("direita"), ela também pode girar em volta dele (a mão solta,
        # deslizando); tudo escolhido junto, chave a chave, sem saltos nas torções dos antebraços (Viterbi)
        ULNAR_AGORA = getattr(cfg, "LIMITE_ULNAR_GOLPE", 0.0)
        rolagens, phi_chave = {}, {}
        # os estados de cada chave: (custo, giro do cabo, giro da mão direita, torção esquerda, torção direita)
        H = Mw @ pose(cfg.IDLE_FP) @ grips("R")(ys["R"], peg_phi["R"]).inverted()
        torR0 = _melhor_cotovelo(esq, idle_M, "R", H, _polo(cfg, "R"), GIROS_DO_COTOVELO[::2])[2][2]
        torL0 = 0.0
        if "L" in lados:
            H = Mw @ pose(cfg.IDLE_FP) @ grips("L")(ys["L"], peg_phi["L"]).inverted()
            torL0 = _melhor_cotovelo(esq, idle_M, "L", H, _polo(cfg, "L"), GIROS_DO_COTOVELO[::2])[2][2]
        camadas = []
        for f, c in cfg.GOLPE_FP:
            if "ponto" not in c:
                camadas.append((f, [(0.0, 0, peg_phi["R"], torL0, torR0)]))
                continue
            W0 = Mw @ pose(c, cfg.IDLE_FP)
            yk = c.get("direita", ys["R"])
            livre = "direita" in c
            estados = []
            for rg in (range(-180, 180, 15) if rolar_livre else (0,)):
                W = _rolar(W0, rg, pivo)
                custo = 0.05 * abs(rg)
                if getattr(cfg, "FIO_NO_GOLPE", False):
                    # o fio (o -Z da arma) para o lado do "alto" de baixo: não vira para cima
                    fio = (W.to_3x3() @ Vector((0.0, 0.0, -1.0))).normalized()
                    alto0 = (W0.to_3x3() @ Vector((0.0, 0.0, 1.0))).normalized()
                    custo += 150.0 * max(0.0, 0.2 - (-fio).dot(alto0))
                torL = torL0
                if "L" in lados:
                    H = W @ grips("L")(ys["L"], peg_phi["L"]).inverted()
                    cL, _, infoL = _melhor_cotovelo(esq, idle_M, "L", H, _polo(cfg, "L"), GIROS_DO_COTOVELO[::2])
                    custo += cL
                    torL = infoL[2]
                opcoes = []
                for phi in (range(0, 360, 30) if livre else [peg_phi["R"]]):
                    H = W @ grips("R")(yk, phi).inverted()
                    cR, _, infoR = _melhor_cotovelo(esq, idle_M, "R", H, _polo(cfg, "R"), GIROS_DO_COTOVELO[::2])
                    opcoes.append((cR + 0.05 * _volta(phi, peg_phi["R"]), phi, infoR[2]))
                opcoes.sort()
                for cR, phi, torR in opcoes[:3]:
                    estados.append((custo + cR, rg, phi, torL, torR))
            camadas.append((f, estados))

        def ligacao(a, b):
            # de uma chave para a outra: o cabo sem girar demais, a mão direita sem girar demais no cabo, e as torções
            # dos antebraços sem dar a volta (mais de 60 graus de uma chave para a outra custa caro)
            return (0.1 * _volta(a[1], b[1]) + 0.3 * _volta(a[2], b[2]) + 4.0 * max(0.0, _volta(a[3], b[3]) - 60.0)
                    + 4.0 * max(0.0, _volta(a[4], b[4]) - 60.0))

        acum = [e[0] for e in camadas[0][1]]
        voltas = []
        for k in range(1, len(camadas)):
            ant, cur = camadas[k - 1][1], camadas[k][1]
            novo_acum, de = [], []
            for e in cur:
                i_m = min(range(len(ant)), key=lambda i: acum[i] + ligacao(ant[i], e))
                novo_acum.append(acum[i_m] + ligacao(ant[i_m], e) + e[0])
                de.append(i_m)
            acum, voltas = novo_acum, voltas + [de]
        j_m = min(range(len(acum)), key=lambda j: acum[j])
        escolha = [j_m]
        for de in reversed(voltas):
            j_m = de[j_m]
            escolha.append(j_m)
        escolha.reverse()
        for (f, estados), j_m in zip(camadas, escolha):
            e = estados[j_m]
            rolagens[f], phi_chave[f] = e[1], e[2]
        ULNAR_AGORA = 0.0
        base = peg_phi["R"]
        un = [(f, base + ((p - base + 180.0) % 360.0 - 180.0)) for f, p in sorted(phi_chave.items())]
        phi_golpe = curva_escalar(un)
        print("  o golpe: cada chave girada em volta do cabo (graus): " + ", ".join(f"{f}:{r}" for f, r in rolagens.items())
              + "; a mão direita no cabo: " + ", ".join(f"{f}:{p:.0f}" for f, p in un))
        g, _ = golpe_poses(rolagens)
        # depois, quadro a quadro: um pouco mais de giro da arma (até 60 graus) e da mão que escorrega (até 30), sem saltos
        # (Viterbi), para os quadros entre as chaves também ficarem bons
        escorrega = {f for f, c in cfg.GOLPE_FP if "direita" in c}
        f_esc = (min(escorrega), max(escorrega)) if escorrega else (0, -1)
        estados = [(dr, dp) for dr in ((-60, -45, -30, -15, 0, 15, 30, 45, 60) if rolar_livre else (0,))
                   for dp in (-30, -15, 0, 15, 30)]
        tabela, torcoes = [], []
        ULNAR_AGORA = getattr(cfg, "LIMITE_ULNAR_GOLPE", 0.0)
        for f, Ma, W, yR_f in g[2]:
            livre = f_esc[0] - 1 < f < f_esc[1] + 1
            linha, tl = [], []
            for dr, dp in estados:
                if dp and not livre:
                    linha.append(1e6)
                    tl.append({})
                    continue
                Wr = _rolar(W, dr, pivo)
                c = 0.05 * (abs(dr) + abs(dp))
                t = {}
                for lado in lados:
                    phi = (phi_golpe.get(f, peg_phi["R"]) + dp) if lado == "R" else peg_phi["L"]
                    H = Wr @ grips(lado)(yR_f if lado == "R" else ys["L"], phi).inverted()
                    cl, _, info = _melhor_cotovelo(esq, Ma, lado, H, _polo(cfg, lado), GIROS_DO_COTOVELO[::2])
                    c += cl
                    t[lado] = info[2]
                linha.append(c)
                tl.append(t)
            tabela.append(linha)
            torcoes.append(tl)
        ULNAR_AGORA = 0.0

        def passo_q(i, k, j):
            # de um quadro para o outro: o giro a mais sem saltar, e as torções dos antebraços sem passar de PASSO_MAX graus
            a, b = estados[k], estados[j]
            c = 0.3 * (abs(a[0] - b[0]) + abs(a[1] - b[1]))
            ta, tb = torcoes[i - 1][k], torcoes[i][j]
            for lado in ta:
                if lado in tb:
                    c += 4.0 * max(0.0, _volta(ta[lado], tb[lado]) - PASSO_MAX)
            return c

        acum, volta = list(tabela[0]), []
        for i in range(len(estados)):
            if estados[i] != (0, 0):
                acum[i] += 1e5                         # o primeiro quadro é o Idle
        for i, linha in enumerate(tabela[1:], start=1):
            novo, de = [], []
            for j in range(len(estados)):
                k = min(range(len(estados)), key=lambda k: acum[k] + passo_q(i, k, j))
                novo.append(acum[k] + passo_q(i, k, j) + linha[j])
                de.append(k)
            acum, volta = novo, volta + [de]
        j = min(range(len(estados)), key=lambda j: acum[j] + (0.0 if estados[j] == (0, 0) else 1e5))
        escolha = [j]
        for de in reversed(volta):
            j = de[j]
            escolha.append(j)
        escolha.reverse()
        novo_g = []
        for (f, Ma, W, yR_f), j in zip(g[2], escolha):
            dr, dp = estados[j]
            novo_g.append((f, Ma, _rolar(W, dr, pivo), yR_f))
            if f in phi_golpe or dp:
                phi_golpe[f] = phi_golpe.get(f, peg_phi["R"]) + dp
        print("  o golpe, quadro a quadro (o giro a mais do machado / da mão direita): " + " ".join(
            f"{f}:{estados[j][0]}/{estados[j][1]}" for (f, _, _, _), j in zip(g[2], escolha)))
        g = ("Golpe", None, novo_g)
        clipe_poses = [x for x in clipe_poses if x[0] != "Golpe"] + [g]
    dedos_y = {}

    def dedos_em(lado, y):
        if lado == "L" or abs(y - ys[lado]) < 1e-4:
            return peg[lado]["dedos"]
        k = round(cabo(y)[0] * 2000) / 2000
        if k not in dedos_y:
            pegada.DIAGONAL = diagonal[lado]
            dedos_y[k] = pegada.dedos_no_cabo(esq.rest, pai, lado, pts, k + getattr(cfg, "DEDOS_RAIO_A_MAIS", 0.0))
        return dedos_y[k]

    def mao_H(lado, W, y, f=None, nome=None):
        phi = phi_golpe[f] if (lado == "R" and nome == "Golpe" and f in phi_golpe) else peg[lado]["phi"]
        return W @ grips(lado)(y, phi).inverted()

    # o lado do cotovelo, quadro a quadro, e os clipes gravados
    th_idle = {}
    novos = []
    golpe_prefixo = idle.name.split("|", 1)[0]
    ordem = sorted(clipe_poses, key=lambda c: c[0] != "Idle")
    for nome, acao, qs in ordem:
        ULNAR_AGORA = getattr(cfg, "LIMITE_ULNAR_GOLPE", 0.0) if nome == "Golpe" else 0.0
        Hs = {lado: {f: mao_H(lado, W, yR_f if lado == "R" else ys["L"], f, nome) for f, _, W, yR_f in qs}
              for lado in lados}
        ths = {}
        for lado in lados:
            ths[lado] = _cotovelos(esq, [(f, Ma) for f, Ma, _, _ in qs], lado, Hs[lado], peg[lado]["polo"],
                                   th_ponta=th_idle.get(lado))
            if nome == "Idle":
                th_idle[lado] = ths[lado][qs[0][0]]
        if os.environ.get("DM_DEPURAR") and nome in os.environ["DM_DEPURAR"].split(","):
            _depurar(esq, nome, qs, lados, Hs, peg)
        quadros, pior, avanco, por_quadro = {}, {}, {}, []
        for f, M_antes, W, yR_f in qs:
            M = dict(M_antes)
            for lado in lados:
                H = Hs[lado][f]
                polo = _polo_girado(M_antes, lado, H, peg[lado]["polo"], ths[lado][f])
                av = braco_no_lugar(esq, M, M_antes, lado, H, polo, dedos_em(lado, yR_f if lado == "R" else ys["L"]))
                avanco[lado] = max(avanco.get(lado, 0.0), av)
            quadros[f] = M
            _medir(esq, M, lados, pior, f, por_quadro)
        if acao is not None:
            velho = acao.name
            acao.name = velho + "_antes"
        else:
            velho = golpe_prefixo + "|Golpe"
        novos.append(pegada._gravar(rig, esq, velho, quadros))
        _relatorio(velho, qs[0][0], qs[-1][0], pior, avanco, por_quadro, todos=(nome == "Golpe"))
        print("    cotovelo (graus): " + "; ".join(
            f"{lado} " + " ".join(str(ths[lado][f]) for f, _, _, _ in qs[::max(1, len(qs) // 8)]) for lado in lados))
    for a in clipes:
        bpy.data.actions.remove(a)
    pegada.DIAGONAL = diagonal_antes
    LIMITE_TORCAO = torcao_antes
    return novos, peg
