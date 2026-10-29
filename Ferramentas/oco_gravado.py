# Os sons do Oco a partir das gravações do Pedro (a pedido em 29/10/2026: "não quero ficar ouvindo a minha própria voz
# no jogo"): a voz padrão, o golpe, a agonia no sol e a dor do tiro. A gravação é só a matéria-prima; cada tomada vira
# três camadas e passa por um tratamento:
#  - o corpo: a voz mais grave, com os formantes descendo junto (a garganta e o peito de alguém bem maior);
#  - o rosnado: uma oitava abaixo, baixinho e abafado, por baixo de tudo;
#  - o sopro: um chiado rouco com a forma das vogais da própria voz (o ar passando por uma garganta seca);
#  - o tremor da garganta (pulsos irregulares, como o "fry"), a saturação, o oco (tubos que não casam: soa vazio, de
#    dentro) e uma sala curta, que também dá a cauda que a supressão de ruído do celular cortou.
# O volume de cada tipo fica 2 dB abaixo do dos sons gerados de antes (densos, eles soam mais fortes); no jogo, todas as
# vozes do Oco ainda passam pelo volume do SonsDoOco.asset (a pedido: "o de antes também era um pouco alto").
# Uso: python oco_gravado.py <pasta das gravações> <pasta de saída>
#   (no jogo: python oco_gravado.py "C:/Users/pedro/Videos" ../Assets/_Game/Audio/Oco; depois, o menu Zombie City
#   Cleaner > Zumbis > Montar os sons do Oco)
#   (na pasta: "VOZ PADRÃO.mp4", "Golpe ataque.mp4", "Agonia no sol.mp4" e "Dor no tiro.mp4", ou .wav/.m4a com o mesmo nome)
import os
import sys
import subprocess
import tempfile
import numpy as np
import scipy.signal as sg
import scipy.io.wavfile as wf
from scipy.ndimage import uniform_filter1d

SR = 44100
AQUI = os.path.dirname(os.path.abspath(__file__))
ATUAIS = os.path.join(AQUI, '..', 'Assets', '_Game', 'Audio', 'Oco')
ABAIXO_DB = -2.0
# o volume dos sons gerados de antes (a voz ativa, a mediana de cada tipo): a referência, que não muda quando os
# gravados ocupam o lugar deles
REFERENCIA_DB = {'Voz': -11.8, 'Golpe': -7.8, 'Agonia': -8.9, 'Tiro': -7.2}

# cada tipo: o arquivo, como separar as tomadas (o buraco entre elas) e o tratamento
TIPOS = {
    'Voz': dict(arquivo='VOZ PADRÃO', buraco=0.3, tons=[-6, -4, -8], rosnado=0.35, sopro=0.30, fry=0.35, saturar=1.8, oco=0.35, sala=0.22),
    'Golpe': dict(arquivo='Golpe ataque', buraco=0.12, tons=[-5], rosnado=0.45, sopro=0.35, fry=0.45, saturar=2.6, oco=0.25, sala=0.12),
    'Agonia': dict(arquivo='Agonia no sol', buraco=0.25, tons=[-3, -5], rosnado=0.28, sopro=0.40, fry=0.25, saturar=2.0, oco=0.30, sala=0.22),
    'Tiro': dict(arquivo='Dor no tiro', buraco=0.12, tons=[-4], rosnado=0.35, sopro=0.30, fry=0.30, saturar=2.2, oco=0.25, sala=0.12),
}


def ler(caminho):
    """Qualquer áudio ou vídeo vira mono, 44,1 kHz (pelo ffmpeg)."""
    with tempfile.TemporaryDirectory() as d:
        wav = os.path.join(d, 'a.wav')
        subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', caminho, '-vn', '-ac', '1', '-ar', str(SR), '-c:a', 'pcm_s16le', wav], check=True)
        _, x = wf.read(wav)
    return x.astype(np.float32) / 32768.0


def tomadas(x, buraco, minimo=0.15, abaixo=30.0):
    """Os trechos com voz (o fundo das gravações é silêncio: o celular já corta o ruído), juntando os buracos curtos."""
    hop = int(0.01 * SR)
    fr = np.array([np.sqrt(np.mean(x[i:i + hop] ** 2)) + 1e-12 for i in range(0, len(x) - hop, hop)])
    ativo = 20 * np.log10(fr) > 20 * np.log10(fr.max()) - abaixo
    seg, i, n = [], 0, len(ativo)
    while i < n:
        if not ativo[i]:
            i += 1
            continue
        j = i
        while True:
            while j < n and ativo[j]:
                j += 1
            k = j
            while k < n and not ativo[k] and (k - j) * 0.01 < buraco:
                k += 1
            if k < n and ativo[k] and (k - j) * 0.01 < buraco:
                j = k
                continue
            break
        if (j - i) * 0.01 >= minimo:
            a, b = max(0, int((i * 0.01 - 0.04) * SR)), min(len(x), int((j * 0.01 + 0.15) * SR))
            seg.append(x[a:b].copy())
        i = j
    return seg


def tom(x, semitons, formante='shifted'):
    """Muda a altura sem mudar a duração (o rubberband do ffmpeg); 'shifted': os formantes descem junto (o corpo maior)."""
    pad = np.zeros(int(0.3 * SR), np.float32)
    with tempfile.TemporaryDirectory() as d:
        a, b = os.path.join(d, 'a.wav'), os.path.join(d, 'b.wav')
        wf.write(a, SR, (np.clip(np.concatenate([pad, x, pad]), -1, 1) * 32767).astype(np.int16))
        r = 2 ** (semitons / 12.0)
        subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', a, '-af',
                        f'rubberband=pitch={r:.6f}:formant={formante}:pitchq=quality:transients=smooth', b], check=True)
        _, y = wf.read(b)
    y = y.astype(np.float32) / 32768.0
    y = y[len(pad):len(pad) + len(x)]
    return np.pad(y, (0, len(x) - len(y)))


def sopro(x, rng):
    """O chiado rouco: ruído com o envelope do espectro da voz (as vogais ficam, a altura some)."""
    _, _, X = sg.stft(x, SR, nperseg=1024, noverlap=768)
    env = uniform_filter1d(np.abs(X), 11, axis=0)
    fase = np.exp(1j * rng.uniform(0, 2 * np.pi, X.shape))
    _, y = sg.istft(env * fase, SR, nperseg=1024, noverlap=768)
    y = y[:len(x)].astype(np.float32)
    y = np.pad(y, (0, len(x) - len(y)))
    return passa(y, 'high', 700)


def fry(x, rng, prof, f=(26.0, 44.0)):
    """O tremor da garganta: pulsos irregulares (cada um com a sua força), como o rosnado do fundo da garganta."""
    n = len(x)
    e = np.zeros(n, np.float32)
    t = 0.0
    while t < n / SR:
        i = int(t * SR)
        L = int(SR / rng.uniform(*f))
        forca = rng.uniform(0.4, 1.0)
        janela = np.hanning(min(L, n - i)) * forca
        e[i:i + len(janela)] = np.maximum(e[i:i + len(janela)], janela)
        t += L / SR
    return x * (1 - prof + prof * e)


def passa(x, tipo, f, ordem=2):
    return sg.sosfilt(sg.butter(ordem, f, btype=tipo, fs=SR, output='sos'), x).astype(np.float32)


def pente(x, f, g):
    d = max(1, int(SR / f))
    a = np.zeros(d + 1)
    a[0], a[d] = 1.0, -g
    return sg.lfilter([1.0], a, x).astype(np.float32)


def oco(x, mistura, g=0.55):
    """A ressonância de um corpo oco: três tubos em paralelo, de alturas que não casam."""
    y = sum(pente(x, f, g) for f in (167.0, 233.0, 311.0)) / 3.0
    y = passa(y, 'low', 3800)
    y = y / (np.abs(y).max() + 1e-9) * (np.abs(x).max() + 1e-9)
    return (1 - mistura) * x + mistura * y


def sala(x, mistura, rng, rt=0.55):
    n = int(rt * 1.4 * SR)
    t = np.arange(n) / SR
    ir = rng.standard_normal(n) * np.exp(-6.9 * t / rt)
    ir = passa(ir.astype(np.float32), 'low', 4500)
    ir /= np.sqrt((ir ** 2).sum())
    cauda = np.zeros(len(x) + n, np.float32)
    cauda[:len(x)] = x
    molhado = sg.oaconvolve(cauda, ir)[:len(cauda)]
    seco = cauda
    return (1 - mistura) * seco + mistura * molhado / (np.abs(molhado).max() + 1e-9) * (np.abs(x).max() + 1e-9)


def oco_de(x, cfg, semitons, rng):
    x = x / (np.abs(x).max() + 1e-9)
    corpo = tom(x, semitons)
    rosnado = passa(tom(x, semitons - 7), 'low', 900)
    rosnado /= np.abs(rosnado).max() + 1e-9
    ar = sopro(corpo, rng)
    ar /= np.abs(ar).max() + 1e-9
    y = corpo + cfg['rosnado'] * rosnado + cfg['sopro'] * ar
    y = fry(y, rng, cfg['fry'])
    y = np.tanh(cfg['saturar'] * y / (np.abs(y).max() + 1e-9)) / np.tanh(cfg['saturar'])
    y = oco(y, cfg['oco'])
    # o tom: tira o grave que embola, realça o peito (150 a 350 Hz) e abafa um pouco o agudo
    y = passa(y, 'high', 70)
    y = y + 0.35 * passa(y, 'band', [150, 350])
    y = passa(y, 'low', 7000)
    y = sala(y, cfg['sala'], rng)
    # entra e sai sem estalo; a cauda some devagar
    m = int(0.012 * SR)
    y[:m] *= np.linspace(0, 1, m)
    q = int(0.25 * SR)
    y[-q:] *= np.linspace(1, 0, q) ** 2
    return y.astype(np.float32)


def rms_ativo(x):
    hop = int(0.02 * SR)
    fr = np.array([np.sqrt(np.mean(x[i:i + hop] ** 2)) for i in range(0, len(x) - hop, hop)])
    return float(np.sqrt(np.mean(fr[fr > fr.max() * 0.2] ** 2)))


def achar(pasta, nome):
    for ext in ('.mp4', '.m4a', '.wav', '.mp3', '.ogg'):
        p = os.path.join(pasta, nome + ext)
        if os.path.exists(p):
            return p
    raise FileNotFoundError(f'{nome} em {pasta}')


if __name__ == '__main__':
    entrada, saida = sys.argv[1], sys.argv[2]
    os.makedirs(saida, exist_ok=True)
    for tipo, cfg in TIPOS.items():
        rng = np.random.default_rng(list(TIPOS).index(tipo) + 11)
        x = ler(achar(entrada, cfg['arquivo']))
        partes = tomadas(x, cfg['buraco'])
        alvo = 10 ** ((REFERENCIA_DB[tipo] + ABAIXO_DB) / 20)
        k = 0
        for semitons in cfg['tons']:
            for p in partes:
                y = oco_de(p, cfg, semitons, rng)
                y *= alvo / (rms_ativo(y) + 1e-9)
                pico = np.abs(y).max()
                if pico > 0.89:   # o pico passa do limite: segura com a curva (sem cortar)
                    y = np.where(np.abs(y) > 0.6, np.sign(y) * (0.6 + 0.29 * np.tanh((np.abs(y) - 0.6) / 0.29)), y)
                k += 1
                wf.write(os.path.join(saida, f'Oco_{tipo}_{k:02d}.wav'), SR, (y * 32767).astype(np.int16))
        print(f'{tipo}: {len(partes)} tomadas x {len(cfg["tons"])} tons = {k} sons (volume do jogo {20 * np.log10(alvo):.1f} dB)')
