# O perigo novo, "o lamento que caça" (04/11/2026, a pedido: "Eu gostei muito desse lamento do coro. Eu acho que
# devemos fazer isso nas musicas da luta contra os zumbis também ... dessa vez a trilha seja mais medonha,
# aterrorizante, com esse coro dando suspense, nível filme de terror de hollywood de trilha sonora. E aquele mecanismo
# de progressão da horda"). O mesmo contrato do perigo do blocos.py (o maestro do jogo lê igual): blocos de 4 compassos
# no mesmo andamento e tom, cada um em três camadas tocadas juntas, cada uma com o volume do nível do perigo. O centro é
# o lamento do tornado (66 bpm, o mesmo eco; duas vozes do coro em trítono descendo um semitom por compasso), e ele
# muda de cara a cada nível da horda:
#  1 a tensão, "o lamento de boca fechada": o coro canta as mesmas notas em 'u', longe; o chão de ré, o coração em
#    repouso (66/min), o metal friccionado ou o fio agudo, os tiques do arco de madeira (algo andando na parede), os
#    sussurros. Sem ostinato, sem tímpano: é a camada da "sombra", que precisa aguentar minutos sem cansar;
#  2 a perseguição, "o lamento abre a boca": o lamento do tornado, exatamente (coro 'a'), as cordas em cluster com
#    trêmolo, o tímpano manco, e o coração que dispara (132/min);
#  3 a horda, "o grito": os metais graves (no máximo dois por bloco) com o golpe, o coro dos homens uma oitava abaixo,
#    a soprano em cluster nos compassos 3 e 4, o motor do cello em semicolcheias, as cordas em enxame (a cunha abre no
#    A, o cerco fecha no B e no C), um grito de metal, o rufo.
# O lamento corre por 8 compassos, na alternância do maestro (A, depois B ou C, depois A...): o A cai de F5/B4 até
# D5/Ab4; o B continua caindo até a dominante (A7, b9); o C sobe de volta até o F5 do A seguinte. O silêncio também é
# arma: metade dos B/C cortam tudo (até o eco) nos 2 últimos tempos, e dois A param o pulso por um compasso.
# Os momentos: a entrada (te viram: 2 tempos de subida até o golpe, que cai no primeiro tempo do perigo; o grito do
# coro vira o lamento), a saída (o lamento expira) e a saída pela pedra (o grito congela; a pedra se forma, estalando).
# Uso (nesta pasta): python lamento.py            compõe em blocos/lamento/ (a chave 'lamento' do manifesto; o perigo
#                                                 de hoje fica como está) e grava as demonstrações em saida/
#                    python lamento.py perigo     compõe no lugar do perigo (blocos/perigo/, a chave 'perigo')
#                    python lamento.py exportar   leva SÓ o perigo para Assets/_Game/Audio/Trilha/perigo e atualiza o
#                                                 trilha.json (o exportar.py apaga a tempestade e o tornado)
# A volta: o perigo antigo continua no blocos.py (python blocos.py blocos perigo).
import os
import sys
import json
import subprocess

MODO = sys.argv[1] if len(sys.argv) > 1 else ''
sys.argv = [sys.argv[0], 'blocos']   # o blocos.py lê a pasta daqui
import numpy as np
import scipy.io.wavfile as wf
import blocos as BL
import instrumentos as I
import instrumentos_terror  # noqa: F401  (põe os instrumentos do terror no estúdio)
from estudio import Peca, mixar, SR

AQUI = os.path.dirname(os.path.abspath(__file__))
DESTINO = os.path.normpath(os.path.join(AQUI, '..', '..', 'Assets', '_Game', 'Audio', 'Trilha'))

LAM = dict(bpm=66, compassos=4, eco=(4.6, 2.8, 6.0, 0.03), molhado=0.5, cauda=6.0, alvo=-17.5)
# por plano: o grave de cada compasso, a voz de cima do lamento (a de baixo é um trítono abaixo), as cordas do cluster
# e o tímpano manco (a raiz no tempo 1, a quinta abaixo no 2,5)
PLANOS = {
    'A': dict(raizes=[38, 38, 38, 38], lam=[77, 76, 75, 74], cordas=[[50, 51, 56]] * 4, manco=[(38, 33)] * 4),
    'B': dict(raizes=[34, 34, 33, 33], lam=[73, 72, 71, 70], cordas=[[46, 47, 52]] * 2 + [[45, 46, 51]] * 2,
              manco=[(34, 41)] * 2 + [(33, 40)] * 2),
    'C': dict(raizes=[38, 38, 39, 39], lam=[73, 74, 75, 76], cordas=[[50, 51, 56]] * 2 + [[51, 52, 57]] * 2,
              manco=[(38, 33)] * 2 + [(39, 34)] * 2),
}
SEMENTES = {'A': 6, 'B': 4, 'C': 4}   # o A toca um bloco sim, um não: mais variações dele
BRAAM = {38: [26, 38, 45, 51], 34: [34, 41, 46, 47], 33: [33, 40, 45, 46], 39: [39, 46, 51, 52]}
AJUSTE = {'entrada': 0.45, 'saida': 0.55, 'pedra': 0.5}   # o golpe por cima do perigo não pode estourar; a saída não
#                                                            pode soar como "a música voltou"


def escolhas(plano, s):
    """O que muda junto nas três camadas de um bloco."""
    r = np.random.default_rng(s * 7 + ord(plano) + 6600)
    q = dict(
        corte=plano != 'A' and s in (0, 2),           # o silêncio nos 2 últimos tempos
        parada=plano == 'A' and s in (1, 4),          # o pulso para no compasso 3
        metal=s % 2 == 0, metal_nota=int(r.choice([86, 88, 89])), metal_curva=float(r.choice([-1.0, 1.0])),
        metal_lado=float(r.choice([-0.6, 0.6])),
        braam2=plano == 'A' and s in (2, 5),
        grito=float(r.choice([6.0, 12.0])),           # o tempo do grito de metal
        grito_nota=int(r.choice([91, 93, 94])), grito_curva=float(r.choice([-4.0, 3.0])),
        uivo=plano == 'B' and s in (1, 3),            # o coro sobe do Bb4 até o F5 do A seguinte
    )
    # os tiques do arco de madeira: de 2 a 4 por compasso, em semicolcheias soltas
    q['tiques'] = []
    for c in range(4):
        k = int(r.integers(2, 5))
        for slot in sorted(r.choice(16, k, replace=False)):
            q['tiques'].append((c, int(slot), int(r.choice([62, 63, 74])), float(r.uniform(0.035, 0.06)), float(r.uniform(-0.7, 0.7))))
    return q


def lamento(plano, s, camada):
    """Um bloco de 4 compassos, uma camada: 1 a tensão, 2 a perseguição, 3 a horda."""
    I.sorte(s * 10 + camada + 6000 + ord(plano) * 100)
    P = PLANOS[plano]
    q = escolhas(plano, s)
    raizes = P['raizes']
    p = Peca(f'lamento_{plano}{s}_c{camada}', LAM['bpm'])
    T, B = p.T, p.b
    fim = T(3, 2) if q['corte'] else T(4)

    def bate(c):   # o pulso existe neste compasso?
        return not (q['parada'] and c == 2)

    if camada == 1:
        # o chão: o ronco (sempre o ré grave) e o baixo da raiz, batendo devagar, por trecho de raiz igual
        c = 0
        while c < 4:
            k = c
            while k < 4 and raizes[k] == raizes[c]:
                k += 1
            dur = (T(k) - T(c)) + (0.8 if k == 4 and not q['corte'] else 0.3)
            if k == 4 and q['corte']:
                dur = fim - T(c)
            solta = 0.3 if k == 4 and q['corte'] else 1.2
            p.add('ronco', T(c), dur, 26, 0.08, eco=0.15, ataque=0.5 if c else 0.8, solta=solta, batimento=0.5)
            p.add('baixo', T(c), dur, raizes[c], 0.075, eco=0.2, ataque=0.5, solta=solta, batimento=0.5)
            c = k
        # o coração em repouso: uma batida por tempo (66/min)
        for c in range(4):
            if not bate(c):
                continue
            for tempo in range(4):
                t = T(c, tempo)
                if t >= fim - 0.05:
                    continue
                p.add('coracao', t, 0, 1.0, 0.065, eco=0.2)
                p.add('coracao', t + 0.2, 0, 1.25, 0.045, eco=0.2)
        # o lamento de boca fechada, longe
        for c in range(4):
            d = min(B(4.3), fim - T(c))
            if d > 0.2:
                p.acorde('coro', T(c), d, [P['lam'][c], P['lam'][c] - 6], 0.018, abertura=0.7, eco=0.9, vogal='u', ataque=1.0,
                         solta=0.3 if q['corte'] and c == 3 else 2.0)
        # o ar: o metal friccionado, ou o fio agudo e os sussurros
        if q['metal']:
            p.add('metal', T(1), min(B(10), fim - T(1)), q['metal_nota'], 0.022, lado=q['metal_lado'], eco=0.85, ataque=3,
                  solta=0.3 if q['corte'] else 4, curva=q['metal_curva'])
        else:
            p.acorde('cordas', T(0), fim - T(0) if q['corte'] else B(16), [81, 82], 0.010, abertura=0.6, eco=0.8, ataque=3,
                     solta=0.3 if q['corte'] else 3, brilho=0.6)
            p.add('sussurro', T(1), B(8), 60, 0.025, eco=0.75, silabas=5.0, ataque=1.5, solta=1.5)
        # algo andando na parede (continua na parada; some no corte)
        for c, slot, nota, v, lado in q['tiques']:
            t = T(c, slot * 0.25)
            if t < fim - 0.05:
                p.add('raspado', t, 0, nota, v, lado=lado, eco=0.5)
    elif camada == 2:
        # O LAMENTO (o do tornado, exatamente)
        for c in range(4):
            d = min(B(4.3), fim - T(c))
            p.acorde('coro', T(c), d, [P['lam'][c], P['lam'][c] - 6], 0.05, abertura=0.5, eco=0.8, vogal='a', ataque=0.6,
                     solta=0.3 if q['corte'] and c == 3 else 1.5)
        # as cordas em cluster com trêmolo, por trecho de cluster igual
        c = 0
        while c < 4:
            k = c
            while k < 4 and P['cordas'][k] == P['cordas'][c]:
                k += 1
            dur = (T(k) - T(c)) if k < 4 else (fim - T(c))
            p.acorde('cordas', T(c), dur, P['cordas'][c], 0.035, abertura=0.5, eco=0.5, ataque=1 if c == 0 else 0.4,
                     solta=0.3 if (k == 4 and q['corte']) else 2, tremolo=10, prof=0.7)
            c = k
        # o coração dispara: a batida no contratempo (com a da camada 1, 132/min) e o reforço no tempo
        for c in range(4):
            if not bate(c):
                continue
            for tempo in range(4):
                for t, vv in ((T(c, tempo + 0.5), 0.16), (T(c, tempo), 0.075)):
                    if t >= fim - 0.05:
                        continue
                    p.add('coracao', t, 0, 1.0, vv, eco=0.2)
                    p.add('coracao', t + 0.2, 0, 1.25, vv * 0.7, eco=0.2)
        # o tímpano que manca (o do lamento)
        for c in range(4):
            if not bate(c) and c != 0:
                continue
            a, b = P['manco'][c]
            if T(c) < fim - 0.05:
                p.add('timpano', T(c), 0, a, 0.16, eco=0.5, decai=1.8)
            if T(c, 2.5) < fim - 0.05 and bate(c):
                p.add('timpano', T(c, 2.5), 0, b, 0.10, eco=0.5, decai=2.5)
    else:
        # os metais graves (no máximo dois por bloco) e o golpe junto
        onde = [0] if plano == 'A' else [2]
        if q['braam2']:
            onde.append(2)
        for c in onde:
            # no B o braam cai junto com o tímpano manco e o coração (o pico das 3 camadas): um pouco mais baixo
            forte = (1.0 if c == 0 else 0.75) if plano == 'A' else (0.8 if plano == 'B' else 0.85)
            for m in BRAAM[raizes[c]]:
                p.add('braam', T(c), min(B(3), fim - T(c)), m, 0.14 * forte, lado=(m - raizes[c]) / 24, eco=0.45, ataque=0.08,
                      solta=1.4, rosnado=0.4)
            if not (plano == 'A' and c == 0):   # no 1 do A o golpe vem da entrada (quando há)
                p.add('impacto', T(c), 0, 26, 0.10 * forte, eco=0.5, decai=1.2)
        # o coro cheio: os homens uma oitava abaixo; a soprano em cluster nos compassos 3 e 4
        for c in range(4):
            d = min(B(4.2), fim - T(c))
            solta = 0.3 if q['corte'] and c == 3 else 1.5
            p.add('gemido', T(c), d, P['lam'][c] - 12, 0.07, lado=-0.2, eco=0.75, vogal='a', fecha='o', ataque=0.3, solta=solta,
                  queda=-1.0, queda_em=0.8, vozes=5, espalho=20)
            if c >= 2:
                p.acorde('gemido', T(c), d, [P['lam'][c] + 6, P['lam'][c] + 7], 0.02, abertura=0.8, eco=0.85, vogal='a', fecha='a',
                         ataque=0.8, solta=solta, queda=-1.0, queda_em=0.85, vozes=4, espalho=25)
        # o motor: o cello em semicolcheias na raiz, o semitom de cima no fim de cada meio compasso
        for c in range(4):
            if not bate(c):
                continue
            for k in range(16):
                t = T(c, k * 0.25)
                if t >= fim - 0.05:
                    continue
                m = raizes[c] + 12 + (1 if k % 8 == 7 else 0)
                p.add('cello', t, B(0.22), m, 0.14 * (1.0 if k % 4 == 0 else 0.6), lado=0.3, eco=0.25, ataque=0.01, solta=0.06)
        # as cordas em enxame: no A a cunha abre a partir do ré; no B e no C o cerco fecha nele (o 1 do A seguinte)
        enxame = dict(eco=0.5, largura=1.0, ponticello=0.4, tremolo=12, prof=0.6)
        if plano == 'A':
            p.add('cluster', T(0), B(8), 62, 0.02, vozes=6, curva=7, direcao=0, ataque=0.5, solta=1.5, **enxame)
        else:
            ini = T(1, 2) if q['corte'] else T(2)
            dur = (fim - ini)
            p.add('cluster', ini, dur, 69, 0.016, lado=-0.35, vozes=5, curva=7, direcao=-1, ataque=1.0, solta=0.3, **enxame)
            p.add('cluster', ini, dur, 55, 0.016, lado=0.35, vozes=5, curva=7, direcao=1, ataque=1.0, solta=0.3, **enxame)
        # o grito de metal (um por bloco), menos nos blocos com corte
        if not q['corte']:
            p.add('metal', B(q['grito']), B(4), q['grito_nota'], 0.045, lado=-q['metal_lado'], eco=0.75, ataque=0.08, solta=1.5,
                  curva=q['grito_curva'])
        # o fim: o rufo puxando o próximo, ou o prato ao contrário e a subida até o corte
        if q['corte']:
            p.add('prato_reverso', T(3), B(2), 60, 0.09, eco=0.2)
            p.add('subida', T(2, 2), B(4), 57, 0.10, lado=0.2, eco=0.2)
        else:
            for k in range(8):
                p.add('timpano', T(3, 2 + k * 0.25), 0, raizes[3], 0.05 + 0.013 * k, eco=0.35, decai=3.5)
        if q['uivo']:
            p.add('coro', T(3, 3), B(1), 70, 0.04, eco=0.8, vogal='a', ataque=0.15, solta=0.6, curva=7.0)
    return p, q


def momento(tipo, s):
    """A entrada (te viram), a saída e a saída pela pedra: (a peça, o encaixe, a duração)."""
    I.sorte(s + 6400)
    r = np.random.default_rng(s + 6500)
    p = Peca(f'lamento_{tipo}_{s}', LAM['bpm'])
    T, B = p.T, p.b
    if tipo == 'entrada':
        golpe = B(2)
        if s == 0:      # o puxão e meio tempo de vácuo
            for m in (50, 51, 56, 62):
                p.add('reverso', 0.0, B(1.5), m, 0.24, lado=(m - 55) / 10, eco=0.15, desafinado=9.0)
            p.add('prato_reverso', 0.0, B(1.5), 60, 0.12, eco=0.1)
        elif s == 1:    # o puxão inteiro e o coro inspirando, subindo uma oitava até o primeiro par do lamento
            for m in (45, 50, 51, 57):
                p.add('reverso', 0.0, golpe, m, 0.24, lado=(m - 55) / 10, eco=0.15, desafinado=9.0)
            p.add('subida', 0.0, golpe, 57, 0.12, lado=0.2, eco=0.2)
            p.acorde('coro', 0.2, golpe - 0.2, [59, 65], 0.03, abertura=0.6, eco=0.6, vogal='u', ataque=golpe - 0.25, solta=0.05, curva=12.0)
        else:           # quase nada: o vidro subindo, um quarto de tempo de vácuo
            p.add('metal', 0.0, B(1.75), 88, 0.05, lado=0.4, eco=0.3, ataque=B(1.6), solta=0.03, curva=2.0)
        p.add('impacto', golpe, 0, 26, 0.32, eco=0.6, decai=0.9)
        p.add('timpano', golpe, 0, 38, 0.35, eco=0.5, decai=1.2)
        p.acorde('cordas', golpe, 0.5, [38, 39, 45, 50, 51], 0.12, abertura=0.7, eco=0.6, ataque=0.02, solta=2.5, brilho=1.0)
        for k in range(3):
            p.add('estridente', golpe + k * 0.07, B(1.5), [89, 88, 87][k], 0.07, lado=-0.5 + 0.5 * k, eco=0.6, curva=-4.0, decai=2)
        # o grito do coro: o F5/B4 vira o lamento que a perseguição abre no mesmo tempo
        p.acorde('gemido', golpe, B(3), [78, 77, 71], 0.06, abertura=0.6, eco=0.8, vogal='a', fecha='u', ataque=0.04, solta=2.5,
                 queda=-1.5, queda_em=0.5, vozes=5, espalho=25)
        return p, golpe, golpe + B(5) + 2.0
    if tipo == 'saida':
        p.add('timpano', 0.0, 0, 38, 0.28, eco=0.5, decai=1.6)
        p.add('ronco', 0.0, B(6), 26, 0.14, eco=0.15, ataque=0.05, solta=5, batimento=0.5)
        if s == 0:      # o lamento expira (cai meio tom, num trítono que não resolve)
            p.acorde('coro', 0.0, B(5), [74, 68], 0.04, abertura=0.5, eco=0.85, vogal='a', ataque=0.05, solta=3.5, curva=-1.0)
        elif s == 1:    # o tema perdido no cello, até o dó sustenido
            p.frase('cello', 0, 0, [(57, 2), (55, 1), (53, 1), (49, 6)], 0.15, lado=-0.2, eco=0.55)
            p.acorde('coro', 0.0, B(8), [62, 69], 0.022, abertura=0.5, eco=0.9, vogal='u', ataque=0.3, solta=3)
        else:           # o coração para
            p.add('piano', B(6), 3.0, 26, 0.18, eco=0.6, corte=700, desafinado=8.0)
            p.add('piano', B(6) + 0.02, 3.0, 33, 0.16, eco=0.6, corte=700, desafinado=8.0)
        for k, (t, v) in enumerate([(1.0, 0.16), (2.6, 0.11), (4.4, 0.07), (6.4, 0.04)]):
            if s == 2 and k == 3:
                break
            p.add('coracao', B(t), 0, 1.0, v, eco=0.3)
            p.add('coracao', B(t) + 0.28, 0, 1.25, v * 0.7, eco=0.3)
        return p, 0.0, B(8)
    # a pedra: o vidro afunda, o grito congela e fecha a boca, a pedra se forma estalando, o baque, a paz da pedra
    for m, v, lado in [(81, 0.045, -0.4), (82, 0.04, 0.4), (88, 0.028, 0.0)]:
        p.add('metal', 0.0, 4.0, m + (s % 2), v, lado=lado, eco=0.8, ataque=0.4, solta=2.0, curva=-1.0)
    p.acorde('gemido', 0.0, B(5), [74, 68], 0.05, abertura=0.4, eco=0.85, vogal='a', fecha='u', ataque=0.3, solta=2.0, queda=-1.0, queda_em=0.5)
    u = np.sort(r.uniform(0, 1, 18))
    for k, x in enumerate(u):
        t = B(1) + (B(5.8) - B(1)) * float(np.sqrt(x))
        nota = int(round(r.uniform(72, 86))) if s == 0 else int(round(86 - 14 * k / 17))
        p.add('raspado', t, 0, nota, 0.03 + 0.025 * k / 17, lado=float(r.uniform(-0.8, 0.8)), eco=0.5)
    p.add('impacto', B(6), 0, 24, 0.35, eco=0.6, decai=1.4)
    p.add('piano', B(6), 3.0, 26, 0.2, eco=0.6, corte=700, desafinado=8.0)
    p.add('piano', B(6) + 0.02, 3.0, 33, 0.18, eco=0.6, corte=700, desafinado=8.0)
    p.acorde('coro', B(6.5), B(5), [50, 57], 0.028, abertura=0.4, eco=0.9, vogal='u', ataque=1.0, solta=3)
    return p, 0.0, B(6) + 5.0


# ============================================================================================== o estúdio
def portao(e, d, t, resto=0.03, fade=0.05):
    """O corte: tudo (até o eco) cai para quase nada a partir de t."""
    i0, m = int(t * SR), int(fade * SR)
    g = np.ones(len(e), np.float32)
    g[i0:i0 + m] = np.linspace(1, resto, m)
    g[i0 + m:] = resto
    return e * g, d * g


def db(x):
    return round(float(20 * np.log10(max(x, 1e-12))), 1)


def pico(e, d):
    return max(float(np.abs(e).max()), float(np.abs(d).max()))


def familia(chave):
    """Como o familia_perigo do blocos.py (o mesmo manifesto): os blocos (3 camadas cada), um volume só para a família
    (as 3 camadas juntas no alvo) e os momentos. Grava em blocos/<chave>/<chave>_<plano><semente>_c<camada>.wav."""
    comprimento = Peca('x', LAM['bpm']).T(LAM['compassos'])
    corte_em = Peca('x', LAM['bpm']).T(3, 2)
    n = int(comprimento * SR)
    brutos = []
    for plano in 'ABC':
        for s in range(SEMENTES[plano]):
            cs = []
            for c in (1, 2, 3):
                p, q = lamento(plano, s, c)
                e, d = mixar(p, comprimento, LAM['eco'], LAM['molhado'], LAM['cauda'])
                if q['corte']:
                    e, d = portao(e, d, corte_em)
                e, d = I.filtrar(e, 'high', 28, 2), I.filtrar(d, 'high', 28, 2)   # o sub que não se ouve só come margem
                cs.append((e.astype(np.float32), d.astype(np.float32)))
            brutos.append((plano, s, cs))
            print(f'  {plano}{s} composto', flush=True)
    ref = [BL.rms(sum(c[0] for c in cs), sum(c[1] for c in cs), n) for _, _, cs in brutos]
    g = 10 ** (LAM['alvo'] / 20) / float(np.median(ref))
    destino = os.path.join(BL.PASTA, chave)
    os.makedirs(destino, exist_ok=True)
    for a in os.listdir(destino):
        if a.endswith('.wav') and a.startswith(chave + '_'):
            os.remove(os.path.join(destino, a))
    blocos = []
    print(f'{"bloco":6} {"c1":>6} {"c1+c2":>6} {"todas":>6}  pico das 3')
    for plano, s, cs in brutos:
        arquivos = []
        for c, (e, d) in enumerate(cs, 1):
            caminho = os.path.join(destino, f'{chave}_{plano}{s}_c{c}.wav')
            BL.gravar(caminho, e * g, d * g)
            arquivos.append(os.path.relpath(caminho, BL.PASTA).replace('\\', '/'))
        niveis = [db(BL.rms(sum(c[0] for c in cs[:k]) * g, sum(c[1] for c in cs[:k]) * g, n)) for k in (1, 2, 3)]
        p3 = db(pico(sum(c[0] for c in cs) * g, sum(c[1] for c in cs) * g))
        blocos.append(dict(nome=f'{chave}_{plano}{s}', plano=plano, camadas=arquivos, rms_por_nivel=niveis, pico_db=p3))
        print(f'{plano}{s:<5} {niveis[0]:6.1f} {niveis[1]:6.1f} {niveis[2]:6.1f}  {p3:6.1f}')
    momentos = []
    for tipo, k in (('entrada', 3), ('saida', 3), ('pedra', 2)):
        for s in range(k):
            p, encaixe, dur = momento(tipo, s)
            e, d = mixar(p, dur, LAM['eco'] if tipo == 'saida' else (4.5, 2.5, 6.0, 0.02), 0.55, 4.0)
            e, d = I.filtrar(e, 'high', 28, 2) * g * AJUSTE[tipo], I.filtrar(d, 'high', 28, 2) * g * AJUSTE[tipo]
            m = int(1.0 * SR)
            e[-m:] *= np.linspace(1, 0, m)
            d[-m:] *= np.linspace(1, 0, m)
            nome = f'{chave}_{tipo}_{s}'
            caminho = os.path.join(destino, nome + '.wav')
            BL.gravar(caminho, e, d)
            momentos.append(dict(nome=nome, tipo=tipo, encaixe=round(encaixe, 6), arquivo=os.path.relpath(caminho, BL.PASTA).replace('\\', '/'),
                                 pico_db=db(pico(e, d)), rms_db=db(BL.rms(e, d))))
            extra = ''
            if tipo == 'entrada':   # o pior caso: a entrada por cima do A0 com as 3 camadas
                _, _, cs = brutos[0]
                i0 = int(encaixe * SR)
                L = max(len(e), i0 + len(cs[0][0]))
                me, md = np.zeros(L), np.zeros(L)
                me[:len(e)] += e
                md[:len(d)] += d
                for (ce, cd), vol in zip(cs, (1.0, 0.9, 0.85)):
                    me[i0:i0 + len(ce)] += ce * g * vol
                    md[i0:i0 + len(cd)] += cd * g * vol
                extra = f', com o A0 inteiro por baixo: pico {db(pico(me, md))} dB'
            print(f'{nome}: pico {momentos[-1]["pico_db"]} dB, rms {momentos[-1]["rms_db"]} dB, encaixe {encaixe:.4f} s{extra}')
    print(f'{chave}: {len(blocos)} blocos x 3 camadas de {comprimento:.3f} s, ganho {db(g)} dB')
    return dict(bpm=LAM['bpm'], compassos=LAM['compassos'], comprimento=comprimento, blocos=blocos, momentos=momentos)


# ============================================================================================== para ouvir
_cache = {}


def _ler(arq):
    if arq not in _cache:
        _, x = wf.read(os.path.join(BL.PASTA, arq))
        _cache[arq] = x.astype(np.float32) / 32767.0
    return _cache[arq]


def _seguir(alvo, m):
    """O volume de uma camada seguindo o alvo como no jogo: sobe em 0,6 s, desce em 3 s."""
    g = np.zeros(m, np.float32)
    atual = float(alvo[0])
    for j in range(0, m, 441):
        a = float(alvo[j])
        passo = (1 / 0.6 if a > atual else 1 / 3.0) * 441 / SR
        atual = min(a, atual + passo) if a > atual else max(a, atual - passo)
        g[j:j + 441] = atual
    return g


def _vento(mix, man, ganho=0.35):
    """O vento da noite por baixo, como no jogo durante a música."""
    v = man['vento_noite']
    t, k = 0.0, 0
    n = len(mix)
    while int(t * SR) < n:
        b = v['blocos'][k % len(v['blocos'])]
        x = _ler(b['arquivo'])
        i0 = int(t * SR)
        m = min(len(x), n - i0)
        mix[i0:i0 + m] += x[:m] * ganho
        t += b['duracao'] - v['sobreposicao']
        k += 3


def _gravar(nome, mix):
    p = float(np.abs(mix).max())
    mix = BL.limitar(mix, 0.7, 0.95)   # o limitador do grupo da música
    wav = os.path.join(AQUI, 'saida', nome + '.wav')
    os.makedirs(os.path.dirname(wav), exist_ok=True)
    wf.write(wav, SR, (mix * 32767).astype(np.int16))
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', wav, '-b:a', '224k', wav[:-4] + '.mp3'], check=True)
    print(f'{nome}.mp3: {len(mix) / SR / 60:.1f} min, pico antes do limitador {db(p)} dB')
    return wav[:-4] + '.mp3'


def _mmss(t):
    return f'{int(t // 60)}:{int(t % 60):02d}'


def demonstracoes(chave):
    man = json.load(open(os.path.join(BL.PASTA, 'manifesto.json'), encoding='utf-8'))
    F = man[chave]
    passo = F['comprimento']
    por = {b['nome'].split('_')[-1]: b for b in F['blocos']}
    momento = lambda nome: next(m for m in F['momentos'] if m['nome'] == f'{chave}_{nome}')
    saidas = []

    # 1) a progressão inteira: a sombra (só a c1, baixa), te viram, a perseguição, a horda, a volta e a saída
    roteiro = [('A0', 1), ('B0', 1), ('A1', 2), ('C1', 2), ('A2', 3), ('B1', 3), ('A3', 3), ('C2', 2), ('A4', 1), ('B3', 1)]
    t0 = 3.0
    golpe = t0 + 2 * passo
    saida = momento('saida_0')
    n = int((t0 + passo * len(roteiro)) * SR) + len(_ler(saida['arquivo']))
    mix = np.zeros((n, 2), np.float32)
    nivel = np.zeros(n)
    for i, (_, nv) in enumerate(roteiro):
        a = int((t0 + i * passo) * SR)
        nivel[a:a + int(passo * SR)] = nv
    sombra = np.zeros(n, bool)
    sombra[:int(golpe * SR)] = True           # antes do golpe: a sombra (a c1 a 0,75)
    for i, (nome, _) in enumerate(roteiro):
        i0 = int((t0 + i * passo) * SR)
        for c, vol in zip((1, 2, 3), (1.0, 0.9, 0.85)):
            x = _ler(por[nome]['camadas'][c - 1])
            m = min(len(x), n - i0)
            alvo = (nivel[i0:i0 + m] >= c).astype(np.float32) * np.where(sombra[i0:i0 + m], 0.75, vol).astype(np.float32)
            if i == 0 and c == 1:
                alvo *= np.clip(np.arange(m) / (4.0 * SR), 0, 1)   # a sombra entra em 4 s
            mix[i0:i0 + m] += x[:m] * _seguir(alvo, m)[:, None]
    x = _ler(momento('entrada_1')['arquivo'])
    i0 = int((golpe - momento('entrada_1')['encaixe']) * SR)
    mix[i0:i0 + len(x)] += x[:n - i0]
    x = _ler(saida['arquivo'])
    i0 = int((t0 + len(roteiro) * passo) * SR)
    mix[i0:i0 + min(len(x), n - i0)] += x[:n - i0]
    _vento(mix, man)
    saidas.append(_gravar('ZCC_Lamento_Progressao', mix))
    print(f'  0:00 a sombra (o coro de boca fechada) | {_mmss(golpe)} TE VIRAM: a perseguição | {_mmss(t0 + 4 * passo)} a horda | '
          f'{_mmss(t0 + 7 * passo)} volta a perseguição | {_mmss(t0 + 8 * passo)} a tensão | {_mmss(t0 + 10 * passo)} a saída')

    # 2) e 3) três minutos de uma coisa só: a sombra (a c1 a 0,75) e a horda (as 3 camadas)
    rng = np.random.default_rng(3)
    for nome, vols in (('ZCC_Lamento_Sombra', (0.75, 0, 0)), ('ZCC_Lamento_Horda', (1.0, 0.9, 0.85))):
        k = int(np.ceil(180 / passo))
        # a escolha do maestro: A, depois B ou C, depois A...; sem repetir os últimos (a memória do jogo: opções - 2)
        ordem, ant, recentes = [], None, {pl: [] for pl in 'ABC'}
        for _ in range(k):
            plano = 'A' if ant != 'A' else ('B' if rng.random() < 0.5 else 'C')
            todas = [b for b in F['blocos'] if b['plano'] == plano]
            opcoes = [b for b in todas if b['nome'] not in recentes[plano]] or todas
            b = opcoes[int(rng.integers(len(opcoes)))]
            recentes[plano] = (recentes[plano] + [b['nome']])[-max(1, len(todas) - 2):]
            ordem.append(b)
            ant = plano
        n = int((1.0 + passo * k + 7) * SR)
        mix = np.zeros((n, 2), np.float32)
        for i, b in enumerate(ordem):
            i0 = int((1.0 + i * passo) * SR)
            for c, vol in enumerate(vols, 1):
                if vol <= 0:
                    continue
                x = _ler(b['camadas'][c - 1])
                m = min(len(x), n - i0)
                g = vol * (np.clip(np.arange(m) / (4.0 * SR), 0, 1) if i == 0 else 1.0)
                mix[i0:i0 + m] += x[:m] * (g[:, None] if i == 0 else g)
        m = int(5 * SR)
        mix[-m:] *= np.linspace(1, 0, m)[:, None]
        _vento(mix, man)
        saidas.append(_gravar(nome, mix))
        print('  ' + ' > '.join(b['nome'].split('_')[-1] for b in ordem))
    return saidas


# ============================================================================================== para o jogo
def exportar():
    """Só o perigo: apaga e refaz Audio/Trilha/perigo/*.ogg e muda no trilha.json só o perigo e o encaixe da entrada
    (o exportar.py apaga também a tempestade e o tornado). Depois, no Unity: Zombie City Cleaner > Trilha > Montar a
    trilha."""
    man = json.load(open(os.path.join(AQUI, 'blocos', 'manifesto.json'), encoding='utf-8'))
    F = man['perigo']
    if F.get('bpm') != LAM['bpm']:
        raise SystemExit('o perigo do manifesto não é o lamento: rode antes "python lamento.py perigo"')
    pasta = os.path.join(DESTINO, 'perigo')
    for a in os.listdir(pasta):
        if a.endswith('.ogg'):
            os.remove(os.path.join(pasta, a))
    k = 0
    for b in F['blocos']:
        for arq in b['camadas']:
            BL_ogg(os.path.join(AQUI, 'blocos', arq), os.path.join(pasta, os.path.basename(arq)[:-4] + '.ogg'))
            k += 1
    for m in F['momentos']:
        BL_ogg(os.path.join(AQUI, 'blocos', m['arquivo']), os.path.join(pasta, m['nome'] + '.ogg'))
        k += 1
    caminho = os.path.join(DESTINO, 'trilha.json')
    info = json.load(open(caminho, encoding='utf-8'))
    info['perigo'] = dict(bpm=F['bpm'], comprimento=F['comprimento'])
    info['encaixeDaEntrada'] = next(m for m in F['momentos'] if m['tipo'] == 'entrada')['encaixe']
    with open(caminho, 'w', encoding='utf-8') as f:
        json.dump(info, f, indent=1)
    print(f'{k} arquivos do perigo em {pasta}; trilha.json: perigo {info["perigo"]}, encaixe {info["encaixeDaEntrada"]}')


def BL_ogg(origem, destino):
    os.makedirs(os.path.dirname(destino), exist_ok=True)
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', origem, '-c:a', 'libvorbis', '-q:a', '5', destino], check=True)


if __name__ == '__main__':
    os.chdir(AQUI)
    if MODO == 'exportar':
        exportar()
    else:
        chave = 'perigo' if MODO == 'perigo' else 'lamento'
        caminho = os.path.join(BL.PASTA, 'manifesto.json')
        man = json.load(open(caminho, encoding='utf-8'))
        man[chave] = familia(chave)
        open(caminho, 'w', encoding='utf-8').write(json.dumps(man, ensure_ascii=False, indent=1, default=float))
        print('manifesto gravado')
        if chave == 'lamento':
            for s in demonstracoes(chave):
                print(s)
