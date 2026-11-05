using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O curativo com tempo (05/11/2026, o sistema de vida por membro: "a vida não se regenera mais sozinha ... só o
    /// tratamento cura"), só no dono. A atadura, a bandagem de pano, a tala, o kit médico, os comprimidos, o xarope e, na
    /// etapa 2, a bandagem antisséptica, o spray e o antibiótico não agem na hora: <see cref="Comecar"/> confere o item na
    /// mochila e o corpo (<see cref="PartesDoCorpo.PodeTratar"/>: escolhe a parte quando ela vem
    /// <see cref="PartesDoCorpo.Sortear"/>, o pior caso primeiro) e começa a barra (<see cref="PartesDoCorpo.TempoDe"/>:
    /// a atadura 3 s, as bandagens 4 s, a tala 5 s, o kit 6 s, o spray 2 s, o que se toma 1,7 s), com os sons
    /// (<see cref="SomDosCurativos"/>: o pano rasgando, a faixa roçando, o zíper do kit, o velcro no fim, a cartela; o
    /// chiado curto do spray). Um golpe, correr, cair, entrar no carro, trocar o item da mão ou o item sair da mochila param o curativo
    /// (o item não é gasto). No fim, o anfitrião confere de novo, gasta o item e trata (<see cref="InventarioJogador.Tratar"/>).
    /// A barra fica embaixo da mira, no lugar da de segurar o E (o mesmo desenho); com a tela do inventário aberta, a
    /// aba CORPO mostra o progresso (lendo <see cref="Ativo"/>, <see cref="Progresso"/> e <see cref="Texto"/>).
    /// Começa pelo clique com o item na mão (<see cref="ArmaJogador"/>), pelo "Usar" da mochila e pela aba CORPO.
    /// </summary>
    public class Tratamento : MonoBehaviour
    {
        /// <summary>O do jogador desta máquina.</summary>
        public static Tratamento Local { get; private set; }

        /// <summary>Tratando agora (a barra correndo): sem atirar, golpear, trocar de item nem largar.</summary>
        public bool Ativo { get; private set; }
        /// <summary>Quanto já foi (0 a 1).</summary>
        public float Progresso => Ativo && _tempo > 0f ? Mathf.Clamp01((Time.time - _inicio) / _tempo) : 0f;
        /// <summary>O item que está sendo usado.</summary>
        public Item Item => _item;
        /// <summary>A parte tratada (o kit: o tronco, mas ele trata o corpo todo; o analgésico: a cabeça).</summary>
        public ParteDoCorpo Parte { get; private set; }
        /// <summary>"Enfaixando o braço esquerdo…", "Pondo a tala na perna direita…", "Usando o kit médico…".</summary>
        public string Texto { get; private set; }
        /// <summary>O amigo que está sendo tratado (a fase 3; null: o próprio corpo).</summary>
        public JogadorRede Alvo { get; private set; }
        /// <summary>Até onde o amigo pode estar (m): mais longe, o curativo para.</summary>
        public const float AlcanceDoAmigo = 2.8f;

        /// <summary>Acima desta velocidade (m/s), ele está correndo: o curativo para (andar, 2; correr, 4,2).</summary>
        const float VelocidadeQueCorre = 3.2f;
        /// <summary>De quanto em quanto tempo a faixa dá a volta (o som de pano roçando).</summary>
        const float VoltaDaFaixa = 1.1f;

        VidaJogador _vida;
        JogadorRede _rede;
        InventarioJogador _inventario;
        ArmaJogador _arma;
        TelaDoInventario _tela;
        Item _item;
        ushort _id, _maoNoComeco;
        float _inicio, _tempo, _proximaVolta;
        bool _rasgouNoKit, _borrifou;
        PuloDaJanela _pulo;
        // o pedido mandado ao anfitrião e ainda sem resposta: o mesmo item não começa outra barra até o corpo voltar
        ushort _esperaId;
        float _esperaAte;
        // o curativo é num amigo (o Alvo pode virar null se ele sair do jogo: aí para, e não cai no próprio corpo)
        bool _noAmigo;

        void Awake()
        {
            Local = this;
            _vida = GetComponent<VidaJogador>();
            _rede = GetComponent<JogadorRede>();
            _inventario = GetComponent<InventarioJogador>();
            _arma = GetComponent<ArmaJogador>();
            _tela = GetComponent<TelaDoInventario>();
            _pulo = GetComponent<PuloDaJanela>();
        }

        void OnEnable()
        {
            if (_vida != null) _vida.Golpeado += AoGolpe;
        }

        void OnDisable()
        {
            if (_vida != null) _vida.Golpeado -= AoGolpe;
            Ativo = false;
            Alvo = null;
            _noAmigo = false;
        }

        void OnDestroy()
        {
            if (Local == this) Local = null;
        }

        /// <summary>
        /// Começa a tratar com o item <paramref name="id"/> da mochila, na <paramref name="parte"/>
        /// (<see cref="PartesDoCorpo.Sortear"/>: automático). Se não pode (nada a tratar, correndo, no carro), avisa o
        /// motivo no HUD e devolve false (o item não é gasto).
        /// </summary>
        public bool Comecar(ushort id, ParteDoCorpo parte) => ComecarEm(id, parte, null);

        /// <summary>
        /// A fase 3 (05/11/2026, a pedido: o amigo faz o curativo em você): começa a tratar o <paramref name="alvo"/> com o
        /// item <paramref name="id"/> da SUA mochila, na <paramref name="parte"/> dele (o TratarOAmigo escolhe pela mira).
        /// </summary>
        public bool ComecarNoAmigo(ushort id, ParteDoCorpo parte, JogadorRede alvo) => alvo != null && ComecarEm(id, parte, alvo);

        bool ComecarEm(ushort id, ParteDoCorpo parte, JogadorRede alvo)
        {
            if (Ativo || _vida == null || _inventario == null) return false;
            if (alvo != null && (!alvo.AlvoValido || alvo.Vida == null || alvo == _rede || NoCarro(alvo))) return false;
            var item = _inventario.Do(id);
            if (item == Item.Nada || !PartesDoCorpo.EhTratamento(item)) return false;
            if (_arma != null && (_arma.Usando || _arma.Montando)) return false;
            if (_vida.Caido.Value) return false;
            if (id == _esperaId && Time.time < _esperaAte) return false;   // o anterior ainda não voltou do anfitrião
            string motivo = null;
            bool pode = false;
            if (_rede != null && _rede.Passageiro != null && _rede.Passageiro.NoCarro) motivo = "No carro, não dá para se tratar";
            else if (_arma != null && _arma.MaosOcupadas) motivo = "Com o pneu nas mãos, não dá para se tratar";
            else if (_arma != null && _arma.Recarregando) motivo = "Recarregando: espere as mãos ficarem livres";
            else if (_pulo != null && _pulo.Pulando) motivo = "Pulando a janela, não dá para se tratar";
            else if (_rede != null && _rede.Velocidade > VelocidadeQueCorre) motivo = "Correndo, não dá para se tratar";
            else if (alvo != null && (alvo.transform.position - transform.position).sqrMagnitude > AlcanceDoAmigo * AlcanceDoAmigo) motivo = "Chegue mais perto do amigo";
            else if (alvo != null) pode = PartesDoCorpo.PodeTratar(alvo.Vida.Corpo.Value, alvo.Vida.Vida.Value, alvo.Vida.vidaMaxima, item, ref parte, out motivo);
            else pode = PartesDoCorpo.PodeTratar(_vida.Corpo.Value, _vida.Vida.Value, _vida.vidaMaxima, item, ref parte, out motivo);
            if (!pode)
            {
                if (!string.IsNullOrEmpty(motivo)) InventarioJogador.AvisoLocal(motivo);
                return false;
            }
            _item = item;
            _id = id;
            _maoNoComeco = _inventario.IdNaMao;
            Parte = parte;
            Alvo = alvo;
            _noAmigo = alvo != null;
            Texto = TextoDe(item, parte);
            if (alvo != null)
            {
                string nome = alvo.Aparencia.Value.nome.ToString();
                Texto = Texto.TrimEnd('…') + (string.IsNullOrEmpty(nome) ? " do amigo…" : $" de {nome}…");
                _inventario.AvisarQueTrata(alvo, item, parte);
            }
            _inicio = Time.time;
            _tempo = Mathf.Max(0.2f, PartesDoCorpo.TempoDe(item));
            _proximaVolta = 0.6f;
            _rasgouNoKit = false;
            Ativo = true;
            Sinalizar();
            _borrifou = false;
            // o som do começo: o pano rasgando (as faixas, a antisséptica também), o zíper do kit, a faixa da tala, a
            // cartela (o xarope, o antibiótico e o spray: a tampa; o spray chia logo depois, no Update)
            if (PartesDoCorpo.Enfaixa(item)) Som(SomDosCurativos.Rasgo, 0.55f);
            else if (item == Item.KitMedico) Som(SomDosCurativos.Ziper, 0.55f);
            else if (item == Item.Tala) Som(SomDosCurativos.Enrolar, 0.5f);
            else if (item == Item.Comprimidos) Som(SomDosCurativos.Cartela, 0.55f);
            else Som(Efeitos.AbrirTampa, item == Item.SprayAntisseptico ? 0.35f : 0.45f);
            return true;
        }

        /// <summary>Para o curativo sem gastar o item (com o motivo no HUD, se vier).</summary>
        public void Cancelar(string motivo = null)
        {
            if (!Ativo) return;
            Ativo = false;
            Alvo = null;
            _noAmigo = false;
            Sinalizar();
            if (!string.IsNullOrEmpty(motivo)) InventarioJogador.AvisoLocal(motivo);
        }

        void AoGolpe(ParteDoCorpo parte, TipoDeGolpe tipo, Lesao lesao, bool abriu)
        {
            if (Ativo) Cancelar("O golpe interrompeu o tratamento");
        }

        void Update()
        {
            if (!Ativo) return;
            if (_vida == null || _inventario == null || _vida.Caido.Value)
            {
                Cancelar();
                return;
            }
            if (_rede != null && _rede.Passageiro != null && _rede.Passageiro.NoCarro)
            {
                Cancelar("No carro, o tratamento parou");
                return;
            }
            if (_rede != null && _rede.Velocidade > VelocidadeQueCorre)
            {
                Cancelar("Correu: o tratamento parou");
                return;
            }
            if (_pulo != null && _pulo.Pulando)
            {
                Cancelar("Pulou a janela: o tratamento parou");
                return;
            }
            if (_noAmigo && (Alvo == null || !Alvo.AlvoValido || NoCarro(Alvo)
                             || (Alvo.transform.position - transform.position).sqrMagnitude > AlcanceDoAmigo * AlcanceDoAmigo))
            {
                Cancelar("O amigo se afastou: o tratamento parou");
                return;
            }
            if (_inventario.Do(_id) != _item)
            {
                Cancelar($"{TelaDoInventario.Maiuscula(Itens.NomeComArtigo(_item))} saiu da mochila: o tratamento parou");
                return;
            }
            ushort mao = _inventario.IdNaMao;
            if (mao != _maoNoComeco)
            {
                // a mão esvaziou sozinha (a tocha que apagou de vez): o curativo segue; outra coisa na mão, para
                if (mao == 0 && _inventario.Do(_maoNoComeco) == Item.Nada) _maoNoComeco = 0;
                else
                {
                    Cancelar("Trocou de item: o tratamento parou");
                    return;
                }
            }
            float t = Time.time - _inicio;
            // a faixa dando a volta (o kit também enfaixa; a tala é amarrada); no kit, a gaze rasgando logo depois do zíper.
            // O que vai pela boca e o spray não enrolam nada
            bool enrola = PartesDoCorpo.Enfaixa(_item) || _item == Item.Tala || _item == Item.KitMedico;
            if (enrola && t >= _proximaVolta && t < _tempo - 0.5f)
            {
                Som(SomDosCurativos.Enrolar, 0.45f);
                _proximaVolta += VoltaDaFaixa;
            }
            // o spray: um chiado curto, depois de tirar a tampa
            if (_item == Item.SprayAntisseptico && !_borrifou && t >= 0.55f)
            {
                _borrifou = true;
                Som(Borrifo, 0.5f);
            }
            if (_item == Item.KitMedico && !_rasgouNoKit && t >= 1.3f)
            {
                _rasgouNoKit = true;
                Som(SomDosCurativos.Rasgo, 0.45f);
            }
            if (t >= _tempo) Terminar();
        }

        // a barra acabou: o anfitrião confere, gasta o item e trata
        void Terminar()
        {
            Ativo = false;
            Sinalizar();
            // o fim: o gole (o que vai pela boca), a tampa de volta (o spray), o velcro ou a fita (o resto)
            if (PartesDoCorpo.PelaBoca(_item)) Efeitos.Som(Efeitos.Gole, Ouvido, 0.45f, false);
            else if (_item == Item.SprayAntisseptico) Som(Efeitos.AbrirTampa, 0.25f);
            else Som(SomDosCurativos.Fecho, 0.55f);
            if (!_inventario.IsServer)
            {
                _esperaId = _id;
                _esperaAte = Time.time + 1f;
            }
            if (_noAmigo)
            {
                if (Alvo != null) _inventario.TratarOAmigo(_id, Parte, Alvo);
            }
            else _inventario.Tratar(_id, Parte);
            Alvo = null;
            _noAmigo = false;
        }

        /// <summary>O jogador está sentado num carro (o curativo no amigo não vale lá dentro).</summary>
        public static bool NoCarro(JogadorRede j) => j != null && j.Passageiro != null && j.Passageiro.NoCarro;

        // a pose do curativo para os outros (a fase 3): o que ele trata, pela rede (o dono escreve)
        void Sinalizar()
        {
            if (_vida == null || !_vida.IsSpawned || !_vida.IsOwner) return;
            byte v = !Ativo ? (byte)0 : _noAmigo ? VidaJogador.TratandoOAmigo
                   : PartesDoCorpo.PelaBoca(_item) ? VidaJogador.TratandoPelaBoca
                   : PartesDoCorpo.Valida(Parte) ? (byte)((int)Parte + 1) : (byte)((int)ParteDoCorpo.Tronco + 1);
            if (_vida.Tratando.Value != v) _vida.Tratando.Value = v;
        }

        Vector3 Ouvido => _rede != null && _rede.cameraMundo != null ? _rede.cameraMundo.transform.position : transform.position + Vector3.up * 1.6f;

        void Som(AudioClip clip, float volume) => Efeitos.Som(clip, Ouvido, volume, false);

        /// <summary>
        /// "Enfaixando o braço esquerdo…", "Pondo a tala na perna direita…", "Borrifando o spray no braço esquerdo…",
        /// "Usando o kit médico…", "Tomando o antibiótico…".
        /// </summary>
        static string TextoDe(Item item, ParteDoCorpo parte)
        {
            string acao = PartesDoCorpo.Acao(item);
            if (PartesDoCorpo.CorpoTodo(item) || PartesDoCorpo.PelaBoca(item) || !PartesDoCorpo.Valida(parte)) return acao + "…";
            if (item == Item.Tala || item == Item.SprayAntisseptico) return $"{acao} {PartesDoCorpo.NaParte(parte)}…";
            return $"{acao} {PartesDoCorpo.NomeComArtigo(parte)}…";
        }

        // o chiado do spray (gerado por código, uma vez só, como os do SomDosCurativos): o ruído agudo que abre rápido,
        // segura um instante e morre (uns 0,35 s)
        static AudioClip _borrifo;
        static AudioClip Borrifo => _borrifo != null ? _borrifo : (_borrifo = SomGerado.Clip("Spray", GerarBorrifo(71)));

        static float[] GerarBorrifo(int semente)
        {
            const int taxa = 44100;
            const float dur = 0.36f;
            var r = new System.Random(semente);
            var y = new float[Mathf.RoundToInt(dur * taxa)];
            // um passa-alta (o ruído menos o grave dele) e um passa-baixa suave por cima: o "ssss" do bico
            float aGrave = 1f - Mathf.Exp(-2f * Mathf.PI * 2200f / taxa), aMacio = 1f - Mathf.Exp(-2f * Mathf.PI * 9000f / taxa);
            float grave = 0f, macio = 0f, max = 1e-6f;
            for (int i = 0; i < y.Length; i++)
            {
                float t = i / (float)taxa;
                float x = (float)(r.NextDouble() * 2 - 1);
                grave += aGrave * (x - grave);
                macio += aMacio * ((x - grave) - macio);
                // abre em 15 ms, segura, e solta nos últimos 120 ms (o dedo saindo do bico), com um tremido leve
                float env = Mathf.Clamp01(t / 0.015f) * Mathf.Clamp01((dur - t) / 0.12f) * (0.9f + 0.1f * Mathf.Sin(t * 90f));
                y[i] = macio * env;
                max = Mathf.Max(max, Mathf.Abs(y[i]));
            }
            float k = 0.6f / max;
            for (int i = 0; i < y.Length; i++) y[i] *= k;
            return y;
        }

        // a barra embaixo da mira, como a de segurar o E (ArmaJogador): com a tela do inventário aberta, a aba CORPO mostra
        void OnGUI()
        {
            if (!Ativo || MenuOnline.Aberto || MenuOnline.Cinema || MenuOnline.ComTela || ModoVideo.Ligado || (_tela != null && _tela.Aberta)) return;
            if (_vida != null && _vida.Caido.Value) return;
            float u = Screen.height / 720f, cx = Screen.width / 2f, cy = Screen.height / 2f;
            float k = Progresso, w = 220 * u, h = Mathf.Max(3f, Mathf.Round(6 * u));
            var barra = new Rect(cx - w / 2, cy + 74 * u, w, h);
            Retangulo(barra.x - 1, barra.y - 1, barra.width + 2, barra.height + 2, new Color(0f, 0f, 0f, 0.6f));
            Retangulo(barra.x, barra.y, barra.width * k, barra.height, new Color(0.95f, 0.85f, 0.45f, 0.95f));
            if (string.IsNullOrEmpty(Texto)) return;
            var pequeno = EstilosDoHud.Rotulo(Mathf.RoundToInt(14 * u), FontStyle.Normal, TextAnchor.UpperCenter);
            pequeno.normal.textColor = new Color(0.9f, 0.88f, 0.8f);
            GUI.Label(new Rect(cx - 300 * u, cy + 84 * u, 600 * u, 24 * u), Texto, pequeno);
        }

        static void Retangulo(float x, float y, float w, float h, Color c)
        {
            var antes = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = antes;
        }
    }
}
