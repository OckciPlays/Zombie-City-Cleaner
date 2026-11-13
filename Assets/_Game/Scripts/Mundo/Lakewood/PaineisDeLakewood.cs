using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Os painéis de metal do Lakewood (12/11/2026, o Plano_Lakewood, "O jogo do mundo sorteado no Lakewood", a parte 4): os
    /// mesmos do mundo sorteado (a chapa, a porta e a placa; a mesma densidade), pelos lugares do Lakewood, da semente do
    /// mapa e com o sorteio deles:
    ///  - nas ruas das cidades: de <see cref="PorTrechoMin"/> a <see cref="PorTrechoMax"/> a cada 60 m de rua, na beira do
    ///    asfalto (fora das pontes e dos cruzamentos);
    ///  - ao lado dos carros abandonados (<see cref="ChanceDoCarro"/>);
    ///  - nos lotes vazios das cidades (<see cref="ChanceDoBaldio"/>) e na frente das lojas e do posto
    ///    (<see cref="ChanceDoEstacionamento"/>).
    /// O número de cada um é o índice em <see cref="Todos"/> (o save guarda os desmontados).
    /// </summary>
    public sealed partial class PaineisDeSucata
    {
        PaineisDeSucata(int semente, System.Func<float, float, Vector2Int> blocoDe, int blocosX, int blocosZ)
        {
            _s = Sorteio.Da(semente, "paineis-de-sucata");
            _blocoDe = blocoDe;
            _blocosX = blocosX;
            _blocosZ = blocosZ;
        }

        static PaineisDeSucata _doLakewood;
        static ChaoDeLakewood _chaoDoLakewood;

        /// <summary>Os painéis do Lakewood (feitos na primeira vez; só na thread principal).</summary>
        public static PaineisDeSucata DoLakewood(ChaoDeLakewood chao, ObjetosDaEstrada objetos)
        {
            if (chao == null) return null;
            if (!ReferenceEquals(chao, _chaoDoLakewood))
            {
                _doLakewood = NoLakewood(chao, objetos);
                _chaoDoLakewood = chao;
            }
            return _doLakewood;
        }

        static PaineisDeSucata NoLakewood(ChaoDeLakewood chao, ObjetosDaEstrada objetos)
        {
            var p = new PaineisDeSucata(chao.Mapa.semente,
                (x, z) => new Vector2Int(Mathf.FloorToInt(x / ChaoDeLakewood.TamanhoDoBloco), Mathf.FloorToInt(z / ChaoDeLakewood.TamanhoDoBloco)),
                chao.BlocosX, chao.BlocosZ);
            // o que já ocupa o chão: os carros
            if (objetos != null)
                for (int i = 0; i < objetos.Todos.Count; i++)
                {
                    float r = RaioDe(objetos.Todos[i].Tipo);
                    if (r > 0f) p.Marcar(objetos.Todos[i].Posicao, r, i);
                }
            p.NasRuasDoLakewood(chao);
            p.NosLotesDoLakewood(chao);
            if (objetos != null) p.PertoDosCarrosDoLakewood(chao, objetos);
            p.Indexar();
            return p;
        }

        // o terreiro da casa da partida fica limpo (os carros arrumados, a mesa)
        static bool NaPartida(ChaoDeLakewood chao, Vector2 q) => chao.Partida != null && chao.Partida.DistanciaDoTerreiro(q.x, q.y) < 10f;

        // um a dois a cada 60 m das ruas das cidades, na beira do asfalto
        void NasRuasDoLakewood(ChaoDeLakewood chao)
        {
            foreach (var est in chao.Estradas)
            {
                if (est.DaUsina || est.Via == null || est.Via.tipo != TipoDeVia.Rua || est.Pontos.Length < 3) continue;
                float total = est.AoLongo[est.AoLongo.Length - 1];
                for (float ini = 0f; ini + 20f < total; ini += 60f)
                {
                    int n = _s.Entre(PorTrechoMin, PorTrechoMax + 1);
                    for (int k = 0; k < n; k++)
                        for (int tentativa = 0; tentativa < 3; tentativa++)
                        {
                            float aqui = Mathf.Min(total - 3f, ini + _s.Entre(3f, 57f));
                            int i = 1;
                            while (i < est.AoLongo.Length - 1 && est.AoLongo[i] < aqui) i++;
                            if (est.Ponte[i] || est.AteONo[i] < 12f) continue;
                            var a = est.Pontos[i - 1];
                            var b = est.Pontos[i];
                            var dir = (b - a).normalized;
                            float t = Mathf.InverseLerp(est.AoLongo[i - 1], est.AoLongo[i], aqui);
                            var meio = Vector2.Lerp(a, b, t);
                            var lado = new Vector2(dir.y, -dir.x) * (_s.Chance(0.5f) ? 1f : -1f);
                            var q = meio + lado * (est.MeiaLargura - _s.Entre(0.5f, 1.4f));
                            if (chao.Agua(q.x, q.y, out _) || NaPartida(chao, q)) continue;
                            if (Por(q, _s.Entre(0f, 360f), Modelo(0.45f, 0.2f), ChaoDoPainel.Asfalto)) break;
                        }
                }
            }
        }

        // os lotes vazios (o baldio) e a frente das lojas e do posto (o estacionamento)
        void NosLotesDoLakewood(ChaoDeLakewood chao)
        {
            foreach (var l in chao.Mapa.lotes)
            {
                bool vazio = l.tipo == UsoDoLote.Vazio;
                bool comercio = l.tipo == UsoDoLote.Loja || l.tipo == UsoDoLote.Posto;
                if (!vazio && !comercio) continue;
                if (chao.Mapa.casaDaPartida != null && chao.Mapa.casaDaPartida.TomaOLote(l)) continue;   // (o lote da casa da partida)
                if (!_s.Chance(vazio ? ChanceDoBaldio : ChanceDoEstacionamento)) continue;
                for (int tentativa = 0; tentativa < 3; tentativa++)
                {
                    // o vazio: em qualquer canto do lote; o comércio: na faixa da frente (onde fica o estacionamento)
                    float x = _s.Entre(-l.largura * 0.5f + 2f, l.largura * 0.5f - 2f);
                    float z = vazio ? _s.Entre(-l.fundo * 0.5f + 2f, l.fundo * 0.5f - 2f) : -l.fundo * 0.5f + _s.Entre(1.5f, 4f);
                    var q = l.NoMapa(x, z);
                    if (chao.Agua(q.x, q.y, out _) || NaPartida(chao, q)) continue;
                    if (Por(q, _s.Entre(0f, 360f), Modelo(0.6f, 0.25f), ChaoDoPainel.Terra)) break;
                }
            }
        }

        // ao lado dos carros abandonados (a porta que caiu dele, na maioria)
        void PertoDosCarrosDoLakewood(ChaoDeLakewood chao, ObjetosDaEstrada objetos)
        {
            var todos = objetos.Todos;
            for (int i = 0; i < todos.Count; i++)
            {
                var o = todos[i];
                if (o.Tipo != TipoDeObjeto.Carro || !_s.Chance(ChanceDoCarro)) continue;
                var frente = FrenteDe(o.Giro);
                var direita = new Vector2(frente.y, -frente.x);
                for (int tentativa = 0; tentativa < 2; tentativa++)
                {
                    float lado = _s.Chance(0.5f) ? 1f : -1f;
                    var q = o.Posicao + direita * (lado * _s.Entre(2.2f, 2.8f)) + frente * _s.Entre(-1.3f, 1.3f);
                    if (chao.Agua(q.x, q.y, out _) || NaPartida(chao, q)) continue;
                    bool asfalto = chao.EstradaPerto(q.x, q.y, 0f, out _, out _, out _, out _);
                    if (Por(q, o.Giro + _s.Entre(-60f, 60f), Modelo(0.25f, 0.6f), asfalto ? ChaoDoPainel.Asfalto : ChaoDoPainel.Terra, i)) break;
                }
            }
        }
    }
}
