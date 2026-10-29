# Os alarmes dos carros abandonados (a pedido em 29/10/2026): a sirene clássica de carro, um ciclo de seis toques (a
# subida, os dois tons, o trinado, o ioiô, a metralhadora e o lamento), com o timbre da buzininha piezo (onda quadrada
# arredondada, só os médios, um pouco saturada). Três alarmes, cada um com a sua ordem, o seu tom e o seu timbre; o
# jogo toca em laço (até 40 s ou até tirarem a bateria).
# Uso: python alarmes.py [pasta]   (sem pasta: Assets/_Game/Audio/Carro)
import os
import sys
import numpy as np
import scipy.signal as sg
import scipy.io.wavfile as wf

SR = 44100
AQUI = os.path.dirname(os.path.abspath(__file__))
PASTA = sys.argv[1] if len(sys.argv) > 1 else os.path.join(AQUI, '..', 'Assets', '_Game', 'Audio', 'Carro')


def onda(freq, quadrada=0.85):
    """A buzina: a fase da frequência (que muda no tempo), uma quadrada arredondada misturada a um pouco de serra."""
    fase = np.cumsum(freq) / SR
    q = np.tanh(np.sin(2 * np.pi * fase) * 6.0)
    s = 2 * (fase % 1.0) - 1
    return quadrada * q + (1 - quadrada) * s


def tempo(d):
    return np.arange(int(d * SR)) / SR


def subida(d, f0, f1, vezes):
    t = tempo(d / vezes)
    u = t / t[-1]
    f = f0 * (f1 / f0) ** (u ** 0.8)
    return np.tile(f, vezes)


def dois_tons(d, fa, fb, passo):
    t = tempo(d)
    return np.where((t // passo) % 2 == 0, fa, fb)


def trinado(d, f, prof, vel):
    t = tempo(d)
    return f * (1 + prof * np.sign(np.sin(2 * np.pi * vel * t)) * 0.5 + prof * 0.5 * np.sin(2 * np.pi * vel * t))


def ioio(d, f0, f1, periodo):
    t = tempo(d)
    u = np.abs(((t / periodo) % 1.0) * 2 - 1)
    return f0 + (f1 - f0) * (1 - u)


def lamento(d, f0, f1):
    t = tempo(d)
    u = 0.5 - 0.5 * np.cos(2 * np.pi * t / d)
    return f0 + (f1 - f0) * u


def metralhadora(d, f, liga):
    t = tempo(d)
    return np.full(len(t), f), ((t // liga) % 2 == 0).astype(float)


def alarme(semente):
    r = np.random.default_rng(semente)
    k = [1.0, 0.92, 1.08][semente % 3]          # o tom de cada alarme
    quadrada = [0.85, 0.7, 0.95][semente % 3]   # o timbre
    toques = {
        'subida': lambda: (subida(3.2, 520 * k, 1650 * k, 4), None),
        'dois_tons': lambda: (dois_tons(3.0, 1050 * k, 760 * k, 0.28), None),
        'trinado': lambda: (trinado(3.0, 1250 * k, 0.28, 13.0), None),
        'ioio': lambda: (ioio(3.2, 700 * k, 1700 * k, 0.42), None),
        'metralhadora': lambda: metralhadora(2.6, 1150 * k, 0.055),
        'lamento': lambda: (lamento(3.4, 480 * k, 1550 * k), None),
    }
    ordem = [['subida', 'dois_tons', 'trinado', 'ioio', 'metralhadora', 'lamento'],
             ['dois_tons', 'ioio', 'metralhadora', 'subida', 'lamento', 'trinado'],
             ['ioio', 'subida', 'lamento', 'dois_tons', 'trinado', 'metralhadora']][semente % 3]
    pedacos = []
    for nome in ordem:
        f, liga = toques[nome]()
        x = onda(f, quadrada)
        if liga is not None:
            x = x * sg.lfilter([0.1], [1, -0.9], liga)   # o liga-desliga sem estalar
        # cada toque entra e sai sem estalo
        m = int(0.004 * SR)
        x[:m] *= np.linspace(0, 1, m)
        x[-m:] *= np.linspace(1, 0, m)
        pedacos.append(x)
        pedacos.append(np.zeros(int(0.06 * SR)))
    y = np.concatenate(pedacos)
    # a buzininha: só os médios, a ressonância da corneta e um pouco de saturação
    y = sg.sosfilt(sg.butter(2, [550, 5200], btype='band', fs=SR, output='sos'), y)
    b, a = sg.iirpeak(2600 * k, 3, fs=SR)
    y = y + 0.6 * sg.lfilter(b, a, y)
    y = np.tanh(y * 1.8) / np.tanh(1.8)
    # um pouco de chiado do alto-falante velho
    y += sg.sosfilt(sg.butter(2, [2000, 7000], btype='band', fs=SR, output='sos'), r.standard_normal(len(y))) * 0.01
    return y / np.abs(y).max() * 0.7   # -3 dB


if __name__ == '__main__':
    os.makedirs(PASTA, exist_ok=True)
    for s in range(3):
        y = alarme(s)
        caminho = os.path.join(PASTA, f'Alarme_{s + 1:02d}.wav')
        wf.write(caminho, SR, (y * 32767).astype(np.int16))
        print(f'{caminho}: {len(y) / SR:.1f} s')
