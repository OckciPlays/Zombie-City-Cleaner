# Leva a trilha para o jogo: os blocos (blocos.py) e o amanhecer e a morte (compor.py) viram .ogg em
# Assets/_Game/Audio/Trilha/<família>/, e o trilha.json guarda o andamento e o comprimento de cada família (o Unity lê
# no menu Zombie City Cleaner > Trilha > Montar a trilha, que monta o Audio/Resources/TrilhaDoJogo.asset).
# Uso (nesta pasta): python blocos.py   (os blocos, em blocos/)
#                    python compor.py saida amanhecer morte
#                    python exportar.py
import os
import sys
import json
import shutil
import subprocess

AQUI = os.path.dirname(os.path.abspath(__file__))
BLOCOS = os.path.join(AQUI, 'blocos')
SAIDA = os.path.join(AQUI, 'saida')
DESTINO = os.path.normpath(os.path.join(AQUI, '..', '..', 'Assets', '_Game', 'Audio', 'Trilha'))
QUALIDADE = '5'   # o Vorbis do ffmpeg (0 a 10); o Unity comprime de novo no build


def ogg(origem, destino):
    os.makedirs(os.path.dirname(destino), exist_ok=True)
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', origem, '-c:a', 'libvorbis', '-q:a', QUALIDADE, destino], check=True)


def main():
    man = json.load(open(os.path.join(BLOCOS, 'manifesto.json'), encoding='utf-8'))
    # tira os .ogg velhos (os .meta ficam: o Unity reaproveita quando o nome volta)
    if os.path.isdir(DESTINO):
        for raiz, _, arquivos in os.walk(DESTINO):
            for a in arquivos:
                if a.endswith('.ogg'):
                    os.remove(os.path.join(raiz, a))
    n = 0
    for fam in ('neblina', 'noite'):
        for b in man[fam]['blocos']:
            ogg(os.path.join(BLOCOS, b['arquivo']), os.path.join(DESTINO, fam, b['nome'] + '.ogg'))
            n += 1
    for b in man['perigo']['blocos']:
        for arq in b['camadas']:
            ogg(os.path.join(BLOCOS, arq), os.path.join(DESTINO, 'perigo', os.path.basename(arq)[:-4] + '.ogg'))
            n += 1
    for m in man['perigo']['momentos']:
        ogg(os.path.join(BLOCOS, m['arquivo']), os.path.join(DESTINO, 'perigo', m['nome'] + '.ogg'))
        n += 1
    for fam in ('vento_dia', 'vento_noite'):
        for b in man[fam]['blocos']:
            ogg(os.path.join(BLOCOS, b['arquivo']), os.path.join(DESTINO, 'vento', b['nome'] + '.ogg'))
            n += 1
    for nome, arq in (('amanhecer', 'ZCC_07_Momento_Amanhecer.wav'), ('morte', 'ZCC_08_Momento_Morte.wav')):
        origem = os.path.join(SAIDA, arq)
        if not os.path.exists(origem):
            subprocess.run([sys.executable, 'compor.py', 'saida', nome], cwd=AQUI, check=True)
        ogg(origem, os.path.join(DESTINO, 'momentos', nome + '.ogg'))
        n += 1
    entrada = next(m for m in man['perigo']['momentos'] if m['tipo'] == 'entrada')
    info = dict(neblina=dict(bpm=man['neblina']['bpm'], comprimento=man['neblina']['comprimento']),
                noite=dict(bpm=man['noite']['bpm'], comprimento=man['noite']['comprimento']),
                perigo=dict(bpm=man['perigo']['bpm'], comprimento=man['perigo']['comprimento']),
                encaixeDaEntrada=entrada['encaixe'], sobreposicaoDoVento=man['vento_dia']['sobreposicao'])
    with open(os.path.join(DESTINO, 'trilha.json'), 'w', encoding='utf-8') as f:
        json.dump(info, f, indent=1)
    tamanho = sum(os.path.getsize(os.path.join(r, a)) for r, _, fs in os.walk(DESTINO) for a in fs if a.endswith('.ogg'))
    print(f'{n} arquivos em {DESTINO} ({tamanho / 1e6:.1f} MB)')


if __name__ == '__main__':
    main()
