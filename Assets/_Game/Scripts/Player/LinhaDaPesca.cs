using UnityEngine;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A boia e a linha da pesca (07/11/2026), desenhadas no mundo: a boia (o <c>Resources/Boia</c>) e a linha da ponta da
    /// vara até ela, com a barriga de quem está frouxa (a linha esperando cai um pouco; puxando, fica esticada). Quem pesca
    /// (<see cref="PescaJogador"/>) e quem olha (<see cref="PescaVista"/>) usam esta mesma.
    /// <para>
    /// A ponta da vara: no corpo, a ponta da malha da vara na mão (<see cref="PontaNoCorpo"/>). Na primeira pessoa, a vara é
    /// desenhada pela câmera dos braços, perto da origem e com outro campo de visão (<see cref="BracosNaOrigem"/>): a linha
    /// sai do ponto do mundo que cai, na tela, onde a ponta aparece (<see cref="PontaNaTela"/>). Antes, ela saía de um
    /// ponto chutado na frente do olho, longe da vara.
    /// </para>
    /// </summary>
    public class LinhaDaPesca
    {
        const int Pontos = 14;
        GameObject _boia;
        LineRenderer _linha;
        static readonly Vector3[] _caminho = new Vector3[Pontos];

        public bool Visivel => _boia != null && _boia.activeSelf;
        /// <summary>O ponto da boia (o da linha d'água dela) do último desenho.</summary>
        public Vector3 PosicaoDaBoia => _boia != null ? _ponto : Vector3.zero;
        Vector3 _ponto;

        /// <summary>
        /// Quanto da boia fica fora d'água (m), parada: da linha d'água dela até o topo do desenho (27/11/2026, a pesca nova:
        /// a beliscada afunda uma parte disso, e a mordida leva tudo para baixo). Cria o desenho, se ainda não existir.
        /// </summary>
        public float AcimaDaAgua
        {
            get
            {
                Criar();
                return _acima;
            }
        }
        float _acima = 0.04f, _calado;

        /// <summary>
        /// A parte da altura da boia que fica embaixo d'água, parada (27/11/2026, as fotos da pesca nova: ela ficava inteira em
        /// cima d'água, como apoiada nela). Na boia do jogo, a metade branca de baixo afunda e a vermelha fica de fora.
        /// </summary>
        const float FracaoNaAgua = 0.4f;

        /// <summary>
        /// A boia em 'boia' (o ponto da linha d'água dela: na tona, parada) e a linha de 'ponta' até ela; 'folga' de 0
        /// (esticada) a 1 (frouxa).
        /// </summary>
        public void Desenhar(Vector3 ponta, Vector3 boia, float folga)
        {
            Criar();
            _boia.SetActive(true);
            _ponto = boia;
            _boia.transform.position = boia + Vector3.down * _calado;
            _linha.enabled = true;
            Vector3 topo = boia + Vector3.up * (_acima * 0.6f);
            float comprimento = Vector3.Distance(ponta, topo);
            float barriga = Mathf.Clamp01(folga) * Mathf.Min(1.4f, comprimento * 0.09f);
            for (int i = 0; i < Pontos; i++)
            {
                float s = i / (Pontos - 1f);
                _caminho[i] = Vector3.Lerp(ponta, topo, s) + Vector3.down * (barriga * 4f * s * (1f - s));
            }
            _linha.SetPositions(_caminho);
        }

        public void Esconder()
        {
            if (_boia != null) _boia.SetActive(false);
            if (_linha != null) _linha.enabled = false;
        }

        public void Destruir()
        {
            if (_boia != null) Object.Destroy(_boia);
            if (_linha != null) Object.Destroy(_linha.gameObject);
        }

        void Criar()
        {
            if (_boia != null) return;
            var prefab = Resources.Load<GameObject>("Boia");
            if (prefab != null) _boia = Object.Instantiate(prefab);
            else
            {
                _boia = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _boia.transform.localScale = Vector3.one * 0.07f;
                _boia.GetComponent<Renderer>().material.color = new Color(0.9f, 0.15f, 0.12f);
            }
            foreach (var c in _boia.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            _boia.name = "Boia da pesca";
            // a linha d'água e o que fica fora d'água, pelo desenho (a boia nasce na origem; até 25 cm, por segurança)
            var desenhos = _boia.GetComponentsInChildren<Renderer>();
            if (desenhos.Length > 0)
            {
                var caixa = desenhos[0].bounds;
                for (int i = 1; i < desenhos.Length; i++) caixa.Encapsulate(desenhos[i].bounds);
                float baixo = caixa.min.y - _boia.transform.position.y, alto = Mathf.Min(caixa.max.y - _boia.transform.position.y, baixo + 0.25f);
                _calado = baixo + FracaoNaAgua * (alto - baixo);
                _acima = Mathf.Max(0.01f, alto - _calado);
            }
            var go = new GameObject("Linha da pesca");
            _linha = go.AddComponent<LineRenderer>();
            _linha.positionCount = Pontos;
            _linha.widthMultiplier = 0.006f;
            _linha.numCapVertices = 0;
            _linha.material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.92f, 0.92f, 0.88f, 1f) };
            _linha.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _linha.receiveShadows = false;
        }

        // ------------------------------------------------------------------ a ponta da vara

        /// <summary>A ponta da vara na mão do corpo (o que os outros veem; e o dono, na terceira pessoa).</summary>
        public static bool PontaNoCorpo(JogadorRede rede, out Vector3 ponta)
        {
            ponta = default;
            if (rede == null) return false;
            // a vara nova (07/11/2026): a ponta dela, presa à mão direita do corpo
            var nova = rede.GetComponent<VaraNoCorpo>();
            if (nova != null && nova.Ligada) return nova.Ponta(out ponta);
            if (rede.naMaoCorpo == null) return false;
            int m = Itens.MaoDe(Item.VaraDePesca);
            if (m < 0 || m >= rede.naMaoCorpo.Length || rede.naMaoCorpo[m] == null || !rede.naMaoCorpo[m].activeInHierarchy) return false;
            return PontaDaVara(rede.naMaoCorpo[m], out ponta);
        }

        /// <summary>
        /// Na primeira pessoa: o ponto do mundo, a 'longe' m da câmera do mundo, que cai na tela onde a ponta da vara dos
        /// braços aparece. Sem os braços (ou a ponta atrás da câmera), um ponto acima e à direita do olhar.
        /// </summary>
        public static Vector3 PontaNaTela(JogadorRede rede, ArmaJogador arma)
        {
            var mundo = rede.cameraMundo;
            var bracos = rede.cameraBracos;
            // os braços com a vara nova (07/11/2026): o osso da ponta deles
            if (mundo != null && bracos != null && arma != null && arma.PontaDaVaraNosBracos(out var daNova))
            {
                var tela = bracos.WorldToScreenPoint(daNova);
                if (tela.z > 0.05f) return mundo.ScreenToWorldPoint(new Vector3(tela.x, tela.y, Mathf.Max(0.35f, tela.z)));
            }
            int m = Itens.ModeloNaMao(Item.VaraDePesca);
            if (mundo != null && bracos != null && arma != null && arma.modelos != null && m >= 0 && m < arma.modelos.Length
                && arma.modelos[m] != null && arma.modelos[m].activeInHierarchy && PontaDaVara(arma.modelos[m], out var ponta))
            {
                var tela = bracos.WorldToScreenPoint(ponta);
                if (tela.z > 0.05f) return mundo.ScreenToWorldPoint(new Vector3(tela.x, tela.y, Mathf.Max(0.35f, tela.z)));
            }
            var olho = rede.Olho;
            return olho.position + olho.forward * 1.2f + olho.right * 0.25f + olho.up * 0.7f;
        }

        // a ponta da malha da vara (a origem é a pegada; a ponta, para +Z: o VaraNaMao)
        static bool PontaDaVara(GameObject modelo, out Vector3 ponta)
        {
            ponta = default;
            var vara = modelo.GetComponentInChildren<VaraNaMao>(true);
            if (vara == null || !vara.isActiveAndEnabled) return false;
            var filtro = vara.GetComponent<MeshFilter>();
            float z = filtro != null && filtro.sharedMesh != null ? filtro.sharedMesh.bounds.max.z : 1.5f;
            ponta = vara.transform.TransformPoint(new Vector3(0f, 0f, z));
            return true;
        }
    }
}
