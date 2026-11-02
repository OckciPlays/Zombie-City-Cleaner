# As famílias do clima na trilha (a pedido em 02/11/2026: "uma família nova de trilhas para a tempestade" e "uma outra
# família para o tornado, mais angustiante, ainda mais quando avistado"). No chuvisco e na chuva, a trilha é a de sempre
# (a neblina de dia, a noite à noite); estas duas entram na tempestade e com o tornado perto. Os blocos seguem o padrão
# do blocos.py (o mesmo tom de ré menor, o andamento de cada família, a entrada e a saída fechando o trecho, a cauda do
# eco depois do bloco), e o maestro do jogo escolhe a ordem na hora (MaestroDaTrilha.Clima.cs).
#  - A tempestade (52 bpm): o grave pesado que não para, o cello batendo como a chuva no telhado, os tímpanos rolando
#    como trovão longe, o coro escuro e as cordas em tremolo.
#  - O tornado (66 bpm): a entrada é o golpe de quando ele é avistado (o piano ao contrário até o estouro, o metal
#    gritando, o coro); no meio, as cordas subindo em espiral, o coração acelerado, o lamento do coro descendo de
#    semitom em semitom e o metal raspando; a saída desaba (tudo caindo de tom).
# Uso (nesta pasta): python clima.py            (compõe as duas famílias em blocos/, no manifesto.json)
#                    python clima.py exportar   (só converte estas duas para Assets/_Game/Audio/Trilha e põe no
#                                               trilha.json: as outras famílias ficam como estão)
import os
import sys
import json
import subprocess

EXPORTAR = len(sys.argv) > 1 and sys.argv[1] == 'exportar'
sys.argv = [sys.argv[0], 'blocos']   # o blocos.py lê a pasta daqui
import numpy as np
import blocos as BL
import instrumentos as I
from estudio import Peca

AQUI = os.path.dirname(os.path.abspath(__file__))

# ============================================================================================== a tempestade
TEM = dict(bpm=52, compassos=4, eco=(6.0, 3.4, 7.5, 0.05), molhado=0.58, cauda=8.0, alvo=-20.0)
# os planos de acorde de cada bloco (o grave muda a cada compasso)
PLANOS_TEM = [[38, 38, 34, 33], [38, 34, 36, 33], [38, 38, 39, 38], [34, 34, 33, 33], [38, 43, 39, 33]]


def tempestade(tipo, semente):
    I.sorte(semente + 7000)
    r = np.random.default_rng(semente + 7100)
    p = Peca(f'tempestade_{tipo}_{semente}', TEM['bpm'])
    T, B = p.T, p.b
    plano = PLANOS_TEM[semente % len(PLANOS_TEM)]
    # o grave pesado que liga um bloco ao outro
    if tipo == 'entrada':
        p.add('baixo', 0, T(4) + 1, 38, 0.18, eco=0.3, ataque=6, solta=5, batimento=0.6)
    elif tipo == 'saida':
        p.add('baixo', 0, T(3), 38, 0.17, eco=0.3, ataque=1, solta=8, batimento=0.6)
    else:
        for c, m in enumerate(plano):
            p.add('baixo', T(c), B(4) + 0.6, m, 0.17, eco=0.3, ataque=0.8, solta=1.2, batimento=0.6)

    if tipo == 'entrada':
        # o trovão chegando: o rufo dos tímpanos crescendo, as cordas do grave subindo
        for k in range(24):
            t = T(1) + k * B(0.25)
            p.add('timpano', t, 0, 38, 0.03 + 0.006 * k, eco=0.45, decai=3)
        p.add('timpano', T(2, 2), 0, 38, 0.32, eco=0.6, decai=1.6)
        p.acorde('cordas', T(0, 2), B(12), [38, 39, 45], 0.03, abertura=0.4, eco=0.55, ataque=5, solta=5, brilho=0.6)
        p.acorde('coro', T(2, 2), B(6), [50, 53, 57], 0.035, abertura=0.5, eco=0.8, vogal='u', ataque=2, solta=4)
    elif tipo == 'ostinato':
        # o cello batendo como a chuva: colcheias com acento, um semitom de vez em quando
        acentos = [1, .55, .7, .55, .9, .55, .75, .6]
        for c, m0 in enumerate(plano):
            for k in range(8):
                m = m0 + 12 + (1 if k == 6 and r.random() < 0.6 else 0)
                p.add('cello', T(c, k * 0.5), B(0.42), m, 0.15 * acentos[k], lado=-0.3, eco=0.3, ataque=0.02, solta=0.12)
            if c % 2 == 0:
                p.add('timpano', T(c), 0, m0, 0.14, eco=0.4, decai=2.2)
        p.acorde('cordas', T(0), B(16), [62, 65, 69] if plano[0] == 38 else [62, 65, 70], 0.022, abertura=0.6, eco=0.6, ataque=3, solta=4)
        if r.random() < 0.5:
            p.frase('cello', 2, 0, [(57, 2), (55, 1), (53, 1), (51, 4)], 0.2, lado=0.35, eco=0.5)
    elif tipo == 'tambores':
        # o trovão: rufos que crescem e somem em lugares diferentes do bloco, os pizz no grave
        for _ in range(int(r.integers(2, 4))):
            ini = float(r.uniform(0, T(3)))
            n = int(r.integers(8, 18))
            for k in range(n):
                u = k / n
                v = 0.05 + 0.12 * np.sin(np.pi * u)
                p.add('timpano', ini + k * B(0.25), 0, int(r.choice([38, 33, 34])), v, lado=float(r.uniform(-0.4, 0.4)), eco=0.5, decai=2.5)
        for c, m0 in enumerate(plano):
            for tempo in (0, 1.5, 3):
                p.add('pizz', T(c, tempo), 0, m0 + 12, 0.1, lado=0.25, eco=0.35, decai=4)
        p.acorde('coro', T(0, 2), B(13), [50, 53, 58], 0.025, abertura=0.5, eco=0.85, vogal='u', ataque=4, solta=5)
    elif tipo == 'coro':
        # o coro escuro, mudando com o grave, e as cordas tremendo por baixo
        topos = {38: [62, 65, 69], 34: [62, 65, 70], 33: [61, 64, 69], 36: [60, 64, 67], 39: [63, 67, 70], 43: [62, 67, 70]}
        for c, m0 in enumerate(plano):
            p.acorde('coro', T(c), B(4.4), topos[m0], 0.035, abertura=0.5, eco=0.85, vogal=str(r.choice(['u', 'o'])), ataque=1.2, solta=2.5)
        p.acorde('cordas', T(0), B(16), [50, 51], 0.03, abertura=0.3, eco=0.5, ataque=2, solta=3, tremolo=7, prof=0.55)
        if r.random() < 0.6:
            p.add('sino', T(int(r.integers(1, 3))), 0, 50, 0.06, lado=float(r.uniform(-0.5, 0.5)), eco=0.85, longo=1.5)
    elif tipo == 'cordas':
        # as cordas em tremolo, segundas menores esfregando; um metal que desce
        pares = [[62, 63], [65, 66], [57, 58], [69, 70]]
        for c in range(4):
            a, b = pares[(c + semente) % 4]
            p.acorde('cordas', T(c), B(4.5), [a, b], 0.035, abertura=0.6, eco=0.55, ataque=0.8, solta=1.5, tremolo=9, prof=0.7, brilho=0.9)
        p.add('metal', T(1), B(10), int(r.choice([86, 88, 89])), 0.03, lado=float(r.choice([-0.5, 0.5])), eco=0.8, ataque=3, solta=4, curva=-2.0)
        for c in (0, 2):
            p.add('timpano', T(c), 0, plano[c], 0.12, eco=0.45, decai=2)
    elif tipo == 'saida':
        p.add('timpano', 0, 0, 38, 0.2, eco=0.6, decai=1.5)
        p.acorde('cordas', 0, B(12), [38, 45], 0.04, abertura=0.4, eco=0.6, ataque=0.5, solta=6, brilho=0.6)
        p.frase('cello', 1, 0, [(57, 2), (55, 1), (53, 1), (50, 6)], 0.2, lado=0.2, eco=0.6)
        p.add('sino', T(2), 0, 50, 0.055, lado=-0.3, eco=0.85, longo=1.6)
    return p


TEM_BLOCOS = ([('entrada', s) for s in range(3)] + [('ostinato', s) for s in range(6)] + [('tambores', s) for s in range(5)]
              + [('coro', s) for s in range(5)] + [('cordas', s) for s in range(5)] + [('saida', s) for s in range(3)])


# ============================================================================================== o tornado
TOR = dict(bpm=66, compassos=4, eco=(4.6, 2.8, 6.0, 0.03), molhado=0.5, cauda=6.0, alvo=-17.5)


def tornado(tipo, semente):
    I.sorte(semente + 8000)
    r = np.random.default_rng(semente + 8100)
    p = Peca(f'tornado_{tipo}_{semente}', TOR['bpm'])
    T, B = p.T, p.b
    if tipo != 'entrada' and tipo != 'saida':
        # o grave que vibra o tempo todo (ré com o batimento forte)
        p.add('baixo', 0, T(4) + 0.8, 38, 0.2, eco=0.25, ataque=0.5, solta=1.5, batimento=1.4)
    if tipo == 'entrada':
        # avistado: o piano ao contrário até o estouro; o metal gritando, o coro, e o coração disparando
        golpe = B(2)
        for m in [(50, 51, 56, 62), (45, 50, 51, 57), (50, 56, 57, 63)][semente % 3]:
            p.add('reverso', 0.0, golpe, m, 0.32, lado=(m - 55) / 10, eco=0.4, desafinado=9.0)
        p.add('subida', 0.0, golpe, 57, 0.14, lado=0.2, eco=0.4)
        p.acorde('cordas', golpe, B(3), [38, 39, 44, 45, 50], 0.14, abertura=0.7, eco=0.6, ataque=0.02, solta=3, brilho=1.0)
        p.add('timpano', golpe, 0, 38, 0.7, eco=0.55, decai=1.2)
        p.add('timpano', golpe + 0.02, 0, 33, 0.5, eco=0.55, decai=1.4)
        p.add('metal', golpe, B(6), int(r.choice([91, 93, 94])), 0.08, lado=-0.4, eco=0.7, ataque=0.05, solta=2, curva=3.0)
        p.add('metal', golpe + 0.1, B(6), int(r.choice([86, 87])), 0.06, lado=0.4, eco=0.7, ataque=0.05, solta=2, curva=-2.0)
        p.acorde('coro', golpe, B(6), [74, 75], 0.06, abertura=0.5, eco=0.75, vogal='a', ataque=0.1, solta=3, curva=-1.0)
        p.add('baixo', golpe, T(4) - golpe + 1, 38, 0.2, eco=0.3, ataque=0.1, solta=2, batimento=1.4)
        t = golpe + B(1)
        while t < T(4) - 0.4:
            p.add('coracao', t, 0, 1.1, 0.2, eco=0.2)
            p.add('coracao', t + 0.22, 0, 1.35, 0.14, eco=0.2)
            t += 0.62
    elif tipo == 'espiral':
        # as cordas subindo em espiral (cada compasso escorrega meio tom para cima), a subida chiando, o tímpano batendo
        base = [[50, 51, 57], [50, 56, 57], [45, 51, 56]][semente % 3]
        for c in range(4):
            notas = [m + c for m in base]
            p.acorde('cordas', T(c), B(4.2), notas, 0.045, abertura=0.7, eco=0.45, ataque=0.3, solta=0.8, tremolo=12, prof=0.75, brilho=1.0, curva=1.0)
            for k in range(8):
                p.add('timpano', T(c, k * 0.5), 0, 38, 0.07 + (0.06 if k % 4 == 0 else 0), eco=0.3, decai=4)
        p.add('subida', T(2), B(8), 57, 0.12, lado=-0.3, eco=0.5)
        p.acorde('coro', T(0), B(16), [74, 75], 0.03, abertura=0.6, eco=0.8, vogal='a', ataque=3, solta=3, curva=2.0)
    elif tipo == 'coracao':
        # o coração acelerado, o cello em semicolcheias no grave, o metal raspando
        t = 0.0
        while t < T(4) - 0.3:
            p.add('coracao', t, 0, 1.1, 0.22, eco=0.2)
            p.add('coracao', t + 0.2, 0, 1.35, 0.15, eco=0.2)
            t += 0.55
        for c in range(4):
            for k in range(16):
                m = 50 + (1 if k % 8 == 7 else 0)
                p.add('cello', T(c, k * 0.25), B(0.22), m, 0.12 * (1.0 if k % 4 == 0 else 0.6), lado=0.3, eco=0.25, ataque=0.01, solta=0.06)
        p.add('metal', T(1), B(9), int(r.choice([88, 89, 91])), 0.04, lado=float(r.choice([-0.5, 0.5])), eco=0.75, ataque=1, solta=2, curva=float(r.choice([-3.0, 3.0])))
    elif tipo == 'lamento':
        # o lamento: o coro descendo de semitom em semitom, alto, com as cordas tremendo embaixo
        topo = int(r.choice([74, 75, 77]))
        for c in range(4):
            p.acorde('coro', T(c), B(4.3), [topo - c, topo - c - 6], 0.05, abertura=0.5, eco=0.8, vogal='a', ataque=0.6, solta=1.5)
        p.acorde('cordas', T(0), B(16), [50, 51, 56], 0.04, abertura=0.5, eco=0.5, ataque=1, solta=2, tremolo=10, prof=0.7)
        for c in range(4):
            p.add('timpano', T(c), 0, 38, 0.16, eco=0.5, decai=1.8)
            p.add('timpano', T(c, 2.5), 0, 33, 0.1, eco=0.5, decai=2.5)
    elif tipo == 'metal':
        # o metal raspando em todas as direções, os pizz soltos, o rufo crescendo no fim
        for _ in range(4):
            p.add('metal', float(r.uniform(0, T(3))), B(float(r.uniform(4, 8))), int(r.integers(80, 94)), 0.035,
                  lado=float(r.uniform(-0.7, 0.7)), eco=0.8, ataque=0.8, solta=1.5, curva=float(r.uniform(-4, 4)))
        for _ in range(10):
            p.add('pizz', float(r.uniform(0, T(4) - 0.5)), 0, int(r.choice([50, 51, 56, 57, 62, 63])), 0.12, lado=float(r.uniform(-0.6, 0.6)), eco=0.4)
        for k in range(16):
            p.add('timpano', T(3) + k * B(0.25), 0, 38, 0.04 + 0.012 * k, eco=0.4, decai=3)
        p.acorde('cordas', T(0), B(12), [38, 39], 0.05, abertura=0.3, eco=0.4, ataque=2, solta=2, brilho=1.0)
    elif tipo == 'saida':
        # desaba: as cordas caindo de tom, o último golpe, o coração diminuindo
        p.acorde('cordas', 0, B(8), [50, 51, 57, 62], 0.06, abertura=0.7, eco=0.6, ataque=0.05, solta=3, brilho=0.9, curva=-7.0)
        p.add('timpano', 0, 0, 38, 0.4, eco=0.6, decai=1.4)
        p.add('baixo', 0, B(10), 38, 0.2, eco=0.3, ataque=0.05, solta=5, batimento=1.4)
        for k, v in enumerate([0.2, 0.15, 0.1, 0.06]):
            p.add('coracao', B(2 + 2.6 * k), 0, 1.0, v, eco=0.3)
            p.add('coracao', B(2 + 2.6 * k) + 0.3, 0, 1.25, v * 0.7, eco=0.3)
    # o golpe e o desabar: mais fortes que o resto, sem estourar (o resto da família dá o volume)
    if tipo in ('entrada', 'saida'):
        k = 0.36 if tipo == 'entrada' else 0.68
        p.notas = [(i, t, d, m, v * k, lado, eco, ex) for (i, t, d, m, v, lado, eco, ex) in p.notas]
    return p


TOR_BLOCOS = ([('entrada', s) for s in range(3)] + [('espiral', s) for s in range(4)] + [('coracao', s) for s in range(4)]
              + [('lamento', s) for s in range(4)] + [('metal', s) for s in range(3)] + [('saida', s) for s in range(2)])


# ============================================================================================== a exportação
DESTINO = os.path.normpath(os.path.join(AQUI, '..', '..', 'Assets', '_Game', 'Audio', 'Trilha'))


def exportar():
    man = json.load(open(os.path.join(AQUI, 'blocos', 'manifesto.json'), encoding='utf-8'))
    n = 0
    for fam in ('tempestade', 'tornado'):
        pasta = os.path.join(DESTINO, fam)
        if os.path.isdir(pasta):
            for a in os.listdir(pasta):
                if a.endswith('.ogg'):
                    os.remove(os.path.join(pasta, a))
        for b in man[fam]['blocos']:
            BL_ogg(os.path.join(AQUI, 'blocos', b['arquivo']), os.path.join(pasta, b['nome'] + '.ogg'))
            n += 1
    caminho = os.path.join(DESTINO, 'trilha.json')
    info = json.load(open(caminho, encoding='utf-8'))
    for fam in ('tempestade', 'tornado'):
        info[fam] = dict(bpm=man[fam]['bpm'], comprimento=man[fam]['comprimento'])
    with open(caminho, 'w', encoding='utf-8') as f:
        json.dump(info, f, indent=1)
    print(f'{n} blocos do clima em {DESTINO}')


def BL_ogg(origem, destino):
    os.makedirs(os.path.dirname(destino), exist_ok=True)
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', origem, '-c:a', 'libvorbis', '-q:a', '5', destino], check=True)


if __name__ == '__main__':
    if EXPORTAR:
        exportar()
    else:
        os.chdir(AQUI)
        caminho = os.path.join('blocos', 'manifesto.json')
        man = json.load(open(caminho, encoding='utf-8')) if os.path.exists(caminho) else {}
        man['tempestade'] = BL.familia_musical('tempestade', TEM, TEM_BLOCOS, tempestade, lambda it: it[0] in ('ostinato', 'coro', 'cordas'))
        man['tornado'] = BL.familia_musical('tornado', TOR, TOR_BLOCOS, tornado, lambda it: it[0] in ('espiral', 'coracao', 'lamento'))
        open(caminho, 'w', encoding='utf-8').write(json.dumps(man, ensure_ascii=False, indent=1, default=float))
        print('manifesto gravado')
