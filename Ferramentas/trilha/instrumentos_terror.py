# Os instrumentos do terror (04/11/2026, a pedido: a luta contra os zumbis "mais medonha, aterrorizante, com esse coro
# dando suspense, nível filme de terror de hollywood"). Somam-se aos do instrumentos.py (o mesmo jeito: tabelas de
# harmônicos, vozes desafinadas, envelopes) e entram no mesmo dicionário, para o estúdio tocar todos:
#  - cluster: as cordas num cachinho de semitons, cada voz deslizando para um lado (o "enxame" dos filmes de terror);
#  - estridente: o violino que grita (o arco forte perto do cavalete, o ataque seco);
#  - braam: os metais graves num acorde, o filtro abrindo enquanto cresce, o rosnado;
#  - impacto: o golpe grave do trailer (o seno que despenca de tom, o estalo, o ar);
#  - gemido: o coro que geme (desafinado entre as vozes, a vogal fechando, a nota caindo no fim);
#  - sussurro: o ar das vozes, sílabas que vêm e vão de um lado para o outro (estéreo);
#  - raspado: o tique seco do arco de madeira na corda (col legno);
#  - prato_reverso: o chiado que cresce até cortar no golpe;
#  - ronco: o grave que não para (o ruído de baixo, o seno batendo devagar).
import numpy as np
import instrumentos as I

SR = I.SR


def _ruido(n):
    return I._rng.standard_normal(n).astype(np.float32)


def cluster(f, d, v, ex):
    """'largura' semitons em volta da nota, 'vozes' vozes; 'curva' (semitons) desliza cada voz, para cima ou para baixo
    ('direcao' 0: cada uma para um lado; -1 todas descendo; 1 todas subindo), cada uma num tanto; 'ponticello' (0 a 1)
    o brilho vítreo e o chiado do arco perto do cavalete; 'tremolo' (Hz) e 'prof' como nas cordas."""
    at, so = ex.get('ataque', 1.0), ex.get('solta', 1.5)
    largura, vozes = ex.get('largura', 3.0), int(ex.get('vozes', 7))
    curva, direcao, pont = ex.get('curva', 0.0), ex.get('direcao', 0), ex.get('ponticello', 0.0)
    n = int((d + so) * SR)
    t = np.arange(n, dtype=np.float32) / SR
    x = np.zeros(n, np.float32)
    for k in range(vozes):
        desvio = -largura / 2 + largura * k / max(vozes - 1, 1) + I._rng.uniform(-0.12, 0.12)
        fk = f * 2 ** (desvio / 12)
        sinal = direcao if direcao else (1 if I._rng.random() < 0.5 else -1)
        ck = abs(curva) * I._rng.uniform(0.35, 1.0) * sinal
        vib = 0.004 * fk / (2 * np.pi * 5.5) * np.sin(2 * np.pi * I._rng.uniform(4.6, 6.4) * t + I._rng.uniform(0, 6))
        tb = I.tab('serra', fk * 2 ** (max(ck, 0) / 12), ex.get('brilho', 0.9))
        x += I.ler(tb, I._fase(fk, t, d, ck) + vib) * (0.8 + 0.2 * I.suave(n, 0.6))
    x /= np.sqrt(vozes)
    if pont > 0:
        alto = I.filtrar(x, 'high', 2200)
        chiado = I.filtrar(_ruido(n), 'band', [3000, 9500]) * 0.2 * (0.6 + 0.4 * I.suave(n, 4))
        x = x * (1 - 0.55 * pont) + (alto * 2.2 + chiado) * pont
    e = I.env(n, at, d, so)
    if 'tremolo' in ex:
        e = e * (1 - ex.get('prof', 0.6) * (0.5 + 0.5 * np.sin(2 * np.pi * ex['tremolo'] * t)))
    return x * e * v * 0.55


def estridente(f, d, v, ex):
    """O violino que grita: três arcos fortes um nada desafinados, o chiado do cavalete, ataque seco que some
    ('decai'), e 'curva' semitons de deslize."""
    n = int((d + 0.3) * SR)
    t = np.arange(n, dtype=np.float32) / SR
    curva = ex.get('curva', 0.0)
    tb = I.tab('serra', f * 2 ** (max(curva, 0) / 12), 1.4)
    ph = I._fase(f, t, d, curva)
    vib = 0.006 * f / (2 * np.pi * 7) * np.sin(2 * np.pi * 7 * t)
    x = I.ler(tb, ph + vib) + I.ler(tb, ph * 1.006 + vib + 0.3) + I.ler(tb, ph * 0.994 + 0.6)
    x = I.filtrar(x, 'high', 500)
    x += I.filtrar(_ruido(n), 'band', [2500, 9000]) * 0.5
    e = np.minimum(t / 0.006, 1) * (0.35 + 0.65 * np.exp(-t * ex.get('decai', 3.0)))
    h = int(d * SR)
    if h < n:
        e[h:] *= np.clip(1 - np.arange(n - h) / (0.25 * SR), 0, 1)
    return (x * e * v * 0.3).astype(np.float32)


def braam(f, d, v, ex):
    """Os metais graves: três vozes de serra, o rosnado ('rosnado' 0 a 1, na velocidade 'rosnar' Hz), o filtro que abre
    junto com o volume ('abre' 0 a 1) e a saturação do sopro forte."""
    at, so = ex.get('ataque', 0.3), ex.get('solta', 1.2)
    n = int((d + so) * SR)
    t = np.arange(n, dtype=np.float32) / SR
    tb = I.tab('serra', f, 1.6)
    ph = I._fase(f, t, d, ex.get('curva', 0.0))
    x = I.ler(tb, ph) + I.ler(tb, ph * 1.004 + 0.2) + I.ler(tb, ph * 0.9965 + 0.55)
    ros = ex.get('rosnado', 0.35)
    x *= 1 + ros * np.sin(2 * np.pi * ex.get('rosnar', 31) * t) * (0.6 + 0.4 * I.suave(n, 3))
    e = I.env(n, at, d, so)
    abre = np.clip(e, 0, 1) ** 1.4 * ex.get('abre', 1.0)
    escuro = I.filtrar(x, 'low', 180 + 2.5 * f)
    claro = I.filtrar(x, 'low', 2800, 2)
    y = escuro * (1 - abre) + claro * abre
    y = np.tanh(y * 1.4) / 1.4
    return (y * e * v * 0.55).astype(np.float32)


def impacto(f, d, v, ex):
    """O golpe grave: o seno que despenca da oitava de cima até a nota, o estalo, o ar que fica ('decai')."""
    dur = ex.get('dur', 4.5)
    n = int(dur * SR)
    t = np.arange(n, dtype=np.float32) / SR
    fr = f * (1 + 2.2 * np.exp(-t * 16))
    corpo = np.sin(2 * np.pi * np.cumsum(fr) / SR) * np.exp(-t * ex.get('decai', 1.1))
    estalo = I.filtrar(_ruido(n), 'low', 3500) * np.exp(-t * 45)
    ar = I.filtrar(_ruido(n), 'band', [150, 1800]) * np.exp(-t * 2.2) * 0.18
    y = np.tanh(1.6 * (corpo + 0.6 * estalo + ar)) / 1.2
    return (y * np.minimum(t * 800, 1) * v).astype(np.float32)


def gemido(f, d, v, ex):
    """O coro que geme: 'vozes' vozes desafinadas entre si ('espalho' cents), a vogal indo de 'vogal' para 'fecha' ao
    longo da nota, e no fim ('queda_em', fração da nota) a nota caindo 'queda' semitons, como quem chora."""
    at, so = ex.get('ataque', 1.0), ex.get('solta', 1.8)
    n = int((d + so) * SR)
    t = np.arange(n, dtype=np.float32) / SR
    queda, queda_em = ex.get('queda', -1.0), ex.get('queda_em', 0.7)
    u = np.clip((t - d * queda_em) / max(d * (1 - queda_em), 0.05), 0, 1)
    fator = 2.0 ** (queda / 12 * u * u * (3 - 2 * u))
    abre, fecha = I.tab_coro(ex.get('vogal', 'a'), f), I.tab_coro(ex.get('fecha', 'u'), f)
    mistura = np.clip(t / max(d, 0.1), 0, 1) ** 1.5
    vozes, espalho = int(ex.get('vozes', 4)), ex.get('espalho', 18.0)
    x = np.zeros(n, np.float32)
    for k in range(vozes):
        cents = -espalho + 2 * espalho * k / max(vozes - 1, 1)
        fk = f * 2 ** (cents / 1200)
        vib = 0.005 * fk / (2 * np.pi * 4.8) * np.sin(2 * np.pi * I._rng.uniform(4.2, 5.4) * t + I._rng.uniform(0, 6))
        ph = np.cumsum(fk * fator) / SR + vib + I._rng.uniform(0, 1)
        x += I.ler(abre, ph) * (1 - mistura) + I.ler(fecha, ph) * mistura
    x /= np.sqrt(vozes)
    sopro = I.filtrar(_ruido(n), 'band', [900, 4500]) * 0.05
    return (x + sopro) * I.env(n, at, d, so) * v * 0.6


def sussurro(f, d, v, ex):
    """Os sussurros (estéreo): o ar pelas vogais, sílabas de 'silabas' por segundo que vêm e vão, palavras soltas, e o
    's' chiando de vez em quando; um lado diferente do outro (várias bocas)."""
    n = int(d * SR)
    t = np.arange(n, dtype=np.float32) / SR
    rapidez = ex.get('silabas', 6.0)
    lados = []
    for c in range(2):
        r = _ruido(n)
        y = np.zeros(n, np.float32)
        for F, a, B in [(700, 1.0, 260), (1200, 0.7, 300), (2600, 0.35, 500)]:
            centro = F * (1 + 0.15 * I.suave(n, 2.5))
            y += I.filtrar(r, 'band', [float(np.min(centro)) * 0.8, float(np.max(centro)) * 1.15]) * a
        k = max(4, int(d * rapidez))
        silabas = np.interp(np.linspace(0, k - 1, n), np.arange(k), I._rng.uniform(0, 1, k) ** 2).astype(np.float32)
        palavras = np.clip(I.suave(n, 0.7) * 1.4 + 0.2, 0, 1)
        s = I.filtrar(_ruido(n), 'band', [4500, 10000]) * np.clip(I.suave(n, 3) * 2 - 1.2, 0, 1) * 0.6
        lados.append((y * silabas + s) * palavras)
    escala = max(np.abs(lados[0]).max(), np.abs(lados[1]).max(), 1e-9)
    e = I.env(n, ex.get('ataque', 1.0), d - ex.get('solta', 1.0), ex.get('solta', 1.0))
    return lados[0] / escala * e * v, lados[1] / escala * e * v


def raspado(f, d, v, ex):
    """O tique seco do arco de madeira na corda: o estalo e um nada da nota."""
    n = int(0.35 * SR)
    t = np.arange(n, dtype=np.float32) / SR
    estalo = I.filtrar(_ruido(n), 'band', [1200, 6500]) * np.exp(-t * 110)
    tom = np.sin(2 * np.pi * f * t) * np.exp(-t * 28) * 0.6
    return ((estalo + tom) * np.minimum(t * 3000, 1) * v).astype(np.float32)


def prato_reverso(f, d, v, ex):
    """O prato ao contrário: o chiado agudo que cresce até cortar seco no fim da duração (o golpe)."""
    n = int(d * SR)
    t = np.arange(n, dtype=np.float32) / SR
    r = _ruido(n)
    y = I.filtrar(r, 'high', 3500) * 0.7 + I.filtrar(r, 'band', [6000, 13000]) * 0.6
    y *= (t / max(d, 1e-3)) ** 3.2
    corte = int(0.012 * SR)
    y[-corte:] *= np.linspace(1, 0, corte)
    return (y * v).astype(np.float32)


def ronco(f, d, v, ex):
    """O grave que não para: o seno da nota batendo devagar com o vizinho ('batimento' Hz) e o ruído de baixo rolando."""
    at, so = ex.get('ataque', 2.0), ex.get('solta', 2.0)
    n = int((d + so) * SR)
    t = np.arange(n, dtype=np.float32) / SR
    b = ex.get('batimento', 0.7)
    x = np.sin(2 * np.pi * f * t) + 0.8 * np.sin(2 * np.pi * (f + b) * t + 0.4) + 0.25 * np.sin(2 * np.pi * 2 * f * t)
    rolo = I.filtrar(_ruido(n), 'low', 110, 2) * (0.6 + 0.4 * I.suave(n, 0.4)) * 2.5
    return ((x * 0.5 + rolo) * I.env(n, at, d, so) * v * 0.5).astype(np.float32)


I.INSTRUMENTOS.update({'cluster': cluster, 'estridente': estridente, 'braam': braam, 'impacto': impacto, 'gemido': gemido,
                       'sussurro': sussurro, 'raspado': raspado, 'prato_reverso': prato_reverso, 'ronco': ronco})
