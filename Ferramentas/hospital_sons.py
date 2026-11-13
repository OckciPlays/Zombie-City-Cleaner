# Os sons do Hospital Principal (o Plano_Hospital.md da pasta de arte, a parte 6; 07/11/2026, a pedido: "o mais importante
# é o silêncio"; o hospital está abandonado há anos e sem energia: nada apita, nada liga). Gerados por código, como os
# outros da pasta Ferramentas, mas só com o numpy (os filtros e os ecos pela FFT), para rodar no Python que vem com o
# Blender (aqui não há o scipy nem o ffmpeg):
#   "C:\Program Files (x86)\Steam\steamapps\common\Blender\5.2\python\bin\python.exe" hospital_sons.py [pasta]
# (sem pasta: Assets/_Game/Audio/Hospital). Uns 40 s.
#
# Os eventos (mono, 44,1 kHz, cada um com o eco de dentro do prédio: o concreto, o azulejo, os corredores compridos; o
# jogo põe cada um num ponto do hospital, abafado pelas paredes):
#  - Porta: a porta rangendo devagar (o atrito da dobradiça: os pulsos do "agarra e solta" excitando a madeira);
#  - Goteira: uma a três gotas caindo numa poça (o "plim" que sobe de tom);
#  - Metal: o metal estalando (a chapa ou o perfil que dilata: o clique e os parciais desafinados);
#  - Cano: a tubulação batendo (os "tonc" graves, do subsolo);
#  - Estrutura: a madeira e o forro rangendo devagar (mais grave e mais lento que a porta);
#  - Vento: o vento passando por uma janela quebrada (o assobio que sobe e desce, com o sopro);
#  - Papeis: os papéis mexidos pelo vento (os estalinhos do papel);
#  - Pancada: uma pancada longe, abafada (um baque grave);
#  - Maca: uma maca rolando um pouco (a rodinha que guincha e o rolar), e o tranco no fim.
# Sem música: houve duas (o piano velho com o grave, no ré menor da trilha), e o usuário preferiu o hospital só com os
# sons (07/11/2026: "vamos deixar sem a música, somente os sons").
import os
import sys
import wave

import numpy as np

SR = 44100
AQUI = os.path.dirname(os.path.abspath(__file__))
PASTA = sys.argv[1] if len(sys.argv) > 1 else os.path.join(AQUI, '..', 'Assets', '_Game', 'Audio', 'Hospital')


# ------------------------------------------------------------------ as ferramentas


def t_de(n, sr=SR):
    return np.arange(n) / sr


def _pot2(n):
    return 1 << int(np.ceil(np.log2(max(n, 2))))


def filtro(x, sr=SR, baixo=None, alto=None, ordem=2):
    """Passa-faixa de fase zero pela FFT: corta abaixo de 'baixo' e acima de 'alto' (Hz), com a queda de uma Butterworth."""
    n = len(x)
    N = _pot2(n + 2048)
    X = np.fft.rfft(x, N)
    f = np.fft.rfftfreq(N, 1 / sr)
    H = np.ones_like(f)
    if alto:
        H *= 1 / np.sqrt(1 + (f / alto) ** (2 * ordem))
    if baixo:
        H *= 1 / np.sqrt(1 + (baixo / np.maximum(f, 1e-3)) ** (2 * ordem))
    return np.fft.irfft(X * H, N)[:n]


def ressoar(x, freqs, decaimentos, ganhos, sr=SR):
    """O que bate num corpo: x convoluído com os modos dele (senos que decaem)."""
    d = max(decaimentos) * 6
    t = t_de(int(d * sr), sr)
    ir = np.zeros(len(t))
    for f, dc, g in zip(freqs, decaimentos, ganhos):
        if f < sr / 2 * 0.95:
            ir += g * np.sin(2 * np.pi * f * t) * np.exp(-t / dc)
    return convolver(x, ir)[:len(x) + len(ir)]


def convolver(x, h):
    n = len(x) + len(h) - 1
    N = _pot2(n)
    return np.fft.irfft(np.fft.rfft(x, N) * np.fft.rfft(h, N), N)[:n]


def eco_de_dentro(rt, rng, sr=SR, escuro=3500.0, cedo=((0.011, 0.5), (0.023, 0.35), (0.037, 0.25), (0.052, 0.18))):
    """A resposta de um espaço fechado de concreto: os primeiros retornos e a cauda que decai em rt (os agudos antes)."""
    n = int(rt * 1.2 * sr)
    t = t_de(n, sr)
    ruido = rng.standard_normal(n)
    # a cauda em três faixas: o grave dura o rt inteiro, o meio 75%, o agudo 45%
    g = filtro(ruido, sr, alto=500) * np.exp(-6.9 * t / rt)
    m = filtro(ruido, sr, baixo=500, alto=escuro) * np.exp(-6.9 * t / (rt * 0.75))
    a = filtro(ruido, sr, baixo=escuro) * np.exp(-6.9 * t / (rt * 0.45)) * 0.5
    ir = (g + m + a) * np.minimum(t / 0.02, 1.0)
    ir /= np.sqrt((ir ** 2).sum()) + 1e-9
    for atraso, ganho in cedo:
        i = int(atraso * sr)
        if i < n:
            ir[i] += ganho * (0.8 + 0.4 * rng.random())
    return ir


def com_eco(x, rt, molhado, rng, sr=SR, escuro=3500.0):
    ir = eco_de_dentro(rt, rng, sr, escuro)
    seco = np.zeros(len(x) + len(ir) - 1)
    seco[:len(x)] = x
    cauda = convolver(x, ir)
    cauda *= (np.abs(x).max() + 1e-9) / (np.abs(cauda).max() + 1e-9)
    return seco + molhado * cauda


def envelope(n, ataque, solta, sr=SR):
    e = np.ones(n)
    a = max(1, int(ataque * sr))
    s = max(1, int(solta * sr))
    e[:a] = np.linspace(0, 1, a)
    e[-s:] *= np.linspace(1, 0, s) ** 1.5
    return e


def suave(n, passo, rng):
    """Um ruído lento (0 a 1), um valor a cada 'passo' amostras, ligado por curvas."""
    k = n // passo + 3
    v = rng.random(k)
    x = np.arange(n) / passo
    i = np.floor(x).astype(int)
    f = x - i
    f = f * f * (3 - 2 * f)
    return v[i] * (1 - f) + v[i + 1] * f


def atrito(dur, ritmo, modos, decaimentos, ganhos, rng, irregular=0.25, sr=SR, ataque=0.15, solta=0.4):
    """O rangido: os pulsos do "agarra e solta" (o ritmo em Hz ao longo do tempo) excitando os modos de um corpo."""
    n = int(dur * sr)
    x = np.zeros(n)
    fase = 0.0
    i = 0
    while i < n:
        r = max(ritmo(i / sr), 3.0)
        x[i] = (0.5 + 0.5 * rng.random()) * (1 if rng.random() > 0.08 else 0)
        passo = sr / r * (1 + irregular * (rng.random() - 0.5))
        i += max(1, int(passo))
    x = filtro(x, sr, baixo=60)
    # um pouco de chiado do atrito por baixo
    x += filtro(rng.standard_normal(n), sr, baixo=1500, alto=6000) * 0.04
    x *= envelope(n, ataque, solta, sr)
    return ressoar(x, modos, decaimentos, ganhos, sr)


def fechar(x, pico_db, fade=0.06, sr=SR):
    x = np.asarray(x, dtype=np.float64)
    x = x - np.mean(x)
    m = int(fade * sr)
    if m > 0 and len(x) > 2 * m:
        x[-m:] *= np.linspace(1, 0, m) ** 2
        x[:64] *= np.linspace(0, 1, 64)
    # sem o silêncio do fim (a cauda abaixo de -60 dB)
    lim = np.abs(x).max() * 10 ** (-60 / 20)
    fim = len(x)
    while fim > sr // 4 and np.abs(x[fim - 256:fim]).max() < lim:
        fim -= 256
    x = x[:fim]
    x *= 10 ** (pico_db / 20) / (np.abs(x).max() + 1e-9)
    return x


def gravar(nome, x, sr=SR):
    os.makedirs(PASTA, exist_ok=True)
    x = np.asarray(x)
    canais = 1 if x.ndim == 1 else x.shape[0]
    dados = np.clip(x if canais == 1 else x.T.reshape(-1), -1, 1)
    with wave.open(os.path.join(PASTA, nome + '.wav'), 'wb') as w:
        w.setnchannels(canais)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes((dados * 32767).astype('<i2').tobytes())
    dur = (x.shape[-1]) / sr
    print(f'{nome}: {dur:.1f} s')


# ------------------------------------------------------------------ os eventos


def porta(seed):
    rng = np.random.default_rng(seed)
    dur = rng.uniform(2.2, 3.4)
    base = rng.uniform(55, 95)
    # a porta anda devagar: o ritmo sobe, oscila e cai (a dobradiça seca)
    curva = suave(int(dur * 100) + 4, 30, rng)
    def ritmo(t):
        u = t / dur
        return base * (0.5 + 1.6 * np.sin(np.pi * u) ** 0.7) * (0.75 + 0.5 * curva[int(t * 100)])
    modos = [rng.uniform(380, 460), rng.uniform(880, 1050), rng.uniform(1500, 1800), rng.uniform(2400, 2900)]
    x = atrito(dur, ritmo, modos, [0.03, 0.022, 0.015, 0.01], [1.0, 0.7, 0.45, 0.25], rng)
    return fechar(com_eco(x, 1.9, 0.42, rng), -3)


def goteira(seed):
    rng = np.random.default_rng(seed)
    gotas = 1 + seed % 3   # uma, duas ou três gotas
    dur = 0.5 + gotas * 1.5
    n = int(dur * SR)
    x = np.zeros(n)
    t0 = 0.05
    for g in range(gotas):
        f0 = rng.uniform(950, 1500)
        m = int(0.09 * SR)
        t = t_de(m)
        f = f0 * (1 + 0.7 * (1 - np.exp(-t / 0.012)))   # o "plim" sobe de tom
        fase = 2 * np.pi * np.cumsum(f) / SR
        gota = np.sin(fase) * np.exp(-t / 0.028) * (0.7 + 0.3 * rng.random())
        gota[:int(0.002 * SR)] += rng.standard_normal(int(0.002 * SR)) * 0.25
        i = int(t0 * SR)
        x[i:i + m] += gota[:max(0, min(m, n - i))]
        t0 += rng.uniform(0.6, 1.5)
        if t0 > dur - 0.3:
            break
    return fechar(com_eco(x, 1.5, 0.5, rng, escuro=4500), -6)


def metal(seed):
    rng = np.random.default_rng(seed)
    n = int(1.8 * SR)
    x = np.zeros(n)
    estalos = 1 if rng.random() < 0.6 else 2
    for k in range(estalos):
        i = int((0.03 + k * rng.uniform(0.25, 0.6)) * SR)
        x[i] += 1.0
        x[i:i + 90] += filtro(rng.standard_normal(90), baixo=2500) * 0.4
    f0 = rng.uniform(280, 650)
    razoes = [1.0, 2.76, 5.40, 8.93, 13.3]
    y = ressoar(x, [f0 * r * rng.uniform(0.98, 1.02) for r in razoes], [0.7, 0.45, 0.28, 0.16, 0.09], [0.8, 0.6, 0.4, 0.25, 0.15])
    y = filtro(y, baixo=150)
    return fechar(com_eco(y, 2.0, 0.4, rng), -4)


def cano(seed):
    rng = np.random.default_rng(seed)
    n = int(2.6 * SR)
    x = np.zeros(n)
    t0 = 0.04
    for k in range(rng.integers(2, 5)):
        x[int(t0 * SR)] += (1.0 - 0.18 * k) * rng.uniform(0.7, 1.0)
        t0 += rng.uniform(0.18, 0.7)
        if t0 > 1.9:
            break
    f0 = rng.uniform(120, 210)
    y = ressoar(filtro(x, alto=3000), [f0, f0 * 2.31, f0 * 4.12, f0 * 6.6], [0.5, 0.32, 0.18, 0.1], [1.0, 0.55, 0.3, 0.15])
    return fechar(com_eco(filtro(y, alto=3500), 2.3, 0.5, rng, escuro=2500), -4)


def estrutura(seed):
    rng = np.random.default_rng(seed)
    dur = rng.uniform(1.6, 2.8)
    curva = suave(int(dur * 100) + 4, 40, rng)
    def ritmo(t):
        u = t / dur
        return rng.uniform(22, 45) * (0.4 + 1.2 * np.sin(np.pi * u) ** 0.8) * (0.7 + 0.6 * curva[int(t * 100)])
    modos = [rng.uniform(170, 220), rng.uniform(390, 470), rng.uniform(760, 900)]
    x = atrito(dur, ritmo, modos, [0.05, 0.035, 0.02], [1.0, 0.6, 0.3], rng, irregular=0.45, ataque=0.3, solta=0.6)
    x = filtro(x, alto=2500)
    return fechar(com_eco(x, 2.1, 0.45, rng, escuro=2500), -5)


def vento(seed):
    rng = np.random.default_rng(seed)
    dur = rng.uniform(5.5, 7.5)
    n = int(dur * SR)
    ruido = rng.standard_normal(n)
    # a rajada (sobe, oscila e passa) e o assobio da fresta, que segue a força dela
    forca = envelope(n, dur * 0.35, dur * 0.4) * (0.65 + 0.35 * suave(n, SR // 3, rng))
    sopro = filtro(ruido, baixo=150, alto=1400) * forca
    # o assobio: o ruído numa faixa estreita que anda (em pedaços, cada um com o seu centro)
    assobio = np.zeros(n)
    bloco = SR // 10
    centro0 = rng.uniform(520, 760)
    janela = np.hanning(bloco * 2)
    for i in range(0, n - bloco, bloco):
        trecho = ruido[i:i + bloco * 2] if i + bloco * 2 <= n else np.pad(ruido[i:], (0, i + bloco * 2 - n))
        c = centro0 * (0.8 + 0.5 * forca[i])
        N = _pot2(len(trecho))
        X = np.fft.rfft(trecho * janela, N)
        f = np.fft.rfftfreq(N, 1 / SR)
        H = 1 / (1 + (14 * (f / c - c / np.maximum(f, 1))) ** 2)
        y = np.fft.irfft(X * H, N)[:len(trecho)]
        fim = min(n, i + len(trecho))
        assobio[i:fim] += y[:fim - i]
    assobio *= forca ** 1.5
    x = sopro * 0.6 + assobio * 2.2
    return fechar(com_eco(x, 1.2, 0.25, rng), -8)


def papeis(seed):
    rng = np.random.default_rng(seed)
    dur = rng.uniform(1.6, 2.6)
    n = int(dur * SR)
    forca = envelope(n, dur * 0.3, dur * 0.5) * (0.5 + 0.5 * suave(n, SR // 8, rng))
    x = np.zeros(n)
    i = 0
    while i < n:
        taxa = 20 + 140 * forca[i]
        i += int(SR / taxa * rng.exponential(1.0)) + 1
        if i < n:
            m = rng.integers(40, 220)
            fim = min(n, i + m)
            x[i:fim] += rng.standard_normal(fim - i) * np.exp(-np.arange(fim - i) / (m / 4)) * rng.uniform(0.3, 1.0)
    x = filtro(x, baixo=1800, alto=9000) + filtro(rng.standard_normal(n), baixo=2500, alto=7000) * forca * 0.06
    return fechar(com_eco(x, 1.3, 0.3, rng), -9)


def pancada(seed):
    rng = np.random.default_rng(seed)
    n = int(1.5 * SR)
    x = np.zeros(n)
    for k in range(1 if rng.random() < 0.6 else 2):
        i = int((0.03 + k * rng.uniform(0.35, 0.7)) * SR)
        m = int(0.35 * SR)
        t = t_de(m)
        f = rng.uniform(55, 80) * (1 + 0.6 * np.exp(-t / 0.03))
        baque = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.09)
        baque += filtro(rng.standard_normal(m), alto=500) * np.exp(-t / 0.05) * 0.6
        fim = min(n, i + m)
        x[i:fim] += baque[:fim - i] * (1.0 - 0.3 * k)
    y = filtro(com_eco(x, 2.6, 0.65, rng, escuro=1500), alto=1100)
    return fechar(y, -3)


def maca(seed):
    rng = np.random.default_rng(seed)
    dur = rng.uniform(2.2, 3.2)
    n = int(dur * SR)
    t = t_de(n)
    # a velocidade: empurrada, rola e para
    vel = np.clip(np.sin(np.pi * np.minimum(t / (dur * 0.85), 1.0)) ** 0.8, 0, 1)
    rolar = filtro(rng.standard_normal(n), baixo=40, alto=320) * vel * 0.5
    # o guincho da rodinha: a cada volta (de 2 a 5 por segundo, pela velocidade)
    x = rolar
    fase = 0.0
    f_g = rng.uniform(1500, 2300)
    i = 0
    while i < n:
        v = vel[i]
        if v > 0.15 and rng.random() < 0.85:
            m = int(rng.uniform(0.03, 0.07) * SR)
            tt = t_de(m)
            f = f_g * (1 + 0.15 * np.sin(np.pi * tt / tt[-1]))
            g = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.sin(np.pi * tt / tt[-1]) ** 2 * v * 0.5
            fim = min(n, i + m)
            x[i:fim] += g[:fim - i]
        i += int(SR / max(2.0 + 3.0 * v, 0.5))
    # o tranco no fim (bateu em alguma coisa)
    i = int(dur * 0.86 * SR)
    m = int(0.25 * SR)
    tt = t_de(m)
    tranco = (np.sin(2 * np.pi * 95 * tt) * np.exp(-tt / 0.05) + filtro(rng.standard_normal(m), alto=900) * np.exp(-tt / 0.03)) * 0.7
    fim = min(n, i + m)
    x[i:fim] += tranco[:fim - i]
    return fechar(com_eco(x, 1.8, 0.4, rng), -5)


EVENTOS = [
    ('Porta', porta, 3), ('Goteira', goteira, 3), ('Metal', metal, 3), ('Cano', cano, 2), ('Estrutura', estrutura, 2),
    ('Vento', vento, 2), ('Papeis', papeis, 2), ('Pancada', pancada, 2), ('Maca', maca, 2),
]


def main():
    for nome, fn, quantos in EVENTOS:
        for i in range(quantos):
            gravar(f'{nome}_{i + 1:02d}', fn(100 * (EVENTOS.index((nome, fn, quantos)) + 1) + i))


if __name__ == '__main__':
    main()
