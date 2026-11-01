# Os sons provisórios dos zumbis especiais (01/11/2026, a pedido: "os sons, por hora faz você mesmo; depois eu vou
# ajustando"), sintetizados aqui: Assets/_Game/Audio/Resources/Especiais/<nome>.wav (o ZombieManager carrega pelo nome).
#   Brutamonte_Terra   o chão abrindo quando ele brota (o ronco grave e as rachaduras)
#   Brutamonte_Rugido  o rugido curto quando o olho trava num jogador
#   Brutamonte_Furia   o urro da fúria
#   Brutamonte_Baque   o baque do avanço e das braçadas
#   Long_Grito         o grito que chama a horda (agudo, rasgado, com eco)
#   Long_Cuspe         o gorgolejo do papo e o cuspe
#   Long_Agonia        a agonia na luz (virando pedra)
#   Gosma_Splash       a gosma caindo no chão
# Para trocar por um som gravado: um .wav com o mesmo nome na mesma pasta.
#
# Rodar: py Ferramentas/especiais_sons.py

import os

import numpy as np
from scipy import signal
from scipy.io import wavfile

AQUI = os.path.dirname(os.path.abspath(__file__))
SAIDA = os.path.join(os.path.dirname(AQUI), "Assets", "_Game", "Audio", "Resources", "Especiais")
TAXA = 44100
rng = np.random.default_rng(7)


def t(dur):
    return np.arange(int(dur * TAXA)) / TAXA


def ruido(n):
    return rng.standard_normal(n)


def passa_baixa(x, f, ordem=4):
    b, a = signal.butter(ordem, f / (TAXA / 2), "low")
    return signal.lfilter(b, a, x)


def passa_alta(x, f, ordem=2):
    b, a = signal.butter(ordem, f / (TAXA / 2), "high")
    return signal.lfilter(b, a, x)


def banda(x, f, q=4.0):
    b, a = signal.iirpeak(f / (TAXA / 2), q)
    return signal.lfilter(b, a, x)


def envelope(n, ataque, queda, sustenta=1.0):
    e = np.ones(n) * sustenta
    na = max(1, int(ataque * TAXA))
    nq = max(1, int(queda * TAXA))
    e[:na] = np.linspace(0, sustenta, na)
    e[-nq:] *= np.linspace(1, 0, nq) ** 1.5
    return e


def oscilador(freq, forma="serra"):
    fase = np.cumsum(freq) / TAXA
    if forma == "serra":
        return 2 * (fase % 1.0) - 1
    if forma == "quadrada":
        return np.sign(np.sin(2 * np.pi * fase))
    return np.sin(2 * np.pi * fase)


def eco(x, atrasos=(0.11, 0.23, 0.37), ganhos=(0.35, 0.22, 0.12)):
    y = np.concatenate([x, np.zeros(int(0.6 * TAXA))])
    for a, g in zip(atrasos, ganhos):
        d = int(a * TAXA)
        y[d:d + len(x)] += x * g
    return y


def saturar(x, k=2.0):
    return np.tanh(k * x) / np.tanh(k)


def gravar(nome, x, volume=0.9):
    x = x - np.mean(x)
    x = x / (np.max(np.abs(x)) + 1e-9) * volume
    os.makedirs(SAIDA, exist_ok=True)
    wavfile.write(os.path.join(SAIDA, nome + ".wav"), TAXA, (x * 32767).astype(np.int16))
    print("som:", nome, f"{len(x) / TAXA:.2f} s")


# ------------------------------------------------------------------ o Brutamonte

def brutamonte_terra():
    d = 3.8
    n = len(t(d))
    ronco = passa_baixa(np.cumsum(ruido(n)) * 0.02, 110)
    ronco *= envelope(n, 1.2, 1.0) * (0.7 + 0.3 * np.sin(2 * np.pi * 7 * t(d)))
    # as rachaduras: estalos filtrados, mais no fim (o chão cedendo)
    estalos = np.zeros(n)
    for _ in range(26):
        p = int(rng.uniform(0.6, 3.3) * TAXA)
        m = int(rng.uniform(0.02, 0.09) * TAXA)
        if p + m >= n:
            continue
        estalos[p:p + m] += ruido(m) * np.exp(-np.linspace(0, 6, m)) * rng.uniform(0.3, 1.0)
    estalos = banda(estalos, 1600, 1.2) + 0.5 * banda(estalos, 400, 1.5)
    pedras = passa_baixa(ruido(n), 2500) * envelope(n, 2.2, 0.8) * 0.15
    gravar("Brutamonte_Terra", 1.4 * ronco + 0.6 * estalos + pedras)


def rugido(d, f0, subida, aspereza, nome, volume=0.95):
    tt = t(d)
    n = len(tt)
    # o tom sobe e desce, com tremor
    curva = f0 * (1 + subida * np.sin(np.pi * np.clip(tt / d, 0, 1)) ** 1.5) * (1 + 0.03 * np.sin(2 * np.pi * 5.5 * tt))
    voz = oscilador(curva, "serra") + 0.5 * oscilador(curva * 0.5, "serra")
    voz *= 1 + aspereza * np.sin(2 * np.pi * 31 * tt)   # a garganta rasgando
    formantes = banda(voz, 320, 3) + 0.8 * banda(voz, 720, 4) + 0.4 * banda(voz, 1500, 5)
    sopro = banda(ruido(n), 600, 1.0) * 0.5
    x = saturar((formantes + sopro) * envelope(n, 0.15, d * 0.45), 2.5)
    x += 0.6 * passa_baixa(voz, 150) * envelope(n, 0.1, d * 0.5)
    gravar(nome, eco(x, (0.09, 0.19), (0.25, 0.12)), volume)


def brutamonte_baque():
    d = 0.7
    tt = t(d)
    n = len(tt)
    f = 70 * np.exp(-tt * 9) + 32
    corpo = np.sin(2 * np.pi * np.cumsum(f) / TAXA) * np.exp(-tt * 7)
    poeira = passa_baixa(ruido(n), 900) * np.exp(-tt * 16) * 0.6
    gravar("Brutamonte_Baque", saturar(corpo + poeira, 1.8))


# ------------------------------------------------------------------ o Long

def long_grito():
    d = 2.8
    tt = t(d)
    n = len(tt)
    # dois gritos sobrepostos, um pouco fora do tom, subindo e quebrando no fim
    curva = 820 * (1 + 0.45 * np.clip(tt / 0.5, 0, 1)) * (1 - 0.18 * np.clip((tt - 1.8) / 1.0, 0, 1))
    curva *= 1 + 0.025 * np.sin(2 * np.pi * 7.5 * tt) + 0.01 * ruido(n).cumsum() / np.sqrt(n)
    a = oscilador(curva, "serra")
    b = oscilador(curva * 1.33, "quadrada") * 0.4
    c = oscilador(curva * 0.5, "serra") * 0.5
    voz = (a + b + c) * (1 + 0.5 * np.sin(2 * np.pi * 47 * tt))   # o chiado rasgado
    formantes = banda(voz, 1100, 3) + banda(voz, 2400, 4) * 0.7 + banda(voz, 3600, 6) * 0.4
    sopro = passa_alta(ruido(n), 1500) * 0.35
    x = saturar((formantes + sopro) * envelope(n, 0.08, 1.0), 3.0)
    gravar("Long_Grito", eco(x, (0.13, 0.29, 0.47), (0.4, 0.25, 0.15)))


def long_cuspe():
    d = 0.9
    tt = t(d)
    n = len(tt)
    # o gorgolejo (bolhas no papo) e o "ptuh"
    bolhas = banda(ruido(n), 260, 2) * (0.5 + 0.5 * np.sign(np.sin(2 * np.pi * 19 * tt))) * envelope(n, 0.05, 0.3) * (tt < 0.55)
    p = int(0.5 * TAXA)
    cuspe = np.zeros(n)
    m = int(0.18 * TAXA)
    cuspe[p:p + m] = banda(ruido(m), 1400, 1.5) * np.exp(-np.linspace(0, 8, m))
    gravar("Long_Cuspe", 1.2 * bolhas + 1.5 * cuspe)


def long_agonia():
    d = 1.8
    tt = t(d)
    n = len(tt)
    curva = 620 * np.exp(-tt * 0.9) + 150
    voz = oscilador(curva, "serra") * (1 + 0.6 * np.sin(2 * np.pi * 23 * tt))
    voz = banda(voz, 900, 3) + 0.6 * banda(voz, 2000, 5)
    estalos = np.zeros(n)
    for _ in range(18):
        q = int(rng.uniform(0.2, 1.6) * TAXA)
        m = int(0.03 * TAXA)
        if q + m < n:
            estalos[q:q + m] += ruido(m) * np.exp(-np.linspace(0, 7, m))
    estalos = banda(estalos, 2800, 1.5)
    gravar("Long_Agonia", eco(saturar(voz * envelope(n, 0.05, 1.0), 2.2) + 0.5 * estalos))


def gosma_splash():
    d = 0.8
    tt = t(d)
    n = len(tt)
    f = 1800 * np.exp(-tt * 6) + 250
    splat = np.zeros(n)
    m = int(0.25 * TAXA)
    rb = ruido(m)
    for k in range(0, m, 256):
        seg = rb[k:k + 256]
        splat[k:k + len(seg)] = banda(seg, f[k], 1.2)
    splat *= np.exp(-tt * 9)
    # os pingos
    pingos = np.zeros(n)
    for _ in range(5):
        q = int(rng.uniform(0.25, 0.7) * TAXA)
        m2 = int(0.05 * TAXA)
        if q + m2 < n:
            fq = rng.uniform(500, 900)
            tp = np.arange(m2) / TAXA
            pingos[q:q + m2] += np.sin(2 * np.pi * (fq + 3000 * tp) * tp) * np.exp(-tp * 60) * 0.5
    gravar("Gosma_Splash", splat * 2 + pingos)


if __name__ == "__main__":
    brutamonte_terra()
    rugido(1.6, 62, 0.35, 0.5, "Brutamonte_Rugido")
    rugido(2.2, 70, 0.7, 0.7, "Brutamonte_Furia", 1.0)
    brutamonte_baque()
    long_grito()
    long_cuspe()
    long_agonia()
    gosma_splash()
