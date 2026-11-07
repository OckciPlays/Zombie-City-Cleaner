# O acabamento das fotos da moto (Python comum, com o Pillow; roda fora do Blender):
#   python moto_fotos.py v1
# Lê renders/<versão>/Moto_*.png (fundo transparente, do moto.py) e grava, sem apagar nada:
# - renders/<versão>/com_fundo/Moto_<vista>.png: cada foto sobre um fundo claro de estúdio;
# - renders/<versão>/Moto_<versão>_prancha.png: as 6 vistas numa folha, com os nomes e os números;
# - renders/<versão>/Moto_<versão>_comparacao.png: a foto de referência, a lateral do modelo na mesma escala e as duas
#   sobrepostas.
import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

AQUI = os.path.dirname(os.path.abspath(__file__))
VISTAS = [
    ("lateral_direita", "Lateral direita"),
    ("lateral_esquerda", "Lateral esquerda"),
    ("tres_quartos_frente", "Três quartos de frente"),
    ("tres_quartos_tras", "Três quartos de trás"),
    ("frente", "Frente"),
    ("motor_close", "Motor (close)"),
]


def fonte(tam, negrito=False):
    for nome in (("segoeuib.ttf" if negrito else "segoeui.ttf"), "arial.ttf"):
        try:
            return ImageFont.truetype(os.path.join("C:/Windows/Fonts", nome), tam)
        except OSError:
            continue
    return ImageFont.load_default()


def fundo(tam):
    """Um fundo de estúdio: claro em cima, um pouco mais escuro embaixo."""
    w, h = tam
    bg = Image.new("RGBA", tam)
    px = bg.load()
    for y in range(h):
        t = y / max(1, h - 1)
        c = int(244 - 22 * t * t)
        for x in range(w):
            px[x, y] = (c, c, c - 3, 255)
    return bg


def com_fundo(caminho):
    im = Image.open(caminho).convert("RGBA")
    bg = fundo(im.size)
    bg.alpha_composite(im)
    return bg.convert("RGB")


def main():
    versao = sys.argv[1] if len(sys.argv) > 1 else "v1"
    pasta = os.path.join(AQUI, "renders", versao)
    saida = os.path.join(pasta, "com_fundo")
    os.makedirs(saida, exist_ok=True)
    medidas = json.load(open(os.path.join(AQUI, f"medidas_{versao}.json"), encoding="utf-8"))
    fotos = {}
    for chave, _ in VISTAS:
        img = com_fundo(os.path.join(pasta, f"Moto_{chave}.png"))
        img.save(os.path.join(saida, f"Moto_{chave}.png"))
        fotos[chave] = img
    # a prancha: 3 colunas x 2 linhas, cada célula 800 x 520
    cw, chh, topo = 800, 520, 120
    folha = Image.new("RGB", (cw * 3, topo + chh * 2 + 60), (250, 250, 248))
    d = ImageDraw.Draw(folha)
    tri = medidas["triangulos_total"]
    d.text((30, 22), "Moto do Zombie City Cleaner — Fat Boy 2019 \"inspirada\" (" + versao + ")", font=fonte(40, True),
           fill=(25, 25, 25))
    def br(x, casas):
        return f"{x:.{casas}f}".replace(".", ",")

    d.text((30, 74), f"{tri:,} triângulos".replace(",", ".") +
           f"   ·   comprimento {br(medidas['comprimento'], 2)} m   ·   entre-eixos {br(medidas['entre_eixos'], 3)} m"
           f"   ·   banco a {br(medidas['altura_do_banco'], 3)} m   ·   altura {br(medidas['altura'], 2)} m"
           f"   ·   dois lugares (piloto e garupa)", font=fonte(24), fill=(70, 70, 70))
    for i, (chave, nome) in enumerate(VISTAS):
        img = fotos[chave].copy()
        img.thumbnail((cw - 20, chh - 50))
        x = (i % 3) * cw + (cw - img.width) // 2
        y = topo + (i // 3) * chh
        folha.paste(img, (x, y))
        d.text(((i % 3) * cw + 24, y + chh - 46), nome, font=fonte(26, True), fill=(40, 40, 40))
    d.text((30, folha.height - 44), "Render: Blender 5.2, Cycles (CPU). Script: A Moto/moto.py", font=fonte(20),
           fill=(120, 120, 120))
    folha.save(os.path.join(pasta, f"Moto_{versao}_prancha.png"))
    # a comparação com a foto
    ref = Image.open(os.path.join(AQUI, "referencia", "FatBoy_2019_lateral.png")).convert("RGB")
    orto = Image.new("RGBA", ref.size, (255, 255, 255, 255))
    orto.alpha_composite(Image.open(os.path.join(pasta, "Moto_comparacao_ortho.png")).convert("RGBA"))
    orto = orto.convert("RGB")
    mistura = Image.blend(ref, orto, 0.5)
    w, h = ref.size
    esc = 0.5
    tw, th = int(w * esc), int(h * esc)
    comp = Image.new("RGB", (tw * 3 + 40, th + 90), (250, 250, 248))
    d = ImageDraw.Draw(comp)
    for i, (img, nome) in enumerate(((ref, "Foto de referência"), (orto, "Modelo (mesma escala)"),
                                     (mistura, "As duas sobrepostas"))):
        comp.paste(img.resize((tw, th), Image.LANCZOS), (10 + i * (tw + 10), 70))
        d.text((20 + i * (tw + 10), 20), nome, font=fonte(30, True), fill=(30, 30, 30))
    comp.save(os.path.join(pasta, f"Moto_{versao}_comparacao.png"))
    print("PRONTO", pasta)


if __name__ == "__main__":
    main()
