# Benchmark dos zumbis

## 23/10/2026 09:35 — Parte 5, cidade em blocos

- Editor 6000.6.5f1, 1918×887
- AMD Ryzen 7 5700X 8-Core Processor  (16 núcleos), AMD Radeon RX 9060 XT
- Zumbis espalhados pela cidade, 10 s de medida para cada quantidade, sem vsync. Jogadores na partida: 3.

| Zumbis | Perto / médio / longe | Quadro médio (ms) | Quadro p95 (ms) | Thread principal (ms) | IA + LOD (ms) | Rede (ms) | Animação (ms) | Navegação (ms) | Física (ms) | Draw calls | Rede (KB/s por cliente) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 50 | 4 / 15 / 29 | 4,4 | 5,6 | 4,4 | 0,00 | 0,00 | 0,08 | 0,11 | 0,03 | 2459 | 0,6 |
| 100 | 4 / 33 / 62 | 4,6 | 5,8 | 4,6 | 0,01 | 0,00 | 0,09 | 0,16 | 0,03 | 2457 | 1,7 |
| 250 | 10 / 67 / 171 | 5,1 | 6,4 | 5,1 | 0,02 | 0,01 | 0,14 | 0,25 | 0,05 | 2480 | 3,1 |
| 500 | 44 / 160 / 295 | 5,8 | 6,8 | 5,8 | 0,04 | 0,02 | 0,28 | 0,38 | 0,09 | 2523 | 8,3 |
## 23/10/2026 13:08 — Parte 8, cidade de arte, antes de otimizar (noite)

- Editor 6000.6.5f1, 1918×887
- AMD Ryzen 7 5700X 8-Core Processor  (16 núcleos), AMD Radeon RX 9060 XT
- Zumbis espalhados pela cidade, 10 s de medida para cada quantidade, sem vsync. Jogadores na partida: 1. Câmera em (-6, 2, -9), olhando (0.6, 0.0, 0.8).

| Zumbis | Perto / médio / longe | Quadro médio (ms) | Quadro p95 (ms) | Thread principal (ms) | GPU (ms) | IA + LOD (ms) | Rede (ms) | Animação (ms) | Navegação (ms) | Física (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Rede (KB/s por cliente) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 50 | 1 / 20 / 29 | 4,3 | 5,0 | 4,3 | 2,4 | 0,00 | 0,00 | 0,09 | 0,09 | 0,02 | 983 | 0 | 67 | 507 | 0,0 |
| 100 | 3 / 22 / 74 | 4,7 | 5,7 | 4,7 | 2,5 | 0,01 | 0,00 | 0,12 | 0,10 | 0,03 | 996 | 0 | 68 | 587 | 0,0 |
| 250 | 7 / 64 / 178 | 5,1 | 6,0 | 5,1 | 2,6 | 0,02 | 0,00 | 0,24 | 0,18 | 0,03 | 1048 | 0 | 67 | 915 | 0,0 |
| 500 | 17 / 138 / 343 | 5,9 | 6,8 | 5,9 | 3,0 | 0,03 | 0,00 | 0,44 | 0,28 | 0,06 | 1109 | 0 | 68 | 1309 | 0,0 |

## 23/10/2026 13:11 — Parte 8, com oclusão e neblina rala dentro de casa (noite)

- Editor 6000.6.5f1, 1918×887
- AMD Ryzen 7 5700X 8-Core Processor  (16 núcleos), AMD Radeon RX 9060 XT
- Zumbis espalhados pela cidade, 10 s de medida para cada quantidade, sem vsync. Jogadores na partida: 1. Câmera em (-6, 2, -9), olhando (0.6, 0.0, 0.8).

| Zumbis | Perto / médio / longe | Quadro médio (ms) | Quadro p95 (ms) | Thread principal (ms) | GPU (ms) | IA + LOD (ms) | Rede (ms) | Animação (ms) | Navegação (ms) | Física (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Rede (KB/s por cliente) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 50 | 1 / 20 / 29 | 4,6 | 5,9 | 4,6 | 2,4 | 0,00 | 0,00 | 0,09 | 0,09 | 0,02 | 939 | 0 | 63 | 461 | 0,0 |
| 100 | 1 / 32 / 65 | 5,2 | 7,3 | 5,2 | 2,5 | 0,01 | 0,00 | 0,13 | 0,13 | 0,03 | 953 | 0 | 64 | 551 | 0,0 |
| 250 | 13 / 59 / 176 | 5,5 | 6,5 | 5,5 | 2,7 | 0,02 | 0,00 | 0,23 | 0,20 | 0,04 | 998 | 0 | 64 | 828 | 0,0 |
| 500 | 15 / 129 / 355 | 6,2 | 7,5 | 6,2 | 2,6 | 0,03 | 0,00 | 0,40 | 0,28 | 0,06 | 1066 | 0 | 63 | 1209 | 0,0 |

## 23/10/2026 14:42 — ajustes antes da Parte 7: 4 modelos de zumbi e ragdoll (noite)

- Editor 6000.6.5f1, 1918×887
- AMD Ryzen 7 5700X 8-Core Processor  (16 núcleos), AMD Radeon RX 9060 XT
- Zumbis espalhados pela cidade, 10 s de medida para cada quantidade, sem vsync. Jogadores na partida: 1. Câmera em (-6, 2, -9), olhando (0.6, 0.0, 0.8).

| Zumbis | Perto / médio / longe | Quadro médio (ms) | Quadro p95 (ms) | Thread principal (ms) | GPU (ms) | IA + LOD (ms) | Rede (ms) | Animação (ms) | Navegação (ms) | Física (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Rede (KB/s por cliente) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 50 | 0 / 18 / 32 | 3,7 | 4,6 | 3,7 | 2,5 | 0,00 | 0,00 | 0,07 | 0,08 | 0,03 | 623 | 0 | 58 | 299 | 0,0 |
| 100 | 0 / 28 / 72 | 3,8 | 4,4 | 3,8 | 2,5 | 0,00 | 0,00 | 0,05 | 0,10 | 0,03 | 617 | 0 | 58 | 265 | 0,0 |
| 250 | 3 / 62 / 184 | 4,2 | 4,7 | 4,2 | 2,1 | 0,01 | 0,00 | 0,12 | 0,17 | 0,04 | 655 | 0 | 60 | 497 | 0,0 |
| 500 | 18 / 110 / 371 | 4,9 | 5,5 | 4,9 | 2,1 | 0,03 | 0,00 | 0,23 | 0,23 | 0,07 | 695 | 0 | 60 | 784 | 0,0 |

## 23/10/2026 20:10 — ambientação: antes (noite, 2560×1440)

- Editor 6000.6.5f1, 2560×1440
- 11th Gen Intel(R) Core(TM) i5-11600KF @ 3.90GHz (12 núcleos), AMD Radeon RX 6750 XT
- Zumbis espalhados pela cidade, 10 s de medida para cada quantidade, sem vsync. Jogadores na partida: 1. Câmera em (-6, 2, -9), olhando (0.6, 0.0, 0.8).

| Zumbis | Perto / médio / longe | Quadro médio (ms) | Quadro p95 (ms) | Thread principal (ms) | GPU (ms) | IA + LOD (ms) | Rede (ms) | Animação (ms) | Navegação (ms) | Física (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Rede (KB/s por cliente) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 50 | 3 / 13 / 34 | 12,6 | 13,8 | 12,6 | 8,4 | 0,01 | 0,00 | 0,10 | 0,11 | 0,11 | 864 | 0 | 65 | 457 | 0,0 |
| 100 | 4 / 27 / 68 | 12,9 | 14,1 | 12,8 | 8,4 | 0,01 | 0,00 | 0,13 | 0,14 | 0,12 | 875 | 0 | 65 | 524 | 0,0 |
| 250 | 10 / 60 / 179 | 13,0 | 14,3 | 13,0 | 9,0 | 0,03 | 0,00 | 0,21 | 0,21 | 0,17 | 915 | 0 | 64 | 751 | 0,0 |
| 500 | 12 / 124 / 362 | 13,4 | 14,3 | 13,4 | 9,9 | 0,05 | 0,00 | 0,38 | 0,28 | 0,20 | 993 | 0 | 61 | 1147 | 0,0 |

## 23/10/2026 20:11 — ambientação: antes, 2ª rodada (noite, 2560×1440)

- Editor 6000.6.5f1, 2560×1440
- 11th Gen Intel(R) Core(TM) i5-11600KF @ 3.90GHz (12 núcleos), AMD Radeon RX 6750 XT
- Zumbis espalhados pela cidade, 10 s de medida para cada quantidade, sem vsync. Jogadores na partida: 1. Câmera em (-6, 2, -9), olhando (0.6, 0.0, 0.8).

| Zumbis | Perto / médio / longe | Quadro médio (ms) | Quadro p95 (ms) | Thread principal (ms) | GPU (ms) | IA + LOD (ms) | Rede (ms) | Animação (ms) | Navegação (ms) | Física (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Rede (KB/s por cliente) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 50 | 0 / 15 / 34 | 13,4 | 14,0 | 13,4 | 9,1 | 0,01 | 0,00 | 0,08 | 0,10 | 0,10 | 853 | 0 | 65 | 388 | 0,0 |
| 100 | 4 / 18 / 78 | 13,4 | 14,1 | 13,4 | 9,3 | 0,01 | 0,00 | 0,10 | 0,12 | 0,11 | 865 | 0 | 64 | 463 | 0,0 |
| 250 | 6 / 61 / 182 | 13,4 | 14,0 | 13,4 | 9,4 | 0,02 | 0,00 | 0,19 | 0,18 | 0,13 | 899 | 0 | 64 | 659 | 0,0 |
| 500 | 10 / 144 / 344 | 13,4 | 14,1 | 13,4 | 9,4 | 0,05 | 0,00 | 0,37 | 0,29 | 0,20 | 965 | 0 | 64 | 1072 | 0,0 |

## 23/10/2026 20:22 — ambientação: depois da fatia 1, rua abandonada (noite, 2560×1440)

- Editor 6000.6.5f1, 2560×1440
- 11th Gen Intel(R) Core(TM) i5-11600KF @ 3.90GHz (12 núcleos), AMD Radeon RX 6750 XT
- Zumbis espalhados pela cidade, 10 s de medida para cada quantidade, sem vsync. Jogadores na partida: 1. Câmera em (-6, 2, -9), olhando (0.6, 0.0, 0.8).

| Zumbis | Perto / médio / longe | Quadro médio (ms) | Quadro p95 (ms) | Thread principal (ms) | GPU (ms) | IA + LOD (ms) | Rede (ms) | Animação (ms) | Navegação (ms) | Física (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Rede (KB/s por cliente) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 50 | 1 / 19 / 30 | 13,1 | 14,0 | 13,0 | 8,9 | 0,01 | 0,00 | 0,07 | 0,11 | 0,09 | 901 | 0 | 65 | 376 | 0,0 |
| 100 | 4 / 26 / 69 | 13,3 | 14,0 | 13,3 | 8,9 | 0,01 | 0,00 | 0,12 | 0,14 | 0,11 | 927 | 0 | 65 | 540 | 0,0 |
| 250 | 10 / 71 / 168 | 13,4 | 14,0 | 13,4 | 9,1 | 0,03 | 0,00 | 0,18 | 0,20 | 0,16 | 957 | 0 | 65 | 717 | 0,0 |
| 500 | 12 / 145 / 341 | 13,4 | 14,1 | 13,4 | 9,1 | 0,05 | 0,00 | 0,38 | 0,29 | 0,20 | 1049 | 0 | 64 | 1262 | 0,0 |

## 23/10/2026 20:26 — ambientação: depois da fatia 1, 2ª rodada (noite, 2560×1440)

- Editor 6000.6.5f1, 2560×1440
- 11th Gen Intel(R) Core(TM) i5-11600KF @ 3.90GHz (12 núcleos), AMD Radeon RX 6750 XT
- Zumbis espalhados pela cidade, 10 s de medida para cada quantidade, sem vsync. Jogadores na partida: 1. Câmera em (-6, 2, -9), olhando (0.6, 0.0, 0.8).

| Zumbis | Perto / médio / longe | Quadro médio (ms) | Quadro p95 (ms) | Thread principal (ms) | GPU (ms) | IA + LOD (ms) | Rede (ms) | Animação (ms) | Navegação (ms) | Física (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Rede (KB/s por cliente) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 50 | 2 / 13 / 35 | 13,0 | 14,0 | 12,9 | 8,7 | 0,01 | 0,00 | 0,07 | 0,10 | 0,10 | 906 | 0 | 64 | 407 | 0,0 |
| 100 | 1 / 35 / 64 | 12,9 | 13,9 | 12,8 | 8,4 | 0,01 | 0,00 | 0,13 | 0,14 | 0,11 | 923 | 0 | 64 | 507 | 0,0 |
| 250 | 7 / 63 / 179 | 12,8 | 13,8 | 12,7 | 8,3 | 0,03 | 0,00 | 0,26 | 0,21 | 0,15 | 979 | 0 | 65 | 835 | 0,0 |
| 500 | 15 / 143 / 341 | 12,7 | 13,9 | 12,7 | 8,3 | 0,06 | 0,00 | 0,41 | 0,30 | 0,20 | 1031 | 0 | 65 | 1169 | 0,0 |

## 23/10/2026 21:02 — ambientação: depois da fatia 2 e da lanterna nova (noite, 2560×1440)

- Editor 6000.6.5f1, 2560×1440
- 11th Gen Intel(R) Core(TM) i5-11600KF @ 3.90GHz (12 núcleos), AMD Radeon RX 6750 XT
- Zumbis espalhados pela cidade, 10 s de medida para cada quantidade, sem vsync. Jogadores na partida: 1. Câmera em (-6, 2, -9), olhando (0.6, 0.0, 0.8).

| Zumbis | Perto / médio / longe | Quadro médio (ms) | Quadro p95 (ms) | Thread principal (ms) | GPU (ms) | IA + LOD (ms) | Rede (ms) | Animação (ms) | Navegação (ms) | Física (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Rede (KB/s por cliente) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 50 | 0 / 17 / 32 | 13,4 | 14,1 | 13,4 | 9,0 | 0,01 | 0,00 | 0,08 | 0,11 | 0,10 | 922 | 0 | 73 | 400 | 0,0 |
| 100 | 4 / 31 / 64 | 13,4 | 14,2 | 13,4 | 8,6 | 0,02 | 0,00 | 0,13 | 0,16 | 0,13 | 947 | 0 | 75 | 556 | 0,0 |
| 250 | 10 / 65 / 174 | 13,4 | 14,5 | 13,4 | 8,7 | 0,04 | 0,00 | 0,18 | 0,22 | 0,18 | 965 | 0 | 76 | 659 | 0,0 |
| 500 | 9 / 134 / 356 | 13,4 | 14,1 | 13,4 | 9,1 | 0,04 | 0,00 | 0,31 | 0,28 | 0,19 | 1009 | 0 | 75 | 934 | 0,0 |

## 23/10/2026 21:03 — ambientação: depois da fatia 2 e da lanterna nova, 2ª rodada (noite, 2560×1440)

- Editor 6000.6.5f1, 2560×1440
- 11th Gen Intel(R) Core(TM) i5-11600KF @ 3.90GHz (12 núcleos), AMD Radeon RX 6750 XT
- Zumbis espalhados pela cidade, 10 s de medida para cada quantidade, sem vsync. Jogadores na partida: 1. Câmera em (-6, 2, -9), olhando (0.6, 0.0, 0.8).

| Zumbis | Perto / médio / longe | Quadro médio (ms) | Quadro p95 (ms) | Thread principal (ms) | GPU (ms) | IA + LOD (ms) | Rede (ms) | Animação (ms) | Navegação (ms) | Física (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Rede (KB/s por cliente) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 50 | 3 / 11 / 36 | 13,4 | 14,1 | 13,4 | 8,7 | 0,01 | 0,00 | 0,08 | 0,10 | 0,11 | 926 | 0 | 72 | 425 | 0,0 |
| 100 | 2 / 33 / 64 | 13,4 | 14,1 | 13,4 | 9,0 | 0,01 | 0,00 | 0,14 | 0,16 | 0,12 | 955 | 0 | 75 | 603 | 0,0 |
| 250 | 9 / 65 / 174 | 13,4 | 14,2 | 13,4 | 9,3 | 0,03 | 0,00 | 0,21 | 0,21 | 0,17 | 975 | 0 | 76 | 733 | 0,0 |
| 500 | 10 / 145 / 343 | 13,4 | 14,2 | 13,4 | 9,0 | 0,05 | 0,00 | 0,40 | 0,31 | 0,21 | 1053 | 0 | 76 | 1201 | 0,0 |

## 23/10/2026 21:07 — ambientação: fatia 2 + lanterna, pisca sem MaterialPropertyBlock (noite, 2560×1440)

- Editor 6000.6.5f1, 2560×1440
- 11th Gen Intel(R) Core(TM) i5-11600KF @ 3.90GHz (12 núcleos), AMD Radeon RX 6750 XT
- Zumbis espalhados pela cidade, 10 s de medida para cada quantidade, sem vsync. Jogadores na partida: 1. Câmera em (-6, 2, -9), olhando (0.6, 0.0, 0.8).

| Zumbis | Perto / médio / longe | Quadro médio (ms) | Quadro p95 (ms) | Thread principal (ms) | GPU (ms) | IA + LOD (ms) | Rede (ms) | Animação (ms) | Navegação (ms) | Física (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Rede (KB/s por cliente) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 50 | 1 / 12 / 37 | 13,4 | 14,1 | 13,4 | 9,1 | 0,01 | 0,00 | 0,07 | 0,10 | 0,09 | 913 | 0 | 66 | 392 | 0,0 |
| 100 | 1 / 34 / 63 | 13,1 | 14,1 | 13,1 | 9,0 | 0,01 | 0,00 | 0,11 | 0,16 | 0,11 | 928 | 0 | 67 | 482 | 0,0 |
| 250 | 4 / 66 / 178 | 13,4 | 14,3 | 13,4 | 9,4 | 0,03 | 0,00 | 0,16 | 0,19 | 0,13 | 945 | 0 | 67 | 626 | 0,0 |
| 500 | 12 / 134 / 352 | 13,2 | 14,2 | 13,2 | 9,6 | 0,05 | 0,00 | 0,38 | 0,29 | 0,21 | 1045 | 0 | 67 | 1182 | 0,0 |

## 23/10/2026 21:08 — ambientação: fatia 2 + lanterna, pisca sem MaterialPropertyBlock, 2ª rodada (noite, 2560×1440)

- Editor 6000.6.5f1, 2560×1440
- 11th Gen Intel(R) Core(TM) i5-11600KF @ 3.90GHz (12 núcleos), AMD Radeon RX 6750 XT
- Zumbis espalhados pela cidade, 10 s de medida para cada quantidade, sem vsync. Jogadores na partida: 1. Câmera em (-6, 2, -9), olhando (0.6, 0.0, 0.8).

| Zumbis | Perto / médio / longe | Quadro médio (ms) | Quadro p95 (ms) | Thread principal (ms) | GPU (ms) | IA + LOD (ms) | Rede (ms) | Animação (ms) | Navegação (ms) | Física (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Rede (KB/s por cliente) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 50 | 2 / 22 / 26 | 13,1 | 14,0 | 13,1 | 8,8 | 0,01 | 0,00 | 0,11 | 0,12 | 0,11 | 929 | 0 | 67 | 490 | 0,0 |
| 100 | 5 / 31 / 63 | 13,3 | 14,1 | 13,2 | 8,9 | 0,01 | 0,00 | 0,11 | 0,15 | 0,13 | 932 | 0 | 67 | 515 | 0,0 |
| 250 | 14 / 61 / 173 | 13,3 | 14,2 | 13,2 | 9,0 | 0,03 | 0,00 | 0,23 | 0,20 | 0,18 | 979 | 0 | 67 | 783 | 0,0 |
| 500 | 14 / 143 / 341 | 13,1 | 15,3 | 13,1 | 8,5 | 0,08 | 0,00 | 0,45 | 0,36 | 0,26 | 1042 | 0 | 68 | 1149 | 0,0 |

## 23/10/2026 21:15 — ambientação: fatia 2 + lanterna, versão final (pisca Unlit) (noite, 2560×1440)

> **Rodada contaminada:** durante ela foram tiradas fotos de teste (câmeras extras desenhando), o que pesa na GPU. Não use para comparar; veja as duas rodadas seguintes.

- Editor 6000.6.5f1, 2560×1440
- 11th Gen Intel(R) Core(TM) i5-11600KF @ 3.90GHz (12 núcleos), AMD Radeon RX 6750 XT
- Zumbis espalhados pela cidade, 10 s de medida para cada quantidade, sem vsync. Jogadores na partida: 1. Câmera em (-6, 2, -9), olhando (0.6, 0.0, 0.8).

| Zumbis | Perto / médio / longe | Quadro médio (ms) | Quadro p95 (ms) | Thread principal (ms) | GPU (ms) | IA + LOD (ms) | Rede (ms) | Animação (ms) | Navegação (ms) | Física (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Rede (KB/s por cliente) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 50 | 3 / 7 / 39 | 13,1 | 14,5 | 13,1 | 10,0 | 0,01 | 0,00 | 0,08 | 0,11 | 0,12 | 912 | 0 | 63 | 419 | 0,0 |
| 100 | 1 / 25 / 72 | 13,2 | 14,1 | 13,2 | 10,4 | 0,01 | 0,00 | 0,09 | 0,14 | 0,11 | 913 | 0 | 68 | 433 | 0,0 |
| 250 | 8 / 68 / 173 | 13,3 | 14,4 | 13,2 | 9,9 | 0,03 | 0,00 | 0,23 | 0,22 | 0,17 | 967 | 0 | 67 | 773 | 0,0 |
| 500 | 9 / 147 / 342 | 13,0 | 14,1 | 13,0 | 9,6 | 0,05 | 0,00 | 0,37 | 0,30 | 0,22 | 1025 | 0 | 67 | 1096 | 0,0 |

## 23/10/2026 21:21 — ambientação: fatia 2 + lanterna, final (noite, 2560×1440)

- Editor 6000.6.5f1, 2560×1440
- 11th Gen Intel(R) Core(TM) i5-11600KF @ 3.90GHz (12 núcleos), AMD Radeon RX 6750 XT
- Zumbis espalhados pela cidade, 10 s de medida para cada quantidade, sem vsync. Jogadores na partida: 1. Câmera em (-6, 2, -9), olhando (0.6, 0.0, 0.8).

| Zumbis | Perto / médio / longe | Quadro médio (ms) | Quadro p95 (ms) | Thread principal (ms) | GPU (ms) | IA + LOD (ms) | Rede (ms) | Animação (ms) | Navegação (ms) | Física (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Rede (KB/s por cliente) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 50 | 4 / 13 / 33 | 12,8 | 14,0 | 12,8 | 8,5 | 0,01 | 0,00 | 0,09 | 0,11 | 0,11 | 914 | 0 | 67 | 450 | 0,0 |
| 100 | 3 / 28 / 69 | 12,8 | 13,9 | 12,8 | 8,7 | 0,01 | 0,00 | 0,11 | 0,15 | 0,11 | 926 | 0 | 68 | 522 | 0,0 |
| 250 | 14 / 63 / 171 | 12,9 | 14,0 | 12,9 | 8,8 | 0,03 | 0,00 | 0,24 | 0,21 | 0,18 | 977 | 0 | 68 | 826 | 0,0 |
| 500 | 20 / 145 / 333 | 13,2 | 14,3 | 13,1 | 8,7 | 0,06 | 0,00 | 0,39 | 0,32 | 0,21 | 1043 | 0 | 67 | 1151 | 0,0 |

## 23/10/2026 21:22 — ambientação: fatia 2 + lanterna, final, 2ª rodada (noite, 2560×1440)

- Editor 6000.6.5f1, 2560×1440
- 11th Gen Intel(R) Core(TM) i5-11600KF @ 3.90GHz (12 núcleos), AMD Radeon RX 6750 XT
- Zumbis espalhados pela cidade, 10 s de medida para cada quantidade, sem vsync. Jogadores na partida: 1. Câmera em (-6, 2, -9), olhando (0.6, 0.0, 0.8).

| Zumbis | Perto / médio / longe | Quadro médio (ms) | Quadro p95 (ms) | Thread principal (ms) | GPU (ms) | IA + LOD (ms) | Rede (ms) | Animação (ms) | Navegação (ms) | Física (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Rede (KB/s por cliente) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 50 | 3 / 12 / 35 | 12,9 | 14,0 | 12,9 | 9,0 | 0,01 | 0,00 | 0,11 | 0,11 | 0,11 | 923 | 0 | 67 | 496 | 0,0 |
| 100 | 3 / 26 / 70 | 12,9 | 14,2 | 12,9 | 9,4 | 0,01 | 0,00 | 0,13 | 0,14 | 0,12 | 932 | 0 | 67 | 551 | 0,0 |
| 250 | 6 / 61 / 181 | 13,3 | 14,3 | 13,3 | 8,9 | 0,03 | 0,00 | 0,19 | 0,21 | 0,16 | 952 | 0 | 67 | 679 | 0,0 |
| 500 | 17 / 143 / 338 | 12,9 | 14,2 | 12,9 | 8,6 | 0,06 | 0,00 | 0,42 | 0,31 | 0,21 | 1032 | 0 | 67 | 1208 | 0,0 |

## 23/10/2026 23:26 — armas rebalanceadas, sniper e desmembramento (noite, 2560×1440)

- Editor 6000.6.5f1, 2560×1440
- 11th Gen Intel(R) Core(TM) i5-11600KF @ 3.90GHz (12 núcleos), AMD Radeon RX 6750 XT
- Zumbis espalhados pela cidade, 10 s de medida para cada quantidade, sem vsync. Jogadores na partida: 1. Câmera em (-6, 2, -9), olhando (0.6, 0.0, 0.8).

| Zumbis | Perto / médio / longe | Quadro médio (ms) | Quadro p95 (ms) | Thread principal (ms) | GPU (ms) | IA + LOD (ms) | Rede (ms) | Animação (ms) | Navegação (ms) | Física (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Rede (KB/s por cliente) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 50 | 3 / 15 / 31 | 13,4 | 14,2 | 13,4 | 9,0 | 0,01 | 0,00 | 0,09 | 0,11 | 0,11 | 912 | 0 | 67 | 435 | 0,0 |
| 100 | 1 / 30 / 68 | 13,4 | 14,2 | 13,4 | 9,2 | 0,01 | 0,00 | 0,12 | 0,15 | 0,10 | 930 | 0 | 67 | 530 | 0,0 |
| 250 | 12 / 64 / 173 | 13,4 | 14,1 | 13,4 | 9,2 | 0,03 | 0,00 | 0,21 | 0,21 | 0,18 | 963 | 0 | 68 | 727 | 0,0 |
| 500 | 13 / 143 / 342 | 13,4 | 14,4 | 13,4 | 8,4 | 0,06 | 0,00 | 0,46 | 0,31 | 0,22 | 1078 | 0 | 67 | 1400 | 0,0 |

## 23/10/2026 23:27 — armas rebalanceadas, sniper e desmembramento, 2ª rodada (noite, 2560×1440)

- Editor 6000.6.5f1, 2560×1440
- 11th Gen Intel(R) Core(TM) i5-11600KF @ 3.90GHz (12 núcleos), AMD Radeon RX 6750 XT
- Zumbis espalhados pela cidade, 10 s de medida para cada quantidade, sem vsync. Jogadores na partida: 1. Câmera em (-6, 2, -9), olhando (0.6, 0.0, 0.8).

| Zumbis | Perto / médio / longe | Quadro médio (ms) | Quadro p95 (ms) | Thread principal (ms) | GPU (ms) | IA + LOD (ms) | Rede (ms) | Animação (ms) | Navegação (ms) | Física (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Rede (KB/s por cliente) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 50 | 1 / 14 / 35 | 13,4 | 14,2 | 13,4 | 8,7 | 0,01 | 0,00 | 0,09 | 0,11 | 0,11 | 913 | 0 | 67 | 440 | 0,0 |
| 100 | 0 / 26 / 73 | 13,4 | 14,2 | 13,4 | 8,7 | 0,01 | 0,00 | 0,09 | 0,13 | 0,10 | 914 | 0 | 68 | 446 | 0,0 |
| 250 | 5 / 76 / 168 | 13,4 | 14,0 | 13,4 | 9,2 | 0,03 | 0,00 | 0,22 | 0,20 | 0,14 | 967 | 0 | 68 | 768 | 0,0 |
| 500 | 10 / 153 / 335 | 13,4 | 14,0 | 13,4 | 9,0 | 0,05 | 0,00 | 0,40 | 0,29 | 0,20 | 1040 | 0 | 68 | 1193 | 0,0 |

## 24/10/2026 12:42 — zumbis leves (ZombieGaroto: 1 modelo, 2.195 triângulos, aparência por zumbi no shader), noite

- Editor 6000.6.5f1, 1918×887
- AMD Ryzen 7 5700X 8-Core Processor  (16 núcleos), AMD Radeon RX 9060 XT
- Zumbis espalhados pela cidade, 10 s de medida para cada quantidade, sem vsync. Jogadores na partida: 1. Câmera em (-6, 2, -9), olhando (0.6, -0.1, 0.8).

| Zumbis | Perto / médio / longe | Quadro médio (ms) | Quadro p95 (ms) | Thread principal (ms) | GPU (ms) | IA + LOD (ms) | Rede (ms) | Animação (ms) | Navegação (ms) | Física (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Rede (KB/s por cliente) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 50 | 2 / 13 / 34 | 5,6 | 6,7 | 5,5 | 2,6 | 0,01 | 0,00 | 0,08 | 0,13 | 0,05 | 1029 | 0 | 90 | 438 | 0,0 |
| 100 | 2 / 30 / 68 | 6,1 | 7,1 | 6,1 | 2,7 | 0,01 | 0,00 | 0,13 | 0,19 | 0,06 | 1045 | 0 | 91 | 473 | 0,0 |
| 250 | 12 / 70 / 167 | 6,6 | 8,0 | 6,6 | 2,8 | 0,02 | 0,00 | 0,25 | 0,26 | 0,09 | 1090 | 0 | 91 | 571 | 0,0 |
| 500 | 12 / 132 / 355 | 7,8 | 9,5 | 7,8 | 2,9 | 0,05 | 0,00 | 0,40 | 0,34 | 0,14 | 1133 | 0 | 91 | 659 | 0,0 |

## 24/10/2026 12:45 — A/B: os 4 modelos de antes (~6.300 triângulos, URP Lit), noite

- Editor 6000.6.5f1, 1918×887
- AMD Ryzen 7 5700X 8-Core Processor  (16 núcleos), AMD Radeon RX 9060 XT
- Zumbis espalhados pela cidade, 10 s de medida para cada quantidade, sem vsync. Jogadores na partida: 1. Câmera em (-6, 2, -9), olhando (0.6, 0.0, 0.8).

| Zumbis | Perto / médio / longe | Quadro médio (ms) | Quadro p95 (ms) | Thread principal (ms) | GPU (ms) | IA + LOD (ms) | Rede (ms) | Animação (ms) | Navegação (ms) | Física (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Rede (KB/s por cliente) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 50 | 1 / 11 / 37 | 5,5 | 6,7 | 5,5 | 2,3 | 0,00 | 0,00 | 0,08 | 0,13 | 0,05 | 1002 | 0 | 86 | 455 | 0,0 |
| 100 | 3 / 30 / 66 | 5,9 | 7,1 | 5,9 | 2,4 | 0,01 | 0,00 | 0,12 | 0,18 | 0,06 | 1023 | 0 | 87 | 585 | 0,0 |
| 250 | 4 / 67 / 177 | 6,4 | 7,8 | 6,4 | 2,7 | 0,02 | 0,00 | 0,26 | 0,25 | 0,07 | 1066 | 0 | 87 | 857 | 0,0 |
| 500 | 9 / 156 / 334 | 7,2 | 8,2 | 7,2 | 2,9 | 0,04 | 0,00 | 0,44 | 0,34 | 0,11 | 1128 | 0 | 87 | 1245 | 0,0 |

## 24/10/2026 12:47 — A/B: zumbis leves (ZombieGaroto), 2ª rodada, noite

- Editor 6000.6.5f1, 1918×887
- AMD Ryzen 7 5700X 8-Core Processor  (16 núcleos), AMD Radeon RX 9060 XT
- Zumbis espalhados pela cidade, 10 s de medida para cada quantidade, sem vsync. Jogadores na partida: 1. Câmera em (-6, 2, -9), olhando (0.6, 0.0, 0.8).

| Zumbis | Perto / médio / longe | Quadro médio (ms) | Quadro p95 (ms) | Thread principal (ms) | GPU (ms) | IA + LOD (ms) | Rede (ms) | Animação (ms) | Navegação (ms) | Física (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Rede (KB/s por cliente) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 50 | 4 / 15 / 30 | 5,8 | 7,2 | 5,7 | 2,2 | 0,01 | 0,00 | 0,09 | 0,14 | 0,05 | 1011 | 0 | 90 | 437 | 0,0 |
| 100 | 3 / 28 / 68 | 6,1 | 8,3 | 6,1 | 2,7 | 0,01 | 0,00 | 0,15 | 0,17 | 0,06 | 1035 | 0 | 89 | 490 | 0,0 |
| 250 | 6 / 71 / 171 | 7,4 | 9,6 | 7,4 | 2,5 | 0,03 | 0,00 | 0,23 | 0,29 | 0,13 | 939 | 0 | 86 | 461 | 0,0 |
| 500 | 49 / 162 / 288 | 9,1 | 11,8 | 9,1 | 1,9 | 0,12 | 0,00 | 0,51 | 0,54 | 0,33 | 713 | 0 | 90 | 497 | 0,0 |

