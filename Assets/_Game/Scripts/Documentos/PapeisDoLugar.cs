using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Mundo;
using Forma = ZombieCityCleaner.Mundo.DocumentosDoHospital.Forma;

namespace ZombieCityCleaner.Documentos
{
    /// <summary>
    /// Os papéis que se leem num lugar (o Hospital Principal: o Plano_Hospital.md da pasta de arte, a parte 6), no espaço
    /// deste objeto (o da obra): ao começar, cada um vira um objeto com a malha dele (<see cref="MalhaDoPapel"/>), o material
    /// dos papéis (<c>Resources/Documentos/M_Papeis</c>) e o gatilho da mira (<see cref="PapelNaMira"/>, "[E] Ler"). O texto
    /// vem do <see cref="arquivo"/>, pelo id. Nada vai pela rede: o lugar é fixo e cada jogador lê o seu.
    /// </summary>
    public class PapeisDoLugar : MonoBehaviour
    {
        [Serializable]
        public struct Papel
        {
            public string id;
            public Forma forma;
            public Vector3 lugar;
            public Quaternion giro;
            public bool naParede;
            [Tooltip("A caixa de colisão do móvel embaixo do papel (a mira passa por ela até ele).")]
            public bool temCaixa;
            public Vector3 caixaMeio, caixaTamanho;
            public float caixaGiro;
        }

        [Tooltip("O arquivo dos textos em Resources (sem a extensão).")]
        public string arquivo = "Documentos/Hospital";
        public Material material;
        public List<Papel> papeis = new List<Papel>();

        /// <summary>Os papéis feitos, pelo id (para o F1 e as fotos).</summary>
        public readonly Dictionary<string, PapelNaMira> Feitos = new Dictionary<string, PapelNaMira>();

        void Start() => Fazer();

        public void Fazer()
        {
            foreach (var p in Feitos.Values) if (p != null) Destroy(p.gameObject);
            Feitos.Clear();
            if (material == null) material = Resources.Load<Material>("Documentos/M_Papeis");
            var textos = Documento.DoArquivo(arquivo);
            foreach (var p in papeis)
            {
                if (!textos.TryGetValue(p.id, out var doc))
                {
                    Debug.LogWarning($"[Documentos] O papel \"{p.id}\" não tem texto em Resources/{arquivo}.txt.");
                    continue;
                }
                Feitos[p.id] = Um(p, doc, transform, material);
            }
        }

        /// <summary>O papel de um lugar do <c>DocumentosDoHospital</c> (as coordenadas da obra).</summary>
        public static Papel De(DocumentosDoHospital.Papel p) => new Papel
        {
            id = p.id, forma = p.forma, lugar = p.lugar, giro = p.giro, naParede = p.naParede,
            temCaixa = p.temCaixa, caixaMeio = p.caixaMeio, caixaTamanho = p.caixaTamanho, caixaGiro = p.caixaGiro,
        };

        /// <summary>
        /// Um papel no espaço de 'pai' (o da obra): a malha, o material, o gatilho e a mira, com a caixa do móvel embaixo no
        /// mundo (o pai já no lugar dele). O hospital do mundo (o HospitalDoMundo) põe os de cada andar com o andar.
        /// </summary>
        public static PapelNaMira Um(Papel p, Documento doc, Transform pai, Material material)
        {
            var go = new GameObject("Papel " + p.id);
            go.transform.SetParent(pai, false);
            go.transform.SetLocalPositionAndRotation(p.lugar, p.giro);
            go.AddComponent<MeshFilter>().sharedMesh = MalhaDoPapel.De(p.forma, doc.letra);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material != null ? material : Resources.Load<Material>("Documentos/M_Papeis");
            r.shadowCastingMode = ShadowCastingMode.Off;
            var t = DocumentosDoHospital.Tamanho(p.forma);
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            // o tamanho é (a largura, a altura da página, a grossura); no papel, a grossura vai em y e a página em z
            col.size = new Vector3(t.x + 0.05f, Mathf.Max(t.z, 0.01f) + 0.05f, t.y + 0.05f);
            var mira = go.AddComponent<PapelNaMira>();
            mira.documento = doc;
            mira.forma = p.forma;
            if (p.temCaixa)
            {
                var caixa = pai.localToWorldMatrix * Matrix4x4.TRS(p.caixaMeio, Quaternion.Euler(0f, p.caixaGiro, 0f), Vector3.one);
                mira.temCaixa = true;
                mira.paraACaixa = caixa.inverse;
                mira.caixa = Vector3.Scale(p.caixaTamanho, pai.lossyScale);
            }
            return mira;
        }
    }
}
