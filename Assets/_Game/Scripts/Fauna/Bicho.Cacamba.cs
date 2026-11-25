using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// O guaxinim e o lixo (23/11/2026, a pedido: "o guaxinim revirando as latas menores (elas caindo após) e entrando nas
    /// latas grandes (não caindo, mas com eles dentro e depois quando chegamos perto ou mexemos no lixo eles saem correndo
    /// com um jump scare no jogador, com um barulho alto por exemplo)"), no anfitrião:
    /// <list type="bullet">
    /// <item>a lata da casa: ele chega, fica uns segundos mexendo (a lata balança, em LixoDaCidade) e ela tomba (a queda é
    /// desenhada em cada máquina, no lixo espalhado: FaunaDoMundo.Castores.cs);</item>
    /// <item>a caçamba das lojas: ele vai até a beirada, pula para dentro e fica lá uns minutos, remexendo de vez em quando
    /// (o barulho baixo, que dá para ouvir chegando perto). Quem chega a menos de <see cref="FaunaDoMundo.SustoPerto"/> m dela,
    /// ou começa a revirá-la, faz todos os de dentro saírem num pulo, um atrás do outro, para o lado de quem chegou e
    /// correndo dali (o susto: FaunaDoMundo.Lixo.cs). Sozinho, ele sai calmo e joga um pouco de lixo do lado.</item>
    /// </list>
    /// O pulo é feito aqui, pela posição (a cópia nos outros só segue o ponto que chega); dentro da caçamba, ele fica parado
    /// no fundo, escondido pelas paredes dela.
    /// </summary>
    public sealed partial class Bicho
    {
        internal enum FaseDaCacamba : byte { Fora, Indo, Entrando, Dentro, Saindo }

        /// <summary>Onde o guaxinim está com a caçamba (no anfitrião).</summary>
        internal FaseDaCacamba NaCacamba { get; private set; }
        /// <summary>A caçamba dele (o índice em <see cref="LixoDaCidade.Pontos"/>), ou -1.</summary>
        internal int Cacamba { get; private set; } = -1;

        Vector3 _meioDaCacamba;
        Quaternion _giroDaCacamba = Quaternion.identity;
        Vector3 _puloDe, _puloPara;
        float _puloDesde, _puloDura, _puloAlto, _dentroAte, _proximoRemexerDentro, _semCacambaAte;
        bool _saiuNoSusto;
        // a lata da casa: até quando ele fica mexendo antes de ela tombar, e qual (0: nenhuma)
        float _derrubandoAte;
        Vector3 _lataDerrubando;

        /// <summary>No máximo quantos guaxinins cabem numa caçamba.</summary>
        internal const int CabemNaCacamba = 3;

        /// <summary>Ele aceita entrar numa caçamba agora (depois de sair de uma, fica uns minutos sem).</summary>
        internal bool QuerCacamba(float agora) => agora >= _semCacambaAte;

        /// <summary>O guaxinim vai para a caçamba (o índice, o meio dela no chão e o giro de pé).</summary>
        internal void IrParaACacamba(int indice, Vector3 meio, float giro)
        {
            Cacamba = indice;
            _meioDaCacamba = meio;
            _giroDaCacamba = Quaternion.Euler(0f, giro, 0f);
            NaCacamba = FaseDaCacamba.Indo;
            _derrubandoAte = 0f;
        }

        /// <summary>O F1: o guaxinim já dentro da caçamba, esperando alguém chegar.</summary>
        internal void PorDentroDaCacamba(int indice, Vector3 meio, float giro, float agora)
        {
            IrParaACacamba(indice, meio, giro);
            NaCacamba = FaseDaCacamba.Dentro;
            transform.SetPositionAndRotation(DentroDaCacamba(), GiroDentro());
            _dentroAte = agora + 600f;
            _proximoRemexerDentro = agora + Random.Range(3f, 8f);
            Velocidade = 0f;
            Estado = EstadoDoBicho.Pastando;
        }

        // o ponto na borda da caçamba (no chão, por fora) na direção 'rumo' (do meio), com 'folga' m para fora
        Vector3 BeiradaDaCacamba(Vector3 rumo, float folga)
        {
            var tam = ModelosDoLixo.TamanhoDaCacamba;
            Vector3 l = Quaternion.Inverse(_giroDaCacamba) * Plano(rumo);
            if (l.sqrMagnitude < 0.0001f) l = Vector3.back;
            l.Normalize();
            float hx = tam.x * 0.5f, hz = tam.z * 0.5f;
            float s = Mathf.Min(Mathf.Abs(l.x) > 0.001f ? hx / Mathf.Abs(l.x) : 999f, Mathf.Abs(l.z) > 0.001f ? hz / Mathf.Abs(l.z) : 999f);
            Vector3 p = _meioDaCacamba + _giroDaCacamba * (l * (s + folga));
            p.y = Chao(p, out _);
            return p;
        }

        // o lugar dele no fundo da caçamba (até 3: um em cada terço, ao comprido)
        Vector3 DentroDaCacamba() => _meioDaCacamba + _giroDaCacamba * new Vector3(((Id % 3) - 1) * 0.55f, 0.14f, 0f);

        Quaternion GiroDentro() => _giroDaCacamba * Quaternion.Euler(0f, Id % 2 == 0 ? 90f : -90f, 0f);

        void ComecarPulo(Vector3 de, Vector3 para, float dura, float atraso, FaseDaCacamba fase)
        {
            _puloDe = de;
            _puloPara = para;
            _puloDesde = Time.time + atraso;
            _puloDura = dura;
            // o arco passa por cima da borda (1,3 m da caçamba), com folga para o corpo
            float topo = _meioDaCacamba.y + ModelosDoLixo.TamanhoDaCacamba.y + 0.3f;
            _puloAlto = Mathf.Max(0.4f, (topo - Mathf.Max(de.y, para.y)) / 0.85f);
            NaCacamba = fase;
            _temDestino = false;
            _velAlvo = 0f;
        }

        /// <summary>
        /// A decisão com a caçamba (no PensarGuaxinim, antes de tudo). Falso: ele não está com a caçamba (ou desistiu), e a mente
        /// de sempre decide.
        /// </summary>
        bool PensarNaCacamba(FaunaDoMundo f, float agora)
        {
            switch (NaCacamba)
            {
                case FaseDaCacamba.Indo:
                {
                    // o perigo perto antes de entrar: desiste (a mente de sempre foge ou sobe na árvore)
                    if (agora >= _distraidoAte && f.Perceber(this, out var de, out _, out _) && Vector3.Distance(transform.position, de) < Dados.fuga)
                    {
                        Desistir();
                        return false;
                    }
                    if (f.QuantosNaCacamba(Cacamba) >= CabemNaCacamba)
                    {
                        Desistir();
                        return false;
                    }
                    Vector3 p = transform.position;
                    Vector3 beirada = BeiradaDaCacamba(p - _meioDaCacamba, 0.35f);
                    float d = Vector3.Distance(Plano(p), Plano(beirada));
                    if (d > 0.45f)
                    {
                        Estado = EstadoDoBicho.Andando;
                        Ir(beirada, d > 15f ? Dados.trotar : Dados.andar);
                        return true;
                    }
                    // na beirada: o pulo para dentro
                    Estado = EstadoDoBicho.Andando;
                    ComecarPulo(p, DentroDaCacamba(), 0.75f, 0f, FaseDaCacamba.Entrando);
                    return true;
                }
                case FaseDaCacamba.Entrando:
                case FaseDaCacamba.Saindo:
                    if (agora >= _puloDesde + _puloDura) AcabarPulo(f, agora);
                    return true;
                case FaseDaCacamba.Dentro:
                    Estado = EstadoDoBicho.Pastando;
                    _temDestino = false;
                    _velAlvo = 0f;
                    // o barulho baixo de dentro (quem chega perto e presta atenção ouve)
                    if (agora >= _proximoRemexerDentro)
                    {
                        _proximoRemexerDentro = agora + Random.Range(7f, 18f);
                        if (Random.value < 0.6f) Tocar(SomDoBicho.Remexer);
                    }
                    if (agora >= _dentroAte) SairDaCacamba(f, agora, null, 0f);
                    return true;
            }
            return false;
        }

        void Desistir()
        {
            NaCacamba = FaseDaCacamba.Fora;
            Cacamba = -1;
            _semCacambaAte = Time.time + Random.Range(60f, 120f);
        }

        void AcabarPulo(FaunaDoMundo f, float agora)
        {
            if (NaCacamba == FaseDaCacamba.Entrando)
            {
                NaCacamba = FaseDaCacamba.Dentro;
                transform.SetPositionAndRotation(DentroDaCacamba(), GiroDentro());
                // fica uns minutos (de 4 a 10), remexendo
                _dentroAte = agora + Random.Range(240f, 600f);
                _proximoRemexerDentro = agora + Random.Range(4f, 10f);
                Velocidade = 0f;
                return;
            }
            // saiu: no chão de novo
            transform.position = _puloPara;
            int indice = Cacamba;
            NaCacamba = FaseDaCacamba.Fora;
            Cacamba = -1;
            _semCacambaAte = agora + Random.Range(180f, 420f);
            if (_saiuNoSusto)
            {
                // correndo para longe de quem chegou
                _perigoAte = agora + 10f;
                Fugir(agora, true);
                return;
            }
            // calmo: joga um pouco de lixo do lado e vai embora andando
            var pontos = LixoDaCidade.Pontos;
            if (f != null && indice >= 0 && indice < pontos.Count) f.ReviraLixeira(LixoDaCidade.OndeEspalha(pontos[indice]), (byte)TipoDeLixo.Cacamba);
            Estado = EstadoDoBicho.Andando;
            Ir(transform.position + Plano(transform.position - _meioDaCacamba).normalized * 10f, Dados.trotar);
        }

        /// <summary>
        /// Sai da caçamba num pulo. 'de': quem assustou (null: sai calmo, sozinho): o pulo vai para o lado dele, de viés (passa
        /// perto e corre para longe). 'atraso' (s): os de dentro saem um atrás do outro. Falso se ele não estava dentro.
        /// </summary>
        internal bool SairDaCacamba(FaunaDoMundo f, float agora, Vector3? de, float atraso)
        {
            if (NaCacamba != FaseDaCacamba.Dentro && NaCacamba != FaseDaCacamba.Entrando) return false;
            _saiuNoSusto = de.HasValue;
            Vector3 rumo;
            if (de.HasValue)
            {
                Vector3 para = Plano(de.Value - _meioDaCacamba);
                if (para.sqrMagnitude < 0.01f) para = _giroDaCacamba * Vector3.back;
                rumo = Quaternion.Euler(0f, Random.value < 0.5f ? -60f : 60f, 0f) * para.normalized;
                _perigo = de.Value;
            }
            else rumo = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            Vector3 comeco = NaCacamba == FaseDaCacamba.Dentro ? DentroDaCacamba() : transform.position;
            Estado = de.HasValue ? EstadoDoBicho.Fugindo : EstadoDoBicho.Andando;
            ComecarPulo(comeco, BeiradaDaCacamba(rumo, 0.9f), de.HasValue ? 0.45f : 0.7f, atraso, FaseDaCacamba.Saindo);
            return true;
        }

        /// <summary>
        /// O movimento com a caçamba (no Andar, a cada quadro, antes de tudo): o pulo pelo arco e o parado lá dentro. Falso:
        /// ele não está com a caçamba (ou ainda vai até ela andando), e o andar de sempre segue.
        /// </summary>
        bool MoverNaCacamba(float agora)
        {
            switch (NaCacamba)
            {
                case FaseDaCacamba.Dentro:
                    transform.SetPositionAndRotation(DentroDaCacamba(), GiroDentro());
                    Velocidade = 0f;
                    return true;
                case FaseDaCacamba.Entrando:
                case FaseDaCacamba.Saindo:
                {
                    float u = Mathf.Clamp01((agora - _puloDesde) / Mathf.Max(0.05f, _puloDura));
                    Vector3 p = Vector3.Lerp(_puloDe, _puloPara, u) + Vector3.up * (Mathf.Sin(u * Mathf.PI) * _puloAlto);
                    Vector3 dir = Plano(_puloPara - _puloDe);
                    transform.SetPositionAndRotation(p, dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir) : transform.rotation);
                    Velocidade = agora < _puloDesde ? 0f : dir.magnitude / Mathf.Max(0.05f, _puloDura);
                    if (u >= 1f) AcabarPulo(FaunaDoMundo.Instancia, agora);
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------ a lata da casa

        /// <summary>
        /// Na lata da casa: fica de 1,6 a 3 s mexendo (a lata balança nas telas) antes de ela tombar. Verdadeiro enquanto mexe;
        /// falso quando é hora de ela tombar.
        /// </summary>
        bool MexendoNaLata(Vector3 lata, float agora)
        {
            if (_derrubandoAte <= 0f || (Plano(_lataDerrubando) - Plano(lata)).sqrMagnitude > 0.25f)
            {
                _derrubandoAte = agora + Random.Range(1.6f, 3f);
                _lataDerrubando = lata;
                Tocar(SomDoBicho.Remexer);
            }
            if (agora < _derrubandoAte)
            {
                Estado = EstadoDoBicho.Pastando;
                _temDestino = false;
                _velAlvo = 0f;
                Vector3 para = Plano(lata - transform.position);
                if (para.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(para);
                return true;
            }
            _derrubandoAte = 0f;
            return false;
        }
    }
}
