# O som da TV ligando na abertura do menu (a pedido em 29/10/2026: "como se fosse ligando realmente a TV - e depois já
# começaria a trilha"). Uma TV de tubo antiga, no tempo da imagem do UI/FundoDoMenu (o arquivo começa quando a tela
# preta começa; o ponto claro aparece aos 0,6 s, a linha se abre aos 0,95 s e o chiado some até ~2,5 s):
#  - o botão: o aperto seco e o encaixe de plástico;
#  - o relé dentro da TV, um tique;
#  - a desmagnetização (o "bum" da bobina, 60 Hz com as harmônicas, morrendo em ~1 s, com o metal da máscara vibrando);
#  - o estalo da alta tensão quando o ponto acende, os estalinhos da estática carregando o vidro e o assobio fino do
#    transformador (15.734 Hz, bem baixo);
#  - o "vuuush" do tubo acordando e o chiado (a neve) enquanto a imagem abre, sumindo junto com o chiado da tela;
#  - um zumbido de 60 Hz por baixo, que some no fim.
# A trilha do menu entra depois (o FundoDoMenu a começa aos 1,5 s do ponto, subindo do silêncio).
# Uso: python tv_ligando.py [pasta] [prévia]   (sem pasta: Assets/_Game/Audio/Menu/Resources; com "prévia" e o caminho
# de um .mp3, grava também o som seguido da trilha do menu entrando, como no jogo)
import os
import sys
import subprocess
import tempfile
import numpy as np
import scipy.signal as sg
import scipy.io.wavfile as wf

SR = 44100
AQUI = os.path.dirname(os.path.abspath(__file__))
PASTA = sys.argv[1] if len(sys.argv) > 1 else os.path.join(AQUI, '..', 'Assets', '_Game', 'Audio', 'Menu', 'Resources')
DURACAO = 3.4
# o tempo da imagem (UI/FundoDoMenu: Preto, Linha, Abrir, Aquecer)
PONTO = 0.6
ABRE = PONTO + 0.35
CHIADO_FIM = PONTO + 0.35 + 0.5 + 1.8 * 0.6
# o som
BOTAO = 0.24
RELE = 0.43
DEGAUSS = 0.47
# a trilha do menu (no jogo: a partir de 1,5 s do ponto, ao quadrado, em 4 s)
MUSICA_ENTRA = PONTO + 1.5
MUSICA_SOBE = 4.0

N = int(SR * DURACAO)
T = np.arange(N) / SR
rng = np.random.default_rng(29)


def ruido():
    return rng.standard_normal(N)


def filtro(x, tipo, f, ordem=4):
    sos = sg.butter(ordem, f, btype=tipo, fs=SR, output='sos')
    return sg.sosfilt(sos, x)


def queda(t0, tau, ataque=0.0015):
    """Um envelope que começa em t0, sobe em 'ataque' e cai com a constante 'tau'."""
    dt = T - t0
    e = (1 - np.exp(-np.clip(dt, 0, None) / ataque)) * np.exp(-np.clip(dt, 0, None) / tau)
    return np.where(dt >= 0, e, 0.0)


def ressoa(freq, tau, t0):
    dt = np.clip(T - t0, 0, None)
    return np.where(T >= t0, np.sin(2 * np.pi * freq * dt), 0.0) * queda(t0, tau, 0.0008)


def rampa(t0, t1):
    """0 antes de t0, 1 depois de t1, suave no meio."""
    u = np.clip((T - t0) / (t1 - t0), 0, 1)
    return u * u * (3 - 2 * u)


def botao():
    s = filtro(ruido(), 'highpass', 2500) * queda(BOTAO, 0.004) * 0.55       # o aperto
    s += ressoa(3400, 0.012, BOTAO) * 0.22
    t1 = BOTAO + 0.024                                                       # o encaixe: mais grave, o plástico
    s += filtro(ruido(), 'lowpass', 1800) * queda(t1, 0.018) * 0.7
    s += ressoa(165, 0.035, t1) * 0.32 + ressoa(870, 0.022, t1) * 0.28 + ressoa(2300, 0.01, t1) * 0.12
    return s


def rele():
    s = filtro(ruido(), 'bandpass', [1200, 6000]) * queda(RELE, 0.003) * 0.25
    s += ressoa(2600, 0.006, RELE) * 0.1
    return s


def degauss():
    dt = np.clip(T - DEGAUSS, 0, None)
    env = np.where(T >= DEGAUSS, (1 - np.exp(-dt / 0.015)) * np.exp(-dt / 0.42), 0.0)
    f = 60.0 * (1 + 0.012 * np.sin(2 * np.pi * 4.5 * dt))   # a bobina balança um pouco
    fase = 2 * np.pi * np.cumsum(f) / SR
    zumbido = sum(a * np.sin(k * fase) for k, a in [(1, 1.0), (2, 0.7), (3, 0.5), (4, 0.3), (5, 0.22), (7, 0.12), (9, 0.06)])
    zumbido = np.tanh(1.8 * zumbido) * env
    corpo = filtro(ruido(), 'lowpass', 300) * queda(DEGAUSS, 0.08, 0.005) * 0.5   # o "bum" da caixa
    metal = ressoa(412, 0.32, DEGAUSS) * 0.22 + ressoa(1135, 0.24, DEGAUSS + 0.004) * 0.12 + ressoa(2290, 0.15, DEGAUSS + 0.008) * 0.06
    return zumbido * 0.42 + corpo + metal


def alta_tensao():
    s = filtro(ruido(), 'highpass', 1500) * queda(PONTO, 0.014) * 0.5       # o estalo do ponto acendendo
    s += filtro(ruido(), 'lowpass', 2500) * queda(PONTO + 0.003, 0.06) * 0.18  # o "pff"
    # os estalinhos da estática no vidro, cada vez mais raros
    e = np.zeros((2, N))
    t = PONTO + 0.05
    while t < ABRE + 0.9:
        prog = (t - PONTO) / (ABRE + 0.9 - PONTO)
        t += rng.exponential(1 / (45 * (1 - prog) + 4))
        i = int(t * SR)
        if i >= N - 200:
            break
        k = int(rng.uniform(0.0008, 0.003) * SR)
        g = rng.uniform(0.04, 0.22) * (1 - 0.6 * prog)
        estalo = rng.standard_normal(k) * np.exp(-np.arange(k) / (k / 4)) * g
        lado = rng.uniform(0.25, 0.75)
        e[0, i:i + k] += estalo * (1 - lado)
        e[1, i:i + k] += estalo * lado
    e = np.stack([filtro(c, 'highpass', 1800) for c in e])
    return s, e


def assobio():
    env = rampa(PONTO, PONTO + 0.25) * (1 - rampa(DURACAO - 0.9, DURACAO - 0.1))
    return np.sin(2 * np.pi * 15734 * T) * env * 0.012


def tubo():
    """O vuuush do tubo acordando: o ruído subindo de grave para agudo, de 0,6 s ao fim da abertura."""
    faixas = [(250, 600), (600, 1400), (1400, 3200), (3200, 7000)]
    s = np.zeros(N)
    ini, fim = PONTO + 0.02, ABRE + 0.55
    passo = (fim - ini) / len(faixas)
    for j, f in enumerate(faixas):
        a = ini + j * passo
        env = rampa(a - passo * 0.6, a + passo * 0.4) * (1 - rampa(a + passo * 0.6, a + passo * 1.6))
        s += filtro(ruido(), 'bandpass', list(f)) * env
    return s * 0.11


def neve():
    """O chiado da tela de neve, em estéreo, tremendo um pouco, enquanto a imagem abre e esquenta."""
    tremor = 1 + 0.35 * filtro(rng.standard_normal(N), 'lowpass', 22) * 6
    env = rampa(ABRE - 0.02, ABRE + 0.08) * (1 - rampa(ABRE + 0.45, CHIADO_FIM)) ** 1.5
    return np.stack([filtro(ruido(), 'bandpass', [260, 8500]) * tremor * env * 0.17 for _ in range(2)])


def zumbido_de_fundo():
    env = rampa(DEGAUSS, DEGAUSS + 0.3) * (1 - rampa(CHIADO_FIM - 0.3, CHIADO_FIM + 0.6))
    return (0.4 * np.sin(2 * np.pi * 60 * T) + np.sin(2 * np.pi * 120 * T + 0.7)) * env * 0.011


def montar():
    meio = botao() + rele() + degauss() + assobio() + tubo() + zumbido_de_fundo()
    estalo, estalinhos = alta_tensao()
    meio += estalo
    som = np.stack([meio, meio]) + estalinhos + neve()
    som = np.stack([filtro(c, 'highpass', 28, 2) for c in som])
    som *= 10 ** (-3 / 20) / np.abs(som).max()          # o pico a -3 dB
    # o fim já em silêncio, sem estalo
    som *= 1 - rampa(DURACAO - 0.3, DURACAO - 0.02)
    return som


def gravar(caminho, som):
    wf.write(caminho, SR, (np.clip(som.T, -1, 1) * 32767).astype(np.int16))


def previa(som, mp3):
    """O som e a trilha do menu entrando como no jogo, 16 s."""
    ogg = os.path.join(AQUI, '..', 'Assets', '_Game', 'Audio', 'Trilha', 'momentos', 'menu.ogg')
    with tempfile.TemporaryDirectory() as tmp:
        wav = os.path.join(tmp, 'menu.wav')
        subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', ogg, '-ar', str(SR), '-ac', '2', wav], check=True)
        _, m = wf.read(wav)
        m = m.astype(np.float64).T / 32768
        total = int(16 * SR)
        mix = np.zeros((2, total))
        mix[:, :som.shape[1]] += som * 0.9
        ini = int(MUSICA_ENTRA * SR)
        trecho = m[:, :total - ini]
        u = np.clip(np.arange(trecho.shape[1]) / (MUSICA_SOBE * SR), 0, 1)
        mix[:, ini:ini + trecho.shape[1]] += trecho * u * u
        saida = os.path.join(tmp, 'previa.wav')
        gravar(saida, mix)
        subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', saida, '-b:a', '192k', mp3], check=True)


def main():
    os.makedirs(PASTA, exist_ok=True)
    som = montar()
    caminho = os.path.join(PASTA, 'TV_Ligando.wav')
    gravar(caminho, som)
    rms = lambda a, b: 20 * np.log10(np.sqrt((som[:, int(a * SR):int(b * SR)] ** 2).mean()) + 1e-9)
    print(f'{caminho}: {DURACAO} s; o botão {rms(BOTAO, BOTAO + 0.08):.1f} dB, a bobina {rms(DEGAUSS, DEGAUSS + 0.5):.1f} dB, '
          f'a neve {rms(ABRE + 0.1, ABRE + 0.5):.1f} dB')
    if len(sys.argv) > 3 and sys.argv[2] == 'prévia':
        previa(som, sys.argv[3])
        print('prévia:', sys.argv[3])


if __name__ == '__main__':
    main()
