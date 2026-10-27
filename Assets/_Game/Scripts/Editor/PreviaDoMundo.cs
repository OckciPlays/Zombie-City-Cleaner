using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// A prévia do mundo no Editor, sem dar Play (Plano_Mapa, Partes M1 e M2): gera o mundo de uma semente e mostra na
    /// cena aberta o mapa inteiro de uma vez (o terreno em células de 32 m, com as texturas do chão, e o lago), o terreno da
    /// partida, um marco alto em cima dela e a borda. Menu Zombie City Cleaner > Mundo > Prévia com semente. A prévia não é
    /// salva com a cena.
    /// </summary>
    public class PreviaDoMundo : EditorWindow
    {
        const string NomeDaRaiz = "Prévia do mundo";
        int _semente = 1;
        TamanhoDoMundo _tamanho = TamanhoDoMundo.Grande;
        string _resumo;

        [MenuItem("Zombie City Cleaner/Mundo/Prévia com semente")]
        static void Abrir() => GetWindow<PreviaDoMundo>("Prévia do mundo");

        void OnGUI()
        {
            EditorGUILayout.HelpBox("Gera o mundo sem dar Play e mostra o mapa inteiro na cena aberta: o terreno (de 32 em 32 m) e o lago, " +
                                    "o terreno da partida com um marco alto e a borda. A prévia não é salva com a cena.", MessageType.None);
            _semente = Mathf.Max(1, EditorGUILayout.IntField("Semente", _semente));
            _tamanho = (TamanhoDoMundo)EditorGUILayout.EnumPopup("Tamanho", _tamanho);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Gerar a prévia")) _resumo = Gerar(_semente, _tamanho);
            if (GUILayout.Button("Semente nova"))
            {
                _semente = GeradorDoMundo.NovaSemente();
                _resumo = Gerar(_semente, _tamanho);
            }
            if (GUILayout.Button("Apagar a prévia"))
            {
                Apagar();
                _resumo = null;
            }
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(_resumo)) EditorGUILayout.HelpBox(_resumo, MessageType.Info);
        }

        /// <summary>Gera e monta a prévia na cena aberta; devolve o resumo para a janela (e o log).</summary>
        public static string Gerar(int semente, TamanhoDoMundo tamanho)
        {
            float t0 = Time.realtimeSinceStartup;
            var d = GeradorDoMundo.Gerar(semente, tamanho);
            float geracao = (Time.realtimeSinceStartup - t0) * 1000f;
            Apagar();
            var cidade = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/Cidade.mat");
            var raiz = new GameObject(NomeDaRaiz) { hideFlags = HideFlags.DontSave };
            void Peca(string nome, Mesh malha, Material material)
            {
                if (malha == null) return;
                malha.hideFlags = HideFlags.DontSave;
                var go = new GameObject(nome) { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(raiz.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = malha;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
            t0 = Time.realtimeSinceStartup;
            Peca("Terreno", MalhasDoTerreno.Mapa(d, 32f), AssetDatabase.LoadAssetAtPath<Material>(TexturasDoChao.MaterialDoChao));
            Peca("Lago", MalhasDoTerreno.AguaDoMapa(d, 8f), AssetDatabase.LoadAssetAtPath<Material>(TexturasDoChao.MaterialDaAgua));
            Peca("Partida", MalhasDoTerreno.Partida(d), cidade);
            // o marco em cima da partida (para achar de longe) e a borda do mapa
            var obra = new MalhaDeObra();
            obra.Caixa(Matrix4x4.Translate(d.Partida + Vector3.up * 30f), new Vector3(2f, 60f, 2f), "PlacaAmarela");
            float m = d.Metade, alto = GeradorDoMundo.Base + d.AlturaDaSerra + 60f;
            obra.Caixa(Matrix4x4.Translate(new Vector3(0f, alto / 2f, m)), new Vector3(d.Lado, alto, 4f), "PlacaVermelha");
            obra.Caixa(Matrix4x4.Translate(new Vector3(0f, alto / 2f, -m)), new Vector3(d.Lado, alto, 4f), "PlacaVermelha");
            obra.Caixa(Matrix4x4.Translate(new Vector3(m, alto / 2f, 0f)), new Vector3(4f, alto, d.Lado), "PlacaVermelha");
            obra.Caixa(Matrix4x4.Translate(new Vector3(-m, alto / 2f, 0f)), new Vector3(4f, alto, d.Lado), "PlacaVermelha");
            Peca("Marco e borda", obra.ParaMesh("Prévia do marco e da borda"), cidade);
            float malhas = (Time.realtimeSinceStartup - t0) * 1000f;

            var vista = SceneView.lastActiveSceneView;
            if (vista != null)
            {
                vista.LookAt(d.Partida + Vector3.up * 20f, Quaternion.Euler(35f, d.GiroDaPartida + 180f, 0f), 120f);
                vista.Repaint();
            }
            string resumo = $"\"{Tamanhos.NomeComLado(tamanho)}\", semente {semente}, gerador {GeradorDoMundo.Versao}: gerado em {geracao:F0} ms " +
                            $"(a prévia em {malhas:F0} ms). {d.BlocosPorLado} × {d.BlocosPorLado} blocos de 64 m. " +
                            $"Lago em ({d.Lago.Centro.x:F0}, {d.Lago.Centro.y:F0}), raio {d.Lago.Raio:F0} m, água a {d.Lago.Nivel:F1} m. " +
                            $"Partida em ({d.Partida.x:F1}, {d.Partida.y:F1}, {d.Partida.z:F1}), olhando para {d.GiroDaPartida:F0}°. Resumo {d.Resumo:X16}.";
            Debug.Log("[Prévia do mundo] " + resumo);
            return resumo;
        }

        public static void Apagar()
        {
            // o objeto sai antes das malhas: com o GPU Resident Drawer, um renderer com a malha destruída dá erro de desenho
            GameObject velho;
            while ((velho = GameObject.Find(NomeDaRaiz)) != null)
            {
                var malhas = new System.Collections.Generic.List<Mesh>();
                foreach (var f in velho.GetComponentsInChildren<MeshFilter>(true)) malhas.Add(f.sharedMesh);
                DestroyImmediate(velho);
                foreach (var m in malhas) DestroyImmediate(m);
            }
        }
    }
}
