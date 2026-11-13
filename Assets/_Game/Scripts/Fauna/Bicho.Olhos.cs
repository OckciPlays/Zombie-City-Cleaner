using UnityEngine;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// Os olhos na lanterna (a etapa 4, 12/11/2026), em cada máquina (nada vai pela rede): à noite, quando a lanterna do
    /// jogador desta máquina aponta para o bicho (num cone de ~16°, até 70 m, sem nada na frente), os dois olhos brilham
    /// de longe, na cor de cada um (o veado verde-branco, o lobo e o coiote amarelo-verde, o urso laranja, o guaxinim
    /// amarelo...). Dois pontinhos sem luz da cena (o material sem sombra), postos na frente da cabeça a cada quadro.
    /// </summary>
    public sealed partial class Bicho
    {
        static Material _materialDosOlhos;
        static readonly int CorId = Shader.PropertyToID("_BaseColor");
        Transform _olhoE, _olhoD;
        MaterialPropertyBlock _corDosOlhos;
        float _proximaVista;
        bool _olhosAcesos;

        static Color CorDosOlhos(Especie e) => e switch
        {
            Especie.Veado or Especie.Alce => new Color(0.85f, 1f, 0.8f),
            Especie.Lobo or Especie.Coiote => new Color(0.95f, 1f, 0.35f),
            Especie.UrsoNegro or Especie.UrsoPardo => new Color(1f, 0.6f, 0.2f),
            Especie.Guaxinim or Especie.Gamba => new Color(1f, 0.85f, 0.3f),
            Especie.Coelho or Especie.Castor or Especie.Esquilo => new Color(1f, 0.35f, 0.25f),
            _ => new Color(0.9f, 0.9f, 0.6f),
        };

        void Olhos(float dt)
        {
            if (_lingua != null && _lingua.gameObject.activeSelf && Time.time > _linguaAte) _lingua.gameObject.SetActive(false);
            bool acender = false;
            // as aves (o urubu, o peru, o ganso...), o peixe e a cobra quase não têm o brilho (sem a camada atrás do olho)
            if (!Morto && Dados.corpo != Corpo.Peixe && Dados.corpo != Corpo.Cobra && Dados.corpo != Corpo.Ave && CicloDoDia.Noite > 0.45f && Modelo != null && Modelo.cabeca != null)
            {
                var j = JogadorRede.Local;
                var cam = Camera.main;
                if (j != null && cam != null && j.Lanterna != null && j.Lanterna.Ligada.Value)
                {
                    Vector3 cabeca = Modelo.cabeca.position;
                    Vector3 para = cabeca - cam.transform.position;
                    float d = para.magnitude;
                    if (d < 70f && d > 1.5f && Vector3.Angle(cam.transform.forward, para) < 16f && Vector3.Dot(transform.forward, -para) > -0.2f)
                    {
                        // a linha de visão (o cenário na frente), conferida de tempo em tempo
                        if (Time.time > _proximaVista)
                        {
                            _proximaVista = Time.time + 0.3f;
                            _olhosAcesos = !Physics.Linecast(cam.transform.position, cabeca, 1, QueryTriggerInteraction.Ignore);
                        }
                        acender = _olhosAcesos;
                    }
                }
            }
            if (!acender)
            {
                if (_olhoE != null && _olhoE.gameObject.activeSelf) { _olhoE.gameObject.SetActive(false); _olhoD.gameObject.SetActive(false); }
                return;
            }
            if (_olhoE == null) CriarOlhos();
            if (!_olhoE.gameObject.activeSelf) { _olhoE.gameObject.SetActive(true); _olhoD.gameObject.SetActive(true); }
            // na frente da cabeça, afastados pelo tamanho do bicho; maiores de longe (para se ver os pontinhos)
            var c = Camera.main.transform.position;
            float tam = Mathf.Clamp(Altura * 0.05f, 0.02f, 0.08f) * Mathf.Lerp(1f, 3f, Mathf.InverseLerp(10f, 70f, Vector3.Distance(c, Modelo.cabeca.position)));
            Vector3 frente = transform.forward * Mathf.Max(0.04f, Altura * 0.14f), lado = transform.right * Mathf.Max(0.02f, Altura * 0.07f);
            Vector3 meio = Modelo.cabeca.position + frente + Vector3.up * Altura * 0.03f;
            _olhoE.position = meio - lado;
            _olhoD.position = meio + lado;
            _olhoE.localScale = _olhoD.localScale = Vector3.one * tam;
        }

        void CriarOlhos()
        {
            if (_materialDosOlhos == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                _materialDosOlhos = new Material(shader) { name = "Fauna_Olhos", enableInstancing = true };
            }
            _corDosOlhos = new MaterialPropertyBlock();
            _corDosOlhos.SetColor(CorId, CorDosOlhos(Especie) * 2.5f);
            _olhoE = OlhoNovo("Olho E");
            _olhoD = OlhoNovo("Olho D");
        }

        Transform OlhoNovo(string nome)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = nome;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, true);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = _materialDosOlhos;
            r.SetPropertyBlock(_corDosOlhos);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
            return go.transform;
        }
    }
}
