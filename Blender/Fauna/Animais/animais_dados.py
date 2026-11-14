# Os dados de cada bicho: o FBX, o grupo, a escala e os pontos do esqueleto (lidos no raio-X, Renders/RaioX/<FBX>.png,
# no espaço normalizado do Tripo: 1 m na maior medida, olhando para -Y, os pés em z = 0).
#
# A escala: "lombo" = (a altura da cernelha no FBX, a altura de verdade em metros; a tabela do Docs/20 do Pedro), ou
# "escala" = o fator direto (as aves e o sapo: pelo comprimento e pela envergadura).

MODELOS = {}


def bicho(nome, fbx, grupo, escala, P):
    MODELOS[nome] = dict(nome=nome, fbx=fbx, grupo=grupo, escala=escala, P=P)


# ---------------------------------------------------------------------------------------------------------------------
# Os canídeos
# ---------------------------------------------------------------------------------------------------------------------

bicho("Lobo_Macho", "LoboMacho", "canideo", ("lombo", 0.565, 0.80), dict(
    quadril=(0.20, 0.47),
    coluna=[(0.10, 0.47), (0.0, 0.47), (-0.09, 0.46), (-0.17, 0.44)],
    pescoco=[(-0.24, 0.50), (-0.30, 0.54)],
    focinho=(-0.50, 0.44),
    mandibula=((-0.34, 0.46), (-0.47, 0.41)),
    orelha=((0.05, -0.33, 0.60), (0.08, -0.37, 0.69)),
    cauda=[(0.34, 0.48), (0.40, 0.38), (0.45, 0.26), (0.49, 0.13)],
    d=[(0.06, -0.08, 0.53), (0.08, -0.16, 0.40), (0.085, -0.07, 0.29), (0.08, -0.11, 0.09), (0.078, -0.15, 0.035),
       (0.077, -0.215, 0.012)],
    t=[(0.075, 0.27, 0.45), (0.08, 0.21, 0.31), (0.08, 0.335, 0.18), (0.08, 0.30, 0.04), (0.08, 0.225, 0.012)],
))
