using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// O Long, o cuspidor (01/11/2026, a pedido): não mata, ajuda as hordas. Só de noite e raro (ZombieManager.Especiais.cs).
    /// Fica longe (de 14 a 30 m: mais perto, ele se afasta; mais longe, chega; sem ver ninguém, mas com alguém a menos de
    /// 50 m, vai na direção dele até ver: ele nasce fora da vista de todos) e cospe a gosma preta do papo: primeiro nas
    /// luzes (a fogueira acesa, a tocha na mão de alguém, o farol do carro), depois em quem estiver mais perto, mirando onde
    /// ele vai estar. A gosma prende quem leva em cheio (segurar E para se soltar) e apaga a lanterna de quem pisar na poça
    /// (só volta trocando as pilhas); ZombieManager.Gosma.cs faz o voo, a poça e os efeitos. Ferido (sem morrer), ele grita:
    /// os zumbis em volta vêm, e outros chegam. O papo é um ponto fraco: acertado, estoura (ele não cospe mais e a gosma cai
    /// em volta dele). E ele odeia a luz: a lanterna nele, de perto, por <c>tempoNaLuz</c>, o petrifica, e ele se desfaz.
    /// </summary>
    public partial class Zumbi
    {
        static readonly int CuspirId = Animator.StringToHash("Cuspir");
        static readonly int GritarId = Animator.StringToHash("Gritar");
        const float LancamentoDoCuspe = 0.45f;   // do começo da animação até a gosma sair (s)

        byte _cuspes, _gritos;
        bool _semPapo, _vaiCuspir;
        float _proximoCuspe, _proximoGrito, _acaoDoLongAte, _cuspirEm, _naLuz, _desfazerEm = -1f, _incharAte, _proximaBuscaDoLong;
        Vector3 _alvoDoCuspe;
        bool _acaoNoAnimador;
        Vector3 _papoEscala0;
        bool _temPapo0;

        byte LongNaRede => (byte)((_cuspes & 3) | ((_gritos & 3) << 2) | (_semPapo ? 16 : 0));

        void ReceberLong(byte valor, bool comEfeito)
        {
            byte cuspes = (byte)(valor & 3), gritos = (byte)((valor >> 2) & 3);
            if (comEfeito && cuspes != (_cuspes & 3)) MostrarCuspe();
            if (comEfeito && gritos != (_gritos & 3)) MostrarGrito();
            _cuspes = cuspes;
            _gritos = gritos;
            _semPapo = (valor & 16) != 0;
        }

        void PrepararLong()
        {
            _proximoCuspe = Time.time + Random.Range(2f, 4f);
            _proximoGrito = 0f;
            _proximaBuscaDoLong = 0f;
        }

        void ZerarLong()
        {
            _cuspes = _gritos = 0;
            _semPapo = _vaiCuspir = false;
            _naLuz = 0f;
            _desfazerEm = -1f;
            _acaoDoLongAte = 0f;
            _acaoNoAnimador = false;
            if (papo != null && _temPapo0) papo.localScale = _papoEscala0;
        }

        // ------------------------------------------------------------------ anfitrião

        /// <summary>A decisão do Long (no lugar de perceber e perseguir): a distância e o cuspe.</summary>
        void PensarLong(float agora)
        {
            var c = A.cuspidor;
            if (agora < _acaoDoLongAte) return;   // cuspindo ou gritando
            if (_acaoNoAnimador)
            {
                _acaoNoAnimador = false;
                AplicarPernasNoAnimador();
            }
            // o jogador mais perto que ele vê (e o mais perto de todos, visto ou não)
            Vector3 olhoDele = transform.position + Vector3.up * 2.1f;
            int quem = -1, perto = -1;
            float menor = c.distancia.y + 20f, menorDeTodos = menor;
            for (int i = 0; i < _m.QuantosJogadores; i++)
            {
                ref readonly var p = ref _m.Jogadores[i];
                float d = Vector3.Distance(p.pes, transform.position);
                if (d < menorDeTodos)
                {
                    menorDeTodos = d;
                    perto = i;
                }
                if (d < menor && (d < c.distancia.x || _m.Enxerga(olhoDele, p.peito)))
                {
                    menor = d;
                    quem = i;
                }
            }
            if (quem < 0 && perto >= 0)
            {
                // ninguém à vista, mas alguém por perto (ele nasce fora da vista de todos): vai na direção dele até ver
                // (a casa no meio, a esquina); vendo, a distância e o cuspe decidem
                Decisao = $"procurando o jogador ({menorDeTodos:0} m, fora da vista)";
                if (agora >= _proximaBuscaDoLong && NoNavMesh)
                {
                    _proximaBuscaDoLong = agora + 2f;
                    Ir(_m.Jogadores[perto].pes, c.velocidade, 1f);
                    Atual = Estado.Investigando;
                }
                return;
            }
            if (quem < 0)
            {
                // ninguém: vaga devagar
                Decisao = "ninguém à vista";
                if (Atual == Estado.Parado && agora >= _fimDaPausa) Vagar(agora, A);
                else if (Atual == Estado.Vagando && Chegou()) Pausar(agora);
                return;
            }
            ref readonly var alvo = ref _m.Jogadores[quem];
            _alvo = alvo.jogador;
            Vector3 deMim = transform.position - alvo.pes;
            deMim.y = 0f;
            Vector3 fora = deMim.sqrMagnitude > 0.01f ? deMim.normalized : -transform.forward;
            if (menor < c.distancia.x)
            {
                // perto demais: se afasta (de costas para o jogador)
                Decisao = $"perto demais ({menor:0} m): se afasta";
                if (NoNavMesh && NavMesh.SamplePosition(transform.position + fora * 9f, out var hit, 4f, NavMesh.AllAreas))
                {
                    Ir(hit.position, c.velocidade * 1.4f, 0.5f);
                    Atual = Estado.Vagando;
                }
                return;
            }
            if (menor > c.distancia.y)
            {
                // longe: chega até uns 22 m
                Decisao = $"longe ({menor:0} m): chegando";
                if (NoNavMesh && NavMesh.SamplePosition(alvo.pes + fora * 22f, out var hit, 4f, NavMesh.AllAreas))
                {
                    Ir(hit.position, c.velocidade, 1f);
                    Atual = Estado.Investigando;
                }
                return;
            }
            // na distância: para, olha e cospe
            if (NoNavMesh && agente.hasPath) agente.ResetPath();
            Atual = Estado.Parado;
            OlharPara(alvo.pes);
            if (_semPapo || agora < _proximoCuspe)
            {
                Decisao = _semPapo ? "sem papo" : $"na distância ({menor:0} m): o próximo cuspe em {_proximoCuspe - agora:0.0} s";
                return;
            }
            // a luz mais forte por perto vem primeiro (ele odeia a luz); senão, o jogador mais perto (quem anda sai da frente)
            Vector3 boca = BocaDoLong;
            if (!_m.LuzParaCuspir(transform.position, c.alcanceDoCuspe, boca, out var ponto))
            {
                ponto = alvo.pes;
                if (Vector3.Distance(transform.position, ponto) > c.alcanceDoCuspe || !_m.Enxerga(boca, alvo.peito))
                {
                    Decisao = "o jogador fora do alcance do cuspe (ou atrás de algo)";
                    return;
                }
                Decisao = "cuspiu no jogador";
            }
            else Decisao = "cuspiu numa luz";
            Cuspir(ponto, agora);
        }

        Vector3 BocaDoLong
        {
            get
            {
                var cabeca = partes != null && partes.Length > CorpoDeZumbi.ParteCabeca ? partes[CorpoDeZumbi.ParteCabeca] : null;
                return cabeca != null ? cabeca.position + transform.forward * 0.12f : transform.position + Vector3.up * 2.05f + transform.forward * 0.2f;
            }
        }

        void Cuspir(Vector3 ponto, float agora)
        {
            var c = A.cuspidor;
            _alvoDoCuspe = ponto;
            _vaiCuspir = true;
            _cuspirEm = agora + LancamentoDoCuspe;
            _acaoDoLongAte = agora + 1.15f;
            _proximoCuspe = agora + Random.Range(c.intervaloDoCuspe.x, c.intervaloDoCuspe.y);
            _cuspes++;
            OlharPara(ponto);
            MostrarCuspe();
        }

        /// <summary>A cada quadro (anfitrião): a gosma sai na hora certa, e a lanterna de perto (o ponto fraco).</summary>
        void LongNoQuadro(float agora, float dt)
        {
            var c = A.cuspidor;
            if (_vaiCuspir && agora >= _cuspirEm)
            {
                _vaiCuspir = false;
                if (!_semPapo) _m.LancarGosma(BocaDoLong, _alvoDoCuspe, this);
            }
            if (_desfazerEm > 0f)
            {
                if (agora >= _desfazerEm)
                {
                    _desfazerEm = -1f;
                    Morrer(agora, false, transform.position + Vector3.up, -transform.forward, true);   // a estátua se desfaz
                }
                return;
            }
            if (Petrificado) return;
            // a lanterna nele, de perto: a luz conta enquanto o miolo do facho bate (vai baixando devagar fora dela)
            if (NaLanternaDePerto(c)) _naLuz += dt;
            else _naLuz = Mathf.Max(0f, _naLuz - dt * 0.5f);
            if (_naLuz >= c.tempoNaLuz)
            {
                _naLuz = 0f;
                PetrificarJa();
                _m.AgoniaDoLong(this);
                _desfazerEm = agora + 1.3f;
            }
        }

        bool NaLanternaDePerto(AjustesDoLong c)
        {
            Vector3 meio = transform.position + Vector3.up * 1.4f;
            foreach (var f in _m.Fachos)
            {
                if (f.carro || f.dono == null) continue;
                Vector3 d = meio - f.origem;
                float dist = d.magnitude;
                if (dist > c.alcanceDaLanterna || dist < 0.2f) continue;
                // o miolo do facho (um pouco mais largo de perto: o corpo dele é comprido)
                if (Vector3.Dot(d / dist, f.direcao) >= Mathf.Cos(Mathf.Deg2Rad * 22f)) return true;
            }
            return false;
        }

        /// <summary>Ferido (sem morrer): o grito (de tempos em tempos) e, no papo, a gosma estoura.</summary>
        void LongFerido(RegiaoDoCorpo regiao, JogadorRede autor, float agora)
        {
            if (regiao == RegiaoDoCorpo.Papo && !_semPapo)
            {
                _semPapo = true;
                _vaiCuspir = false;
                if (hitboxDoPapo != null && hitboxDoPapo.colisor != null) hitboxDoPapo.colisor.enabled = false;
                _m.EstourarPapo(papo != null ? papo.position : transform.position + Vector3.up * 1.6f, this);
            }
            if (agora >= _proximoGrito)
            {
                _proximoGrito = agora + A.cuspidor.intervaloDoGrito;
                _gritos++;
                _acaoDoLongAte = agora + 1.7f;
                _vaiCuspir = false;
                if (NoNavMesh && agente.hasPath) agente.ResetPath();
                MostrarGrito();
                _m.GritoDoLong(this, autor);
            }
        }

        // ------------------------------------------------------------------ em todas as máquinas

        void MostrarCuspe()
        {
            _incharAte = Time.time + LancamentoDoCuspe;
            _acaoNoAnimador = true;
            AplicarPernasNoAnimador();
            Tocar(CuspirId, 0.1f, 0f);
            _m?.SomDoLong(this, false);
        }

        void MostrarGrito()
        {
            _acaoNoAnimador = true;
            AplicarPernasNoAnimador();
            Tocar(GritarId, 0.1f, 0f);
            _m?.SomDoLong(this, true);
        }

        /// <summary>O papo: respira devagar, incha antes do cuspe e murcha depois de estourar.</summary>
        void PapoDepoisDaAnimacao(float dt)
        {
            if (!Copia && _acaoNoAnimador && Time.time >= _acaoDoLongAte)
            {
                _acaoNoAnimador = false;
                AplicarPernasNoAnimador();
            }
            if (Copia && _acaoNoAnimador && Time.time >= _incharAte + 1.2f)
            {
                _acaoNoAnimador = false;
                AplicarPernasNoAnimador();
            }
            if (hitboxDoPapo != null && hitboxDoPapo.colisor != null && _semPapo) hitboxDoPapo.colisor.enabled = false;
            if (papo == null) return;
            if (!_temPapo0)
            {
                _papoEscala0 = papo.localScale;
                _temPapo0 = true;
            }
            float k;
            if (_semPapo) k = 0.55f;
            else if (Time.time < _incharAte) k = 1f + 0.4f * (1f - (_incharAte - Time.time) / LancamentoDoCuspe);
            else k = 1f + 0.05f * Mathf.Sin(Time.time * 2.1f + Id);
            papo.localScale = _papoEscala0 * k;
        }
    }
}
