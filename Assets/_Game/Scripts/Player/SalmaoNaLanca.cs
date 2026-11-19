using UnityEngine;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O salmão preso na ponta da lança (18/11/2026, a etapa 3 da fogueira; o anfitrião decide: JogadorRede.Salmao.cs). Em
    /// todas as máquinas, pelo <see cref="JogadorRede.SalmaoNaLanca"/>: o peixe atravessado pela lança, perto da ponta, na
    /// lança do corpo (o que os outros veem; para o dono em primeira pessoa, só a sombra, como a lança) e na de primeira pessoa
    /// (a ponta medida no osso da mão direita: <see cref="PontaNaMaoFP"/>). Ele se debate: forte nos primeiros 3 s (gira na lança e sacode a cauda), depois um
    /// tranco de vez em quando. No anfitrião, a lança que sai da mão leva o peixe para a mochila.
    /// </summary>
    [DefaultExecutionOrder(103)]
    public class SalmaoNaLanca : MonoBehaviour
    {
        const float AntesDaPonta = 0.16f;

        JogadorRede _rede;
        ArmaJogador _arma;
        Transform _peixeCorpo, _peixeFP;
        MeshRenderer _rCorpo;
        bool _antes;
        float _desde, _proximoTranco, _tranco = -10f;

        void Awake()
        {
            _rede = GetComponent<JogadorRede>();
            _arma = GetComponent<ArmaJogador>();
        }

        void OnDestroy()
        {
            if (_peixeCorpo != null) Destroy(_peixeCorpo.gameObject);
            if (_peixeFP != null) Destroy(_peixeFP.gameObject);
        }

        void Update()
        {
            // o anfitrião: a lança saiu da mão com o peixe nela, e ele vai para a mochila
            if (_rede != null && _rede.IsServer && _rede.IsSpawned && _rede.SalmaoNaLanca.Value && _rede.Inventario != null && _rede.Inventario.NaMao != Item.Lanca)
                _rede.TirarOSalmao();
        }

        void LateUpdate()
        {
            bool tem = _rede != null && _rede.IsSpawned && _rede.SalmaoNaLanca.Value;
            if (tem != _antes)
            {
                _antes = tem;
                _desde = Time.time;
                _proximoTranco = Time.time + 3f;
                if (!tem)
                {
                    if (_peixeCorpo != null) _peixeCorpo.gameObject.SetActive(false);
                    if (_peixeFP != null) _peixeFP.gameObject.SetActive(false);
                }
            }
            if (!tem) return;
            // o debater: forte no começo, depois os trancos
            float t = Time.time - _desde, forca;
            if (t < 3f) forca = Mathf.Lerp(1f, 0.35f, t / 3f);
            else
            {
                if (Time.time >= _proximoTranco)
                {
                    _tranco = Time.time;
                    _proximoTranco = Time.time + Random.Range(1.5f, 4f);
                }
                forca = 0.12f + 0.75f * Mathf.Clamp01(1f - (Time.time - _tranco) / 0.6f);
            }
            float giro = Mathf.Sin(Time.time * 19f) * 38f * forca;
            float cauda = Mathf.Sin(Time.time * 23f + 1.3f) * 16f * forca;
            NoCorpo(giro, cauda);
            NaPrimeiraPessoa(giro, cauda);
        }

        // a lança do corpo: a malha vai no Z dela, e a ponta é a ponta da malha
        void NoCorpo(float giro, float cauda)
        {
            int m = Itens.Modelo(Item.Lanca);
            var naMao = _rede.naMaoCorpo;
            if (naMao == null || m < 0 || m >= naMao.Length || naMao[m] == null) return;
            var mf = naMao[m].GetComponentInChildren<MeshFilter>(true);
            if (mf == null || mf.sharedMesh == null || !mf.TryGetComponent<MeshRenderer>(out var lanca)) return;
            if (_peixeCorpo == null)
            {
                _peixeCorpo = Criar("Salmão na lança (corpo)", mf.transform, mf.gameObject.layer, out _rCorpo);
                if (_peixeCorpo == null) return;
            }
            if (_peixeCorpo.parent != mf.transform) _peixeCorpo.SetParent(mf.transform, false);
            if (!_peixeCorpo.gameObject.activeSelf) _peixeCorpo.gameObject.SetActive(true);
            // como a lança: escondida, ou só a sombra para o dono em primeira pessoa
            _rCorpo.enabled = lanca.enabled;
            _rCorpo.shadowCastingMode = lanca.shadowCastingMode;
            _rCorpo.renderingLayerMask = lanca.renderingLayerMask;
            Vector3 dir = mf.transform.forward;
            Vector3 ponta = mf.transform.TransformPoint(new Vector3(0f, 0f, mf.sharedMesh.bounds.max.z));
            Pendurar(_peixeCorpo, ponta - dir * AntesDaPonta, dir, giro, cauda);
        }

        // a lança de primeira pessoa: ela é parte da malha dos braços, presa ao osso da mão direita (o vazio Boca_FP_Lanca
        // fica na pegada, e não na ponta: 18/11/2026, o Pedro viu o peixe sumir). A ponta é o vértice dela mais longe da
        // mão, achado uma vez na pose de repouso (no espaço da mão); a direção, da mão até a ponta
        Transform _maoFP;
        Vector3 _pontaNaMaoFP;
        bool _pontaFPProcurada, _temPontaFP;

        void NaPrimeiraPessoa(float giro, float cauda)
        {
            int m = Itens.Modelo(Item.Lanca);
            var modelos = _arma != null ? _arma.modelos : null;
            if (modelos == null || m < 0 || m >= modelos.Length || modelos[m] == null || !modelos[m].activeInHierarchy) return;
            if (!_pontaFPProcurada)
            {
                _pontaFPProcurada = true;
                _temPontaFP = AcharAPontaFP(modelos[m], out _maoFP, out _pontaNaMaoFP);
            }
            if (!_temPontaFP || _maoFP == null) return;
            if (_peixeFP == null)
            {
                _peixeFP = Criar("Salmão na lança (primeira pessoa)", _maoFP, _maoFP.gameObject.layer, out var r);
                if (_peixeFP == null) return;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (_peixeFP.parent != _maoFP) _peixeFP.SetParent(_maoFP, false);
            if (!_peixeFP.gameObject.activeSelf) _peixeFP.gameObject.SetActive(true);
            Vector3 ponta = _maoFP.TransformPoint(_pontaNaMaoFP);
            Vector3 dir = ponta - _maoFP.position;
            if (dir.sqrMagnitude < 1e-4f) return;
            dir.Normalize();
            Pendurar(_peixeFP, ponta - dir * AntesDaPonta, dir, giro, cauda);
        }

        /// <summary>
        /// A ponta da lança de primeira pessoa no espaço do osso hand.R (o Jogador_FP_Lanca.fbx: o vértice mais longe da mão,
        /// a 1,32 m, medido no Editor em 18/11/2026 com o BakeMesh; a malha não é legível no jogo). Se o modelo mudar, meça de novo.
        /// </summary>
        static readonly Vector3 PontaNaMaoFP = new Vector3(0.8979f, 0.9673f, -0.0391f);

        static bool AcharAPontaFP(GameObject modelo, out Transform mao, out Vector3 ponta)
        {
            mao = null;
            ponta = PontaNaMaoFP;
            foreach (var t in modelo.GetComponentsInChildren<Transform>(true))
                if (t.name == "hand.R")
                {
                    mao = t;
                    return true;
                }
            return false;
        }

        // o peixe atravessado pela lança (de lado: a grossura dele, o Y da malha, vai na direção da lança), o meio no ponto
        static void Pendurar(Transform peixe, Vector3 ponto, Vector3 dir, float giro, float cauda)
        {
            Vector3 lado = Vector3.Cross(Vector3.up, dir);
            if (lado.sqrMagnitude < 0.01f) lado = Vector3.Cross(Vector3.forward, dir);
            lado.Normalize();
            Quaternion baseRot = Quaternion.LookRotation(Vector3.Cross(lado, dir), dir);   // X de lado, Y na lança
            Quaternion rot = Quaternion.AngleAxis(giro, dir) * baseRot * Quaternion.Euler(0f, 0f, cauda);
            var mf = peixe.GetComponent<MeshFilter>();
            Vector3 meio = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.center : Vector3.zero;
            peixe.SetPositionAndRotation(ponto - rot * meio, rot);
        }

        static Transform Criar(string nome, Transform pai, int camada, out MeshRenderer r)
        {
            r = null;
            var malha = ItensDaCaca.MalhaDoItem(Item.Salmao);
            var itens = ItensNoMundo.Instancia;
            if (malha == null || itens == null) return null;
            var go = new GameObject(nome);
            go.layer = camada;
            go.transform.SetParent(pai, false);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = itens.MaterialDe(Item.Salmao);
            return go.transform;
        }
    }
}
