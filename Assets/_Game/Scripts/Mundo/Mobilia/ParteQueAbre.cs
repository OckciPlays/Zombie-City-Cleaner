using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Uma parte de um móvel que abre (a mobília nova, Plano_Mobilia.md da pasta de arte): a porta gira na dobradiça (a
    /// origem dela), a tampa (a do baú) gira para cima na dobradiça de trás e a gaveta corre para a frente do móvel (+Z
    /// dele). O jogador mira nela e aperta o E
    /// (<see cref="Alternar"/>); ela abre ou fecha em <see cref="duracao"/> segundos, suave no começo e no fim.
    /// Na cena de testes, direto; no mundo, o estado vem do anfitrião (<see cref="Definir"/>, pelo MobiliaNovaDoMundo).
    /// </summary>
    public class ParteQueAbre : MonoBehaviour
    {
        public enum Tipo { Porta, Gaveta, Tampa }

        public Tipo tipo;
        [Tooltip("A porta: quantos graus ela abre (o sinal leva a ponta dela para a frente do móvel). A tampa: quantos graus ela sobe (em volta do x). A gaveta: quantos metros ela corre.")]
        public float quanto = 100f;
        public float duracao = 0.35f;

        [Tooltip("O nome no aviso do E (\"a porta\", \"o forno\"); vazio: o do tipo.")]
        public string rotulo;

        public bool Aberta { get; private set; }
        public string Nome => !string.IsNullOrEmpty(rotulo) ? rotulo : tipo == Tipo.Porta ? "a porta" : tipo == Tipo.Tampa ? "a tampa" : "a gaveta";

        float _t, _alvo;
        Quaternion _giro0;
        Vector3 _lugar0;
        bool _guardado;

        void Guardar()
        {
            if (_guardado) return;
            _guardado = true;
            _giro0 = transform.localRotation;
            _lugar0 = transform.localPosition;
        }

        void Awake() => Guardar();

        /// <summary>Abre se está fechada, fecha se está aberta.</summary>
        public void Alternar()
        {
            Guardar();
            Aberta = !Aberta;
            _alvo = Aberta ? 1f : 0f;
            enabled = true;
        }

        /// <summary>Fica aberta ou fechada (o estado do anfitrião); 'ja': sem a animação (o móvel acabou de aparecer).</summary>
        public void Definir(bool aberta, bool ja = false)
        {
            Guardar();
            if (Aberta == aberta && !ja) return;
            Aberta = aberta;
            _alvo = aberta ? 1f : 0f;
            if (ja) _t = _alvo;
            enabled = true;
        }

        void Update()
        {
            _t = Mathf.MoveTowards(_t, _alvo, Time.deltaTime / Mathf.Max(0.01f, duracao));
            float s = _t * _t * (3f - 2f * _t);
            if (tipo == Tipo.Porta) transform.localRotation = _giro0 * Quaternion.Euler(0f, quanto * s, 0f);
            else if (tipo == Tipo.Tampa) transform.localRotation = _giro0 * Quaternion.Euler(quanto * s, 0f, 0f);
            else transform.localPosition = _lugar0 + Vector3.forward * (quanto * s);
            if (Mathf.Approximately(_t, _alvo)) enabled = false;
        }

        /// <summary>
        /// A parte pelo nome do objeto ("<Móvel>.Porta.<n>", "<Móvel>.Tampa.<n>", "<Móvel>.Forno.<n>" ou "<Móvel>.Gaveta.<n>")
        /// e pela malha (nas coordenadas dela, a origem na dobradiça ou na frente da gaveta): a porta abre 100° para o lado
        /// em que se estende (a ponta vem para a frente, +Z), a tampa sobe 90° (a ponta, na frente, vai para cima; a dobradiça
        /// fica no alto dela, e de pé ela fica toda na frente da dobradiça: não entra na parede nem na caixa da privada), a porta
        /// do forno desce 88° (a dobradiça embaixo: o alto dela vem para a frente) e a gaveta corre 70% do fundo dela. Null
        /// se o nome não é de uma parte que abre.
        /// </summary>
        public static ParteQueAbre Configurar(GameObject go, Mesh malha)
        {
            bool porta = go.name.Contains(".Porta."), gaveta = go.name.Contains(".Gaveta."), forno = go.name.Contains(".Forno.");
            bool tampa = go.name.Contains(".Tampa.") || forno;
            if (!porta && !gaveta && !tampa) return null;
            var p = go.GetComponent<ParteQueAbre>();
            if (p == null) p = go.AddComponent<ParteQueAbre>();
            var b = malha.bounds;
            if (porta)
            {
                p.tipo = Tipo.Porta;
                // estendida para -X, o giro positivo (o horário, visto de cima) leva a ponta para +Z
                p.quanto = b.center.x < 0f ? 100f : -100f;
            }
            else if (forno)
            {
                p.tipo = Tipo.Tampa;
                p.rotulo = "o forno";
                // estendida para +Y (a dobradiça embaixo), o giro positivo em volta do x leva o alto para +Z
                p.quanto = b.center.y > 0f ? 88f : -88f;
                p.duracao = 0.45f;
            }
            else if (tampa)
            {
                p.tipo = Tipo.Tampa;
                // estendida para +Z, o giro negativo em volta do x levanta a ponta
                p.quanto = b.center.z > 0f ? -90f : 90f;
                p.duracao = 0.5f;
            }
            else
            {
                p.tipo = Tipo.Gaveta;
                p.quanto = Mathf.Max(0.05f, -b.min.z) * 0.7f;
                p.duracao = 0.3f;
            }
            return p;
        }
    }
}
