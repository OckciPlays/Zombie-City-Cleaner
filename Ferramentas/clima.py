# Os sons do clima (a pedido em 02/11/2026: o chuvisco, a chuva, a tempestade, os raios e o tornado), gerados por código,
# como os do ambiente:
#  - as chuvas, em laço e estéreo: a fina (o chuvisco), a normal e a forte; a chuva ouvida de dentro (batendo no telhado)
#    e de dentro do carro (no teto de chapa);
#  - o vento forte da tempestade e o rugido do tornado (em laço);
#  - os trovões pela distância (a pedido: "os raios que caem longe fazem barulhos de longe ... os que caem perto, mais
#    alto"): longe (o ronco grave e comprido), no meio (o estalo abafado e o rolar) e perto (o estalo seco, o estouro e o
#    rolar com o eco das casas); o estalo elétrico do raio bem do lado e o zumbido no ouvido depois dele;
#  - as pisadas na poça (o jogo toca no passo de quem pisa).
# Refeitos em 02/11/2026 (a chuva, o tornado e os trovões), inspirados nos sons do planeta de tempestade do Missão Lázaro,
# que o Pedro mandou como referência (sem copiar: lá a chuva bate no traje; aqui, no chão em volta).
# Uso: python clima.py [pasta]   (sem pasta: Assets/_Game/Audio/Resources/Clima, carregados pelo nome)
import os
import sys
import numpy as np
import scipy.signal as sg
import scipy.io.wavfile as wf

SR = 44100
AQUI = os.path.dirname(os.path.abspath(__file__))
PASTA = sys.argv[1] if len(sys.argv) > 1 else os.path.join(AQUI, '..', 'Assets', '_Game', 'Audio', 'Resources', 'Clima')


def t_de(n):
    return np.arange(n) / SR


def passa(x, tipo, f, ordem=2):
    return sg.sosfilt(sg.butter(ordem, f, btype=tipo, fs=SR, output='sos'), x, axis=0).astype(np.float32)


def rosa(n, rng):
    """Ruído rosa (o chiado de chuva e de vento, mais natural que o branco)."""
    b = [0.049922035, -0.095993537, 0.050612699, -0.004408786]
    a = [1, -2.494956002, 2.017265875, -0.522189400]
    return (sg.lfilter(b, a, rng.standard_normal(n)) * 4).astype(np.float32)


def marrom(n, rng):
    x = np.cumsum(rng.standard_normal(n)).astype(np.float32)
    x = passa(x, 'high', 15)
    return x / (np.abs(x).max() + 1e-9)


def gravar(nome, x, pico_db=-1.0):
    os.makedirs(PASTA, exist_ok=True)
    x = np.asarray(x, np.float32)
    x = x / (np.abs(x).max() + 1e-9) * (10 ** (pico_db / 20))
    wf.write(os.path.join(PASTA, nome + '.wav'), SR, (x * 32767).astype(np.int16))
    print(nome, f'{len(x) / SR:.1f} s')


def em_laco(x, junta=2.0):
    """Fecha o laço: o fim desliza para o começo (sem estalo nem buraco ao repetir)."""
    m = int(junta * SR)
    y = x[:-m].copy()
    rampa = np.linspace(0, 1, m)[:, None] if x.ndim == 2 else np.linspace(0, 1, m)
    y[:m] = y[:m] * rampa + x[-m:] * (1 - rampa)
    return y


def gotas(n, rng, por_segundo, f, decai, nivel, canais=2):
    """Gotas soltas: estalinhos curtos (um ruído e um tom que somem rápido), espalhados no estéreo."""
    y = np.zeros((n, canais), np.float32)
    k = rng.poisson(por_segundo * n / SR)
    m = int(0.03 * SR)
    t = t_de(m)
    for _ in range(k):
        i = rng.integers(0, n - m)
        fi = f * rng.uniform(0.6, 1.6)
        g = (np.sin(2 * np.pi * fi * t) * 0.6 + rng.standard_normal(m) * 0.4) * np.exp(-t / (decai * rng.uniform(0.5, 1.5)))
        g *= nivel * rng.uniform(0.2, 1.0)
        if canais == 1:
            y[i:i + m, 0] += g
            continue
        lado = rng.uniform(0, 1)
        y[i:i + m, 0] += g * (1 - lado) * 1.4
        y[i:i + m, 1] += g * lado * 1.4
    return y


def chiado(n, rng, baixo, alto, canais=2):
    y = np.stack([rosa(n, rng) for _ in range(canais)], axis=1)
    return passa(passa(y, 'high', baixo), 'low', alto)


def banda(x, baixo, alto, ordem=2):
    return sg.sosfilt(sg.butter(ordem, [baixo, alto], btype='band', fs=SR, output='sos'), x, axis=0).astype(np.float32)


def unidade(x):
    """O mesmo volume (RMS 1) para cada camada: assim os pesos da mistura dizem quanto de cada uma entra."""
    return (x / (np.sqrt(np.mean(np.square(x, dtype=np.float64))) + 1e-9)).astype(np.float32)


def cheio(x, forca=1.6):
    """Um pouco de saturação: o som fica mais cheio (e mais alto) sem estourar."""
    x = x / (np.abs(x).max() + 1e-9)
    return (np.tanh(x * forca) / np.tanh(forca)).astype(np.float32)


def pingos(n, rng, por_segundo, baixo, alto, decai_ms, expoente=2.0):
    """Pingos soltos (a ideia do planeta de tempestade do Missão Lázaro: a chuva não é um chiado, são centenas de pingos):
    cada um um estalinho de ruído numa faixa, que some em uns milissegundos, com forças bem diferentes."""
    imp = np.zeros(n, np.float32)
    k = rng.poisson(por_segundo * n / SR)
    np.add.at(imp, rng.integers(0, n, k), (rng.random(k) ** expoente).astype(np.float32) + 0.03)
    a = np.exp(-1.0 / (decai_ms / 1000.0 * SR))
    env = sg.lfilter([1.0], [1.0, -a], imp).astype(np.float32)
    return unidade(banda(rng.standard_normal(n).astype(np.float32), baixo, alto)) * env


def plins(n, rng, por_segundo):
    """A gota caindo numa poça: o "plim" curto, que sobe de tom (a bolha que se fecha)."""
    y = np.zeros(n, np.float32)
    m = int(0.03 * SR)
    t = t_de(m)
    for _ in range(rng.poisson(por_segundo * n / SR)):
        i = rng.integers(0, n - m)
        f0 = rng.uniform(900, 2400)
        f = f0 * (1 + 0.7 * np.minimum(t / 0.012, 1))
        y[i:i + m] += np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / rng.uniform(0.006, 0.014)) * rng.uniform(0.2, 1.0)
    return y


# ============================================================================================== as chuvas
# (refeitas em 02/11/2026, a pedido: "até que estão bom ... porém o som deles não está legal"; a inspiração foi a chuva do
# planeta de tempestade do outro jogo do Pedro, que ele gostou, mas sem o traje: aqui a chuva bate no chão, nas folhas, nos
# carros e nas poças em volta, e o volume não sobe nem desce em ondas)
def chuva_de_pingos(semente, dur, por_segundo, fundo, grossos, plins_por_s):
    rng = np.random.default_rng(semente)
    n = int(dur * SR)
    lados = []
    for _ in range(2):
        y = pingos(n, rng, por_segundo * 0.55, 2600, 6500, 2.2) * 1.0       # os finos, no asfalto e nas folhas
        y += pingos(n, rng, por_segundo * 0.45, 1000, 2600, 3.8) * 0.85      # os cheios, mais perto
        if grossos > 0:
            y += pingos(n, rng, grossos, 220, 900, 9.0, 1.4) * 0.9           # as gotas grossas no chão
        # o fundo: a chuva longe, todas as gotas juntas (no chuvisco, quase nada)
        y += unidade(banda(rosa(n, rng), 1100, 8000)) * fundo
        if plins_por_s > 0:
            y += unidade(plins(n, rng, plins_por_s) + 1e-6) * 0.35
        lados.append(y)
    return em_laco(cheio(np.stack(lados, axis=1), 1.3))


def chuva_fina(semente, dur=14.0):
    """O chuvisco: poucos pingos, finos, e quase nada de fundo."""
    return chuva_de_pingos(semente, dur, 260, 0.08, 0, 0)


def chuva(semente, dur=14.0):
    """A chuva: muitos pingos, o fundo da chuva longe e uns plins nas poças."""
    return chuva_de_pingos(semente, dur, 950, 0.35, 25, 5)


def chuva_forte(semente, dur=14.0):
    """A tempestade: os pingos sem conta, o fundo forte, as gotas grossas batendo e as poças borbulhando."""
    return chuva_de_pingos(semente, dur, 2300, 0.7, 140, 16)


def chuva_telhado(semente, dur=14.0):
    """A chuva de dentro de casa: os pingos batendo no telhado (o "toc" abafado da telha e da madeira), o fundo grave e
    umas gotas na vidraça."""
    rng = np.random.default_rng(semente)
    n = int(dur * SR)
    lados = []
    for _ in range(2):
        y = pingos(n, rng, 900, 260, 1100, 11.0) * 1.0
        y += pingos(n, rng, 260, 700, 1800, 6.0) * 0.45
        y += unidade(banda(rosa(n, rng), 90, 900)) * 0.45
        y += pingos(n, rng, 14, 2200, 5000, 3.0, 1.0) * 0.25                # a vidraça
        lados.append(passa(y, 'low', 2400))
    return em_laco(cheio(np.stack(lados, axis=1), 1.3))


def chuva_carro(semente, dur=14.0):
    """A chuva de dentro do carro: os pingos no teto de chapa (cada um faz a chapa soar), o tambor grave do teto e o
    chiado do vidro."""
    rng = np.random.default_rng(semente)
    n = int(dur * SR)
    lados = []
    for _ in range(2):
        batidas = pingos(n, rng, 1100, 900, 4500, 3.0)
        chapa = np.zeros(n, np.float32)
        for f, q, g in ((1850, 9, 1.0), (3050, 11, 0.6), (620, 6, 0.5)):
            b, a = sg.iirpeak(f * rng.uniform(0.97, 1.03), q, fs=SR)
            chapa += unidade(sg.lfilter(b, a, batidas).astype(np.float32)) * g
        y = chapa * 0.8 + unidade(batidas) * 0.35
        y += unidade(banda(rosa(n, rng), 60, 400)) * 0.5                      # o teto tremendo
        y += unidade(banda(rosa(n, rng), 1500, 6000)) * 0.12                  # o vidro
        lados.append(y)
    return em_laco(cheio(np.stack(lados, axis=1), 1.4))


# ============================================================================================== o vento e o tornado
def vento_forte(semente, dur=32.0):
    """O vento da tempestade: o sopro largo, o assobio que sobe e desce nas frestas e as rajadas que batem."""
    rng = np.random.default_rng(semente)
    n = int(dur * SR)
    t = t_de(n)
    base = chiado(n, rng, 60, 1800) * 0.7
    # o assobio: um filtro estreito que passeia
    assobio = np.zeros((n, 2), np.float32)
    bloco = 2048
    for c in range(2):
        r = rosa(n, rng)
        fase = rng.uniform(0, 6.28)
        for i in range(0, n, bloco):
            f = 420 + 260 * np.sin(2 * np.pi * (i / SR) / 6.7 + fase) + 120 * np.sin(2 * np.pi * (i / SR) / 2.1 + c)
            b, a = sg.iirpeak(f, 9, fs=SR)
            assobio[i:i + bloco, c] = sg.lfilter(b, a, r[i:i + bloco])
    rajada = 0.55 + 0.45 * np.clip(np.sin(2 * np.pi * t / 7.9) * 0.7 + np.sin(2 * np.pi * t / 3.1 + 1.3) * 0.4, -1, 1)
    y = (base + assobio * 0.5) * rajada[:, None]
    return em_laco(y, 3.0)


def tornado(semente, dur=30.0):
    """O tornado (refeito em 02/11/2026, a pedido: "ao invés desse som ... parece um granulado, ele só fazer um som de vento
    forte e constante"): o vento mais forte que existe, sem parar e sem os estalos de antes. O rugido grave, o corpo do
    vento, o uivo e o assobio nas frestas, que só passeia devagar. Os trovões vêm dos raios em volta dele (CicloDoDia)."""
    rng = np.random.default_rng(semente)
    n = int(dur * SR)
    t = t_de(n)
    lados = []
    for c in range(2):
        rugido = unidade(passa(marrom(n, rng), 'low', 110))
        corpo = unidade(banda(rosa(n, rng), 110, 480))
        uivo = unidade(banda(rosa(n, rng), 480, 1200))
        r = rosa(n, rng)
        assobio = np.zeros(n, np.float32)
        bloco = 2048
        fase = rng.uniform(0, 6.28)
        for i in range(0, n, bloco):
            f = 820 + 180 * np.sin(2 * np.pi * (i / SR) / 11.0 + fase) + 60 * np.sin(2 * np.pi * (i / SR) / 4.7 + c)
            b, a = sg.iirpeak(f, 7, fs=SR)
            assobio[i:i + bloco] = sg.lfilter(b, a, r[i:i + bloco])
        # quase constante: só um balanço lento de ±1 dB
        lento = 1 + 0.08 * np.sin(2 * np.pi * t / 9.7 + c) + 0.05 * np.sin(2 * np.pi * t / 4.3 + 1.7 * c)
        lados.append((rugido * 1.0 + corpo * 0.9 + uivo * 0.42 + unidade(assobio) * 0.14) * lento)
    return em_laco(cheio(np.stack(lados, axis=1), 1.2), 3.0)


# ============================================================================================== os trovões
# (refeitos em 02/11/2026, a pedido: "falta um pouco mais de tremor no raio, mais de potência". O de antes tinha quase tudo
# abaixo de 80 Hz, um grave que a caixa de som pequena e o fone não tocam. A inspiração foi o trovão do planeta de
# tempestade do outro jogo do Pedro: o grave do estrondo e o ronco nos médios, que rola em trancos que ninguém prevê.)
def trovao(semente, tipo):
    """'longe' (o ronco que chega devagar e rola muito), 'medio' (o estalo abafado e o rolar) ou 'perto' (o estalo seco em
    três rasgos, o estouro grave na hora e o rolar com o eco das casas). O grave treme rápido (o tremor, de 8 a 13 Hz)."""
    rng = np.random.default_rng(semente)
    dur = {'longe': rng.uniform(7.5, 9.5), 'medio': rng.uniform(6.5, 8.0), 'perto': rng.uniform(5.5, 7.0)}[tipo]
    n = int(dur * SR)
    t = t_de(n)
    branco = rng.standard_normal(n).astype(np.float32)
    # o grave do estrondo, de 25 a 230 Hz (só abaixo de 80 Hz, a caixa de som pequena e o fone não tocam)
    grave = unidade(passa(passa(marrom(n, rng) * 0.35 + rng.standard_normal(n).astype(np.float32) * 0.65, 'low', 230, 4), 'high', 25))
    ronco = unidade(banda(rng.standard_normal(n).astype(np.float32), 160, 520))
    medio = unidade(banda(rng.standard_normal(n).astype(np.float32), 500, 1500))
    rasgo = unidade(banda(branco, 1800, 5200))
    # os trancos: o trovão rola em golpes que ninguém prevê, mais juntos perto, mais espalhados longe
    trancos = np.zeros(n, np.float32)
    golpes = int({'longe': rng.integers(4, 7), 'medio': rng.integers(5, 8), 'perto': rng.integers(5, 9)}[tipo])
    janela = {'longe': 4.0, 'medio': 3.2, 'perto': 2.6}[tipo]
    for k in range(golpes):
        t0 = 0.0 if k == 0 else rng.uniform(0.15, janela)
        forca = 1.0 if k == 0 else rng.uniform(0.35, 0.95)
        subida = rng.uniform(0.25, 0.6) if tipo == 'longe' else rng.uniform(0.03, 0.2)
        queda = rng.uniform(0.5, 1.6)
        d = t - t0
        trancos += np.where(d < 0, 0, np.minimum(np.maximum(d, 0) / subida, 1) * np.exp(-np.maximum(d, 0) / queda)) * forca
    # o rolar contínuo por baixo: sobe e cai devagar, num balanço irregular
    ataque, queda = {'longe': (2.2, 0.38), 'medio': (8.0, 0.5), 'perto': (40.0, 0.62)}[tipo]
    base = (1 - np.exp(-t * ataque)) * np.exp(-t * queda)
    balanco = 0.6 + 0.4 * np.abs(np.sin(t * 5.3 + np.sin(t * 2.1) * 2.0))
    env = base * balanco * 0.8 + trancos * np.exp(-t * 0.22) * 0.75
    # o tremor: o grave treme rápido e irregular (o que se sente no peito)
    ftremor = rng.uniform(8.0, 13.0) * (1 + 0.25 * np.sin(2 * np.pi * 0.7 * t + rng.uniform(0, 6)))
    tremor = 1 + 0.5 * np.sin(2 * np.pi * np.cumsum(ftremor) / SR) * (0.6 + 0.4 * np.sin(2 * np.pi * 0.31 * t))
    g, r, m, rs = {'longe': (1.0, 1.05, 0.55, 0.0), 'medio': (1.0, 1.0, 0.65, 0.12), 'perto': (1.05, 0.9, 0.7, 0.25)}[tipo]
    y = (grave * g * tremor + ronco * r * (0.8 + 0.2 * tremor) + medio * m) * env
    y += rasgo * rs * env * np.exp(-t * 1.5)
    if tipo == 'medio':
        # o estalo abafado do começo (a distância comeu o agudo)
        y += unidade(banda(branco, 350, 2200)) * np.exp(-t / 0.12) * 1.1
    if tipo == 'perto':
        # o estalo: três rasgos curtos, um em cima do outro, e o estouro grave junto
        for t0, f in ((0.0, 1.0), (rng.uniform(0.04, 0.08), 0.8), (rng.uniform(0.12, 0.2), 0.6)):
            d = t - t0
            y += rasgo * np.where(d < 0, 0, np.exp(-np.maximum(d, 0) * 26)) * 5.0 * f
            y += branco * np.where(d < 0, 0, np.exp(-np.maximum(d, 0) * 70)) * 1.6 * f
        festouro = 34 + 60 * np.exp(-t / 0.25)
        y += np.sin(2 * np.pi * np.cumsum(festouro) / SR) * np.exp(-t / 0.6) * np.minimum(t / 0.004, 1) * 2.2
        # o eco das casas: dois retornos mais escuros
        for atraso, ganho in ((0.19, 0.32), (0.44, 0.2)):
            d = int(atraso * SR)
            y[d:] += passa(y[:-d].copy(), 'low', 1800) * ganho
    if tipo == 'longe':
        y = passa(y, 'low', 2400)   # o agudo some no caminho
    return cheio(y, 1.8)


def trovao_longe(semente):
    return trovao(semente, 'longe')


def trovao_medio(semente):
    return trovao(semente, 'medio')


def trovao_perto(semente):
    return trovao(semente, 'perto')


def estalo(semente):
    """O raio bem do lado: o estalo elétrico (o chiado que rasga o ar) antes do estouro."""
    rng = np.random.default_rng(semente)
    n = int(1.2 * SR)
    t = t_de(n)
    zumbe = np.sin(2 * np.pi * 120 * t) * 0.2 + np.sin(2 * np.pi * 240 * t) * 0.1
    chiado_ = passa(rng.standard_normal(n).astype(np.float32), 'high', 2500)
    picos = (rng.random(n) < 0.05) * rng.standard_normal(n) * 2
    e = np.exp(-t / 0.18)
    return (chiado_ * 0.8 + picos + zumbe) * e


def zumbido():
    """O zumbido no ouvido depois do raio bem perto: um apito agudo que some devagar."""
    n = int(5.0 * SR)
    t = t_de(n)
    f = 3600 + 30 * np.sin(2 * np.pi * 0.4 * t)
    y = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.minimum(t / 0.05, 1) * np.exp(-t / 1.6)
    return y.astype(np.float32)


def poca(semente):
    """A pisada na poça: o chape da água (um ruído médio que espirra) e o "plop" grave da sola."""
    rng = np.random.default_rng(semente)
    n = int(0.35 * SR)
    t = t_de(n)
    chape = passa(passa(rng.standard_normal(n).astype(np.float32), 'high', 700), 'low', 5000) * np.exp(-t / rng.uniform(0.05, 0.09))
    respingos = gotas(n, rng, 60, 2800, 0.004, 0.6, 1)[:, 0]
    plop = np.sin(2 * np.pi * (180 + 120 * np.exp(-t / 0.02)) * t) * np.exp(-t / 0.04) * 0.6
    return chape + respingos * 0.5 + plop


if __name__ == '__main__':
    gravar('chuva_fina', chuva_fina(11), -6)
    gravar('chuva', chuva(12), -3)
    gravar('chuva_forte', chuva_forte(13), -1)
    gravar('chuva_telhado', chuva_telhado(14), -3)
    gravar('chuva_carro', chuva_carro(15), -3)
    gravar('vento_forte', vento_forte(16), -2)
    gravar('tornado', tornado(17), -1)
    for i in range(4):
        gravar(f'trovao_longe_{i + 1}', trovao_longe(100 + i), -3)
    for i in range(3):
        gravar(f'trovao_medio_{i + 1}', trovao_medio(200 + i), -2)
    for i in range(3):
        gravar(f'trovao_perto_{i + 1}', trovao_perto(300 + i), -0.5)
    for i in range(2):
        gravar(f'estalo_{i + 1}', estalo(400 + i), -2)
    gravar('zumbido', zumbido(), -14)
    for i in range(5):
        gravar(f'poca_{i + 1}', poca(500 + i), -3)
