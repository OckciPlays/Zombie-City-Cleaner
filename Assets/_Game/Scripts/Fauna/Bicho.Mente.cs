using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// Um grupo de bichos da mesma espécie (o rebanho de veados: o macho alfa, as fêmeas e os filhotes), no anfitrião. O
    /// líder escolhe o pasto; os outros pastam em volta. Quando um foge, o alarme corre o grupo (cada um com um pequeno
    /// atraso, como a bandeira branca passando de um para outro).
    /// </summary>
    internal sealed class Grupo
    {
        public Especie especie;
        public readonly List<Bicho> membros = new List<Bicho>();
        public Bicho lider;
        public Vector3 pasto;
        public float trocarPastoEm;
        /// <summary>O alarme do grupo: de onde vem o perigo e até quando.</summary>
        public Vector3 alarmeDe;
        public float alarmeAte = -999f;
        /// <summary>A direção da fuga (o líder escolhe; os outros vão junto).</summary>
        public Vector3 fugaPara;

        // ---------------------------------------------------- a matilha (a etapa 2: Bicho.Matilha.cs)
        /// <summary>A presa que a matilha persegue, até quando, e quando o líder pensa em caçar de novo.</summary>
        public Bicho presa;
        public float cacaDesde, cacaAte, proximaCaca;
        /// <summary>A matilha já disparou atrás da presa (antes, espreita).</summary>
        public bool correndo;
        /// <summary>A carcaça em que a matilha come, até quando.</summary>
        public Bicho carcaca;
        public float comerAte;
        /// <summary>O jogador cercado (à noite, ferido ou sozinho), o giro da roda e o próximo bote (um de cada vez).</summary>
        public Player.JogadorRede alvo;
        public float rondaGiro, proximoBote, largaAte, pensouNoAlvo, ultimoTiro = -1f;
        public Bicho atacante;
        /// <summary>A matilha sem o alfa: se desfez (foge e não ataca mais por um tempo).</summary>
        public bool desfeita;
        /// <summary>O cerco do F1 (vale de dia).</summary>
        public bool cercoDeTeste;

        // ---------------------------------------------------- a água (a etapa 3: Bicho.Agua.cs)
        /// <summary>O ninho dos gansos (na margem): a fêmea choca e o macho guarda.</summary>
        public bool temNinho;
        public Vector3 ninho;
        /// <summary>O resto em que os urubus descem (o corpo do zumbi, ou a carcaça: 'restoBicho').</summary>
        public bool temResto;
        public Vector3 resto;
        public Bicho restoBicho;
        /// <summary>A toca dos castores (no raso do lago).</summary>
        public bool temToca;
        public Vector3 toca;
        /// <summary>Onde o bando pousa depois de voar (o primeiro escolhe; os outros vão junto).</summary>
        public Vector3 pouso;
        public float pousoEm = -999f;

        /// <summary>Os vivos (sem os filhotes) e a posição de 'b' entre eles (para a roda e o cerco).</summary>
        public int Adultos(Bicho b, out int indice)
        {
            int n = 0;
            indice = 0;
            foreach (var m in membros)
            {
                if (m == null || m.Morto || m.Variante == Variante.Filhote) continue;
                if (m == b) indice = n;
                n++;
            }
            return n;
        }

        public bool TemFilhote
        {
            get
            {
                foreach (var b in membros) if (b != null && !b.Morto && b.Variante == Variante.Filhote) return true;
                return false;
            }
        }

        public Vector3 Centro
        {
            get
            {
                Vector3 s = Vector3.zero;
                int n = 0;
                foreach (var b in membros)
                    if (b != null && !b.Morto)
                    {
                        s += b.transform.position;
                        n++;
                    }
                return n > 0 ? s / n : pasto;
            }
        }
    }

    /// <summary>A mente do bicho (só no anfitrião): os sentidos, o pasto, o alerta, a fuga, a investida do alfa e o ferimento.</summary>
    public sealed partial class Bicho
    {
        internal float ProximaDecisao;
        /// <summary>Na fila do F1 (FaunaDoMundo.FilaDeTodos): parado, não pensa nem anda.</summary>
        public bool Exposto { get; internal set; }
        /// <summary>O lugar dele no pasto do grupo (a distância do meio).</summary>
        internal Vector3 LugarNoGrupo;

        Vector3 _destino;
        bool _temDestino;
        float _velAlvo;
        Vector3 _perigo;
        float _perigoAte = -999f, _alertaAte, _fugaAte, _olharAte, _proximaOlhada, _deitadoAte, _proximaInvestida;
        float _desvio, _proximaSondagem, _sangrando, _paraDeSangrar, _fugindoDesde;
        /// <summary>Até quando não nota a gente (o grupo do F1 chega distraído, para dar tempo de ver).</summary>
        float _distraidoAte;
        JogadorRede _alvo;
        bool _golpeou;

        /// <summary>Não nota a gente até 'ate' (Time.time): o grupo posto pelo F1.</summary>
        internal void Distrair(float ate) => _distraidoAte = ate;

        void ZerarMente()
        {
            _temDestino = false;
            _velAlvo = 0f;
            _perigoAte = -999f;
            _alertaAte = _fugaAte = _olharAte = _deitadoAte = 0f;
            _proximaOlhada = Time.time + Random.Range(3f, 10f);
            _proximaInvestida = 0f;
            _desvio = 0f;
            _sangrando = 0f;
            _alvo = null;
            _golpeou = false;
            Exposto = false;
            _distraidoAte = 0f;
            Grupo = null;
        }

        // ------------------------------------------------------------------ a decisão (a cada 0,3 a 3 s)

        internal void Pensar(FaunaDoMundo f, float agora)
        {
            if (Morto) return;
            // o sangramento (o ferido perde vida aos poucos e pode morrer longe; às vezes estanca)
            if (_sangrando > 0f)
            {
                if (agora > _paraDeSangrar) _sangrando = 0f;
                else
                {
                    Vida -= _sangrando * Mathf.Max(0.3f, agora - _ultimaDecisao);
                    if (Vida <= 0f)
                    {
                        f.Matar(this, null);
                        return;
                    }
                }
            }
            float dt = Mathf.Clamp(agora - _ultimaDecisao, 0f, 3f);
            _ultimaDecisao = agora;
            switch (Especie)
            {
                case Especie.Salmao: PensarPeixe(f, agora, dt); return;
                case Especie.Pato:
                case Especie.Ganso: PensarAveDaAgua(f, agora, dt); return;
                case Especie.Aguia: PensarAguia(f, agora, dt); return;
                case Especie.Castor: PensarCastor(f, agora, dt); return;
                // a etapa 4 (a noite e os detalhes: Bicho.Noite.cs)
                case Especie.Sapo: PensarSapo(f, agora, dt); return;
                case Especie.Cascavel: PensarCascavel(f, agora, dt); return;
                case Especie.Guaxinim: PensarGuaxinim(f, agora, dt); return;
                case Especie.Gamba: PensarGamba(f, agora, dt); return;
                case Especie.Urubu: PensarUrubu(f, agora, dt); return;
                case Especie.Esquilo: PensarEsquilo(f, agora, dt); return;
                case Especie.Peru: PensarPeru(f, agora, dt); return;
            }
            switch (Dados.papel)
            {
                case Papel.Matilha: PensarMatilha(f, agora, dt); return;
                case Papel.Urso: PensarUrso(f, agora, dt); return;
                default: PensarPresa(f, agora, dt); return;
            }
        }

        // a presa (o veado, o coelho e os outros que só fogem): o pasto, a vigia, o alerta, a fuga e a investida do alfa
        void PensarPresa(FaunaDoMundo f, float agora, float dt)
        {
            // os sentidos: o perigo mais perto que ele notou (gente, zumbi ou predador); o distraído do F1 ainda não nota ninguém
            if (f.Perceber(this, out var de, out var quem, out bool zumbi, agora < _distraidoAte))
            {
                _perigo = de;
                _perigoAte = agora + (zumbi ? 6f : 12f);
                _alvo = zumbi ? null : quem;   // (o alfa só investe contra gente)
                AvisarGrupo(de, agora, Vector3.Distance(transform.position, de) < Dados.fuga);
            }
            bool temPerigo = agora < _perigoAte;
            if (!temPerigo && Grupo != null && agora < Grupo.alarmeAte)
            {
                _perigo = Grupo.alarmeDe;
                temPerigo = true;
            }
            float d = temPerigo ? Vector3.Distance(transform.position, _perigo) : 999f;

            switch (Estado)
            {
                case EstadoDoBicho.Investindo:
                    PensarNaInvestida(f, agora);
                    return;
                case EstadoDoBicho.Fugindo:
                    if (agora < _fugaAte && (temPerigo || d < Dados.visao)) { Fugir(agora, false); return; }
                    if (temPerigo && d < Dados.fuga * 1.6f) { Fugir(agora, true); return; }
                    // passou: trota mais um pouco e volta a pastar
                    Estado = EstadoDoBicho.Andando;
                    _velAlvo = Dados.trotar;
                    _alertaAte = agora + Random.Range(4f, 8f);
                    return;
            }

            if (temPerigo)
            {
                // perto demais: foge na hora; o alfa, com filhote no grupo, às vezes investe
                if (d < Dados.fuga * (Ferido ? 2f : 1f))
                {
                    if (PodeInvestir(agora, d)) { ComecarInvestida(agora); return; }
                    if (Grupo != null && Grupo.lider != null && Grupo.lider != this && Grupo.lider.Estado == EstadoDoBicho.Fugindo)
                        _fugaAte = Grupo.lider._fugaAte;
                    Fugir(agora, true);
                    return;
                }
                // mais longe: o alerta (para, olha, bate o casco); a chance de fugir cresce com a aproximação
                if (Estado != EstadoDoBicho.Alerta)
                {
                    Estado = EstadoDoBicho.Alerta;
                    _alertaAte = agora + Random.Range(2.5f, 6f);
                    if (Especie == Especie.Veado && Random.value < 0.35f) Tocar(SomDoBicho.Bufo);
                    if (Especie == Especie.Alce && Random.value < 0.3f) Tocar(SomDoBicho.Mugido);
                    _temDestino = false;
                    _velAlvo = 0f;
                    // o alfa vai para a frente do rebanho, entre o perigo e os outros
                    if (Alfa && Grupo != null && Grupo.TemFilhote)
                    {
                        Vector3 c = Grupo.Centro;
                        Ir(c + (_perigo - c).normalized * 5f, Dados.andar);
                    }
                }
                Olhar(_perigo);
                float perto = 1f - Mathf.InverseLerp(Dados.fuga, Dados.visao, d);
                if (Random.value < (0.08f + 0.5f * perto * perto) * Mathf.Max(0.3f, dt * 2f))
                {
                    Fugir(agora, true);
                    return;
                }
                if (agora > _alertaAte && d > Dados.fuga * 1.4f) _perigoAte = Mathf.Min(_perigoAte, agora + 3f);
                return;
            }

            // sem perigo: pasta, anda com o grupo, olha em volta de vez em quando; à noite, deita para descansar
            if (Estado == EstadoDoBicho.Alerta && agora < _alertaAte) return;
            _olhar = Mathf.MoveTowards(_olhar, 0f, 40f * dt);
            if (Estado == EstadoDoBicho.Deitado)
            {
                if (agora < _deitadoAte) return;
                Estado = EstadoDoBicho.Pastando;
            }
            else if (CicloDoDia.Noite > 0.7f && Random.value < 0.04f * dt && !Dados.noturno)
            {
                Estado = EstadoDoBicho.Deitado;
                _deitadoAte = agora + Random.Range(40f, 150f);
                _temDestino = false;
                _velAlvo = 0f;
                return;
            }
            if (agora > _proximaOlhada)
            {
                // a vigilância do rebanho: levanta a cabeça e olha em volta
                _proximaOlhada = agora + Random.Range(6f, 16f);
                Estado = EstadoDoBicho.Alerta;
                _alertaAte = agora + Random.Range(1f, 2.5f);
                _olhar = Random.Range(-50f, 50f);
                _temDestino = false;
                _velAlvo = 0f;
                return;
            }
            Pastar(f, agora);
        }

        float _ultimaDecisao;

        void Pastar(FaunaDoMundo f, float agora)
        {
            Estado = EstadoDoBicho.Pastando;
            Vector3 lugar;
            if (Grupo == null || Grupo.lider == null || Grupo.lider.Morto)
            {
                // sozinho (ou sem líder): anda devagar pelo pasto em volta
                if (!_temDestino || Random.value < 0.05f)
                    lugar = transform.position + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(3f, 12f);
                else return;
            }
            else
            {
                if (Grupo.lider == this && agora > Grupo.trocarPastoEm)
                {
                    // o líder muda o pasto: um lugar na mesma mancha de mata, a 15–35 m
                    Grupo.trocarPastoEm = agora + Random.Range(25f, 70f);
                    Grupo.pasto = f.NovoPasto(Grupo.pasto, Especie);
                }
                lugar = Grupo.pasto + LugarNoGrupo;
                if (Variante == Variante.Filhote)
                {
                    // o filhote fica colado em uma fêmea
                    foreach (var b in Grupo.membros)
                        if (b != null && !b.Morto && b.Variante == Variante.Femea)
                        {
                            lugar = b.transform.position + (transform.position - b.transform.position).normalized * 1.5f;
                            break;
                        }
                }
            }
            float dist = Vector3.Distance(Plano(transform.position), Plano(lugar));
            if (dist > 2.5f) Ir(lugar, dist > 25f ? Dados.trotar : Dados.andar * Random.Range(0.6f, 1f));
            else if (Random.value < 0.25f)
            {
                // pastando: um passinho de vez em quando
                Ir(transform.position + Quaternion.Euler(0f, Random.Range(-60f, 60f), 0f) * transform.forward * Random.Range(0.6f, 1.6f), Dados.andar * 0.4f);
            }
            else
            {
                _temDestino = false;
                _velAlvo = 0f;
            }
        }

        static Vector3 Plano(Vector3 p) => new Vector3(p.x, 0f, p.z);

        void Ir(Vector3 destino, float velocidade)
        {
            _destino = destino;
            _temDestino = true;
            _velAlvo = velocidade;
        }

        void Olhar(Vector3 ponto)
        {
            Vector3 para = transform.InverseTransformPoint(ponto);
            _olhar = Mathf.Clamp(Mathf.Atan2(para.x, para.z) * Mathf.Rad2Deg, -70f, 70f);
        }

        // ------------------------------------------------------------------ a fuga

        void Fugir(float agora, bool comecar)
        {
            if (Estado != EstadoDoBicho.Fugindo) _fugindoDesde = agora;
            Estado = EstadoDoBicho.Fugindo;
            if (comecar && agora > _fugaAte - 2f) _fugaAte = agora + Random.Range(9f, 16f);
            Vector3 p = transform.position;
            Vector3 longe = Plano(p - _perigo);
            if (longe.sqrMagnitude < 0.01f) longe = -transform.forward;
            longe.Normalize();
            // o grupo foge junto, para o lado que o líder escolheu
            if (Grupo != null)
            {
                if (Grupo.lider == this || Grupo.fugaPara == Vector3.zero || comecar && Grupo.lider == null)
                    Grupo.fugaPara = Quaternion.Euler(0f, Random.Range(-25f, 25f), 0f) * longe;
                longe = (Grupo.fugaPara * 2f + longe).normalized;
            }
            // o coelho foge em zigue-zague (cada decisão, um lado)
            if (Dados.vida == ZombieCityCleaner.Fauna.Vida.Pequeno && Dados.corpo == Corpo.Quadrupede) longe = Quaternion.Euler(0f, Random.Range(25f, 55f) * (Random.value < 0.5f ? -1f : 1f), 0f) * longe;
            float vel = Dados.correr * (Variante == Variante.Filhote ? 0.85f : 1f) * (Ferido ? 0.8f : 1f) * Random.Range(0.92f, 1.05f);
            // cansa: depois de uns 10 s fugindo, perde até um quarto da velocidade (a matilha alcança quem corre muito)
            vel *= Mathf.Lerp(1f, 0.75f, Mathf.Clamp01((agora - _fugindoDesde - 10f) / 12f));
            Ir(p + longe * 60f + (Grupo != null ? LugarNoGrupo : Vector3.zero), vel);
            if (comecar && Especie == Especie.Veado && Random.value < 0.5f) Tocar(SomDoBicho.Bufo);
            _olhar = 0f;
            Deitar(false);
        }

        void Deitar(bool deitar)
        {
            if (!deitar && Estado == EstadoDoBicho.Deitado) Estado = EstadoDoBicho.Pastando;
        }

        /// <summary>O alarme passa para o grupo (cada um reage com um pequeno atraso).</summary>
        void AvisarGrupo(Vector3 de, float agora, bool fugir)
        {
            if (Grupo == null) return;
            Grupo.alarmeDe = de;
            Grupo.alarmeAte = agora + (fugir ? 10f : 5f);
            if (!fugir) return;
            foreach (var b in Grupo.membros)
            {
                if (b == null || b == this || b.Morto) continue;
                b._perigo = de;
                b._perigoAte = agora + 8f;
                if (b.Estado != EstadoDoBicho.Fugindo && b.Estado != EstadoDoBicho.Investindo)
                    b.ProximaDecisao = Mathf.Min(b.ProximaDecisao, agora + Random.Range(0.15f, 0.7f));
            }
        }

        /// <summary>Um barulho forte (o tiro): foge na hora se estiver perto, senão fica alerta.</summary>
        internal void OuvirTiro(Vector3 ponto, float raio, float agora)
        {
            if (Morto || Exposto) return;
            float d = Vector3.Distance(transform.position, ponto);
            if (d > raio * 1.4f) return;
            if (Dados.corpo == Corpo.Ave && !Passagem)
            {
                if (d > raio) return;
                _perigo = ponto;
                _perigoAte = agora + 10f;
                if (Especie == Especie.Aguia) { Estado = EstadoDoBicho.Fugindo; _fugaAte = agora + 15f; Carregando = false; return; }
                if (Especie == Especie.Urubu) { _altitudeAlvo = 22f; _semGenteDesde = agora; return; }
                if (Especie == Especie.Peru) { _altitudeAlvo = _empoleirado ? _altitudeAlvo : 0f; if (Estado != EstadoDoBicho.Fugindo && !_empoleirado) { AvisarGrupo(ponto, agora, true); Fugir(agora, true); } return; }
                if (Estado != EstadoDoBicho.Fugindo) { AvisarGrupo(ponto, agora, true); Decolar(agora); }
                return;
            }
            if (Especie == Especie.Castor) { if (d < raio) Assustar(ponto, agora); return; }
            if (Especie == Especie.Esquilo || Especie == Especie.Guaxinim) { if (d < raio) { _perigo = ponto; _perigoAte = agora + 10f; ProximaDecisao = Mathf.Min(ProximaDecisao, agora + 0.2f); } return; }
            if (Especie == Especie.Sapo) { if (d < raio) CalarOCoro(agora); return; }
            if (Especie == Especie.Cascavel) return;
            if (Dados.papel == Papel.Matilha) { TiroNaMatilha(ponto, d < raio, agora); return; }
            if (Dados.papel == Papel.Urso) { TiroNoUrso(ponto, d < raio, agora); return; }
            _perigo = ponto;
            _perigoAte = agora + 15f;
            if (d < raio && Estado != EstadoDoBicho.Investindo)
            {
                AvisarGrupo(ponto, agora, true);
                ProximaDecisao = Mathf.Min(ProximaDecisao, agora + Random.Range(0.05f, 0.4f));
                _fugaAte = agora + Random.Range(12f, 20f);
            }
        }

        // ------------------------------------------------------------------ a investida do alfa

        bool PodeInvestir(float agora, float d)
        {
            if (_alvo == null || !_alvo.AlvoValido || agora < _proximaInvestida) return false;
            // o alce (a etapa 3): a mãe com o filhote investe em quem chega perto; o macho, quando é provocado
            bool maeDoAlce = Especie == Especie.Alce && Variante == Variante.Femea && Grupo != null && Grupo.TemFilhote;
            bool alceProvocado = Especie == Especie.Alce && Variante == Variante.Macho && Ferido;
            bool alfaDoVeado = Especie != Especie.Alce && Alfa && !Ferido && Grupo != null && Grupo.TemFilhote;
            if (!maeDoAlce && !alceProvocado && !alfaDoVeado) return false;
            if (d > (Especie == Especie.Alce ? 22f : 16f)) return false;
            _proximaInvestida = agora + 45f;
            return Random.value < (Especie == Especie.Alce ? 0.6f : FaunaDoMundo.ChanceDaInvestida);
        }

        void ComecarInvestida(float agora)
        {
            if (Especie == Especie.Alce) Tocar(SomDoBicho.Mugido);
            Estado = EstadoDoBicho.Investindo;
            _fugaAte = agora + 7f;   // o tempo máximo da investida
            _golpeou = false;
            Ir(_alvo.transform.position, Dados.correr * 0.75f);
        }

        void PensarNaInvestida(FaunaDoMundo f, float agora)
        {
            if (_alvo == null || !_alvo.AlvoValido || agora > _fugaAte || _golpeou)
            {
                _perigo = _alvo != null ? _alvo.transform.position : _perigo;
                _perigoAte = agora + 10f;
                Fugir(agora, true);
                return;
            }
            Vector3 alvo = _alvo.transform.position;
            float d = Vector3.Distance(Plano(transform.position), Plano(alvo));
            Olhar(alvo);
            if (d < 1.9f + 0.5f * Escala)
            {
                // a cabeçada com a galhada: o dano no tronco e o empurrão
                _golpeou = true;
                f.Golpear(this, _alvo);
                Ir(transform.position - (alvo - transform.position).normalized * 6f, Dados.trotar);
                return;
            }
            if (d > 28f)
            {
                _perigoAte = agora + 8f;
                Fugir(agora, true);
                return;
            }
            Ir(alvo, Dados.correr * 0.75f);
        }

        // ------------------------------------------------------------------ o ferimento

        /// <summary>Levou um tiro (ou um golpe) e não morreu: sangra, foge e às vezes estanca.</summary>
        internal void Ferir(float dano, Vector3 de, float agora, JogadorRede autor = null)
        {
            Ferido = true;
            float max = Especies.Pontos(Especie, Variante);
            // quanto maior o ferimento, mais sangra (de 0,4% a 2,5% da vida por segundo) e menos chance de estancar
            float grave = Mathf.Clamp01(dano / max);
            _sangrando = Mathf.Max(_sangrando, max * Mathf.Lerp(0.004f, 0.025f, grave));
            _paraDeSangrar = agora + (Random.value < 0.55f - 0.4f * grave ? Random.Range(25f, 60f) : 999f);
            _perigo = de;
            _perigoAte = agora + 20f;
            if (Dados.papel == Papel.Urso) { FeridoUrso(autor, agora); return; }
            if (Especie == Especie.Castor) { Assustar(de, agora); return; }
            if (Dados.corpo == Corpo.Ave && Especie != Especie.Aguia && Estado != EstadoDoBicho.Fugindo) { AvisarGrupo(de, agora, true); Decolar(agora); return; }
            if (Especie == Especie.Alce && Variante == Variante.Macho && autor != null && Estado != EstadoDoBicho.Investindo)
            {
                _alvo = autor;
                if (PodeInvestir(agora, Vector3.Distance(transform.position, autor.transform.position))) { ComecarInvestida(agora); return; }
            }
            if (Dados.papel == Papel.Matilha) { FeridoNaMatilha(autor, agora); return; }
            AvisarGrupo(de, agora, true);
            if (Estado != EstadoDoBicho.Investindo) Fugir(agora, true);
        }

        // ------------------------------------------------------------------ o movimento (a cada quadro, no anfitrião)

        static readonly float[] Desvios = { 0f, 30f, -30f, 60f, -60f, 95f, -95f, 140f, -140f, 180f };
        // o cenário (as árvores, as paredes), os carros e os jogadores
        const int MascaraDoCaminho = 1 | (1 << 11) | (1 << 9);

        internal void Andar(float dt, float agora)
        {
            Vector3 p = transform.position;
            if (Morto)
            {
                Velocidade = 0f;
                return;
            }
            float velAlvo = _temDestino ? _velAlvo : 0f;
            Vector3 para = _destino - p;
            para.y = 0f;
            float dist = para.magnitude;
            if (_temDestino && dist < 0.5f + Velocidade * 0.15f)
            {
                _temDestino = false;
                velAlvo = 0f;
            }
            Vector3 dir = dist > 0.01f ? para / dist : transform.forward;
            if (velAlvo > 0.05f && agora >= _proximaSondagem)
            {
                _proximaSondagem = agora + (velAlvo > 4f ? 0.1f : 0.25f);
                _desvio = Desviar(p, dir, velAlvo);
            }
            if (velAlvo > 0.05f) dir = Quaternion.Euler(0f, _desvio, 0f) * dir;
            // a aceleração (a fuga arranca rápido) e a freada
            float acel = Estado == EstadoDoBicho.Fugindo || Estado == EstadoDoBicho.Investindo ? Dados.correr * 1.6f : Mathf.Max(1.5f, Dados.trotar);
            Velocidade = Mathf.MoveTowards(Velocidade, velAlvo, acel * dt);
            if (Velocidade > 0.02f)
            {
                float giro = (Velocidade > Dados.trotar ? 260f : 160f) * dt;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), giro);
                p += transform.forward * Velocidade * dt;
            }
            p.y = AlturaDe(p, dt, agora);
            transform.position = p;
            if (_peixeNaBoca != null) Levar(_peixeNaBoca);
        }

        // o primeiro rumo livre (de árvore, parede, carro, gente, água funda e da borda do mapa) perto do desejado
        float Desviar(Vector3 p, Vector3 dir, float vel)
        {
            float raio = Mathf.Max(0.12f, Dados.altura * Escala * 0.22f);
            float olhar = 1.4f + vel * 0.45f + Dados.comprimento * Escala * 0.5f;
            Vector3 origem = p + Vector3.up * Mathf.Max(0.25f, Dados.altura * Escala * 0.6f);
            bool temMapa = FaunaDoMundo.TemMapa;
            bool voando = Voando || Dados.meio == Meio.Ar;
            for (int i = 0; i < Desvios.Length; i++)
            {
                Vector3 d = Quaternion.Euler(0f, _desvio * 0.5f + Desvios[i], 0f) * dir;
                if (!voando && Physics.SphereCast(origem, raio, d, out var hit, olhar, MascaraDoCaminho, QueryTriggerInteraction.Ignore)
                    && !(Estado == EstadoDoBicho.Investindo && _alvo != null && hit.collider.transform.IsChildOf(_alvo.transform))) continue;
                Vector3 adiante = p + d * olhar;
                if (temMapa)
                {
                    if (!voando && AguaBarra(FaunaDoMundo.AguaEm(adiante.x, adiante.z, out _))) continue;
                    if (!FaunaDoMundo.DentroDoMapa(adiante.x, adiante.z, 30f)) continue;
                }
                return _desvio * 0.5f + Desvios[i];
            }
            return 180f;
        }
    }
}
