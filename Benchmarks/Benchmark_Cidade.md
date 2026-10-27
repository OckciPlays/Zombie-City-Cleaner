# Benchmark da cidade

O centro da maior cidade do mapa (Plano_Mapa, Parte M5), de dia e de noite, com a oclusão pela GPU como estava no asset da URP.

## 26/10/2026 19:01 — Cruz Alta, mapa Grande (8 km), semente 12345

- Editor 6000.6.5f1, 1080×1920
- AMD Ryzen 7 5700X 8-Core Processor  (16 núcleos), AMD Radeon RX 9060 XT
- A cidade: raio de 992 m, 2994 construções. O percurso: uma volta de 8 s no cruzamento do meio e 14 s pela avenida do meio a 8 m/s, sem vsync, com a câmera vendo até 150 m.

| Hora | Oclusão pela GPU | Quadro médio (ms) | Quadro p95 (ms) | Pior (ms) | Thread principal (ms) | GPU (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Blocos do chão | Luzes acesas | Zumbis |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 12h | desligada | 3,9 | 5,2 | 58,8 | 3,9 | 1,7 | 193 | 0 | 75 | 115 | 41 | 1 | 0 |
| 23h | desligada | 4,1 | 5,4 | 28,5 | 4,1 | 1,7 | 209 | 0 | 77 | 118 | 44 | 18 | 0 |

> A Game view estava em pé (1080 × 1920): a câmera vê menos dos lados. Na rodada de antes, que não terminou (a mesma cidade e o mesmo percurso, com a Game view deitada, 1918 × 887), o meio-dia sem a oclusão deu: quadro médio 4,3 ms, p95 6,0, pior 25,2, thread principal 4,3, GPU 1,7, 281 draw calls, 0 batches, 76 SetPass, 226 mil triângulos, 41 blocos, 1 luz. Logo depois, a primeira versão do benchmark ligou a oclusão pela GPU no meio do Play: o driver de vídeo caiu na hora (D3D11, `0x887A0005`, "device removed") e o Editor fechou. Sem zumbis: a cena Mundo ainda não tem.

## 27/10/2026 12:17 — Cruz Alta, mapa Grande (8 km), semente 12345

> **Com o abandono (Parte M9c), a primeira versão do shader** (sem a variante das instâncias DOTS: o GPU Resident Drawer não juntava os objetos, daí os 773 draw calls). A primeira volta pelo centro, carregando os blocos.

- Editor 6000.6.5f1, 1918×887
- AMD Ryzen 7 5700X 8-Core Processor  (16 núcleos), AMD Radeon RX 9060 XT
- A cidade: raio de 992 m, 2465 construções. O percurso: uma volta de 8 s no cruzamento do meio e 14 s pela avenida do meio a 8 m/s, sem vsync, com a câmera vendo até 150 m.

| Hora | Oclusão pela GPU | Quadro médio (ms) | Quadro p95 (ms) | Pior (ms) | Thread principal (ms) | GPU (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Blocos do chão | Luzes acesas | Zumbis |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 12h | desligada | 5,1 | 6,6 | 98,2 | 5,1 | 3,6 | 773 | 0 | 110 | 2431 | 41 | 1 | 0 |
| 23h | desligada | 5,1 | 6,6 | 58,1 | 5,1 | 3,8 | 782 | 0 | 110 | 2580 | 41 | 1 | 0 |

## 27/10/2026 12:19 — Cruz Alta, mapa Grande (8 km), semente 12345

> **Sem o abandono** (a comparação, na mesma sessão): os materiais trocados pelo Cidade.mat (o Lit da URP) no meio do Play, a hera e as pichações desligadas. As janelas tapadas e quebradas, o lixo e o mato das frestas continuavam lá (pesam pouco).

- Editor 6000.6.5f1, 1918×887
- AMD Ryzen 7 5700X 8-Core Processor  (16 núcleos), AMD Radeon RX 9060 XT
- A cidade: raio de 992 m, 2465 construções. O percurso: uma volta de 8 s no cruzamento do meio e 14 s pela avenida do meio a 8 m/s, sem vsync, com a câmera vendo até 150 m.

| Hora | Oclusão pela GPU | Quadro médio (ms) | Quadro p95 (ms) | Pior (ms) | Thread principal (ms) | GPU (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Blocos do chão | Luzes acesas | Zumbis |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 12h | desligada | 4,3 | 5,5 | 49,7 | 4,3 | 3,1 | 527 | 0 | 88 | 2085 | 41 | 1 | 0 |
| 23h | desligada | 4,4 | 5,6 | 48,8 | 4,4 | 3,2 | 534 | 0 | 87 | 2232 | 41 | 1 | 0 |

## 27/10/2026 12:22 — Cruz Alta, mapa Grande (8 km), semente 12345

> **Com o abandono (Parte M9c), o shader com a variante DOTS** (como ficou). Contra a rodada sem o abandono: +0,6 ms no quadro médio, +0,4 ms na GPU, +9 draw calls e +360 mil triângulos (quase tudo a hera). O pior quadro subiu (68 a 78 ms, contra 49 a 50): a malha da hera de cada bloco (até 130 mil vértices no centro, 2 a 4 ms) saía no mesmo quadro que a das casas; resolvido nas rodadas de baixo (a hera sai depois, um bloco por quadro). Os blocos do centro desta rodada carregaram sem as pichações (os campos estavam trocados para a comparação).

- Editor 6000.6.5f1, 1918×887
- AMD Ryzen 7 5700X 8-Core Processor  (16 núcleos), AMD Radeon RX 9060 XT
- A cidade: raio de 992 m, 2465 construções. O percurso: uma volta de 8 s no cruzamento do meio e 14 s pela avenida do meio a 8 m/s, sem vsync, com a câmera vendo até 150 m.

| Hora | Oclusão pela GPU | Quadro médio (ms) | Quadro p95 (ms) | Pior (ms) | Thread principal (ms) | GPU (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Blocos do chão | Luzes acesas | Zumbis |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 12h | desligada | 4,9 | 6,2 | 78,0 | 4,9 | 3,5 | 536 | 0 | 100 | 2450 | 44 | 1 | 0 |
| 23h | desligada | 4,9 | 6,1 | 68,1 | 4,9 | 3,5 | 543 | 0 | 102 | 2590 | 41 | 1 | 0 |

## 27/10/2026 12:26 — Cruz Alta, mapa Grande (8 km), semente 12345

> **Com o abandono, como ficou** (o shader com a variante DOTS e a malha da hera saindo depois, um bloco por quadro e só perto: `TerrenoDoMundo.FazerHera`). Uma sessão nova do Play; a primeira volta, carregando o centro.

- Editor 6000.6.5f1, 1918×887
- AMD Ryzen 7 5700X 8-Core Processor  (16 núcleos), AMD Radeon RX 9060 XT
- A cidade: raio de 992 m, 2465 construções. O percurso: uma volta de 8 s no cruzamento do meio e 14 s pela avenida do meio a 8 m/s, sem vsync, com a câmera vendo até 150 m.

| Hora | Oclusão pela GPU | Quadro médio (ms) | Quadro p95 (ms) | Pior (ms) | Thread principal (ms) | GPU (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Blocos do chão | Luzes acesas | Zumbis |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 12h | desligada | 4,0 | 4,9 | 41,2 | 4,0 | 3,1 | 541 | 0 | 97 | 2451 | 41 | 1 | 0 |
| 23h | desligada | 4,0 | 4,9 | 33,1 | 4,0 | 3,1 | 548 | 0 | 97 | 2595 | 42 | 1 | 0 |

## 27/10/2026 12:27 — Cruz Alta, mapa Grande (8 km), semente 12345

> **Com o abandono, como ficou**, a segunda volta (o centro já carregado).

- Editor 6000.6.5f1, 1918×887
- AMD Ryzen 7 5700X 8-Core Processor  (16 núcleos), AMD Radeon RX 9060 XT
- A cidade: raio de 992 m, 2465 construções. O percurso: uma volta de 8 s no cruzamento do meio e 14 s pela avenida do meio a 8 m/s, sem vsync, com a câmera vendo até 150 m.

| Hora | Oclusão pela GPU | Quadro médio (ms) | Quadro p95 (ms) | Pior (ms) | Thread principal (ms) | GPU (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Blocos do chão | Luzes acesas | Zumbis |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 12h | desligada | 3,9 | 4,8 | 29,5 | 3,9 | 3,0 | 541 | 0 | 98 | 2452 | 41 | 1 | 0 |
| 23h | desligada | 4,0 | 5,1 | 34,6 | 4,0 | 3,1 | 548 | 0 | 97 | 2602 | 41 | 1 | 0 |

## 27/10/2026 12:28 — Cruz Alta, mapa Grande (8 km), semente 12345

> **Sem o abandono**, na mesma sessão (os materiais trocados pelo Cidade.mat, a hera e as pichações desligadas). Contra a rodada de cima: o quadro igual (3,9 a 4,1 ms), a GPU +0,1 a 0,2 ms, +14 draw calls, +10 SetPass e +370 mil triângulos com o abandono; o pior quadro também igual.

- Editor 6000.6.5f1, 1918×887
- AMD Ryzen 7 5700X 8-Core Processor  (16 núcleos), AMD Radeon RX 9060 XT
- A cidade: raio de 992 m, 2465 construções. O percurso: uma volta de 8 s no cruzamento do meio e 14 s pela avenida do meio a 8 m/s, sem vsync, com a câmera vendo até 150 m.

| Hora | Oclusão pela GPU | Quadro médio (ms) | Quadro p95 (ms) | Pior (ms) | Thread principal (ms) | GPU (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Blocos do chão | Luzes acesas | Zumbis |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 12h | desligada | 4,1 | 5,2 | 36,1 | 4,1 | 2,9 | 527 | 0 | 88 | 2084 | 41 | 1 | 0 |
| 23h | desligada | 3,9 | 4,7 | 36,9 | 3,9 | 2,9 | 533 | 0 | 87 | 2230 | 41 | 1 | 0 |

## 27/10/2026 17:49 — Cruz Alta, mapa Grande (8 km), semente 12345

> **Com a mobília** (a fatia 1 da mobília: os móveis do Blender nos pontos, com as caixas de colisão e o NavMesh; depois da grama 3D da M9d). Uma sessão nova do Play; a primeira volta, carregando o centro.

- Editor 6000.6.5f1, 1918×887
- AMD Ryzen 7 5700X 8-Core Processor  (16 núcleos), AMD Radeon RX 9060 XT
- A cidade: raio de 992 m, 2465 construções. O percurso: uma volta de 8 s no cruzamento do meio e 14 s pela avenida do meio a 8 m/s, sem vsync, com a câmera vendo até 150 m.

| Hora | Oclusão pela GPU | Quadro médio (ms) | Quadro p95 (ms) | Pior (ms) | Thread principal (ms) | GPU (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Blocos do chão | Luzes acesas | Zumbis |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 12h | desligada | 4,1 | 5,1 | 55,8 | 4,1 | 3,1 | 541 | 0 | 94 | 2784 | 41 | 1 | 0 |
| 23h | desligada | 4,0 | 4,9 | 41,8 | 4,0 | 3,1 | 548 | 0 | 93 | 2932 | 41 | 1 | 0 |

## 27/10/2026 17:50 — Cruz Alta, mapa Grande (8 km), semente 12345

> **Com a mobília**, a segunda volta (o centro já carregado).

- Editor 6000.6.5f1, 1918×887
- AMD Ryzen 7 5700X 8-Core Processor  (16 núcleos), AMD Radeon RX 9060 XT
- A cidade: raio de 992 m, 2465 construções. O percurso: uma volta de 8 s no cruzamento do meio e 14 s pela avenida do meio a 8 m/s, sem vsync, com a câmera vendo até 150 m.

| Hora | Oclusão pela GPU | Quadro médio (ms) | Quadro p95 (ms) | Pior (ms) | Thread principal (ms) | GPU (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Blocos do chão | Luzes acesas | Zumbis |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 12h | desligada | 4,0 | 4,9 | 63,6 | 4,0 | 3,1 | 542 | 0 | 93 | 2786 | 41 | 1 | 0 |
| 23h | desligada | 4,0 | 4,9 | 61,1 | 4,0 | 3,1 | 549 | 0 | 93 | 2937 | 41 | 1 | 0 |

## 27/10/2026 17:52 — Cruz Alta, mapa Grande (8 km), semente 12345

> **Sem a mobília dos blocos**, na mesma sessão (o raio da mobília em 0 e as 5 malhas de móveis do centro desligadas; os móveis dos andares dos prédios e as caixas de colisão continuam). Contra a de cima: o quadro igual (4,0 ms), a GPU igual, +2 draw calls e +9 a 13 mil triângulos com a mobília; o pior quadro foi maior sem ela (77 a 85 ms contra 61 a 64), então os picos não são dos móveis. A pé (a travessia a 15 km/h por 60 s da casa da família, Santa Bárbara do Rio): com a mobília, média 4,9 ms, 1% pior 6,8 ms, pior 90,5 ms e 2 quadros acima de 33 ms; sem ela, 4,9 / 6,5 / 81,4 ms e os mesmos 2.

- Editor 6000.6.5f1, 1918×887
- AMD Ryzen 7 5700X 8-Core Processor  (16 núcleos), AMD Radeon RX 9060 XT
- A cidade: raio de 992 m, 2465 construções. O percurso: uma volta de 8 s no cruzamento do meio e 14 s pela avenida do meio a 8 m/s, sem vsync, com a câmera vendo até 150 m.

| Hora | Oclusão pela GPU | Quadro médio (ms) | Quadro p95 (ms) | Pior (ms) | Thread principal (ms) | GPU (ms) | Draw calls | Batches | SetPass | Triângulos (mil) | Blocos do chão | Luzes acesas | Zumbis |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 12h | desligada | 4,0 | 5,0 | 85,0 | 4,0 | 3,1 | 540 | 0 | 94 | 2777 | 41 | 1 | 0 |
| 23h | desligada | 4,0 | 5,0 | 76,9 | 4,0 | 3,1 | 547 | 0 | 93 | 2924 | 41 | 1 | 0 |

