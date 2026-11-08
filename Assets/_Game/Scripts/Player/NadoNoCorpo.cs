using UnityEngine;
using ZombieCityCleaner.Combat;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O nado visto e ouvido (07/11/2026, a pedido: "atualizar a parte de nadar"), em todas as máquinas, para cada jogador
    /// na água funda:
    /// <list type="bullet">
    /// <item>o tchibum ao entrar (o respingo e o som <c>Agua_Entrar</c>) e a água escorrendo ao sair (<c>Agua_Sair</c>);</item>
    /// <item>a cada braçada, o barulho dela (<c>Agua_Bracada_0..3</c>) e uma ondinha em volta, no ritmo do nado (1,2 s; o
    /// rápido, 0,8 s); parado, só boiando, uma ondinha mais fraca a cada 2 s;</item>
    /// <item>a puxada de ar de quem sobe depois de um tempo embaixo (<c>Agua_Respirar</c>);</item>
    /// <item>o corpo nadando: o parâmetro "Nadando" do controlador do corpo (e a camada dos braços sem peso), quando o
    /// controlador tem; o que está na mão some do corpo (como no carro) e volta na saída.</item>
    /// </list>
    /// Na água funda: no dono, o <see cref="MovimentoPrimeiraPessoa.Nadando"/>; nos outros, a mesma conta pela posição que
    /// chega (a fundura acima dos pés, a mesma em todas as máquinas). Os sons provisórios vêm de Ferramentas/agua_sons.py.
    /// </summary>
    public class NadoNoCorpo : MonoBehaviour
    {
        JogadorRede _rede;
        MovimentoPrimeiraPessoa _movimento;
        VidaJogador _vida;
        bool _naAgua, _temParametro, _procurouParametro;
        float _ciclo, _embaixoDesde = -1f, _nivel;
        Animator _animadorVisto;
        RuntimeAnimatorController _controladorVisto;

        static readonly int NadandoId = Animator.StringToHash("Nadando");
        static readonly int RapidoId = Animator.StringToHash("NadandoRapido");
        const int CamadaDosBracos = 1;
        // quem passa disto (m/s) na água está nadando rápido (o normal vai até 1,9; o rápido, 2,8)
        const float RapidoAcimaDe = 2.3f;

        /// <summary>O jogador está nadando (na água funda, fora do carro e do caiaque), nesta máquina.</summary>
        public bool NaAgua => _naAgua;

        void Awake()
        {
            _rede = GetComponent<JogadorRede>();
            _movimento = GetComponent<MovimentoPrimeiraPessoa>();
            _vida = GetComponent<VidaJogador>();
        }

        void Update()
        {
            if (_rede == null || !_rede.IsSpawned) return;
            bool antes = _naAgua;
            _naAgua = NaAguaAgora();
            var pos = transform.position;
            Vector3 naTona = new Vector3(pos.x, _nivel + 0.02f, pos.z);
            if (_naAgua && !antes) Entrou(naTona);
            else if (!_naAgua && antes) Saiu(naTona);
            CorpoNadando();
            if (!_naAgua)
            {
                _embaixoDesde = -1f;
                return;
            }
            // a braçada: o mesmo ritmo dos braços e da câmera (no dono, o do movimento)
            float vel = _rede.Velocidade;
            bool nadando = vel > 0.35f;
            float periodo = !nadando ? MovimentoPrimeiraPessoa.BoiandoParado
                          : vel > RapidoAcimaDe ? MovimentoPrimeiraPessoa.BracadaRapida : MovimentoPrimeiraPessoa.BracadaNormal;
            float antesDoCiclo = _ciclo;
            if (_rede.IsOwner && _movimento != null && _movimento.Nadando) _ciclo = _movimento.CicloDaBracada;
            else _ciclo = Mathf.Repeat(_ciclo + Time.deltaTime / periodo, 1f);
            // a braçada empurra no meio do ciclo (a cabeça sobe ali, na câmera do dono): o som e a onda saem nessa hora
            if (antesDoCiclo < 0.45f && _ciclo >= 0.45f)
            {
                if (nadando)
                {
                    Efeitos.Som(Sons.Bracadas, naTona, vel > RapidoAcimaDe ? 0.75f : 0.55f, true, 28f);
                    AguaNaSuperficie.Onda(naTona, vel > RapidoAcimaDe ? 1.3f : 1f);
                    AguaNaSuperficie.Respingo(naTona + transform.forward * 0.55f, 6, 0.6f);
                }
                else AguaNaSuperficie.Onda(naTona, 0.55f);
            }
            // a puxada de ar: quem ficou mais de 2 s com a cabeça embaixo e subiu
            bool embaixo = Agua.Submerso(pos);
            if (embaixo && _embaixoDesde < 0f) _embaixoDesde = Time.time;
            else if (!embaixo && _embaixoDesde >= 0f)
            {
                if (Time.time - _embaixoDesde > 2f) Efeitos.Som(Sons.Respirar, naTona + Vector3.up * 0.3f, 0.8f, !_rede.IsOwner, 22f);
                _embaixoDesde = -1f;
            }
        }

        bool NaAguaAgora()
        {
            if (_vida != null && _vida.Caido.Value) return false;
            if (_rede.Passageiro != null && _rede.Passageiro.NoCarro) return false;
            if (PassageiroDoCaiaque.Sentado(this)) return false;
            var pos = transform.position;
            if (!Agua.Nivel(pos, out _nivel)) return false;
            if (_rede.IsOwner && _movimento != null && _movimento.enabled) return _movimento.Nadando;
            float fundo = _nivel - pos.y;
            return fundo > (_naAgua ? 1.05f : 1.25f);
        }

        void Entrou(Vector3 naTona)
        {
            float queda = Mathf.Clamp01(_rede.Velocidade / 4f) * 0.5f + 0.7f;
            Efeitos.Som(Sons.Entrar, naTona, 0.9f, true, 40f);
            AguaNaSuperficie.Respingo(naTona, Mathf.RoundToInt(26 * queda), 1.1f * queda);
            AguaNaSuperficie.Onda(naTona, 1.6f);
            _ciclo = 0f;
            _rede.EsconderNaMao(true);   // a arma (ou o item) fica guardada enquanto ele nada
        }

        void Saiu(Vector3 naTona)
        {
            Efeitos.Som(Sons.Sair, naTona + Vector3.up * 0.5f, 0.6f, true, 25f);
            AguaNaSuperficie.Onda(naTona, 0.8f);
            // a mão volta, menos com o pneu nas mãos (ele esconde também) e sentado (o carro e o caiaque cuidam)
            bool pneu = _rede.Inventario != null && _rede.Inventario.CarregandoPneu;
            bool sentado = (_rede.Passageiro != null && _rede.Passageiro.NoCarro) || PassageiroDoCaiaque.Sentado(this);
            if (!pneu && !sentado) _rede.EsconderNaMao(false);
        }

        // o corpo nadando: só no controlador que tem o parâmetro "Nadando" (o da água; sem ele, nada muda)
        void CorpoNadando()
        {
            var animador = _rede.animadorCorpo;
            if (animador == null || !animador.isActiveAndEnabled) return;
            if (animador != _animadorVisto || animador.runtimeAnimatorController != _controladorVisto)
            {
                _animadorVisto = animador;
                _controladorVisto = animador.runtimeAnimatorController;
                _temParametro = false;
                foreach (var p in animador.parameters)
                    if (p.nameHash == NadandoId) _temParametro = true;
                _procurouParametro = true;
            }
            if (!_procurouParametro || !_temParametro) return;
            if (animador.GetBool(NadandoId) != _naAgua)
            {
                animador.SetBool(NadandoId, _naAgua);
                if (animador.layerCount > CamadaDosBracos) animador.SetLayerWeight(CamadaDosBracos, _naAgua ? 0f : 1f);
            }
            animador.SetBool(RapidoId, _naAgua && _rede.Velocidade > RapidoAcimaDe);
        }

        // os sons da água (Resources/Agua), carregados uma vez
        static class Sons
        {
            static AudioClip[] _bracadas;
            static AudioClip _entrar, _sair, _respirar;
            static bool _carregou;

            static void Carregar()
            {
                if (_carregou) return;
                _carregou = true;
                _bracadas = new AudioClip[4];
                for (int i = 0; i < 4; i++) _bracadas[i] = Resources.Load<AudioClip>($"Agua/Agua_Bracada_{i}");
                _entrar = Resources.Load<AudioClip>("Agua/Agua_Entrar");
                _sair = Resources.Load<AudioClip>("Agua/Agua_Sair");
                _respirar = Resources.Load<AudioClip>("Agua/Agua_Respirar");
            }

            public static AudioClip[] Bracadas { get { Carregar(); return _bracadas; } }
            public static AudioClip Entrar { get { Carregar(); return _entrar; } }
            public static AudioClip Sair { get { Carregar(); return _sair; } }
            public static AudioClip Respirar { get { Carregar(); return _respirar; } }
        }
    }
}
