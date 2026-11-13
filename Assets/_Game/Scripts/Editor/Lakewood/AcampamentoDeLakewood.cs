using System.Text;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A comunidade do Lakewood (12/11/2026, o Plano_Lakewood, "O jogo do mundo sorteado no Lakewood", a parte 6; a decisão:
    /// "a comunidade pode ficar perto da cidade que tem hoje a woodcreek", e o acampamento grande dos comerciantes do Pedro):
    /// acha o lugar na beira da SR-27, fora da Woodcreek (de 350 m a 1,1 km do meio dela e a 60 m do contorno, pelo menos),
    /// o mais plano, longe da água, dos lotes e das outras estradas, e grava um lote do tipo
    /// <see cref="UsoDoLote.Acampamento"/> (64 × 64 m, de frente para a rodovia) no fim da lista dos lotes do mapa (os números
    /// dos outros não mudam). O chão fica plano pelo patamar do lote; o resto (a cerca, a casa, as barracas, os comerciantes
    /// e a zona segura) é do AcampamentosDoMundo, no jogo.
    /// </summary>
    public static class AcampamentoDeLakewood
    {
        const string Cidade = "Woodcreek";
        const string Rodovia = "SR-27";
        public const float Lado = 64f;

        [MenuItem("Zombie City Cleaner/Lakewood/Etapa 6: o acampamento dos comerciantes (perto da Woodcreek)", false, 31)]
        public static void Fazer()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de pôr o acampamento.");
                return;
            }
            var mapa = MontagemDeLakewood.Carregar(false);
            if (mapa == null || mapa.estradas.Count == 0)
            {
                Debug.LogWarning("Falta o mapa com as estradas.");
                return;
            }
            var relatorio = new StringBuilder();
            Undo.RecordObject(mapa, "O acampamento");
            // o de antes sai (o chão é feito sem ele, para a busca não contar com ele)
            int antes = mapa.lotes.RemoveAll(l => l.tipo == UsoDoLote.Acampamento);
            var chao = new ChaoDeLakewood(mapa, new RelevoDeLakewood(mapa));
            if (!Achar(mapa, chao, relatorio, out var lote))
            {
                Debug.LogWarning("[Lakewood] O acampamento: nenhum lugar serve. " + relatorio);
                return;
            }
            mapa.lotes.Add(lote);
            EditorUtility.SetDirty(mapa);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Lakewood] O acampamento dos comerciantes ({(antes > 0 ? "no lugar do de antes" : "novo")}): {relatorio}");
        }

        /// <summary>O lugar do acampamento: o lote na beira da SR-27, fora da Woodcreek, o mais plano.</summary>
        public static bool Achar(MapaDeLakewood mapa, ChaoDeLakewood chao, StringBuilder relatorio, out LoteDoMapa lote)
        {
            lote = null;
            var cidade = mapa.cidades.Find(c => c.nome == Cidade);
            if (cidade == null)
            {
                relatorio.Append("sem a Woodcreek no mapa.");
                return false;
            }
            float melhorNota = float.MaxValue;
            Vector2 melhor = default, frente = default;
            string rua = Rodovia;
            int vistos = 0;
            foreach (var e in chao.Estradas)
            {
                if (e.DaUsina || e.Via == null || e.Via.tipo != TipoDeVia.Rodovia) continue;
                for (int i = 2; i + 2 < e.Pontos.Length; i += 2)
                {
                    var p = e.Pontos[i];
                    float aoMeio = Vector2.Distance(p, cidade.centro);
                    if (aoMeio < 350f || aoMeio > 1100f || e.Ponte[i] || e.AteONo[i] < 60f) continue;
                    var dir = (e.Pontos[i + 1] - e.Pontos[i - 1]).normalized;
                    var normal = new Vector2(-dir.y, dir.x);
                    foreach (float lado in new[] { 1f, -1f })
                    {
                        // a frente do lote a 9 m da beira do asfalto
                        var centro = p + normal * (lado * (e.MeiaLargura + 9f + Lado * 0.5f));
                        vistos++;
                        if (!Cabe(mapa, chao, cidade, centro, out float aspereza)) continue;
                        float nota = aspereza + Mathf.Abs(aoMeio - 550f) * 0.004f;
                        if (nota >= melhorNota) continue;
                        melhorNota = nota;
                        melhor = centro;
                        frente = -normal * lado;
                        rua = string.IsNullOrEmpty(e.Via.nome) ? Rodovia : e.Via.nome;
                    }
                }
            }
            if (melhorNota == float.MaxValue)
            {
                relatorio.Append($"{vistos} lugares vistos.");
                return false;
            }
            melhor = new Vector2(Mathf.Round(melhor.x), Mathf.Round(melhor.y));
            lote = new LoteDoMapa
            {
                cidade = Cidade, tipo = UsoDoLote.Acampamento, nome = "acampamento", rua = rua, meio = melhor,
                giro = LoteDoMapa.GiroPara(frente), largura = Lado, fundo = Lado, semente = 7121,
            };
            relatorio.Append($"em ({melhor.x:0}; {melhor.y:0}), na {rua}, a {Vector2.Distance(melhor, cidade.centro):0} m do meio da {Cidade}, " +
                             $"a aspereza {melhorNota:0.0} ({vistos} lugares vistos).");
            return true;
        }

        // o lote cabe ali: fora da cidade, da água e dos lotes, sem outra estrada dentro, e a aspereza (o maior desnível do chão)
        static bool Cabe(MapaDeLakewood mapa, ChaoDeLakewood chao, CidadeDoMapa cidade, Vector2 centro, out float aspereza)
        {
            aspereza = 0f;
            if (Linhas.Dentro(centro, cidade.contorno) || Linhas.Distancia(centro, cidade.contorno, true) < 60f + Lado * 0.5f) return false;
            if (centro.x < 200f || centro.y < 200f || centro.x > mapa.largura - 200f || centro.y > mapa.comprimento - 200f) return false;
            if (chao.Agua(centro.x, centro.y, out _, 60f)) return false;
            foreach (var l in mapa.lotes) if (Vector2.Distance(l.meio, centro) < 90f) return false;
            if (mapa.casaDaPartida != null && mapa.casaDaPartida.tem && Vector2.Distance(mapa.casaDaPartida.MeioDaCasa, centro) < 150f) return false;
            float min = float.MaxValue, max = float.MinValue;
            for (int a = -2; a <= 2; a++)
            for (int b = -2; b <= 2; b++)
            {
                var q = centro + new Vector2(a, b) * (Lado * 0.25f);
                if (chao.EstradaPerto(q.x, q.y, 2f, out _, out _, out _, out _)) return false;
                float h = chao.Relevo.Altura(q.x, q.y);
                min = Mathf.Min(min, h);
                max = Mathf.Max(max, h);
            }
            aspereza = max - min;
            return aspereza < 9f;
        }
    }
}
