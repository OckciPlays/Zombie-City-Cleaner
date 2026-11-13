using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O Lakewood como o mundo do jogo para os sobreviventes, os militares e os acampamentos (12/11/2026, o Plano_Lakewood,
    /// "O jogo do mundo sorteado no Lakewood", as partes 5 e 6):
    ///  - as cidades são as do mapa (na ordem de <see cref="MapaDeLakewood.cidades"/>), e o ponto conta como de uma só dentro
    ///    do contorno das que têm os lotes (hoje, a Woodcreek); a principal é a grande (os 6 grupos), e as outras têm 2;
    ///  - as bases militares são os lugares do mapa do tipo base (Fort Crowley), com o portão para a estrada mais perto;
    ///  - os acampamentos são os lotes do tipo <see cref="UsoDoLote.Acampamento"/> (o chão plano é o patamar do lote).
    /// </summary>
    public partial class LakewoodNaCena
    {
        int IMundoDoJogo.QuantasCidades => mapa != null ? mapa.cidades.Count : 0;
        Vector2 IMundoDoJogo.MeioDaCidade(int cidade) => mapa.cidades[cidade].centro;

        bool IMundoDoJogo.CidadeGrande(int cidade) =>
            mapa != null && cidade >= 0 && cidade < mapa.cidades.Count && mapa.cidades[cidade].tipo == TipoDeCidade.Principal;

        int IMundoDoJogo.GruposDaCidade(int cidade, int naGrande)
        {
            if (mapa == null || cidade < 0 || cidade >= mapa.cidades.Count) return 1;
            var t = mapa.cidades[cidade].tipo;
            return t == TipoDeCidade.Principal ? Mathf.Clamp(naGrande, 1, 8) : t == TipoDeCidade.Rural ? 1 : 2;
        }

        // as cidades com os lotes (o índice no mapa, a caixa e o contorno), feitas uma vez
        List<(int indice, Rect caixa, List<Vector2> contorno)> _cidadesDoJogo;

        int IMundoDoJogo.CidadeEm(float x, float z)
        {
            if (mapa == null) return -1;
            if (_cidadesDoJogo == null)
            {
                _cidadesDoJogo = new List<(int, Rect, List<Vector2>)>();
                for (int i = 0; i < mapa.cidades.Count; i++)
                {
                    var c = mapa.cidades[i];
                    if (c.contorno == null || c.contorno.Count < 3 || !mapa.TemLotes(c.nome)) continue;
                    float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
                    foreach (var q in c.contorno)
                    {
                        x0 = Mathf.Min(x0, q.x); z0 = Mathf.Min(z0, q.y);
                        x1 = Mathf.Max(x1, q.x); z1 = Mathf.Max(z1, q.y);
                    }
                    _cidadesDoJogo.Add((i, Rect.MinMaxRect(x0, z0, x1, z1), c.contorno));
                }
            }
            var p = new Vector2(x, z);
            foreach (var (indice, caixa, contorno) in _cidadesDoJogo)
                if (caixa.Contains(p) && Linhas.Dentro(p, contorno)) return indice;
            return -1;
        }

        List<Lugar> _bases;

        IReadOnlyList<Lugar> IMundoDoJogo.BasesMilitares
        {
            get
            {
                if (_bases == null)
                {
                    _bases = new List<Lugar>();
                    if (mapa != null)
                        foreach (var l in mapa.lugares)
                            if (l.tipo == TipoDeLugarDoMapa.BaseMilitar)
                                _bases.Add(new Lugar { Tipo = TipoDeLugar.BaseMilitar, Nome = l.nome, Centro = l.ponto, Raio = l.raio, Giro = l.giro });
                }
                return _bases;
            }
        }

        // o portão da base para o ponto de estrada mais perto (fora da cerca); sem estrada a 600 m, o giro do lugar
        float IMundoDoJogo.GiroDaBase(Lugar b)
        {
            if (Chao == null) return b.Giro;
            float melhor = 600f * 600f;
            Vector2 alvo = default;
            bool achou = false;
            foreach (var e in Chao.Estradas)
                for (int i = 0; i < e.Pontos.Length; i++)
                {
                    float d = (e.Pontos[i] - b.Centro).sqrMagnitude;
                    if (d >= melhor || d < 30f * 30f) continue;
                    melhor = d;
                    alvo = e.Pontos[i];
                    achou = true;
                }
            if (!achou) return b.Giro;
            var para = alvo - b.Centro;
            return Mathf.Atan2(para.x, para.y) * Mathf.Rad2Deg;
        }

        List<Acampamento> _acampamentos;

        IReadOnlyList<Acampamento> IMundoDoJogo.Acampamentos
        {
            get
            {
                if (_acampamentos == null)
                {
                    _acampamentos = new List<Acampamento>();
                    if (mapa != null && Chao != null)
                        for (int i = 0; i < mapa.lotes.Count && i < Chao.Lotes.Count; i++)
                        {
                            var l = mapa.lotes[i];
                            if (l.tipo != UsoDoLote.Acampamento) continue;
                            int cidade = mapa.cidades.FindIndex(c => c.nome == l.cidade);
                            // a frente do lote (para a rua) é a do portão; o raio pelo lote (a cerca fica em 0,6 dele)
                            float raio = Mathf.Min(l.largura, l.fundo) * 0.5f / 0.6f - 3f;
                            _acampamentos.Add(new Acampamento
                            {
                                Tipo = TipoDeLugar.Acampamento, Nome = cidade >= 0 ? $"Acampamento de {mapa.cidades[cidade].nome}" : "Acampamento",
                                Centro = l.meio, Raio = Mathf.Min(raio, 45f), Giro = l.giro, Indice = _acampamentos.Count, Cidade = cidade,
                                Grande = true, Altura = Chao.Lotes[i].Altura, Frente = l.Frente,
                            });
                        }
                }
                return _acampamentos;
            }
        }
    }
}
