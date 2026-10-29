using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O taco de sinuca na mão (29/10/2026, a pedido: "segurando o taco com somente uma mão, com o taco em pé"): a mão
    /// direita segura o taco pela empunhadura, em pé, como um cajado (o bloco_taco.py da pasta de arte: no corpo, o clipe
    /// Taco_Segurar; na primeira pessoa, os braços de <see cref="Itens.TacoFP"/>). Os FBX não trazem o taco, só onde ele
    /// fica: a pegada (no corpo, o vazio Arma_Taco no weapon.R; na primeira pessoa, o osso gun) e a ponta (Boca_Taco e
    /// Boca_FP_Taco). Aqui o taco da mão, com a cor de cada um (a malha do chão, do <see cref="ItensNoMundo"/>), vai entre
    /// os dois a cada quadro, depois da animação. Segue as regras do que está na mão: no dono, o do corpo só aparece com o
    /// corpo à vista (a terceira pessoa); some no carro e na vista de cima da mesa (a câmera dos braços desligada). Posto
    /// pelo <see cref="JogadorRede"/> quando ele nasce.
    /// </summary>
    [DefaultExecutionOrder(150)]   // depois da animação e da câmera de terceira pessoa
    public class TacoNaMao : MonoBehaviour
    {
        // onde a mão segura o taco: a 42 cm da base, no meio da empunhadura (o PEGADA_NO_TACO do bloco_taco.py)
        const float Pegada = 0.42f;

        JogadorRede _rede;
        ArmaJogador _arma;
        InventarioJogador _inventario;
        GameObject _modeloFP;
        Transform _fp, _corpo, _gun, _pontaFP;
        MeshFilter _malhaFP, _malhaCorpo;
        Mesh _malha;
        // a malha do taco: para que lado é a ponta e onde a mão segura (no espaço dela)
        Vector3 _eixo = Vector3.right;
        Vector3 _pegadaNaMalha;

        void Awake()
        {
            _rede = GetComponent<JogadorRede>();
            _arma = GetComponent<ArmaJogador>();
            _inventario = GetComponent<InventarioJogador>();
        }

        void OnDisable() => Mostrar(false, false);

        void OnDestroy()
        {
            if (_fp != null) Destroy(_fp.gameObject);
            if (_corpo != null) Destroy(_corpo.gameObject);
        }

        void LateUpdate()
        {
            var item = _inventario != null && _inventario.IsSpawned ? _inventario.NaMao : Item.Nada;
            var malha = Itens.EhTaco(item) && _rede != null && !_rede.NaMaoEscondida ? MalhaDo(item) : null;
            if (malha == null)
            {
                Mostrar(false, false);
                return;
            }
            Montar(malha);
            bool dono = _rede.IsOwner;
            // a primeira pessoa: só no dono, com os braços do taco à vista (a câmera dos braços ligada)
            var camBracos = _rede.cameraBracos;
            bool fp = dono && _gun != null && _pontaFP != null && camBracos != null && camBracos.enabled && !_rede.EmTerceiraPessoa
                      && _modeloFP.activeInHierarchy;
            // o corpo: nos outros, sempre; no dono, só com o corpo à vista (como o que está na mão do corpo)
            var pegada = _rede.tacoCorpo;
            var ponta = _rede.pontaDoTacoCorpo;
            bool corpo = pegada != null && ponta != null && (!dono || _rede.CorpoVisivelNoDono);
            Mostrar(fp, corpo);
            if (fp) Por(_fp, _gun.position, _pontaFP.position);
            if (corpo)
            {
                if (_corpo.gameObject.layer != pegada.gameObject.layer) _corpo.gameObject.layer = pegada.gameObject.layer;
                Por(_corpo, pegada.position, ponta.position);
            }
        }

        // o taco com a pegada no lugar dela e a ponta para a ponta
        void Por(Transform taco, Vector3 pegada, Vector3 ponta)
        {
            var giro = Quaternion.FromToRotation(_eixo, ponta - pegada);
            taco.SetPositionAndRotation(pegada - giro * _pegadaNaMalha, giro);
        }

        void Mostrar(bool fp, bool corpo)
        {
            if (_fp != null && _fp.gameObject.activeSelf != fp) _fp.gameObject.SetActive(fp);
            if (_corpo != null && _corpo.gameObject.activeSelf != corpo) _corpo.gameObject.SetActive(corpo);
        }

        static Mesh MalhaDo(Item item)
        {
            var mundo = ItensNoMundo.Instancia;
            int k = Itens.IndiceDaSinuca(item);
            return mundo != null && mundo.malhasDaSinuca != null && k >= 0 && k < mundo.malhasDaSinuca.Length ? mundo.malhasDaSinuca[k] : null;
        }

        // as duas cópias do taco (a de primeira pessoa, nos braços do taco, e a do corpo), com a malha do taco da mão
        void Montar(Mesh malha)
        {
            var material = ItensNoMundo.Instancia.material;
            if (_fp == null && _arma != null && _arma.modelos != null && Itens.TacoFP < _arma.modelos.Length)
            {
                _modeloFP = _arma.modelos[Itens.TacoFP];
                foreach (var t in _modeloFP.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "gun") _gun = t;
                    else if (t.name == "Boca_FP_Taco") _pontaFP = t;
                }
                _fp = Novo("Taco (primeira pessoa)", _modeloFP.transform, _modeloFP.layer, material, out _malhaFP).transform;
                _fp.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            if (_corpo == null) _corpo = Novo("Taco (corpo)", transform, gameObject.layer, material, out _malhaCorpo).transform;
            if (malha == _malha) return;
            _malha = malha;
            Medir(malha);
            if (_malhaFP != null) _malhaFP.sharedMesh = malha;
            _malhaCorpo.sharedMesh = malha;
        }

        // solto (sem a escala dos ossos): a pose vem a cada quadro
        static GameObject Novo(string nome, Transform pai, int camada, Material material, out MeshFilter filtro)
        {
            var go = new GameObject(nome) { layer = camada };
            go.transform.SetParent(pai, false);
            filtro = go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            go.SetActive(false);
            return go;
        }

        // o taco está deitado ao longo do X da malha: a ponta é o lado mais fino; a mão segura a 42 cm da base
        void Medir(Mesh malha)
        {
            var b = malha.bounds;
            float raioMin = 0f, raioMax = 0f;
            int nMin = 0, nMax = 0;
            foreach (var v in malha.vertices)
            {
                float r = new Vector2(v.y - b.center.y, v.z - b.center.z).magnitude;
                if (v.x < b.min.x + 0.03f) { raioMin += r; nMin++; }
                if (v.x > b.max.x - 0.03f) { raioMax += r; nMax++; }
            }
            bool pontaNoMax = nMin == 0 || (nMax > 0 && raioMax / nMax < raioMin / nMin);
            _eixo = pontaNoMax ? Vector3.right : Vector3.left;
            float baseX = pontaNoMax ? b.min.x : b.max.x;
            _pegadaNaMalha = new Vector3(baseX + _eixo.x * Pegada, b.center.y, b.center.z);
        }
    }
}
