# A sniper v3 leve (30/10/2026, a pedido: "8500 triângulos está um pouco pesada demais, será que conseguimos abaixar um
# pouco?"): a mesma Mosin com a luneta "scout" da v3 (v3_mosin.py), com metade dos triângulos.
# - a luneta: as mesmas peças nos mesmos lugares, com 16 lados nos tubos (eram 24), 10 nas torres (eram 20), 6 nos
#   parafusos (eram 10), o chanfro só nos aros (não nas arestas compridas) e as fendas do trilho sem chanfro;
# - a Mosin: cada peça da fonte passa pelo Decimate (por colapso) na fração de DECIMAR, antes de tudo.
# A Sniper3 (v3_mosin.py, Sniper3.blend, Sniper3.fbx) fica como estava.
#
# Como rodar: blender -b --factory-startup --disable-autoexec --python Blender/Armas/rodar_v2.py -- v3_mosin_leve tudo

from v3_mosin import *  # noqa: F401,F403  (as constantes e o construir da v3)

NOME = "Sniper3Leve"
FOTO = "sniper_v3_leve"
LUNETA_LEVE = True
_P = "mosin9130 "
DECIMAR = {
    _P + "stock": 0.5,
    _P + "receiver": 0.6,
    _P + "barrel": 0.4,
    _P + "handguard upper": 0.6,
    _P + "bolt head": 0.5,
    _P + "bolt body": 0.6,
}
