# A trilha da introdução do Dia 0 (01/11/2026, a pedido: "uma trilha sonora boa estilo Hollywood e uma animação que se
# encaixe em tudo isso"). Em ré menor, com o tema do jogo (lá, sol, fá, mi), no tempo da introdução (IntroDoDiaZero.cs):
#   0-8 s     a TV ligando na festa: o piano entra sozinho, com o tema;
#   8-24 s    o Brasil, os primeiros sintomas: as cordas por baixo, o grave e o coração;
#   24-40 s   o homem de pedra: o piano ao contrário, o pizzicato, o coro longe, o sino e os tímpanos;
#   40-56 s   as estátuas pelo país: os "braams" (o metal grave do cinema) a cada compasso, os tambores, a subida;
#   56-72 s   o dia 30, o ataque: o tema nas cordas e no coro, os braams e os tambores em tudo; corta seco;
#   72-92 s   os Estados Unidos, o nosso estado: o silêncio, o coração, o piano sem resolver e o último braam.
# Uso (nesta pasta): python intro_dia0.py   (sai em saida/ e vai para Assets/_Game/Audio/DiaZero/IntroDoDiaZero.ogg)
import os
import subprocess
import numpy as np
import scipy.signal as sg
import instrumentos as I
from estudio import Peca, renderizar, SR

AQUI = os.path.dirname(os.path.abspath(__file__))
DESTINO = os.path.normpath(os.path.join(AQUI, '..', '..', 'Assets', '_Game', 'Audio', 'DiaZero', 'IntroDoDiaZero.ogg'))

TEMA_1 = [(69, 2), (67, 1), (65, 1), (64, 4)]
TEMA_2 = [(69, 2), (67, 1), (65, 1), (62, 4)]
TEMA_4 = [(69, 2), (67, 1), (65, 1), (61, 4)]


def braam(f, d, v, ex):
    """O "braam" do cinema: o metal grave em pilha (a nota, a oitava de baixo e a quinta), com o rosnado (o tremor rápido),
    o filtro abrindo no ataque e a saturação."""
    at, so = ex.get('ataque', 0.08), ex.get('solta', 2.5)
    n = int((d + so) * SR)
    t = np.arange(n, dtype=np.float32) / SR
    x = np.zeros(n, np.float32)
    for r, w in [(0.5, 0.9), (1.0, 1.0), (1.5, 0.45), (2.0, 0.35), (3.0, 0.12)]:
        for k in range(3):
            fase = I._rng.uniform(0, 1)
            desaf = 1 + (k - 1) * 0.006
            ph = f * r * desaf * t + fase
            x += w * (2 * (ph - np.floor(ph)) - 1)
    rosnado = 0.75 + 0.25 * np.sin(2 * np.pi * ex.get('rosnado', 31.0) * t)
    abre = 300 + 2600 * np.exp(-t * 2.2) + 500 * np.minimum(t / 0.05, 1)
    # o filtro que fecha: em pedaços de 50 ms (o passa-baixa que muda no tempo)
    y = np.zeros(n, np.float32)
    passo = int(0.05 * SR)
    zi = None
    for i in range(0, n, passo):
        c = float(abre[min(i, n - 1)])
        sos = sg.butter(2, min(c, SR * 0.45), btype='low', fs=SR, output='sos')
        if zi is None:
            zi = sg.sosfilt_zi(sos) * 0
        bloco, zi = sg.sosfilt(sos, x[i:i + passo], zi=zi)
        y[i:i + passo] = bloco
    y = np.tanh(y * rosnado * 0.35) * 1.6
    return (y * I.env(n, at, d, so) * v).astype(np.float32)


def tambor(f, d, v, ex):
    """O tambor grande (o taiko): o baque que cai de tom, a pele e o estalo."""
    n = int(1.6 * SR)
    t = np.arange(n, dtype=np.float32) / SR
    giro = f * t + f * 1.2 * (1 - np.exp(-t * 18)) / 18
    corpo = np.sin(2 * np.pi * giro) * np.exp(-t * 4.5)
    pele = I.filtrar(I._rng.uniform(-1, 1, n).astype(np.float32), 'band', [120, 900]) * np.exp(-t * 14) * 0.9
    estalo = I.filtrar(I._rng.uniform(-1, 1, n).astype(np.float32), 'high', 2500) * np.exp(-t * 80) * 0.4
    return ((corpo + pele + estalo) * np.minimum(t * 800, 1) * v).astype(np.float32)


I.INSTRUMENTOS['braam'] = braam
I.INSTRUMENTOS['tambor'] = tambor

AC = {'Dm': [50, 53, 57], 'Bb': [46, 50, 53, 58], 'Gm': [43, 50, 55, 58], 'A': [45, 52, 57, 61], 'F': [41, 48, 53, 57]}
GRAVE = {'Dm': 38, 'Bb': 34, 'Gm': 31, 'A': 33, 'F': 29}


def compor():
    I.sorte(2026)
    p = Peca('IntroDoDiaZero', 60)
    T, B = p.T, p.b
    piano = dict(desafinado=5.0, corte=6000)

    # 0-8 s: o piano sozinho (a TV ligando)
    p.frase('piano', 1, 0, TEMA_1, 0.16, lado=-0.1, eco=0.55, pedal=0.8, **piano)
    p.add('piano', T(1), B(4), 50, 0.08, lado=-0.3, eco=0.55, **piano)
    p.add('piano', T(1), B(4), 57, 0.07, lado=-0.3, eco=0.55, **piano)

    # 8-24 s: o Brasil (as cordas, o grave, o coração a partir do compasso 4)
    for c, a in zip(range(2, 6), ['Dm', 'Bb', 'Gm', 'A']):
        p.acorde('cordas', T(c), B(4) + 0.3, AC[a], 0.05, abertura=0.6, eco=0.6, ataque=1.4, solta=1.6)
        p.add('baixo', T(c), B(4), GRAVE[a], 0.12, eco=0.3, ataque=1.0, solta=1.2)
    p.frase('piano', 2, 0, TEMA_2, 0.14, lado=-0.1, eco=0.55, pedal=0.8, **piano)
    p.frase('piano', 4, 0, TEMA_4, 0.14, lado=-0.1, eco=0.55, pedal=0.8, **piano)
    for c in range(4, 6):
        for k in range(4):
            t = T(c, k)
            p.add('coracao', t, 0, 1.0, 0.16, eco=0.25)
            p.add('coracao', t + 0.27, 0, 1.25, 0.11, eco=0.25)

    # 24-40 s: o homem de pedra (o reverso, o pizzicato, o coro longe, o sino, os tímpanos)
    p.add('reverso', T(6) - 2.4, 2.4, 62, 0.22, lado=0.2, eco=0.6, **piano)
    p.add('sino', T(6), 0, 50, 0.07, lado=-0.5, eco=0.85, longo=1.6)
    for c, a in zip(range(6, 10), ['Dm', 'Dm', 'Bb', 'A']):
        p.add('baixo', T(c), B(4), GRAVE[a], 0.13, eco=0.3, ataque=0.5, solta=1.0)
        for k in range(8):
            m = AC[a][k % len(AC[a])] + 12
            p.add('pizz', T(c, k * 0.5), 0, m, 0.07 + 0.03 * (k % 4 == 0), lado=0.25 * (1 if k % 2 else -1), eco=0.3)
    p.acorde('coro', T(7), B(12), [62, 65, 69], 0.035, abertura=0.6, eco=0.85, vogal='u', ataque=3, solta=3)
    p.add('cello', T(6), B(16), 38, 0.06, eco=0.4, ataque=2.0, solta=2.0)
    for c in (7, 8, 9):
        p.add('timpano', T(c), 0, 38, 0.16, eco=0.4, decai=1.8)
        p.add('timpano', T(c, 3.5), 0, 38, 0.08, eco=0.4, decai=2.5)
    p.add('subida', T(9, 1), B(3), 220, 0.06, eco=0.5)

    # 40-56 s: as estátuas pelo país (os braams, os tambores, as cordas em tremolo, a subida)
    for c, a in zip(range(10, 14), ['Dm', 'Bb', 'Gm', 'A']):
        forca = 0.13 + 0.03 * (c - 10)
        p.add('braam', T(c), B(3), GRAVE[a] + 12, forca, eco=0.45, solta=2.2)
        p.acorde('cordas', T(c), B(4), [m + 12 for m in AC[a]], 0.045, abertura=0.7, eco=0.55, ataque=0.3, solta=0.6, tremolo=12, prof=0.7)
        for k in (0, 1.5, 2, 3, 3.5):
            p.add('tambor', T(c, k), 0, 55, 0.22 if k == 0 else 0.12, lado=0.1 * (k - 2), eco=0.35)
    p.add('subida', T(12), B(8), 160, 0.09, eco=0.5)
    p.acorde('coro', T(12), B(8), [62, 65, 70], 0.045, abertura=0.6, eco=0.8, vogal='o', ataque=2, solta=1.5)

    # 56-72 s: o ataque (o tema no alto, o coro, os braams de dois em dois tempos, os tambores)
    for c, a in zip(range(14, 18), ['Dm', 'Bb', 'Gm', 'A']):
        for k in (0, 2):
            p.add('braam', T(c, k), B(2), GRAVE[a] + 12, 0.19, eco=0.45, solta=1.4)
        p.acorde('cordas', T(c), B(4), [m + 12 for m in AC[a]] + [AC[a][0] + 24], 0.06, abertura=0.8, eco=0.55, ataque=0.15, solta=0.5)
        p.acorde('coro', T(c), B(4), [m + 12 for m in AC[a]], 0.05, abertura=0.7, eco=0.8, vogal='a', ataque=0.4, solta=0.6)
        for k in (0, 0.75, 1.5, 2, 2.75, 3, 3.5):
            p.add('tambor', T(c, k), 0, 52, 0.25 if k in (0, 2) else 0.13, lado=0.15 * (k - 1.75), eco=0.35)
        p.add('timpano', T(c), 0, GRAVE[a], 0.2, eco=0.4, decai=2.2)
    p.frase('cordas', 14, 0, [(81, 2), (79, 1), (77, 1), (76, 4), (81, 2), (79, 1), (77, 1), (73, 4)], 0.07, lado=0.15, eco=0.6, ataque=0.2, solta=0.6)
    p.frase('cello', 14, 0, [(57, 2), (55, 1), (53, 1), (52, 4), (57, 2), (55, 1), (53, 1), (49, 4)], 0.09, lado=-0.2, eco=0.5)
    p.add('subida', T(17), B(4) - 0.05, 300, 0.12, eco=0.4)

    # 72-92 s: o silêncio, o coração, o piano sem resolver (o nosso estado) e o último braam
    for k in range(4):
        t = T(18, k)
        p.add('coracao', t, 0, 0.95, 0.2, eco=0.3)
        p.add('coracao', t + 0.28, 0, 1.2, 0.14, eco=0.3)
    p.add('piano', T(18, 0.5), B(3), 69, 0.14, lado=-0.1, eco=0.7, **piano)
    p.add('baixo', T(19), B(8), 38, 0.12, eco=0.3, ataque=2.0, solta=2.0, batimento=0.4)
    p.add('metal', T(19), B(8), 81, 0.025, lado=0.5, eco=0.8, ataque=3, solta=3)
    p.frase('piano', 19, 0, TEMA_4, 0.15, lado=-0.1, eco=0.6, pedal=1.0, **piano)
    p.add('reverso', T(21) - 2.0, 2.0, 50, 0.25, eco=0.6, **piano)
    p.add('braam', T(21), B(4), 38, 0.22, eco=0.6, solta=5.0, rosnado=26)
    p.add('tambor', T(21), 0, 48, 0.3, eco=0.5)
    p.add('timpano', T(21), 0, 38, 0.24, eco=0.5, decai=1.0)
    p.acorde('cordas', T(21), B(5), [38, 50, 57, 62], 0.06, abertura=0.6, eco=0.8, ataque=0.1, solta=4.0)
    return p


def main():
    p = compor()
    wav, _ = renderizar(p, 88.0, eco_cfg=(4.0, 2.4, 5.0, 0.04), molhado=0.5, alvo_db=-17.0, cauda=4.0, saida=os.path.join(AQUI, 'saida'))
    os.makedirs(os.path.dirname(DESTINO), exist_ok=True)
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', wav, '-c:a', 'libvorbis', '-q:a', '6', DESTINO], check=True)
    print('->', DESTINO)


if __name__ == '__main__':
    main()
