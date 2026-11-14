using UnityEngine;
using ZombieCityCleaner.Vehicles;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// A etapa 5, a parte dos ganchos e do carro (13/11/2026; o plano aprovado em 12/11/2026), no anfitrião:
    /// - o veado no farol: à noite, o veado na frente de um carro de farol aceso (a até 45 m, no cone de ~22°) fica parado,
    ///   olhando a luz, de 2,5 a 7 s (7 em 10; os outros correm na hora), e depois dispara. O carro que não freia bate (o
    ///   atropelamento dos bichos: Carro.cs);
    /// - o filhote órfão: a mãe (o último adulto do grupo) morreu; o filhote fica perto da carcaça dela, chorando de vez em
    ///   quando; foge de quem chega muito perto e volta. As espécies que dá para domar (o lobo e os ursos) ficam marcadas
    ///   (<see cref="DadosDaEspecie.domavel"/>), e <see cref="PodeDomar"/> é o lugar da habilidade futura (sem nada na tela).
    /// </summary>
    public sealed partial class Bicho
    {
        float _paradoNoFarolAte, _proximoFarol, _proximoChoro;
        Vector3 _farolDe;
        Bicho _maeMorta;

        /// <summary>O filhote ficou sem a mãe (o último adulto do grupo morreu).</summary>
        public bool Orfao { get; internal set; }

        /// <summary>O gancho de domar (a habilidade futura): o filhote órfão de uma espécie que dá para domar, vivo.</summary>
        public bool PodeDomar => Orfao && !Morto && Dados.domavel;

        /// <summary>A mãe morreu: o filhote fica órfão, perto da carcaça dela.</summary>
        internal void FicarOrfao(Bicho mae)
        {
            Orfao = true;
            _maeMorta = mae;
        }

        // ------------------------------------------------------------------ o veado no farol

        /// <summary>O veado parado no farol (a cada decisão). Verdadeiro: está parado (não pensa em mais nada).</summary>
        bool NoFarol(float agora)
        {
            if (Especie != Especie.Veado || Morto) return false;
            if (_paradoNoFarolAte > 0f)
            {
                if (agora < _paradoNoFarolAte)
                {
                    Estado = EstadoDoBicho.Alerta;
                    _temDestino = false;
                    _velAlvo = 0f;
                    Olhar(_farolDe);
                    return true;
                }
                // acordou: dispara para longe da luz
                _paradoNoFarolAte = 0f;
                _proximoFarol = agora + 10f;
                _perigo = _farolDe;
                _perigoAte = agora + 10f;
                _fugaAte = agora + Random.Range(6f, 10f);
                Fugir(agora, true);
                return true;
            }
            if (CicloDoDia.Noite < 0.45f || agora < _proximoFarol) return false;
            _proximoFarol = agora + 0.5f;
            Vector3 p = transform.position;
            foreach (var c in Carro.Todos)
            {
                if (c == null || !c.FaroisLigados) continue;
                Vector3 para = p - c.transform.position;
                para.y = 0f;
                float d = para.magnitude;
                if (d < 2f || d > 45f) continue;
                Vector3 frente = c.transform.forward;
                frente.y = 0f;
                if (Vector3.Angle(frente, para) > 22f) continue;
                if (Random.value < 0.7f)
                {
                    _paradoNoFarolAte = agora + Random.Range(2.5f, 7f);
                    _farolDe = c.transform.position;
                    Estado = EstadoDoBicho.Alerta;
                    _temDestino = false;
                    _velAlvo = 0f;
                    Olhar(_farolDe);
                    return true;
                }
                _proximoFarol = agora + 8f;   // este não ficou: corre como sempre
                return false;
            }
            return false;
        }

        // ------------------------------------------------------------------ o filhote órfão

        /// <summary>O filhote órfão: fica perto da carcaça da mãe (enquanto ela existe), chora, foge de quem chega perto e volta.</summary>
        bool PensarOrfao(FaunaDoMundo f, float agora)
        {
            if (!Orfao || Variante != Variante.Filhote) return false;
            if (_maeMorta == null || !_maeMorta.Morto || !_maeMorta.gameObject.activeInHierarchy) return false;   // a carcaça sumiu: segue a vida
            Vector3 p = transform.position, mae = _maeMorta.transform.position;
            if (f.PerceberGente(this, out var de, out _) && Vector3.Distance(p, de) < 8f)
            {
                _perigo = de;
                _perigoAte = agora + 6f;
                Estado = EstadoDoBicho.Fugindo;
                Ir(p + Plano(p - de).normalized * 15f, Dados.correr * 0.8f);
                return true;
            }
            if (agora > _proximoChoro)
            {
                _proximoChoro = agora + Random.Range(6f, 16f);
                _proximoSom = 0f;
                Tocar(SomDoBicho.Guincho);
            }
            float d = Vector3.Distance(Plano(p), Plano(mae));
            if (d > 6f)
            {
                Estado = EstadoDoBicho.Andando;
                Ir(mae + LugarNoGrupo.normalized * 2f, d > 20f ? Dados.trotar : Dados.andar);
                return true;
            }
            // perto dela: deita do lado, ou fuça em volta
            if (Estado != EstadoDoBicho.Deitado && Random.value < 0.3f) DeitarAte(agora + Random.Range(20f, 60f));
            else if (Estado == EstadoDoBicho.Deitado && agora > _deitadoAte) Estado = EstadoDoBicho.Pastando;
            return true;
        }
    }
}
