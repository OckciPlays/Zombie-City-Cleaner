# O maestro da trilha, simulado (as mesmas regras que vão para o jogo, em C#), para ouvir uma sessão inteira antes de
# pôr no jogo. Um roteiro diz o que acontece (dia ou noite, o perigo em volta, quem virou pedra, o amanhecer, a morte);
# o maestro decide o que toca, e a mixagem soma os blocos como o jogo vai somar (cada bloco começa no fim do anterior,
# as camadas do perigo sobem e descem de volume, o vento sobe no silêncio).
# Uso: python maestro.py [pasta dos blocos] [sessão: dia|noite]
import os
import sys
import json
import math
import subprocess
import numpy as np
import scipy.io.wavfile as wf

SR = 44100
PASTA = sys.argv[1] if len(sys.argv) > 1 else 'blocos'
MAN = json.load(open(os.path.join(PASTA, 'manifesto.json'), encoding='utf-8'))
DT = 0.05

# as regras (os números que vão virar ajustes no jogo)
REGRAS = dict(
    trecho_neblina=(4, 8),         # blocos no meio de um trecho de música (fora a entrada e a saída)
    trecho_noite=(3, 6),
    silencio_depois_do_trecho=(40, 65),   # no jogo, mais longo (1 a 3 min); aqui menor, para caber na sessão
    silencio_depois_do_perigo=(20, 35),
    desce_um_nivel=4.0,            # o nível de perigo sobe na hora e desce um degrau a cada 4 s sem motivo
    entrada_de_novo=40.0,          # o "te viram" só toca se o último perigo acabou há mais de 40 s
    pedra_conta=15.0,              # a saída pela pedra, se algum caçador virou pedra nos últimos 15 s
    vento_na_musica=0.35,          # o vento por baixo da música (no silêncio, 1)
    camadas=(1.0, 0.9, 0.85),      # o volume de cada camada do perigo
)

_cache = {}


def ler(arq):
    if arq not in _cache:
        sr, x = wf.read(os.path.join(PASTA, arq))
        _cache[arq] = (x.astype(np.float32) / 32767.0).T.copy()
    return _cache[arq]


class Fonte:
    def __init__(self, arq, inicio, ganho, grupo, camada=0):
        self.arq, self.inicio, self.grupo, self.camada = arq, inicio, grupo, camada
        self.dados = ler(arq)
        self.fim = inicio + self.dados.shape[1] / SR
        self.ganho, self.alvo, self.vel = ganho, ganho, 0.0
        self.pontos = [(inicio, ganho)]
        self.agenda = []   # (quando, alvo, segundos)

    def rampa(self, alvo, segundos, quando=None):
        if quando is not None:
            self.agenda.append((quando, alvo, segundos))
            return
        if abs(alvo - self.alvo) > 1e-4:
            self.alvo = alvo
            self.vel = abs(alvo - self.ganho) / max(segundos, 1e-3)

    def passo(self, agora):
        for item in list(self.agenda):
            if agora >= item[0]:
                self.agenda.remove(item)
                self.rampa(item[1], item[2])
        if self.ganho != self.alvo:
            k = self.vel * DT
            self.ganho = self.alvo if abs(self.alvo - self.ganho) <= k else self.ganho + math.copysign(k, self.alvo - self.ganho)
        self.pontos.append((agora, self.ganho))


class Maestro:
    def __init__(self, roteiro, semente):
        self.r = np.random.default_rng(semente)
        self.roteiro = roteiro
        self.fontes, self.log = [], []
        self.estado, self.proxima_musica = 'silencio', roteiro.get('primeira_musica', 5.0)
        self.nivel, self.descendo_desde = 0, None
        self.fim_perigo, self.teve_entrada = -999.0, False
        self.recentes = {}
        self.vento_prox, self.musica_ate = 0.0, 0.0
        self.amanhecer_pendente, self.morto = False, False
        self.fila, self.prox_bloco, self.familia = [], 0.0, None
        self.camada2_depois = 0.0

    def anotar(self, t, texto):
        self.log.append((t, texto))

    def tocar(self, arq, t, ganho=1.0, grupo='musica', camada=0):
        f = Fonte(arq, t, ganho, grupo, camada)
        self.fontes.append(f)
        if grupo != 'vento':
            self.musica_ate = max(self.musica_ate, f.fim - 4.0)
        return f

    def escolher(self, familia, lista, chave):
        """Um bloco ao acaso, sem repetir os últimos que tocaram."""
        rec = self.recentes.setdefault(familia + chave, [])
        livres = [b for b in lista if b['nome'] not in rec] or lista
        b = livres[int(self.r.integers(0, len(livres)))]
        rec.append(b['nome'])
        del rec[:-max(1, min(10, len(lista) - 2))]
        return b

    # ------------------------------------------------------------------ a exploração (a neblina, a noite)
    def comecar_trecho(self, t, dia):
        self.familia = 'neblina' if dia else 'noite'
        a, b = REGRAS['trecho_' + self.familia]
        n = int(self.r.integers(a, b + 1))
        seguinte = {'neblina': {'entrada': ['piano', 'piano', 'vazio'], 'piano': ['piano', 'cordas', 'cello', 'cordas', 'vazio'],
                                'vazio': ['piano', 'cello', 'cordas'], 'cordas': ['cello', 'piano', 'vazio', 'piano'],
                                'cello': ['piano', 'vazio', 'cordas', 'piano']},
                    'noite': {'entrada': ['cello', 'coro'], 'cello': ['coro', 'coracao', 'vidro'], 'coro': ['cello', 'coracao', 'vidro'],
                              'coracao': ['cello', 'vidro', 'coro'], 'vidro': ['cello', 'coro']}}[self.familia]
        tipos, ultimo = [], 'entrada'
        for _ in range(n):
            op = [x for x in seguinte[ultimo] if not (tipos[-2:] == [x, x]) and not (x == 'vazio' and 'vazio' in tipos[-2:])]
            ultimo = op[int(self.r.integers(0, len(op)))]
            tipos.append(ultimo)
        self.fila = ['entrada'] + tipos + ['saida']
        self.prox_bloco, self.estado = t, 'exploracao'
        dur = len(self.fila) * MAN[self.familia]['comprimento']
        self.anotar(t, f'começa um trecho da {"Neblina" if dia else "Noite"} ({len(self.fila)} blocos, {dur / 60:.1f} min): {" > ".join(self.fila)}')

    def seguir_trecho(self, t):
        if t < self.prox_bloco - 0.3:
            return
        if not self.fila:
            a, b = REGRAS['silencio_depois_do_trecho']
            self.estado = 'silencio'
            self.proxima_musica = self.prox_bloco + 5 + float(self.r.uniform(a, b))
            self.anotar(self.prox_bloco, f'o trecho acabou: só o vento até {mmss(self.proxima_musica)}')
            return
        tipo = self.fila.pop(0)
        lista = [b for b in MAN[self.familia]['blocos'] if b['tipo'] == tipo]
        b = self.escolher(self.familia, lista, tipo)
        self.tocar(b['arquivo'], self.prox_bloco, grupo='exploracao')
        self.prox_bloco += MAN[self.familia]['comprimento']

    def apagar(self, t, grupos, segundos):
        for f in self.fontes:
            if f.grupo in grupos and f.fim > t:
                if f.inicio > t + 0.05:
                    f.fim = f.inicio   # agendado e ainda não começou: não toca
                    f.pontos = [(f.inicio, 0.0)]
                    f.ganho = f.alvo = 0.0
                else:
                    f.rampa(0.0, segundos)

    # ------------------------------------------------------------------ o perigo
    def comecar_perigo(self, t, com_entrada):
        self.apagar(t, ('exploracao', 'momento'), 2.0)
        self.fila = []
        P = MAN['perigo']
        if com_entrada:
            st = self.escolher('perigo', [m for m in P['momentos'] if m['tipo'] == 'entrada'], 'entrada')
            self.tocar(st['arquivo'], t, grupo='momento')
            t0 = t + st['encaixe']
            self.teve_entrada = True
            self.anotar(t, f'TE VIRAM: o golpe da entrada, e o perigo começa no golpe (nível {self.nivel})')
        else:
            t0 = t + 0.1
            self.teve_entrada = self.nivel >= 2   # já te viram sem o golpe (o último perigo foi há pouco): não toca depois
            self.anotar(t, f'o perigo começa sem o golpe (nível {self.nivel}' + (', o último perigo foi há pouco)' if self.nivel >= 2 else ', só a tensão)'))
        self.perigo_t0, self.prox_bloco, self.plano_ant = t0, t0, None
        self.estado = 'perigo'
        self.entrar_devagar = not com_entrada
        self.camada2_depois = 0.0

    def seguir_perigo(self, t):
        P = MAN['perigo']
        if t >= self.prox_bloco - 0.3:
            plano = 'A' if self.plano_ant in (None, 'B', 'C') else ('B' if self.r.random() < 0.5 else 'C')
            b = self.escolher('perigo', [x for x in P['blocos'] if x['plano'] == plano], plano)
            for c, arq in enumerate(b['camadas'], 1):
                ligado = self.nivel >= c and not (c == 2 and self.prox_bloco < self.camada2_depois)
                g0 = REGRAS['camadas'][c - 1] if ligado else 0.0
                f = self.tocar(arq, self.prox_bloco, 0.0 if self.entrar_devagar else g0, 'perigo', c)
                if self.entrar_devagar and ligado:
                    f.rampa(g0, 3.0 if c == 1 else 1.5)   # sem o golpe, o perigo chega devagar
            self.entrar_devagar = False
            self.plano_ant = plano
            self.prox_bloco += P['comprimento']
        # o volume de cada camada segue o nível (sobe rápido, desce devagar)
        for f in self.fontes:
            if f.grupo == 'perigo' and f.fim > t and f.inicio <= t + 0.5 and not f.agenda:
                ligado = self.nivel >= f.camada and not (f.camada == 2 and t < self.camada2_depois)
                alvo = REGRAS['camadas'][f.camada - 1] if ligado else 0.0
                f.rampa(alvo, 0.6 if alvo > f.ganho else 3.0)   # o mesmo alvo não mexe na rampa que já corre

    def entrada_no_meio(self, t):
        """Já estava na tensão e agora te viram: o golpe cai num tempo do perigo que já toca."""
        P = MAN['perigo']
        st = self.escolher('perigo', [m for m in P['momentos'] if m['tipo'] == 'entrada'], 'entrada')
        tempo = 60.0 / P['bpm']
        k = math.ceil((t + st['encaixe'] - self.perigo_t0) / tempo)
        golpe = self.perigo_t0 + k * tempo
        self.tocar(st['arquivo'], golpe - st['encaixe'], grupo='momento')
        self.camada2_depois = golpe
        self.teve_entrada = True
        self.anotar(t, f'TE VIRAM (já na tensão): o golpe cai no tempo do perigo, em {mmss(golpe)}')

    def terminar_perigo(self, t, pedra):
        P = MAN['perigo']
        compasso = 4 * 60.0 / P['bpm']
        b = self.perigo_t0 + math.ceil((t - self.perigo_t0) / compasso + 0.01) * compasso
        for f in self.fontes:
            if f.grupo == 'perigo' and f.fim > b:
                if f.inicio >= b - 0.01:
                    f.fim = f.inicio
                    f.pontos = [(f.inicio, 0.0)]
                    f.ganho = f.alvo = 0.0
                else:
                    f.rampa(0.0, 1.2, quando=b)
        if self.amanhecer_pendente:
            arq, nome = MAN['momentos']['amanhecer'], 'o AMANHECER no lugar da saída'
            self.amanhecer_pendente = False
        elif pedra:
            arq = self.escolher('perigo', [m for m in P['momentos'] if m['tipo'] == 'pedra'], 'pedra')['arquivo']
            nome = 'a saída pela PEDRA (quem te caçava virou estátua)'
        else:
            arq = self.escolher('perigo', [m for m in P['momentos'] if m['tipo'] == 'saida'], 'saida')['arquivo']
            nome = 'a saída do perigo'
        f = self.tocar(arq, b, grupo='momento')
        a, c = REGRAS['silencio_depois_do_perigo']
        self.estado = 'silencio'
        self.fim_perigo = b
        self.teve_entrada = False
        self.proxima_musica = f.fim + float(self.r.uniform(a, c))
        self.anotar(t, f'o perigo acabou: {nome}, no compasso ({mmss(b)}); depois só o vento até {mmss(self.proxima_musica)}')

    # ------------------------------------------------------------------ o quadro
    def nivel_do_jogo(self, t):
        bruto = 0
        for a, b, n in self.roteiro['perigo']:
            if a <= t < b:
                bruto = max(bruto, n)
        if bruto > self.nivel:
            self.nivel, self.descendo_desde = bruto, None
        elif bruto < self.nivel:
            if self.descendo_desde is None:
                self.descendo_desde = t
            elif t - self.descendo_desde >= REGRAS['desce_um_nivel']:
                self.nivel -= 1
                self.descendo_desde = t
        else:
            self.descendo_desde = None
        return bruto

    def quadro(self, t):
        dia = not any(a <= t < b for a, b in self.roteiro.get('noite', []))
        antes = self.nivel
        self.nivel_do_jogo(t)
        if self.nivel != antes and not self.morto:
            self.anotar(t, f'nível do perigo: {antes} -> {self.nivel}')
        # o vento: sempre, mais alto no silêncio
        if t >= self.vento_prox - 0.2:
            fam = 'vento_dia' if dia else 'vento_noite'
            b = self.escolher(fam, MAN[fam]['blocos'], '')
            inicio = max(self.vento_prox, t)
            self.tocar(b['arquivo'], inicio, 1.0 if self.estado == 'silencio' else REGRAS['vento_na_musica'], 'vento')
            self.vento_prox = inicio + b['duracao'] - MAN[fam]['sobreposicao']
        vivas = [f for f in self.fontes if f.inicio - DT <= t <= f.fim + DT]
        tocando = any(f.grupo != 'vento' and (f.ganho > 0.02 or f.alvo > 0.02) and t < f.fim - 5.0 for f in vivas)
        alvo_vento = REGRAS['vento_na_musica'] if tocando or self.estado in ('exploracao', 'perigo') else 1.0
        for f in vivas:
            if f.grupo == 'vento':
                f.rampa(alvo_vento, 4.0)
            f.passo(t)
        if self.morto:
            return
        # a morte corta tudo
        if any(abs(t - m) < DT / 2 for m in self.roteiro.get('morte', [])):
            self.apagar(t, ('exploracao', 'perigo', 'momento'), 0.4)
            self.tocar(MAN['momentos']['morte'], t + 0.1, grupo='momento')
            self.morto, self.estado = True, 'fim'
            self.anotar(t, 'MORTE: tudo some e toca a morte; depois só o vento')
            return
        if any(abs(t - m) < DT / 2 for m in self.roteiro.get('amanhecer', [])):
            if self.estado == 'perigo':
                self.amanhecer_pendente = True
                if self.nivel >= 2:
                    self.anotar(t, 'amanheceu no meio do perigo: o amanhecer espera o perigo acabar')
                else:
                    self.terminar_perigo(t, False)
            else:
                self.apagar(t, ('exploracao',), 3.0)
                f = self.tocar(MAN['momentos']['amanhecer'], t, grupo='momento')
                self.estado, self.fila = 'silencio', []
                self.proxima_musica = f.fim + float(self.r.uniform(*REGRAS['silencio_depois_do_trecho']))
                self.anotar(t, f'AMANHECER: a noite some e toca o amanhecer; depois só o vento até {mmss(self.proxima_musica)}')
        if self.estado in ('silencio', 'exploracao'):
            if self.nivel >= 1:
                pode = t - self.fim_perigo > REGRAS['entrada_de_novo']
                self.comecar_perigo(t, com_entrada=self.nivel >= 2 and pode)
            elif self.estado == 'silencio' and t >= self.proxima_musica:
                self.comecar_trecho(t, dia)
            elif self.estado == 'exploracao':
                self.seguir_trecho(t)
        if self.estado == 'perigo':
            if self.nivel >= 2 and not self.teve_entrada and t - self.fim_perigo > REGRAS['entrada_de_novo']:
                self.entrada_no_meio(t)
            if self.nivel == 0:
                pedra = any(t - REGRAS['pedra_conta'] <= p <= t for p in self.roteiro.get('pedra', []))
                self.terminar_perigo(t, pedra)
            else:
                self.seguir_perigo(t)


def mmss(t):
    return f'{int(t // 60)}:{int(t % 60):02d}'


def mixar(m, duracao, nome):
    n = int((duracao + 10) * SR)
    e, d = np.zeros(n, np.float32), np.zeros(n, np.float32)
    usados = {}
    for f in m.fontes:
        if f.fim <= f.inicio:
            continue
        i0 = int(round(f.inicio * SR))
        if i0 >= n:
            continue
        x = f.dados[:, :min(f.dados.shape[1], n - i0)]
        k = x.shape[1]
        pt, pg = zip(*f.pontos)
        g = np.interp(f.inicio + np.arange(k) / SR, pt, pg).astype(np.float32)
        if f.fim < f.inicio + f.dados.shape[1] / SR - 0.01:
            k = min(k, int((f.fim - f.inicio) * SR))
        e[i0:i0 + k] += x[0, :k] * g[:k]
        d[i0:i0 + k] += x[1, :k] * g[:k]
        if f.grupo != 'vento':
            usados[f.arq] = usados.get(f.arq, 0) + 1
    e, d = e[:int(duracao * SR)], d[:int(duracao * SR)]
    pico = 20 * np.log10(max(np.abs(e).max(), np.abs(d).max()))
    env_ = np.maximum(np.abs(e), np.abs(d))
    por_s = env_[:len(env_) // SR * SR].reshape(-1, SR).max(axis=1)
    altos = [(i, 20 * np.log10(v)) for i, v in enumerate(por_s) if v > 0.7]
    if altos:
        print('acima do joelho do limitador (-3 dB) em:', ', '.join(f'{mmss(i)} ({v:.1f})' for i, v in altos[:20]), f'... {len(altos)} s ao todo')
    # o limitador do grupo da música no jogo (o mixer do Unity vai ter um)
    def limitar(x, joelho=0.7, teto=0.95):
        a = np.abs(x)
        y = x.copy()
        acima = a > joelho
        y[acima] = np.sign(x[acima]) * (joelho + (teto - joelho) * np.tanh((a[acima] - joelho) / (teto - joelho)))
        return y
    e, d = limitar(e), limitar(d)
    mf = int(3 * SR)
    e[-mf:] *= np.linspace(1, 0, mf)
    d[-mf:] *= np.linspace(1, 0, mf)
    os.makedirs('saida', exist_ok=True)
    wav = os.path.join('saida', nome + '.wav')
    wf.write(wav, SR, (np.stack([e, d], axis=1) * 32767).astype(np.int16))
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', wav, '-b:a', '224k', wav[:-4] + '.mp3'], check=True)
    repetidos = sum(1 for v in usados.values() if v > 1)
    print(f'{nome}: {duracao / 60:.1f} min, pico antes do limitador {pico:.1f} dB, {len(usados)} arquivos de música diferentes, '
          f'{repetidos} tocaram mais de uma vez')
    return wav


ROTEIROS = {
    # o dia: dois trechos da neblina com silêncio entre eles; um zumbi investiga, te vê, chega a horda, o sol os
    # petrifica; pouco depois outro te vê (sem o golpe de novo); a neblina volta
    'dia': dict(duracao=600, semente=5, primeira_musica=6.0,
                perigo=[(232, 244, 1), (244, 262, 2), (262, 292, 3), (292, 300, 1), (338, 350, 2)],
                pedra=[(296), (299)]),
    # a noite: um trecho da noite; te veem de repente e você escapa; outro trecho; amanhece; a neblina; a horda; a morte
    'noite': dict(duracao=600, semente=9, primeira_musica=6.0, noite=[(0, 395)],
                  perigo=[(168, 205, 2), (205, 212, 1), (505, 520, 2), (520, 560, 3)],
                  amanhecer=[395.0], morte=[561.0]),
}

if __name__ == '__main__':
    qual = sys.argv[2] if len(sys.argv) > 2 else 'dia'
    rot = ROTEIROS[qual]
    m = Maestro(rot, rot['semente'])
    t = 0.0
    while t < rot['duracao']:
        m.quadro(round(t, 4))
        t += DT
    nome = f'ZCC_Sessao_{qual}'
    mixar(m, rot['duracao'], nome)
    with open(os.path.join('saida', nome + '_roteiro.txt'), 'w', encoding='utf-8') as arq:
        for t, texto in m.log:
            arq.write(f'{mmss(t)}  {texto}\n')
    for t, texto in m.log:
        print(f'{mmss(t)}  {texto}')
