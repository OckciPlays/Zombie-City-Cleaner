using UnityEngine;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// A mente do castor (a etapa 3, 12/11/2026), no anfitrião. A família vive na toca (uma casa de galhos no raso do lago:
    /// FaunaDoMundo.Castores.cs) e nada em volta dela. De tempo em tempo, um adulto vai roer uma árvore da beira: rói de
    /// cabeça baixa até ela cair (o tempo é da árvore, somando os castores; ela cai pelo sistema do machado e fica o toco
    /// roído em ponta de lápis) e arrasta um galho até a toca, que cresce. Assustado (gente, zumbi, predador, o tiro), corre
    /// para a água, bate a cauda (o estalo) e mergulha por uns segundos.
    /// </summary>
    public sealed partial class Bicho
    {
        bool _roendo, _fugindoParaAgua;
        long _arvore;
        Vector3 _peDaArvore, _aguaDaFuga;
        float _raioDaArvore, _proximaRoida;

        void PensarCastor(FaunaDoMundo f, float agora, float dt)
        {
            Vector3 p = transform.position;
            if (f.Perceber(this, out var de, out _, out _, agora < _distraidoAte) && Vector3.Distance(p, de) < Dados.fuga && agora > _mergulhoAte)
                Assustar(de, agora);
            if (_fugindoParaAgua)
            {
                if (FaunaDoMundo.FunduraEm(p) > 0.55f) { Mergulhar(agora); return; }
                Estado = EstadoDoBicho.Fugindo;
                Ir(_aguaDaFuga, Dados.correr);
                return;
            }
            Vector3 toca = Grupo != null && Grupo.temToca ? Grupo.toca : p;
            // o líder troca o lugar de nadar: na água, em volta da toca
            if (Grupo != null && Grupo.lider == this && agora > Grupo.trocarPastoEm)
            {
                Grupo.trocarPastoEm = agora + Random.Range(15f, 40f);
                if (FaunaDoMundo.PontoNaAgua(toca, 1.5f, 14f, 0.5f, 3f, out var nado)) Grupo.pasto = nado;
            }
            if (agora < _mergulhoAte)
            {
                // mergulhado: nada por baixo, de volta para a toca
                Estado = EstadoDoBicho.Andando;
                Ir(toca + LugarNoGrupo * 0.5f, Dados.trotar);
                return;
            }
            if (Carregando)
            {
                // o galho até a toca: ela cresce um pouco
                Estado = EstadoDoBicho.Andando;
                float d = Vector3.Distance(Plano(p), Plano(toca));
                if (d < 2.6f)
                {
                    Carregando = false;
                    f.CrescerToca(toca, 0.012f);
                    return;
                }
                Ir(toca, FaunaDoMundo.FunduraEm(p) > 0.3f ? Dados.trotar : Dados.andar);
                return;
            }
            if (_roendo)
            {
                if (FlorestaDoMundo.Cortada(_arvore))
                {
                    // caiu (por este ou por outro castor): leva um galho
                    _roendo = false;
                    Carregando = true;
                    return;
                }
                Vector3 lado = Plano(p - _peDaArvore);
                lado = lado.sqrMagnitude > 0.01f ? lado.normalized : -transform.forward;
                Vector3 junto = _peDaArvore + lado * (_raioDaArvore + Dados.comprimento * Escala * 0.45f);
                if (Vector3.Distance(Plano(p), Plano(junto)) > 0.5f)
                {
                    Estado = EstadoDoBicho.Andando;
                    Ir(junto, Dados.andar * 1.4f);
                    return;
                }
                // roendo: de cabeça baixa, de frente para o tronco
                Estado = EstadoDoBicho.Pastando;
                _temDestino = false;
                _velAlvo = 0f;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(-lado), 90f * Mathf.Max(0.1f, dt));
                if (f.Roer(_arvore, _peDaArvore, Mathf.Max(0.1f, dt), p))
                {
                    _roendo = false;
                    Carregando = true;
                }
                return;
            }
            if (Variante != Variante.Filhote && agora > _proximaRoida)
            {
                _proximaRoida = agora + Random.Range(20f, 60f) * (FaunaDoMundo.CastorRapido ? 0.1f : 1f);
                if ((FaunaDoMundo.CastorRapido || Random.value < 0.5f) && f.ArvoreParaRoer(toca, out _arvore, out _peDaArvore, out _raioDaArvore))
                {
                    _roendo = true;
                    return;
                }
            }
            Pastar(f, agora);
        }

        /// <summary>Assustado: para a água (e, nela, a cauda batendo e o mergulho).</summary>
        void Assustar(Vector3 de, float agora)
        {
            _perigo = de;
            _perigoAte = agora + 10f;
            _roendo = false;
            if (Carregando) Carregando = false;
            Vector3 p = transform.position;
            if (FaunaDoMundo.FunduraEm(p) > 0.55f) { Mergulhar(agora); return; }
            if (FaunaDoMundo.AguaMaisPerto(p, 0.8f, 40f, out _aguaDaFuga))
            {
                _fugindoParaAgua = true;
                Estado = EstadoDoBicho.Fugindo;
                Ir(_aguaDaFuga, Dados.correr);
            }
            else Fugir(agora, true);
        }

        void Mergulhar(float agora)
        {
            _fugindoParaAgua = false;
            _proximoSom = 0f;
            Tocar(SomDoBicho.Estalo);
            _mergulhoAte = agora + Random.Range(10f, 25f);
            Estado = EstadoDoBicho.Andando;
        }
    }
}
