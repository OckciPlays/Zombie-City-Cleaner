#!/bin/bash
# Constrói a versão e assa os 10 passes (5 canais x 2 atlas), um processo por vez, e compõe/exporta.
V=${1:-v1}
B="/d/SteamLibrary/steamapps/common/Blender/blender.exe"
export OCO_CPU=1
cd "D:/teste blender/A Caminhonete"
LOG="assar_$V.log"
: > "$LOG"
"$B" -b --factory-startup --python rodar.py -- $V construir >> "$LOG" 2>&1 || { echo "FALHOU construir" >> "$LOG"; exit 1; }
for canal in cor aspereza metal normal ao; do
  for atlas in A B; do
    ok=0
    for tentativa in 1 2 3; do
      if "$B" -b --factory-startup "Caminhonete_$V.blend" --python rodar.py -- $V assar $canal $atlas >> "$LOG" 2>&1; then ok=1; break; fi
      echo "repetindo $canal $atlas ($tentativa)" >> "$LOG"
    done
    [ $ok = 1 ] || { echo "FALHOU $canal $atlas" >> "$LOG"; exit 1; }
  done
done
"$B" -b --factory-startup "Caminhonete_$V.blend" --python rodar.py -- $V compor_exportar >> "$LOG" 2>&1 || { echo "FALHOU compor" >> "$LOG"; exit 1; }
echo "TUDO PRONTO $V" >> "$LOG"
