using UnityEngine;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// A mente do castor (a etapa 3, 12/11/2026), no anfitrião. A família vive na toca (uma casa de galhos no raso do lago:
    /// FaunaDoMundo.Castores.cs) e nada em volta dela. De tempo em tempo, um adulto vai roer uma árvore da beira: rói de
    /// cabeça baixa até ela cair (o tempo é da árvore, somando os castores; ela cai pelo sistema do machado e fica o toco
    /// roído em ponta de lápis) e arrasta um galho até a toca, que cresce. Assustado (gente, zumbi, predador, o tiro), corre
    /// para a água, bate a cauda (o estalo) e mergulha por uns segundos. No riacho (13/11/2026), a família tem a represa:
    /// mais da metade dos galhos vai para ela, e de tempo em tempo um adulto vai remendá-la (com o barro e os gravetos), de
    /// cabeça baixa por uns segundos.
    /// </summary>
    public sealed partial class Bicho
    {
        bool _roendo, _fugindoParaAgua, _paraRepresa;
        long _arvore;
        Vector3 _peDaArvore, _aguaDaFuga, _pontoDaRepresa;
        float _raioDaArvore, _proximaRoida, _proximoReparo, _remendandoAte;

        /// <summary>Um ponto na face de cima da represa (contra a correnteza), em algum lugar da largura.</summary>
        Vector3 PontoNaRepresa(Grupo g)
        {
            var giro = Quaternion.Euler(0f, g.represaGiro, 0f);
            Vector3 atravessa = giro * Vector3.right, correnteza = giro * Vector3.forward;
            return g.represa + atravessa * Random.Range(-0.4f, 0.4f) * g.represaLargura - correnteza * 1.1f;
        }

        /// <summary>O galho vai para a represa (mais da metade das vezes) ou para a toca.</summary>
        void LevarGalho(Grupo g)
        {
            Carregando = true;
            _paraRepresa = g != null && g.temRepresa && Random.value < 0.6f;
            if (_paraRepresa) _pontoDaRepresa = PontoNaRepresa(g);
        }

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
            if (agora < _remendandoAte)
            {
                // remendando a represa: de cabeça baixa, o barro e os gravetos
                Estado = EstadoDoBicho.Pastando;
                _temDestino = false;
                _velAlvo = 0f;
                return;
            }
            if (Carregando)
            {
                // o galho até a toca (ela cresce um pouco) ou até a represa (13/11)
                Estado = EstadoDoBicho.Andando;
                Vector3 para = _paraRepresa && Grupo != null && Grupo.temRepresa ? _pontoDaRepresa : toca;
                float d = Vector3.Distance(Plano(p), Plano(para));
                if (d < (_paraRepresa ? 1.4f : 2.6f))
                {
                    Carregando = false;
                    if (_paraRepresa)
                    {
                        f.CrescerRepresa(para, 0.02f);
                        _remendandoAte = agora + Random.Range(4f, 9f);
                    }
                    else f.CrescerToca(toca, 0.012f);
                    return;
                }
                Ir(para, FaunaDoMundo.FunduraEm(p) > 0.3f ? Dados.trotar : Dados.andar);
                return;
            }
            // (13/11) o reparo da represa: de tempo em tempo, um adulto vai lá e remenda
            if (Grupo != null && Grupo.temRepresa && Variante != Variante.Filhote && agora > _proximoReparo)
            {
                if (_proximoReparo <= 0f) { _proximoReparo = agora + Random.Range(10f, 40f); }
                else
                {
                    _proximoReparo = agora + Random.Range(45f, 120f) * (FaunaDoMundo.CastorRapido ? 0.15f : 1f);
                    if (Random.value < 0.6f)
                    {
                        _pontoDaRepresa = PontoNaRepresa(Grupo);
                        _paraRepresa = true;
                        Carregando = true;   // (o barro e os gravetos da margem)
                        return;
                    }
                }
            }
            if (_roendo)
            {
                if (ArvoresDoMundo.Cortada(_arvore))
                {
                    // caiu (por este ou por outro castor): leva um galho
                    _roendo = false;
                    LevarGalho(Grupo);
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
                    LevarGalho(Grupo);
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
            _remendandoAte = 0f;
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
