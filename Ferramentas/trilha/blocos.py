# A trilha em blocos (a pedido em 29/10/2026: "a repetição dos sons ... para ficar uma trilha sonora prolongada e não
# apenas repetindo o mesmo som de 2:47 infinitamente"). Cada família (a neblina, a noite, o perigo, o vento) vira muitos
# blocos curtos, todos no mesmo andamento e no mesmo tom, que se encaixam em qualquer ordem: o maestro do jogo escolhe o
# próximo na hora, sem repetir os últimos. Cada arquivo tem o bloco e, depois dele, a cauda do eco (o próximo bloco
# começa no fim do bloco, e a cauda do anterior soa por baixo). O perigo vem em três camadas por bloco (a tensão, a
# perseguição, a horda), tocadas juntas, cada uma com o seu volume. O volume de cada família é um só (sem normalizar
# bloco por bloco), para o bloco calmo continuar calmo.
# Uso: python blocos.py [pasta] [famílias...]   (sem famílias: todas)
import os
import sys
import json
import numpy as np
import scipy.io.wavfile as wf
import instrumentos as I
from estudio import Peca, mixar, SR

PASTA = sys.argv[1] if len(sys.argv) > 1 else 'blocos'
QUAIS = set(sys.argv[2:])

TEMA_1 = [(69, 2), (67, 1), (65, 1), (64, 4)]
TEMA_2 = [(69, 2), (67, 1), (65, 1), (62, 4)]
TEMA_3 = [(65, 1), (67, 1), (69, 2), (72, 2), (70, 2)]
TEMA_4 = [(69, 2), (67, 1), (65, 1), (61, 4)]
PIANO = dict(desafinado=7.0, corte=4500)

# os acordes: a mão esquerda do piano, o grave, as cordas
AC = {'Dm': ([50, 57], 38, [50, 53, 57]), 'Bb': ([46, 53], 34, [46, 50, 53, 57]), 'Gm': ([43, 50, 58], 31, [43, 46, 50, 52]),
      'A': ([45, 52, 61], 33, [45, 49, 52, 55]), 'F': ([41, 48, 57], 29, [41, 48, 53, 57])}
GOTAS = {'Dm': [74, 77, 81, 86], 'Bb': [74, 77, 81, 86], 'Gm': [74, 79, 82, 86], 'A': [76, 81, 85], 'F': [77, 81, 84]}


def oitava(frase, k):
    return [(m + k if m is not None else None, d) for m, d in frase]


# ============================================================================================== a neblina (o dia)
NEB = dict(bpm=56, compassos=4, eco=(5.5, 3.2, 7.0, 0.05), molhado=0.6, cauda=7.0, alvo=-21.0)


def neblina(tipo, semente):
    I.sorte(semente)
    r = np.random.default_rng(semente + 1000)
    p = Peca(f'neblina_{tipo}_{semente}', NEB['bpm'])
    T, B = p.T, p.b
    mel = dict(lado=-0.1, eco=0.5, pedal=0.9, **PIANO)
    mao = dict(lado=-0.25, eco=0.5, **PIANO)
    gota = dict(lado=0.35, eco=0.75, desafinado=7.0, corte=6000)

    def gotas(k, acordes):
        for _ in range(k):
            c = int(r.integers(0, len(acordes)))
            p.add('piano', T(c, float(r.choice([0, 1, 1.5, 2, 3]))), 2.0, int(r.choice(GOTAS[acordes[c]])), float(r.uniform(0.09, 0.13)), **gota)

    def mao_esquerda(acordes, v=0.18, solta=False):
        for c, a in enumerate(acordes):
            for k, m in enumerate(AC[a][0]):
                p.add('piano', T(c) + (0.13 * k if solta else 0), B(4) + 0.6, m, v, **mao)

    def grave(acordes, por, v=0.14):
        for c in range(0, len(acordes), por):
            p.add('baixo', T(c), B(4 * por), AC[acordes[c]][1], v, eco=0.3, ataque=1.5, solta=2)

    if tipo == 'entrada':
        # sem melodia: o grave que chega, as cordas abertas, o metal frio, o sino longe
        p.add('baixo', T(0, float(r.uniform(0, 2))), T(4), 38, 0.15, eco=0.3, ataque=5, solta=3)
        p.acorde('cordas', T(0, 2), B(14), [50, 57], 0.035, abertura=0.3, eco=0.6, ataque=5, solta=4, brilho=0.6)
        p.add('metal', T(int(r.integers(0, 2)), 2), 7.0, int(r.choice([81, 86, 88])), 0.045, lado=float(r.choice([-0.55, 0.55])), eco=0.7, ataque=2.5, solta=3)
        p.add('sino', T(2, float(r.choice([0, 2]))), 0, int(r.choice([62, 57])), 0.055, lado=-0.6, eco=0.85, longo=1.3)
        if r.random() < 0.5:
            p.add('piano', T(3, 2), B(2) + 1.5, 69, 0.22, **mel)
    elif tipo == 'piano':
        # o piano sozinho: o tema em cima, a mão esquerda embaixo, e a resposta (nada, gotas ou o eco do tema)
        acordes, tema = [(['Dm', 'Bb', 'Gm', 'Dm'], TEMA_1), (['Dm', 'Bb', 'Dm', 'Dm'], TEMA_2), (['Dm', 'A', 'Bb', 'A'], TEMA_4),
                         (['F', 'Gm', 'Bb', 'A'], TEMA_3), (['Gm', 'Dm', 'Bb', 'A'], TEMA_2), (['Dm', 'Bb', 'Gm', 'A'], TEMA_1)][semente % 6]
        baixa = r.random() < 0.25
        atraso = 1 if r.random() < 0.3 else 0
        p.frase('piano', 0, atraso, oitava(tema, -12 if baixa else 0), 0.40 if not baixa else 0.36, **mel)
        mao_esquerda(acordes, solta=r.random() < 0.35)
        resposta = r.choice(['nada', 'gotas', 'eco', 'gotas'])
        if resposta == 'gotas':
            gotas(int(r.integers(2, 4)), acordes)
        elif resposta == 'eco':
            fim = oitava(tema[1:], 12 if not baixa else 0)
            p.frase('piano', 2, 1, fim, 0.2, lado=0.3, eco=0.7, pedal=1.2, **PIANO)
        if r.random() < 0.4:
            p.acorde('cordas', T(0), B(16), AC[acordes[0]][2][:3], 0.022, abertura=0.5, eco=0.6, ataque=3, solta=3)
    elif tipo == 'cordas':
        # as cordas, o grave e o tema duas vezes, uma em cada acorde; às vezes a voz distante
        pares = [(('Dm', TEMA_1), ('Bb', TEMA_2)), (('Gm', TEMA_3), ('A', TEMA_4)), (('Bb', TEMA_1), ('Gm', TEMA_2)),
                 (('Dm', TEMA_2), ('A', TEMA_4)), (('F', TEMA_3), ('Bb', TEMA_1))][semente % 5]
        acordes = [pares[0][0]] * 2 + [pares[1][0]] * 2
        for i, (a, tema) in enumerate(pares):
            p.acorde('cordas', T(2 * i), B(8), AC[a][2], 0.042, abertura=0.6, eco=0.6, ataque=2.5, solta=3)
            p.add('baixo', T(2 * i), B(8), AC[a][1], 0.14, eco=0.3, ataque=1.5, solta=2)
            p.add('piano', T(2 * i), B(8) + 0.5, AC[a][1] + 12, 0.19, **mao)
            p.frase('piano', 2 * i, 0, tema, 0.40, **mel)
        if r.random() < 0.6:
            topo = {'Dm': 74, 'Bb': 74, 'Gm': 74, 'A': 73, 'F': 72}[pares[1][0]]
            p.add('coro', T(2), B(8), topo, 0.045, lado=0.35, eco=0.85, vogal='u', ataque=3, solta=3)
        if r.random() < 0.3:
            p.add('sino', T(3), 0, 57, 0.045, lado=-0.5, eco=0.85, longo=1.5)
    elif tipo == 'cello':
        # o violoncelo carrega o tema no grave; o piano cai em gotas, no agudo; às vezes o metal
        acordes, frase = [(['Dm', 'Dm', 'Bb', 'Bb'], [(57, 2), (55, 1), (53, 1), (52, 4), (None, 2), (57, 1), (55, 1), (53, 4)]),
                          (['Dm', 'Dm', 'Gm', 'Gm'], [(57, 2), (55, 1), (53, 1), (50, 4), (53, 1), (55, 1), (57, 2), (60, 2), (58, 2)]),
                          (['F', 'F', 'A', 'A'], [(53, 1), (55, 1), (57, 2), (60, 2), (58, 2), (57, 4), (None, 4)]),
                          (['Dm', 'Dm', 'A', 'A'], [(57, 2), (55, 1), (53, 1), (52, 4), (None, 2), (57, 1), (55, 1), (49, 4)])][semente % 4]
        p.frase('cello', 0, 0, frase, 0.30, lado=-0.3, eco=0.45)
        for c in (0, 2):
            p.acorde('cordas', T(c), B(8), AC[acordes[c]][2][:3], 0.038, abertura=0.5, eco=0.6, ataque=2.5, solta=3)
        grave(acordes, 2, 0.13)
        gotas(int(r.integers(2, 5)), acordes)
        if r.random() < 0.5:
            p.add('metal', T(1), 10.0, int(r.choice([88, 86])), 0.03, lado=0.6, eco=0.8, ataque=3, solta=4)
        if r.random() < 0.3:
            p.add('sino', T(3), 0, 57, 0.045, lado=-0.5, eco=0.85, longo=1.5)
    elif tipo == 'vazio':
        # o respiro: as cordas baixinho, o grave, uma ou duas gotas, às vezes uma nota só do tema
        a = ['Dm', 'Bb', 'Gm'][semente % 3]
        p.acorde('cordas', T(0), B(16), AC[a][2][:3], 0.03, abertura=0.6, eco=0.65, ataque=3, solta=4)
        p.add('baixo', T(0), B(16), AC[a][1], 0.12, eco=0.3, ataque=2.5, solta=3)
        gotas(int(r.integers(1, 3)), [a] * 4)
        if r.random() < 0.5:
            p.add('piano', T(2), B(4) + 1.0, 69, 0.24, **mel)
        if r.random() < 0.4:
            p.add('metal', T(0, 2), 8.0, int(r.choice([81, 88])), 0.035, lado=-0.5, eco=0.8, ataque=3, solta=4)
        if r.random() < 0.3:
            p.add('sino', T(1), 0, 62, 0.045, lado=0.5, eco=0.85, longo=1.3)
    elif tipo == 'saida':
        # o fim do trecho: o tema começa e não termina; o dó sustenido sozinho, sem resolver
        if semente % 2 == 0:
            p.frase('piano', 0, 0, [(69, 2), (67, 1), (65, 1)], 0.36, **mel)
            p.add('piano', T(1), B(6) + 1.0, 61, 0.30, **mel)
            p.add('piano', T(1), B(6) + 1.0, 45, 0.18, **mao)
        else:
            p.frase('cello', 0, 0, [(57, 2), (55, 1), (53, 1), (49, 8)], 0.28, lado=-0.2, eco=0.5)
            p.add('piano', T(2), B(4) + 1.0, 61, 0.22, **mel)
        p.acorde('cordas', T(0), B(14), [50, 53, 57, 64], 0.034, abertura=0.6, eco=0.65, ataque=3, solta=6)
        p.add('baixo', T(0), B(15), 38, 0.12, eco=0.3, ataque=2, solta=5)
        if r.random() < 0.6:
            p.add('sino', T(3), 0, 62, 0.05, lado=0.5, eco=0.85, longo=1.3)
    return p


NEB_BLOCOS = ([('entrada', s) for s in range(3)] + [('piano', s) for s in range(12)] + [('cordas', s) for s in range(6)]
              + [('cello', s) for s in range(8)] + [('vazio', s) for s in range(5)] + [('saida', s) for s in range(4)])


# ============================================================================================== a noite
NOI = dict(bpm=46, compassos=4, eco=(7.0, 3.5, 9.0, 0.06), molhado=0.65, cauda=9.0, alvo=-23.0)


def noite(tipo, semente):
    I.sorte(semente + 500)
    r = np.random.default_rng(semente + 2000)
    p = Peca(f'noite_{tipo}_{semente}', NOI['bpm'])
    T, B = p.T, p.b
    # o grave que não para (o ré com batimento), de um bloco para o outro
    if tipo == 'entrada':
        p.add('baixo', 0, T(4) + 1, 38, 0.16, eco=0.3, ataque=8, solta=5, batimento=0.45)
    elif tipo == 'saida':
        p.add('baixo', 0, T(3), 38, 0.15, eco=0.3, ataque=2, solta=8, batimento=0.45)
    else:
        p.add('baixo', 0, T(4) + 1, 38, 0.16, eco=0.3, ataque=2, solta=5, batimento=0.45)

    if tipo == 'entrada':
        p.acorde('cordas', T(0, 2), B(14), [38, 39], 0.025, abertura=0.3, eco=0.5, ataque=6, solta=6, brilho=0.5)
        m = int(r.choice([81, 82, 88]))
        p.add('metal', T(1), B(10), m, 0.03, lado=0.5, eco=0.8, ataque=4, solta=5)
        p.add('metal', T(1, 2), B(9), m + 1, 0.02, lado=0.6, eco=0.8, ataque=4, solta=5)
    elif tipo == 'cello':
        frase = [[(57, 2), (55, 1), (53, 1), (51, 4)], [(57, 2), (55, 1), (53, 1), (50, 4)], [(58, 2), (57, 2), (51, 4)],
                 [(69, 2), (67, 1), (65, 1), (63, 4)], [(57, 2), (55, 1), (53, 1), (51, 2), (50, 2)]][semente % 5]
        inicio = int(r.choice([0, 0, 1]))
        p.frase('cello', inicio, 0, frase, 0.28 if frase[0][0] < 60 else 0.24, lado=float(r.choice([-0.25, 0.25])), eco=0.55)
        notas = [[38, 39], [43, 46, 50], [39, 43, 46, 50]][semente % 3]
        p.acorde('cordas', T(0), B(16), notas, 0.03, abertura=0.5, eco=0.6, ataque=4, solta=5)
        if r.random() < 0.4:
            p.add('sino', T(3), 0, 50, 0.06, lado=float(r.choice([-0.4, 0.4])), eco=0.85, longo=1.6)
    elif tipo == 'coro':
        notas = [[62, 65, 70], [63, 67, 70], [62, 65, 69], [62, 67, 70]][semente % 4]
        p.acorde('coro', T(0, 2), B(14), notas, 0.03, abertura=0.5, eco=0.85, vogal=str(r.choice(['u', 'o'])), ataque=5, solta=6)
        if r.random() < 0.6:
            p.add('sino', T(int(r.integers(1, 3))), 0, 50, 0.065, lado=float(r.uniform(-0.5, 0.5)), eco=0.85, longo=1.6)
        if r.random() < 0.5:
            p.add('metal', T(1), B(10), int(r.choice([81, 88])), 0.028, lado=0.5, eco=0.8, ataque=4, solta=5)
    elif tipo == 'coracao':
        # o coração, longe, que chega e vai embora dentro do bloco; o piano batendo no grave
        t, fim = T(0, 1), T(4) - 0.5
        while t < fim:
            u = (t - T(0, 1)) / (fim - T(0, 1))
            v = 0.15 * (1 - abs(2 * u - 1)) ** 0.7
            p.add('coracao', t, 0, 1.0, v, eco=0.35)
            p.add('coracao', t + 0.28, 0, 1.25, v * 0.7, eco=0.35)
            t += float(r.uniform(1.05, 1.2))
        for c in (1, 3) if r.random() < 0.6 else (2,):
            p.add('piano', T(c), B(4), 38, 0.13, eco=0.6, corte=900, desafinado=10.0)
            p.add('piano', T(c) + 0.05, B(4), 33, 0.10, eco=0.6, corte=900, desafinado=10.0)
        notas = [[43, 46, 50], [39, 43, 46, 50]][semente % 2]
        p.acorde('cordas', T(0), B(16), notas, 0.032, abertura=0.5, eco=0.6, ataque=4, solta=5)
    elif tipo == 'vidro':
        p.add('metal', T(0, 2), B(13), int(r.choice([88, 86, 89])), 0.03, lado=float(r.choice([-0.5, 0.5])), eco=0.8, ataque=3, solta=4, curva=-1.0)
        p.acorde('coro', T(1), B(12), [63, 67, 70], 0.024, abertura=0.5, eco=0.85, vogal='o', ataque=4, solta=5)
        if r.random() < 0.6:
            p.frase('cello', 2, 0, [(63, 3), (62, 5)], 0.22, lado=0.25, eco=0.55)
    elif tipo == 'saida':
        p.add('sino', T(0, 2), 0, 50, 0.06, lado=float(r.choice([-0.3, 0.3])), eco=0.85, longo=1.6)
        p.frase('cello', 1, 0, [(63, 3), (62, 5)] if semente % 2 == 0 else [(57, 2), (55, 1), (53, 1), (51, 4)], 0.24, lado=0.2, eco=0.55)
        p.acorde('cordas', T(0), B(12), [38, 45], 0.028, abertura=0.4, eco=0.6, ataque=3, solta=6)
    return p


NOI_BLOCOS = ([('entrada', s) for s in range(3)] + [('cello', s) for s in range(8)] + [('coro', s) for s in range(6)]
              + [('coracao', s) for s in range(5)] + [('vidro', s) for s in range(4)] + [('saida', s) for s in range(3)])


# ============================================================================================== o perigo, em camadas
PER = dict(bpm=88, compassos=4, eco=(2.6, 1.6, 3.5, 0.02), molhado=0.35, cauda=4.0, alvo=-17.0)
PLANOS = {'A': [38, 38, 38, 38], 'B': [34, 34, 33, 33], 'C': [34, 34, 36, 36]}


def perigo(plano, semente, camada):
    """Um bloco de 4 compassos do perigo, uma camada: 1 a tensão, 2 a perseguição, 3 a horda."""
    I.sorte(semente * 10 + camada + 3000)
    r = np.random.default_rng(semente * 7 + ord(plano))   # a mesma escolha nas três camadas do bloco
    raizes = PLANOS[plano]
    p = Peca(f'perigo_{plano}{semente}_c{camada}', PER['bpm'])
    T, B = p.T, p.b
    acentos = [[1, .55, .75, .55, .9, .55, .8, .6], [1, .5, .6, .8, .55, .9, .6, .75]][int(r.integers(0, 2))]
    tema_em = int(r.choice([0, 0, 2]))
    com_subida = plano != 'A'
    rufo = r.random() < 0.6
    if camada == 1:
        for c, m0 in enumerate(raizes):
            for k in range(8):
                m = m0 + 1 if k == 6 else m0
                p.add('cordas', T(c, k * 0.5), B(0.38), m, 0.2 * acentos[k], lado=-0.2, eco=0.25, ataque=0.008, solta=0.09, brilho=1.0)
                p.add('pizz', T(c, k * 0.5), 0, m + 12, 0.10 * acentos[k], lado=0.2, eco=0.25)
            p.add('timpano', T(c), 0, m0, 0.2, eco=0.3, decai=2.5)
        p.add('baixo', T(0), B(8), raizes[0], 0.13, eco=0.25, ataque=0.3, solta=0.8)
        p.add('baixo', T(2), B(8), raizes[2], 0.13, eco=0.25, ataque=0.3, solta=0.8)
        p.acorde('cordas', T(0), B(16), [69, 70], 0.02, abertura=0.5, eco=0.6, ataque=1.5, solta=2)
    elif camada == 2:
        for c in range(4):
            for tempo in (0, 2):
                p.add('coracao', T(c, tempo), 0, 1.0, 0.2, eco=0.2)
                p.add('coracao', T(c, tempo) + 0.26, 0, 1.25, 0.14, eco=0.2)
            p.add('timpano', T(c, 3), 0, raizes[c], 0.09, eco=0.3, decai=4)
            p.add('timpano', T(c, 3.5), 0, raizes[c], 0.11, eco=0.3, decai=4)
        p.frase('cello', tema_em, 0, [(57, 1), (55, 0.5), (53, 0.5), (52, 2), (57, 1), (55, 0.5), (53, 0.5), (50, 2)],
                0.3, lado=0.3, eco=0.35, ataque=0.03)
        if com_subida:
            p.add('subida', T(2), B(8), 57, 0.13, lado=0.3, eco=0.5)
    else:
        for c in (0, 2):
            topo = {38: [74, 77, 81], 34: [74, 77, 82], 33: [73, 76, 81], 36: [72, 76, 79]}[raizes[c]]
            p.acorde('cordas', T(c), B(8), topo, 0.04, abertura=0.6, eco=0.4, ataque=0.3, solta=0.8, tremolo=11, prof=0.7, brilho=1.0)
        p.acorde('coro', T(0), B(16), [69, 74] if plano == 'A' else [74, 77], 0.045, abertura=0.4, eco=0.6, vogal='a', ataque=1, solta=2)
        for c, m0 in enumerate(raizes):
            for tempo in range(4):
                p.add('cello', T(c, tempo), B(0.8), m0 + 12, 0.12, lado=-0.35, eco=0.25, ataque=0.02, solta=0.15)
        if rufo:
            for k in range(8):
                p.add('timpano', T(3, 2 + k * 0.25), 0, raizes[3], 0.07 + 0.025 * k, eco=0.3, decai=4)
    return p


def perigo_momento(tipo, semente):
    """O que liga o perigo ao resto: a entrada (te viram: 2 tempos de piano ao contrário até o golpe, que cai no
    primeiro tempo do perigo), a saída (o perigo acabou) e a saída pela pedra (os que te caçavam viraram pedra)."""
    I.sorte(semente + 4000)
    r = np.random.default_rng(semente + 4100)
    p = Peca(f'perigo_{tipo}_{semente}', PER['bpm'])
    T, B = p.T, p.b
    if tipo == 'entrada':
        golpe = B(2)
        for m in [(50, 51, 57, 62), (50, 57, 62, 63), (45, 50, 51, 57)][semente % 3]:
            p.add('reverso', 0.0, golpe, m, 0.3, lado=(m - 55) / 10, eco=0.4, desafinado=6.0)
        p.acorde('cordas', golpe, 0.5, [38, 39, 45, 50], 0.13, abertura=0.6, eco=0.6, ataque=0.02, solta=2.5, brilho=1.0)
        p.add('timpano', golpe, 0, 38, 0.6, eco=0.5, decai=1.5)
        p.add('metal', golpe, 2.0, int(r.choice([93, 91, 94])), 0.07, lado=float(r.choice([-0.4, 0.4])), eco=0.7, ataque=0.05, solta=1.5, curva=2.0)
        p.add('coro', golpe, 0.8, int(r.choice([75, 74])), 0.05, lado=-0.3, eco=0.7, vogal='a', ataque=0.1, solta=1.5)
        return p, golpe, B(2) + 3.0
    if tipo == 'saida':
        p.add('timpano', 0, 0, 38, 0.3, eco=0.4, decai=1.8)
        p.acorde('cordas', 0, B(6), [38, 45], 0.09, abertura=0.4, eco=0.5, ataque=0.05, solta=3.5, brilho=0.8)
        p.acorde('cordas', 0, B(5), [69, 70] if semente % 2 == 0 else [69, 74], 0.025, abertura=0.5, eco=0.6, ataque=0.05, solta=4)
        for k, v in enumerate([0.16, 0.11, 0.06]):
            p.add('coracao', B(1 + 2.4 * k), 0, 1.0, v, eco=0.3)
            p.add('coracao', B(1 + 2.4 * k) + 0.3, 0, 1.25, v * 0.7, eco=0.3)
        return p, 0.0, B(8)
    # a saída pela pedra: o vidro que afunda, as vozes em trítono, o baque da pedra
    for m, v, lado in [(81, 0.05, -0.4), (82, 0.045, 0.4), (88, 0.03, 0.0)]:
        p.add('metal', 0.0, 4.0, m + (semente % 2), v, lado=lado, eco=0.8, ataque=0.4, solta=2.0, curva=-1.0)
    p.acorde('coro', 0.2, 3.0, [50, 56], 0.06, abertura=0.3, eco=0.8, vogal='u', ataque=1.0, solta=2.0, curva=-1.0)
    p.add('timpano', B(6), 0, 38, 0.3, eco=0.6, decai=2)
    p.add('piano', B(6), 3.0, 26, 0.22, eco=0.6, corte=700, desafinado=8.0)
    p.add('piano', B(6) + 0.02, 3.0, 33, 0.2, eco=0.6, corte=700, desafinado=8.0)
    return p, 0.0, B(6) + 3.0


# ============================================================================================== o vento (o silêncio)
def vento(semente, noite_):
    """O silêncio: só o vento. Um fundo grave que não para e, às vezes, uma rajada que vem de um lado, cresce e clareia
    enquanto chega, bate em você (aberta, dos dois lados) e passa para o outro lado, sumindo; às vezes um assovio fino."""
    r = np.random.default_rng(semente + (7000 if noite_ else 6000))
    I.sorte(semente + (7100 if noite_ else 6100))
    dur = float(r.uniform(24, 36))
    n = int(dur * SR)
    t = np.arange(n, dtype=np.float32) / SR
    escuro = 0.7 if noite_ else 1.0
    ruido = lambda: r.standard_normal(n).astype(np.float32)
    # o fundo
    e = I.filtrar(ruido(), 'low', 230 * escuro, 2) * (0.6 + 0.4 * I.suave(n, 0.1))
    d = I.filtrar(ruido(), 'low', 230 * escuro, 2) * (0.6 + 0.4 * I.suave(n, 0.1))
    chiado = I.filtrar(ruido(), 'band', [1500 * escuro, 4000 * escuro]) * 0.05 * (0.5 + 0.5 * I.suave(n, 0.15))
    e, d = e + chiado, d + 0.8 * chiado + 0.2 * I.filtrar(ruido(), 'band', [1500 * escuro, 4000 * escuro]) * 0.05
    fundo = max(np.sqrt(np.mean(e ** 2)), 1e-9)
    e, d = e / fundo * 0.1, d / fundo * 0.1
    # as rajadas
    faixas = [(70, 220), (220, 550), (550, 1300), (1300, 3000), (3000, 6500)]
    k = int(r.choice([0, 1, 1, 2, 2])) if dur > 26 else int(r.choice([0, 1, 1]))
    centros = sorted(r.uniform(7, dur - 9, k)) if k else []
    if k == 2 and centros[1] - centros[0] < 9:
        centros = [centros[0]]
    for tc in centros:
        sobe, desce, forca = float(r.uniform(3.5, 6.5)), float(r.uniform(3.0, 6.0)), float(r.uniform(0.6, 1.0))
        lado = float(r.choice([-1, 1]))
        g = np.where(t < tc, np.clip((t - (tc - sobe)) / sobe, 0, 1) ** 2, np.exp(-np.maximum(t - tc, 0) / (desce / 2.5)))
        g = (g * forca).astype(np.float32)
        pos = np.where(t < tc, lado * 0.85 * (1 - np.clip((t - (tc - sobe)) / sobe, 0, 1)), -lado * 0.5 * np.clip((t - tc) / desce, 0, 1))
        ge, gd = np.cos((pos + 1) * np.pi / 4) * 1.414, np.sin((pos + 1) * np.pi / 4) * 1.414
        largura = 0.25 + 0.75 * g
        re, rd = np.zeros(n, np.float32), np.zeros(n, np.float32)
        for i, (a, b) in enumerate(faixas):
            peso = (g ** (1 + 0.7 * i)) * [1.0, 0.9, 0.65, 0.4, 0.22][i]
            comum = I.filtrar(ruido(), 'band', [a * escuro, b * escuro])
            re += peso * (comum * ge + I.filtrar(ruido(), 'band', [a * escuro, b * escuro]) * largura)
            rd += peso * (comum * gd + I.filtrar(ruido(), 'band', [a * escuro, b * escuro]) * largura)
        # o tranco grave quando bate em você
        tranco = I.filtrar(ruido(), 'low', 90) * g ** 3 * (0.6 + 0.4 * np.sin(2 * np.pi * float(r.uniform(3, 6)) * t))
        re, rd = re + tranco * 0.8, rd + tranco * 0.8
        if r.random() < 0.45:
            f0 = float(r.uniform(650, 1250)) * escuro
            f = f0 * (1 + 0.08 * g)
            fase = np.cumsum(f) / SR
            banda = lambda: I.filtrar(ruido(), 'low', 25, 2)
            assovio = (np.cos(2 * np.pi * fase) * banda() + np.sin(2 * np.pi * fase) * banda()) * g ** 2
            assovio /= np.abs(assovio).max() + 1e-9
            re, rd = re + assovio * 0.12 * ge, rd + assovio * 0.12 * gd
        escala = max(np.sqrt(np.mean(re[np.abs(t - tc) < 1.5] ** 2)), 1e-9)
        e, d = e + re / escala * 0.2 * forca, d + rd / escala * 0.2 * forca
    # entra e sai devagar (o próximo começa 4 s antes deste acabar)
    m = int(4.0 * SR)
    rampa = np.sin(np.linspace(0, np.pi / 2, m)) ** 2
    e[:m] *= rampa
    d[:m] *= rampa
    e[-m:] *= rampa[::-1]
    d[-m:] *= rampa[::-1]
    return e, d, dur, len(centros)


# ============================================================================================== o estúdio dos blocos
def limitar(x, joelho=0.6, teto=0.92):
    a = np.abs(x)
    acima = a > joelho
    y = x.copy()
    y[acima] = np.sign(x[acima]) * (joelho + (teto - joelho) * np.tanh((a[acima] - joelho) / (teto - joelho)))
    return y


def gravar(caminho, e, d):
    os.makedirs(os.path.dirname(caminho), exist_ok=True)
    wf.write(caminho, SR, (np.stack([limitar(e), limitar(d)], axis=1) * 32767).astype(np.int16))


def rms(e, d, ate=None):
    e, d = e[:ate], d[:ate]
    return float(np.sqrt(np.mean((e.astype(np.float64) ** 2 + d.astype(np.float64) ** 2) / 2)) + 1e-12)


def familia_musical(nome, cfg, lista, compor, referencia):
    """Mistura todos os blocos, acha um volume só para a família (pela média dos blocos de referência) e grava."""
    comprimento = Peca('x', cfg['bpm']).T(cfg['compassos'])
    brutos, info = [], []
    for item in lista:
        p = compor(*item)
        e, d = mixar(p, comprimento, cfg['eco'], cfg['molhado'], cfg['cauda'])
        temp = os.path.join(PASTA, '_temp', p.nome + '.npy')
        os.makedirs(os.path.dirname(temp), exist_ok=True)
        np.save(temp, np.stack([e, d]).astype(np.float32))
        brutos.append((p.nome, temp, rms(e, d, int(comprimento * SR))))
        info.append(item)
    ref = [v for (nm, tp, v), it in zip(brutos, info) if referencia(it)]
    g = 10 ** (cfg['alvo'] / 20) / float(np.median(ref))
    blocos = []
    for (nm, tp, _), it in zip(brutos, info):
        e, d = np.load(tp)
        os.remove(tp)
        caminho = os.path.join(PASTA, nome, nm + '.wav')
        e, d = e * g, d * g
        m = int(1.0 * SR)
        e[-m:] *= np.linspace(1, 0, m)
        d[-m:] *= np.linspace(1, 0, m)
        gravar(caminho, e, d)
        pico = 20 * np.log10(max(np.abs(e).max(), np.abs(d).max()) + 1e-12)
        blocos.append(dict(nome=nm, tipo=it[0], arquivo=os.path.relpath(caminho, PASTA).replace('\\', '/'), pico_db=round(pico, 1),
                           rms_db=round(20 * np.log10(rms(e, d, int(comprimento * SR))), 1)))
    print(f'{nome}: {len(blocos)} blocos de {comprimento:.2f} s, ganho {20 * np.log10(g):.1f} dB, '
          f'pico máx {max(b["pico_db"] for b in blocos):.1f} dB')
    return dict(bpm=cfg['bpm'], compassos=cfg['compassos'], comprimento=comprimento, blocos=blocos)


def familia_perigo():
    comprimento = Peca('x', PER['bpm']).T(PER['compassos'])
    brutos = []
    for plano in 'ABC':
        for s in range(3):
            camadas = []
            for c in (1, 2, 3):
                e, d = mixar(perigo(plano, s, c), comprimento, PER['eco'], PER['molhado'], PER['cauda'])
                camadas.append((e.astype(np.float32), d.astype(np.float32)))
            brutos.append((plano, s, camadas))
    # o volume: as três camadas juntas (a horda) no alvo
    ref = [rms(sum(c[0] for c in cs), sum(c[1] for c in cs), int(comprimento * SR)) for _, _, cs in brutos]
    g = 10 ** (PER['alvo'] / 20) / float(np.median(ref))
    blocos, pico_soma = [], -99
    for plano, s, cs in brutos:
        arquivos = []
        for c, (e, d) in enumerate(cs, 1):
            caminho = os.path.join(PASTA, 'perigo', f'perigo_{plano}{s}_c{c}.wav')
            gravar(caminho, e * g, d * g)
            arquivos.append(os.path.relpath(caminho, PASTA).replace('\\', '/'))
        se, sd = sum(c[0] for c in cs) * g, sum(c[1] for c in cs) * g
        pico_soma = max(pico_soma, 20 * np.log10(max(np.abs(se).max(), np.abs(sd).max())))
        niveis = [round(20 * np.log10(rms(sum(c[0] for c in cs[:k]) * g, sum(c[1] for c in cs[:k]) * g, int(comprimento * SR))), 1) for k in (1, 2, 3)]
        blocos.append(dict(nome=f'perigo_{plano}{s}', plano=plano, camadas=arquivos, rms_por_nivel=niveis))
    momentos = []
    for tipo, sementes in (('entrada', 3), ('saida', 2), ('pedra', 2)):
        for s in range(sementes):
            p, encaixe, dur = perigo_momento(tipo, s)
            e, d = mixar(p, dur, PER['eco'] if tipo == 'saida' else (4.5, 2.5, 6.0, 0.02), 0.55, 4.0)
            caminho = os.path.join(PASTA, 'perigo', p.nome + '.wav')
            m = int(1.0 * SR)
            ajuste = {'entrada': 0.5, 'saida': 1.0, 'pedra': 0.7}[tipo]   # o golpe por cima do perigo não pode estourar
            e, d = e * g * ajuste, d * g * ajuste
            e[-m:] *= np.linspace(1, 0, m)
            d[-m:] *= np.linspace(1, 0, m)
            gravar(caminho, e, d)
            momentos.append(dict(nome=p.nome, tipo=tipo, encaixe=round(encaixe, 4), arquivo=os.path.relpath(caminho, PASTA).replace('\\', '/'),
                                 pico_db=round(20 * np.log10(max(np.abs(e).max(), np.abs(d).max())), 1)))
    print(f'perigo: {len(blocos)} blocos x 3 camadas de {comprimento:.2f} s, ganho {20 * np.log10(g):.1f} dB, pico das 3 juntas {pico_soma:.1f} dB; '
          f'por nível {blocos[0]["rms_por_nivel"]}')
    return dict(bpm=PER['bpm'], compassos=PER['compassos'], comprimento=comprimento, blocos=blocos, momentos=momentos)


def familia_vento(noite_):
    nome = 'vento_noite' if noite_ else 'vento_dia'
    brutos = [vento(s, noite_) for s in range(10)]
    g = 10 ** (-29.0 / 20) / float(np.median([rms(e, d) for e, d, _, _ in brutos]))
    blocos = []
    for s, (e, d, dur, k) in enumerate(brutos):
        caminho = os.path.join(PASTA, nome, f'{nome}_{s}.wav')
        gravar(caminho, e * g, d * g)
        blocos.append(dict(nome=f'{nome}_{s}', arquivo=os.path.relpath(caminho, PASTA).replace('\\', '/'), duracao=round(dur, 3), rajadas=k))
    print(f'{nome}: {len(blocos)} blocos, {sum(b["rajadas"] for b in blocos)} rajadas')
    return dict(sobreposicao=4.0, blocos=blocos)


if __name__ == '__main__':
    caminho = os.path.join(PASTA, 'manifesto.json')
    man = json.load(open(caminho, encoding='utf-8')) if os.path.exists(caminho) else {}
    if not QUAIS or 'neblina' in QUAIS:
        man['neblina'] = familia_musical('neblina', NEB, NEB_BLOCOS, neblina, lambda it: it[0] in ('piano', 'cordas', 'cello'))
    if not QUAIS or 'noite' in QUAIS:
        man['noite'] = familia_musical('noite', NOI, NOI_BLOCOS, noite, lambda it: it[0] in ('cello', 'coro', 'coracao', 'vidro'))
    if not QUAIS or 'perigo' in QUAIS:
        man['perigo'] = familia_perigo()
    if not QUAIS or 'vento' in QUAIS:
        man['vento_dia'] = familia_vento(False)
        man['vento_noite'] = familia_vento(True)
    man['momentos'] = dict(amanhecer='../saida/ZCC_07_Momento_Amanhecer.wav', morte='../saida/ZCC_08_Momento_Morte.wav')
    texto = json.dumps(man, ensure_ascii=False, indent=1, default=float)
    open(caminho, 'w', encoding='utf-8').write(texto)
    print('manifesto gravado')
