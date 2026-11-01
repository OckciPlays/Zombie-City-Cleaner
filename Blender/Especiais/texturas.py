# As texturas dos zumbis especiais para o jogo (o Python comum, com o PIL; o especiais.py faz o resto no Blender):
# <Tipo>_V<n>_BaseColor.png, _Normal.png e _MetalLiso.png (o metal em R, 0, e a lisura, 1 - a rugosidade, no alfa), em
# Assets/_Game/Art/Characters/Zombie/Especiais. No Brutamonte, a íris (textura própria) entra numa faixa de 256 px
# embaixo do atlas, onde o especiais.py pôs os UVs do globo do olho.
#
# Rodar: py texturas.py

import os
import sys

from PIL import Image

AQUI = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(AQUI))
ARTE = r"D:\teste blender"
SAIDA = os.path.join(REPO, "Assets", "_Game", "Art", "Characters", "Zombie", "Especiais")
FAIXA = 256

FONTES = {
    "Brutamonte": ("O Brutamonte", "Brutamonte", [("V1", r"export\Brutamonte.fbm"), ("V2", r"Variantes\V2_Regata\export\Brutamonte_V2.fbm"),
                                                  ("V3", r"Variantes\V3_Xadrez\export\Brutamonte_V3.fbm")]),
    "Long": ("O Guspidor", "Guspidor", [("V1", r"export\Guspidor.fbm"), ("V2", r"Variantes\V2_Camisa_Aberta\export\Guspidor_V2.fbm"),
                                        ("V3", r"Variantes\V3_Regata\export\Guspidor_V3.fbm")]),
    "Corredor": ("O Corredor v2", "Corredor", [("V1", r"export\Corredor.fbm"), ("V2", r"Variantes\V2_Manga_Longa\export\Corredor_V2.fbm"),
                                               ("V3", r"Variantes\V3_Regata\export\Corredor_V3.fbm")]),
}


def com_faixa(img, faixa):
    """A imagem com a faixa embaixo (no UV, a faixa é o v de 0 a 256/altura)."""
    w, h = img.size
    out = Image.new(img.mode, (w, h + FAIXA))
    out.paste(img, (0, 0))
    out.paste(faixa, (0, h))
    return out


def fazer(tipo, var, pasta, prefixo, fbm):
    origem = os.path.join(ARTE, pasta, fbm)
    nome = f"{tipo}_{var}"
    cor = Image.open(os.path.join(origem, f"{prefixo}_BaseColor.png")).convert("RGB")
    nor = Image.open(os.path.join(origem, f"{prefixo}_Normal.png")).convert("RGB")
    rug = Image.open(os.path.join(origem, f"{prefixo}_Roughness.png")).convert("L")
    if tipo == "Brutamonte":
        w = cor.size[0]
        iris = Image.open(os.path.join(origem, f"{prefixo}_Olho_Iris.png")).convert("RGB").resize((FAIXA, FAIXA), Image.LANCZOS)
        f = Image.new("RGB", (w, FAIXA), (30, 6, 6))
        f.paste(iris, (0, 0))
        cor = com_faixa(cor, f)
        nor = com_faixa(nor, Image.new("RGB", (w, FAIXA), (128, 128, 255)))
        rug = com_faixa(rug, Image.new("L", (w, FAIXA), 40))   # o olho molhado brilha
    cor.save(os.path.join(SAIDA, f"{nome}_BaseColor.png"))
    nor.save(os.path.join(SAIDA, f"{nome}_Normal.png"))
    liso = Image.eval(rug, lambda v: 255 - v)
    zero = Image.new("L", rug.size, 0)
    Image.merge("RGBA", (zero, zero, zero, liso)).save(os.path.join(SAIDA, f"{nome}_MetalLiso.png"))
    print("texturas:", nome, cor.size)


if __name__ == "__main__":
    so = sys.argv[1:]
    os.makedirs(SAIDA, exist_ok=True)
    for tipo, (pasta, prefixo, lista) in FONTES.items():
        for var, fbm in lista:
            if so and tipo not in so and f"{tipo}_{var}" not in so:
                continue
            fazer(tipo, var, pasta, prefixo, fbm)
