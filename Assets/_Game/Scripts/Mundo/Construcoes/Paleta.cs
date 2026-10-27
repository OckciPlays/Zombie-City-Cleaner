using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// As cores da paleta (Art/Paleta.png, 16 × 16), pelo nome: a mesma lista do `CORES` do `cidade_kit.py` (pasta de
    /// arte, Modelo 3D/Cidade). Ao mudar uma cor de lugar lá, mude aqui. A linha conta de baixo para cima (é o v do UV).
    /// </summary>
    public static class Paleta
    {
        static readonly Dictionary<string, Vector2Int> Cores = new Dictionary<string, Vector2Int>
        {
            // linha 5: chão e concreto
            ["Asfalto"] = new Vector2Int(0, 5), ["AsfaltoRemendo"] = new Vector2Int(1, 5), ["Calcada"] = new Vector2Int(2, 5),
            ["Concreto"] = new Vector2Int(3, 5), ["ConcretoClaro"] = new Vector2Int(4, 5), ["MeioFio"] = new Vector2Int(5, 5),
            ["Faixa"] = new Vector2Int(6, 5), ["Bunker"] = new Vector2Int(7, 5), ["Terra"] = new Vector2Int(8, 5),
            ["Grama"] = new Vector2Int(9, 5), ["GramaSeca"] = new Vector2Int(10, 5), ["Entulho"] = new Vector2Int(11, 5),
            ["Barreira"] = new Vector2Int(12, 5), ["Oleo"] = new Vector2Int(13, 5), ["Grade"] = new Vector2Int(14, 5),
            // linha 6: casas e prédio
            ["ParedeVerde"] = new Vector2Int(0, 6), ["ParedeBege"] = new Vector2Int(1, 6), ["ParedeAzul"] = new Vector2Int(2, 6),
            ["ParedeInterna"] = new Vector2Int(3, 6), ["Piso"] = new Vector2Int(4, 6), ["Teto"] = new Vector2Int(5, 6),
            ["Telhado"] = new Vector2Int(6, 6), ["TelhadoAzul"] = new Vector2Int(7, 6), ["Tijolo"] = new Vector2Int(8, 6),
            ["TijoloEscuro"] = new Vector2Int(9, 6), ["Batente"] = new Vector2Int(10, 6), ["Madeira"] = new Vector2Int(11, 6),
            ["Vidro"] = new Vector2Int(12, 6), ["JanelaAcesa"] = new Vector2Int(13, 6), ["Cortina"] = new Vector2Int(14, 6),
            ["Mofo"] = new Vector2Int(15, 6),
            // linha 7: metal e carros
            ["Poste"] = new Vector2Int(0, 7), ["Metal"] = new Vector2Int(1, 7), ["Cromo"] = new Vector2Int(2, 7),
            ["Ferrugem"] = new Vector2Int(3, 7), ["FerrugemEscura"] = new Vector2Int(4, 7), ["Pneu"] = new Vector2Int(5, 7),
            ["CarroAzul"] = new Vector2Int(6, 7), ["CarroVinho"] = new Vector2Int(7, 7), ["CarroCinza"] = new Vector2Int(8, 7),
            ["CarroVerde"] = new Vector2Int(9, 7), ["CarroBege"] = new Vector2Int(10, 7), ["CarroBranco"] = new Vector2Int(11, 7),
            ["Farol"] = new Vector2Int(12, 7), ["Lanterna"] = new Vector2Int(13, 7), ["Interior"] = new Vector2Int(14, 7),
            ["VidroCarro"] = new Vector2Int(15, 7),
            // linha 8: plantas
            ["Pinheiro"] = new Vector2Int(0, 8), ["PinheiroEscuro"] = new Vector2Int(1, 8), ["PinheiroClaro"] = new Vector2Int(2, 8),
            ["Tronco"] = new Vector2Int(3, 8), ["TroncoEscuro"] = new Vector2Int(4, 8), ["Copa"] = new Vector2Int(5, 8),
            ["CopaSeca"] = new Vector2Int(6, 8), ["Galho"] = new Vector2Int(7, 8), ["FolhaMorta"] = new Vector2Int(8, 8),
            ["Arbusto"] = new Vector2Int(9, 8), ["FolhaEscura"] = new Vector2Int(10, 8), ["Folha"] = new Vector2Int(11, 8),
            ["FolhaClara"] = new Vector2Int(12, 8), ["FolhaAmarela"] = new Vector2Int(13, 8), ["TroncoClaro"] = new Vector2Int(14, 8),
            ["Hera"] = new Vector2Int(15, 8),
            // linha 9: sinalização e objetos
            ["PlacaVermelha"] = new Vector2Int(0, 9), ["PlacaBranca"] = new Vector2Int(1, 9), ["PlacaVerde"] = new Vector2Int(2, 9),
            ["PlacaAmarela"] = new Vector2Int(3, 9), ["Hidrante"] = new Vector2Int(4, 9), ["Lixeira"] = new Vector2Int(5, 9),
            ["Cacamba"] = new Vector2Int(6, 9), ["Saco"] = new Vector2Int(7, 9), ["BancoMadeira"] = new Vector2Int(8, 9),
            ["Laranja"] = new Vector2Int(9, 9), ["ListraBranca"] = new Vector2Int(10, 9),
            ["Papelao"] = new Vector2Int(11, 9), ["Cerca"] = new Vector2Int(12, 9), ["MadeiraVelha"] = new Vector2Int(13, 9),
            ["Lente"] = new Vector2Int(14, 9), ["Sangue"] = new Vector2Int(15, 9),
            // linha 10: detalhes das casas
            ["ParedeVerdeEscura"] = new Vector2Int(0, 10), ["ParedeBegeEscura"] = new Vector2Int(1, 10),
            ["ParedeAzulEscura"] = new Vector2Int(2, 10), ["PapelVerde"] = new Vector2Int(3, 10), ["PapelRosa"] = new Vector2Int(4, 10),
            ["Azulejo"] = new Vector2Int(5, 10), ["Taco"] = new Vector2Int(6, 10), ["Carpete"] = new Vector2Int(7, 10),
            ["PersianaVerde"] = new Vector2Int(8, 10), ["PersianaVinho"] = new Vector2Int(9, 10), ["Rodape"] = new Vector2Int(10, 10),
            ["Cumeeira"] = new Vector2Int(11, 10), ["TelhadoEscuro"] = new Vector2Int(12, 10), ["TelhadoAzulEscuro"] = new Vector2Int(13, 10),
            // linha 13: a vegetação da floresta (Parte M9): as coníferas, as cascas, os bordos, o salgueiro e o chão
            ["Abeto"] = new Vector2Int(0, 13), ["AbetoClaro"] = new Vector2Int(1, 13), ["PinheiroBranco"] = new Vector2Int(2, 13),
            ["CascaVermelha"] = new Vector2Int(3, 13), ["Betula"] = new Vector2Int(4, 13), ["BetulaMarca"] = new Vector2Int(5, 13),
            ["BordoVermelho"] = new Vector2Int(6, 13), ["BordoLaranja"] = new Vector2Int(7, 13), ["Salgueiro"] = new Vector2Int(8, 13),
            ["Carvalho"] = new Vector2Int(9, 13), ["Samambaia"] = new Vector2Int(10, 13), ["Capim"] = new Vector2Int(11, 13),
            ["CapimSeco"] = new Vector2Int(12, 13), ["Pedra"] = new Vector2Int(13, 13), ["PedraEscura"] = new Vector2Int(14, 13),
            ["Musgo"] = new Vector2Int(15, 13),
            // linha 14: o abandono das cidades (Parte M9b)
            ["VidroSujo"] = new Vector2Int(0, 14), ["Compensado"] = new Vector2Int(1, 14),
        };

        /// <summary>O UV do meio do quadradinho da cor.</summary>
        public static Vector2 UV(string cor)
        {
            if (!Cores.TryGetValue(cor, out var c)) throw new KeyNotFoundException($"A cor \"{cor}\" não está na Paleta.");
            return new Vector2((c.x + 0.5f) / 16f, (c.y + 0.5f) / 16f);
        }
    }
}
