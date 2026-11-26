using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A animação do craft (25/11/2026, a pedido: "quando for criar o item, ter uma animação do personagem fazendo o item,
    /// a câmera ir para a terceira pessoa, os itens aparecerem na mesa e irem sumindo"). No dono, o CRIAR da aba Criar fecha
    /// a mochila, a câmera sai do personagem para o lado da mesa (no mesmo dia, a pedido: "na terceira pessoa ficou ruim,
    /// pois parece que o personagem está bloqueando a visão do craft": de lado, aparecem o personagem trabalhando e o
    /// tampo; atrás do ombro, as costas dele tapavam tudo) e o personagem vira para a mesa; os materiais aparecem no tampo (da mesa
    /// de ferramentas, da bancada do bunker ou de uma casa) ou, feito à mão, no chão na frente dele; ele martela três vezes
    /// (o som do martelo das barricadas), os materiais somem um a um e o item feito aparece. Dura <see cref="Duracao"/> s; um
    /// clique pula. No fim, a receita é pedida ao anfitrião como antes (<see cref="InventarioJogador.Criar"/>: se der errado,
    /// nada sai) e a mochila volta aberta na aba Criar. Os outros jogadores veem o mesmo (sem a câmera) pelo RPC do
    /// <see cref="InventarioJogador"/>. Fica no objeto do jogador, criado na primeira vez.
    /// </summary>
    [DefaultExecutionOrder(200)]   // depois da CameraTerceiraPessoa (100): a câmera de lado é a última a mexer na câmera
    public class AnimacaoDoCraft : MonoBehaviour
    {
        public const float Duracao = 2.5f;
        // o martelo_1 tem 3 golpes (0,06, 0,68 e 1,34 s): ele começa aqui, e o braço desce um pouco antes de cada golpe
        const float SomDoMartelo = 0.19f, AntesDoGolpe = 0.12f;
        static readonly float[] Golpes = { 0.25f, 0.87f, 1.53f };
        const float SomemDe = 0.6f, SomemAte = 1.8f, ApareceEm = 2.0f, Cresce = 0.18f, TempoDeVirar = 0.35f;
        // o maior que cabe no tampo (o que passa, encolhe); o item feito pode ser maior (a lança encolhida parecia um palito)
        const float MaiorPeca = 0.55f, MaiorFeito = 1.3f;
        // a câmera de lado: chega em TempoDaCamera; a máscara das paredes (sem os jogadores, os braços e a cabeça)
        const float TempoDaCamera = 0.45f;
        static readonly int MascaraDaCamera = ~((1 << 2) | (1 << JogadorRede.CamadaJogador) | (1 << JogadorRede.CamadaBracos) | (1 << JogadorRede.CamadaDaCabeca));
        static readonly int FireId = Animator.StringToHash("Fire");

        JogadorRede _rede;
        InventarioJogador _inv;
        int _receita = -1, _golpe;
        bool _doDono, _naMesa, _cinemaAntes, _somTocou;
        float _comeco, _yaw0, _pitch0;
        Vector3 _centro;
        Quaternion _giro;
        readonly List<GameObject> _pecas = new List<GameObject>();
        readonly List<float> _somemEm = new List<float>();
        GameObject _resultado;
        Vector3 _escalaDoResultado = Vector3.one;
        static AudioClip _martelo;
        Vector3 _camPos, _camAlvo;
        // feito à mão, no chão: o personagem se abaixa (o C) enquanto trabalha, e levanta no fim (se ele já estava abaixado, fica)
        bool _abaixouParaOCraft;

        /// <summary>A animação está tocando (no dono ou vista nos outros).</summary>
        public bool Tocando => _receita >= 0;

        /// <summary>A animação do dono está tocando (o teste espera por ela).</summary>
        public bool NoDono => Tocando && _doDono;

        /// <summary>Para o teste: os materiais e o item feito à vista agora.</summary>
        public int PecasAVista()
        {
            int n = 0;
            foreach (var p in _pecas) if (p != null && p.activeSelf) n++;
            return n + (_resultado != null && _resultado.activeSelf ? 1 : 0);
        }

        /// <summary>Para o teste: o meio da mesa (ou do chão) onde a animação acontece.</summary>
        public Vector3 Centro => _centro;

        void Awake()
        {
            _rede = GetComponent<JogadorRede>();
            _inv = GetComponent<InventarioJogador>();
        }

        // ------------------------------------------------------------------ no dono

        /// <summary>No dono: começa a animação da receita (false: aqui não dá, e o craft sai na hora).</summary>
        public bool Comecar(int receita)
        {
            if (Tocando || _rede == null || _inv == null || !_rede.IsOwner || receita < 0 || receita >= Receitas.Todas.Length) return false;
            if (ModoVideo.Ligado || MenuOnline.Cinema || (_rede.Passageiro != null && _rede.Passageiro.NoCarro)) return false;
            var vida = GetComponent<VidaJogador>();
            if (vida != null && (vida.Vida.Value <= 0f || vida.Caido.Value)) return false;
            if (!Lugar(Receitas.Todas[receita], out var centro, out float giro, out bool naMesa)) return false;
            Preparar(receita, centro, giro, naMesa);
            _doDono = true;
            _yaw0 = transform.eulerAngles.y;
            _pitch0 = _rede.Pitch.Value;
            var mov = GetComponent<MovimentoPrimeiraPessoa>();
            _abaixouParaOCraft = !naMesa && mov != null && !mov.Agachado && mov.PedirAbaixar(true);
            CameraDeLado();
            _inv.AnunciarCraft(receita, centro, giro, naMesa);
            GetComponent<TelaDoInventario>()?.FecharParaOCraft();
            _cinemaAntes = MenuOnline.Cinema;
            MenuOnline.Cinema = true;   // sem os comandos, sem o HUD; o mouse continua preso
            CameraTerceiraPessoa.NoCraft = true;
            return true;
        }

        // onde: a mesa de oficina (as receitas dela); a mesa de ferramentas montada ou a bancada mais perto; senão, o chão
        bool Lugar(Receita r, out Vector3 centro, out float giro, out bool naMesa)
        {
            var pes = transform.position;
            naMesa = false;
            var b = Bancada.MaisPerto(pes, r.oficina);
            var mundo = ConstrucoesNoMundo.Instancia;
            if (!r.oficina && mundo != null && mundo.MesaMaisPerto(pes, out var pm, out float gm)
                && (b == null || (pm - pes).sqrMagnitude <= (b.transform.position - pes).sqrMagnitude))
            {
                centro = pm + Vector3.up * ConstrucoesNoMundo.TampoDaMesa;
                giro = gm;
                naMesa = true;
                return true;
            }
            if (b != null && TampoDa(b, pes, out centro, out giro))
            {
                naMesa = true;
                return true;
            }
            // à mão: no chão, uns 75 cm na frente
            var frente = transform.forward;
            frente.y = 0f;
            frente = frente.sqrMagnitude > 0.01f ? frente.normalized : Vector3.forward;
            centro = pes + frente * 0.75f;
            int mascara = ~((1 << 2) | (1 << JogadorRede.CamadaJogador) | (1 << JogadorRede.CamadaBracos) | (1 << JogadorRede.CamadaDaCabeca));
            centro.y = Physics.Raycast(centro + Vector3.up * 1.2f, Vector3.down, out var chao, 2.6f, mascara, QueryTriggerInteraction.Ignore)
                ? chao.point.y : pes.y;
            giro = Mathf.Atan2(frente.x, frente.z) * Mathf.Rad2Deg;
            return true;
        }

        // o tampo de uma bancada da cena: o alto da caixa dela, virado para quem está na frente
        static bool TampoDa(Bancada b, Vector3 pes, out Vector3 centro, out float giro)
        {
            centro = default;
            giro = 0f;
            Bounds caixa;
            var col = b.GetComponent<Collider>();
            if (col != null) caixa = col.bounds;
            else
            {
                var rs = b.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) return false;
                caixa = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) caixa.Encapsulate(rs[i].bounds);
            }
            // (uma caixa alta demais, como um armário inteiro, não é um tampo: fica na altura da mão)
            // (a mesa de oficina, 25/11/2026: o tampo dela; a caixa vai até o alto do painel de trás)
            float alto = b.alturaDoTampo > 0f ? b.transform.TransformPoint(new Vector3(0f, b.alturaDoTampo, 0f)).y : Mathf.Min(caixa.max.y, pes.y + 1.15f);
            centro = new Vector3(caixa.center.x, alto, caixa.center.z);
            var d = centro - pes;
            d.y = 0f;
            giro = d.sqrMagnitude > 0.01f ? Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg : b.transform.eulerAngles.y;
            return true;
        }

        // ------------------------------------------------------------------ em todas as máquinas

        /// <summary>Nos outros (o RPC): mostra os materiais e o personagem trabalhando, sem a câmera.</summary>
        public void Mostrar(int receita, Vector3 centro, float giro, bool naMesa)
        {
            if (_doDono) return;
            Preparar(receita, centro, giro, naMesa);
        }

        void Preparar(int receita, Vector3 centro, float giro, bool naMesa)
        {
            Limpar();
            _receita = receita;
            _centro = centro;
            _giro = Quaternion.Euler(0f, giro, 0f);
            _naMesa = naMesa;
            _comeco = Time.time;
            _golpe = 0;
            _somTocou = false;
            var r = Receitas.Todas[receita];
            // os materiais: até 2 de cada (a pilha de pregos, 1), no máximo 6
            var itens = new List<Item>();
            foreach (var g in r.gasta)
            {
                int n = Itens.EmPilha(g.item) && g.qtd > 4 ? 1 : Mathf.Clamp(g.qtd, 1, 2);
                for (int i = 0; i < n && itens.Count < 6; i++) itens.Add(g.item);
            }
            float passo = naMesa ? 0.24f : 0.22f, largura = naMesa ? 1.0f : 0.85f;
            if (itens.Count > 1 && passo * (itens.Count - 1) > largura) passo = largura / (itens.Count - 1);
            // (na mesa de oficina, de 2 m, a morsa fica num canto: as peças, no meio, não chegam nela)
            float meio = 0f, x0 = meio - passo * (itens.Count - 1) / 2f;
            for (int i = 0; i < itens.Count; i++)
            {
                var local = new Vector3(x0 + passo * i, 0f, (i & 1) == 0 ? -0.07f : 0.07f);
                _pecas.Add(Peca(itens[i], local, 17f * i - 20f));
                _somemEm.Add(_comeco + SomemDe + (SomemAte - SomemDe) * (i + 0.5f) / itens.Count);
            }
            _resultado = Peca(r.resultado, new Vector3(meio, 0f, -0.04f), 8f, MaiorFeito);
            _escalaDoResultado = _resultado.transform.localScale;
            _resultado.SetActive(false);
        }

        GameObject Peca(Item item, Vector3 local, float giroProprio, float maiorQueCabe = MaiorPeca)
        {
            var go = new GameObject($"Craft ({Itens.Nome(item)})");
            var mundo = ItensNoMundo.Instancia;
            var malha = mundo != null ? mundo.MalhaDe(item) : null;
            if (malha == null) return go;   // (sem modelo: nada à vista)
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mundo.MateriaisDe(item);
            var b = malha.bounds;
            float maior = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            float escala = maior > maiorQueCabe ? maiorQueCabe / maior : 1f;
            var rot = _giro * Quaternion.Euler(0f, giroProprio, 0f)
                      * (Itens.EhArma(item) || Itens.EhBranca(item) ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity);
            // o apoio: o mais baixo da caixa girada fica no tampo, e o meio dela no ponto
            float baixo = float.MaxValue;
            Vector3 centroDaCaixa = Vector3.zero;
            for (int c = 0; c < 8; c++)
            {
                var p = rot * (escala * new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z));
                baixo = Mathf.Min(baixo, p.y);
                centroDaCaixa += p / 8f;
            }
            go.transform.localScale = Vector3.one * escala;
            go.transform.SetPositionAndRotation(_centro + _giro * local + new Vector3(-centroDaCaixa.x, -baixo + 0.003f, -centroDaCaixa.z), rot);
            return go;
        }

        void Update()
        {
            if (!Tocando) return;
            float t = Time.time - _comeco;
            if (_doDono)
            {
                var vida = GetComponent<VidaJogador>();
                if (_rede == null || (vida != null && (vida.Vida.Value <= 0f || vida.Caido.Value)))
                {
                    Acabar(false);   // morreu ou caiu no meio: nada sai, os materiais ficam
                    return;
                }
                bool pular = t > 0.15f && ((Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                                           || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame));
                if (pular || t >= Duracao || MenuOnline.Aberto)
                {
                    Acabar(true);
                    return;
                }
                Virar(t);
            }
            else if (t >= Duracao)
            {
                Limpar();
                _receita = -1;
                return;
            }
            // o martelo (o som com os 3 golpes) e o braço descendo antes de cada um
            if (!_somTocou && t >= SomDoMartelo)
            {
                _somTocou = true;
                if (_martelo == null) _martelo = Resources.Load<AudioClip>("Armadilhas/martelo_1");
                Efeitos.Som(_martelo != null ? _martelo : SomDasArmadilhas.Martelo, _centro, 0.75f, true, 25f);
            }
            if (_golpe < Golpes.Length && t >= Golpes[_golpe] - AntesDoGolpe)
            {
                _golpe++;
                if (_rede != null && _rede.animadorCorpo != null) _rede.animadorCorpo.SetTrigger(FireId);
            }
            // os materiais somem, um a um
            for (int i = 0; i < _pecas.Count; i++)
                if (_pecas[i] != null && _pecas[i].activeSelf && Time.time >= _somemEm[i])
                {
                    _pecas[i].SetActive(false);
                    Efeitos.Som(Efeitos.Largar, _pecas[i].transform.position, 0.3f, true, 12f);
                }
            // e o feito aparece, crescendo
            if (_resultado != null && t >= ApareceEm)
            {
                if (!_resultado.activeSelf)
                {
                    _resultado.SetActive(true);
                    Efeitos.Som(Efeitos.Carregador, _resultado.transform.position, 0.45f, true, 12f);
                }
                float k = Mathf.Clamp01((t - ApareceEm) / Cresce);
                float s = 1f + 0.12f * Mathf.Sin(k * Mathf.PI);   // (um pulinho)
                _resultado.transform.localScale = _escalaDoResultado * (k * s);
            }
        }

        // ------------------------------------------------------------------ a câmera de lado (só no dono)

        // de lado, um pouco para o lado de quem trabalha e de cima: o personagem de um lado do quadro e o tampo (ou o chão) no
        // meio. O lado com mais espaço (sem parede no caminho) ganha
        void CameraDeLado()
        {
            var pes = transform.position;
            var paraMesa = _centro - pes;
            paraMesa.y = 0f;
            paraMesa = paraMesa.sqrMagnitude > 0.01f ? paraMesa.normalized : Flat(transform.forward);
            var lado = Vector3.Cross(Vector3.up, paraMesa).normalized;
            // (à mão, no chão: o meio entre o personagem e os materiais, mais longe e mais alto, para a cabeça caber no quadro)
            _camAlvo = _naMesa ? Vector3.Lerp(_centro, pes + Vector3.up * 1.2f, 0.2f) : Vector3.Lerp(_centro, pes, 0.3f) + Vector3.up * 0.6f;
            float aoLado = _naMesa ? 1.7f : 2.4f, recua = _naMesa ? 0.9f : 1.1f, sobe = _naMesa ? 0.75f : 0.95f;
            Vector3 melhor = _camAlvo + Vector3.up * sobe;
            float folgaMelhor = -1f;
            foreach (float s in new[] { 1f, -1f })
            {
                var quer = _camAlvo + lado * (aoLado * s) - paraMesa * recua + Vector3.up * sobe;
                var d = quer - _camAlvo;
                float l = d.magnitude;
                float livre = l;
                if (Physics.SphereCast(_camAlvo, 0.2f, d / l, out var bate, l, MascaraDaCamera, QueryTriggerInteraction.Ignore))
                    livre = Mathf.Max(0.3f, bate.distance - 0.05f);
                if (livre / l > folgaMelhor)
                {
                    folgaMelhor = livre / l;
                    melhor = _camAlvo + d / l * livre;
                }
                if (livre >= l - 0.01f) break;   // (a direita livre: fica com ela)
            }
            _camPos = melhor;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }

        void LateUpdate()
        {
            if (!_doDono || !Tocando || _rede == null || _rede.cameraMundo == null) return;
            var cam = _rede.cameraMundo.transform;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - _comeco) / TempoDaCamera));
            var rot = Quaternion.LookRotation(_camAlvo - _camPos);
            // de onde a câmera estava (a cabeça, a CameraTerceiraPessoa já a pôs neste quadro) até o lado
            cam.SetPositionAndRotation(Vector3.Lerp(cam.position, _camPos, k), Quaternion.Slerp(cam.rotation, rot, k));
        }

        // o dono vira para a mesa (ou para o chão na frente), suave
        void Virar(float t)
        {
            var mov = GetComponent<MovimentoPrimeiraPessoa>();
            if (mov == null) return;
            var olho = transform.position + Vector3.up * 1.6f;
            var d = _centro - olho;
            var plano = new Vector2(d.x, d.z);
            float yaw = plano.sqrMagnitude > 0.04f ? Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg : _yaw0;
            float pitch = Mathf.Clamp(Mathf.Atan2(-d.y, Mathf.Max(0.2f, plano.magnitude)) * Mathf.Rad2Deg, 5f, _naMesa ? 45f : 40f);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / TempoDeVirar));
            mov.Olhar(Mathf.LerpAngle(_yaw0, yaw, k), Mathf.Lerp(_pitch0, pitch, k));
        }

        /// <summary>
        /// No dono: para a animação sem fazer nada (os materiais ficam; a câmera e os comandos voltam). (25/11/2026, a revisão)
        /// As cenas de cinema chamam isto antes de ligar o <see cref="MenuOnline.Cinema"/>: o flag é um só, e o craft que
        /// acabava no meio da tela dos 5 anos ou da passagem do Dia 0 o desligava, e o jogador voltava a se mexer (voando)
        /// no meio da tela preta, com o HUD e a mochila de volta.
        /// </summary>
        public void Cancelar()
        {
            if (Tocando && _doDono) Acabar(false);
        }

        /// <summary>Para o craft do jogador desta máquina, se houver um (antes de uma cena de cinema começar).</summary>
        public static void CancelarNoJogadorLocal()
        {
            var j = JogadorRede.Local;
            var a = j != null ? j.GetComponent<AnimacaoDoCraft>() : null;
            if (a != null) a.Cancelar();
        }

        void Acabar(bool fazer)
        {
            int k = _receita;
            Limpar();
            _receita = -1;
            if (!_doDono) return;
            _doDono = false;
            Devolver();
            if (fazer && k >= 0 && _inv != null) _inv.Criar(k);
            if (fazer && !MenuOnline.Aberto) GetComponent<TelaDoInventario>()?.AbrirNaAbaCriar();
        }

        // a câmera e os comandos de volta (e o personagem de pé, se ele se abaixou para o craft)
        void Devolver()
        {
            if (_abaixouParaOCraft)
            {
                _abaixouParaOCraft = false;
                GetComponent<MovimentoPrimeiraPessoa>()?.PedirAbaixar(false);
            }
            CameraTerceiraPessoa.NoCraft = false;
            if (!_cinemaAntes) MenuOnline.Cinema = false;
        }

        void Limpar()
        {
            foreach (var p in _pecas)
                if (p != null) Destroy(p);
            _pecas.Clear();
            _somemEm.Clear();
            if (_resultado != null) Destroy(_resultado);
            _resultado = null;
        }

        void OnDisable()
        {
            Limpar();
            _receita = -1;
            if (_doDono)
            {
                _doDono = false;
                Devolver();
            }
        }
    }
}
