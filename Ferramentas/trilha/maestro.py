# O maestro da trilha, simulado (as mesmas regras que vão para o jogo, em C#), para ouvir uma sessão inteira antes de
# pôr no jogo. Um roteiro diz o que acontece (dia ou noite, o perigo em volta, quem virou pedra, o amanhecer, a morte);
# o maestro decide o que toca, e a mixagem soma os blocos como o jogo vai somar (cada bloco começa no fim do anterior,
# as camadas do perigo sobem e descem de volume, o vento sobe no silêncio).
# Uso: python maestro.py [pasta dos blocos] [roteiro: dia|noite|zumbis|cerco] [regras: novas|velhas]
#   A família do perigo vem de FAMILIA_DO_PERIGO (o padrão é a 'perigo', a de hoje), para ouvir uma família nova antes
#   de trocá-la no jogo. No Git Bash: FAMILIA_DO_PERIGO=lamento python maestro.py blocos zumbis novas
#   (no PowerShell: $env:FAMILIA_DO_PERIGO='lamento'). Sai em saida/ZCC_Sessao_<roteiro>_<regras>.wav/.mp3/_roteiro.txt.
#
# As regras novas (04/11/2026, a pedido): os amigos do Pedro acharam a música de luta "muito cansativa, pois toda hora
# a música entra". Agora a música do perigo só entra quando é com VOCÊ, e é rara de propósito:
#   - só conta quem caça você (ou quem briga a até 8 m); a tensão precisa de 2 investigando a até 12 m (ou 1 colado);
#   - a caça precisa durar (2 s; 1 s se o "te viram" está liberado; 6 s na recarga; a horda, 0,5 s);
#   - no máximo 3 entradas a cada 5 min (a horda sempre fura), e 45 s de recarga depois de cada perigo;
#   - a tensão sozinha nunca corta a exploração; no silêncio, ela só abre a SOMBRA (o coro baixinho), e raramente;
#   - o "te viram" fica raro (no máximo 1 a cada 2 min) e só cai em três momentos com sentido;
#   - o perigo acaba depois de 6 s sem ninguém (a graça), ou de 20 s só com investigadores; a saída só toca depois de
#     uma caça de verdade, e senão as camadas só apagam (o fim mudo).
# As regras de hoje ficam no conjunto 'velhas', iguais ao que era, para comparar (a regressão).
# O MaestroDaTrilha.cs segue estas regras: as mesmas decisões, com os mesmos números.
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
INF = float('inf')
EPS = 1e-6   # folga nas contas de tempo (os tempos aqui são múltiplos de 0,05 s, com erro de arredondamento)

# a família do perigo simulada (04/11/2026): a de hoje ('perigo', 88 bpm) ou uma nova (a 'lamento', 66 bpm). A barra
# e o tempo saem do bpm dela, como no jogo (o TrilhaDoJogo.bpmDoPerigo)
FAMILIA = os.environ.get('FAMILIA_DO_PERIGO', 'perigo')
if FAMILIA not in MAN or 'momentos' not in MAN[FAMILIA]:
    sys.exit(f'a família do perigo "{FAMILIA}" não está no {os.path.join(PASTA, "manifesto.json")}')
PERIGO = MAN[FAMILIA]
COMPASSO = 4 * 60.0 / PERIGO['bpm']   # a barra (4 tempos)
TEMPO = 60.0 / PERIGO['bpm']

# as regras (os números que vão virar ajustes no jogo); estas são as de hoje
REGRAS = dict(
    trecho_neblina=(4, 8),         # blocos no meio de um trecho de música (fora a entrada e a saída)
    trecho_noite=(3, 6),
    silencio_depois_do_trecho=(40, 65),   # no jogo, mais longo (1 a 3 min); aqui menor, para caber na sessão
    silencio_depois_do_perigo=(20, 35),
    desce_um_nivel=4.0,            # o nível de perigo sobe na hora e desce um degrau a cada 4 s sem motivo
    desce_da_horda=4.0,            # o degrau do 3 para o 2 (nas regras de hoje, o mesmo dos outros)
    entrada_de_novo=40.0,          # o "te viram" só toca se o último perigo acabou há mais de 40 s
    pedra_conta=15.0,              # a saída pela pedra, se algum caçador virou pedra nos últimos 15 s
    vento_na_musica=0.35,          # o vento por baixo da música (no silêncio, 1)
    camadas=(1.0, 0.9, 0.85),      # o volume de cada camada do perigo
)

# o nível que cada tipo de ameaça do roteiro dá (04/11/2026). Nas regras de hoje, um barulho põe um zumbi investigando
# perto (1), e a briga do amigo, a lanterna do cliente (a falsa caça do Esquivando nas cópias) e a caça dão 2; 4 caçando
# já é a horda. Nas novas, um barulho só (um zumbi indo ver) e a lanterna não dão nada; 2 investigando dão a tensão;
# o amigo brigando a 8-15 m também é só a tensão; a horda precisa de 5 caçando (ou 3 atacando)
VELHO = {'barulho': 1, 'barulho2': 1, 'vizinho': 2, 'lanterna': 2, 'caca': 2, 'caca4': 3, 'horda': 3}
NOVO = {'barulho': 0, 'barulho2': 1, 'vizinho': 1, 'lanterna': 0, 'caca': 2, 'caca4': 2, 'horda': 3}
# a distância (m) do caçador teu mais perto em cada tipo, para o "te viram" das regras novas, que precisa de um caçador
# a até 15 m (o golpePerto). None: nenhum caçador teu (o barulho e a lanterna são só zumbis andando; o vizinho é o amigo
# brigando a 8-15 m, com zumbis que caçam ele). Um item do roteiro pode trazer a distância dele em 4º lugar
# (a, b, 'caca', 25): um caçador a 25 m (até os 30 m do raioDaCaca) conta, mas não dá o golpe
PERTO = {'barulho': None, 'barulho2': None, 'vizinho': None, 'lanterna': None, 'caca': 10.0, 'caca4': 8.0, 'horda': 5.0}
PERTO_DO_NIVEL = {1: None, 2: 10.0, 3: 5.0}   # os roteiros antigos, que dão o nível direto: (a, b, nível)

REGRAS_VELHAS = dict(REGRAS, nome='velhas', mapa=VELHO)
REGRAS_NOVAS = dict(
    REGRAS, nome='novas', mapa=NOVO,
    olhar_a_cada=0.25,             # o jogo olha os zumbis a cada 0,25 s (as regras velhas daqui olhavam a cada quadro)
    entrada_de_novo=90.0,          # o "te viram" só se o último perigo acabou há 90 s (a horda não precisa disso)
    pedra_conta=25.0,              # a pedra conta por 25 s: a graça alonga o fim
    desce_da_horda=6.0,            # do 3 para o 2 em 6 s, para a horda não ficar subindo e descendo
    # quanto tempo a caça precisa durar para a música entrar (s). Um buraco de até 0,5 s (2 olhadas) não zera a conta
    confirmar=dict(caca=2.0, golpe=1.0, horda=0.5, recarga=6.0), furo=0.5,
    orcamento=(3, 300.0),          # no máximo 3 entradas em 300 s (a horda fura)
    recarga=45.0,                  # depois de cada perigo, 45 s em que a caça precisa de 6 s e a sombra não abre
    musica_depois_da_caca=30.0,    # a caça que espera empurra a exploração para depois dela
    graca=6.0,                     # o perigo acaba depois de 6 s no nível 0 (duas caças perto viram um perigo só)
    teto_sem_caca=20.0,            # ou depois de 20 s seguidos sem ninguém te caçando (só investigadores em volta)
    golpe_a_cada=120.0,            # o "te viram": no máximo 1 a cada 2 min
    golpe_perto=15.0,              # e só com um caçador teu a até 15 m
    saida_depois_de=12.0,          # a saída só toca se a caça (o nível 2 ou mais) durou 12 s
    saida_a_cada=45.0,             # e se a última saída tocou há 45 s; senão, o fim mudo
    fim_mudo=4.0,                  # o fim mudo: as camadas apagam em 4 s na barra, sem clipe
    # a sombra: no silêncio, a tensão por 8 s abre só a c1 (o coro), a 75%, entrando em 4 s, por até 2 blocos, no
    # máximo 1 a cada 3 min; depois dela, 10 s até a exploração poder voltar
    sombra=dict(confirmar=8.0, a_cada=180.0, blocos=2, volume=0.75, entra=4.0, silencio_depois=10.0),
)
REGRAS_POR_NOME = {'velhas': REGRAS_VELHAS, 'novas': REGRAS_NOVAS}

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
        self.cortado = False   # apagando (ou esperando a barra para apagar): o nível não mexe mais nele, como o cortar do C#

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
    def __init__(self, roteiro, semente, regras=None):
        self.R = regras or REGRAS_NOVAS
        self.novas = self.R['nome'] == 'novas'
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
        # as regras novas (04/11/2026)
        self.bruto, self.mais_perto, self.proxima_olhada = 0, INF, 0.0
        self.caca_desde = self.ultima_caca = None        # desde quando alguém te caça (o bruto 2 ou mais)
        self.tensao_desde = self.ultima_tensao = None    # desde quando há tensão (o bruto 1 ou mais)
        self.zero_desde = self.sem_caca_desde = None     # no perigo: desde quando o nível é 0; desde quando o bruto é 1 ou 0
        self.recarga_ate, self.fim_da_saida = 0.0, 0.0
        self.ultimo_golpe = self.ultima_sombra = self.ultima_saida = -999.0
        self.sombra, self.sombra_ate = False, 0.0
        self.teve_golpe, self.chegou_a_tres, self.esperando = False, False, False
        self.camada3_depois, self.tempo_na_caca = 0.0, 0.0
        self.entradas = []
        # o que aconteceu na sessão (o resumo no fim do roteiro)
        self.conta = dict(perigo_comeca=0, viradas=0, sombras=0, golpes=0, saidas=0, pedras=0, mudos=0, amanheceres=0,
                          trechos=0, cortes=0, perigo=0.0, na_sombra=0.0, c2=0.0, exploracao=0.0)

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
        a, b = self.R['trecho_' + self.familia]
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
        self.conta['trechos'] += 1
        dur = len(self.fila) * MAN[self.familia]['comprimento']
        self.anotar(t, f'começa um trecho da {"Neblina" if dia else "Noite"} ({len(self.fila)} blocos, {dur / 60:.1f} min): {" > ".join(self.fila)}')

    def seguir_trecho(self, t):
        if t < self.prox_bloco - 0.3:
            return
        if not self.fila:
            a, b = self.R['silencio_depois_do_trecho']
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
                f.cortado = True

    # ------------------------------------------------------------------ o perigo
    def comecar_perigo(self, t, com_entrada, sombra=False):
        if self.estado == 'exploracao':
            self.conta['cortes'] += 1
        if sombra:
            # a SOMBRA (só nas regras novas): vem do silêncio, então não apaga nada; só a c1, baixinha, entrando devagar
            t0 = t + 0.15
            S = self.R['sombra']
            self.sombra, self.ultima_sombra = True, t
            self.sombra_ate = t0 + S['blocos'] * PERIGO['comprimento']
            self.conta['sombras'] += 1
            self.anotar(t, f'a SOMBRA: só a c1 (o coro da tensão) a {S["volume"]:.0%}, por até {S["blocos"]} blocos, até '
                           f'{mmss(self.sombra_ate)} (nível {self.nivel})')
        else:
            corta = ' [corta a exploração]' if self.novas and self.estado == 'exploracao' else ''
            self.apagar(t, ('exploracao', 'momento'), 2.0)
            if self.novas:
                # 04/11/2026: a grade velha (o perigo ou a sombra que acabou há pouco e ainda espera a barra para apagar)
                # sai em 1 s, quando a nova entra: não ficam dois coros fora de fase (o que já está apagando continua)
                self.apagar(t, ('perigo',), 1.0)
            self.fila = []
            P = PERIGO
            if self.novas:
                self.entradas.append(t)
            self.conta['perigo_comeca'] += 1
            if com_entrada:
                st = self.escolher('perigo', [m for m in P['momentos'] if m['tipo'] == 'entrada'], 'entrada')
                self.tocar(st['arquivo'], t, grupo='momento')
                t0 = t + st['encaixe']
                self.teve_entrada = True
                self.teve_golpe, self.ultimo_golpe = True, t
                self.conta['golpes'] += 1
                self.anotar(t, f'TE VIRAM: o golpe da entrada, e o perigo começa no golpe (nível {self.nivel})' + corta)
            elif self.novas:
                t0 = t + 0.15
                self.teve_golpe = False
                self.anotar(t, f'o perigo começa sem o golpe (nível {self.nivel}, {self.sem_golpe_porque(t, self.bruto >= 3)})' + corta)
            else:
                t0 = t + 0.1
                self.teve_entrada = self.nivel >= 2   # já te viram sem o golpe (o último perigo foi há pouco): não toca depois
                self.anotar(t, f'o perigo começa sem o golpe (nível {self.nivel}' + (', o último perigo foi há pouco)' if self.nivel >= 2 else ', só a tensão)'))
        self.perigo_t0, self.prox_bloco, self.plano_ant = t0, t0, None
        self.estado = 'perigo'
        self.entrar_devagar = not com_entrada
        self.camada2_depois = self.camada3_depois = 0.0
        self.tempo_na_caca = 0.0
        self.chegou_a_tres = self.nivel >= 3 and not sombra   # a horda que já entra com o perigo não ganha o golpe (c) depois
        self.zero_desde = self.sem_caca_desde = None
        self.esperando = False

    def camada_ligada(self, c, quando):
        if self.sombra:
            return c == 1   # a sombra toca só a c1, seja qual for o nível
        # 04/11/2026: no perigo de verdade, a c1 fica ligada seja qual for o nível: na graça (o nível 0 por 6 s) o coro
        # segura, e quem a apaga é a saída (1,2 s) ou o fim mudo (4 s), na barra. Antes ela sumia em 3 s e a saída vinha
        # depois de segundos de silêncio ("a música voltou"). As regras velhas não mudam: nelas o nível 0 já termina o perigo
        return (c == 1 or self.nivel >= c) and not (c == 2 and quando < self.camada2_depois) and not (c == 3 and quando < self.camada3_depois)

    def volume(self, c):
        return self.R['sombra']['volume'] if self.sombra and c == 1 else self.R['camadas'][c - 1]

    def seguir_perigo(self, t):
        P = PERIGO
        # a sombra toca no máximo os blocos dela: o seguinte só entra se ela virar caça
        cabe = not (self.sombra and self.prox_bloco >= self.sombra_ate - 0.01)
        if t >= self.prox_bloco - 0.3 and cabe:
            plano = 'A' if self.plano_ant in (None, 'B', 'C') else ('B' if self.r.random() < 0.5 else 'C')
            b = self.escolher('perigo', [x for x in P['blocos'] if x['plano'] == plano], plano)
            for c, arq in enumerate(b['camadas'], 1):
                ligado = self.camada_ligada(c, self.prox_bloco)
                g0 = self.volume(c) if ligado else 0.0
                f = self.tocar(arq, self.prox_bloco, 0.0 if self.entrar_devagar else g0, 'perigo', c)
                if self.entrar_devagar and ligado:
                    # sem o golpe, o perigo chega devagar; a sombra, mais devagar ainda (4 s)
                    f.rampa(g0, self.R['sombra']['entra'] if self.sombra else (3.0 if c == 1 else 1.5))
            self.entrar_devagar = False
            self.plano_ant = plano
            self.prox_bloco += P['comprimento']
        # o volume de cada camada segue o nível (sobe rápido, desce devagar)
        for f in self.fontes:
            if f.grupo == 'perigo' and f.fim > t and f.inicio <= t + 0.5 and not f.agenda and not f.cortado:
                alvo = self.volume(f.camada) if self.camada_ligada(f.camada, t) else 0.0
                f.rampa(alvo, 0.6 if alvo > f.ganho else 3.0)   # o mesmo alvo não mexe na rampa que já corre

    def entrada_no_meio(self, t, camada=2, onde='já na tensão'):
        """O golpe no meio de um perigo que já toca: cai num tempo da grade, e a camada (a c2 ou a c3) espera por ele."""
        P = PERIGO
        st = self.escolher('perigo', [m for m in P['momentos'] if m['tipo'] == 'entrada'], 'entrada')
        k = math.ceil((t + st['encaixe'] - self.perigo_t0) / TEMPO)
        golpe = self.perigo_t0 + k * TEMPO
        self.tocar(st['arquivo'], golpe - st['encaixe'], grupo='momento')
        if camada == 3:
            self.camada3_depois = golpe
        else:
            self.camada2_depois = golpe
            if self.novas:
                self.camada3_depois = golpe   # na virada com a horda, a c3 também espera o golpe (não entra antes da c2)
        self.teve_entrada = True
        self.teve_golpe, self.ultimo_golpe = True, t
        self.conta['golpes'] += 1
        if self.novas:
            self.anotar(t, f'TE VIRAM ({onde}): o golpe cai no tempo do perigo, em {mmss(golpe)}; '
                           + ('a c3 espera por ele' if camada == 3 else 'a c2 (e a c3) esperam por ele'))
        else:
            self.anotar(t, f'TE VIRAM (já na tensão): o golpe cai no tempo do perigo, em {mmss(golpe)}')

    def terminar_perigo(self, t, pedra):
        P = PERIGO
        b = self.perigo_t0 + math.ceil((t - self.perigo_t0) / COMPASSO + 0.01) * COMPASSO
        # qual saída toca (R9): o amanhecer pendente, a pedra, a saída (só depois de uma caça de verdade e se a última
        # saída foi há 45 s) ou, nas regras novas, o fim mudo (sem clipe). A sombra sempre acaba muda
        arq = None
        if self.sombra:
            nome = 'a sombra se apaga'
        elif self.amanhecer_pendente:
            arq, nome = MAN['momentos']['amanhecer'], 'o AMANHECER no lugar da saída'
            self.amanhecer_pendente = False
            self.conta['amanheceres'] += 1
        elif pedra:
            arq = self.escolher('perigo', [m for m in P['momentos'] if m['tipo'] == 'pedra'], 'pedra')['arquivo']
            nome = 'a saída pela PEDRA (quem te caçava virou estátua)'
            self.conta['pedras'] += 1
        elif not self.novas or (self.tempo_na_caca >= self.R['saida_depois_de'] - EPS and b - self.ultima_saida >= self.R['saida_a_cada'] - EPS):
            arq = self.escolher('perigo', [m for m in P['momentos'] if m['tipo'] == 'saida'], 'saida')['arquivo']
            nome = 'a saída do perigo'
            self.ultima_saida = b
            self.conta['saidas'] += 1
        else:
            nome = (f'o fim mudo (as camadas apagam em {self.R["fim_mudo"]:.0f} s, sem clipe: '
                    + (f'a caça durou só {self.tempo_na_caca:.0f} s)' if self.tempo_na_caca < self.R['saida_depois_de'] - EPS
                       else f'a última saída foi há {b - self.ultima_saida:.0f} s)'))
            self.conta['mudos'] += 1
        apaga = 1.2 if arq else self.R['fim_mudo']
        for f in self.fontes:
            if f.grupo == 'perigo' and f.fim > b:
                if f.inicio >= b - 0.01:
                    f.fim = f.inicio
                    f.pontos = [(f.inicio, 0.0)]
                    f.ganho = f.alvo = 0.0
                else:
                    f.rampa(0.0, apaga, quando=b)
                    f.cortado = True
        self.estado = 'silencio'
        if self.sombra:
            # a sombra não conta como perigo: não mexe no fim do perigo nem arma a recarga
            self.sombra = False
            self.proxima_musica = max(self.proxima_musica, b + self.R['sombra']['silencio_depois'])
            self.anotar(t, f'a sombra se apaga, na barra ({mmss(b)}): a c1 some em {self.R["fim_mudo"]:.0f} s, sem clipe')
            return
        a, c = self.R['silencio_depois_do_perigo']
        if arq:
            f = self.tocar(arq, b, grupo='momento')
            self.fim_da_saida = f.fim
        else:
            self.fim_da_saida = b + self.R['fim_mudo']
        self.fim_perigo = b
        self.teve_entrada = self.teve_golpe = False
        self.proxima_musica = self.fim_da_saida + float(self.r.uniform(a, c))
        if self.novas:
            self.recarga_ate = b + self.R['recarga']
            self.anotar(t, f'o perigo acabou: {nome}, no compasso ({mmss(b)}), depois de {self.tempo_na_caca:.0f} s de caça; '
                           f'a recarga até {mmss(self.recarga_ate)}; depois só o vento até {mmss(self.proxima_musica)}')
        else:
            self.anotar(t, f'o perigo acabou: {nome}, no compasso ({mmss(b)}); depois só o vento até {mmss(self.proxima_musica)}')

    # ------------------------------------------------------------------ as regras novas (04/11/2026)
    def orcamento_cheio(self, t):
        """R5: já foram 3 entradas nos últimos 300 s (a sombra que vira caça conta)."""
        n, janela = self.R['orcamento']
        self.entradas = [e for e in self.entradas if t - e < janela]
        return len(self.entradas) >= n

    def pedra_recente(self, t):
        return any(t - self.R['pedra_conta'] <= p <= t for p in self.roteiro.get('pedra', []))

    def pode_golpe(self, t, horda):
        """R11: o "te viram" no máximo 1 a cada 2 min, 90 s depois do último perigo (a horda não precisa disso), e só
        com um caçador teu a até 15 m."""
        R = self.R
        return (t - self.ultimo_golpe >= R['golpe_a_cada'] - EPS and (horda or t - self.fim_perigo >= R['entrada_de_novo'] - EPS)
                and self.mais_perto <= R['golpe_perto'])

    def sem_golpe_porque(self, t, horda):
        R = self.R
        if t - self.ultimo_golpe < R['golpe_a_cada'] - EPS:
            return f'o último golpe foi há {t - self.ultimo_golpe:.0f} s'
        if not horda and t - self.fim_perigo < R['entrada_de_novo'] - EPS:
            return f'o último perigo acabou há {t - self.fim_perigo:.0f} s'
        if self.mais_perto > R['golpe_perto']:
            return f'nenhum caçador a até {R["golpe_perto"]:.0f} m'
        return 'o golpe está liberado'

    def marcar_os_desde(self, t):
        """Desde quando há caça (o bruto 2 ou mais) e tensão (1 ou mais), aceitando um buraco de até 0,5 s (R4)."""
        furo = self.R['furo']
        if self.bruto >= 2:
            if self.caca_desde is None:
                self.caca_desde = t
            self.ultima_caca = t
        elif self.caca_desde is not None and t - self.ultima_caca > furo + EPS:
            self.caca_desde = None
            self.esperando = False
        if self.bruto >= 1:
            if self.tensao_desde is None:
                self.tensao_desde = t
            self.ultima_tensao = t
        elif self.tensao_desde is not None and t - self.ultima_tensao > furo + EPS:
            self.tensao_desde = None

    # quanto a caça (e a tensão) já durou: de quando começou até a última olhada que a viu. No buraco, a conta para (não
    # confirma ninguém enquanto ninguém te caça) e, se a caça volta em até 0,5 s, continua de onde estava
    def duracao_da_caca(self):
        return self.ultima_caca - self.caca_desde if self.caca_desde is not None else -1.0

    def duracao_da_tensao(self):
        return self.ultima_tensao - self.tensao_desde if self.tensao_desde is not None else -1.0

    def pode_entrar(self, t):
        """No silêncio ou na exploração (R4, R5, R6): 'perigo' (a caça confirmada entra), 'sombra' (só no silêncio),
        'esperando' (a caça já durou 2 s, mas a recarga ou o orçamento não deixam) ou 'nada'."""
        R, C = self.R, self.R['confirmar']
        horda = self.bruto >= 3
        cheio = self.orcamento_cheio(t)
        na_recarga = t < self.recarga_ate
        cacando = self.duracao_da_caca()
        if horda:
            precisa = C['horda']           # a horda: 0,5 s, sempre (fura o orçamento e a recarga)
        elif cheio:
            precisa = INF                  # o orçamento cheio: só a horda entra
        elif na_recarga:
            precisa = C['recarga']         # na recarga: 6 s
        elif self.pode_golpe(t, False):
            precisa = C['golpe']           # o golpe liberado e o caçador perto: 1 s
        else:
            precisa = C['caca']            # o resto: 2 s
        if cacando >= precisa - EPS:
            return 'perigo'
        S = R['sombra']
        if (self.estado == 'silencio' and self.duracao_da_tensao() >= S['confirmar'] - EPS
                and not na_recarga and t - self.ultima_sombra > S['a_cada'] and t >= self.fim_da_saida):
            return 'sombra'
        if cacando >= C['caca'] - EPS:
            return 'esperando'
        return 'nada'

    def caca_esperando(self, t):
        """R7: a caça já está confirmada, mas a música não pode entrar. A exploração que toca fecha no fim do bloco
        (sem corte), e a próxima música fica para depois da briga."""
        if not self.esperando:
            self.esperando = True
            n, janela = self.R['orcamento']
            if self.orcamento_cheio(t):
                motivo = f'orçamento: {len(self.entradas)} entradas em {janela / 60:.0f} min'
            else:
                motivo = f'recarga: faltam {self.recarga_ate - t:.0f} s, e a caça precisa de {self.R["confirmar"]["recarga"]:.0f} s'
            self.anotar(t, f'caça esperando ({motivo})')
        if self.estado == 'exploracao' and self.fila:
            # nenhum bloco do trecho tocou ainda (no jogo, o tornado avistado acabou de abrir o dele): a entrada toca, e
            # logo a saída (04/11/2026; senão o trecho seria só a saída)
            curta = ['entrada', 'saida'] if self.fila[0] == 'entrada' else ['saida']
            if self.fila != curta:
                self.fila = curta
                self.anotar(t, 'o trecho fecha por causa da caça: a saída no fim do bloco que toca, sem corte')
        self.proxima_musica = max(self.proxima_musica, t + self.R['musica_depois_da_caca'])

    def virar_a_sombra(self, t, horda):
        """R6: a caça confirmada durante a sombra a transforma em perseguição (conta como entrada)."""
        self.sombra = False
        self.entradas.append(t)
        self.conta['viradas'] += 1
        self.chegou_a_tres = self.nivel >= 3
        if self.pode_golpe(t, horda):
            self.anotar(t, f'a sombra vira caça: o perigo começa (nível {self.nivel})')
            self.entrada_no_meio(t, 2, 'a sombra vira caça')
        else:
            # sem o golpe: a c2 (e a c3, na horda) sobem numa rampa de 1,5 s; a c1 vai de 75% a 100% como sempre (0,6 s)
            self.anotar(t, f'a sombra vira caça: o perigo começa (nível {self.nivel}), sem o golpe '
                           f'({self.sem_golpe_porque(t, horda)}): a c2 entra numa rampa de 1,5 s')
            for f in self.fontes:
                if f.grupo == 'perigo' and f.camada >= 2 and f.fim > t and f.inicio <= t + 0.5 and not f.agenda and not f.cortado:
                    alvo = self.volume(f.camada) if self.camada_ligada(f.camada, t) else 0.0
                    if alvo > f.ganho:
                        f.rampa(alvo, 1.5)

    def acabou_o_perigo(self, t):
        """R8: o fim do perigo. A sombra acaba no fim dos blocos dela ou com 6 s no nível 0."""
        R = self.R
        zero = t - self.zero_desde if self.zero_desde is not None else -1.0
        if self.sombra:
            return t >= self.sombra_ate - EPS or zero >= R['graca'] - EPS
        graca = 0.0 if self.pedra_recente(t) else R['graca']   # o caçador virou pedra: acaba já
        sem_caca = t - self.sem_caca_desde if self.sem_caca_desde is not None else -1.0
        return zero >= graca - EPS or sem_caca >= R['teto_sem_caca'] - EPS

    def perigo_novo(self, t):
        """O perigo nas regras novas, a cada quadro: a virada da sombra, o golpe da horda e o fim."""
        if self.nivel >= 2:
            self.tempo_na_caca += DT   # o tempo de caça do perigo (a sombra inclusive), para a saída ou o fim mudo (R9)
        if self.sombra:
            horda = self.bruto >= 3
            C = self.R['confirmar']
            cacando = self.duracao_da_caca()
            if cacando >= (C['horda'] if horda else C['caca']) - EPS and (horda or not self.orcamento_cheio(t)):
                self.virar_a_sombra(t, horda)
        if not self.sombra:
            # (c) a primeira chegada ao nível 3 de um perigo que ainda não teve golpe: o golpe, com a c3 esperando
            if self.nivel >= 3 and not self.chegou_a_tres:
                self.chegou_a_tres = True
                if not self.teve_golpe and self.pode_golpe(t, True):
                    self.entrada_no_meio(t, 3, 'a horda chegou')
        self.zero_desde = (self.zero_desde if self.zero_desde is not None else t) if self.nivel == 0 else None
        self.sem_caca_desde = (self.sem_caca_desde if self.sem_caca_desde is not None else t) if self.bruto <= 1 else None
        if self.acabou_o_perigo(t):
            self.terminar_perigo(t, self.pedra_recente(t))
        else:
            self.seguir_perigo(t)

    # ------------------------------------------------------------------ o quadro
    def ameaca(self, t):
        """O bruto (o nível na hora, de 0 a 3) e a distância do caçador teu mais perto, pelo roteiro."""
        bruto, perto = 0, INF
        for a, b, n in self.roteiro.get('perigo', []):
            if a <= t < b:
                bruto = max(bruto, n)
                if PERTO_DO_NIVEL.get(n) is not None:
                    perto = min(perto, PERTO_DO_NIVEL[n])
        for item in self.roteiro.get('ameaca', []):
            a, b, tipo = item[:3]
            if a <= t < b:
                bruto = max(bruto, self.R['mapa'][tipo])
                m = item[3] if len(item) > 3 else PERTO[tipo]
                if m is not None:
                    perto = min(perto, m)
        return bruto, perto

    def nivel_do_jogo(self, t):
        # as regras novas olham a cada 0,25 s, como o jogo, e guardam desde quando há caça e tensão; as velhas, a cada quadro
        if not self.novas or t >= self.proxima_olhada - EPS:
            if self.novas:
                self.proxima_olhada = t + self.R['olhar_a_cada']
            self.bruto, self.mais_perto = self.ameaca(t)
            if self.novas:
                self.marcar_os_desde(t)
        bruto = self.bruto
        if bruto > self.nivel:
            self.nivel, self.descendo_desde = bruto, None
        elif bruto < self.nivel:
            if self.descendo_desde is None:
                self.descendo_desde = t
            elif t - self.descendo_desde >= (self.R['desce_da_horda'] if self.nivel == 3 else self.R['desce_um_nivel']):
                self.nivel -= 1
                self.descendo_desde = t
        else:
            self.descendo_desde = None
        return bruto

    def contar(self):
        """O tempo de cada estado (o resumo no fim do roteiro); conta o quadro que acabou de passar."""
        if self.estado == 'perigo':
            self.conta['perigo'] += DT
            if self.sombra:
                self.conta['na_sombra'] += DT
            elif self.camada_ligada(2, self.ultimo_t):
                self.conta['c2'] += DT
        elif self.estado == 'exploracao':
            self.conta['exploracao'] += DT

    def quadro(self, t):
        if hasattr(self, 'ultimo_t'):
            self.contar()
        self.ultimo_t = t
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
            self.tocar(b['arquivo'], inicio, 1.0 if self.estado == 'silencio' else self.R['vento_na_musica'], 'vento')
            self.vento_prox = inicio + b['duracao'] - MAN[fam]['sobreposicao']
        vivas = [f for f in self.fontes if f.inicio - DT <= t <= f.fim + DT]
        tocando = any(f.grupo != 'vento' and (f.ganho > 0.02 or f.alvo > 0.02) and t < f.fim - 5.0 for f in vivas)
        alvo_vento = self.R['vento_na_musica'] if tocando or self.estado in ('exploracao', 'perigo') else 1.0
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
            self.morto, self.estado, self.sombra = True, 'fim', False
            self.anotar(t, 'MORTE: tudo some e toca a morte; depois só o vento')
            return
        if any(abs(t - m) < DT / 2 for m in self.roteiro.get('amanhecer', [])):
            if self.estado == 'perigo' and not self.sombra:
                self.amanhecer_pendente = True   # o terminar_perigo toca (e conta) o amanhecer no lugar da saída
                if self.nivel >= 2:
                    self.anotar(t, 'amanheceu no meio do perigo: o amanhecer espera o perigo acabar')
                else:
                    self.terminar_perigo(t, False)
            else:
                # no silêncio, na exploração ou na sombra (que se comporta como o silêncio): o amanhecer na hora
                self.apagar(t, ('exploracao', 'perigo') if self.sombra else ('exploracao',), 3.0)
                self.sombra = False
                f = self.tocar(MAN['momentos']['amanhecer'], t, grupo='momento')
                self.estado, self.fila = 'silencio', []
                self.fim_da_saida = max(self.fim_da_saida, f.fim)   # a sombra não abre por cima do amanhecer
                self.conta['amanheceres'] += 1
                self.proxima_musica = f.fim + float(self.r.uniform(*self.R['silencio_depois_do_trecho']))
                self.anotar(t, f'AMANHECER: a noite some e toca o amanhecer; depois só o vento até {mmss(self.proxima_musica)}')
        if self.estado in ('silencio', 'exploracao'):
            if self.novas:
                o = self.pode_entrar(t)
                if o == 'perigo':
                    self.comecar_perigo(t, com_entrada=self.pode_golpe(t, self.bruto >= 3))
                elif o == 'sombra':
                    self.comecar_perigo(t, com_entrada=False, sombra=True)
                else:
                    if o == 'esperando':
                        self.caca_esperando(t)
                    if self.estado == 'silencio' and o == 'nada' and t >= self.proxima_musica:
                        self.comecar_trecho(t, dia)
                    elif self.estado == 'exploracao':
                        self.seguir_trecho(t)
            elif self.nivel >= 1:
                pode = t - self.fim_perigo > self.R['entrada_de_novo']
                self.comecar_perigo(t, com_entrada=self.nivel >= 2 and pode)
            elif self.estado == 'silencio' and t >= self.proxima_musica:
                self.comecar_trecho(t, dia)
            elif self.estado == 'exploracao':
                self.seguir_trecho(t)
        if self.estado == 'perigo':
            if self.novas:
                self.perigo_novo(t)
                return
            if self.nivel >= 2 and not self.teve_entrada and t - self.fim_perigo > self.R['entrada_de_novo']:
                self.entrada_no_meio(t)
            if self.nivel == 0:
                pedra = self.pedra_recente(t)
                self.terminar_perigo(t, pedra)
            else:
                self.seguir_perigo(t)

    def resumo(self, duracao):
        c = self.conta
        tv = c['golpes']
        return [
            f'# resumo (regras {self.R["nome"]}, família do perigo "{FAMILIA}", {PERIGO["bpm"]} bpm, bloco de {PERIGO["comprimento"]:.2f} s)',
            f'# entradas: {c["perigo_comeca"] + c["viradas"]} "o perigo começa" (das quais {c["viradas"]} na virada da sombra); '
            f'{c["sombras"]} sombra(s); {tv} TE VIRAM',
            f'# fins: {c["saidas"]} saída(s), {c["pedras"]} pela pedra, {c["mudos"]} mudo(s), {c["amanheceres"]} amanhecer(es)',
            f'# trechos: {c["trechos"]} ({c["cortes"]} cortado(s) pelo perigo)',
            f'# tempo: perigo {c["perigo"]:.0f} s ({c["perigo"] / duracao:.0%}), dos quais {c["na_sombra"]:.0f} s de sombra e '
            f'{c["c2"]:.0f} s com a c2; exploração {c["exploracao"]:.0f} s ({c["exploracao"] / duracao:.0%})',
        ]


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
    # 04/11/2026: 10 min de noite pesada, do ponto de vista de um cliente no co-op, com cada tipo de ameaça (o nível de
    # cada uma vem do VELHO ou do NOVO): os tiros (um zumbi indo ver, ou dois), a lanterna passando num zumbi, as caças,
    # a briga do amigo perto, a horda. A pedra em 299, no fim da briga, para ouvir a saída pela pedra
    'zumbis': dict(duracao=600, semente=11, primeira_musica=8.0, noite=[(0, 600)], pedra=[299], ameaca=[
        (40, 65, 'barulho'), (70, 74, 'lanterna'), (92, 97, 'barulho'), (104, 122, 'barulho2'),
        (120, 138, 'caca'), (138, 146, 'barulho2'), (150, 154, 'barulho'),
        (172, 175, 'caca'), (195, 215, 'vizinho'), (222, 226, 'barulho'), (245, 250, 'lanterna'),
        (262, 268, 'caca'), (268, 292, 'horda'), (292, 300, 'caca4'), (300, 310, 'barulho2'),
        (322, 325, 'caca'), (330, 336, 'caca'), (352, 380, 'barulho'), (378, 383, 'lanterna'),
        (400, 430, 'caca'), (436, 446, 'caca'), (448, 452, 'barulho'), (470, 500, 'barulho2'),
        (500, 540, 'caca4'), (515, 535, 'horda'), (560, 564, 'barulho'), (580, 586, 'caca')]),
    # 04/11/2026: o cerco, 12 caças de verdade de 15 s, uma a cada 50 s, cada uma seguida de 10 s de dois investigando
    # (o orçamento e a recarga trabalhando)
    'cerco': dict(duracao=600, semente=11, primeira_musica=8.0, noite=[(0, 600)],
                  ameaca=[(a, a + 15, 'caca') for a in range(30, 600, 50)] + [(a + 15, a + 25, 'barulho2') for a in range(30, 600, 50)]),
}


def simular(qual, regras):
    """Roda o maestro no roteiro, sem mixar (as decisões e as fontes)."""
    rot = ROTEIROS[qual]
    m = Maestro(rot, rot['semente'], REGRAS_POR_NOME[regras])
    t = 0.0
    while t < rot['duracao']:
        m.quadro(round(t, 4))
        t += DT
    return m


if __name__ == '__main__':
    qual = sys.argv[2] if len(sys.argv) > 2 else 'dia'
    regras = sys.argv[3] if len(sys.argv) > 3 else 'novas'
    if qual not in ROTEIROS or regras not in REGRAS_POR_NOME:
        sys.exit(f'uso: python maestro.py [pasta] [{"|".join(ROTEIROS)}] [{"|".join(REGRAS_POR_NOME)}]')
    rot = ROTEIROS[qual]
    m = simular(qual, regras)
    nome = f'ZCC_Sessao_{qual}_{regras}'
    mixar(m, rot['duracao'], nome)
    resumo = m.resumo(rot['duracao'])
    with open(os.path.join('saida', nome + '_roteiro.txt'), 'w', encoding='utf-8') as arq:
        for t, texto in m.log:
            arq.write(f'{mmss(t)}  {texto}\n')
        for linha in resumo:
            arq.write(linha + '\n')
    for t, texto in m.log:
        print(f'{mmss(t)}  {texto}')
    for linha in resumo:
        print(linha)
