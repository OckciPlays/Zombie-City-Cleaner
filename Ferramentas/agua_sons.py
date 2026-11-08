# Os sons provisórios da água (07/11/2026, a pedido: "atualizar a parte de nadar"), sintetizados aqui:
# Assets/_Game/Audio/Resources/Agua/<nome>.wav (o Player/NadoNoCorpo carrega pelo nome).
#   Agua_Entrar      o tchibum de quem entra na água funda (o baque, o respingo e as bolhas)
#   Agua_Bracada_0..3  uma braçada (a água empurrada e o chiado da volta), quatro variações
#   Agua_Respirar    a puxada de ar de quem sobe depois de um tempo embaixo
#   Agua_Sair        a água escorrendo e pingando de quem sai do fundo
# Para trocar por um som gravado: um .wav com o mesmo nome na mesma pasta.
#
# Rodar: py Ferramentas/agua_sons.py

import os

import numpy as np
from scipy import signal
from scipy.io import wavfile

AQUI = os.path.dirname(os.path.abspath(__file__))
SAIDA = os.path.join(os.path.dirname(AQUI), "Assets", "_Game", "Audio", "Resources", "Agua")
TAXA = 44100
rng = np.random.default_rng(11)


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


def passa_banda(x, f1, f2, ordem=2):
    b, a = signal.butter(ordem, [f1 / (TAXA / 2), f2 / (TAXA / 2)], "band")
    return signal.lfilter(b, a, x)


def envelope(n, ataque, queda):
    """Sobe em 'ataque' s e cai exponencial com a constante 'queda' s."""
    tt = np.arange(n) / TAXA
    sobe = np.clip(tt / max(ataque, 1e-4), 0, 1)
    return sobe * np.exp(-np.maximum(tt - ataque, 0) / queda)


def bolha(n, em, freq, dur=0.05, vol=0.3):
    """Uma bolha: um 'plop' de frequência subindo, curtinho."""
    x = np.zeros(n)
    i0 = int(em * TAXA)
    m = int(dur * TAXA)
    if i0 >= n:
        return x
    m = min(m, n - i0)
    tt = np.arange(m) / TAXA
    f = freq * (1 + 2.2 * tt / dur)
    fase = 2 * np.pi * np.cumsum(f) / TAXA
    x[i0:i0 + m] = np.sin(fase) * np.exp(-tt / (dur * 0.35)) * vol
    return x


def normalizar(x, pico=0.85):
    x = x - np.mean(x)
    m = np.max(np.abs(x))
    if m > 0:
        x = x / m * pico
    # as pontas sem estalo
    f = int(0.004 * TAXA)
    x[:f] *= np.linspace(0, 1, f)
    x[-f:] *= np.linspace(1, 0, f)
    return x


def salvar(nome, x):
    os.makedirs(SAIDA, exist_ok=True)
    wavfile.write(os.path.join(SAIDA, nome + ".wav"), TAXA, (normalizar(x) * 32767).astype(np.int16))
    print("ok", nome)


def entrar():
    dur = 1.3
    n = len(t(dur))
    # o baque grave do corpo batendo na água
    baque = passa_baixa(ruido(n), 260) * envelope(n, 0.004, 0.09) * 2.2
    # o respingo: o chiado claro que sobe e cai
    respingo = passa_banda(ruido(n), 900, 6500) * envelope(n, 0.012, 0.22) * 0.9
    # a água voltando (gotas caindo de novo), um chiado mais fraco depois
    volta = passa_banda(ruido(n), 1500, 7000) * envelope(n, 0.25, 0.3) * 0.25
    volta[: int(0.18 * TAXA)] = 0
    # as bolhas
    bolhas = np.zeros(n)
    for _ in range(26):
        bolhas += bolha(n, rng.uniform(0.03, 0.9), rng.uniform(380, 1300), rng.uniform(0.025, 0.07), rng.uniform(0.05, 0.22))
    salvar("Agua_Entrar", baque + respingo + volta + bolhas)


def bracada(k):
    dur = 0.62
    n = len(t(dur))
    tt = np.arange(n) / TAXA
    # a água empurrada: um "chuá" que cresce e some (o meio da braçada)
    pico = rng.uniform(0.16, 0.24)
    forma = np.exp(-((tt - pico) / 0.09) ** 2)
    chua = passa_banda(ruido(n), 350, 2600) * forma * 0.9
    # o chiado da volta, mais claro e mais fraco
    forma2 = np.exp(-((tt - (pico + 0.22)) / 0.08) ** 2)
    chiado = passa_banda(ruido(n), 1800, 7000) * forma2 * 0.35
    bolhas = np.zeros(n)
    for _ in range(rng.integers(3, 7)):
        bolhas += bolha(n, rng.uniform(0.1, 0.5), rng.uniform(500, 1400), rng.uniform(0.02, 0.05), rng.uniform(0.04, 0.12))
    salvar(f"Agua_Bracada_{k}", chua + chiado + bolhas)


def respirar():
    dur = 0.75
    n = len(t(dur))
    tt = np.arange(n) / TAXA
    # a puxada de ar: ruído filtrado com um "formante" que sobe (o "háá" engasgado), rápido no começo
    forma = envelope(n, 0.03, 0.22)
    centro = 900 + 900 * np.clip(tt / 0.3, 0, 1)
    sopro = np.zeros(n)
    bloco = 512
    r = ruido(n)
    for i in range(0, n, bloco):
        c = centro[min(i, n - 1)]
        pedaco = passa_banda(r[max(0, i - 2048): i + bloco], c * 0.6, c * 1.8)[-min(bloco, n - i):]
        sopro[i:i + len(pedaco)] = pedaco
    sopro *= forma
    # a água escorrendo do rosto, de leve
    agua = passa_banda(ruido(n), 2000, 7000) * envelope(n, 0.01, 0.35) * 0.2
    salvar("Agua_Respirar", sopro + agua)


def sair():
    dur = 1.4
    n = len(t(dur))
    escorre = passa_banda(ruido(n), 1200, 6000) * envelope(n, 0.02, 0.45) * 0.6
    gotas = np.zeros(n)
    for _ in range(40):
        gotas += bolha(n, rng.uniform(0.05, 1.3), rng.uniform(900, 2400), rng.uniform(0.012, 0.03), rng.uniform(0.04, 0.16))
    salvar("Agua_Sair", escorre + gotas)


if __name__ == "__main__":
    entrar()
    for k in range(4):
        bracada(k)
    respirar()
    sair()
