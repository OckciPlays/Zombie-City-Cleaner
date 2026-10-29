# Os sons do Oco (etapa 7, a pedido em 28/10/2026): uma voz de zumbi "oca" e aterrorizante, gerada aqui (fonte e filtros
# de voz, ressonância de tubo, assovio de vento num oco, estalos da garganta) misturada com a voz de zumbi que o jogo já
# tem (SomZombie1, deformada). Uso: python sons_do_oco.py <voz_original.wav> <pasta de saída>
import os, sys
from fractions import Fraction
import numpy as np
import scipy.signal as sg
import scipy.io.wavfile as wf

SR = 44100
ORIGINAL, SAIDA = sys.argv[1], sys.argv[2]
os.makedirs(SAIDA, exist_ok=True)

_sr, _x = wf.read(ORIGINAL)
VOZ = _x.astype(np.float32) / 32768.0
TRECHOS = [(0.1, 1.15), (1.7, 2.7), (3.1, 3.95), (4.35, 5.35), (5.3, 7.0)]


def trecho(i):
    a, b = TRECHOS[i]
    x = VOZ[int(a * SR):int(b * SR)].copy()
    return x / (np.abs(x).max() + 1e-9)


def t_de(n):
    return np.arange(n) / SR


def tom(x, semitons):
    """Muda a altura (e a duração junto): para baixo, mais longo e grave."""
    f = 2 ** (semitons / 12.0)
    fr = Fraction(1 / f).limit_denominator(300)
    return sg.resample_poly(x, fr.numerator, fr.denominator).astype(np.float32)


def suave(n, rng, velocidade=8.0):
    """Ruído lento (de -1 a 1), para tremores e variações."""
    k = max(4, int(n / SR * velocidade) + 4)
    pontos = rng.uniform(-1, 1, k)
    return np.interp(np.linspace(0, k - 1, n), np.arange(k), pontos).astype(np.float32)


def passa_banda(x, f1, f2, ordem=2):
    b, a = sg.butter(ordem, [f1, f2], btype='band', fs=SR)
    return sg.lfilter(b, a, x).astype(np.float32)


def passa_baixa(x, f, ordem=2):
    b, a = sg.butter(ordem, f, fs=SR)
    return sg.lfilter(b, a, x).astype(np.float32)


def passa_alta(x, f, ordem=2):
    b, a = sg.butter(ordem, f, btype='high', fs=SR)
    return sg.lfilter(b, a, x).astype(np.float32)


def pente(x, f, g):
    d = max(1, int(SR / f))
    a = np.zeros(d + 1, dtype=np.float64)
    a[0], a[d] = 1.0, -g
    return sg.lfilter([1.0], a, x).astype(np.float32)


def oco(x, mistura=0.5, g=0.6):
    """A ressonância de um corpo oco: três tubos em paralelo (alturas que não casam: soa vazio, de dentro)."""
    y = sum(pente(x, f, g) for f in (167.0, 233.0, 311.0)) / 3.0
    y = passa_baixa(y, 3800)
    y = y / (np.abs(y).max() + 1e-9) * (np.abs(x).max() + 1e-9)
    return ((1 - mistura) * x + mistura * y).astype(np.float32)


def saturar(x, forca):
    return (np.tanh(forca * x) / np.tanh(forca)).astype(np.float32)


VOGAIS = {  # formantes (Hz) e larguras
    'a': ((730, 1090, 2440, 3400), (90, 110, 160, 250)),
    'o': ((450, 800, 2830, 3300), (80, 90, 150, 250)),
    'u': ((330, 700, 2400, 3200), (70, 90, 150, 250)),
    'e': ((530, 1840, 2480, 3500), (80, 110, 160, 250)),
    'h': ((600, 1200, 2500, 3500), (140, 180, 220, 300)),   # um "rh" rouco, aberto
}


def formantes(x, vogal, escala=1.0):
    fs, bs = VOGAIS[vogal]
    y = np.zeros_like(x)
    for f, b, ganho in zip(fs, bs, (1.0, 0.55, 0.32, 0.18)):
        f = min(f * escala, SR / 2 - 500)
        bb, aa = sg.iirpeak(f, f / b, fs=SR)
        y += ganho * sg.lfilter(bb, aa, x).astype(np.float32)
    return y


def voz_sintetica(dur, f0, vogais, rng, fry=0.4, sopro=0.3, jitter=0.03, escala=1.0, forca=2.5):
    """
    Uma voz rouca: pulsos da glote com a altura f0(t) (tremida), cada pulso com força sorteada, um em cada dois mais
    fraco (o "fry", o rosnado), sopro no ritmo dos pulsos, e as vogais (os formantes) mudando ao longo do tempo.
    """
    n = int(dur * SR)
    t = t_de(n)
    f = f0(t) * (1 + jitter * suave(n, rng, 14))
    fase = np.cumsum(f / SR)
    ciclo = np.floor(fase).astype(np.int64)
    p = fase - ciclo
    pulso = np.where(p < 0.65, np.sin(np.pi * p / 0.65 * 0.5) ** 2, np.cos(np.pi * (p - 0.65) / 0.35 * 0.5) ** 2)
    fonte = np.diff(pulso, prepend=0.0).astype(np.float32) * 40
    forcas = 1 + 0.35 * rng.uniform(-1, 1, ciclo.max() + 2)
    fonte *= forcas[ciclo] * (1 - fry * (ciclo % 2))
    ruido = passa_banda(rng.standard_normal(n).astype(np.float32), 500, 6000) * (0.4 + 0.6 * pulso)
    fonte = fonte / (np.abs(fonte).max() + 1e-9) + sopro * ruido / (np.abs(ruido).max() + 1e-9)
    # as vogais: cada uma filtrada inteira, e o peso de cada uma muda no tempo
    saidas = [formantes(fonte, v, escala) for v in vogais]
    if len(saidas) == 1:
        y = saidas[0]
    else:
        pos = np.linspace(0, len(saidas) - 1, n)
        y = np.zeros(n, dtype=np.float32)
        for i, s in enumerate(saidas):
            y += np.clip(1 - np.abs(pos - i), 0, 1) * s
    y = y / (np.abs(y).max() + 1e-9)
    return saturar(y, forca)


def assovio(dur, f, rng, aspero=0.5):
    """O vento num oco: um tom que desliza (f(t)), soprado (a força treme) com um pouco de ruído em volta."""
    n = int(dur * SR)
    t = t_de(n)
    freq = f(t) * (1 + 0.012 * suave(n, rng, 6))
    s = np.sin(2 * np.pi * np.cumsum(freq / SR)) * (0.6 + 0.4 * suave(n, rng, 11))
    s += 0.5 * np.sin(2 * np.pi * np.cumsum(freq * 1.497 / SR)) * (0.5 + 0.5 * suave(n, rng, 9))   # um parcial que não casa
    r = passa_banda(rng.standard_normal(n).astype(np.float32), 400, 1600)
    s = s / (np.abs(s).max() + 1e-9) + aspero * r / (np.abs(r).max() + 1e-9)
    return (s / (np.abs(s).max() + 1e-9)).astype(np.float32)


def estalos(tempos, rng, n, grave=0.6):
    """Estalos da garganta (um "k" seco): cada um, um ressoar curto agudo e um baque grave."""
    y = np.zeros(n, dtype=np.float32)
    for tt in tempos:
        i = int(tt * SR)
        if i >= n:
            continue
        m = min(n - i, int(0.05 * SR))
        tl = t_de(m)
        fa = rng.uniform(1700, 3200)
        k = np.sin(2 * np.pi * fa * tl) * np.exp(-tl / 0.0025) + grave * np.sin(2 * np.pi * rng.uniform(220, 380) * tl) * np.exp(-tl / 0.009)
        k += 0.3 * rng.standard_normal(m) * np.exp(-tl / 0.0015)
        y[i:i + m] += rng.uniform(0.6, 1.0) * k.astype(np.float32)
    return y


def chiado(dur, rng, densidade=60):
    """A pele queimando: um chiado com estalinhos."""
    n = int(dur * SR)
    h = passa_alta(rng.standard_normal(n).astype(np.float32), 3500) * 0.25
    pontos = np.zeros(n, dtype=np.float32)
    k = int(dur * densidade)
    pontos[rng.integers(0, n, k)] = rng.uniform(0.3, 1.0, k) * rng.choice([-1, 1], k)
    estalinhos = passa_alta(sg.lfilter([1], [1, -0.6], pontos).astype(np.float32), 2500)
    y = h * (0.6 + 0.4 * suave(n, rng, 5)) + estalinhos
    return (y / (np.abs(y).max() + 1e-9)).astype(np.float32)


def envelope(n, ataque, queda, sustentar=1.0, curva=2.0):
    t = t_de(n)
    dur = n / SR
    e = np.minimum(1, t / max(ataque, 1e-3))
    fim = np.clip((dur - t) / max(queda, 1e-3), 0, 1) ** curva
    return (e * fim * sustentar).astype(np.float32)


def caber(x, n):
    if len(x) >= n:
        return x[:n]
    return np.concatenate([x, np.zeros(n - len(x), dtype=np.float32)])


def mixar(*partes):
    n = max(len(p) for p, _ in partes)
    y = np.zeros(n, dtype=np.float32)
    for p, g in partes:
        y[:len(p)] += g * p
    return y


def salvar(nome, x, pico=0.89, cauda=0.04):
    x = np.asarray(x, dtype=np.float32)
    x = x - np.mean(x)
    x = passa_alta(x, 45, 2)
    m = int(cauda * SR)
    if m and len(x) > 2 * m:
        x[-m:] *= np.linspace(1, 0, m) ** 2
        x[:int(0.004 * SR)] *= np.linspace(0, 1, int(0.004 * SR))
    x = x / (np.abs(x).max() + 1e-9) * pico
    wf.write(os.path.join(SAIDA, nome + '.wav'), SR, (x * 32767).astype(np.int16))
    print(f'{nome}: {len(x) / SR:.2f} s')


# ---------------------------------------------------------------------------------------------- a voz (o som padrão)
def voz(i):
    rng = np.random.default_rng(100 + i)
    dur = rng.uniform(1.9, 3.1)
    base = rng.uniform(68, 92)
    sobe = rng.uniform(1.15, 1.45)
    vogais = [['o', 'a', 'h'], ['u', 'o', 'a'], ['h', 'a', 'o'], ['a', 'o', 'u'], ['o', 'h', 'u'], ['u', 'a', 'h']][i % 6]
    s = voz_sintetica(dur, lambda t: base * (1 + (sobe - 1) * np.sin(np.pi * np.clip(t / dur * 1.2, 0, 1)) ** 1.5), vogais, rng,
                      fry=rng.uniform(0.35, 0.6), sopro=0.35, escala=0.92)
    n = len(s)
    real = oco(tom(trecho(i % 4), -rng.uniform(3, 5)), 0.6)
    a = assovio(dur, lambda t: rng.uniform(520, 640) * (1 - 0.18 * t / dur), rng)
    y = mixar((oco(s, 0.55) * envelope(n, 0.25, 0.6), 0.75), (caber(real, n) * envelope(n, 0.05, 0.4), 0.55), (a * envelope(n, 0.6, 0.7) , 0.12))
    # o fim: uma inspiração rouca (o ar entrando no oco)
    m = int(0.45 * SR)
    ins = passa_banda(rng.standard_normal(m).astype(np.float32), 800, 3500) * np.sin(np.linspace(0, np.pi, m)) ** 2
    ins *= 0.5 + 0.5 * np.sign(np.sin(2 * np.pi * 22 * t_de(m)))   # chocalhando
    y = np.concatenate([y, ins * 0.18])
    salvar(f'Oco_Voz_{i + 1:02d}', saturar(y / (np.abs(y).max() + 1e-9), 1.6))


# ---------------------------------------------------------------------------------------------- o golpe (ao atacar)
def golpe(i):
    rng = np.random.default_rng(200 + i)
    dur = rng.uniform(0.45, 0.65)
    f = rng.uniform(130, 165)
    s = voz_sintetica(dur, lambda t: f * (1.25 - 0.45 * t / dur), ['a', 'h'], rng, fry=0.55, sopro=0.5, escala=1.05, forca=5)
    n = len(s)
    real = tom(trecho([0, 3, 1][i % 3])[int(0.15 * SR):], rng.uniform(0, 2))
    y = mixar((oco(s, 0.35) * envelope(n, 0.015, 0.25, curva=1.5), 0.85), (caber(real, n) * envelope(n, 0.01, 0.2), 0.5))
    salvar(f'Oco_Golpe_{i + 1:02d}', saturar(y / (np.abs(y).max() + 1e-9), 2.5))


# ---------------------------------------------------------------------------------------------- o chamado e a resposta
def chamado(i):
    rng = np.random.default_rng(300 + i)
    dur = rng.uniform(1.9, 2.5)
    n = int(dur * SR)
    # estalos que aceleram, depois o lamento oco que sobe e cai, e estalos no fim
    inicio = np.cumsum(np.geomspace(0.11, 0.045, 8))
    fim = dur - 0.35 + np.cumsum(rng.uniform(0.05, 0.08, 4))
    k = estalos(np.concatenate([inicio - inicio[0] + 0.02, fim]), rng, n)
    comeco = inicio[-1] + 0.05
    resto = dur - comeco
    pico = rng.uniform(1.6, 1.9)
    s = voz_sintetica(resto, lambda t: 92 * (1 + (pico - 1) * np.sin(np.pi * np.clip(t / resto, 0, 1)) ** 0.8), ['u', 'o', 'u'], rng,
                      fry=0.25, sopro=0.45, escala=0.95, forca=2)
    a = assovio(resto, lambda t: 610 * (1 + (pico - 1) * 0.55 * np.sin(np.pi * np.clip(t / resto, 0, 1))), rng, aspero=0.35)
    m = len(s)
    lamento = mixar((oco(s, 0.7, g=0.7) * envelope(m, 0.25, 0.5), 0.7), (a * envelope(m, 0.3, 0.5), 0.45))
    y = np.zeros(n, dtype=np.float32)
    j = int(comeco * SR)
    y[j:j + m] += lamento[:n - j]
    y += k * 0.55
    salvar(f'Oco_Chamado_{i + 1:02d}', saturar(y / (np.abs(y).max() + 1e-9), 1.4))


def resposta(i):
    rng = np.random.default_rng(400 + i)
    dur = rng.uniform(0.9, 1.3)
    n = int(dur * SR)
    q = rng.integers(3, 6)
    k = estalos(0.02 + np.cumsum(rng.uniform(0.06, 0.1, q)), rng, n)
    comeco = 0.02 + 0.09 * q
    resto = dur - comeco
    s = voz_sintetica(resto, lambda t: rng.uniform(78, 95) * (1 + 0.25 * np.sin(np.pi * np.clip(t / resto, 0, 1))), ['o', 'u'], rng, fry=0.45, sopro=0.4)
    a = assovio(resto, lambda t: 560 * (1 - 0.1 * t / resto), rng, aspero=0.4)
    m = len(s)
    lamento = mixar((oco(s, 0.65, g=0.65) * envelope(m, 0.12, 0.35), 0.75), (a * envelope(m, 0.2, 0.3), 0.3))
    y = np.zeros(n, dtype=np.float32)
    j = int(comeco * SR)
    y[j:j + m] += lamento[:n - j]
    y += k * 0.6
    salvar(f'Oco_Resposta_{i + 1:02d}', y)


# ---------------------------------------------------------------------------------------------- a agonia no sol
def agonia(i):
    rng = np.random.default_rng(500 + i)
    dur = rng.uniform(1.7, 2.6)
    f = rng.uniform(200, 290)
    vib = rng.uniform(5, 7)
    # grito esganiçado, com o vibrato largo do desespero e quebras (soluços)
    s = voz_sintetica(dur, lambda t: f * (1 + 0.09 * np.sin(2 * np.pi * vib * t)) * (1 - 0.25 * t / dur), [['a', 'e'], ['e', 'a', 'h'], ['a', 'h'], ['h', 'e']][i % 4],
                      rng, fry=0.3, sopro=0.7, escala=1.12, jitter=0.06, forca=4)
    n = len(s)
    quebras = 0.55 + 0.45 * np.clip(np.sin(2 * np.pi * rng.uniform(2.2, 3.2) * t_de(n) + rng.uniform(0, 6)) * 2.5, -1, 1)
    real = tom(trecho([2, 3, 0, 1][i % 4]), rng.uniform(4, 6.5))
    real = real * (0.6 + 0.4 * np.sin(2 * np.pi * vib * t_de(len(real))))
    y = mixar((oco(s, 0.3) * quebras * envelope(n, 0.06, 0.5), 0.8), (caber(real, n) * envelope(n, 0.03, 0.4), 0.5), (chiado(dur, rng) * envelope(n, 0.3, 0.4), 0.22))
    salvar(f'Oco_Agonia_{i + 1:02d}', saturar(y / (np.abs(y).max() + 1e-9), 2.2))


# ---------------------------------------------------------------------------------------------- o tiro que leva (a dor)
def tiro(i):
    rng = np.random.default_rng(600 + i)
    dur = rng.uniform(0.3, 0.5)
    f = rng.uniform(150, 210)
    s = voz_sintetica(dur, lambda t: f * (1.35 - 0.6 * t / dur), [['h', 'a'], ['a', 'o'], ['e', 'h'], ['a', 'u']][i % 4], rng, fry=0.5, sopro=0.55, escala=1.05, forca=4)
    n = len(s)
    real = tom(trecho(i % 4)[int(rng.uniform(0.1, 0.3) * SR):], rng.uniform(1, 4))
    y = mixar((oco(s, 0.3) * envelope(n, 0.008, 0.2, curva=1.3), 0.8), (caber(real, n) * envelope(n, 0.005, 0.15), 0.45))
    salvar(f'Oco_Tiro_{i + 1:02d}', saturar(y / (np.abs(y).max() + 1e-9), 3))


# ---------------------------------------------------------------------------------------------- a pedra
def graos(n, rng, densidade, f1, f2, decai=0.004, forca=(0.2, 1.0)):
    y = np.zeros(n, dtype=np.float32)
    k = int(n / SR * densidade)
    for _ in range(k):
        i = rng.integers(0, n)
        m = min(n - i, int(decai * 8 * SR))
        tl = t_de(m)
        fq = rng.uniform(f1, f2)
        g = np.sin(2 * np.pi * fq * tl + rng.uniform(0, 6)) * np.exp(-tl / (decai * rng.uniform(0.5, 1.5)))
        g += 0.5 * rng.standard_normal(m) * np.exp(-tl / (decai * 0.4))
        y[i:i + m] += rng.uniform(*forca) * g.astype(np.float32)
    return y


def racha(n, rng, t0, forca=1.0):
    """Uma rachadura grande: um estalo seco, o ruído que se abre e um baque grave."""
    y = np.zeros(n, dtype=np.float32)
    i = int(t0 * SR)
    m = min(n - i, int(0.25 * SR))
    tl = t_de(m)
    r = passa_banda(rng.standard_normal(m).astype(np.float32), 900, 7000) * np.exp(-tl / 0.03)
    r[:int(0.002 * SR)] += 1.5 * rng.standard_normal(int(0.002 * SR))
    baque = np.sin(2 * np.pi * rng.uniform(70, 110) * tl) * np.exp(-tl / 0.05)
    y[i:i + m] += forca * (r + 0.7 * baque).astype(np.float32)
    return y


def petrificando():
    """O laço enquanto a pedra sobe: a crosta estalando (miúdo), um ranger grave e uma rachadura de vez em quando."""
    rng = np.random.default_rng(700)
    dur = 3.0
    n = int(dur * SR)
    miudo = graos(n, rng, 70, 1500, 6500, 0.003, (0.1, 0.6))
    ranger = passa_baixa(np.cumsum(rng.standard_normal(n)).astype(np.float32), 220)
    ranger = passa_alta(ranger, 40) * (0.5 + 0.5 * suave(n, rng, 3))
    ranger /= np.abs(ranger).max() + 1e-9
    y = miudo / (np.abs(miudo).max() + 1e-9) + 0.45 * ranger
    for t0 in (0.4, 1.35, 2.2):
        y += 0.55 * racha(n, rng, t0 + rng.uniform(-0.1, 0.1), 0.8)
    # o laço sem emenda: o fim cruza com o começo
    m = int(0.25 * SR)
    y[:m] = y[:m] * np.linspace(0, 1, m) + y[-m:] * np.linspace(1, 0, m)
    y = y[:-m]
    x = y - np.mean(y)
    x = saturar(x / (np.abs(x).max() + 1e-9), 3.0)
    x = x / (np.abs(x).max() + 1e-9) * 0.85
    wf.write(os.path.join(SAIDA, 'Oco_Petrificando.wav'), SR, (x * 32767).astype(np.int16))
    print(f'Oco_Petrificando: {len(x) / SR:.2f} s (laço)')


def petrificou():
    """A pedra fecha: uma rachadura forte, o baque, e os farelos assentando."""
    rng = np.random.default_rng(710)
    n = int(1.3 * SR)
    y = racha(n, rng, 0.0, 1.0) + 0.6 * racha(n, rng, 0.07, 0.6)
    farelo = graos(n, rng, 45, 2000, 7000, 0.004, (0.05, 0.4)) * np.exp(-t_de(n) / 0.45)
    y += 0.6 * farelo / (np.abs(farelo).max() + 1e-9)
    salvar('Oco_Petrificou', saturar(y / (np.abs(y).max() + 1e-9), 3.5), cauda=0.1)


def soltando():
    """À noite, a casca quebrando quando ele se solta: várias rachaduras, a casca caindo em pedaços."""
    rng = np.random.default_rng(720)
    n = int(1.6 * SR)
    y = np.zeros(n, dtype=np.float32)
    for t0 in np.cumsum(rng.uniform(0.05, 0.18, 6)):
        y += rng.uniform(0.5, 1.0) * racha(n, rng, t0, 1.0)
    pedacos = graos(n, rng, 35, 600, 3500, 0.012, (0.2, 0.9)) * np.clip(np.exp(-(t_de(n) - 0.5) / 0.4), 0, 1)
    y += 0.7 * pedacos / (np.abs(pedacos).max() + 1e-9)
    salvar('Oco_Soltando', saturar(y / (np.abs(y).max() + 1e-9), 3.5), cauda=0.12)


# desde 29/10/2026 a voz, o golpe, a agonia e a dor do tiro saem das gravações do Pedro (oco_gravado.py): estes só
# com "tudo" no fim do comando (python sons_do_oco.py voz.wav pasta tudo), para não apagar os gravados
if len(sys.argv) > 3 and sys.argv[3] == 'tudo':
    for i in range(6): voz(i)
    for i in range(3): golpe(i)
    for i in range(4): agonia(i)
    for i in range(4): tiro(i)
for i in range(3): chamado(i)
for i in range(3): resposta(i)
petrificando()
petrificou()
soltando()
