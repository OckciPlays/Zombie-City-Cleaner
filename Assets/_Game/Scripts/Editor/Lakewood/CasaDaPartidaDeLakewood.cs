using System.Linq;
using System.Text;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A casa da partida do Lakewood (10/11/2026, a pedido: "coloque para o jogador nascer perto de alguma estrada, pode
    /// ser a estrada que ajustamos que vai para a represa... uma casa perto da estrada... sem mobília, só com mesas e
    /// armas"): o lugar era achado na South Shore Highway (<see cref="Achar"/>: nos primeiros 2,2 km depois da barragem, o
    /// trecho reto mais plano, com o chão na altura da pista e longe da água, dos rios, da usina e das outras estradas).
    /// Desde 12/11/2026 (a pedido: "vamos mudar a casa de spawn hoje para a casa perto do lago, assim a gente nasce perto da
    /// cidade"), ela fica no lote de casa da Woodcreek mais perto do lago (<see cref="NaBeiraDoLago"/>: o lote 114, na
    /// Sawmill Road), que fica vazio. Grava no mapa (<see cref="MapaDeLakewood.casaDaPartida"/>; a partida vai para o
    /// terreiro) e acerta a cena aberta: os pontos de nascimento, a câmera do menu, os veículos e a mesa.
    /// </summary>
    public static class CasaDaPartidaDeLakewood
    {
        const string Estrada = "South Shore Highway";
        const float AteMetros = 2200f;
        const string Cidade = "Woodcreek";
        const string PastaDosVeiculos = "Assets/_Game/Prefabs/Veiculos/";
        static readonly string[] Veiculos = { "Carro", "Cupe", "Moto_Cross", "Moto_FatBoy" };
        const string Mesa = MontagemDaMobiliaNova.PastaDosPrefabs + "/Mesa6.prefab";

        [MenuItem("Zombie City Cleaner/Lakewood/A casa da partida (perto do lago, na Woodcreek)", false, 6)]
        public static void Fazer()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Saia do Play antes de pôr a casa da partida.");
                return;
            }
            var mapa = MontagemDeLakewood.Carregar(false);
            if (mapa == null || mapa.estradas.Count == 0)
            {
                Debug.LogWarning("Falta o mapa com as estradas (Etapa 5: traçar as estradas).");
                return;
            }
            var relatorio = new StringBuilder();
            var chao = new ChaoDeLakewood(mapa, new RelevoDeLakewood(mapa));
            Undo.RecordObject(mapa, "A casa da partida");
            if (!NaBeiraDoLago(mapa, chao, relatorio))
            {
                Debug.LogWarning("[Lakewood] A casa da partida: nenhum lugar serve. " + relatorio);
                return;
            }
            EditorUtility.SetDirty(mapa);
            AssetDatabase.SaveAssets();
            // o chão de novo, agora com o terreiro
            chao = new ChaoDeLakewood(mapa, new RelevoDeLakewood(mapa));
            var cena = EditorSceneManager.GetActiveScene();
            var lakewood = Object.FindAnyObjectByType<LakewoodNaCena>();
            if (cena.path == MontagemDeLakewood.Cena && lakewood != null)
            {
                PorNaCena(cena, chao, lakewood);
                EditorSceneManager.SaveScene(cena);
                relatorio.Append(" A cena foi acertada e salva.");
            }
            else relatorio.Append(" Abra a cena do Lakewood e rode de novo (ou o 3. Montar a cena) para acertar a cena.");
            Debug.Log("[Lakewood] A casa da partida: " + relatorio);
        }

        /// <summary>
        /// O lugar perto do lago (12/11/2026): o lote de casa da Woodcreek mais perto da água (a casa da partida antiga, se
        /// já tomou um, conta), de frente para a estrada dele. O ponto da estrada é o mais perto do meio do lote, e a casa fica
        /// do lado do lote, na perpendicular da estrada. O lote fica vazio (sem a casa da cidade e sem o patamar).
        /// </summary>
        public static bool NaBeiraDoLago(MapaDeLakewood mapa, ChaoDeLakewood chao, StringBuilder relatorio)
        {
            LoteDoMapa melhor = null;
            float perto = float.MaxValue;
            foreach (var l in mapa.lotes)
            {
                if (l.cidade != Cidade) continue;
                if (l.tipo != UsoDoLote.Casa && !(mapa.casaDaPartida != null && mapa.casaDaPartida.TomaOLote(l))) continue;
                float d = chao.DistanciaDaAgua(l.meio.x, l.meio.y, 400f, out _);
                if (d < perto)
                {
                    perto = d;
                    melhor = l;
                }
            }
            if (melhor == null)
            {
                relatorio.Append($"Sem lote de casa na {Cidade} (Etapa 6: a Woodcreek).");
                return false;
            }
            var est = chao.Estradas.Find(e => e.Via.nome == melhor.rua);
            if (est == null)
            {
                relatorio.Append($"Sem a estrada do lote ({melhor.rua}).");
                return false;
            }
            int k = 0;
            float menor = float.MaxValue;
            for (int i = 3; i < est.Pontos.Length - 3; i++)
            {
                float d = Vector2.Distance(est.Pontos[i], melhor.meio);
                if (d < menor)
                {
                    menor = d;
                    k = i;
                }
            }
            var t = (est.Pontos[k + 3] - est.Pontos[k - 3]).normalized;
            var n = new Vector2(-t.y, t.x);
            if (Vector2.Dot(n, melhor.meio - est.Pontos[k]) < 0f) n = -n;
            var c2 = mapa.casaDaPartida ?? new CasaDaPartida();
            c2.tem = true;
            c2.naEstrada = est.Pontos[k];
            c2.paraACasa = n;
            c2.estrada = melhor.rua;
            mapa.casaDaPartida = c2;
            int vazios = 0;
            foreach (var l in mapa.lotes)
                if (l.cidade == Cidade && l.tipo == UsoDoLote.Casa && c2.TomaOLote(l))
                {
                    l.tipo = UsoDoLote.Vazio;
                    vazios++;
                }
            var lugar = new ChaoDeLakewood(mapa, chao.Relevo).Partida;
            if (lugar != null) mapa.partida = lugar.NoMapa(LugarDaPartida.PartidasEmA[1], LugarDaPartida.LinhaDaPartida);
            relatorio.Append($"na {Cidade}, no lote de casa mais perto do lago (a água a {perto:F0} m do meio dele), na {melhor.rua}, em ({c2.naEstrada.x:F0}; {c2.naEstrada.y:F0}), setor {mapa.NomeDoSetorEm(c2.naEstrada.x, c2.naEstrada.y)}; {vazios} lote(s) de casa ficaram vazios.");
            if (lugar != null) relatorio.Append($" A casa: {lugar.Planta.molde} de {lugar.Planta.W:F1} × {lugar.Planta.D:F1} m; o terreiro de {lugar.Terreiro.width:F0} × {lugar.Terreiro.height:F0} m a {lugar.Altura:F1} m.");
            else relatorio.Append(" (sem o terreiro: a estrada não foi achada no ponto)");
            return lugar != null;
        }

        /// <summary>
        /// O lugar antigo, na South Shore (10/11/2026): em cada ponto da estrada (de 2 em 2), dos dois lados, o terreiro e a casa (de 10 a 44 m da linha do meio,
        /// 15 m para cada lado) no relevo; a nota é a diferença do mais alto para o mais baixo mais 1,5 vez a diferença da
        /// média para a pista (o terreiro fica na altura dela). Fora: a curva de mais de 10° em 48 m, a água a menos de 15 m,
        /// um rio a 20 m, a caixa da usina e outra estrada a menos de 60 m do meio.
        /// </summary>
        public static bool Achar(MapaDeLakewood mapa, ChaoDeLakewood chao, StringBuilder relatorio)
        {
            var est = chao.Estradas.Find(e => e.Via.nome == Estrada);
            if (est == null)
            {
                relatorio.Append($"Sem a {Estrada}.");
                return false;
            }
            var relevo = chao.Relevo;
            float melhor = float.MaxValue;
            int qual = -1;
            Vector2 fora = default;
            for (int k = 15; k < est.Pontos.Length - 15; k += 2)
            {
                if (est.AoLongo[k] > AteMetros) break;
                if (est.Ponte[k] || est.Ponte[k - 6] || est.Ponte[k + 6]) continue;
                var t = (est.Pontos[k + 3] - est.Pontos[k - 3]).normalized;
                float curva = Vector2.Angle(est.Pontos[k] - est.Pontos[k - 6], est.Pontos[k + 6] - est.Pontos[k]);
                if (curva > 10f) continue;
                float hr = est.Alturas[k];
                for (int s = -1; s <= 1; s += 2)
                {
                    var n = new Vector2(-t.y, t.x) * s;
                    float mn = float.MaxValue, mx = float.MinValue, soma = 0f;
                    int c = 0;
                    bool ruim = false;
                    for (float d = 10f; d <= 44f && !ruim; d += 4f)
                    for (float a = -15f; a <= 15f; a += 5f)
                    {
                        var p = est.Pontos[k] + n * d + t * a;
                        if (!mapa.Dentro(p.x, p.y) || chao.Agua(p.x, p.y, out _, 15f) || chao.RioMaisPerto(p.x, p.y, 20f, out _) || chao.Usina.NaCaixa(p.x, p.y))
                        {
                            ruim = true;
                            break;
                        }
                        float h = relevo.Altura(p.x, p.y);
                        mn = Mathf.Min(mn, h);
                        mx = Mathf.Max(mx, h);
                        soma += h;
                        c++;
                    }
                    if (ruim) continue;
                    var meio = est.Pontos[k] + n * 27f;
                    bool outra = false;
                    foreach (var e2 in chao.Estradas)
                    {
                        if (e2 == est) continue;
                        for (int q = 0; q < e2.Pontos.Length && !outra; q += 2) outra = Vector2.Distance(e2.Pontos[q], meio) < 60f;
                        if (outra) break;
                    }
                    if (outra) continue;
                    float nota = (mx - mn) + 1.5f * Mathf.Abs(soma / c - hr);
                    if (nota >= melhor) continue;
                    melhor = nota;
                    qual = k;
                    fora = n;
                }
            }
            if (qual < 0)
            {
                relatorio.Append($"Nenhum trecho da {Estrada} serve nos primeiros {AteMetros:F0} m.");
                return false;
            }
            var c2 = mapa.casaDaPartida ?? new CasaDaPartida();
            c2.tem = true;
            c2.naEstrada = est.Pontos[qual];
            c2.paraACasa = fora;
            c2.estrada = Estrada;
            mapa.casaDaPartida = c2;
            var lugar = new ChaoDeLakewood(mapa, chao.Relevo).Partida;
            if (lugar != null) mapa.partida = lugar.NoMapa(LugarDaPartida.PartidasEmA[1], LugarDaPartida.LinhaDaPartida);
            relatorio.Append($"na {Estrada}, a {est.AoLongo[qual]:F0} m da cabeceira leste da barragem, em ({c2.naEstrada.x:F0}; {c2.naEstrada.y:F0}), setor {mapa.NomeDoSetorEm(c2.naEstrada.x, c2.naEstrada.y)} (a nota {melhor:F2}: o relevo e a pista quase na mesma altura).");
            if (lugar != null) relatorio.Append($" A casa: {lugar.Planta.molde} de {lugar.Planta.W:F1} × {lugar.Planta.D:F1} m; o terreiro de {lugar.Terreiro.width:F0} × {lugar.Terreiro.height:F0} m a {lugar.Altura:F1} m.");
            return true;
        }

        /// <summary>
        /// A casa da partida na cena (com ela no mapa): os pontos de nascimento no terreiro (de frente para a casa), a partida
        /// e a câmera do menu (olhando a casa), os quatro veículos e a mesa no LakewoodNaCena, e os veículos na lista da rede.
        /// </summary>
        public static void PorNaCena(UnityEngine.SceneManagement.Scene cena, ChaoDeLakewood chao, LakewoodNaCena lakewood)
        {
            var lugar = chao.Partida;
            if (lugar == null) return;
            var pontos = cena.GetRootGameObjects().FirstOrDefault(g => g.name == "Pontos de nascimento");
            if (pontos != null)
            {
                int i = 0;
                foreach (Transform p in pontos.transform)
                {
                    float a = LugarDaPartida.PartidasEmA[Mathf.Min(i, LugarDaPartida.PartidasEmA.Length - 1)];
                    Undo.RecordObject(p, "A casa da partida");
                    p.SetPositionAndRotation(lugar.NoMundo(a, LugarDaPartida.LinhaDaPartida, 0.1f), Quaternion.Euler(0f, lugar.GiroDaCasa, 0f));
                    i++;
                }
            }
            var camera = cena.GetRootGameObjects().FirstOrDefault(g => g.name == "Câmera do menu");
            if (camera != null)
            {
                Undo.RecordObject(camera.transform, "A casa da partida");
                var olho = lugar.NoMundo(10f, 11f, 4.5f);
                camera.transform.SetPositionAndRotation(olho, Quaternion.LookRotation(lugar.PosicaoDaCasa + Vector3.up * 2.5f - olho));
            }
            Undo.RecordObject(lakewood, "A casa da partida");
            lakewood.partida = lugar.NoMundo(0f, LugarDaPartida.LinhaDaPartida);
            lakewood.veiculos = Veiculos.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(PastaDosVeiculos + n + ".prefab")).ToArray();
            lakewood.mesaDaPartida = AssetDatabase.LoadAssetAtPath<GameObject>(Mesa);
            EditorUtility.SetDirty(lakewood);
            int faltam = lakewood.veiculos.Count(v => v == null);
            if (faltam > 0) Debug.LogWarning($"[Lakewood] A casa da partida: faltam {faltam} veículos em {PastaDosVeiculos}.");
            if (lakewood.mesaDaPartida == null) Debug.LogWarning($"[Lakewood] A casa da partida: falta a mesa ({Mesa}: Mobília nova > Montar).");
            // os veículos na lista dos prefabs da rede (o anfitrião cria)
            var lista = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            if (lista != null)
                foreach (var v in lakewood.veiculos)
                    if (v != null && !lista.PrefabList.Any(p => p.Prefab == v))
                    {
                        lista.Add(new NetworkPrefab { Prefab = v });
                        EditorUtility.SetDirty(lista);
                    }
            EditorSceneManager.MarkSceneDirty(cena);
        }
    }
}
