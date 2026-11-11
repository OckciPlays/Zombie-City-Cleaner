# A folha de conferência das armadilhas e das defesas (10/11/2026, a etapa 5): as fotos do armadilhas.py numa imagem só.
#
# Como rodar (fora do Blender, com o Pillow), depois do armadilhas.py:
#   py Blender/armadilhas_folha.py <pasta de saída dele> [largura da prévia]
#   (lê <pasta>/fotos; grava <pasta>/Folha_Armadilhas.png e, com a largura, uma prévia reduzida ao lado)

import os
import sys

from PIL import Image, ImageDraw, ImageFont

FUNDO = (46, 48, 52)
TEXTO = (235, 235, 235)


def _abrir(caminho, larg, alt):
    im = Image.open(caminho).convert("RGBA")
    esc = min(larg / im.width, alt / im.height)
    im = im.resize((max(1, int(im.width * esc)), max(1, int(im.height * esc))), Image.LANCZOS)
    cel = Image.new("RGBA", (larg, alt), FUNDO + (255,))
    cel.alpha_composite(im, ((larg - im.width) // 2, (alt - im.height) // 2))
    return cel.convert("RGB")


def folha(pasta, previa=None):
    f = os.path.join(pasta, "fotos")
    vb = ("frente", "34", "lado")
    linhas = [
        ("t1, o alarme de latas (o marcador vermelho é o vazio Latas)", [f"AlarmeLatas_{v}" for v in ("34", "lado", "perto")]),
        ("t2, a barricada de porta (1,265 x 2,415 m): inteira e danificada",
         [f"BarricadaPorta_{v}" for v in vb] + [f"BarricadaPorta_Danificada_{v}" for v in vb]),
        ("t2, a barricada de janela (1,38 x 1,515 m, o peitoril a 0,90 m): inteira e danificada",
         [f"BarricadaJanela_{v}" for v in vb] + [f"BarricadaJanela_Danificada_{v}" for v in vb]),
        ("t3, o tapete de pregos", [f"TapetePregos_{v}" for v in ("34", "lado", "perto")]),
        ("t4, as estacas (o marcador é o vazio Ponta)", [f"Estacas_{v}" for v in ("34", "lado", "perto")]),
        ("t6, a cerca de fogo (o marcador é o vazio Pavio; a linha de fogo de ~4 m é da Unity)",
         [f"CercaFogo_{v}" for v in ("34", "lado", "perto")]),
        ("Os itens (Loot_Armadilhas.fbx)",
         [f"Foto_Item_{n}" for n in ("AlarmeLatas", "Tabuas", "TapetePregos", "Estacas", "CercaFogo")]),
    ]
    lw, ah = 330, 235
    larg_total = 6 * lw
    alt_total = sum(ah + 26 for _ in linhas) + 8
    out = Image.new("RGB", (larg_total, alt_total), FUNDO)
    d = ImageDraw.Draw(out)
    try:
        fonte = ImageFont.truetype("arial.ttf", 16)
        miuda = ImageFont.truetype("arial.ttf", 12)
    except OSError:
        fonte = miuda = ImageFont.load_default()
    y = 4
    for titulo, nomes in linhas:
        d.text((8, y + 4), titulo, fill=TEXTO, font=fonte)
        y += 26
        for k, n in enumerate(nomes):
            a = os.path.join(f, n + ".png")
            if os.path.exists(a):
                out.paste(_abrir(a, lw - 4, ah), (k * lw + 2, y))
                d.text((k * lw + 6, y + ah - 16), n, fill=(220, 220, 170), font=miuda)
        y += ah
    caminho = os.path.join(pasta, "Folha_Armadilhas.png")
    out.save(caminho)
    print("folha:", caminho, out.size)
    if previa:
        p = out.resize((previa, int(out.height * previa / out.width)), Image.LANCZOS)
        p.save(os.path.join(pasta, "Folha_Armadilhas_previa.png"))


if __name__ == "__main__":
    folha(sys.argv[1] if len(sys.argv) > 1 else ".", int(sys.argv[2]) if len(sys.argv) > 2 else None)
