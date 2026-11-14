# As ações de cada bicho (os nomes do Docs/20 do Pedro: as poses comuns e os gestos de cada espécie). Rode depois do
# animais_kit.py, do animais_quadrupede.py e do animais_poses.py, no mesmo dicionário.
#
# Cada grupo (o canídeo, o cervídeo, o urso...) tem a função das ações dele em GRUPOS; os quadrúpedes dividem os
# construtores daqui (de pé, sentar, deitar, nadar, morrer), cada um com o parâmetro s de 0 (de pé) a 1 (a pose), para
# as transições refazerem o IK a cada quadro (os pés não escorregam).

import math
from mathutils import Vector, Quaternion

# andar, trotar, correr (m/s): a tabela do Docs/20
VELOCIDADES = {
    "Lobo": (1.5, 3.0, 6.8), "Coiote": (1.4, 2.8, 6.4), "Cervo": (1.3, 2.6, 6.0), "Alce": (1.4, 2.8, 6.0),
    "UrsoNegro": (1.2, 2.6, 5.8), "UrsoPardo": (1.3, 2.8, 6.0), "Coelho": (0.8, 2.2, 5.5), "Castor": (0.8, 1.5, 3.0),
    "Guaxinim": (0.9, 2.0, 4.5), "Gamba": (0.7, 1.5, 3.0),
}
TORSO = ("Quadril", "Pelve", "Coluna_1", "Coluna_2", "Coluna_3", "Peito")


def especie(nome):
    return nome.split("_")[0]


def velocidades(nome):
    v = VELOCIDADES.get(especie(nome), (1.0, 2.0, 4.0))
    if nome.endswith("Filhote"):
        v = tuple(x * 0.75 for x in v)
    return v


def giro_yz(de, para):
    """O giro em volta do X (em radianos) que leva a direção de (no plano YZ) à direção para."""
    a = math.atan2(de.z, de.y)
    b = math.atan2(para.z, para.y)
    d = b - a
    while d > math.pi:
        d -= 2 * math.pi
    while d < -math.pi:
        d += 2 * math.pi
    return d


def curva(t, chaves):
    """Interpola (suave) uma lista de (tempo, valor), com valores número ou tupla."""
    if t <= chaves[0][0]:
        return chaves[0][1]
    for (t0, v0), (t1, v1) in zip(chaves, chaves[1:]):
        if t <= t1:
            u = suave((t - t0) / max(t1 - t0, 1e-9))
            if isinstance(v0, (tuple, list)):
                return tuple(a + (b - a) * u for a, b in zip(v0, v1))
            return v0 + (v1 - v0) * u
    return chaves[-1][1]


# ---------------------------------------------------------------------------------------------------------------------
# Os ajudantes da pose
# ---------------------------------------------------------------------------------------------------------------------

def respirar(b, pose, t, forca=1.0, vezes=2):
    r = math.sin(TAU * t * vezes)
    if b.tem("Peito"):
        pose.girar("Peito", qx(-0.8 * forca * r))
    pose.mover("Quadril", (0, 0, 0.002 * b.perna * forca * r))


def orelhas(b, pose, frente=0.0, lado=0.0, tremor=0.0):
    """As orelhas: frente (+) gira a ponta para a frente (- para trás, deitadas); lado abre (+); tremor é um giro rápido."""
    for s, l in ((1, "L"), (-1, "R")):
        o = "Orelha." + l
        if b.tem(o):
            pose.girar(o, qx(-frente) @ qy(s * lado) @ qz(s * tremor))


def tremida(t, centro, largura=0.04):
    d = (t - centro) / largura
    if abs(d) >= 1:
        return 0.0
    return math.sin(d * math.pi * 3) * (1 - abs(d))


def cauda(b, pose, x=0.0, z=0.0, onda=0.0, t=0.0, fase=0.7, vezes=1):
    """A cauda: x abaixa (+) ou levanta (-) a cauda inteira (repartido), z vira para o lado; onda = o abano."""
    n = len(b.cauda)
    for i, o in enumerate(b.cauda):
        w = 1.0 / max(n, 1)
        pose.girar(o, qz(z * w + onda * math.sin(TAU * t * vezes - fase * i)) @ qx(-x * w))


def boca(b, pose, abrir):
    if b.tem("Mandibula"):
        pose.girar("Mandibula", qx(abrir))


def focinho(b, pose):
    Rw, Hw = b.fk(pose)
    return b.c.ponta(Rw, Hw, "Cabeca")


def cabeca_ate_z(b, pose, z_alvo, vira=0.0, maximo=70.0, pesos=None, corpo=True, passos_corpo=10):
    """Abaixa a cabeça (repartida no pescoço, até o máximo) até a ponta do focinho chegar à altura z_alvo; se não
    alcança e corpo, o corpo da frente desce e inclina (as patas da frente dobram pelo IK depois). Mexe na pose."""
    base = pose.copia()
    for k in range(passos_corpo):
        p = base.copia()
        if k:
            p.girar("Quadril", qx(2.5 * k))
            p.mover("Quadril", (0, 0, -0.02 * k * b.perna))
        lo, hi = -60.0, maximo
        for _ in range(16):
            m = 0.5 * (lo + hi)
            p2 = p.copia()
            b.olhar(p2, abaixar=m, virar=vira, pesos=pesos)
            if focinho(b, p2).z > z_alvo:
                lo = m
            else:
                hi = m
        b.olhar(p, abaixar=0.5 * (lo + hi), virar=vira, pesos=pesos)
        if not corpo or focinho(b, p).z <= z_alvo + 0.015 * b.perna:
            break
    pose.R, pose.T, pose.S = p.R, p.T, p.S
    return 0.5 * (lo + hi)


def mascara_torso(b):
    """Os vértices do tronco (o osso de maior peso é da coluna): para o chão de deitar e sentar."""
    nomes, W = pesos(b.ob)
    dono = np.argmax(W, 1)
    ok = np.array([n in TORSO or n.startswith("Pescoco") for n in nomes])
    return ok[dono]


def chao_de(b, pose, mascara):
    V = b.malha(pose)
    return float(V[mascara, 2].min()) if mascara.any() else float(V[:, 2].min())


def acertar_altura(b, construir, mascara, alvo=0.0, vezes=3, d0=0.0):
    """Acha o quanto o corpo desce (dz) para o ponto mais baixo da máscara ficar na altura alvo; construir(dz) -> pose."""
    dz = d0
    for _ in range(vezes):
        z = chao_de(b, construir(dz), mascara)
        dz += alvo - z
    return dz


def nao_entrar(b, pose):
    """Se a malha entrou no chão, sobe o corpo (só sobe)."""
    z = b.chao_da_pose(pose)
    if z < -0.002 * b.k:
        pose.mover("Quadril", (0, 0, -z))
    return pose


# ---------------------------------------------------------------------------------------------------------------------
# De pé, com as patas paradas
# ---------------------------------------------------------------------------------------------------------------------

def em_pe(b, mexer=None, pes=None, giros3=None, giros4=None, firmar=0.0):
    """De pé com as quatro patas no lugar do repouso (ou nos pés dados), depois do corpo mexido por mexer(pose)."""
    pose = Pose()
    if mexer:
        mexer(pose)
    if firmar:
        b.firmar_cabeca(pose, firmar)
    alvos = {ch: (pes or {}).get(ch, b.pe_repouso(ch)) for ch in b.pernas}
    b.pes_no_chao(pose, alvos, giros3, giros4)
    return pose


def acao_parado(b, n=72, mexer_extra=None):
    def f(t):
        def mexer(p):
            respirar(b, p, t)
            p.mover("Quadril", (0.004 * b.perna * math.sin(TAU * t), 0, 0))
            p.girar("Quadril", qy(0.8 * math.sin(TAU * t)))
            b.olhar(p, abaixar=2 * math.sin(TAU * t), virar=12 * math.sin(TAU * t + 0.6) * suave(abs(math.sin(TAU * t))))
            orelhas(b, p, tremor=10 * tremida(t, 0.62) + 6 * tremida(t, 0.2, 0.03))
            cauda(b, p, onda=3, t=t)
            if mexer_extra:
                mexer_extra(p, t)
        return em_pe(b, mexer)
    return gravar_ciclo(b, "Parado", n, f)


def acao_marcha(b, nome, marcha, v, extra=None):
    n, T = periodo(b, marcha, v)
    return gravar_ciclo(b, nome, n, lambda t: passo(b, marcha, t, v, T, extra))


def marchas(b, ajustes=None):
    m = {k: dict(v) for k, v in MARCHAS_PADRAO.items()}
    for k, a in (ajustes or {}).items():
        m[k].update(a)
    return m


def acao_alerta(b, n=48, cabeca=-18.0, orelha=14.0, cauda_x=-8.0, nome="Alerta", mexer_extra=None):
    def f(t):
        def mexer(p):
            respirar(b, p, t, 1.4, 3)
            p.girar("Quadril", qx(-1.5))
            b.olhar(p, abaixar=cabeca + 2 * math.sin(TAU * t * 2), virar=10 * math.sin(TAU * t))
            orelhas(b, p, frente=orelha, tremor=6 * tremida(t, 0.5, 0.05))
            cauda(b, p, x=cauda_x, onda=1.5, t=t)
            if mexer_extra:
                mexer_extra(p, t)
        return em_pe(b, mexer)
    return gravar_ciclo(b, nome, n, f)


def acao_comer(b, n=60, nome="Comer", puxar=0.0, mastigar=8.0, z_focinho=None, abre_pernas=0.0, vira=0.0,
               maximo=70.0, passos_corpo=10):
    """A cabeça no chão (pastando ou na carcaça): mastiga e, com puxar, dá trancos para trás."""
    zf = z_focinho if z_focinho is not None else 0.03 * b.perna

    def f(t):
        def mexer(p):
            respirar(b, p, t, 0.8)
            p.girar("Quadril", qx(4.0))
            p.mover("Quadril", (0, 0.02 * b.perna, -0.03 * b.perna))
            tranco = puxar * max(0.0, math.sin(TAU * t * 2)) ** 2
            p.mover("Quadril", (0, tranco * 0.05 * b.perna, 0))
            cabeca_ate_z(b, p, zf + tranco * 0.06 * b.perna, vira=vira, maximo=maximo, passos_corpo=passos_corpo)
            boca(b, p, mastigar * (0.5 + 0.5 * math.sin(TAU * t * 6)))
            orelhas(b, p, tremor=8 * tremida(t, 0.3) + 8 * tremida(t, 0.8))
            cauda(b, p, onda=4, t=t, vezes=2)
        pes = None
        if abre_pernas:
            pes = {}
            for ch in ("D.L", "D.R"):
                if ch in b.pernas:
                    pe = b.pe_repouso(ch)
                    pe.x *= 1 + abre_pernas
                    pe.y -= 0.08 * b.perna * abre_pernas * 3
                    pes[ch] = pe
        return em_pe(b, mexer, pes=pes)
    return gravar_ciclo(b, nome, n, f)


# ---------------------------------------------------------------------------------------------------------------------
# Sentar, deitar, dormir, nadar e morrer (os quadrúpedes)
# ---------------------------------------------------------------------------------------------------------------------

class Construtores:
    """As poses de sentar e deitar com o parâmetro s (0 de pé, 1 a pose), com a altura do corpo achada pela malha."""

    def __init__(self, b, estilo):
        self.b = b
        self.estilo = estilo          # "canideo", "cervideo", "urso"...
        self.torso = mascara_torso(b)
        self._queda = {}

    # -- sentar --
    def sentado(self, s, dz=None, cabeca=-15.0, mexer=None):
        b = self.b
        e = suave(s)
        th = {"canideo": 32.0, "urso": 40.0, "pequeno": 45.0}.get(self.estilo, 32.0)
        if dz is None:
            dz = self.queda("sentado") * e

        def construir(dzz):
            pose = Pose()
            pose.girar("Quadril", qx(-th * e))
            pose.girar("Pelve", qx(-22.0 * e))
            pose.mover("Quadril", (0, 0.04 * b.perna * e, dzz))
            for o in b.coluna[:-1]:
                pose.girar(o, qx(-4 * e))
            b.firmar_cabeca(pose, 0.6 * e)
            b.olhar(pose, abaixar=cabeca * e)
            cauda(b, pose, x=40 * e, z=25 * e)
            if mexer:
                mexer(pose)
            Rw, Hw = b.fk(pose)
            alvos, g3 = {}, {}
            for ch, per in b.pernas.items():
                pe0 = per.pe.copy()
                if ch[0] == "D":
                    alvo = pe0 + Vector((0, 0.10 * b.perna * e, 0))
                    alvos[ch] = alvo
                    g3[ch] = 0.0
                else:
                    hip = Hw[per.ossos[0]]
                    alvo = Vector((pe0.x * (1 + 0.25 * e), hip.y - 0.35 * per.L3, pe0.z))
                    alvos[ch] = pe0.lerp(alvo, e)
                    g3[ch] = giro_yz(per.d3, Vector((0, -1, 0.05))) * e
            b.pes_no_chao(pose, alvos, g3)
            return pose
        return construir(dz)

    def queda(self, qual):
        """O quanto o corpo desce na pose cheia (achado uma vez pela malha)."""
        if qual not in self._queda:
            b = self.b
            if qual == "sentado":
                # a garupa no chão: os vértices de trás do tronco
                V0 = vertices(b.ob)
                masc = self.torso & (V0[:, 1] > b.c.cab["Quadril"].y - 0.05 * b.perna)
                self._queda[qual] = acertar_altura(b, lambda d: self.sentado(1.0, dz=d), masc, 0.0,
                                                   d0=-0.45 * b.h_quadril)
            elif qual == "deitado":
                self._queda[qual] = acertar_altura(b, lambda d: self.deitado(1.0, dz=d), self.torso, 0.0,
                                                   d0=-0.6 * b.h_quadril)
        return self._queda[qual]

    # -- deitar --
    def deitado(self, s, dz=None, cabeca=0.0, mexer=None, rolar=0.0):
        b = self.b
        e = suave(s)
        if dz is None:
            dz = self.queda("deitado") * e
        ungulado = self.estilo == "cervideo"

        def construir(dzz):
            pose = Pose()
            pose.mover("Quadril", (0, 0, dzz))
            pose.girar("Quadril", qy(rolar * e))
            pose.girar("Pelve", qy(6 * e if not ungulado else 4 * e))
            b.firmar_cabeca(pose, 0.3 * e)
            b.olhar(pose, abaixar=cabeca * e - 6 * e)
            cauda(b, pose, x=25 * e, z=20 * e)
            if mexer:
                mexer(pose)
            Rw, Hw = b.fk(pose)
            alvos, g3, g4, cv = {}, {}, {}, {}
            for ch, per in b.pernas.items():
                pe0 = per.pe.copy()
                topo = Hw[per.ossos[0]]
                Lp = per.L1 + per.L2 + per.L3
                if ch[0] == "D":
                    if ungulado:
                        # dobrada embaixo do peito: o pulso na frente, a canela para trás
                        alvo = Vector((pe0.x * 0.9, topo.y + 0.10 * Lp, pe0.z + 0.02 * b.perna))
                        dir3 = Vector((0, 1, -0.15))
                    else:
                        # esticada para a frente, o antebraço no chão
                        alvo = Vector((pe0.x * 0.9, topo.y - 0.62 * Lp, pe0.z))
                        dir3 = Vector((0, -1, -0.05))
                else:
                    lado = 1.0 if ch[2] == "L" else -1.0
                    alvo = Vector((pe0.x * (1.35 if not ungulado else 1.15), topo.y - 0.42 * Lp, pe0.z + 0.01 * b.perna))
                    dir3 = Vector((0.15 * lado, -1, -0.05))
                alvos[ch] = pe0.lerp(alvo, e)
                g3[ch] = giro_yz(per.d3, dir3) * e
                g4[ch] = (rad(60) if (ungulado and ch[0] == "D") else 0.0) * e
            b.pes_no_chao(pose, alvos, g3, g4)
            return pose
        return construir(dz)

    # -- nadar --
    def nadando(self, t, cabeca=-25.0):
        b = self.b
        pose = Pose()
        pose.girar("Quadril", qx(-12))
        pose.mover("Quadril", (0, 0, 0.01 * b.perna * math.sin(TAU * t * 2)))
        b.firmar_cabeca(pose, 0.9)
        b.olhar(pose, abaixar=cabeca + 3 * math.sin(TAU * t * 2))
        orelhas(b, pose, frente=-10)
        cauda(b, pose, x=-15, onda=6, t=t)
        Rw, Hw = b.fk(pose)
        alvos, g3 = {}, {}
        fases = {"D.L": 0.0, "D.R": 0.5, "T.L": 0.25, "T.R": 0.75}
        for ch, per in b.pernas.items():
            f = (t + fases[ch]) % 1.0
            Lp = per.L1 + per.L2 + per.L3
            topo = Hw[per.ossos[0]]
            c = Vector((per.pe.x, topo.y - (0.12 if ch[0] == "D" else -0.05) * Lp, topo.z - 0.62 * Lp))
            ry, rz = 0.28 * Lp, 0.16 * Lp
            alvos[ch] = c + Vector((0, -ry * math.cos(TAU * f), -rz * math.sin(TAU * f)))
            # dobra na volta (em cima), estica na remada (embaixo)
            g3[ch] = rad(55) * max(0.0, math.sin(TAU * f)) if ch[0] == "D" else rad(35) * max(0.0, math.sin(TAU * f))
        b.pes_no_chao(pose, alvos, g3, {ch: rad(40) for ch in alvos})
        return pose

    # -- morto --
    def morto(self, s, dz=None):
        """Caído de lado (o lado esquerdo para cima), as patas soltas."""
        b = self.b
        e = suave(s)

        def construir(dzz):
            pose = Pose()
            pose.mover("Quadril", (0, 0, dzz))
            pose.girar("Quadril", qy(88 * e))
            b.olhar(pose, abaixar=-10 * e, inclinar=-12 * e)
            boca(b, pose, 12 * e)
            orelhas(b, pose, frente=-20 * e)
            cauda(b, pose, x=10 * e, z=-25 * e)
            for ch, per in b.pernas.items():
                l = ch[2]
                s_ = 1.0 if l == "L" else -1.0
                frente = ch[0] == "D"
                b1, b2, b3, b4 = per.ossos
                pose.girar(b1, qx((-25 if frente else 20) * e) @ qy(14 * s_ * e))
                pose.girar(b2, qx((15 if frente else -20) * e))
                pose.girar(b3, qx((20 if frente else 25) * e))
                pose.girar(b4, qx(25 * e))
                if frente and b.tem("Omoplata." + l):
                    pose.girar("Omoplata." + l, qx(-10 * e))
            return pose
        if dz is None:
            if "morto" not in self._queda:
                self._queda["morto"] = acertar_altura(b, construir, np.ones(len(b.ob.data.vertices), bool), 0.0,
                                                      d0=-0.5 * b.h_quadril)
            dz = self._queda["morto"] * e
        return construir(dz)


def acoes_comuns_de_chao(b, K, nomes_extras=None, cabeca_sentado=-15.0, dormir_cabeca=55.0, nadar=True):
    """Sentar, Sentado, Deitar, Deitado, Dormir, Levantar, Nadar, Morrer e Morto."""
    out = []
    if K.estilo != "cervideo":
        out.append(gravar_clipe(b, "Sentar", 20, lambda t: K.sentado(t, cabeca=cabeca_sentado)))

        def sentado(t):
            def mexer(p):
                respirar(b, p, t)
                b.olhar(p, virar=14 * math.sin(TAU * t), abaixar=2 * math.sin(TAU * t * 2))
                orelhas(b, p, tremor=8 * tremida(t, 0.4))
            return K.sentado(1.0, cabeca=cabeca_sentado, mexer=mexer)
        out.append(gravar_ciclo(b, "Sentado", 72, sentado))
        out.append(gravar_clipe(b, "Levantar_Sentado", 16, lambda t: K.sentado(1 - t, cabeca=cabeca_sentado)))
    out.append(gravar_clipe(b, "Deitar", 30, lambda t: K.deitado(t)))

    def deitado(t):
        def mexer(p):
            respirar(b, p, t)
            b.olhar(p, virar=12 * math.sin(TAU * t))
            orelhas(b, p, tremor=8 * tremida(t, 0.7))
        return K.deitado(1.0, mexer=mexer)
    out.append(gravar_ciclo(b, "Deitado", 72, deitado))

    def dormir(t):
        def mexer(p):
            respirar(b, p, t, 0.8, 1)
            orelhas(b, p, frente=-12)
        p = K.deitado(1.0, mexer=mexer)
        cabeca_ate_z(b, p, 0.10 * b.perna, vira=20, maximo=45, corpo=False)
        return p
    out.append(gravar_ciclo(b, "Dormir", 96, dormir))
    out.append(gravar_clipe(b, "Levantar", 26, lambda t: K.deitado(1 - t)))
    if nadar:
        out.append(gravar_ciclo(b, "Nadar", periodo_nado(b), lambda t: K.nadando(t)))

    def morrer(t):
        # os joelhos cedem (até 0,35), e ele tomba de lado
        if t < 0.4:
            u = t / 0.4
            p = K.deitado(0.55 * u)
            b.olhar(p, abaixar=20 * suave(u))
            return nao_entrar(b, p)
        u = (t - 0.4) / 0.6
        a = K.deitado(0.55)
        b.olhar(a, abaixar=20)
        bb = K.morto(1.0)
        p = misturar(a, bb, suave(u) ** 1.3)
        return nao_entrar(b, p)
    out.append(gravar_clipe(b, "Morrer", 34, morrer))
    out.append(gravar_clipe(b, "Morto", 2, lambda t: K.morto(1.0)))
    return out


def periodo_nado(b):
    return max(16, int(round(0.9 * FPS * (b.perna / 0.6) ** 0.4)))


# ---------------------------------------------------------------------------------------------------------------------
# Os canídeos (o lobo e o coiote)
# ---------------------------------------------------------------------------------------------------------------------

def acoes_canideo(b, nome):
    va, vt, vc = velocidades(nome)
    M = marchas(b, {"galope": dict(cauda_x=-12.0)})
    K = Construtores(b, "canideo")
    out = [acao_parado(b)]
    out.append(acao_marcha(b, "Andar", M["andar"], va))
    out.append(acao_marcha(b, "Trotar", M["trotar"], vt))
    out.append(acao_marcha(b, "Correr", M["galope"], vc))

    # a espreita: agachado, a cabeça baixa e para a frente, devagar
    esp = dict(M["andar"])
    esp.update(beta=0.74, altura=0.08, sobe=0.004, cabeca=1.0, cauda=3.0, cauda_x=18.0, rolar=1.0, guinar=1.5)

    def agachar(bb, p, t):
        p.mover("Quadril", (0, 0, -0.13 * b.perna))
        p.girar("Quadril", qx(2.0))
        b.olhar(p, abaixar=4, pesos=[0.7, 0.5, -0.2])
        orelhas(b, p, frente=15)
    out.append(acao_marcha(b, "Espreitar", esp, 0.45 * va, agachar))
    out.append(acao_alerta(b))

    def rosnar(p, t):
        p.mover("Quadril", (0, 0.03 * b.perna, -0.08 * b.perna))
        p.girar("Quadril", qx(4.0))
        b.olhar(p, abaixar=6 + 2 * math.sin(TAU * t * 6), pesos=[0.8, 0.5, -0.3])
        orelhas(b, p, frente=-28)
        boca(b, p, 10 + 4 * math.sin(TAU * t * 5))
        cauda(b, p, x=30)
    out.append(acao_alerta(b, 36, cabeca=0.0, orelha=0.0, cauda_x=0.0, nome="Rosnar", mexer_extra=rosnar))

    # o bote: agacha, salta curto para a frente com a boca aberta, fecha e volta
    def morder(t):
        cha = [(0.0, (0, 0, 0, 0, 0)), (0.28, (0.06, -0.09, 18, 0, 0.0)), (0.45, (-0.30, 0.03, -4, 28, 1.0)),
               (0.55, (-0.32, 0.0, 6, 2, 1.0)), (0.7, (-0.2, -0.02, 8, 0, 0.7)), (1.0, (0, 0, 0, 0, 0))]
        dy, dzc, cab, abre, pata = curva(t, cha)

        def mexer(p):
            p.mover("Quadril", (0, dy * b.perna, dzc * b.perna))
            p.girar("Quadril", qx(cab * 0.25))
            b.olhar(p, abaixar=cab)
            boca(b, p, abre)
            orelhas(b, p, frente=-15 * (abre > 5))
            cauda(b, p, x=-10)
        pes = {}
        for ch in ("D.L", "D.R"):
            pe = b.pe_repouso(ch)
            pe.y += dy * b.perna * pata
            pe.z += 0.12 * b.perna * math.sin(math.pi * min(1, max(0, (t - 0.3) / 0.3)))
            pes[ch] = pe
        return em_pe(b, mexer, pes=pes)
    out.append(gravar_clipe(b, "Morder", 22, morder))
    out.append(acao_comer(b, 48, "Comer", puxar=1.0, mastigar=14, abre_pernas=0.15))

    # o uivo (sentado): a cabeça sobe até o focinho apontar para o céu, a boca abre
    def uivar(t):
        sobe = curva(t, [(0, 0), (0.18, 1), (0.85, 1), (1, 0)])
        tre = 1.5 * math.sin(TAU * t * 9) * sobe

        def mexer(p):
            b.olhar(p, abaixar=-62 * sobe + tre)
            boca(b, p, 20 * curva(t, [(0, 0), (0.22, 0), (0.32, 1), (0.8, 0.9), (0.9, 0), (1, 0)]))
            orelhas(b, p, frente=-15 * sobe)
            respirar(b, p, t, 1.0, 1)
        return K.sentado(1.0, mexer=mexer)
    out.append(gravar_clipe(b, "Uivar", 84, uivar))
    out += acoes_comuns_de_chao(b, K)
    return out


# ---------------------------------------------------------------------------------------------------------------------
# Os cervídeos (o veado e o alce)
# ---------------------------------------------------------------------------------------------------------------------

def acoes_cervideo(b, nome):
    va, vt, vc = velocidades(nome)
    veado = especie(nome) == "Cervo"
    macho = nome.endswith("Macho")
    # o galope transverso (as patas de trás, depois as da frente na mesma ordem); o veado foge com a cauda levantada
    M = marchas(b, {"galope": dict(fases={"T.L": 0.0, "T.R": 0.10, "D.L": 0.45, "D.R": 0.56},
                                   cauda_x=-70.0 if veado else -10.0, coluna=6.0, dobra3=(110, 70), passo=1.05,
                                   desce_max=0.06),
                    "andar": dict(cabeca=3.0), "trotar": dict(dobra3=(85, 55))})
    K = Construtores(b, "cervideo")
    out = [acao_parado(b)]
    out.append(acao_marcha(b, "Andar", M["andar"], va))
    out.append(acao_marcha(b, "Trotar", M["trotar"], vt))
    out.append(acao_marcha(b, "Correr", M["galope"], vc))
    out.append(acao_comer(b, 72, "Comer", mastigar=6, abre_pernas=0.18, maximo=110, passos_corpo=3,
                          z_focinho=0.07 * b.perna))
    out.append(acao_alerta(b, 48, cabeca=-14.0, orelha=18.0, cauda_x=-25.0 if veado else -5.0))

    # bufar: a cabeça sobe e desce com o sopro e a pata da frente bate no chão duas vezes
    def bufar(t):
        bate = (max(0.0, math.sin(math.pi * min(1, max(0, (t - 0.35) / 0.18))))
                + max(0.0, math.sin(math.pi * min(1, max(0, (t - 0.6) / 0.18)))))

        def mexer(p):
            sopro = curva(t, [(0, 0), (0.12, 1), (0.25, 0), (1, 0)])
            b.olhar(p, abaixar=-14 - 10 * sopro)
            orelhas(b, p, frente=18)
            cauda(b, p, x=-30 if veado else -8)
            respirar(b, p, t, 2.0, 3)
        pes = {"D.L": b.pe_repouso("D.L") + Vector((0, -0.04 * b.perna * bate, 0.16 * b.perna * bate))}
        return em_pe(b, mexer, pes=pes, giros3={"D.L": rad(70) * bate}, giros4={"D.L": rad(40) * bate})
    out.append(gravar_clipe(b, "Bufar", 40, bufar))

    if macho:
        # a cabeçada com a galhada: abaixa a cabeça, avança e levanta
        def cabecada(t):
            dy, abaixa, sob = curva(t, [(0, (0, 0, 0)), (0.3, (0.05, 45, -0.05)), (0.5, (-0.28, 30, -0.02)),
                                        (0.58, (-0.30, -10, 0.03)), (0.75, (-0.15, 10, 0)), (1, (0, 0, 0))])

            def mexer(p):
                p.mover("Quadril", (0, dy * b.perna, sob * b.perna))
                p.girar("Quadril", qx(abaixa * 0.12))
                b.olhar(p, abaixar=abaixa)
                orelhas(b, p, frente=-20 * (abaixa > 20))
            pes = {}
            for ch in ("D.L", "D.R"):
                pe = b.pe_repouso(ch)
                pe.y += dy * b.perna * 0.9
                pe.z += 0.1 * b.perna * math.sin(math.pi * min(1, max(0, (t - 0.32) / 0.25)))
                pes[ch] = pe
            return em_pe(b, mexer, pes=pes)
        out.append(gravar_clipe(b, "Cabecada", 30, cabecada))

    if not veado:
        # o alce bravo: as orelhas para trás, a cabeça baixa e para a frente
        def bravo(p, t):
            p.mover("Quadril", (0, 0, -0.03 * b.perna))
            b.olhar(p, abaixar=12 + 3 * math.sin(TAU * t * 2))
            orelhas(b, p, frente=-35)
            cauda(b, p, x=-15, onda=4, t=t, vezes=3)
        out.append(acao_alerta(b, 48, cabeca=0.0, orelha=0.0, cauda_x=0.0, nome="Bravo", mexer_extra=bravo))

    # pisotear: empina e bate as patas da frente no chão (a mãe com o filhote; também o macho)
    def pisotear(t):
        sobe = curva(t, [(0, 0), (0.25, 1), (0.42, 0.05), (0.55, 0.8), (0.7, 0.0), (1, 0)])

        def mexer(p):
            p.girar("Quadril", qx(-22 * sobe))
            p.mover("Quadril", (0, 0.05 * b.perna * sobe, 0))
            b.olhar(p, abaixar=10 * sobe)
            orelhas(b, p, frente=-30 * min(1, sobe * 2))
        pes = {}
        for ch in ("D.L", "D.R"):
            pe = b.pe_repouso(ch)
            pe.y -= 0.12 * b.perna * sobe
            pe.z += 0.55 * b.perna * sobe
            pes[ch] = pe
        return em_pe(b, mexer, pes=pes, giros3={"D.L": rad(80) * sobe, "D.R": rad(80) * sobe},
                     giros4={"D.L": rad(50) * sobe, "D.R": rad(50) * sobe})
    out.append(gravar_clipe(b, "Pisotear", 36, pisotear))

    if not veado:
        # o mugido: a cabeça estica para a frente e para cima, a boca abre
        def mugir(t):
            m = curva(t, [(0, 0), (0.25, 1), (0.75, 1), (1, 0)])

            def mexer(p):
                b.olhar(p, abaixar=-25 * m, pesos=[0.2, 0.4, 0.4])
                boca(b, p, 16 * curva(t, [(0, 0), (0.3, 1), (0.7, 1), (0.85, 0), (1, 0)]))
                orelhas(b, p, frente=-10 * m)
                respirar(b, p, t, 2.0, 1)
            return em_pe(b, mexer)
        out.append(gravar_clipe(b, "Mugir", 48, mugir))

        # comer na água: a cabeça mergulha fundo (abaixo dos cascos: no raso, a água cobre), sobe mastigando e sacode
        def comer_agua(t):
            m = curva(t, [(0, 0.3), (0.15, 1), (0.45, 1), (0.6, 0.0), (0.9, 0.0), (1, 0.3)])

            def mexer(p):
                p.girar("Quadril", qx(4.0 * m))
                if m > 0.01:
                    cabeca_ate_z(b, p, mix(0.9 * b.perna, 0.04 * b.perna, m), maximo=110, passos_corpo=3)
                boca(b, p, 6 * (1 - m) * (0.5 + 0.5 * math.sin(TAU * t * 8)))
                b.olhar(p, inclinar=12 * tremida(t, 0.66, 0.06))
            return em_pe(b, mexer)
        out.append(gravar_ciclo(b, "Comer_Agua", 96, comer_agua))
    out += acoes_comuns_de_chao(b, K)
    return out


# ---------------------------------------------------------------------------------------------------------------------
# Os ursos
# ---------------------------------------------------------------------------------------------------------------------

def em_pe_nas_patas_de_tras(b, s, mexer=None, maos=None, giro=62.0):
    """De pé nas patas de trás (s de 0 a 1): o corpo gira para cima em volta do quadril, as patas de trás ficam no
    lugar e as da frente penduradas na frente do peito (ou nas mãos dadas: {ch: (ponto, giro3)})."""
    e = suave(s)
    pose = Pose()
    pose.girar("Quadril", qx(-giro * e))
    for o in b.coluna:
        pose.girar(o, qx(-4 * e))
    pose.mover("Quadril", (0, 0.06 * b.perna * e, 0))
    b.firmar_cabeca(pose, 0.85 * e)
    cauda(b, pose, x=20 * e)
    if mexer:
        mexer(pose)
    for _ in range(3):
        alvos = {ch: b.pe_repouso(ch) for ch in ("T.L", "T.R")}
        falta = b.estirado(pose, alvos, 0.96)
        if falta <= 0:
            break
        pose.mover("Quadril", (0, 0, -falta))
    Rw, Hw = b.fk(pose)
    alvos, g3, g4 = {}, {}, {}
    for ch, per in b.pernas.items():
        if ch[0] == "T":
            alvos[ch] = per.pe.copy()
            continue
        ombro = Hw[per.ossos[0]]
        Lp = per.L1 + per.L2 + per.L3
        solto = Vector((per.pe.x * 0.9, ombro.y - 0.22 * Lp, ombro.z - 0.62 * Lp))
        gg = rad(60)
        if maos and ch in maos:
            solto, gg = maos[ch]
        alvos[ch] = per.pe.lerp(solto, e)
        g3[ch] = gg * e
        g4[ch] = rad(50) * e
    b.pes_no_chao(pose, alvos, g3, g4)
    return pose


def acoes_urso(b, nome):
    va, vt, vc = velocidades(nome)
    filhote = nome.endswith("Filhote")
    M = marchas(b, {"andar": dict(cabeca=4.0, bate=18, altura=0.10, passo=0.85), "trotar": dict(bate=22, passo=0.85),
                    "galope": dict(fases={"T.L": 0.0, "T.R": 0.12, "D.R": 0.48, "D.L": 0.6}, coluna=10.0, arfar=5.0,
                                   bate=25, dobra3=(90, 70), passo=0.9)})
    K = Construtores(b, "urso")
    out = [acao_parado(b)]

    def fucando(bb, p, t):
        b.olhar(p, abaixar=14 + 4 * math.sin(TAU * t * 2), virar=10 * math.sin(TAU * t))
    out.append(acao_marcha(b, "Andar", M["andar"], va, fucando))
    out.append(acao_marcha(b, "Trotar", M["trotar"], vt))
    out.append(acao_marcha(b, "Correr", M["galope"], vc))
    out.append(acao_comer(b, 72, "Comer", mastigar=10))

    # fuçar o chão: a cabeça baixa de um lado para o outro, cheirando
    def fucar(t):
        def mexer(p):
            respirar(b, p, t, 1.0, 4)
            p.girar("Quadril", qx(3.0))
            cabeca_ate_z(b, p, 0.08 * b.perna + 0.03 * b.perna * math.sin(TAU * t * 3), vira=25 * math.sin(TAU * t))
            orelhas(b, p, tremor=6 * tremida(t, 0.5))
        return em_pe(b, mexer)
    out.append(gravar_ciclo(b, "Fucar", 72, fucar))
    out.append(acao_alerta(b, 48, cabeca=-12.0, orelha=10.0, cauda_x=0.0))

    # de pé: levanta, fica cheirando e batendo os dentes (o aviso), desce
    out.append(gravar_clipe(b, "Ficar_De_Pe", 26, lambda t: em_pe_nas_patas_de_tras(b, t)))

    def de_pe(t):
        def mexer(p):
            respirar(b, p, t, 2.0, 2)
            b.olhar(p, abaixar=-18 + 6 * math.sin(TAU * t * 2), virar=14 * math.sin(TAU * t))
            boca(b, p, 12 * max(0.0, math.sin(TAU * t * 4)) ** 4)
            orelhas(b, p, frente=-10)
        return em_pe_nas_patas_de_tras(b, 1.0, mexer)
    out.append(gravar_ciclo(b, "De_Pe", 72, de_pe))
    out.append(gravar_clipe(b, "Descer", 22, lambda t: em_pe_nas_patas_de_tras(b, 1 - t)))

    # a patada: a pata da frente direita sobe e desce batendo de cima para baixo e de lado, com o corpo junto
    def patada(t):
        sobe = curva(t, [(0, 0), (0.35, 1), (0.5, 0.6), (0.6, 0.0), (1, 0)])
        bate = curva(t, [(0, 0), (0.35, 0), (0.55, 1), (0.8, 1), (1, 0)])

        def mexer(p):
            p.girar("Quadril", qx(-8 * sobe) @ qz(-10 * sobe + 12 * bate))
            p.mover("Quadril", (0, 0.06 * b.perna * sobe - 0.1 * b.perna * bate, 0))
            b.olhar(p, abaixar=10 * bate - 10 * sobe)
            boca(b, p, 18 * max(sobe, bate * 0.6))
            orelhas(b, p, frente=-30)
        pe = b.pe_repouso("D.R")
        pe = pe + Vector((0.10 * b.perna * sobe - 0.06 * b.perna * bate, -0.25 * b.perna * (sobe + bate),
                          0.55 * b.perna * sobe))
        return em_pe(b, mexer, pes={"D.R": pe}, giros3={"D.R": rad(50) * sobe}, giros4={"D.R": rad(30) * sobe})
    out.append(gravar_clipe(b, "Patada", 26, patada))

    # pescar: de pé no raso, a cabeça baixa olhando a água; a patada na água
    def pescar(t):
        def mexer(p):
            respirar(b, p, t, 1.0, 2)
            p.girar("Quadril", qx(5.0))
            p.mover("Quadril", (0, 0, -0.04 * b.perna))
            cabeca_ate_z(b, p, 0.25 * b.perna + 0.03 * b.perna * math.sin(TAU * t), vira=18 * math.sin(TAU * t + 1))
            orelhas(b, p, frente=12)
        return em_pe(b, mexer)
    out.append(gravar_ciclo(b, "Pescar", 96, pescar))

    def patada_agua(t):
        sobe = curva(t, [(0, 0), (0.3, 1), (0.42, 0.0), (0.7, 0.0), (1, 0)])
        avanca = curva(t, [(0, 0), (0.3, 0.5), (0.42, 1), (0.7, 1), (1, 0)])

        def mexer(p):
            p.girar("Quadril", qx(5.0 - 6 * sobe))
            p.mover("Quadril", (0, -0.08 * b.perna * avanca, -0.04 * b.perna))
            cabeca_ate_z(b, p, 0.2 * b.perna - 0.1 * b.perna * avanca)
            boca(b, p, 10 * avanca * (t > 0.42))
        pe = b.pe_repouso("D.R") + Vector((0, -0.3 * b.perna * avanca, 0.5 * b.perna * sobe))
        return em_pe(b, mexer, pes={"D.R": pe}, giros3={"D.R": rad(60) * sobe})
    out.append(gravar_clipe(b, "Patada_Agua", 28, patada_agua))

    # sentado comendo: as patas da frente seguram a comida na boca
    def comer_sentado(t):
        def mexer(p):
            respirar(b, p, t, 1.0, 2)
            b.olhar(p, abaixar=4 + 5 * math.sin(TAU * t * 2))
            boca(b, p, 12 * (0.5 + 0.5 * math.sin(TAU * t * 6)))
        p = K.sentado(1.0, cabeca=0.0, mexer=mexer)
        Rw, Hw = b.fk(p)
        fo = b.c.ponta(Rw, Hw, "Cabeca")
        maos = {}
        for ch in ("D.L", "D.R"):
            s_ = 1 if ch[2] == "L" else -1
            maos[ch] = Vector((s_ * 0.05 * b.perna, fo.y + 0.12 * b.perna,
                               fo.z - 0.12 * b.perna + 0.02 * b.perna * math.sin(TAU * t * 2)))
        b.pes_no_chao(p, maos, {ch: rad(70) for ch in maos}, {ch: rad(80) for ch in maos})
        return p
    out.append(gravar_ciclo(b, "Comer_Sentado", 72, comer_sentado))
    if filhote:
        def chorar(t):
            def mexer(p):
                c = max(0.0, math.sin(TAU * t * 3))
                b.olhar(p, abaixar=-30 - 8 * c)
                boca(b, p, 14 * c)
                respirar(b, p, t, 2.0, 3)
            return K.sentado(1.0, mexer=mexer)
        out.append(gravar_ciclo(b, "Chorar", 48, chorar))
    out += acoes_comuns_de_chao(b, K)
    return out


# ---------------------------------------------------------------------------------------------------------------------
# Os pequenos: o guaxinim, o gambá, o castor e o coelho
# ---------------------------------------------------------------------------------------------------------------------

def pes_sob_o_corpo(b, pose, quais=None, abre=0.0):
    """Os alvos dos pés debaixo de onde o corpo levou a raiz de cada pata (com o giro do pai no plano do chão)."""
    Rw, Hw = b.fk(pose)
    out = {}
    for ch, per in b.pernas.items():
        if quais and ch not in quais:
            continue
        b1 = per.ossos[0]
        pai = b.c.pai[b1]
        R = Rw[pai]
        fwd = R @ Vector((0, -1, 0))
        yaw = math.atan2(fwd.x, -fwd.y)
        Rz = Quaternion(Z, -yaw)
        alvo = Hw[b1] + Rz @ (per.pe - b.c.cab[b1])
        alvo.z = per.pe.z
        if abre:
            alvo.x *= 1 + abre
        out[ch] = alvo
    return out


def acao_comer_sentado(b, K, nome="Comer_Sentado", n=60, cabeca=6.0, perto=0.10):
    """Sentado, as patas da frente seguram a comida na boca, mastigando."""
    def f(t):
        def mexer(p):
            respirar(b, p, t, 1.0, 2)
            b.olhar(p, abaixar=cabeca + 5 * math.sin(TAU * t * 2))
            boca(b, p, 10 * (0.5 + 0.5 * math.sin(TAU * t * 6)))
        p = K.sentado(1.0, cabeca=0.0, mexer=mexer)
        Rw, Hw = b.fk(p)
        fo = b.c.ponta(Rw, Hw, "Cabeca")
        maos = {}
        for ch in ("D.L", "D.R"):
            s_ = 1 if ch[2] == "L" else -1
            maos[ch] = Vector((s_ * 0.04 * b.perna, fo.y + perto * b.perna,
                               fo.z - 0.10 * b.perna + 0.02 * b.perna * math.sin(TAU * t * 2)))
        b.pes_no_chao(p, maos, {ch: rad(70) for ch in maos}, {ch: rad(80) for ch in maos})
        return p
    return gravar_ciclo(b, nome, n, f)


def acoes_guaxinim(b, nome):
    va, vt, vc = velocidades(nome)
    M = marchas(b, {"andar": dict(bate=15, cabeca=3.0), "trotar": dict(bate=18),
                    "galope": dict(fases={"T.L": 0.0, "T.R": 0.08, "D.L": 0.5, "D.R": 0.58}, coluna=14.0, arfar=6.0,
                                   bate=20)})
    K = Construtores(b, "pequeno")
    out = [acao_parado(b)]
    out.append(acao_marcha(b, "Andar", M["andar"], va))
    out.append(acao_marcha(b, "Trotar", M["trotar"], vt))
    out.append(acao_marcha(b, "Correr", M["galope"], vc))

    # alerta: fica de pé nas patas de trás, cheirando
    def alerta(t):
        def mexer(p):
            respirar(b, p, t, 1.5, 3)
            b.olhar(p, abaixar=-10 + 4 * math.sin(TAU * t * 3), virar=18 * math.sin(TAU * t))
            orelhas(b, p, frente=12)
        return em_pe_nas_patas_de_tras(b, 1.0, mexer, giro=68)
    out.append(gravar_clipe(b, "Ficar_De_Pe", 18, lambda t: em_pe_nas_patas_de_tras(b, t, giro=68)))
    out.append(gravar_ciclo(b, "Alerta", 48, alerta))

    # revirar o lixo: de pé, inclinado para a frente, as mãos mexendo na lata (em roda, alternadas)
    def revirar(t):
        def mexer(p):
            b.olhar(p, abaixar=30 + 6 * math.sin(TAU * t * 2), virar=10 * math.sin(TAU * t))
            orelhas(b, p, frente=8)
        p = em_pe_nas_patas_de_tras(b, 1.0, mexer, giro=42)
        Rw, Hw = b.fk(p)
        maos = {}
        for ch, fase in (("D.L", 0.0), ("D.R", 0.5)):
            per = b.pernas[ch]
            ombro = Hw[per.ossos[0]]
            Lp = per.L1 + per.L2 + per.L3
            a = TAU * (t * 2 + fase)
            maos[ch] = Vector((per.pe.x * 0.8, ombro.y - 0.55 * Lp + 0.12 * Lp * math.cos(a),
                               ombro.z - 0.55 * Lp + 0.12 * Lp * math.sin(a)))
        b.pes_no_chao(p, maos, {ch: rad(40) for ch in maos}, {ch: rad(70) for ch in maos})
        return p
    out.append(gravar_ciclo(b, "Revirar", 48, revirar))

    # subir na árvore: o corpo em pé, de frente para o tronco (em -Y), as patas subindo em diagonal
    def no_tronco(t, ritmo):
        pose = Pose()
        pose.girar("Quadril", qx(-82))
        pose.mover("Quadril", (0, 0.10 * b.perna, 0.25 * b.perna))
        for o in b.coluna:
            pose.girar(o, qx(-2))
        b.firmar_cabeca(pose, 0.3)
        b.olhar(pose, abaixar=-20 + 5 * math.sin(TAU * t), virar=10 * math.sin(TAU * t) * (ritmo == 0))
        cauda(b, pose, x=50, onda=5, t=t)
        Rw, Hw = b.fk(pose)
        plano = min(Hw[per.ossos[0]].y for per in b.pernas.values()) - 0.42 * b.perna
        alvos, g3, g4 = {}, {}, {}
        fases = {"D.L": 0.0, "T.R": 0.0, "D.R": 0.5, "T.L": 0.5}
        for ch, per in b.pernas.items():
            Lp = per.L1 + per.L2 + per.L3
            raiz = Hw[per.ossos[0]]
            f = (t + fases[ch]) % 1.0
            sobe = (0.18 * Lp * math.cos(TAU * f)) if ritmo else 0.0
            dz = (-0.2 if ch[0] == "D" else 0.05) * Lp + sobe
            alvos[ch] = Vector((per.pe.x * 1.4, plano + (0.04 * Lp * max(0, math.sin(TAU * f)) if ritmo else 0), raiz.z + dz))
            g3[ch] = rad(-60)
            g4[ch] = rad(-80)
        b.pes_no_chao(pose, alvos, g3, g4)
        return pose
    out.append(gravar_ciclo(b, "Subir", 20, lambda t: no_tronco(t, 1)))
    out.append(gravar_ciclo(b, "Agarrado", 72, lambda t: no_tronco(t, 0)))
    out.append(acao_comer_sentado(b, K))
    out += acoes_comuns_de_chao(b, K)
    return out


def acoes_gamba(b, nome):
    va, vt, vc = velocidades(nome)
    M = marchas(b, {"andar": dict(bate=15, cabeca=2.0, cauda=4.0), "trotar": dict(bate=18),
                    "galope": dict(fases={"T.L": 0.0, "T.R": 0.1, "D.L": 0.5, "D.R": 0.6}, coluna=10.0, bate=20)})
    K = Construtores(b, "pequeno")
    out = [acao_parado(b)]
    out.append(acao_marcha(b, "Andar", M["andar"], va))
    out.append(acao_marcha(b, "Trotar", M["trotar"], vt))
    out.append(acao_marcha(b, "Correr", M["galope"], vc))
    out.append(acao_comer(b, 60, "Comer", mastigar=10))

    # o aviso: a cauda levanta e as patas da frente batem no chão, alternadas
    def aviso(t):
        bate = {"D.L": max(0.0, math.sin(TAU * t * 2)), "D.R": max(0.0, math.sin(TAU * t * 2 + math.pi))}

        def mexer(p):
            p.girar("Quadril", qx(-4))
            cauda(b, p, x=-120 * curva(t, [(0, 0.6), (0.1, 1), (0.9, 1), (1, 0.6)]))
            b.olhar(p, abaixar=-6)
            boca(b, p, 8)
        pes = {ch: b.pe_repouso(ch) + Vector((0, -0.03 * b.perna * v, 0.18 * b.perna * v)) for ch, v in bate.items()}
        return em_pe(b, mexer, pes=pes, giros3={ch: rad(50) * v for ch, v in bate.items()})
    out.append(gravar_ciclo(b, "Aviso", 24, aviso))

    # o espirro: o corpo dobra em U (a cabeça e o traseiro para o mesmo lado), a cauda para cima, e espirra
    def espirrar(t):
        u = curva(t, [(0, 0), (0.3, 1), (0.8, 1), (1, 0)])
        jato = tremida(t, 0.55, 0.12)

        def mexer(p):
            p.girar("Quadril", qz(35 * u))
            for o in b.coluna:
                p.girar(o, qz(-28 * u))
            b.olhar(p, virar=-30 * u)
            cauda(b, p, x=-110 * u + 15 * jato, z=-20 * u)
            boca(b, p, 10 * u)
        pose = Pose()
        mexer(pose)
        return em_pe(b, mexer, pes=pes_sob_o_corpo(b, pose))
    out.append(gravar_clipe(b, "Espirrar", 40, espirrar))

    # chiar (o gambá de verdade): a boca bem aberta, a cabeça baixa e o corpo arqueado
    def chiar(t):
        def mexer(p):
            p.girar("Quadril", qx(5))
            for o in b.coluna[:-1]:
                p.girar(o, qx(-5))
            b.olhar(p, abaixar=4 + 3 * math.sin(TAU * t * 4))
            boca(b, p, 28 + 4 * math.sin(TAU * t * 8))
            orelhas(b, p, frente=-20)
            cauda(b, p, x=15, onda=6, t=t, vezes=3)
        return em_pe(b, mexer)
    out.append(gravar_ciclo(b, "Chiar", 36, chiar))

    # fingir de morto (o gambá-de-orelha-branca): cai de lado, a boca aberta e a língua... e fica
    def fingir(t):
        p = K.morto(min(1.0, t / 0.4))
        boca(b, p, 25 * suave(min(1.0, t / 0.4)))
        return nao_entrar(b, p)
    out.append(gravar_clipe(b, "Fingir_Morto", 30, fingir))
    out += acoes_comuns_de_chao(b, K)
    return out


def acoes_castor(b, nome):
    va, vt, vc = velocidades(nome)
    M = marchas(b, {"andar": dict(bate=15, cabeca=2.0, rolar=5.0, guinar=4.0, cauda=4.0, altura=0.14),
                    "trotar": dict(bate=18, rolar=3.0),
                    "galope": dict(fases={"T.L": 0.0, "T.R": 0.05, "D.L": 0.5, "D.R": 0.55}, coluna=12.0, bate=20)})
    K = Construtores(b, "pequeno")
    out = [acao_parado(b)]
    out.append(acao_marcha(b, "Andar", M["andar"], va))
    out.append(acao_marcha(b, "Correr", M["galope"], vc))

    # carregando o galho: andando com a cabeça mais alta (o galho na boca)
    def com_galho(bb, p, t):
        b.olhar(p, abaixar=-14)
        boca(b, p, 6)
    out.append(acao_marcha(b, "Andar_Com_Galho", M["andar"], va * 0.8, com_galho))

    # nadar: as patas da frente encolhidas, as de trás remando juntas e a cauda ondulando
    def nadar(t):
        pose = Pose()
        pose.girar("Quadril", qx(-6))
        b.firmar_cabeca(pose, 0.9)
        b.olhar(pose, abaixar=-15)
        cauda(b, pose, x=-10, onda=10, t=t, fase=0.9)
        Rw, Hw = b.fk(pose)
        alvos, g3 = {}, {}
        for ch, per in b.pernas.items():
            Lp = per.L1 + per.L2 + per.L3
            topo = Hw[per.ossos[0]]
            if ch[0] == "D":
                alvos[ch] = Vector((per.pe.x * 0.7, topo.y - 0.1 * Lp, topo.z - 0.45 * Lp))
                g3[ch] = rad(70)
            else:
                f = (t + (0.0 if ch[2] == "L" else 0.5)) % 1.0
                alvos[ch] = Vector((per.pe.x * 1.2, topo.y + 0.25 * Lp - 0.4 * Lp * math.cos(TAU * f),
                                    topo.z - 0.5 * Lp - 0.12 * Lp * math.sin(TAU * f)))
                g3[ch] = rad(40) * max(0.0, math.sin(TAU * f))
        b.pes_no_chao(pose, alvos, g3, {ch: rad(60) for ch in alvos})
        return pose
    out.append(gravar_ciclo(b, "Nadar", 24, nadar))

    # mergulhar: o focinho desce, a cauda sobe e some (o corpo desce)
    def mergulhar(t):
        p = nadar(t * 2 % 1.0)
        u = curva(t, [(0, 0), (0.5, 1), (1, 1)])
        p.girar("Quadril", qx(55 * u))
        p.mover("Quadril", (0, -0.2 * b.perna * u, -0.5 * b.perna * curva(t, [(0, 0), (0.3, 0), (1, 1)])))
        cauda(b, p, x=-30 * u)
        return p
    out.append(gravar_clipe(b, "Mergulhar", 30, mergulhar))

    # bater a cauda na água (o estalo)
    def bater(t):
        p = nadar(t)
        sobe = curva(t, [(0, 0), (0.35, 1), (0.48, -0.4), (0.7, 0), (1, 0)])
        cauda(b, p, x=-80 * sobe)
        p.girar("Quadril", qx(10 * max(0, -sobe) - 6 * max(0, sobe)))
        return p
    out.append(gravar_clipe(b, "Bater_Cauda", 24, bater))

    # roer o tronco: de pé nas patas de trás e na cauda, as mãos no tronco, a cabeça de lado roendo
    def roer(t):
        def mexer(p):
            b.olhar(p, abaixar=10, virar=28 + 4 * math.sin(TAU * t * 3), inclinar=-18)
            boca(b, p, 14 * (0.5 + 0.5 * math.sin(TAU * t * 8)))
            cauda(b, p, x=60)
        p = em_pe_nas_patas_de_tras(b, 1.0, mexer, giro=58)
        Rw, Hw = b.fk(p)
        maos = {}
        for ch in ("D.L", "D.R"):
            per = b.pernas[ch]
            ombro = Hw[per.ossos[0]]
            Lp = per.L1 + per.L2 + per.L3
            maos[ch] = Vector((per.pe.x * 0.9, ombro.y - 0.7 * Lp, ombro.z + 0.1 * Lp * (1 if ch[2] == "L" else 0.6)))
        b.pes_no_chao(p, maos, {ch: rad(-30) for ch in maos}, {ch: rad(-70) for ch in maos})
        return p
    out.append(gravar_ciclo(b, "Roer", 48, roer))
    out.append(acao_comer_sentado(b, K))
    out += acoes_comuns_de_chao(b, K, nadar=False)
    return out


def acoes_coelho(b, nome):
    va, vt, vc = velocidades(nome)
    K = Construtores(b, "pequeno")

    def parado(t):
        def mexer(p):
            respirar(b, p, t, 1.5, 4)
            fu = 3 * max(0.0, math.sin(TAU * t * 12)) * (t % 0.5 < 0.25)
            p.girar("Cabeca", qx(-fu))
            b.olhar(p, virar=10 * math.sin(TAU * t))
            orelhas(b, p, tremor=10 * tremida(t, 0.4) - 10 * tremida(t, 0.75), lado=4 * math.sin(TAU * t))
        return em_pe(b, mexer)
    out = [gravar_ciclo(b, "Parado", 72, parado)]

    # o pulo: as de trás empurram, o corpo sobe e avança, as da frente pousam na frente e as de trás vêm atrás
    def pulo(t, forca):
        L = b.perna
        cha_corpo = [(0, (0, 0, 0)), (0.12, (-0.04, 6, 0.0)), (0.32, (0.10, 0, 0.55)), (0.5, (0.04, 10, 0.15)),
                     (0.68, (-0.06, -4, 0.0)), (1, (0, 0, 0))]
        dz, pit, voo = curva(t, cha_corpo)
        fr = curva(t, [(0, (0, 0)), (0.15, (0, 0)), (0.3, (-0.5, 0.35)), (0.48, (-0.75, 0.0)), (0.75, (-0.2, 0.0)),
                       (1, (0, 0))])
        tr = curva(t, [(0, (0, 0)), (0.12, (0.0, 0.0)), (0.3, (0.45, 0.25)), (0.55, (0.25, 0.35)), (0.7, (-0.35, 0.05)),
                       (0.78, (-0.35, 0.0)), (1, (0, 0))])

        def mexer(p):
            p.mover("Quadril", (0, 0, (dz + voo * 0.5) * L * forca))
            p.girar("Quadril", qx(pit * forca))
            b.firmar_cabeca(p, 0.6)
            orelhas(b, p, frente=-18 * voo * forca)
            cauda(b, p, x=-20 * voo)
        pes, g3, g4 = {}, {}, {}
        for ch in b.pernas:
            dy, sobe = fr if ch[0] == "D" else tr
            pes[ch] = b.pe_repouso(ch) + Vector((0, dy * L * forca, sobe * L * forca))
            g3[ch] = rad(40) * sobe / 0.35 if ch[0] == "D" else rad(-30) * sobe / 0.35
            g4[ch] = rad(30) * sobe / 0.35
        return em_pe(b, mexer, pes=pes, giros3=g3, giros4=g4)
    out.append(gravar_ciclo(b, "Pular", 16, lambda t: pulo(t, 0.7)))
    out.append(gravar_ciclo(b, "Correr", 11, lambda t: pulo(t, 1.0)))
    out.append(acao_comer(b, 48, "Comer", mastigar=10))

    # alerta: sobe nas patas de trás, as orelhas em pé
    def alerta(t):
        def mexer(p):
            respirar(b, p, t, 2.0, 4)
            b.olhar(p, abaixar=-5, virar=15 * math.sin(TAU * t))
            orelhas(b, p, frente=10, tremor=8 * tremida(t, 0.5))
        return em_pe_nas_patas_de_tras(b, 1.0, mexer, giro=40)
    out.append(gravar_ciclo(b, "Alerta", 48, alerta))

    # o guincho (pego): se debate, chutando
    def guincho(t):
        def mexer(p):
            p.girar("Quadril", qy(18 * math.sin(TAU * t * 3)) @ qz(12 * math.sin(TAU * t * 2)))
            b.olhar(p, abaixar=-25 + 10 * math.sin(TAU * t * 4), virar=20 * math.sin(TAU * t * 3))
            boca(b, p, 18)
            orelhas(b, p, frente=-30)
        pes = {}
        for ch in b.pernas:
            f = TAU * t * 4 + (0 if ch[2] == "L" else math.pi)
            pes[ch] = b.pe_repouso(ch) + Vector((0, 0.15 * b.perna * math.sin(f), 0.2 * b.perna * (1 + math.cos(f)) * 0.5))
        return em_pe(b, mexer, pes=pes)
    out.append(gravar_ciclo(b, "Guincho", 24, guincho))
    out.append(gravar_clipe(b, "Deitar", 20, lambda t: K.deitado(t)))
    out.append(gravar_ciclo(b, "Deitado", 72, lambda t: K.deitado(1.0, mexer=lambda p: respirar(b, p, t))))
    out.append(gravar_clipe(b, "Levantar", 16, lambda t: K.deitado(1 - t)))

    def morrer(t):
        p = misturar(Pose(), K.morto(1.0), suave(t) ** 1.2)
        return nao_entrar(b, p)
    out.append(gravar_clipe(b, "Morrer", 20, morrer))
    out.append(gravar_clipe(b, "Morto", 2, lambda t: K.morto(1.0)))
    return out


# ---------------------------------------------------------------------------------------------------------------------
# As aves (o pato, o peru, a águia e o urubu)
# ---------------------------------------------------------------------------------------------------------------------

ASAS = ("Asa", "Asa_2", "Asa_3")


class Asas:
    """As asas pelas direções de cada osso (no espaço do bicho): a aberta e a fechada, misturadas pela abertura, mais a
    batida (o giro em volta do eixo do corpo, + = para cima) e a torção (a corda da asa virando para baixo quando
    fecha). Serve para as do modelo abertas (a águia, o urubu) e fechadas (o pato, o peru)."""

    def __init__(self, b, abertas_no_modelo):
        self.b = b
        c = b.c
        self.rest = {o: c.dir(o + ".L") for o in ASAS}
        if abertas_no_modelo:
            self.aberta = dict(self.rest)
            self.fechada = {"Asa": Vector((0.30, 0.45, -0.84)).normalized(),
                            "Asa_2": Vector((0.12, -0.55, -0.83)).normalized(),
                            "Asa_3": Vector((0.05, 0.80, -0.60)).normalized()}
            self.torce = 1.0
        else:
            self.fechada = dict(self.rest)
            self.aberta = {"Asa": Vector((1, 0.05, 0.10)).normalized(), "Asa_2": Vector((1, 0.12, 0.0)).normalized(),
                           "Asa_3": Vector((1, 0.30, -0.06)).normalized()}
            self.torce = -1.0

    def por(self, pose, abertura=1.0, batida=(0.0, 0.0, 0.0), varrer=0.0, torcer=None, so=None):
        b = self.b
        c = b.c
        Rw, Hw = b.fk(pose)
        for lado, s in (("L", 1.0), ("R", -1.0)):
            if so and lado != so:
                continue
            for i, o in enumerate(ASAS):
                n = o + "." + lado
                da, df = self.aberta[o], self.fechada[o]
                d = df.lerp(da, abertura)
                d = Vector((d.x * s, d.y, d.z))
                if d.length < 1e-6:
                    d = Vector((s, 0, 0))
                d.normalize()
                # a batida: em volta do eixo do corpo (Y), + para cima; e o varrer para trás (em volta do Z)
                d = Quaternion(Y, -s * rad(batida[i])) @ Quaternion(Z, s * rad(varrer * (1 + 0.3 * i))) @ d
                # o peito pode ter girado: a direção é no espaço do peito
                d = Rw["Peito"] @ d
                Rwn = c.apontar(pose, Rw, Hw, n, d)
                t = self.torce * (1 - abertura) * 70.0 if torcer is None else torcer
                if t:
                    Rwn = Quaternion(d, -s * rad(t)) @ Rwn
                    pai = c.pai[n]
                    pose.R[n] = Rw[pai].inverted() @ Rwn
                    Rw[n] = Rwn
        return pose


def ave_corpo_nivelado(b, frac=1.0):
    """O giro (em volta do X) que deixa o corpo da ave deitado (do quadril ao peito na horizontal)."""
    a = b.c.cab["Quadril"]
    p = b.c.pon["Peito"]
    d = p - a
    ang = math.degrees(math.atan2(d.z, -d.y))     # + = o peito acima do quadril
    return ang * frac


def ave_pernas(b, pose, modo="chao", t=0.0, pes=None, g3=None, g4=None):
    """As pernas da ave: "chao" (os pés no lugar), "voo" (encolhidas para trás), "garras" (para a frente e para baixo),
    "penduradas" (soltas embaixo)."""
    if modo == "chao":
        alvos = {ch: (pes or {}).get(ch, b.pe_repouso(ch)) for ch in b.pernas}
        b.pes_no_chao(pose, alvos, g3, g4)
        return pose
    Rw, Hw = b.fk(pose)
    alvos, gg3, gg4 = {}, {}, {}
    for ch, per in b.pernas.items():
        Lp = per.L1 + per.L2 + per.L3
        raiz = Hw[per.ossos[0]]
        eixo = (b.c.pon["Peito"] - b.c.cab["Quadril"])
        eixo = Vector((0, eixo.y, eixo.z)).normalized()
        fwd = Rw["Quadril"] @ eixo
        up = fwd.cross(X)
        if modo == "voo":
            alvo = raiz - fwd * 0.55 * Lp - up * 0.25 * Lp
            gg3[ch], gg4[ch] = rad(-40), rad(-80)
        elif modo == "garras":
            alvo = raiz + fwd * 0.45 * Lp - up * 0.6 * Lp
            gg3[ch], gg4[ch] = rad(-20), rad(20)
        else:
            alvo = raiz - up * 0.75 * Lp + fwd * 0.05 * Lp * math.sin(TAU * t)
            gg3[ch], gg4[ch] = rad(10), rad(50)
        alvo.x = per.pe.x
        alvos[ch] = alvo
    b.pes_no_chao(pose, alvos, gg3, gg4)
    return pose


def ave_cauda(b, pose, x=0.0, z=0.0, abre=0.0):
    for i, o in enumerate(b.cauda):
        pose.girar(o, qz(z / max(len(b.cauda), 1)) @ qx(x / max(len(b.cauda), 1)))


def ave_batida(t, amp=55.0, centro=10.0, atraso=0.06):
    """As três batidas (a asa, o antebraço e a mão), com a mão atrasada e mais larga; desce rápido (45% do ciclo) e
    sobe devagar. + = para cima."""
    out = []
    for i, k in enumerate((1.0, 1.1, 1.3)):
        f = (t - atraso * i) % 1.0
        u = math.cos(math.pi * f / 0.45) if f < 0.45 else -math.cos(math.pi * (f - 0.45) / 0.55)
        out.append(centro + amp * k * u)
    return tuple(out)


def acoes_ave(b, nome):
    abertas = nome in ("Aguia", "Urubu")
    A = Asas(b, abertas)
    K = Construtores(b, "ave")
    niv = ave_corpo_nivelado(b)
    out = []

    def de_pe(mexer=None, abertura=0.0, batida=(0, 0, 0), pes=None, g3=None):
        p = Pose()
        if mexer:
            mexer(p)
        A.por(p, abertura, batida)
        return ave_pernas(b, p, "chao", pes=pes, g3=g3)

    def parado(t):
        def mexer(p):
            respirar(b, p, t, 1.2, 2)
            b.olhar(p, virar=25 * math.sin(TAU * t) * suave(abs(math.sin(TAU * t))), abaixar=3 * math.sin(TAU * t * 3))
            ave_cauda(b, p, z=6 * tremida(t, 0.7, 0.05))
        return de_pe(mexer, 0.0, (0, 0, 0))
    nome_parado = "Pousado" if abertas else "Parado"
    out.append(gravar_ciclo(b, nome_parado, 72, parado))

    # andar (o pato bamboleia, o peru balança a cabeça para a frente e para trás)
    pato = nome == "Pato"
    m_andar = dict(beta=0.6, fases={"T.L": 0.0, "T.R": 0.5}, passo=0.9 if not pato else 0.7, altura=0.18,
                   sobe=0.015, dobra3=(0, 40), dobra4=(0, 50), bate=20, rolar=8.0 if pato else 3.0,
                   guinar=6.0 if pato else 3.0, cabeca=0.0, cauda=0.0, coluna=0.0, arfar=0.0)
    v_andar = {"Pato": 0.6, "Peru": 1.0, "Urubu": 0.5, "Aguia": 0.5}[nome]

    def andar_extra(bb, p, t):
        A.por(p, 0.0)
        if not pato:
            b.olhar(p, abaixar=4 * math.sin(TAU * t * 2))
            p.girar("Pescoco_1", qx(-6 * math.sin(TAU * t * 2)))
        ave_cauda(b, p, z=(8 if pato else 3) * math.sin(TAU * t))
    out.append(acao_marcha(b, "Andar", m_andar, v_andar, andar_extra))
    if nome == "Peru":
        m_correr = dict(m_andar, beta=0.4, passo=1.1, altura=0.25, sobe=0.03, rolar=2.0, guinar=2.0)

        def correr_extra(bb, p, t):
            p.girar("Quadril", qx(15))
            A.por(p, 0.25, (15, 15, 15))
            b.firmar_cabeca(p, 0.8)
        out.append(acao_marcha(b, "Correr", m_correr, 3.0, correr_extra))

    # comer: bicando no chão (o urubu e a águia rasgam: a cabeça puxa para cima)
    def comer(t):
        rasga = nome in ("Urubu", "Aguia")

        def mexer(p):
            p.girar("Quadril", qx(10))
            bica = max(0.0, math.sin(TAU * t * (2 if rasga else 3)))
            alvo = (0.03 + (0.12 if rasga else 0.05) * (1 - bica)) * b.perna
            cabeca_ate_z(b, p, alvo, maximo=60, corpo=True)
            if rasga:
                b.olhar(p, inclinar=10 * math.sin(TAU * t * 2), virar=8 * math.sin(TAU * t * 2))
        return de_pe(mexer, 0.1 if rasga else 0.0, (10, 10, 10) if rasga else (0, 0, 0))
    out.append(gravar_ciclo(b, "Comer", 48, comer))

    # o chamado (o grasnado do pato, o glu-glu do peru, o grito da águia)
    def chamado(t):
        def mexer(p):
            if nome == "Peru":
                u = curva(t, [(0, 0), (0.2, 1), (0.8, 1), (1, 0)])
                b.olhar(p, abaixar=-30 * u + 10 * math.sin(TAU * t * 10) * u)
                if b.tem("Papo"):
                    p.girar("Papo", qx(25 * math.sin(TAU * t * 10) * u) @ qy(15 * math.sin(TAU * t * 7) * u))
                A.por(p, 0.0)
            elif nome == "Pato":
                for k in range(3):
                    s = tremida(t, 0.2 + 0.25 * k, 0.1)
                    b.olhar(p, abaixar=-15 * abs(s), pesos=[0.2, 0.4, 0.4])
                ave_cauda(b, p, z=10 * math.sin(TAU * t * 3))
            else:
                u = curva(t, [(0, 0), (0.15, 1), (0.85, 1), (1, 0)])
                b.olhar(p, abaixar=-35 * u + 4 * math.sin(TAU * t * 12) * u)
                respirar(b, p, t, 3.0, 4)
        return de_pe(mexer, 0.0)
    out.append(gravar_clipe(b, {"Peru": "Glu_Glu", "Pato": "Grasnar"}.get(nome, "Grito"), 40, chamado))

    # o voo
    def voando(t, amp=55.0, abertura=1.0, pernas="voo", centro=8.0, pitch=None, mexer=None):
        p = Pose()
        p.girar("Quadril", qx(niv if pitch is None else pitch))
        p.mover("Quadril", (0, 0, 0.03 * b.perna * math.sin(TAU * t + 1.0)))
        b.firmar_cabeca(p, 1.0)
        ave_cauda(b, p, x=-niv * 0.6 if abertas else -10)
        if mexer:
            mexer(p)
        A.por(p, abertura, ave_batida(t, amp, centro))
        return ave_pernas(b, p, pernas, t)
    out.append(gravar_ciclo(b, "Voar", 16 if not abertas else 22, lambda t: voando(t)))
    planar_amp = {"Urubu": 0.0, "Aguia": 0.0}.get(nome, 0.0)

    def planar(t):
        def mexer(p):
            p.girar("Quadril", qy((12 if nome == "Urubu" else 6) * math.sin(TAU * t)))
            ave_cauda(b, p, z=6 * math.sin(TAU * t + 1))
            b.olhar(p, virar=10 * math.sin(TAU * t))
        p = voando(0.0, planar_amp, 1.0, "voo", centro=(14 if nome == "Urubu" else 4) + 3 * math.sin(TAU * t * 2),
                   mexer=mexer)
        return p
    out.append(gravar_ciclo(b, "Planar", 72, planar))

    # decolar: agacha, abre as asas e bate forte, o corpo deita e as pernas encolhem
    def decolar(t):
        if t < 0.25:
            u = suave(t / 0.25)

            def mexer(p):
                p.mover("Quadril", (0, 0, -0.18 * b.perna * u))
                p.girar("Quadril", qx(niv * 0.3 * u))
                b.firmar_cabeca(p, 0.7)
            return de_pe(mexer, u, (40 * u, 50 * u, 60 * u))
        u = (t - 0.25) / 0.75
        p = voando(u * 3 % 1.0, 70, 1.0, "voo", pitch=niv * suave(min(1, u * 1.5)))
        p.mover("Quadril", (0, 0, 0.6 * b.perna * suave(u)))
        return p
    out.append(gravar_clipe(b, "Decolar", 36, decolar))

    # pousar: o corpo levanta, as asas freiam, as pernas vão para a frente e ele assenta
    def pousar(t):
        if t < 0.7:
            u = t / 0.7

            def mexer(p):
                p.mover("Quadril", (0, 0, 0.5 * b.perna * (1 - suave(u))))
            p = voando(u * 2 % 1.0, 45 * (1 - u * 0.5), 1.0, "garras", pitch=niv * (1 - suave(u)) - 20 * math.sin(math.pi * u),
                       mexer=mexer)
            return p
        u = suave((t - 0.7) / 0.3)

        def mexer(p):
            p.mover("Quadril", (0, 0, -0.12 * b.perna * (1 - u)))
        return de_pe(mexer, 1 - u, (30 * (1 - u), 30 * (1 - u), 30 * (1 - u)))
    out.append(gravar_clipe(b, "Pousar", 30, pousar))

    if nome == "Pato":
        # boiar e nadar: o corpo parado, os pés remando embaixo
        def nadando(t, forca):
            p = Pose()
            b.olhar(p, virar=12 * math.sin(TAU * t) * (forca < 0.5), abaixar=-4 * forca)
            A.por(p, 0.0)
            ave_cauda(b, p, z=6 * math.sin(TAU * t))
            Rw, Hw = b.fk(p)
            alvos, g3 = {}, {}
            for ch, per in b.pernas.items():
                Lp = per.L1 + per.L2 + per.L3
                f = (t + (0 if ch[2] == "L" else 0.5)) % 1.0
                c0 = per.pe + Vector((0, 0.15 * Lp, 0.15 * Lp))
                alvos[ch] = c0 + Vector((0, -0.25 * Lp * forca * math.cos(TAU * f), 0.08 * Lp * forca * math.sin(TAU * f)))
                g3[ch] = rad(-20)
            b.pes_no_chao(p, alvos, g3, {ch: rad(-30) for ch in alvos})
            return p
        out.append(gravar_ciclo(b, "Boiar", 48, lambda t: nadando(t, 0.4)))
        out.append(gravar_ciclo(b, "Nadar", 20, lambda t: nadando(t, 1.0)))

        # mergulhar a cabeça: o rabo para cima
        def mergulhar(t):
            u = curva(t, [(0, 0), (0.2, 1), (0.8, 1), (1, 0)])
            p = nadando(t * 3 % 1.0, 1.0 * u)
            p.girar("Quadril", qx(95 * u))
            b.olhar(p, abaixar=25 * u)
            ave_cauda(b, p, x=-20 * u)
            return p
        out.append(gravar_clipe(b, "Mergulhar", 60, mergulhar))

    if nome == "Peru":
        # ciscar: um pé raspa o chão para trás duas vezes e a cabeça bica
        def ciscar(t):
            r = curva(t, [(0, 0), (0.1, 1), (0.2, 0), (0.3, 1), (0.4, 0), (1, 0)])
            bica = tremida(t, 0.62, 0.1) ** 2 + tremida(t, 0.8, 0.1) ** 2

            def mexer(p):
                p.girar("Quadril", qx(8))
                if bica > 0.01 or t > 0.5:
                    cabeca_ate_z(b, p, (0.05 + 0.25 * (1 - min(1, bica * 2))) * b.perna, maximo=60, corpo=True)
                else:
                    b.olhar(p, abaixar=25)
            pes = {"T.R": b.pe_repouso("T.R") + Vector((0, 0.25 * b.perna * r - 0.1 * b.perna * (1 - r) * (t < 0.45),
                                                         0.06 * b.perna * r))}
            return de_pe(mexer, 0.0, pes=pes, g3={"T.R": rad(-20) * r})
        out.append(gravar_ciclo(b, "Ciscar", 48, ciscar))

        # exibir (o peru estufado): as asas arrastando, o corpo inflado, virando devagar
        def exibir(t):
            def mexer(p):
                p.girar("Quadril", qx(-6) @ qz(20 * math.sin(TAU * t)))
                b.olhar(p, abaixar=-20, pesos=[0.5, 0.3, 0.2])
                if b.tem("Papo"):
                    p.girar("Papo", qx(10 * math.sin(TAU * t * 6)))
            p = Pose()
            mexer(p)
            A.por(p, 0.0, (-25, -25, -30))
            p.S["Peito"] = Vector((1.08, 1.0, 1.08))
            return ave_pernas(b, p, "chao")
        out.append(gravar_ciclo(b, "Exibir", 72, exibir))
        out.append(gravar_ciclo(b, "Voo_Curto", 14, lambda t: voando(t, 60, 1.0, "penduradas", pitch=niv * 0.7)))

    if nome == "Aguia":
        def mergulho(t):
            p = voando(0.0, 0, 0.35, "garras", pitch=niv + 40)
            ave_cauda(b, p, x=-10)
            return p
        out.append(gravar_ciclo(b, "Mergulho", 24, mergulho))

        def agarrar(t):
            u = curva(t, [(0, 0), (0.35, 1), (0.6, 1), (1, 0.3)])
            p = voando(0.25 + 0.5 * t, 30 * u, 1.0, "garras", pitch=niv - 35 * u)
            return p
        out.append(gravar_clipe(b, "Agarrar", 20, agarrar))
        out.append(gravar_ciclo(b, "Voar_Com_Peixe", 22, lambda t: voando(t, 50, 1.0, "penduradas")))

    if nome == "Urubu":
        out.append(gravar_ciclo(b, "Asas_Abertas", 72, lambda t: de_pe(
            lambda p: (respirar(b, p, t, 1.0, 2), b.olhar(p, virar=20 * math.sin(TAU * t))), 1.0,
            (8 * math.sin(TAU * t), 8 * math.sin(TAU * t), 8 * math.sin(TAU * t)))))

    # dormir: agachado, o pescoço encolhido e a cabeça baixa (o peru empoleirado)
    def dormir(t):
        def mexer(p):
            respirar(b, p, t, 1.0, 1)
            p.mover("Quadril", (0, 0, -0.22 * b.perna))
            for o in b.pescoco:
                p.girar(o, qx(28))
            b.olhar(p, abaixar=35, virar=15)
        p = Pose()
        mexer(p)
        A.por(p, 0.0)
        Rw, Hw = b.fk(p)
        return ave_pernas(b, p, "chao")
    out.append(gravar_ciclo(b, "Empoleirado" if nome == "Peru" else "Dormir", 96, dormir))

    # morrer: as asas meio abertas, tomba de lado
    def morto(s):
        e = suave(s)
        p = Pose()
        p.girar("Quadril", qy(85 * e) @ qx(niv * 0.8 * e))
        b.olhar(p, abaixar=-20 * e, inclinar=-30 * e)
        A.por(p, 0.35 * e, (-10 * e, -15 * e, -20 * e))
        for ch, per in b.pernas.items():
            p.girar(per.ossos[0], qx(-30 * e))
            p.girar(per.ossos[2], qx(30 * e))
            p.girar(per.ossos[3], qx(40 * e))
        return p
    dz_morto = acertar_altura(b, lambda d: (lambda p: (p.mover("Quadril", (0, 0, d)), p)[1])(morto(1.0)),
                              np.ones(len(b.ob.data.vertices), bool), 0.0, d0=-0.3 * b.perna)

    def morrer(t):
        p = morto(t)
        p.mover("Quadril", (0, 0, dz_morto * suave(t)))
        return nao_entrar(b, p)
    out.append(gravar_clipe(b, "Morrer", 24, morrer))
    out.append(gravar_clipe(b, "Morto", 2, lambda t: morrer(1.0)))
    return out


# ---------------------------------------------------------------------------------------------------------------------
# O sapo
# ---------------------------------------------------------------------------------------------------------------------

def acoes_sapo(b, nome):
    K = Construtores(b, "sapo")
    out = []

    def papo(p, inflar):
        p.S["Papo"] = Vector((1 + 0.9 * inflar, 1 + 0.9 * inflar, 1 + 0.9 * inflar))

    def parado(t):
        def mexer(p):
            papo(p, 0.12 * (0.5 + 0.5 * math.sin(TAU * t * 6)))
            p.girar("Quadril", qx(-1.5 * math.sin(TAU * t * 2)))
            b.olhar(p, virar=8 * math.sin(TAU * t) * (t > 0.5))
        return em_pe(b, mexer)
    out.append(gravar_ciclo(b, "Parado", 48, parado))

    def coaxar(t):
        u = curva(t, [(0, 0), (0.3, 1), (0.6, 1), (0.8, 0), (1, 0)])

        def mexer(p):
            papo(p, 1.0 * u + 0.08 * math.sin(TAU * t * 12) * u)
            p.girar("Quadril", qx(-6 * u))
            boca(b, p, -2 * u)
        return em_pe(b, mexer)
    out.append(gravar_clipe(b, "Coaxar", 36, coaxar))

    # o pulo: agacha, as de trás esticam (os pés no chão), o corpo voa para cima e para a frente, as da frente pousam
    def pular(t):
        L = b.perna
        dz, dy, pit = curva(t, [(0, (0, 0, 0)), (0.15, (-0.15, 0.05, 5)), (0.32, (1.6, -0.6, -35)),
                                (0.55, (1.2, -0.9, 10)), (0.75, (0.1, -0.2, 8)), (1, (0, 0, 0))])
        estica = curva(t, [(0, 0), (0.15, 0), (0.3, 1), (0.5, 0.8), (0.75, 0), (1, 0)])

        def mexer(p):
            p.mover("Quadril", (0, dy * L, dz * L))
            p.girar("Quadril", qx(pit))
            boca(b, p, 0)
        p = Pose()
        mexer(p)
        Rw, Hw = b.fk(p)
        alvos, g3, g4 = {}, {}, {}
        for ch, per in b.pernas.items():
            Lp = per.L1 + per.L2 + per.L3
            if ch[0] == "T":
                no_chao = t < 0.28 or t > 0.8
                if no_chao:
                    alvos[ch] = per.pe.copy()
                else:
                    raiz = Hw[per.ossos[0]]
                    atras = Rw["Quadril"] @ Vector((0, 1, -0.35)).normalized()
                    alvos[ch] = raiz + atras * (0.95 * Lp * estica + 0.5 * Lp * (1 - estica))
                    alvos[ch].x = per.pe.x * 0.7
                g3[ch] = rad(-100) * estica
                g4[ch] = rad(-40) * estica
            else:
                raiz = Hw[per.ossos[0]]
                frente = Rw["Quadril"] @ Vector((0, -0.6, -1)).normalized()
                voo = 0.25 < t < 0.7
                alvos[ch] = (raiz + frente * 0.8 * Lp) if voo else per.pe + Vector((0, dy * L if 0.7 <= t < 0.8 else 0, 0))
                if not voo:
                    alvos[ch] = per.pe.copy()
        b.pes_no_chao(p, alvos, g3, g4)
        return p
    out.append(gravar_clipe(b, "Pular", 20, pular))

    def lingua(t):
        u = curva(t, [(0, 0), (0.3, 1), (0.6, 1), (1, 0)])

        def mexer(p):
            p.mover("Quadril", (0, -0.08 * b.perna * u, 0.04 * b.perna * u))
            p.girar("Quadril", qx(-8 * u))
            boca(b, p, 30 * curva(t, [(0, 0), (0.25, 1), (0.55, 1), (0.7, 0), (1, 0)]))
        return em_pe(b, mexer)
    out.append(gravar_clipe(b, "Lingua", 12, lingua))

    # nadar: as de trás chutam juntas, as da frente encolhidas
    def nadar(t):
        p = Pose()
        p.girar("Quadril", qx(-5))
        Rw, Hw = b.fk(p)
        alvos, g3 = {}, {}
        chute = max(0.0, math.sin(TAU * t)) ** 0.6
        for ch, per in b.pernas.items():
            Lp = per.L1 + per.L2 + per.L3
            raiz = Hw[per.ossos[0]]
            if ch[0] == "T":
                alvos[ch] = raiz + Vector((0.35 * Lp * (1 - chute) * (1 if ch[2] == "L" else -1), 0.3 * Lp + 0.6 * Lp * chute,
                                           -0.1 * Lp))
                g3[ch] = rad(-90) * chute
            else:
                alvos[ch] = raiz + Vector((0, 0.15 * Lp, -0.3 * Lp))
                g3[ch] = rad(40)
        b.pes_no_chao(p, alvos, g3, {ch: rad(-30) for ch in alvos})
        return p
    out.append(gravar_ciclo(b, "Nadar", 18, nadar))

    # morrer: de barriga para cima
    def morto(s):
        e = suave(s)
        p = Pose()
        p.girar("Quadril", qy(175 * e))
        boca(b, p, 10 * e)
        for ch, per in b.pernas.items():
            p.girar(per.ossos[0], qx(-20 * e))
        return p
    dz = acertar_altura(b, lambda d: (lambda p: (p.mover("Quadril", (0, 0, d)), p)[1])(morto(1.0)),
                        np.ones(len(b.ob.data.vertices), bool), 0.0, d0=0.0)

    def morrer(t):
        u = suave(t)
        p = morto(t)
        p.mover("Quadril", (0, 0, dz * u + 0.5 * b.perna * math.sin(math.pi * min(1, t * 1.2))))
        return nao_entrar(b, p)
    out.append(gravar_clipe(b, "Morrer", 18, morrer))
    out.append(gravar_clipe(b, "Morto", 2, lambda t: morrer(1.0)))
    return out


GRUPOS = {
    "canideo": acoes_canideo,
    "cervideo": acoes_cervideo,
    "urso": acoes_urso,
    "guaxinim": acoes_guaxinim,
    "gamba": acoes_gamba,
    "castor": acoes_castor,
    "coelho": acoes_coelho,
    "ave": acoes_ave,
    "sapo": acoes_sapo,
}


def acoes_do_bicho(nome, ob, rig, d):
    b = Bicho(nome, ob, rig)
    fn = GRUPOS.get(d["grupo"])
    if fn is None:
        return []
    vivas = {}
    for a in fn(b, nome):
        try:
            vivas[a.name] = a
        except ReferenceError:
            pass
    acoes = list(vivas.values())
    b.aplicar(Pose())
    faixas_nla(rig, acoes, acoes[0])
    return acoes
