# Junta imagens numa folha (grade), pelo Blender sem janela: -- saida.png colunas larg img1 img2 ...  (larg = recorte das colunas da esquerda, 0 = inteira)
import bpy, sys, numpy as np
a = sys.argv[sys.argv.index("--") + 1:]
saida, colunas, larg, imgs = a[0], int(a[1]), int(a[2]), a[3:]
arrs = []
for p in imgs:
    im = bpy.data.images.load(p)
    w, h = im.size
    x = np.array(im.pixels[:], np.float32).reshape(h, w, 4)
    if larg:
        x = x[:, :larg]
    arrs.append(x)
    bpy.data.images.remove(im)
H = max(x.shape[0] for x in arrs); W = max(x.shape[1] for x in arrs)
linhas = (len(arrs) + colunas - 1) // colunas
out = np.zeros((linhas * H, colunas * W, 4), np.float32); out[..., 3] = 1
for i, x in enumerate(arrs):
    r, c = divmod(i, colunas)
    r = linhas - 1 - r
    out[r*H:r*H+x.shape[0], c*W:c*W+x.shape[1]] = x
im = bpy.data.images.new("f", out.shape[1], out.shape[0], alpha=True)
im.pixels.foreach_set(out.ravel())
im.filepath_raw = saida; im.file_format = "PNG"; im.save()
