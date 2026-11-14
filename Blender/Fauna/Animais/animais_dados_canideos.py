# Os canídeos (a loba, o filhote e o coiote): os pontos do esqueleto, lidos no raio-X (Renders/RaioX/<FBX>.png) e
# conferidos nas seções da malha (os laços dos cortes por z = cte nas patas e na cauda e por y = cte no corpo e no
# pescoço), no espaço normalizado do Tripo (1 m na maior medida, olhando para -Y, os pés em z = 0). Os campos são os do
# Lobo_Macho (animais_dados.py; veja o topo do animais_quadrupede.py). Carregado depois do animais_dados.py (a bicho()).
#
# A escala: a cernelha é o alto do lombo bem em cima das patas da frente (y ≈ -0,10), sem o pelo do pescoço.

# A loba: o mesmo corpo do lobo, um pouco mais baixa e mais fina (o lombo a 0,51-0,53, as patas 18% mais juntas).
bicho("Lobo_Femea", "LoboFemea", "canideo", ("lombo", 0.53, 0.72), dict(
    quadril=(0.20, 0.435),
    coluna=[(0.10, 0.44), (0.0, 0.44), (-0.09, 0.435), (-0.17, 0.42)],
    pescoco=[(-0.24, 0.48), (-0.30, 0.525)],
    focinho=(-0.50, 0.435),
    mandibula=((-0.34, 0.45), (-0.465, 0.405)),
    orelha=((0.05, -0.335, 0.59), (0.083, -0.37, 0.665)),
    cauda=[(0.335, 0.45), (0.39, 0.355), (0.432, 0.245), (0.475, 0.125)],
    d=[(0.05, -0.08, 0.48), (0.066, -0.16, 0.38), (0.068, -0.065, 0.275), (0.062, -0.108, 0.09),
       (0.063, -0.148, 0.035), (0.063, -0.212, 0.012)],
    t=[(0.065, 0.28, 0.415), (0.068, 0.205, 0.295), (0.06, 0.34, 0.16), (0.062, 0.31, 0.04), (0.063, 0.243, 0.012)],
))

# O filhote: a cabeça grande e redonda, as orelhas grandes, as patas curtas e grossas e os pés grandes.
bicho("Lobo_Filhote", "LoboFilhote", "canideo", ("lombo", 0.535, 0.42), dict(
    quadril=(0.19, 0.42),
    coluna=[(0.10, 0.425), (0.0, 0.42), (-0.08, 0.42), (-0.16, 0.42)],
    pescoco=[(-0.22, 0.465), (-0.29, 0.51)],
    focinho=(-0.50, 0.425),
    mandibula=((-0.35, 0.43), (-0.465, 0.39)),
    orelha=((0.075, -0.31, 0.60), (0.108, -0.345, 0.715)),
    cauda=[(0.33, 0.43), (0.385, 0.345), (0.43, 0.24), (0.475, 0.135)],
    d=[(0.06, -0.07, 0.47), (0.075, -0.155, 0.355), (0.08, -0.065, 0.225), (0.077, -0.112, 0.10),
       (0.082, -0.15, 0.04), (0.083, -0.228, 0.012)],
    t=[(0.07, 0.275, 0.39), (0.078, 0.185, 0.26), (0.071, 0.325, 0.145), (0.075, 0.29, 0.045), (0.081, 0.193, 0.012)],
))

# O coiote: mais magro e de patas finas, o focinho fino, as orelhas altas e a cauda caída.
bicho("Coiote", "Coiote", "canideo", ("lombo", 0.51, 0.58), dict(
    quadril=(0.19, 0.42),
    coluna=[(0.10, 0.43), (0.0, 0.42), (-0.09, 0.42), (-0.17, 0.405)],
    pescoco=[(-0.24, 0.46), (-0.30, 0.505)],
    focinho=(-0.50, 0.432),
    mandibula=((-0.345, 0.44), (-0.465, 0.40)),
    orelha=((0.05, -0.33, 0.575), (0.09, -0.37, 0.67)),
    cauda=[(0.31, 0.44), (0.385, 0.355), (0.435, 0.24), (0.475, 0.115)],
    d=[(0.045, -0.08, 0.46), (0.058, -0.17, 0.37), (0.062, -0.085, 0.26), (0.058, -0.133, 0.09),
       (0.059, -0.165, 0.035), (0.06, -0.218, 0.012)],
    t=[(0.056, 0.27, 0.395), (0.058, 0.19, 0.28), (0.055, 0.345, 0.165), (0.057, 0.318, 0.04), (0.059, 0.258, 0.012)],
))
