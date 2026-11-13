using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A Woodcreek (Plano_Lakewood, Etapa 6, "A Woodcreek", 11/11/2026, aprovada: a primeira cidade, a vila florestal do
    /// nordeste, com o hospital e a loja de armas novos): as ruas novas e os lotes no banco plano a 187–192 m, gravados no
    /// mapa (<see cref="MapaDeLakewood.ruas"/> e <see cref="MapaDeLakewood.lotes"/>), e o plano dela
    /// (`Imgs/Lakewood/Lakewood_Woodcreek.png`). A Main Street é a SR-27 (a East Shore Highway do sul e a North Shore
    /// Highway do norte, que se encontram no nó do meio); as ruas novas têm 7 m (a alça das cabanas, 5).
    ///
    /// O desenho é fixo: rodar de novo troca as ruas e os lotes da Woodcreek pelos desta receita (o que foi mexido à mão no
    /// asset se perde). Depois de mexer à mão, a Imagem da Woodcreek refaz só o plano.
    /// </summary>
    public static class WoodcreekDeLakewood
    {
        public const string Cidade = "Woodcreek";

        /// <summary>O sorteio dos lotes vazios (um em cada quatro, com a mata).</summary>
        const int SementeDosVazios = 20261111;

        /// <summary>Os nomes das estradas da Etapa 5 dentro da vila (o plano).</summary>
        public static readonly Dictionary<string, string> Apelidos = new Dictionary<string, string>
        {
            { "East Shore Highway", "Main Street" },
            { "North Shore Highway", "Main Street" },
        };

        [MenuItem("Zombie City Cleaner/Lakewood/Etapa 6: a Woodcreek (as ruas e os lotes)", false, 7)]
        public static void Fazer()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de desenhar a Woodcreek.");
                return;
            }
            var mapa = MontagemDeLakewood.Carregar(false);
            if (mapa == null || mapa.estradas.Count == 0)
            {
                Debug.LogWarning("Falta o mapa com as estradas (Etapa 5: traçar as estradas).");
                return;
            }
            float t0 = Time.realtimeSinceStartup;
            try
            {
                EditorUtility.DisplayProgressBar("Lakewood", "A Woodcreek: as ruas e os lotes", 0.2f);
                Undo.RecordObject(mapa, "A Woodcreek");
                var desenho = Desenhar(mapa);
                desenho.Gravar();
                EditorUtility.SetDirty(mapa);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayProgressBar("Lakewood", "A Woodcreek: o plano", 0.6f);
                var chao = new ChaoDeLakewood(mapa, new RelevoDeLakewood(mapa));
                string imagem = ImagemDaCidade.Gravar(mapa, chao, Cidade, Apelidos);
                Debug.Log($"[Lakewood] A Woodcreek ({Time.realtimeSinceStartup - t0:F0} s): {Resumo(mapa)}{desenho.Relatorio}\n  O plano: {imagem}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Zombie City Cleaner/Lakewood/Etapa 6: a imagem da Woodcreek", false, 22)]
        public static void SoAImagem()
        {
            var mapa = MontagemDeLakewood.Carregar(false);
            if (mapa == null || !mapa.TemLotes(Cidade))
            {
                Debug.LogWarning("A Woodcreek ainda não tem os lotes (Etapa 6: a Woodcreek).");
                return;
            }
            try
            {
                EditorUtility.DisplayProgressBar("Lakewood", "A Woodcreek: o plano", 0.4f);
                var chao = new ChaoDeLakewood(mapa, new RelevoDeLakewood(mapa));
                Debug.Log($"[Lakewood] O plano da Woodcreek: {ImagemDaCidade.Gravar(mapa, chao, Cidade, Apelidos)}. {Resumo(mapa)}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>Quantos lotes de cada tipo a Woodcreek tem no mapa.</summary>
        public static string Resumo(MapaDeLakewood mapa)
        {
            var lotes = mapa.lotes.FindAll(l => l.cidade == Cidade);
            var sb = new StringBuilder($"{mapa.ruas.FindAll(r => r.cidade == Cidade).Count} ruas novas e {lotes.Count} lotes (");
            bool primeiro = true;
            foreach (UsoDoLote t in System.Enum.GetValues(typeof(UsoDoLote)))
            {
                int n = lotes.FindAll(l => l.tipo == t).Count;
                if (n == 0) continue;
                sb.Append(primeiro ? "" : ", ").Append($"{t} {n}");
                primeiro = false;
            }
            return sb.Append(").").ToString();
        }

        /// <summary>
        /// A receita (o esboço aprovado): as ruas novas com as pontas ligadas, os lotes especiais nos lugares escolhidos e os
        /// lotes enchidos ao longo das ruas (as lojas na Main Street, as casas nas outras, as cabanas na alça). Não grava.
        /// </summary>
        public static DesenhoDaCidade Desenhar(MapaDeLakewood mapa)
        {
            var d = new DesenhoDaCidade(mapa, DesenhoDaCidade.ChaoSemAsCidades(mapa), Cidade);
            var cidade = mapa.cidades.Find(c => c.nome == Cidade);
            if (cidade != null)
            {
                // a população da placa da entrada e o nome do mapa mais ao norte (no meio, ele tapava a vila)
                if (cidade.populacao <= 0) cidade.populacao = 1240;
                cidade.rotulo = new Vector2(0f, 780f);
            }

            // as ruas novas (7 m; a Spruce Lane, 6, e a alça das cabanas, 5)
            d.Rua("Pine Street", 7f, 9974, 6610, 10270, 6610);
            d.Rua("Fir Street", 7f, 10044, 6890, 10270, 6890);
            d.Rua("Cedar Street", 7f, 10270, 6560, 10270, 7090);
            d.Rua("Creek Street", 7f, 9870, 6690, 9870, 7010, 9800, 7110);
            d.Rua("Spruce Lane", 6f, 9870, 6880, 9993, 6880);
            d.Alca("Cabin Loop", 5f, 9800, 7110, 9845, 7165, 9830, 7235, 9770, 7250, 9745, 7190, 9765, 7140);
            d.LigarAsPontas();

            // os especiais (o lado: 1 à esquerda de quem anda do primeiro ponto da rua para o último, -1 à direita)
            d.Especial(UsoDoLote.Posto, null, "East Shore Highway", 9978, 6470, -1, 44, 36, 3f);
            // o terreno da loja de armas tem 32 × 49,5 m (o estacionamento na frente)
            d.Especial(UsoDoLote.LojaDeArmas, null, "East Shore Highway", 9976, 6550, -1, 36, 52, 3f);
            d.Especial(UsoDoLote.Especial, "motel", "East Shore Highway", 9978, 6505, 1, 46, 32, 4f);
            // o terreno do hospital tem 82,5 × 51,5 m (a praça, o estacionamento e o acesso das ambulâncias)
            d.Especial(UsoDoLote.Hospital, null, "Pine Street", 10125, 6610, 1, 96, 62, 3f);
            d.Especial(UsoDoLote.Especial, "igreja", "Pine Street", 10228, 6610, 1, 28, 40, 6f);
            d.Especial(UsoDoLote.Especial, "escola", "Cedar Street", 10270, 6985, 1, 60, 50, 6f);
            d.Especial(UsoDoLote.Especial, "bombeiros", "North Shore Highway", 9994, 6950, 1, 30, 34, 3f);
            d.Especial(UsoDoLote.Especial, "guarda florestal", "North Shore Highway", 9996, 7200, -1, 28, 30, 5f);

            var sorte = new System.Random(SementeDosVazios);
            Rect Faixa(float zMin, float zMax, float xMin = -1e6f, float xMax = 1e6f) => Rect.MinMaxRect(xMin, zMin, xMax, zMax);
            // o comércio, nos dois lados da Main Street (as lojas quase encostadas, como numa rua principal)
            d.Encher("East Shore Highway", UsoDoLote.Loja, 24, 30, 2.5f, 2f, 0f, sorte, 3, Faixa(6600, 6700));
            d.Encher("North Shore Highway", UsoDoLote.Loja, 24, 30, 2.5f, 2f, 0f, sorte, 3, Faixa(6690, 6830));
            // as casas (12/11/2026, a pedido: "as casas têm muito espaçamento entre elas"): os lotes de 24 m (quase todas
            // as casas do kit cabem; a média tem 18 m de frente), 2 m entre um e outro, a frente a 3 m da beira da rua (5 nas
            // rodovias), 32 m de fundo e um em cada oito vazio (eram 26 × 38 m, 8 m entre eles, a 7 m da rua e um em quatro)
            const float L = 24f, folga = 2f, vazio = 0.12f;
            d.Encher("Pine Street", UsoDoLote.Casa, L, 32, 3f, folga, vazio, sorte);
            d.Encher("Fir Street", UsoDoLote.Casa, L, 32, 3f, folga, vazio, sorte);
            d.Encher("Cedar Street", UsoDoLote.Casa, L, 34, 3f, folga, vazio, sorte);
            d.Encher("Crowley Road", UsoDoLote.Casa, L, 32, 5f, folga, vazio, sorte, 2, Faixa(6900, 7200));
            d.Encher("Creek Street", UsoDoLote.Casa, L, 32, 3f, folga, vazio, sorte);
            d.Encher("Spruce Lane", UsoDoLote.Casa, L, 30, 3f, folga, 0.08f, sorte);
            d.Encher("North Shore Highway", UsoDoLote.Casa, L, 32, 5f, folga, vazio, sorte, 3, Faixa(6880, 7150));
            d.Encher("Sawmill Road", UsoDoLote.Casa, L, 32, 5f, folga, vazio, sorte, 3, Faixa(-1e6f, 1e6f, 9640, 9950));
            d.Encher("East Shore Highway", UsoDoLote.Casa, L, 32, 5f, folga, vazio, sorte, 3, Faixa(6420, 6560));
            // as cabanas, na alça do noroeste, na beira do Wood Creek
            d.Encher("Cabin Loop", UsoDoLote.Cabana, 12, 14, 5f, 10f, 0f, sorte);
            return d;
        }
    }
}
