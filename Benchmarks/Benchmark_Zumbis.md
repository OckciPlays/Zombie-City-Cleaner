# Benchmark dos zumbis

- 23/10/2026 09:35, Editor 6000.6.5f1, 1918×887
- AMD Ryzen 7 5700X 8-Core Processor  (16 núcleos), AMD Radeon RX 9060 XT
- Zumbis espalhados pela cidade, 10 s de medida para cada quantidade, sem vsync. Jogadores na partida: 3.

| Zumbis | Perto / médio / longe | Quadro médio (ms) | Quadro p95 (ms) | Thread principal (ms) | IA + LOD (ms) | Rede (ms) | Animação (ms) | Navegação (ms) | Física (ms) | Draw calls | Rede (KB/s por cliente) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 50 | 4 / 15 / 29 | 4,4 | 5,6 | 4,4 | 0,00 | 0,00 | 0,08 | 0,11 | 0,03 | 2459 | 0,6 |
| 100 | 4 / 33 / 62 | 4,6 | 5,8 | 4,6 | 0,01 | 0,00 | 0,09 | 0,16 | 0,03 | 2457 | 1,7 |
| 250 | 10 / 67 / 171 | 5,1 | 6,4 | 5,1 | 0,02 | 0,01 | 0,14 | 0,25 | 0,05 | 2480 | 3,1 |
| 500 | 44 / 160 / 295 | 5,8 | 6,8 | 5,8 | 0,04 | 0,02 | 0,28 | 0,38 | 0,09 | 2523 | 8,3 |
