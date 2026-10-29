using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Põe no corpo do jogador o que ele leva: a mochila nas costas, o coldre no quadril direito e as armas penduradas
    /// (sem mochila, a da alça na diagonal das costas; com ela, as duas presas nas laterais da mochila; a pistola da alça
    /// no cós e a curta no coldre). Só visual.
    /// As posições são medidas no corpo em repouso (o corpo do personagem no prefab, sem animação, a <c>referencia</c>) e
    /// presas aos ossos (o peito e o quadril), então acompanham a animação em qualquer pose. As medidas do corpo (as costas,
    /// o cinto, o quadril...) são as do personagem (<see cref="CorpoDoPersonagem"/>): o gordo tem as costas mais para trás.
    /// Serve o retrato do inventário, o boneco de teste do menu do desenvolvedor e, desde 28/10/2026, o corpo dos jogadores
    /// (<see cref="EquipamentoDoJogador"/>). O que vai no corpo sai do inventário por <see cref="OQueVeste"/>.
    /// </summary>
    public class EquipamentoNoCorpo : MonoBehaviour
    {
        // medidas do corpo em repouso (o espaço do corpo, olhando para +Z), as do personagem (CorpoDoPersonagem)
        float Costas = -0.115f;        // a superfície das costas, na altura do peito
        float TopoDaMochila = 1.45f;   // logo abaixo da altura dos ombros
        float CentroDasCostas = 1.15f; // onde a arma da alça cruza as costas
        float Cinto = 0.97f;
        float LadoDoQuadril = 0.18f;
        const float InclinacaoDaAlca = 35f;  // a boca do cano por cima do ombro direito
        const float InclinacaoNaLateral = 6f;     // na lateral da mochila, a boca do cano um pouco para fora
        float TopoDaArmaNaLateral = 1.78f;  // até onde a boca do cano sobe (3 cm abaixo do topo da cabeça)
        // a mochila nas costas é menor que a do chão (a pedido, 28/10/2026: "muito grandes em relação ao personagem"; a
        // grande, de 48 × 84 × 33 cm, era mais larga que o tronco do principal em blocos)
        const float TamanhoDaMochila = 0.75f;

        readonly List<GameObject> _pecas = new List<GameObject>();

        /// <summary>O começo do nome de cada peça: a cópia do corpo (o retrato do inventário) acha e tira as que vieram junto.</summary>
        public const string Prefixo = "Equipamento: ";

        /// <summary>As peças postas agora.</summary>
        public IReadOnlyList<GameObject> Pecas => _pecas;

        /// <summary>
        /// O que vai no corpo, pelo inventário (o que vem pela rede, em qualquer máquina): a mochila e o coldre vestidos e as
        /// armas que não estão na mão, como no porte de armas (a da alça; a longa nas costas; a curta no coldre, se houver,
        /// ou nas costas). Devolve uma chave que muda quando alguma delas muda.
        /// </summary>
        public static long OQueVeste(InventarioJogador inventario, out Item mochila, out bool coldre, out Item alca, out Item costas, out Item cintura)
        {
            mochila = inventario.MochilaVestida.Value;
            coldre = inventario.ComColdre.Value;
            alca = costas = cintura = Item.Nada;
            ushort idNaMao = inventario.IdNaMao;
            Item curta1 = Item.Nada, curta2 = Item.Nada, longa = Item.Nada;
            var lista = inventario.Mochila;
            for (int i = 0; i < lista.Count; i++)
            {
                var it = lista[i];
                if (!Itens.EhArma(it.item) || it.id == idNaMao) continue;
                if (it.naAlca) alca = it.item;
                else if (!Itens.EhCurta(it.item)) { if (longa == Item.Nada) longa = it.item; }
                else if (curta1 == Item.Nada) curta1 = it.item;
                else if (curta2 == Item.Nada) curta2 = it.item;
            }
            costas = longa;
            for (int k = 0; k < 2; k++)
            {
                var c = k == 0 ? curta1 : curta2;
                if (c == Item.Nada) continue;
                if (coldre && cintura == Item.Nada) cintura = c;
                else if (costas == Item.Nada) costas = c;
            }
            return (long)mochila | ((coldre ? 1L : 0L) << 8) | ((long)alca << 16) | ((long)costas << 24) | ((long)cintura << 32);
        }

        /// <summary>Tira as peças que vieram numa cópia do corpo (Instantiate leva os filhos dos ossos, não a lista).</summary>
        public static void TirarCopiadas(Transform raiz)
        {
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith(Prefixo)) continue;
                t.gameObject.SetActive(false);
                Destroy(t.gameObject);
            }
        }

        /// <summary>O material das peças (vazio: o dos itens no chão). O retrato do inventário usa o dele, sem sombra.</summary>
        public Material material;
        public bool semSombra;

        /// <summary>
        /// Veste o corpo. <paramref name="alca"/>, <paramref name="costas"/> e <paramref name="cintura"/> são as armas
        /// (ou <see cref="Item.Nada"/>); a da cintura só aparece com o coldre.
        /// </summary>
        public void Vestir(Transform referencia, Item mochila, bool coldre, Item alca, Item costas, Item cintura)
        {
            Limpar();
            var mundo = ItensNoMundo.Instancia;
            if (mundo == null || referencia == null) return;
            var peito = Osso(transform, "chest");
            var quadril = Osso(transform, "hips");
            if (peito == null || quadril == null) return;
            var medidas = GetComponent<CorpoDoPersonagem>();
            if (medidas == null) medidas = referencia.GetComponent<CorpoDoPersonagem>();
            if (medidas != null)
            {
                Costas = medidas.costas;
                TopoDaMochila = medidas.topoDaMochila;
                CentroDasCostas = medidas.centroDasCostas;
                Cinto = medidas.cinto;
                LadoDoQuadril = medidas.ladoDoQuadril;
                TopoDaArmaNaLateral = medidas.topoDaCabeca - 0.03f;
            }

            float fora = Costas;   // até onde já foi ocupado, para trás
            bool comMochila = false;
            Bounds naMochila = default;   // a caixa da mochila, no espaço do corpo

            // a mochila
            if (Itens.EhMochila(mochila))
            {
                var malha = Malha(mundo, mochila);
                if (malha != null)
                {
                    var b = malha.bounds;
                    float s = TamanhoDaMochila;
                    var pos = new Vector3(-b.center.x * s, TopoDaMochila - b.max.y * s, Costas - 0.005f);
                    Por(referencia, peito, "Mochila", malha, mundo.material, pos, Quaternion.identity, s);
                    fora = pos.z + b.min.z * s;
                    comMochila = true;
                    naMochila = new Bounds(pos + b.center * s, b.size * s);
                }
            }

            // com a mochila, as armas vão presas nas laterais dela: a da alça na direita, a das costas na esquerda
            // (a pistola da alça continua no cós)
            if (comMochila)
            {
                if (costas != Item.Nada) NaLateral(referencia, peito, mundo, costas, naMochila, -1f, "Arma nas costas");
                if (alca != Item.Nada && alca != Item.Pistola) NaLateral(referencia, peito, mundo, alca, naMochila, 1f, "Arma da alça");
            }
            else if (alca != Item.Nada && alca != Item.Pistola)
            {
                // sem a mochila: a arma da alça na diagonal das costas
                var malha = Malha(mundo, alca);
                if (malha != null)
                {
                    var rot = DeitadaNasCostas(InclinacaoDaAlca);
                    float meia = malha.bounds.extents.x;
                    var centro = new Vector3(0f, CentroDasCostas, fora - meia - 0.01f);
                    Por(referencia, peito, "Arma da alça", malha, mundo.material, centro - rot * malha.bounds.center, rot);
                }
            }

            // a pistola da alça: no cós da calça, atrás (o cano para baixo, o cabo para cima e para a direita)
            if (alca == Item.Pistola)
            {
                var malha = Malha(mundo, alca);
                if (malha != null)
                {
                    var rot = Quaternion.LookRotation(Vector3.down, Vector3.left);
                    var centro = new Vector3(0.06f, Cinto - 0.02f, Costas + 0.005f - malha.bounds.extents.x);
                    Por(referencia, quadril, "Arma da alça (cós)", malha, mundo.material, centro - rot * malha.bounds.center, rot);
                }
            }

            // o coldre, no quadril direito, com a arma curta dentro
            if (coldre)
            {
                var malha = Malha(mundo, Item.Coldre);
                Vector3 pos = new Vector3(LadoDoQuadril + 0.03f, Cinto, 0.0f);
                if (malha != null) Por(referencia, quadril, "Coldre", malha, mundo.material, pos, Quaternion.identity);
                if (Itens.EhCurta(cintura))
                {
                    var arma = Malha(mundo, cintura);
                    if (arma != null)
                    {
                        // o cano para baixo e o topo da arma para a frente; o cabo sai por cima, para trás
                        var rot = Quaternion.Euler(90f, 0f, 0f);
                        Por(referencia, quadril, "Arma no coldre", arma, mundo.material, pos + new Vector3(0.002f, 0.025f, -0.02f), rot);
                    }
                }
            }
        }

        /// <summary>
        /// Uma arma presa na lateral da mochila (<paramref name="lado"/> 1 a direita, -1 a esquerda): em pé, o cano para
        /// cima e um pouco para fora, o lado dela encostado na mochila e o topo para a frente. No meio da mochila, mas a
        /// boca do cano não passa muito da cabeça (as longas descem).
        /// </summary>
        void NaLateral(Transform referencia, Transform peito, ItensNoMundo mundo, Item arma, Bounds mochila, float lado, string nome)
        {
            var malha = Malha(mundo, arma);
            if (malha == null) return;
            float t = InclinacaoNaLateral * Mathf.Deg2Rad;
            var rot = Quaternion.LookRotation(new Vector3(lado * Mathf.Sin(t), Mathf.Cos(t), 0f), Vector3.forward);
            var b = malha.bounds;
            float meiaAltura = b.extents.z * Mathf.Cos(t);
            float y = Mathf.Min(mochila.center.y, TopoDaArmaNaLateral - meiaAltura);
            var centro = new Vector3(lado * (mochila.extents.x + b.extents.x + 0.005f), y, mochila.center.z);
            Por(referencia, peito, nome, malha, mundo.material, centro - rot * b.center, rot);
        }

        /// <summary>A arma deitada nas costas: o lado dela encostado, a boca do cano para cima, inclinada (graus, + para a direita).</summary>
        static Quaternion DeitadaNasCostas(float inclinacao)
        {
            float t = inclinacao * Mathf.Deg2Rad;
            var cano = new Vector3(Mathf.Sin(t), Mathf.Cos(t), 0f);
            var topo = new Vector3(-Mathf.Cos(t), Mathf.Sin(t), 0f);
            return Quaternion.LookRotation(cano, topo);
        }

        public void Limpar()
        {
            foreach (var p in _pecas)
            {
                if (p == null) continue;
                p.SetActive(false);   // (o Destroy só vale no fim do quadro)
                Destroy(p);
            }
            _pecas.Clear();
        }

        static Mesh Malha(ItensNoMundo mundo, Item item)
        {
            if (Itens.EhEquipamento(item))
            {
                int k = Itens.Equipamento(item);
                return mundo.malhasDeEquipamento != null && k >= 0 && k < mundo.malhasDeEquipamento.Length ? mundo.malhasDeEquipamento[k] : null;
            }
            int m = Itens.Modelo(item);
            return mundo.malhas != null && m >= 0 && m < mundo.malhas.Length ? mundo.malhas[m] : null;
        }

        /// <summary>
        /// Põe uma peça presa ao <paramref name="osso"/>, na pose dada no espaço do corpo em repouso (a da referência),
        /// no tamanho dado (<paramref name="escala"/>: 1, o do item no chão).
        /// </summary>
        void Por(Transform referencia, Transform osso, string nome, Mesh malha, Material material, Vector3 pos, Quaternion rot, float escala = 1f)
        {
            var ossoRef = Osso(referencia, osso.name);
            var go = new GameObject(Prefixo + nome);
            go.layer = gameObject.layer;
            go.transform.SetParent(osso, false);
            // a pose do osso em repouso, no espaço do corpo; a peça, no espaço do osso (com a escala dele, se tiver)
            var ossoNoCorpo = referencia.worldToLocalMatrix * ossoRef.localToWorldMatrix;
            var local = ossoNoCorpo.inverse * Matrix4x4.TRS(pos, rot, Vector3.one * escala);
            go.transform.localPosition = local.GetColumn(3);
            go.transform.localRotation = Quaternion.LookRotation(local.GetColumn(2), local.GetColumn(1));
            go.transform.localScale = new Vector3(((Vector3)local.GetColumn(0)).magnitude, ((Vector3)local.GetColumn(1)).magnitude, ((Vector3)local.GetColumn(2)).magnitude);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = this.material != null ? this.material : material;
            if (semSombra)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            _pecas.Add(go);
        }

        static Transform Osso(Transform raiz, string nome)
        {
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name == nome) return t;
            return null;
        }
    }
}
