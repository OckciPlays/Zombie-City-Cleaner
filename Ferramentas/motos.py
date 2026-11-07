# Os sons das motos (06/11/2026, a pedido: "vamos arrumar os sons delas"), gerados por código (numpy e scipy), como os
# do carro (Vehicles/SomDoCarro), com as medidas tiradas de três vídeos do usuário (a Harley em marcha lenta e
# acelerando, a motocross em ponto morto e acelerando). Os vídeos só serviram de medida; nada deles vai no jogo.
#
#   python Ferramentas/motos.py [pasta]   (grava em Assets/_Game/Audio/Motos, sem a pasta)
#
# Cada moto tem:
# - <moto>_Motor_<n>.wav: laços de 1 s do motor, do giro lento ao rápido (GIROS: quantas vezes a marcha lenta). O jogo
#   toca os dois laços mais perto do giro de agora, misturados, cada um com o pitch perto de 1 (Carro.Moto.cs): o tom do
#   escape não estica como no laço único do carro;
# - <moto>_Pegar.wav: o motor pegando (1,25 s: as primeiras explosões falham, ele sobe e assenta na marcha lenta);
# - <moto>_Buzina.wav: a buzina, um laço de 1 s (um tom só, mais fina que a do carro).
#
# O que foi medido (as faixas do espectro, o centróide e o ritmo das explosões):
# - A Harley (o V2 de 45°): em marcha lenta, um ciclo de 720° a cada 92 ms (10,9 por segundo), com as duas explosões
#   desiguais (315° e 405°: 40 e 52 ms) e um eco de cada uma no escape. 95% da energia abaixo de 150 Hz, o centróide em
#   ~110 Hz. Acelerando, o ciclo chega a ~20 por segundo e a faixa de 150 a 400 Hz sobe para 16%.
# - A cross (o monocilíndrico): em ponto morto, 15,5 explosões por segundo (64 ms), a energia entre 60 e 400 Hz (78%) e
#   ~15% de chiado de 1 a 6 kHz (o centróide em ~650 Hz). No giro alto, ~150 a 160 explosões por segundo (até 210 nas
#   aceleradas), e o som fica rasgado: 44% de 2,5 a 6 kHz, o centróide em ~2,7 kHz.
#
# Cada explosão é um pulso de pressão (meio seno) com um ruído curto, que passa pelas ressonâncias do escape (um banco de
# filtros de pico em paralelo) e por um filtro do chiado. O laço tem um número inteiro de ciclos e é filtrado três vezes
# seguidas (só a do meio é usada: os filtros já assentaram), então não tem emenda.
import os
import sys

import numpy as np
from scipy import signal
from scipy.io import wavfile

SR = 44100
AQUI = os.path.dirname(os.path.abspath(__file__))
SAIDA = os.path.join(AQUI, "..", "Assets", "_Game", "Audio", "Motos")

# o giro de cada laço (vezes a marcha lenta); o Carro mistura os dois mais perto (os mesmos números na montagem das motos)
GIROS = {"FatBoy": [1.0, 1.6, 2.5, 3.6], "Cross": [1.0, 2.0, 3.4, 5.5, 8.5]}

MOTOS = {
    # o V2 de 45°: duas explosões por ciclo (0 e 315°), o eco no escape, o ronco grave (60 a 150 Hz) e quase nada de
    # chiado
    "FatBoy": dict(
        ciclo=10.9, explosoes=[0.0, 315.0 / 720.0], pulso=0.007, eco=(0.022, 0.42),
        modos=[(72.0, 3.0, 0.8), (100.0, 3.5, 1.0), (135.0, 4.0, 0.55), (200.0, 5.0, 0.3), (320.0, 5.0, 0.1)],
        passa_alta=55.0,
        ruido_banda=(110.0, 520.0), ruido_tau=0.016,
        meio_banda=(500.0, 2200.0), meio_tau=0.006,
        chiado_banda=(2200.0, 6000.0), chiado_tau=0.003,
        # a energia de cada parte (o ronco, o ruído grave, o rasgado do meio e o chiado), na marcha lenta e no giro mais
        # alto dos laços
        mistura=((0.905, 0.08, 0.01, 0.005), (0.80, 0.16, 0.03, 0.01)),
        saturar=1.1, variacao=(0.14, 0.035),
    ),
    # o monocilíndrico: uma explosão por ciclo e o ronco rasgado nos médios ("braaap"). A primeira versão seguia o vídeo
    # acelerando (o chiado de 2,5 a 6,5 kHz tomava conta no giro alto: o centróide em 2,4 a 3 kHz) e ficou aguda e
    # irritante no jogo ("muito agudo... um pouco irritante", 06/11/2026): agora o rasgado fica de 350 a 1,8 kHz, o chiado
    # é pouco e para em 4,5 kHz, o estalo é mais macio (4,5 ms) e tudo passa por um passa-baixa de 5 kHz
    # A terceira versão (a pedido: "voltar um pouco mais pro agudo") fica no meio das duas: o rasgado vai até 2,4 kHz, o
    # chiado volta até 5,5 kHz com mais força no giro alto, o estalo é de 3,6 ms e o passa-baixa sobe para 7 kHz
    "Cross": dict(
        ciclo=15.5, explosoes=[0.0], pulso=0.0036, eco=(0.007, 0.3),
        modos=[(105.0, 2.5, 0.7), (165.0, 3.0, 1.0), (250.0, 3.0, 0.9), (380.0, 3.0, 0.6), (580.0, 2.5, 0.4), (900.0, 2.0, 0.22),
               (1400.0, 2.0, 0.1)],
        passa_alta=70.0,
        ruido_banda=(150.0, 900.0), ruido_tau=0.011,
        meio_banda=(400.0, 2400.0), meio_tau=0.006,
        chiado_banda=(2000.0, 5500.0), chiado_tau=0.0035,
        mistura=((0.80, 0.06, 0.08, 0.06), (0.43, 0.14, 0.29, 0.14)),
        passa_baixa=7000.0, saturar=1.25, variacao=(0.08, 0.02),
    ),
}


def _pico(f, q):
    b, a = signal.iirpeak(f / (SR / 2), q)
    return b, a


def _banda(f0, f1, ordem=2):
    return signal.butter(ordem, [f0 / (SR / 2), min(f1, SR / 2 - 100) / (SR / 2)], "band")


def _excitacao(m, giro, n_ciclos, rng, laco=True, tempos=None, forcas=None):
    """O pulso de pressão e o ruído de cada explosão (o comprimento: n_ciclos ciclos exatos, ou os tempos dados)."""
    ciclo = m["ciclo"] * giro
    if tempos is None:
        L = int(round(n_ciclos * SR / ciclo))
        periodo = L / n_ciclos
        tempos, forcas = [], []
        va, vt = m["variacao"]
        for c in range(n_ciclos):
            for k, fase in enumerate(m["explosoes"]):
                intervalo = periodo * (m["explosoes"][(k + 1) % len(m["explosoes"])] - fase) % periodo or periodo
                t = (c + fase) * periodo + rng.normal(0, vt) * intervalo * 0.5
                tempos.append(t)
                forcas.append(max(0.4, 1.0 + rng.normal(0, va)))
    else:
        L = int(max(tempos) + SR * 0.5)
    pulso = np.zeros(L)
    ruidos = {k: np.zeros(L) for k in ("ruido", "meio", "chiado")}
    w = max(3, int(m["pulso"] * SR))
    forma = np.sin(np.pi * np.arange(w) / w)
    eco_t, eco_g = m["eco"]

    def somar(v, i0, dados):
        idx = (i0 + np.arange(len(dados))) % L if laco else np.clip(i0 + np.arange(len(dados)), 0, L - 1)
        np.add.at(v, idx, dados)

    for t, a in zip(tempos, forcas):
        for atraso, ganho in ((0.0, 1.0), (eco_t, eco_g)):
            somar(pulso, int(round(t + atraso * SR)), forma * a * ganho)
        for k in ruidos:
            tau = m[k + "_tau"]
            n = int(SR * tau * 5)
            somar(ruidos[k], int(round(t)), rng.normal(0, 1, n) * np.exp(-np.arange(n) / (SR * tau)) * a)
    return pulso, ruidos


def _rms(v):
    return np.sqrt((v ** 2).mean()) + 1e-12


def _filtrar(m, giro, pulso, ruidos, laco=True, giro_max=None):
    """
    As quatro partes (o ronco das ressonâncias do escape, o ruído grave, o rasgado do meio e o chiado), cada uma
    normalizada e somada com a energia da mistura (entre a da marcha lenta e a do giro mais alto); com laco, filtra três
    voltas e fica com a do meio.
    """
    if laco:
        pulso = np.tile(pulso, 3)
        ruidos = {k: np.tile(v, 3) for k, v in ruidos.items()}
    nome = next(k for k, v in MOTOS.items() if v is m)
    gmax = giro_max or GIROS[nome][-1]
    x = np.clip((giro - 1.0) / max(1e-6, gmax - 1.0), 0.0, 1.0) ** 0.8   # o rasgado chega antes do fim do giro
    lento, rapido = m["mistura"]
    energia = [a + (b - a) * x for a, b in zip(lento, rapido)]
    ronco = np.zeros_like(pulso)
    for f, q, g in m["modos"]:
        b, a = _pico(f, q)
        ronco += signal.lfilter(b, a, pulso) * g
    b, a = signal.butter(2, m["passa_alta"] / (SR / 2), "high")
    ronco = signal.lfilter(b, a, ronco)
    partes = [ronco]
    for k in ("ruido", "meio", "chiado"):
        b, a = _banda(*m[k + "_banda"])
        partes.append(signal.lfilter(b, a, ruidos[k]))
    s = sum(p / _rms(p) * np.sqrt(e) for p, e in zip(partes, energia))
    if "passa_baixa" in m:   # (só a cross: tira o chiado de cima que irritava)
        b, a = signal.butter(2, m["passa_baixa"] / (SR / 2), "low")
        s = signal.lfilter(b, a, s)
    s = np.tanh(s / (np.abs(s).max() + 1e-9) * m["saturar"])
    if laco:
        n = len(s) // 3
        s = s[n:2 * n]
    return s


def _normalizar(s, pico=0.8):
    return s / (np.abs(s).max() + 1e-9) * pico


def laco(nome, giro, semente=1):
    m = MOTOS[nome]
    rng = np.random.default_rng(semente)
    n_ciclos = max(1, int(round(m["ciclo"] * giro)))   # ~1 s
    p, r = _excitacao(m, giro, n_ciclos, rng)
    return _normalizar(_filtrar(m, giro, p, r))


def pegar(nome, semente=7):
    """O motor pegando: 2 explosões falhas, a subida até 1,6 vez a marcha lenta e a queda até ela, e a marcha lenta até o
    fim (1,25 s; o laço do motor entra aos 0,69 s, Carro.LacoEntra)."""
    m = MOTOS[nome]
    rng = np.random.default_rng(semente)
    tempos, forcas = [], []
    t = 0.02 * SR
    # as duas primeiras explosões, soltas e fracas
    for a in (0.45, 0.6):
        tempos.append(t)
        forcas.append(a)
        t += 0.11 * SR
    while t < 1.25 * SR:
        s = t / SR
        # o giro: sobe até 1,6 em 0,3 s e cai para 1 até 0,65 s
        g = 1.0 + 0.6 * np.sin(np.clip((s - 0.22) / 0.45, 0, 1) * np.pi) if s < 0.67 else 1.0
        ciclo = m["ciclo"] * g
        for fase in m["explosoes"]:
            tempos.append(t + fase * SR / ciclo)
            forcas.append(1.0 + rng.normal(0, m["variacao"][0]) + (0.25 if 0.25 < s < 0.5 else 0.0))
        t += SR / ciclo
    p, r = _excitacao(m, 1.0, 0, rng, laco=False, tempos=tempos, forcas=forcas)
    s = _filtrar(m, 1.3, p, r, laco=False)[: int(1.25 * SR)]
    fim = np.ones_like(s)
    a = int(0.95 * SR)
    fim[a:] = np.linspace(1, 0, len(s) - a) ** 2
    return _normalizar(s * fim)


def buzina(f0, semente=11):
    """A buzina de disco de moto: um tom só (f0 inteiro: o laço de 1 s não tem emenda), meio quadrado e zumbido."""
    t = np.arange(SR) / SR
    s = np.zeros(SR)
    for k in range(1, 18):
        g = (1.0 / k) * (1.0 if k % 2 else 0.45)
        s += g * np.sin(2 * np.pi * k * f0 * t + 0.3 * k)
    # o zumbido do diafragma (uma batida lenta inteira em 1 s)
    s *= 1.0 + 0.08 * np.sin(2 * np.pi * 6 * t)
    b, a = signal.butter(2, [600 / (SR / 2), 4500 / (SR / 2)], "band")
    s = 0.6 * s + signal.lfilter(b, a, np.tile(s, 3))[SR:2 * SR] * 1.2
    return _normalizar(np.tanh(s * 1.5), 0.7)


def gravar(caminho, s):
    os.makedirs(os.path.dirname(caminho), exist_ok=True)
    wavfile.write(caminho, SR, (np.clip(s, -1, 1) * 32767).astype(np.int16))


def main():
    # python motos.py [pasta] [--so FatBoy|Cross]: só os sons de uma moto (a outra fica como está)
    args = [a for a in sys.argv[1:]]
    so = None
    if "--so" in args:
        i = args.index("--so")
        so = args[i + 1]
        del args[i:i + 2]
    pasta = args[0] if args else SAIDA
    for nome, giros in GIROS.items():
        if so and nome != so:
            continue
        for i, g in enumerate(giros):
            gravar(os.path.join(pasta, f"{nome}_Motor_{i}.wav"), laco(nome, g, semente=10 + i))
        gravar(os.path.join(pasta, f"{nome}_Pegar.wav"), pegar(nome))
        gravar(os.path.join(pasta, f"{nome}_Buzina.wav"), buzina(415 if nome == "FatBoy" else 470))
    print("PRONTO", os.path.abspath(pasta))


if __name__ == "__main__":
    main()
