# Os sons de ambiente da cidade abandonada (a pedido em 29/10/2026: "isso deve entrar de uma maneira que a trilha sonora e
# esses sons ambientes se encaixem"), gerados por código, como a trilha:
#  - os grilos (à noite, em laço, estéreo: uns perto, um coro longe e o chiado do campo);
#  - os cachorros longe (latidos e uivos; os uivos nas notas do ré menor da trilha: lá, ré e fá);
#  - o que o vento mexe: a janela batendo, a porta batendo, a placa de metal rangendo (terminando num lá ou num ré) e a
#    lata rolando no asfalto;
#  - de dentro dos prédios: a madeira rangendo;
#  - de dia: os corvos.
# Cada evento é mono (o jogo põe cada um num ponto em volta de quem escuta) e já tem o eco da rua (um retorno das
# fachadas e uma cauda curta). Uso: python ambiente.py [pasta]   (sem pasta: Assets/_Game/Audio/Ambiente)
import os
import sys
import numpy as np
import scipy.signal as sg
import scipy.io.wavfile as wf

SR = 44100
AQUI = os.path.dirname(os.path.abspath(__file__))
PASTA = sys.argv[1] if len(sys.argv) > 1 else os.path.join(AQUI, '..', 'Assets', '_Game', 'Audio', 'Ambiente')


def t_de(n):
    return np.arange(n) / SR


def passa(x, tipo, f, ordem=2):
    return sg.sosfilt(sg.butter(ordem, f, btype=tipo, fs=SR, output='sos'), x).astype(np.float32)


def pico(x, f, q):
    b, a = sg.iirpeak(min(f, SR / 2 - 200), q, fs=SR)
    return sg.lfilter(b, a, x).astype(np.float32)


def env(n, ataque, queda):
    t = t_de(n)
    return (np.minimum(t / max(ataque, 1e-4), 1) * np.exp(-t / max(queda, 1e-4))).astype(np.float32)


def eco_da_rua(x, rng, molhado=0.35, rt=1.4, retorno=0.22, atraso=0.18):
    """O eco de fora: um retorno das fachadas (um eco só, mais escuro) e uma cauda curta e esparsa."""
    n = int(rt * 1.3 * SR)
    t = t_de(n)
    ir = rng.standard_normal(n) * np.exp(-6.9 * t / rt) * (rng.random(n) < 0.25)
    ir = passa(ir.astype(np.float32), 'low', 3500)
    ir /= np.sqrt((ir ** 2).sum()) + 1e-9
    y = np.zeros(len(x) + n + int(atraso * SR), np.float32)
    y[:len(x)] += x
    d = int(atraso * SR)
    y[d:d + len(x)] += retorno * passa(x, 'low', 2500)
    cauda = sg.oaconvolve(np.pad(x, (0, n)), ir)[:len(x) + n]
    cauda = cauda / (np.abs(cauda).max() + 1e-9) * (np.abs(x).max() + 1e-9)
    y[:len(cauda)] += molhado * cauda
    return y


def fechar(x, pico_db=-3.0, fade=0.05):
    m = int(fade * SR)
    x = x.copy()
    x[-m:] *= np.linspace(1, 0, m) ** 2
    # sem o silêncio do fim (a cauda do eco abaixo de -70 dB)
    lim = np.abs(x).max() * 10 ** (-70 / 20)
    fim = len(x)
    while fim > SR // 10 and np.abs(x[fim - 512:fim]).max() < lim:
        fim -= 512
    x = x[:fim]
    return x / (np.abs(x).max() + 1e-9) * 10 ** (pico_db / 20)


def gravar(nome, x):
    os.makedirs(PASTA, exist_ok=True)
    if x.ndim == 2:
        dados = (np.clip(x.T, -1, 1) * 32767).astype(np.int16)
    else:
        dados = (np.clip(x, -1, 1) * 32767).astype(np.int16)
    wf.write(os.path.join(PASTA, nome + '.wav'), SR, dados)
    print(f'{nome}: {len(x if x.ndim == 1 else x[0]) / SR:.1f} s')


# ================================================================================================ os grilos (o laço)
def um_grilo(n, rng, f, por_segundo, pulsos, taxa, nivel):
    t = t_de(n)
    fase = 2 * np.pi * f * t + 0.8 * np.sin(2 * np.pi * rng.uniform(3, 7) * t)
    portadora = np.sin(fase) + 0.3 * np.sin(2 * fase)
    e = np.zeros(n, np.float32)
    largura = int(SR / taxa * 0.55)
    janela = np.hanning(largura).astype(np.float32)
    t0 = rng.uniform(0, 1 / por_segundo)
    while t0 < n / SR:
        for k in range(pulsos):
            i = int((t0 + k / taxa) * SR)
            if i + largura < n:
                e[i:i + largura] += janela * rng.uniform(0.7, 1.0)
        t0 += (1 / por_segundo) * rng.uniform(0.85, 1.15)
    return portadora * e * nivel


def grilos(semente, dur=30.0, junta=3.0):
    rng = np.random.default_rng(semente)
    n = int((dur + junta) * SR)
    e, d = np.zeros(n, np.float32), np.zeros(n, np.float32)
    # os de perto: cada um com o seu tom, o seu ritmo e o seu lado
    for _ in range(rng.integers(5, 8)):
        g = um_grilo(n, rng, rng.uniform(3900, 4900), rng.uniform(1.2, 2.8), int(rng.integers(3, 6)), rng.uniform(24, 36), rng.uniform(0.25, 0.6))
        lado = rng.uniform(-0.9, 0.9)
        e += g * np.cos((lado + 1) * np.pi / 4)
        d += g * np.sin((lado + 1) * np.pi / 4)
    # o coro longe: muitos, baixinhos e abafados
    coro_e, coro_d = np.zeros(n, np.float32), np.zeros(n, np.float32)
    for _ in range(30):
        g = um_grilo(n, rng, rng.uniform(3600, 5200), rng.uniform(1.5, 3.5), int(rng.integers(2, 5)), rng.uniform(22, 40), rng.uniform(0.05, 0.12))
        lado = rng.uniform(-1, 1)
        coro_e += g * np.cos((lado + 1) * np.pi / 4)
        coro_d += g * np.sin((lado + 1) * np.pi / 4)
    e += passa(coro_e, 'low', 4800)
    d += passa(coro_d, 'low', 4800)
    # o chiado do campo (um trilo contínuo, longe)
    for canal in (e, d):
        r = passa(rng.standard_normal(n).astype(np.float32), 'band', [4200, 5600])
        canal += r * 0.035 * (0.6 + 0.4 * np.sin(2 * np.pi * 47 * t_de(n)))
    # o laço: o fim entra no começo (sem emenda)
    m = int(junta * SR)
    k = np.sin(np.linspace(0, np.pi / 2, m)) ** 2
    out = []
    for canal in (e, d):
        c = canal[:n - m].copy()
        c[:m] = c[:m] * k + canal[n - m:] * k[::-1]
        out.append(c)
    y = np.stack(out)
    return y / np.abs(y).max() * 0.5


# ================================================================================================ os cachorros
def latido(rng, f0, dur, aspero=0.4):
    """Um latido: pulsos da garganta caindo de tom, rouco, nas vogais de um "au"."""
    n = int(dur * SR)
    t = t_de(n)
    f = f0 * (1.0 - 0.35 * (t / dur)) * (1 + 0.03 * rng.standard_normal(n).cumsum() / np.sqrt(n))
    fase = np.cumsum(f) / SR
    pulsos = (np.sin(2 * np.pi * fase) > 0.6).astype(np.float32) * 2 - 0.4
    pulsos = passa(pulsos, 'low', 3000) + aspero * rng.standard_normal(n).astype(np.float32) * 0.5
    voz = pico(pulsos, 650, 4) + 0.7 * pico(pulsos, 1250, 6) + 0.25 * pico(pulsos, 2600, 8)
    e = np.minimum(t / 0.008, 1) * np.exp(-t / (dur * 0.45))
    return voz * e


def latidos(semente):
    rng = np.random.default_rng(semente)
    y = np.zeros(int(6 * SR), np.float32)
    t = 0.2
    f0 = rng.uniform(380, 520)
    for _ in range(rng.integers(2, 6)):
        b = latido(rng, f0 * rng.uniform(0.95, 1.05), rng.uniform(0.12, 0.2))
        i = int(t * SR)
        y[i:i + len(b)] += b * rng.uniform(0.7, 1.0)
        t += rng.uniform(0.32, 0.75)
    # às vezes outro cachorro responde, mais longe
    if rng.random() < 0.5:
        t += rng.uniform(0.4, 1.0)
        f1 = rng.uniform(300, 600)
        for _ in range(rng.integers(1, 4)):
            b = passa(latido(rng, f1, rng.uniform(0.12, 0.18)), 'low', 1600) * 0.45
            i = int(t * SR)
            if i + len(b) < len(y):
                y[i:i + len(b)] += b
            t += rng.uniform(0.35, 0.8)
    y = y[:int((t + 0.5) * SR)]
    y = passa(passa(y, 'low', 2800), 'high', 180)   # longe: sem o agudo e sem o grave
    return fechar(eco_da_rua(y, rng, 0.55, 1.8, 0.3, 0.24))


def uivo(semente, nota):
    """O uivo: sobe até a nota (do ré menor da trilha), segura tremendo e cai uma terça no fim."""
    rng = np.random.default_rng(semente)
    dur = rng.uniform(2.6, 3.8)
    n = int(dur * SR)
    t = t_de(n)
    u = t / dur
    alvo = 440.0 * 2 ** ((nota - 69) / 12)
    curva = np.where(u < 0.18, alvo * (0.82 + 0.18 * (u / 0.18) ** 0.6),
                     np.where(u < 0.8, alvo, alvo * 2 ** (-3 / 12 * (np.clip(u - 0.8, 0, None) / 0.2) ** 1.5)))
    f = curva * (1 + 0.018 * np.sin(2 * np.pi * 5.2 * t) * np.minimum(u / 0.25, 1)) * (1 + 0.006 * np.sin(2 * np.pi * 0.7 * t))
    fase = np.cumsum(f) / SR
    fonte = sum(np.sin(2 * np.pi * k * fase) / k ** 1.3 for k in range(1, 8)).astype(np.float32)
    fonte += 0.08 * rng.standard_normal(n).astype(np.float32)
    voz = pico(fonte, 700, 3) + 0.6 * pico(fonte, 1100, 4) + 0.2 * pico(fonte, 2500, 6) + 0.3 * fonte
    e = np.minimum(t / 0.35, 1) * np.minimum((dur - t) / 0.5, 1)
    y = voz * np.clip(e, 0, 1)
    y = passa(passa(y, 'low', 3000), 'high', 200)
    return fechar(eco_da_rua(y.astype(np.float32), rng, 0.65, 2.4, 0.3, 0.3))


# ================================================================================================ o que o vento mexe
def batida(rng, modos, decai, forca=1.0, estalo=0.4):
    """Uma batida de madeira: o estalo do contato e os modos da folha (cada um sumindo no seu tempo)."""
    n = int(0.6 * SR)
    t = t_de(n)
    x = np.zeros(n, np.float32)
    for f, a, d in modos:
        x += a * np.sin(2 * np.pi * f * rng.uniform(0.97, 1.03) * t + rng.uniform(0, 6)) * np.exp(-t / (d * decai))
    ruido = rng.standard_normal(n).astype(np.float32) * np.exp(-t / 0.006)
    x += estalo * passa(ruido, 'band', [800, 5000]) * 3
    return x * np.minimum(t / 0.0015, 1) * forca


def chocalho_de_vidro(rng, n):
    x = np.zeros(n, np.float32)
    for _ in range(rng.integers(4, 10)):
        i = int(rng.uniform(0, 0.25) * SR)
        k = int(0.08 * SR)
        tt = t_de(k)
        tik = sum(np.sin(2 * np.pi * f * tt) for f in rng.uniform(2500, 6000, 3)) * np.exp(-tt / 0.012)
        if i + k < n:
            x[i:i + k] += tik * rng.uniform(0.05, 0.15)
    return x


def janela(semente):
    rng = np.random.default_rng(semente)
    y = np.zeros(int(4 * SR), np.float32)
    modos = [(140, 1.0, 0.09), (235, 0.7, 0.07), (410, 0.5, 0.05), (690, 0.3, 0.035), (1150, 0.15, 0.02)]
    t = 0.05
    for k in range(rng.integers(1, 4)):
        b = batida(rng, modos, rng.uniform(0.8, 1.3), rng.uniform(0.6, 1.0) if k else 1.0)
        if rng.random() < 0.5:
            b[:int(0.3 * SR)] += chocalho_de_vidro(rng, int(0.3 * SR))
        i = int(t * SR)
        y[i:i + len(b)] += b
        t += rng.uniform(0.18, 0.9)
    y = y[:int((t + 0.6) * SR)]
    return fechar(eco_da_rua(y, rng, 0.35, 1.3, 0.25, 0.15))


def porta(semente):
    rng = np.random.default_rng(semente)
    modos = [(72, 1.0, 0.16), (118, 0.8, 0.12), (183, 0.5, 0.09), (310, 0.3, 0.06), (520, 0.15, 0.04)]
    y = batida(rng, modos, rng.uniform(0.9, 1.2), 1.0, 0.25)
    # o trinco batendo logo depois
    k = int(0.05 * SR)
    tt = t_de(k)
    trinco = np.sin(2 * np.pi * 1800 * tt) * np.exp(-tt / 0.008) * 0.25
    i = int(rng.uniform(0.04, 0.09) * SR)
    y[i:i + k] += trinco
    return fechar(eco_da_rua(y, rng, 0.45, 1.6, 0.3, 0.2))


def rangido(rng, dur, f_ini, f_fim, ressonancias, aspero=0.6, densidade=1.0):
    """O rangido: o atrito que agarra e solta (pulsos irregulares com a altura deslizando) passando pelas ressonâncias."""
    n = int(dur * SR)
    t = t_de(n)
    u = t / dur
    f = f_ini + (f_fim - f_ini) * (0.5 - 0.5 * np.cos(np.pi * u))
    f = f * (1 + 0.04 * passa(rng.standard_normal(n).astype(np.float32), 'low', 8) * 8)
    fase = np.cumsum(f) / SR
    pulsos = np.zeros(n, np.float32)
    saltos = np.where(np.diff(np.floor(fase)) > 0)[0]
    for i in saltos:
        if rng.random() < densidade:
            j = min(n - 1, max(0, i + int(rng.normal(0, 0.18) * SR / max(f[i], 1.0))))   # agarra e solta fora do compasso
            pulsos[j] += rng.uniform(1 - aspero, 1.0)
    x = sum(a * pico(pulsos, fr, q) for fr, a, q in ressonancias)
    e = np.sin(np.pi * np.clip(u, 0, 1)) ** 0.6
    return x * e


def placa(semente, nota_final):
    """A placa (ou o portão) de metal balançando: um ou dois rangidos, terminando na nota (lá ou ré)."""
    rng = np.random.default_rng(semente)
    alvo = 440.0 * 2 ** ((nota_final - 69) / 12)
    # o atrito agarra e solta devagar (60 a 140 vezes por segundo); quem canta é o metal (as ressonâncias, na nota)
    ress = [(alvo * 2.0, 1.0, 40), (alvo * 3.0, 0.5, 45), (alvo * 4.6, 0.3, 45), (alvo * 1.0, 0.6, 30)]
    y = rangido(rng, rng.uniform(1.0, 1.6), rng.uniform(55, 75), rng.uniform(110, 140), ress, 0.7, 0.75)
    if rng.random() < 0.6:
        volta = rangido(rng, rng.uniform(0.8, 1.3), rng.uniform(110, 140), rng.uniform(50, 70), ress, 0.7, 0.7) * 0.7
        y = np.concatenate([y, np.zeros(int(rng.uniform(0.2, 0.6) * SR), np.float32), volta])
    y = np.tanh(y / (np.abs(y).max() + 1e-9) * 1.5)
    return fechar(eco_da_rua(y.astype(np.float32), rng, 0.4, 1.4, 0.25, 0.17))


def lata(semente):
    """A lata rolando no asfalto com o vento: tiquinhos de metal e o arrasto, e duas batidinhas para parar."""
    rng = np.random.default_rng(semente)
    dur = rng.uniform(2.0, 3.5)
    n = int(dur * SR)
    t = t_de(n)
    giro = rng.uniform(3, 6) * (1 - 0.5 * t / dur)
    fase = np.cumsum(giro) / SR
    x = passa(rng.standard_normal(n).astype(np.float32), 'band', [1200, 4500]) * 0.15 * (0.5 + 0.5 * np.sin(2 * np.pi * fase) ** 2)
    modos = [2200, 3700, 5100]
    k = int(0.03 * SR)
    tt = t_de(k)
    for i in np.where(np.diff(np.floor(fase * 2)) > 0)[0]:
        if rng.random() < 0.35:
            continue   # nem toda volta bate
        tik = sum(np.sin(2 * np.pi * f * rng.uniform(0.9, 1.1) * tt) for f in modos) * np.exp(-tt / 0.005) * rng.uniform(0.2, 0.6)
        if i + k < n:
            x[i:i + k] += tik
    x *= np.minimum(t / 0.3, 1) * np.minimum((dur - t) / 0.4 + 0.2, 1)
    fim = np.zeros(int(0.8 * SR), np.float32)
    for j, (a, at) in enumerate([(0.5, 0.05), (0.3, 0.32)]):
        i = int(at * SR)
        fim[i:i + k] += sum(np.sin(2 * np.pi * f * tt) for f in modos) * np.exp(-tt / 0.02) * a
    y = np.concatenate([x, fim])
    return fechar(eco_da_rua(y, rng, 0.3, 1.1, 0.2, 0.14))


# ================================================================================================ de dentro
def madeira(semente):
    """A madeira do prédio rangendo (de dentro): grave, lento, com estalinhos."""
    rng = np.random.default_rng(semente)
    ress = [(180, 1.0, 8), (320, 0.7, 9), (610, 0.4, 10), (95, 0.6, 6)]
    y = rangido(rng, rng.uniform(1.2, 2.4), rng.uniform(40, 60), rng.uniform(70, 110), ress, 0.7, 0.85)
    for _ in range(rng.integers(2, 6)):
        i = int(rng.uniform(0, len(y) / SR - 0.05) * SR)
        k = int(0.03 * SR)
        y[i:i + k] += passa(rng.standard_normal(k).astype(np.float32), 'band', [1500, 4000]) * np.exp(-t_de(k) / 0.004) * 0.6
    y = np.tanh(y / (np.abs(y).max() + 1e-9) * 1.2)
    # o eco de um cômodo (curto)
    n = int(0.6 * SR)
    ir = rng.standard_normal(n) * np.exp(-6.9 * t_de(n) / 0.5)
    ir = passa(ir.astype(np.float32), 'low', 3000)
    ir /= np.sqrt((ir ** 2).sum())
    z = sg.oaconvolve(np.pad(y, (0, n)), ir)[:len(y) + n]
    z = np.pad(y, (0, n)) + 0.4 * z / (np.abs(z).max() + 1e-9) * np.abs(y).max()
    return fechar(z.astype(np.float32))


# ================================================================================================ de dia
def corvos(semente):
    rng = np.random.default_rng(semente)
    y = np.zeros(int(5 * SR), np.float32)
    t = 0.1
    f0 = rng.uniform(560, 760)
    for _ in range(rng.integers(2, 5)):
        dur = rng.uniform(0.22, 0.38)
        n = int(dur * SR)
        tt = t_de(n)
        f = f0 * (1.05 - 0.15 * tt / dur) * (1 + 0.05 * rng.standard_normal(n).cumsum() / np.sqrt(n))
        fase = np.cumsum(f) / SR
        fonte = 2 * (fase % 1.0) - 1 + 0.6 * rng.standard_normal(n)
        voz = pico(fonte.astype(np.float32), 1300, 4) + 0.8 * pico(fonte.astype(np.float32), 2300, 5) + 0.3 * pico(fonte.astype(np.float32), 3600, 6)
        e = np.minimum(tt / 0.02, 1) * np.minimum((dur - tt) / 0.08, 1)
        i = int(t * SR)
        y[i:i + n] += voz * np.clip(e, 0, 1)
        t += dur + rng.uniform(0.25, 0.55)
    y = y[:int((t + 0.4) * SR)]
    y = passa(y, 'low', 5000)
    return fechar(eco_da_rua(y, rng, 0.45, 1.6, 0.25, 0.2))


if __name__ == '__main__':
    gravar('Grilos_01', grilos(1))
    gravar('Grilos_02', grilos(2))
    for i in range(4):
        gravar(f'Latido_{i + 1:02d}', latidos(10 + i))
    for i, nota in enumerate([69, 74, 65]):   # lá, ré, fá: o ré menor da trilha
        gravar(f'Uivo_{i + 1:02d}', uivo(20 + i, nota))
    for i in range(4):
        gravar(f'Janela_{i + 1:02d}', janela(30 + i))
    for i in range(2):
        gravar(f'Porta_{i + 1:02d}', porta(40 + i))
    for i, nota in enumerate([69, 62, 69]):   # termina num lá ou num ré
        gravar(f'Placa_{i + 1:02d}', placa(50 + i, nota))
    for i in range(2):
        gravar(f'Lata_{i + 1:02d}', lata(60 + i))
    for i in range(3):
        gravar(f'Madeira_{i + 1:02d}', madeira(70 + i))
    for i in range(3):
        gravar(f'Corvos_{i + 1:02d}', corvos(80 + i))
