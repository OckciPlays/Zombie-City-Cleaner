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

