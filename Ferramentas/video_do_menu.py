# O vídeo do fundo do menu (a pedido em 29/10/2026): junta as tomadas que o Video/FilmeDoMenu gravou (uma pasta de JPGs
# por tomada: 1_rua, 2_janela, 3_pisca, 4_perto, 5_alto), cada uma passando para a próxima pelo escuro, com a cor do
# menu (menos saturada, as sombras frias, a vinheta), e fecha o laço no escuro (o fim emenda no começo sem corte). Sai um
# MP4 (H.264, no tamanho das tomadas: 2560 × 1440 desde a segunda versão, 30 por segundo, sem som) e a imagem de capa (o
# quadro que aparece se o vídeo faltar).
# A segunda versão (a pedido: "a qualidade está baixa"): as tomadas em 2560 × 1440 (eram 1280 × 720, esticadas 2 vezes
# na tela), a compressão bem mais leve (CRF 18, era 25) e o grão de filme fora do vídeo (o UI/FundoDoMenu o desenha na
# tela): gravado no vídeo, o grão gastava a compressão e virava blocos nas partes escuras.
# Uso: python video_do_menu.py <pasta das tomadas> [pasta de saída] [LxA]   (sem saída, ou "-": Assets/_Game/Art/Menu/
# Resources, onde o UI/FundoDoMenu carrega o FundoDoMenu_Video e o FundoDoMenu_Capa; com LxA, por exemplo 2560x1440, as
# tomadas menores são ampliadas com o lanczos e um pouco de nitidez, que fica melhor do que a tela esticar o vídeo)
import os
import sys
import glob
import subprocess

AQUI = os.path.dirname(os.path.abspath(__file__))
FPS = 30
ESCURO = 1.4      # cada passagem pelo escuro (s)
CAPA = ('2_janela', 0.72)   # a capa: o quadro desta tomada, nesta altura (0 a 1)
CRF = 19          # a qualidade do H.264 (menor, melhor e maior)
# o grão do pós-processamento do jogo sai antes da compressão (um filtro de ruído leve, no espaço e no tempo): com ele,
# o vídeo passava de 75 MB; o grão volta desenhado na tela, pelo UI/FundoDoMenu
LIMPAR = 'hqdn3d=2:1.5:5:4,'


def tamanho_do_quadro(pasta):
    """A largura e a altura do primeiro JPG da tomada."""
    primeiro = sorted(glob.glob(os.path.join(pasta, '*.jpg')))[0]
    r = subprocess.run(['ffprobe', '-v', 'error', '-select_streams', 'v', '-show_entries', 'stream=width,height', '-of', 'csv=p=0', primeiro],
                       capture_output=True, text=True, check=True)
    largura, altura = r.stdout.strip().split(',')[:2]
    return int(largura), int(altura)


def main():
    tomadas = sys.argv[1]
    saida = sys.argv[2] if len(sys.argv) > 2 and sys.argv[2] != '-' else os.path.join(AQUI, '..', 'Assets', '_Game', 'Art', 'Menu', 'Resources')
    alvo = tuple(int(v) for v in sys.argv[3].lower().split('x')) if len(sys.argv) > 3 else None
    os.makedirs(saida, exist_ok=True)
    pastas = sorted(p for p in glob.glob(os.path.join(tomadas, '*')) if os.path.isdir(p) and glob.glob(os.path.join(p, '*.jpg')))
    if not pastas:
        sys.exit('nenhuma tomada em ' + tomadas)
    duracoes = [len(glob.glob(os.path.join(p, '*.jpg'))) / FPS for p in pastas]
    entradas = []
    for p in pastas:
        entradas += ['-framerate', str(FPS), '-i', os.path.join(p, '%05d.jpg')]

    # cada tomada no tamanho final: as menores ampliadas (o lanczos e um pouco de nitidez); as tomadas podem ter tamanhos
    # diferentes (por exemplo, uma regravada em 2560 × 1440 com as outras em 1280 × 720)
    def ajuste(tamanho):
        if alvo is None or tamanho == alvo:
            return 'setsar=1'
        return f'scale={alvo[0]}:{alvo[1]}:flags=lanczos,unsharp=5:5:0.55:5:5:0,setsar=1'

    filtros = [f'[{k}:v]{ajuste(tamanho_do_quadro(p))}[e{k}]' for k, p in enumerate(pastas)]
    # as passagens pelo escuro (xfade "fadeblack"), uma depois da outra
    atual, fim = '[e0]', duracoes[0]
    for k in range(1, len(pastas)):
        offset = fim - ESCURO
        rotulo = f'[x{k}]'
        filtros.append(f'{atual}[e{k}]xfade=transition=fadeblack:duration={ESCURO}:offset={offset:.3f}{rotulo}')
        atual = rotulo
        fim = offset + duracoes[k]
    total = fim
    # a cor do menu e o laço: entra e sai do escuro (o fim emenda no começo)
    cor = (LIMPAR + 'eq=contrast=1.08:brightness=-0.012:saturation=0.72,'
           'colorbalance=rs=-0.03:bs=0.06:rm=-0.02:bm=0.03,'
           'vignette=angle=PI/4.2,'
           f'fade=t=in:st=0:d={ESCURO / 2:.2f},fade=t=out:st={total - ESCURO / 2:.3f}:d={ESCURO / 2:.2f},'
           # os JPGs vêm na faixa cheia (0 a 255); o vídeo vai na faixa de TV (16 a 235), convertido de verdade (só marcar
           # deixaria o preto abaixo de 16 sumir no Windows)
           'scale=in_range=pc:out_range=tv,format=yuv420p,setparams=range=tv:color_primaries=bt709:color_trc=bt709:colorspace=bt709')
    filtros.append(f'{atual}{cor}[v]')
    video = os.path.join(saida, 'FundoDoMenu_Video.mp4')
    # sem quadros B (o Media Foundation do Windows reclama dos tempos fora de ordem); a cor vai marcada nos quadros
    # (BT.709, no setparams; sem a marca, o Unity avisa que pode mudar a cor). O "film" e o aq-mode 3 dão mais bits às
    # partes escuras e lisas (a neblina), onde os blocos apareciam
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', *entradas, '-filter_complex', ';'.join(filtros), '-map', '[v]',
                    '-c:v', 'libx264', '-preset', 'slow', '-tune', 'film', '-crf', str(CRF), '-x264-params', 'aq-mode=3',
                    '-profile:v', 'high', '-bf', '0', '-r', str(FPS), '-movflags', '+faststart', '-an', video], check=True)
    # a capa: um quadro da tomada escolhida, com a mesma cor
    nome, onde = CAPA
    escolhida = os.path.join(tomadas, nome)
    if not os.path.isdir(escolhida):
        escolhida = pastas[0]
    quadros = sorted(glob.glob(os.path.join(escolhida, '*.jpg')))
    capa = os.path.join(saida, 'FundoDoMenu_Capa.jpg')
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', quadros[int(onde * (len(quadros) - 1))],
                    '-vf', f'{ajuste(tamanho_do_quadro(escolhida))},eq=contrast=1.08:brightness=-0.012:saturation=0.72,colorbalance=rs=-0.03:bs=0.06:rm=-0.02:bm=0.03,vignette=angle=PI/4.2',
                    '-q:v', '2', capa], check=True)
    tamanho = os.path.getsize(video) / 1e6
    print(f'{len(pastas)} tomadas ({", ".join(f"{os.path.basename(p)} {d:.1f} s" for p, d in zip(pastas, duracoes))}) -> {total:.1f} s, {tamanho:.1f} MB; capa {capa}')


if __name__ == '__main__':
    main()
