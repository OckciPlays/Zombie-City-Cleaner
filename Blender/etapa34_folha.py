# A folha de conferência das etapas 3 e 4 das armas de sucata (10/11/2026): as fotos do protecao_sucata.py, do fogo.py,
# do loot_etapa34.py e dos braços de primeira pessoa (lampiao.py e molotov.py, a etapa "conferir") numa imagem só.
#
# Como rodar (fora do Blender, com o Pillow), depois dos outros:
#   py Blender/etapa34_folha.py <pasta de saída deles>
#   (lê <pasta>/fotos e <pasta>/fotos_fp; grava <pasta>/Folha_Etapa34.png)

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


def folha(pasta):
    f, fp = os.path.join(pasta, "fotos"), os.path.join(pasta, "fotos_fp")
    linhas = [
        ("A proteção no homem: sem jaqueta e com jaqueta + colete tático (de frente, de lado, na guarda, agachado)",
         [os.path.join(f, f"protecao_homem_{r}_{v}.png") for r in ("leve", "jaqueta") for v in ("frente", "lado", "guarda", "agachado")], 210, 260),
        ("A proteção na mulher (a forma Mulher das peças)",
         [os.path.join(f, f"protecao_mulher_{r}_{v}.png") for r in ("leve", "jaqueta") for v in ("frente", "lado", "guarda", "agachado")], 210, 260),
        ("De perto: o capacete, as costas, a braçadeira na guarda e a caneleira agachado",
         [os.path.join(f, f"protecao_{r}_{v}.png") for r in ("homem_jaqueta", "mulher_leve") for v in ("cabeca", "costas", "braco", "perna")], 210, 210),
        ("Os itens (Loot_Etapa34.fbx)",
         [os.path.join(f, f"Foto_Item_{n}.png") for n in ("Capacete", "Bracadeiras", "Caneleiras", "Colete", "Lampiao", "Molotov", "KitFogueira")], 240, 200),
        ("O Fogo.fbx (na mão e no mundo) e a mão do corpo (as poses da tocha: segurar e golpe)",
         [os.path.join(f, n) for n in ("Foto_Lampiao.png", "Foto_Molotov.png", "Foto_Fogueira_Portatil.png",
                                      "corpo_lampiao_tocha_segurar_frente.png", "corpo_lampiao_tocha_segurar_lado.png",
                                      "corpo_molotov_tocha_segurar_frente.png", "corpo_molotov_tocha_golpe_lado.png",
                                      "corpo_lampiao_tocha_golpe_frente.png")], 210, 220),
        ("A primeira pessoa: o lampião (Idle, de lado, o soco e o saque)",
         [os.path.join(fp, n) for n in ("lampiao_fp_idle_1.png", "lampiao_fp_idle_lado.png", "lampiao_fp_socodireito_3.png",
                                       "lampiao_fp_sacar_7.png")], 420, 236),
        ("O molotov: o Idle e o arremesso (o SocoDireito, quadros 3, 5, 6, 7; solta no 8; 9 e 11)",
         [os.path.join(fp, f"molotov_fp_{n}.png") for n in ("idle_1", "idle_lado", "socodireito_3", "socodireito_5")], 420, 236),
        ("",
         [os.path.join(fp, f"molotov_fp_socodireito_{q}.png") for q in (6, 7, 8, 9, 11)], 336, 189),
    ]
    larg_total = max(len(fs) * lw for _, fs, lw, _ in linhas)
    alt_total = sum(ah + (22 if t else 4) for t, _, _, ah in linhas) + 8
    out = Image.new("RGB", (larg_total, alt_total), FUNDO)
    d = ImageDraw.Draw(out)
    try:
        fonte = ImageFont.truetype("arial.ttf", 15)
        miuda = ImageFont.truetype("arial.ttf", 11)
    except OSError:
        fonte = miuda = ImageFont.load_default()
    y = 4
    for titulo, arqs, lw, ah in linhas:
        if titulo:
            d.text((8, y + 3), titulo, fill=TEXTO, font=fonte)
            y += 22
        for k, a in enumerate(arqs):
            if os.path.exists(a):
                out.paste(_abrir(a, lw - 4, ah), (k * lw + 2, y))
                d.text((k * lw + 6, y + ah - 14), os.path.basename(a)[:-4], fill=(200, 200, 160), font=miuda)
        y += ah + 4
    caminho = os.path.join(pasta, "Folha_Etapa34.png")
    out.save(caminho)
    print("folha:", caminho, out.size)


if __name__ == "__main__":
    folha(sys.argv[1] if len(sys.argv) > 1 else ".")
