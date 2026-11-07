using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O curativo no amigo (05/11/2026, a fase 3 do sistema de vida, a pedido: "Mirar e segurar E"), só no dono. Mirando
    /// num amigo machucado a até <see cref="Tratamento.AlcanceDoAmigo"/>, embaixo da mira aparece o que dá para fazer nele
    /// com o que está na SUA mochila ("Enfaixar o braço esquerdo de Fulano (atadura)"); tocar o E troca a opção, segurar o E
    /// começa o curativo (<see cref="Tratamento.ComecarNoAmigo"/>: a barra, os sons, a pose; o amigo é avisado). As opções
    /// saem das mesmas regras do próprio corpo (<see cref="PartesDoCorpo.PodeTratar"/>, com o corpo do amigo que vem pela
    /// rede): o sangue primeiro, depois o osso, a infecção (o antibiótico), a febre (o xarope), o resto.
    /// </summary>
    public class TratarOAmigo : MonoBehaviour
    {
        const float SegurarPor = 0.35f;     // s: menos que isso é um toque (troca a opção)
        const float RefazerACada = 0.25f;   // s: as opções são refeitas de tempos em tempos (não a cada quadro)

        JogadorRede _rede, _alvo;
        VidaJogador _vida;
        InventarioJogador _inventario;
        TelaDoInventario _tela;
        ArmaJogador _arma;
        InputAction _interagir;   // o E (ou o botão do controle), o mesmo de interagir
        readonly List<(ushort id, Item item, ParteDoCorpo parte, int prioridade)> _opcoes = new List<(ushort, Item, ParteDoCorpo, int)>();
        int _escolhida;
        float _proximaRefeita, _apertouEm = -1f;
        bool _comecou;
        readonly GUIContent _conteudo = new GUIContent();

        void Awake()
        {
            _rede = GetComponent<JogadorRede>();
            _vida = GetComponent<VidaJogador>();
            _inventario = GetComponent<InventarioJogador>();
            _tela = GetComponent<TelaDoInventario>();
            _arma = GetComponent<ArmaJogador>();
            _interagir = InputSystem.actions != null ? InputSystem.actions.FindAction("Player/Interact", false) : null;
        }

        bool PodeMirar => _rede != null && _rede.IsSpawned && _rede.IsOwner && MenuOnline.Controla && !MenuOnline.Cinema && !ModoVideo.Ligado
                          && (_tela == null || !_tela.Aberta) && _vida != null && !_vida.Caido.Value && !_vida.Desmaiado.Value && !_vida.Dormindo.Value
                          && (_rede.Passageiro == null || !_rede.Passageiro.NoCarro)
                          && (Tratamento.Local == null || !Tratamento.Local.Ativo);

        void Update()
        {
            if (!PodeMirar)
            {
                _alvo = null;
                _apertouEm = -1f;
                return;
            }
            var alvo = AmigoNaMira();
            if (alvo != _alvo)
            {
                _alvo = alvo;
                _escolhida = 0;
                _proximaRefeita = 0f;
            }
            if (_alvo == null)
            {
                _apertouEm = -1f;
                return;
            }
            if (Time.time >= _proximaRefeita) RefazerAsOpcoes();
            // desmaiado (06/11/2026, a mente): segurar o E acorda, antes de qualquer curativo
            if (_alvo.Vida.Desmaiado.Value || _alvo.Vida.Dormindo.Value)
            {
                if (_interagir == null) return;
                if (_interagir.WasPressedThisFrame())
                {
                    _apertouEm = Time.time;
                    _comecou = false;
                }
                if (_apertouEm >= 0f && _interagir.IsPressed() && !_comecou && Time.time - _apertouEm >= SegurarPor)
                {
                    _comecou = true;
                    _inventario.AcordarOAmigo(_alvo);
                }
                if (_interagir.WasReleasedThisFrame()) _apertouEm = -1f;
                return;
            }
            if (_interagir == null || _opcoes.Count == 0) return;
            // tocar troca a opção; segurar começa o curativo
            if (_interagir.WasPressedThisFrame())
            {
                _apertouEm = Time.time;
                _comecou = false;
            }
            if (_apertouEm >= 0f && _interagir.IsPressed() && !_comecou && Time.time - _apertouEm >= SegurarPor)
            {
                _comecou = true;
                var o = _opcoes[Mathf.Clamp(_escolhida, 0, _opcoes.Count - 1)];
                Tratamento.Local?.ComecarNoAmigo(o.id, o.parte, _alvo);
            }
            if (_interagir.WasReleasedThisFrame())
            {
                if (_apertouEm >= 0f && !_comecou && _opcoes.Count > 1) _escolhida = (_escolhida + 1) % _opcoes.Count;
                _apertouEm = -1f;
            }
        }

        // o amigo vivo na frente da mira, a até o alcance (as paredes tapam)
        JogadorRede AmigoNaMira()
        {
            var olho = _rede.Olho;
            if (olho == null) return null;
            // o E já tem outra coisa na mira (a janela aberta, o item no chão, a mochila): ela ganha
            if (_arma != null && _arma.TemAlvoDoE) return null;
            var acertos = Physics.RaycastAll(olho.position, olho.forward, Tratamento.AlcanceDoAmigo + 0.4f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(acertos, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in acertos)
            {
                var j = h.collider.GetComponentInParent<JogadorRede>();
                if (j == _rede) continue;   // o próprio corpo
                if (j == null) return null;   // uma parede, um móvel
                if (!j.AlvoValido || j.Vida == null || Tratamento.NoCarro(j)) return null;
                if ((j.transform.position - transform.position).sqrMagnitude > Tratamento.AlcanceDoAmigo * Tratamento.AlcanceDoAmigo) return null;
                return j;
            }
            return null;
        }

        // o que dá para fazer no amigo com o que está na mochila (a primeira pilha de cada curativo), o pior primeiro
        void RefazerAsOpcoes()
        {
            _proximaRefeita = Time.time + RefazerACada;
            var antes = _opcoes.Count > 0 ? _opcoes[Mathf.Clamp(_escolhida, 0, _opcoes.Count - 1)] : default;
            _opcoes.Clear();
            var e = _alvo.Vida.Corpo.Value;
            float vida = _alvo.Vida.Vida.Value, maxima = _alvo.Vida.vidaMaxima;
            var vistos = new HashSet<Item>();
            foreach (var it in _inventario.Mochila)
            {
                if (!PartesDoCorpo.EhTratamento(it.item) || !vistos.Add(it.item)) continue;
                // o calmante (06/11/2026): só para o amigo abalado (a mente abaixo de 70)
                if (it.item == Item.Calmante && _alvo.Vida.Sanidade.Value >= 70f) continue;
                if (PartesDoCorpo.CorpoTodo(it.item) || PartesDoCorpo.PelaBoca(it.item))
                {
                    var p = PartesDoCorpo.Sortear;
                    if (PartesDoCorpo.PodeTratar(e, vida, maxima, it.item, ref p, out _)) _opcoes.Add((it.id, it.item, p, Prioridade(e, it.item, p)));
                    continue;
                }
                for (int k = 0; k < PartesDoCorpo.Quantas; k++)
                {
                    var p = (ParteDoCorpo)k;
                    if (it.item == Item.Tala && !PartesDoCorpo.EhMembro(p)) continue;
                    var aqui = p;
                    if (PartesDoCorpo.PodeTratar(e, vida, maxima, it.item, ref aqui, out _)) _opcoes.Add((it.id, it.item, p, Prioridade(e, it.item, p)));
                }
            }
            _opcoes.Sort((a, b) => a.prioridade != b.prioridade ? a.prioridade.CompareTo(b.prioridade) : ((int)a.item).CompareTo((int)b.item));
            // a opção escolhida continua a mesma, se ainda existir
            _escolhida = 0;
            for (int i = 0; i < _opcoes.Count; i++)
                if (_opcoes[i].item == antes.item && _opcoes[i].parte == antes.parte) _escolhida = i;
        }

        static int Prioridade(in EstadoDoCorpo e, Item item, ParteDoCorpo p)
        {
            if (e.Sepse && item == Item.Antibiotico) return 0;
            if (PartesDoCorpo.Valida(p) && e.Sangra(p) && (PartesDoCorpo.Enfaixa(item) || item == Item.KitMedico)) return item == Item.BandagemAntisseptica ? 1 : 2;
            if (item == Item.Tala) return 3;
            if (item == Item.Antibiotico) return 4;
            if (item == Item.Remedio && e.ComFebre) return 5;
            if (item == Item.Calmante) return 6;   // (a mente, 06/11/2026)
            if (item == Item.KitMedico) return 6;
            return 7;
        }

        void OnGUI()
        {
            if (_alvo == null || !PodeMirar) return;
            float u = Screen.height / 720f;
            string nome = _alvo.Aparencia.Value.nome.ToString();
            if (string.IsNullOrEmpty(nome)) nome = "o amigo";
            string linha1, linha2;
            if (_alvo.Vida.Desmaiado.Value || _alvo.Vida.Dormindo.Value)
            {
                linha1 = $"Segure E: acordar {nome} ({(_alvo.Vida.Desmaiado.Value ? "desmaiado" : "dormindo")})";
                linha2 = null;
            }
            else if (_opcoes.Count == 0)
            {
                linha1 = _alvo.Vida.Corpo.Value.AlgoErrado || _alvo.Vida.Vida.Value < _alvo.Vida.vidaMaxima - 0.5f
                    ? $"{nome}: nenhum curativo da sua mochila serve agora" : $"{nome} está inteiro";
                linha2 = null;
            }
            else
            {
                var o = _opcoes[Mathf.Clamp(_escolhida, 0, _opcoes.Count - 1)];
                linha1 = $"Segure E: {Acao(o.item, o.parte)} de {nome} ({Itens.Nome(o.item)})";
                linha2 = _opcoes.Count > 1 ? $"toque E: outra opção ({_escolhida + 1} de {_opcoes.Count})" : null;
            }
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            var estilo = EstilosDoHud.Rotulo(Mathf.RoundToInt(15 * u), FontStyle.Bold, TextAnchor.UpperCenter);
            Sombreado(new Rect(cx - 360 * u, cy + 40 * u, 720 * u, 22 * u), linha1, estilo, new Color(0.98f, 0.82f, 0.36f));
            if (linha2 != null)
            {
                var menor = EstilosDoHud.Rotulo(Mathf.RoundToInt(13 * u), FontStyle.Normal, TextAnchor.UpperCenter);
                Sombreado(new Rect(cx - 360 * u, cy + 62 * u, 720 * u, 20 * u), linha2, menor, new Color(0.9f, 0.88f, 0.8f));
            }
        }

        static void Sombreado(Rect r, string texto, GUIStyle estilo, Color cor)
        {
            estilo.normal.textColor = new Color(0f, 0f, 0f, 0.75f);
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), texto, estilo);
            estilo.normal.textColor = cor;
            GUI.Label(r, texto, estilo);
        }

        // "enfaixar o braço esquerdo", "pôr a tala na perna direita", "dar o antibiótico"
        static string Acao(Item item, ParteDoCorpo p)
        {
            if (PartesDoCorpo.PelaBoca(item)) return $"dar {Itens.NomeComArtigo(item)}";
            if (PartesDoCorpo.CorpoTodo(item)) return "usar o kit médico";
            if (item == Item.Tala) return $"pôr a tala {PartesDoCorpo.NaParte(p)}";
            if (item == Item.SprayAntisseptico) return $"limpar {PartesDoCorpo.NomeComArtigo(p)}";
            return $"enfaixar {PartesDoCorpo.NomeComArtigo(p)}";
        }
    }
}
