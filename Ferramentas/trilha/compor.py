# A trilha do Zombie City Cleaner (a pedido em 29/10/2026): solidão, angústia e receio, a cidade vazia na neblina.
# Tudo em ré menor, a partir de um tema só: quatro notas descendo (lá, sol, fá, mi), a solidão do jogador.
# Uso: python compor.py [pasta de saída] [peças...]   (sem peças: todas)
import sys
import instrumentos as I
from estudio import Peca, renderizar

SAIDA = sys.argv[1] if len(sys.argv) > 1 else 'saida'
QUAIS = set(sys.argv[2:])

# o tema (as notas MIDI e os tempos)
TEMA_1 = [(69, 2), (67, 1), (65, 1), (64, 4)]        # lá sol fá | mi (sobre si bemol: a angústia)
TEMA_2 = [(69, 2), (67, 1), (65, 1), (62, 4)]        # lá sol fá | ré (a solidão)
TEMA_3 = [(65, 1), (67, 1), (69, 2), (72, 2), (70, 2)]  # fá sol lá dó si bemol (a esperança que cai)
TEMA_4 = [(69, 2), (67, 1), (65, 1), (61, 4)]        # lá sol fá | dó sustenido, sem resolver (o receio)

PIANO = dict(desafinado=7.0, corte=4500)


def neblina():
    """O dia na neblina (exploração): o piano velho sozinho, a voz longe, o violoncelo, gotas de piano no agudo."""
    I.sorte(11)
    p = Peca('ZCC_01_Neblina', 56)
    T, B = p.T, p.b
    total = T(38)
    p.add('vento', 0, total, None, 0.05, eco=0.15, corte=450, ataque=6, solta=7)
    for c in (9, 20, 33):
        p.add('vento', T(c), B(16), None, 0.03, eco=0.2, corte=950, ataque=4, solta=5)
    # o começo: o vento, um grave que chega, o metal frio, o sino longe
    p.add('baixo', T(1), T(14) - T(1), 38, 0.15, eco=0.3, ataque=6, solta=3)
    p.acorde('cordas', T(2), B(40), [50, 57], 0.035, abertura=0.3, eco=0.6, ataque=6, solta=4, brilho=0.6)
    p.add('metal', T(2, 2), 7.0, 81, 0.05, lado=0.55, eco=0.7, ataque=2.5, solta=3)
    p.add('sino', T(3, 2), 0, 62, 0.06, lado=-0.6, eco=0.85, longo=1.3)
    # A: o piano sozinho, com espaço entre as frases
    mel = dict(lado=-0.1, eco=0.5, pedal=0.9, **PIANO)
    p.frase('piano', 5, 0, TEMA_1, 0.42, **mel)
    p.frase('piano', 8, 0, TEMA_2, 0.42, **mel)
    p.frase('piano', 11, 0, TEMA_3 + [(69, 4)], 0.40, **mel)
    mao = dict(lado=-0.25, eco=0.5, **PIANO)
    for c, notas in [(5, [50, 57]), (6, [46, 53]), (8, [43, 50, 58]), (9, [38, 45, 53]), (11, [41, 48, 57]), (12, [46, 53, 62]), (13, [45, 52, 61]), (14, [50, 53])]:
        for m in notas:
            p.add('piano', T(c), B(4) + 0.6, m, 0.18 if c != 14 else 0.13, **mao)
    gota = dict(lado=0.35, eco=0.75, desafinado=7.0, corte=6000)
    p.add('piano', T(7, 2), 2.0, 86, 0.13, **gota)
    p.add('piano', T(10, 1), 2.0, 81, 0.11, **gota)
    # B: entram as cordas (baixinho), o baixo e a voz distante
    for c, notas, b in [(15, [50, 53, 57], 38), (17, [46, 50, 53, 57], 34), (19, [43, 46, 50, 52], 31), (21, [45, 49, 52, 55], 33)]:
        p.acorde('cordas', T(c), B(8), notas, 0.042, abertura=0.6, eco=0.6, ataque=2.5, solta=3)
        p.add('baixo', T(c), B(8), b, 0.14, eco=0.3, ataque=1.5, solta=2)
        p.add('piano', T(c), B(8) + 0.5, b + 12, 0.19, **mao)
    p.frase('piano', 15, 0, TEMA_1 + TEMA_2 + TEMA_3 + TEMA_4, 0.40, **mel)
    p.add('coro', T(19), B(8), 74, 0.045, lado=0.35, eco=0.85, vogal='u', ataque=3, solta=3)
    p.add('coro', T(21), B(8), 73, 0.045, lado=0.35, eco=0.85, vogal='u', ataque=3, solta=4)
    # C: o violoncelo carrega o tema no grave; o piano cai em gotas, no agudo
    p.frase('cello', 23, 0, [(57, 2), (55, 1), (53, 1), (52, 4), (None, 4), (57, 2), (55, 1), (53, 1), (50, 4),
                              (53, 1), (55, 1), (57, 2), (60, 2), (58, 2), (57, 4)], 0.30, lado=-0.3, eco=0.45)
    for c, notas, b in [(23, [50, 57], 38), (25, [46, 53, 57], 34), (27, [43, 50, 58], 31), (29, [45, 52, 61], 33)]:
        p.acorde('cordas', T(c), B(8), notas, 0.038, abertura=0.5, eco=0.6, ataque=2.5, solta=3)
        p.add('baixo', T(c), B(8), b, 0.13, eco=0.3, ataque=1.5, solta=2)
    for c, tempo, m in [(23, 1, 86), (24, 3, 81), (26, 0, 77), (27, 2, 76), (29, 1, 86), (30, 2, 85)]:
        p.add('piano', T(c, tempo), 2.0, m, 0.12, **gota)
    p.add('metal', T(25), 10.0, 88, 0.03, lado=0.6, eco=0.8, ataque=3, solta=4)
    p.add('metal', T(25, 2), 9.0, 89, 0.018, lado=0.7, eco=0.8, ataque=3, solta=4)
    p.add('sino', T(28), 0, 57, 0.05, lado=-0.5, eco=0.85, longo=1.5)
    # o fim: o tema começa e não termina; o dó sustenido sozinho, sem resolver
    p.frase('piano', 31, 0, [(69, 2), (67, 1), (65, 1)], 0.36, **mel)
    p.add('piano', T(33), B(6) + 1.0, 61, 0.30, **mel)
    p.add('piano', T(33), B(6) + 1.0, 45, 0.18, **mao)
    p.acorde('cordas', T(31), B(14), [50, 53, 57, 64], 0.034, abertura=0.6, eco=0.65, ataque=3, solta=6)
    p.add('baixo', T(31), B(16), 38, 0.12, eco=0.3, ataque=2, solta=5)
    p.add('sino', T(35), 0, 62, 0.05, lado=0.5, eco=0.85, longo=1.3)
    return renderizar(p, total, eco_cfg=(5.5, 3.2, 7.0, 0.05), molhado=0.6, alvo_db=-21, cauda=5, saida=SAIDA)


def noite():
    """A noite: mais lenta e grave; o tema no violoncelo com o mi bemol (o frígio: o pavor), a voz, o coração longe, o
    sino da igreja, o vidro que escorrega para baixo."""
    I.sorte(23)
    p = Peca('ZCC_02_Noite', 46)
    T, B = p.T, p.b
    total = T(28)
    p.add('vento', 0, total, None, 0.06, eco=0.15, corte=300, ataque=6, solta=6)
    for c in (6, 15, 22):
        p.add('vento', T(c), B(12), None, 0.03, eco=0.2, corte=700, ataque=4, solta=4)
    p.add('baixo', T(0), T(25), 38, 0.16, eco=0.3, ataque=8, solta=8, batimento=0.45)
    p.acorde('cordas', T(2), B(24), [38, 39], 0.025, abertura=0.3, eco=0.5, ataque=6, solta=6, brilho=0.5)
    p.add('metal', T(1), B(20), 81, 0.03, lado=0.5, eco=0.8, ataque=4, solta=5)
    p.add('metal', T(1, 2), B(18), 82, 0.022, lado=0.6, eco=0.8, ataque=4, solta=5)
    p.frase('cello', 4, 0, [(57, 2), (55, 1), (53, 1), (51, 4), (None, 4), (57, 2), (55, 1), (53, 1), (50, 4), (None, 4),
                             (58, 2), (57, 2), (51, 4)], 0.28, lado=-0.25, eco=0.55)
    p.acorde('coro', T(8), B(16), [62, 65, 70], 0.03, abertura=0.5, eco=0.85, vogal='u', ataque=5, solta=6)
    for c, v, lado in [(10, 0.07, -0.4), (21, 0.06, 0.4), (27, 0.05, 0.0)]:
        p.add('sino', T(c), 0, 50, v, lado=lado, eco=0.85, longo=1.6)
    # o coração, longe, que chega e vai embora
    t, k = T(12), 0
    while t < T(20):
        u = (t - T(12)) / (T(20) - T(12))
        v = 0.16 * (1 - abs(2 * u - 1)) ** 0.7
        p.add('coracao', t, 0, 1.0, v, eco=0.35)
        p.add('coracao', t + 0.28, 0, 1.25, v * 0.7, eco=0.35)
        t += 1.15
        k += 1
    for c in (13, 15, 17):
        p.add('piano', T(c), B(4), 38, 0.14, eco=0.6, corte=900, desafinado=10.0)
        p.add('piano', T(c) + 0.05, B(4), 33, 0.11, eco=0.6, corte=900, desafinado=10.0)
    for c, notas in [(12, [43, 46, 50]), (16, [39, 43, 46, 50]), (20, [38, 45, 53])]:
        p.acorde('cordas', T(c), B(16), notas, 0.034, abertura=0.5, eco=0.6, ataque=4, solta=5)
    p.frase('cello', 16, 0, [(69, 2), (67, 1), (65, 1), (63, 4), (None, 4), (69, 2), (67, 1), (65, 1), (62, 4), (None, 8),
                              (63, 3), (62, 5)], 0.24, lado=0.25, eco=0.55)
    p.add('metal', T(17), B(14), 88, 0.03, lado=-0.5, eco=0.8, ataque=3, solta=4, curva=-1.0)
    p.acorde('coro', T(18), B(12), [63, 67, 70], 0.025, abertura=0.5, eco=0.85, vogal='o', ataque=4, solta=5)
    return renderizar(p, total, eco_cfg=(7.0, 3.5, 9.0, 0.06), molhado=0.65, alvo_db=-23, cauda=6, saida=SAIDA)


def perigo(nivel):
    """O perigo, em camadas no mesmo trecho (e no mesmo andamento, para trocar de uma para a outra sem emenda):
    1 a tensão (as cordas graves em colcheias, o tímpano), 2 a perseguição (+ o coração, o tema no violoncelo, o metal
    raspando), 3 a horda (+ as cordas tremendo no agudo, o coro, os rufos)."""
    I.sorte(31)
    p = Peca(f'ZCC_03_Perigo_nivel{nivel}', 88)
    T, B = p.T, p.b
    plano = [38] * 4 + [34] * 2 + [33] * 2 + [38] * 4 + [34] * 2 + [36] * 2 + [38] * 4 + [34] * 2 + [33] * 2
    total = T(len(plano))
    acentos = [1, .55, .75, .55, .9, .55, .8, .6]
    for c, r in enumerate(plano):
        for k in range(8):
            m = r + 1 if k == 6 else r
            p.add('cordas', T(c, k * 0.5), B(0.38), m, 0.2 * acentos[k], lado=-0.2, eco=0.25, ataque=0.008, solta=0.09, brilho=1.0)
            p.add('pizz', T(c, k * 0.5), 0, m + 12, 0.10 * acentos[k], lado=0.2, eco=0.25)
        p.add('timpano', T(c), 0, r, 0.2, eco=0.3, decai=2.5)
    # o grave segurando cada raiz
    i = 0
    while i < len(plano):
        j = i
        while j < len(plano) and plano[j] == plano[i]:
            j += 1
        p.add('baixo', T(i), B(4 * (j - i)), plano[i], 0.13, eco=0.25, ataque=0.3, solta=0.8)
        if nivel >= 3:
            topo = {38: [74, 77, 81], 34: [74, 77, 82], 33: [73, 76, 81], 36: [72, 76, 79]}[plano[i]]
            p.acorde('cordas', T(i), B(4 * (j - i)), topo, 0.04, abertura=0.6, eco=0.4, ataque=0.3, solta=0.8, tremolo=11, prof=0.7, brilho=1.0)
        i = j
    p.acorde('cordas', T(0), B(32), [69, 70], 0.022, abertura=0.5, eco=0.6, ataque=3, solta=3)
    p.acorde('cordas', T(12), B(32), [69, 70], 0.022, abertura=0.5, eco=0.6, ataque=3, solta=3)
    if nivel >= 2:
        for c in range(len(plano)):
            for tempo in (0, 2):
                p.add('coracao', T(c, tempo), 0, 1.0, 0.2, eco=0.2)
                p.add('coracao', T(c, tempo) + 0.26, 0, 1.25, 0.14, eco=0.2)
            p.add('timpano', T(c, 3), 0, plano[c], 0.09, eco=0.3, decai=4)
            p.add('timpano', T(c, 3.5), 0, plano[c], 0.11, eco=0.3, decai=4)
        for c0 in (4, 8, 12, 16, 20):
            p.frase('cello', c0, 0, [(57, 1), (55, 0.5), (53, 0.5), (52, 2), (57, 1), (55, 0.5), (53, 0.5), (50, 2)],
                    0.3, lado=0.3, eco=0.35, ataque=0.03)
        for c in (6, 14, 22):
            p.add('subida', T(c), B(8), 57, 0.13, lado=0.3, eco=0.5)
    if nivel >= 3:
        p.acorde('coro', T(8), B(16), [69, 74], 0.045, abertura=0.4, eco=0.6, vogal='a', ataque=1, solta=2)
        p.acorde('coro', T(16), B(16), [74, 77], 0.045, abertura=0.4, eco=0.6, vogal='a', ataque=1, solta=2)
        for c, r in enumerate(plano):
            for tempo in range(4):
                p.add('cello', T(c, tempo), B(0.8), r + 12, 0.12, lado=-0.35, eco=0.25, ataque=0.02, solta=0.15)
        for c in (7, 15, 23):
            for k in range(8):
                p.add('timpano', T(c, 2 + k * 0.25), 0, plano[c], 0.07 + 0.025 * k, eco=0.3, decai=4)
    alvo = {1: -21, 2: -19, 3: -17}[nivel]
    return renderizar(p, total, eco_cfg=(2.6, 1.6, 3.5, 0.02), molhado=0.35, alvo_db=alvo, cauda=3, saida=SAIDA)


def bunker():
    """O bunker: o tema em ré maior, para respirar e pensar; o piano quente, as cordas, o violoncelo e, no fim, uma
    caixinha de música (a família)."""
    I.sorte(47)
    p = Peca('ZCC_04_Bunker', 64)
    T, B = p.T, p.b
    total = T(30)
    seq = ['D', 'D', 'D', 'Bm', 'G', 'D', 'F#m', 'G', 'Em', 'A', 'G', 'D/F#', 'Em', 'A', 'G', 'Gm', 'D', 'A',
           'D', 'Bm', 'G', 'Gm', 'D', 'A']
    ARP = {'D': [50, 57, 62, 66], 'Bm': [47, 54, 59, 62], 'G': [43, 50, 55, 59], 'F#m': [42, 49, 54, 57], 'Em': [40, 47, 52, 55],
           'A': [45, 52, 57, 61], 'D/F#': [42, 50, 57, 62], 'Gm': [43, 50, 55, 58]}
    PAD = {'D': [50, 54, 57], 'Bm': [47, 50, 54], 'G': [43, 47, 50], 'F#m': [42, 45, 49], 'Em': [40, 43, 47], 'A': [45, 49, 52],
           'D/F#': [42, 45, 50], 'Gm': [43, 46, 50]}
    BAIXO = {'D': 38, 'Bm': 35, 'G': 31, 'F#m': 30, 'Em': 28, 'A': 33, 'D/F#': 30, 'Gm': 31}
    quente = dict(desafinado=2.5, corte=3800)
    for c, ch in enumerate(seq):
        v = 0.15 if c < 18 else 0.11
        for k, m in enumerate(ARP[ch]):
            p.add('piano', T(c, k), B(1) + 1.2, m, v * (1.1 if k == 0 else 1), lado=-0.2, eco=0.35, **quente)
        if c >= 2:
            p.add('baixo', T(c), B(4), BAIXO[ch], 0.1, eco=0.25, ataque=0.4, solta=0.8)
        if c >= 10:
            p.acorde('cordas', T(c), B(4), PAD[ch], 0.033 if c < 18 else 0.028, abertura=0.5, eco=0.45, ataque=1.5, solta=2)
    mel = dict(lado=0.05, eco=0.35, pedal=0.7, **quente)
    p.frase('piano', 2, 0, [(69, 2), (67, 1), (66, 1), (64, 4), (69, 2), (67, 1), (66, 1), (62, 4), (66, 1), (67, 1), (69, 2),
                            (74, 2), (73, 2), (71, 2), (69, 2), (69, 4)], 0.4, **mel)
    p.frase('piano', 10, 0, [(74, 2), (73, 1), (71, 1), (69, 4), (71, 2), (69, 1), (67, 1), (66, 4), (67, 1), (69, 1), (71, 2),
                             (70, 2), (69, 2), (66, 2), (64, 2), (64, 4)], 0.38, **mel)
    p.frase('cello', 10, 0, [(59, 4), (57, 4), (55, 4), (57, 4), (59, 2), (62, 2), (58, 4), (57, 4), (52, 2), (57, 2)],
            0.22, lado=0.3, eco=0.4)
    p.frase('caixinha', 18, 0, [(81, 2), (79, 1), (78, 1), (76, 4), (81, 2), (79, 1), (78, 1), (74, 4), (78, 1), (79, 1),
                                (81, 2), (86, 2), (85, 2)], 0.12, lado=0.25, eco=0.55)
    # o fim: ré maior com a nona, devagar
    for k, m in enumerate([50, 57, 64, 66, 69]):
        p.add('piano', T(24, k), B(8), m, 0.2, lado=-0.1, eco=0.4, **quente)
    for k, m in enumerate([62, 66, 69, 76]):
        p.add('piano', T(26, k * 1.5), B(8), m, 0.17, lado=0.1, eco=0.4, **quente)
    p.add('piano', T(28), B(8), 74, 0.15, lado=0.0, eco=0.45, **quente)
    p.acorde('cordas', T(24), B(20), [50, 54, 57, 64], 0.03, abertura=0.5, eco=0.5, ataque=2, solta=6)
    p.add('baixo', T(24), B(20), 38, 0.1, eco=0.25, ataque=1, solta=5)
    p.add('caixinha', T(27, 2), 0, 81, 0.08, lado=0.3, eco=0.6)
    return renderizar(p, total, eco_cfg=(2.4, 1.4, 3.0, 0.02), molhado=0.35, alvo_db=-21, cauda=4, saida=SAIDA)


def visto():
    """Te viram: o piano ao contrário crescendo, o golpe das cordas graves, o tímpano, o metal gritando."""
    I.sorte(53)
    p = Peca('ZCC_05_Momento_Visto', 60)
    for m in (50, 51, 57, 62):
        p.add('reverso', 0.0, 1.0, m, 0.32, lado=(m - 55) / 10, eco=0.4, desafinado=6.0)
    p.acorde('cordas', 1.0, 0.5, [38, 39, 45, 50], 0.13, abertura=0.6, eco=0.6, ataque=0.02, solta=2.5, brilho=1.0)
    p.add('timpano', 1.0, 0, 38, 0.6, eco=0.5, decai=1.5)
    p.add('metal', 1.0, 2.0, 93, 0.07, lado=0.4, eco=0.7, ataque=0.05, solta=1.5, curva=2.0)
    p.add('coro', 1.0, 0.8, 75, 0.05, lado=-0.3, eco=0.7, vogal='a', ataque=0.1, solta=1.5)
    return renderizar(p, 4.5, eco_cfg=(4.5, 2.5, 6.0, 0.02), molhado=0.55, alvo_db=-17, cauda=3, saida=SAIDA)


def estatua():
    """A estátua fechando, perto: o vidro que afunda, as vozes em trítono, o baque da pedra."""
    I.sorte(59)
    p = Peca('ZCC_06_Momento_Estatua', 60)
    for m, v, lado in [(81, 0.05, -0.4), (82, 0.045, 0.4), (88, 0.03, 0.0)]:
        p.add('metal', 0.0, 4.0, m, v, lado=lado, eco=0.8, ataque=0.4, solta=2.0, curva=-1.0)
    p.acorde('coro', 0.2, 3.0, [50, 56], 0.06, abertura=0.3, eco=0.8, vogal='u', ataque=1.0, solta=2.0, curva=-1.0)
    p.add('timpano', 3.5, 0, 38, 0.3, eco=0.6, decai=2)
    p.add('piano', 3.5, 3.0, 26, 0.22, eco=0.6, corte=700, desafinado=8.0)
    p.add('piano', 3.52, 3.0, 33, 0.2, eco=0.6, corte=700, desafinado=8.0)
    return renderizar(p, 6.0, eco_cfg=(5.0, 3.0, 7.0, 0.03), molhado=0.6, alvo_db=-20, cauda=4, saida=SAIDA)


def amanhecer():
    """O amanhecer: as cordas e o coro sobem devagar (ré menor, fá maior) e param num acorde claro, mas inquieto."""
    I.sorte(61)
    p = Peca('ZCC_07_Momento_Amanhecer', 60)
    for t, notas in [(0.0, [50, 57, 64, 65]), (3.5, [53, 57, 60, 65]), (7.0, [46, 53, 57, 62, 64])]:
        p.acorde('cordas', t, 4.0 if t < 7 else 3.0, notas, 0.055, abertura=0.6, eco=0.6, ataque=2.5, solta=4.0)
    for t, m in [(0.0, 69), (3.5, 72), (7.0, 74)]:
        p.add('coro', t, 4.0 if t < 7 else 3.5, m, 0.05, lado=0.2, eco=0.8, vogal='a', ataque=2.0, solta=3.5)
    p.add('sino', 8.0, 0, 81, 0.04, lado=0.5, eco=0.85)
    p.add('piano', 9.5, 3.0, 76, 0.16, lado=-0.3, eco=0.7, desafinado=5.0, corte=5000)
    return renderizar(p, 12.0, eco_cfg=(5.5, 3.2, 7.0, 0.04), molhado=0.6, alvo_db=-21, cauda=4, saida=SAIDA)


def morte():
    """A morte: o golpe grave no piano, o tema no violoncelo que não termina, o sino, o coração parando."""
    I.sorte(67)
    p = Peca('ZCC_08_Momento_Morte', 60)
    for m, v in [(26, 0.4), (38, 0.45), (39, 0.4), (45, 0.4)]:
        p.add('piano', 0.0, 5.0, m, v, eco=0.6, corte=1800, desafinado=9.0)
    p.add('timpano', 0.0, 0, 38, 0.5, eco=0.5, decai=1.2)
    t = 1.2
    for m, d in [(57, 1.2), (55, 0.9), (53, 0.9)]:
        p.add('cello', t, d, m, 0.3, lado=-0.2, eco=0.6)
        t += d
    p.add('sino', 5.0, 0, 50, 0.07, lado=0.3, eco=0.85, longo=1.4)
    for t, v in [(4.5, 0.25), (6.2, 0.18), (8.3, 0.1)]:
        p.add('coracao', t, 0, 1.0, v, eco=0.4)
        p.add('coracao', t + 0.3, 0, 1.25, v * 0.7, eco=0.4)
    return renderizar(p, 9.0, eco_cfg=(5.5, 3.0, 7.0, 0.03), molhado=0.55, alvo_db=-19, cauda=4, saida=SAIDA)


def menu():
    """O menu do jogo (a pedido em 29/10/2026: "algo mais calmo também, transparecendo o medo e a angústia"): devagar, o
    piano velho tocando o tema de longe em longe sobre o ré que não para, as cordas mudando de acorde a cada dois
    compassos, a voz distante, uma caixinha de música um pouco desafinada (a lembrança da família) e o vidro frio. Em
    laço: o fim emenda no começo (o lá do fim chama o ré do começo), sem corte. 32 compassos a 52 bpm (2 min 28 s)."""
    I.sorte(83)
    p = Peca('ZCC_00_Menu', 52)
    T, B = p.T, p.b
    total = T(32)
    alem = 7.0   # o que passa do fim (o vento, o grave, os ecos) entra por baixo do começo
    p.add('vento', 0, total + alem, None, 0.045, eco=0.15, corte=420, ataque=0.01, solta=0.01)
    p.add('baixo', 0, total + alem, 38, 0.11, eco=0.3, ataque=0.01, solta=0.01, batimento=0.35)
    # os acordes, dois compassos cada: a primeira metade em ré menor; a segunda passa pelo mi bemol (a sombra do frígio)
    acordes = [([50, 57, 64], 38), ([46, 53, 57, 62], 34), ([43, 50, 52, 58], 31), ([45, 50, 52, 57], 33),
               ([50, 57, 65], 38), ([46, 50, 57, 62], 34), ([43, 46, 50, 55], 31), ([45, 49, 52, 55], 33),
               ([46, 53, 57, 62], 34), ([41, 48, 57, 60], 29), ([43, 50, 55, 58], 31), ([50, 53, 57, 62], 38),
               ([39, 46, 51, 55], 39), ([46, 50, 53, 58], 34), ([45, 52, 55, 61], 33), ([45, 49, 52, 57], 33)]
    for i, (notas, grave) in enumerate(acordes):
        c = 2 * i
        p.acorde('cordas', T(c), B(8) + 0.5, notas, 0.03, abertura=0.6, eco=0.6, ataque=3.0, solta=4.0, brilho=0.6)
        p.add('baixo', T(c), B(8), grave, 0.07, eco=0.3, ataque=2.0, solta=3.0)
    mel = dict(lado=-0.15, eco=0.6, pedal=1.2, desafinado=8.0, corte=3800)
    mao = dict(lado=-0.3, eco=0.6, desafinado=8.0, corte=3000)
    # o piano: o tema de longe em longe, com espaço (a solidão); a mão esquerda só nos começos de frase
    p.frase('piano', 2, 0, [(69, 2), (67, 1), (65, 1), (64, 6)], 0.30, **mel)
    p.frase('piano', 6, 1, [(69, 2), (67, 1), (65, 1), (62, 6)], 0.28, **mel)
    p.frase('piano', 10, 0, [(65, 1), (67, 1), (69, 2), (72, 2), (70, 4)], 0.27, **mel)
    p.frase('piano', 14, 0, [(69, 2), (67, 1), (65, 1), (61, 6)], 0.30, **mel)   # o dó sustenido, sem resolver
    for c, notas in [(2, [46, 53]), (6, [46, 50]), (10, [41, 48]), (14, [45, 52])]:
        for m in notas:
            p.add('piano', T(c), B(4) + 1.0, m, 0.12, **mao)
    gota = dict(lado=0.4, eco=0.85, desafinado=8.0, corte=5500)
    for c, tempo, m in [(4, 2, 86), (8, 3, 81), (12, 1, 84), (17, 2, 86), (21, 0, 81), (25, 3, 77), (29, 1, 85)]:
        p.add('piano', T(c, tempo), 2.5, m, 0.08, **gota)
    # a segunda metade: a voz longe, a caixinha (a família) e o tema uma oitava acima, mais fraco
    p.add('coro', T(16), B(16), 74, 0.035, lado=0.35, eco=0.9, vogal='u', ataque=4, solta=5)
    p.add('coro', T(24), B(12), 75, 0.03, lado=-0.3, eco=0.9, vogal='u', ataque=4, solta=5)   # o mi bemol
    p.frase('caixinha', 20, 0, [(80.8, 2), (78.8, 1), (76.8, 1), (75.8, 4), (None, 2), (80.8, 2), (78.8, 1), (76.8, 1), (73.8, 4)], 0.07, lado=0.3, eco=0.7)
    p.frase('piano', 26, 0, [(81, 2), (79, 1), (77, 1), (74, 6)], 0.18, **dict(mel, lado=0.2))
    p.add('piano', T(30), B(8) + 2.0, 61, 0.22, **mel)   # o dó sustenido sozinho: chama o ré do começo
    p.add('piano', T(30), B(8) + 2.0, 45, 0.12, **mao)
    # o vidro frio, duas vezes
    p.add('metal', T(8), 9.0, 88, 0.022, lado=0.6, eco=0.85, ataque=3, solta=4)
    p.add('metal', T(23), 10.0, 87, 0.02, lado=-0.6, eco=0.85, ataque=3, solta=4, curva=-1.0)
    p.add('sino', T(12, 2), 0, 50, 0.04, lado=-0.5, eco=0.9, longo=1.5)
    return renderizar_laco(p, total, alem, eco_cfg=(6.0, 3.4, 8.0, 0.05), molhado=0.65, alvo_db=-22, saida=SAIDA)


def renderizar_laco(p, total, alem, eco_cfg, molhado, alvo_db, saida):
    """Em laço: o que passa do fim (o vento, o grave, a cauda dos ecos) entra cruzando por baixo do começo."""
    import numpy as np
    from estudio import mixar, finalizar_sem_fim
    e, d = mixar(p, total + alem, eco_cfg, molhado, cauda=4.0)
    n, m = int(total * I.SR), int(alem * I.SR)
    k = np.sin(np.linspace(0, np.pi / 2, m)) ** 2
    saida_e, saida_d = e[:n].copy(), d[:n].copy()
    saida_e[:m] = e[:m] * k + e[n:n + m] * k[::-1]
    saida_d[:m] = d[:m] * k + d[n:n + m] * k[::-1]
    return finalizar_sem_fim(p.nome, saida_e, saida_d, alvo_db, saida)


PECAS = {'menu': menu, 'neblina': neblina, 'noite': noite, 'perigo1': lambda: perigo(1), 'perigo2': lambda: perigo(2),
         'perigo3': lambda: perigo(3), 'bunker': bunker, 'visto': visto, 'estatua': estatua, 'amanhecer': amanhecer, 'morte': morte}

if __name__ == '__main__':
    for nome, f in PECAS.items():
        if not QUAIS or nome in QUAIS:
            f()
