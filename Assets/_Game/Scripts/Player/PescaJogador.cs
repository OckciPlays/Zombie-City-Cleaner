using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A pesca (06/11/2026, a pedido: "fazer o sistema de pesca ... dar um sentido à vara de pesca"), no dono. Com a vara na
    /// mão e uma isca na mochila, o clique mirando a água (até 18 m, com pelo menos 0,8 m de fundo) lança: a boia cai na
    /// água com a linha. Depois de uns 6 a 20 s, o peixe morde: a boia afunda e é preciso clicar em 1,2 s (fisgar). Aí vem a
    /// puxada: o ponteiro corre e o clique precisa cair na faixa verde (mais estreita no peixe maior). Pegou: o anfitrião
    /// confere e põe o peixe na mochila (<see cref="InventarioJogador.PegouOPeixe"/>), com o XP. Errou: o peixe escapa
    /// com a isca. Clicar esperando recolhe a linha (sem perder a isca). Na margem ou no caiaque (o lugar da frente).
    /// <para>As habilidades (o ramo Pesca): a Paciência (morde 40% mais rápido), a Isca fina (40% de chance de não gastar
    /// a isca), o Peixe grande (mais peixes grandes), a Noite de pesca (a noite e a chuva ajudam em vez de atrapalhar) e
    /// o Mestre pescador (o dourado, raro).</para>
    /// <para>A boia e a linha só aparecem para quem pesca (os outros veem a vara na mão).</para>
    /// </summary>
    public class PescaJogador : MonoBehaviour
    {
        public static PescaJogador Local { get; private set; }

        public const float AlcanceDoLance = 18f, FundoMinimo = 0.8f;

        enum Fase { Parado, Voando, Esperando, Mordendo, Puxando }

        JogadorRede _rede;
        ArmaJogador _arma;
        InventarioJogador _inventario;
        Fase _fase;
        float _tempo, _espera, _ponteiro, _faixa, _centro;
        Item _peixe;
        Vector3 _pontoDaBoia, _deOnde;
        GameObject _boia;
        LineRenderer _linha;
        GUIStyle _estilo;
        float _u;
        static AudioClip _plop;

        void Awake()
        {
            _rede = GetComponent<JogadorRede>();
            _arma = GetComponent<ArmaJogador>();
            _inventario = GetComponent<InventarioJogador>();
        }

        void OnEnable() => Local = this;

        void OnDisable()
        {
            if (Local == this) Local = null;
            Recolher();
        }

        public bool Pescando => _fase != Fase.Parado;

        /// <summary>O clique com a vara na mão (o ArmaJogador chama).</summary>
        public void Clicar()
        {
            switch (_fase)
            {
                case Fase.Parado: Lancar(); break;
                case Fase.Voando:
                case Fase.Esperando:
                    InventarioJogador.AvisoLocal("Recolheu a linha");
                    Recolher();
                    break;
                case Fase.Mordendo: Fisgar(); break;
                case Fase.Puxando: Puxar(); break;
            }
        }

        void Lancar()
        {
            if (_inventario == null || _inventario.Quantos(Item.Isca) <= 0)
            {
                InventarioJogador.AvisoLocal("Sem isca na mochila (o milho vira isca: CRIAR)");
                return;
            }
            var olho = _rede.Olho;
            if (!AchouAgua(olho.position, olho.forward, out _pontoDaBoia))
            {
                InventarioJogador.AvisoLocal("Mire na água funda (até 18 m)");
                return;
            }
            _deOnde = PontaDaVara();
            _fase = Fase.Voando;
            _tempo = 0f;
            Efeitos.Som(Efeitos.SocoVento, olho.position, 0.5f, false);
            CriarBoia();
        }

        // o raio do olhar, de meio em meio metro: o primeiro ponto embaixo do nível da água (com fundo)
        static bool AchouAgua(Vector3 de, Vector3 dir, out Vector3 ponto)
        {
            for (float d = 2f; d <= AlcanceDoLance; d += 0.5f)
            {
                var p = de + dir * d;
                if (Physics.Linecast(de, p, ~((1 << 2) | (1 << 4) | (1 << 6) | (1 << 10) | (1 << 11) | (1 << 12)), QueryTriggerInteraction.Ignore)) break;
                if (!Agua.Nivel(p, out float nivel) || p.y > nivel) continue;
                ponto = new Vector3(p.x, nivel, p.z);
                return Agua.Fundura(ponto) >= FundoMinimo;
            }
            // mirando longe, para cima: o ponto mais longe, na água, cai nela
            var longe = de + new Vector3(dir.x, 0f, dir.z).normalized * Mathf.Min(AlcanceDoLance, 12f);
            if (Agua.Nivel(longe, out float n2) && Agua.Fundura(longe) >= FundoMinimo && dir.y > -0.6f)
            {
                ponto = new Vector3(longe.x, n2, longe.z);
                return !Physics.Linecast(de, ponto + Vector3.up * 0.3f, ~((1 << 2) | (1 << 4) | (1 << 6) | (1 << 10) | (1 << 11) | (1 << 12)), QueryTriggerInteraction.Ignore);
            }
            ponto = default;
            return false;
        }

        void Fisgar()
        {
            _fase = Fase.Puxando;
            _tempo = 0f;
            _ponteiro = 0f;
            // a faixa verde: mais estreita no peixe maior
            _faixa = _peixe switch { Item.PeixePequeno => 0.30f, Item.PeixeMedio => 0.22f, Item.PeixeGrande => 0.15f, _ => 0.10f };
            _centro = Random.Range(0.2f + _faixa / 2f, 0.8f - _faixa / 2f);
        }

        void Puxar()
        {
            bool pegou = Mathf.Abs(Mathf.PingPong(_ponteiro, 1f) - _centro) <= _faixa / 2f;
            bool fina = Habilidades.Tem(_rede, Habilidade.IscaFina) && Random.value < 0.4f;
            _inventario.PegouOPeixe(pegou ? _peixe : Item.Nada, !fina);
            if (!pegou) InventarioJogador.AvisoLocal(fina ? "O peixe escapou (a isca ficou no anzol)" : "O peixe escapou com a isca");
            Recolher();
        }

        Item SortearPeixe()
        {
            bool mestre = Habilidades.Tem(_rede, Habilidade.MestrePescador);
            bool grande = Habilidades.Tem(_rede, Habilidade.PeixeGrande);
            float r = Random.value;
            if (mestre && r < 0.06f) return Item.PeixeDourado;
            r = Random.value;
            float pequeno = grande ? 0.35f : 0.52f, medio = grande ? 0.37f : 0.33f;
            return r < pequeno ? Item.PeixePequeno : r < pequeno + medio ? Item.PeixeMedio : Item.PeixeGrande;
        }

        float SortearEspera()
        {
            float t = Random.Range(6f, 20f);
            if (Habilidades.Tem(_rede, Habilidade.Paciencia)) t *= 0.6f;
            bool ruim = CicloDoDia.Noite > 0.5f || Tempo.Chuva > 0.3f;
            if (ruim) t *= Habilidades.Tem(_rede, Habilidade.NoiteDePesca) ? 0.8f : 1.4f;
            return t;
        }

        void Recolher()
        {
            _fase = Fase.Parado;
            if (_boia != null) _boia.SetActive(false);
            if (_linha != null) _linha.enabled = false;
        }

        void Update()
        {
            if (_rede == null || !_rede.IsOwner) return;
            if (_fase == Fase.Parado) return;
            // trocou de item, saiu de perto ou caiu: a linha volta
            if (_arma == null || _arma.NaMao != Item.VaraDePesca || !_arma.isActiveAndEnabled
                || (_rede.Vida != null && _rede.Vida.Caido.Value)
                || new Vector2(transform.position.x - _pontoDaBoia.x, transform.position.z - _pontoDaBoia.z).magnitude > AlcanceDoLance + 6f)
            {
                Recolher();
                return;
            }
            float dt = Time.deltaTime;
            _tempo += dt;
            var ponta = PontaDaVara();
            switch (_fase)
            {
                case Fase.Voando:
                {
                    // a boia voa em arco da ponta da vara à água
                    float k = Mathf.Clamp01(_tempo / 0.7f);
                    var p = Vector3.Lerp(_deOnde, _pontoDaBoia, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 2.5f;
                    _boia.transform.position = p;
                    if (k >= 1f)
                    {
                        _fase = Fase.Esperando;
                        _tempo = 0f;
                        _espera = SortearEspera();
                        Plop(_pontoDaBoia, 0.5f);
                    }
                    break;
                }
                case Fase.Esperando:
                    Boiar(0.02f);
                    if (_tempo >= _espera)
                    {
                        _fase = Fase.Mordendo;
                        _tempo = 0f;
                        _peixe = SortearPeixe();
                        Plop(_pontoDaBoia, 0.9f);
                    }
                    break;
                case Fase.Mordendo:
                    // a boia afunda e volta: 1,2 s para fisgar
                    _boia.transform.position = _pontoDaBoia + Vector3.down * (0.12f + 0.06f * Mathf.Sin(_tempo * 30f));
                    if (_tempo > 1.2f)
                    {
                        InventarioJogador.AvisoLocal("O peixe soltou a isca: clique quando a boia afundar");
                        _fase = Fase.Esperando;
                        _tempo = 0f;
                        _espera = SortearEspera() * 0.6f;
                    }
                    break;
                case Fase.Puxando:
                    Boiar(0.06f);
                    _ponteiro += dt * (_peixe == Item.PeixeDourado ? 1.6f : _peixe == Item.PeixeGrande ? 1.35f : 1.1f);
                    if (_tempo > 6f)
                    {
                        _inventario.PegouOPeixe(Item.Nada, true);
                        InventarioJogador.AvisoLocal("Demorou: o peixe escapou com a isca");
                        Recolher();
                        return;
                    }
                    break;
            }
            if (_linha != null && _boia != null && _boia.activeSelf)
            {
                _linha.enabled = true;
                _linha.SetPosition(0, ponta);
                _linha.SetPosition(1, _boia.transform.position + Vector3.up * 0.03f);
            }
        }

        void Boiar(float altura) => _boia.transform.position = _pontoDaBoia + Vector3.up * (Mathf.Sin(Time.time * 2.2f) * altura);

        // a ponta da vara: na frente e um pouco acima da mão direita (a câmera)
        Vector3 PontaDaVara()
        {
            var olho = _rede.Olho;
            return olho.position + olho.forward * 1.6f + olho.right * 0.3f + Vector3.up * 0.55f;
        }

        void CriarBoia()
        {
            if (_boia == null)
            {
                var prefab = Resources.Load<GameObject>("Boia");
                if (prefab != null) _boia = Instantiate(prefab);
                else
                {
                    _boia = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(_boia.GetComponent<Collider>());
                    _boia.transform.localScale = Vector3.one * 0.07f;
                    var r = _boia.GetComponent<Renderer>();
                    r.material.color = new Color(0.9f, 0.15f, 0.12f);
                }
                foreach (var c in _boia.GetComponentsInChildren<Collider>()) Destroy(c);
                _boia.name = "Boia da pesca";
                var linha = new GameObject("Linha da pesca");
                _linha = linha.AddComponent<LineRenderer>();
                _linha.positionCount = 2;
                _linha.widthMultiplier = 0.006f;
                _linha.material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.92f, 0.92f, 0.88f, 1f) };
                _linha.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            _boia.SetActive(true);
            _boia.transform.position = _deOnde;
        }

        void OnDestroy()
        {
            if (_boia != null) Destroy(_boia);
            if (_linha != null) Destroy(_linha.gameObject);
        }

        void Plop(Vector3 onde, float volume)
        {
            if (_plop == null) _plop = GerarPlop();
            Efeitos.Som(_plop, onde, volume, true, 20f);
        }

        // um "plop" na água: um estalo grave que cai de tom, com chiado
        static AudioClip GerarPlop()
        {
            const int taxa = 22050;
            int n = taxa / 4;
            var dados = new float[n];
            var r = new System.Random(11);
            float filtro = 0f, fase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)taxa;
                fase += 2f * Mathf.PI * Mathf.Lerp(520f, 140f, Mathf.Clamp01(t * 6f)) / taxa;
                filtro = Mathf.Lerp(filtro, (float)(r.NextDouble() * 2 - 1), 0.3f);
                dados[i] = (Mathf.Sin(fase) * 0.7f + filtro * 0.35f) * Mathf.Exp(-t * 18f);
            }
            var clip = AudioClip.Create("Plop", n, 1, taxa, false);
            clip.SetData(dados, 0);
            return clip;
        }

        void OnGUI()
        {
            if (_fase == Fase.Parado || _rede == null || !_rede.IsOwner || MenuOnline.Aberto || MenuOnline.Cinema) return;
            if (Event.current.type != EventType.Repaint) return;
            float u = Screen.height / 720f;
            if (_estilo == null || !Mathf.Approximately(u, _u))
            {
                _u = u;
                _estilo = new GUIStyle(EstilosDoHud.Rotulo(Mathf.RoundToInt(15 * u), FontStyle.Bold, TextAnchor.MiddleCenter));
            }
            float cx = Screen.width * 0.5f, y = Screen.height * 0.5f + 90 * u;
            string texto = _fase switch
            {
                Fase.Voando => "",
                Fase.Esperando => "Esperando o peixe (clique: recolher a linha)",
                Fase.Mordendo => "MORDEU! Clique!",
                _ => "Puxe: clique com o ponteiro na faixa verde",
            };
            Texto(new Rect(cx - 300 * u, y, 600 * u, 24 * u), texto, _fase == Fase.Mordendo ? new Color(1f, 0.85f, 0.3f) : new Color(0.95f, 0.93f, 0.86f));
            if (_fase != Fase.Puxando) return;
            float w = 260 * u, h = 14 * u, x = cx - w / 2f, yb = y + 28 * u;
            Retangulo(new Rect(x, yb, w, h), new Color(0f, 0f, 0f, 0.55f));
            Retangulo(new Rect(x + w * (_centro - _faixa / 2f), yb, w * _faixa, h), new Color(0.3f, 0.75f, 0.35f, 0.9f));
            float px = x + w * Mathf.PingPong(_ponteiro, 1f);
            Retangulo(new Rect(px - 2 * u, yb - 4 * u, 4 * u, h + 8 * u), Color.white);
        }

        void Texto(Rect r, string t, Color c)
        {
            if (string.IsNullOrEmpty(t)) return;
            _estilo.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), t, _estilo);
            _estilo.normal.textColor = c;
            GUI.Label(r, t, _estilo);
        }

        static void Retangulo(Rect r, Color c)
        {
            var antes = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = antes;
        }
    }
}
