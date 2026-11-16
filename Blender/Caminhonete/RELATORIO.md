# A caminhonete (15/11/2026)

O primeiro carro do Zombie City Cleaner: uma picape grande de cabine simples dos anos 90, inspirada nas fotos do usuário
(`referencia/`), **sem nome, marca nem emblema** em lugar nenhum. A grade tem uma plaqueta lisa no meio.

## Versões (todas guardadas lado a lado)

| | v1 | v2 | **v3 (no jogo)** |
|---|---|---|---|
| Triângulos (4 rodas + estepe) | 10.032 | 10.032 | **8.132** |
| Roda | 760 (24 lados) | 760 | **380 (16 lados chapados)** |
| Estepe | no meio | no meio | **do lado direito, inclinado 6°** |
| Rodas de liga | escuras | claras | claras |
| Barras da grade | invisíveis | aço | aço |

- **v2:** as rodas de liga mais claras e as barras da grade em aço (pretas no fundo preto, não apareciam).
- **v3**, a pedido:
  - "a roda na caçamba seria mais interessante ser mais lateral ... encostada em um dos lados";
  - "não deixar [os pneus] tão realistas/redondos, vai consumir muitos triângulos".

## Medidas (eixos da Unity)

- 4,99 × 1,94 × 1,87 m (2,30 de largura com os retrovisores).
- Entre-eixos de 2,98 m e bitola de 1,66 m.
- Rodas de 0,385 m (245/75 aro 16).
- Caçamba de 2 m: o piso em 0,84 e a beirada em 1,38.
- O motorista fica onde fica o da perua, 42 cm mais alto e 4 cm para trás.

## Como refazer

1. Edite o `caminhonete.py` (copie antes para `versoes_do_script/caminhonete_vN.py`).
2. `./assar_tudo.sh vN`: constrói, assa os 10 passes na CPU e exporta em `export/vN/`.
3. `blender -b --factory-startup Caminhonete_vN.blend --python foto_final.py -- vN renders/vN`: tira as fotos com as
   texturas. O `foto.py` tira as fotos de forma (Workbench).
4. Copie `export/vN/Caminhonete.fbx` e `Texturas/` para `Assets/_Game/Art/Veiculos/Caminhonete/`.
5. Na Unity, rode Zombie City Cleaner > Veículos > Montar a caminhonete (com a chave).
