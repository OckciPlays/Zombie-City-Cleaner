# Os instrumentos da trilha do Zombie City Cleaner, calculados por código (o mesmo jeito da trilha do Missão Lázaro:
# tabelas de harmônicos, vozes um pouco desafinadas, envelopes), mais os que o clima pede: o piano velho e desafinado,
# o metal friccionado (o agudo frio), o vento da neblina, o sino da igreja longe, a subida de ruído, e um eco de espaço
# grande por convolução.
import numpy as np
import scipy.signal as sg

SR = 44100
N = 8192
_rng = np.random.default_rng(1983)


def sorte(seed):
    global _rng
    _rng = np.random.default_rng(seed)


def hz(m):
    return 440.0 * 2.0 ** ((m - 69.0) / 12.0)


def _tabela(pesos):
    i = np.arange(N) / N
    x = np.zeros(N)
    for k, w in enumerate(pesos, 1):
        if w:
            x += w * np.sin(2 * np.pi * k * i)
    return (x / (np.abs(x).max() + 1e-12)).astype(np.float32)


HARM = [1, 2, 3, 4, 6, 8, 12, 16, 24, 32, 48]
TAB = {}
for _n in HARM:
    TAB['serra', _n] = _tabela([(1 / k) / (1 + (k / 9) ** 2) for k in range(1, _n + 1)])
    TAB['cello', _n] = _tabela([(1 / k) * (1 + 0.9 * np.exp(-((k - 4) / 2.2) ** 2)) / (1 + (k / 14) ** 2) for k in range(1, _n + 1)])
    TAB['pclaro', _n] = _tabela([(1 / k) ** 1.15 * (1.25 if k in (2, 3) else 1) for k in range(1, _n + 1)])
    TAB['pescuro', _n] = _tabela([[1, 0.36, 0.12, 0.05][k - 1] if k <= 4 else 0 for k in range(1, _n + 1)])
TAB['baixo'] = _tabela([1, 0.35, 0.10])

VOGAIS = {'a': [(800, 1, 110), (1150, .5, 120), (2900, .1, 160)], 'u': [(350, 1, 80), (600, .35, 90), (2700, .05, 150)],
          'o': [(450, 1, 90), (800, .45, 100), (2800, .06, 150)]}
_coro = {}


def tab(tipo, f, brilho=1.0):
    limite = 9000.0 * brilho / max(f, 1.0)
    esc = 1
    for n in HARM:
        if n <= limite:
            esc = n
    return TAB[tipo, esc]


def tab_coro(vogal, f):
    chave = (vogal, int(round(f)))
    if chave not in _coro:
        pesos = []
        for k in range(1, int(min(9000.0 / f, 40)) + 1):
            g = 0.04 + sum(a / (1 + ((k * f - F) / B) ** 2) for F, a, B in VOGAIS[vogal])
            pesos.append(g / k ** 0.6)
        _coro[chave] = _tabela(pesos)
    return _coro[chave]


def ler(t, fase):
    p = (np.mod(fase, 1.0) * N).astype(np.float64)
    i = p.astype(np.int64)
    fr = (p - i).astype(np.float32)
    return t[i % N] * (1 - fr) + t[(i + 1) % N] * fr


def env(n, ataque, segura, solta):
    i = np.arange(n, dtype=np.float32)
    a = max(ataque * SR, 1.0)
    x = np.clip(i / a, 0, 1)
    e = x * x * (3 - 2 * x)
    h = int(segura * SR)
    if h < n:
        s = max(solta * SR, 1.0)
        y = np.clip(1 - (i[h:] - h) / s, 0, 1)
        e[h:] *= y * y
    return e.astype(np.float32)


def suave(n, por_segundo, fase=0):
    k = max(4, int(n / SR * por_segundo) + 4)
    pontos = _rng.uniform(-1, 1, k)
    return np.interp(np.linspace(0, k - 1, n), np.arange(k), pontos).astype(np.float32)


def _sos(tipo, f, ordem=2):
    if tipo == 'band':
        return sg.butter(ordem, f, btype='band', fs=SR, output='sos')
    return sg.butter(ordem, f, btype=tipo, fs=SR, output='sos')


def filtrar(x, tipo, f, ordem=2):
    return sg.sosfilt(_sos(tipo, f, ordem), x).astype(np.float32)


def _fase(f, t, d, curva):
    if not curva:
        return f * t
    fator = 2.0 ** (curva / 12.0 * np.clip(t / max(d, 1e-3), 0, 1))
    return np.cumsum(f * fator) / SR


# ---------------------------------------------------------------------------------------------- os instrumentos
# cada um devolve o som (mono, ou (esquerda, direita) no vento), a partir da frequência f, da duração d (s), do volume v

def piano(f, d, v, ex):
    """O piano velho: duas tabelas (clara que apaga, escura que fica), três cordas um nada desafinadas entre si e cada
    nota com o seu desvio (desafinado, em cents), o martelo; 'corte' abafa (o piano longe, na neblina)."""
    desaf = ex.get('desafinado', 7.0)
    f = f * 2 ** (_rng.uniform(-desaf, desaf) / 1200)
    n = int((d + 0.4) * SR)
    t = np.arange(n, dtype=np.float32) / SR
    decai = 0.30 + f / 1100.0
    apaga = 3.0 + f / 260.0
    corpo = np.exp(-t * decai) * np.minimum(t * 500, 1)
    luz = np.exp(-t * apaga)
    h = int(d * SR)
    if h < n:
        corpo[h:] *= np.clip(1 - np.arange(n - h) / (0.35 * SR), 0, 1)
    claro, escuro = tab('pclaro', f), tab('pescuro', f)
    ph = f * t
    x = ler(escuro, ph) * (1 - luz) + ler(claro, ph) * luz + ler(escuro, ph * (1 + desaf * 0.00013) + 0.13) * 0.55
    if desaf > 3:
        x += ler(escuro, ph * (1 - desaf * 0.00009) + 0.57) * 0.3
    m = min(n, 700)
    x[:m] += _rng.uniform(-1, 1, m).astype(np.float32) * (1 - np.arange(m) / m) * 0.3
    y = x * corpo * v * 0.6
    if 'corte' in ex:
        y = filtrar(y, 'low', ex['corte'])
    return y


def reverso(f, d, v, ex):
    """O piano ao contrário: a nota cresce e corta (a chegada do susto)."""
    y = piano(f, d + 0.5, v, ex)
    y = y[::-1][-int(d * SR) - 1:]
    corte = int(0.015 * SR)
    y[-corte:] *= np.linspace(1, 0, corte)
    return y


def cordas(f, d, v, ex):
    at, so = ex.get('ataque', 0.8), ex.get('solta', 1.2)
    n = int((d + so) * SR)
    t = np.arange(n, dtype=np.float32) / SR
    tb = tab('serra', f, ex.get('brilho', 0.8))
    vib = 0.0035 * f / (2 * np.pi * 5.3) * np.sin(2 * np.pi * 5.3 * t + _rng.uniform(0, 6))
    ph = _fase(f, t, d, ex.get('curva', 0))
    x = ler(tb, ph * 0.9962 + vib) + ler(tb, ph + vib * 1.2 + 0.31) + ler(tb, ph * 1.0035 + vib * 0.8 + 0.67)
    e = env(n, at, d, so)
    if 'tremolo' in ex:
        e = e * (1 - ex.get('prof', 0.6) * (0.5 + 0.5 * np.sin(2 * np.pi * ex['tremolo'] * t)))
    return x * e * v * 0.55


def cello(f, d, v, ex):
    at, so = ex.get('ataque', 0.16), ex.get('solta', 0.4)
    n = int((d + so) * SR)
    t = np.arange(n, dtype=np.float32) / SR
    tb = tab('cello', f)
    chegada = np.minimum(t / 0.45, 1)
    vib = 0.0055 * f / (2 * np.pi * 5.6) * np.sin(2 * np.pi * 5.6 * t) * chegada
    u = np.minimum(t / max(d, 0.01), 1)
    arco = 0.78 + 0.22 * (1 - np.abs(u * 2 - 1))
    ph = _fase(f, t, d, ex.get('curva', 0))
    x = ler(tb, ph + vib) + ler(tb, ph * 1.002 + vib + 0.4) * 0.35
    x += _rng.uniform(-1, 1, n).astype(np.float32) * 0.03
    return x * env(n, at, d, so) * arco * v * 0.65


def coro(f, d, v, ex):
    at, so = ex.get('ataque', 2.0), ex.get('solta', 2.0)
    n = int((d + so) * SR)
    t = np.arange(n, dtype=np.float32) / SR
    tb = tab_coro(ex.get('vogal', 'u'), f)
    vib = 0.004 * f / (2 * np.pi * 4.6) * np.sin(2 * np.pi * 4.6 * t + _rng.uniform(0, 6))
    ph = _fase(f, t, d, ex.get('curva', 0))
    x = ler(tb, ph * 0.995 + vib) + ler(tb, ph * 1.004 + vib * 0.7 + 0.5) + ler(tb, ph + vib * 1.3 + 0.25)
    sopro = filtrar(_rng.standard_normal(n).astype(np.float32), 'band', [800, 4000]) * 0.04
    return (x + sopro) * env(n, at, d, so) * v * 0.6


def baixo(f, d, v, ex):
    at, so = ex.get('ataque', 0.6), ex.get('solta', 1.0)
    n = int((d + so) * SR)
    t = np.arange(n, dtype=np.float32) / SR
    tb = TAB['baixo']
    x = ler(tb, f * t)
    if 'batimento' in ex:
        x = 0.6 * x + 0.6 * ler(tb, (f + ex['batimento']) * t + 0.3)
    return x * env(n, at, d, so) * v * 0.38   # o grave sustentado embolava tudo: bem mais baixo


def caixinha(f, d, v, ex):
    n = int(3.0 * SR)
    t = np.arange(n, dtype=np.float32) / SR
    x = (np.sin(2 * np.pi * f * t) * np.exp(-t * 1.6) + np.sin(2 * np.pi * f * 3 * t) * 0.3 * np.exp(-t * 5)
         + np.sin(2 * np.pi * f * 5.93 * t) * 0.12 * np.exp(-t * 11))
    return (x * np.minimum(t * 600, 1) * v).astype(np.float32)


def timpano(f, d, v, ex):
    decai = ex.get('decai', 2.0)
    n = int(min(5.0 / decai + 0.1, 6.0) * SR)
    t = np.arange(n, dtype=np.float32) / SR
    giro = f * t + f * 0.5 * (1 - np.exp(-t * 25)) / 25
    x = np.sin(2 * np.pi * giro) + np.sin(2 * np.pi * giro * 1.5) * 0.25 * np.exp(-t * 4)
    ruido = _rng.uniform(-1, 1, n).astype(np.float32)
    return ((x * np.exp(-t * decai) + ruido * np.exp(-t * 60) * 0.3) * np.minimum(t * 400, 1) * v).astype(np.float32)


def coracao(f, d, v, ex):
    """O coração: um baque grave que cai de tom (f é a altura relativa, 1 = normal)."""
    n = int(0.32 * SR)
    t = np.arange(n, dtype=np.float32) / SR
    giro = 44 * f * t + 22 * f * (1 - np.exp(-t * 30)) / 30
    corpo = np.sin(2 * np.pi * giro) * np.exp(-t * 16)
    ruido = filtrar(_rng.uniform(-1, 1, n).astype(np.float32), 'low', 600)
    return ((corpo + ruido * np.exp(-t * 50) * 0.8) * np.minimum(t * 500, 1) * v).astype(np.float32)


def sino(f, d, v, ex):
    """O sino da igreja, longe: parciais que não casam, cada um sumindo no seu tempo ('longo' segura mais)."""
    lento = ex.get('longo', 1.0)
    n = int(9.0 * lento * SR)
    t = np.arange(n, dtype=np.float32) / SR
    parciais = [(0.5, 0.6, 0.6), (1, 1, 0.9), (1.19, 0.45, 1.4), (1.5, 0.2, 1.8), (2.0, 0.35, 2.2), (2.52, 0.22, 3.5), (3.36, 0.1, 5)]
    x = np.zeros(n, np.float32)
    for r, a, dec in parciais:
        x += a * np.sin(2 * np.pi * f * r * t + _rng.uniform(0, 6)) * np.exp(-t * dec / lento)
    m = int(0.004 * SR)
    x[:m] += _rng.uniform(-1, 1, m) * 0.4
    return (x * np.minimum(t * 500, 1) * v).astype(np.float32)


def metal(f, d, v, ex):
    """O metal friccionado (uma tigela, um vidro tocado com arco): parciais que não casam, cada um tremendo devagar, e o
    chiado do arco. 'curva' desliza a nota (semitons ao longo da duração)."""
    at, so = ex.get('ataque', 1.5), ex.get('solta', 2.0)
    n = int((d + so) * SR)
    t = np.arange(n, dtype=np.float32) / SR
    base = _fase(f, t, d, ex.get('curva', 0))
    x = np.zeros(n, np.float32)
    for r, w in [(1, 1), (2.0, 0.22), (2.76, 0.38), (5.4, 0.14), (8.93, 0.05)]:
        if f * r > 15000:
            continue
        trem = 0.75 + 0.25 * np.sin(2 * np.pi * _rng.uniform(0.25, 1.1) * t + _rng.uniform(0, 6))
        x += w * trem * (np.sin(2 * np.pi * r * base + _rng.uniform(0, 6)) + 0.6 * np.sin(2 * np.pi * r * base * 1.0016 + _rng.uniform(0, 6)))
    b, a = sg.iirpeak(min(f, 15000), 18, fs=SR)
    arco = sg.lfilter(b, a, _rng.standard_normal(n)).astype(np.float32)
    x += 0.5 * arco / (np.abs(arco).max() + 1e-9) * (0.6 + 0.4 * suave(n, 3))
    return x * env(n, at, d, so) * v * 0.5


def pizz(f, d, v, ex):
    n = int(0.9 * SR)
    t = np.arange(n, dtype=np.float32) / SR
    ph = f * t
    luz = np.exp(-t * 14)
    x = ler(tab('serra', f, 0.6), ph) * luz + ler(tab('serra', f, 0.15), ph) * (1 - luz)
    return x * np.exp(-t * ex.get('decai', 6.5)) * np.minimum(t * 900, 1) * v


def vento(f, d, v, ex):
    """O vento da neblina, em estéreo: chiado grave que vem e vai em rajadas, e um assovio fino de vez em quando."""
    n = int(d * SR)
    corte = ex.get('corte', 500)
    lados = []
    for c in range(2):
        r = _rng.standard_normal(n).astype(np.float32)
        grave = filtrar(r, 'low', corte, 2) * (0.55 + 0.45 * suave(n, 0.12))
        fino = filtrar(_rng.standard_normal(n).astype(np.float32), 'band', [corte * 1.6, corte * 2.4]) * np.clip(suave(n, 0.08), 0, 1) ** 2 * 0.5
        lados.append(grave + fino)
    e = env(n, ex.get('ataque', 3.0), d - ex.get('solta', 3.0), ex.get('solta', 3.0))
    escala = max(np.abs(lados[0]).max(), np.abs(lados[1]).max(), 1e-9)
    return (lados[0] / escala * e * v * 1.9, lados[1] / escala * e * v * 1.9)


def subida(f, d, v, ex):
    """A subida (o metal raspando e o chiado crescendo até o susto)."""
    n = int(d * SR)
    t = np.arange(n, dtype=np.float32) / SR
    r = _rng.standard_normal(n).astype(np.float32)
    y = np.zeros(n, np.float32)
    faixas = [(300, 800), (800, 2000), (2000, 5000), (5000, 11000)]
    for i, (a, b) in enumerate(faixas):
        c = (i + 0.6) / len(faixas)
        y += filtrar(r, 'band', [a, b]) * np.exp(-((t / d - c) / 0.25) ** 2)
    y /= np.abs(y).max() + 1e-9
    base = np.cumsum(f * 2 ** (2.5 * t / d)) / SR
    y += 0.35 * (np.sin(2 * np.pi * base) + 0.5 * np.sin(2 * np.pi * 2.76 * base))
    return y * (t / d) ** 1.6 * v


INSTRUMENTOS = {'piano': piano, 'reverso': reverso, 'cordas': cordas, 'cello': cello, 'coro': coro, 'baixo': baixo,
                'caixinha': caixinha, 'timpano': timpano, 'coracao': coracao, 'sino': sino, 'metal': metal, 'pizz': pizz,
                'vento': vento, 'subida': subida}


# ---------------------------------------------------------------------------------------------- o eco

def eco(rt_grave, rt_agudo, duracao, antes=0.03, seed=7):
    """A resposta de um espaço grande (estéreo): ruído que some mais devagar no grave do que no agudo."""
    r = np.random.default_rng(seed)
    n = int(duracao * SR)
    t = np.arange(n, dtype=np.float32) / SR
    rt_meio = (rt_grave + rt_agudo) / 2
    lados = []
    for c in range(2):
        x = r.standard_normal(n).astype(np.float32)
        y = (filtrar(x, 'low', 400) * np.exp(-6.91 * t / rt_grave) + filtrar(x, 'band', [400, 3000]) * np.exp(-6.91 * t / rt_meio)
             + filtrar(x, 'high', 3000) * np.exp(-6.91 * t / rt_agudo))
        y *= np.minimum(1, t / 0.025)
        y = np.concatenate([np.zeros(int(antes * SR), np.float32), y])
        lados.append(y / np.sqrt((y.astype(np.float64) ** 2).sum()))
    return lados
