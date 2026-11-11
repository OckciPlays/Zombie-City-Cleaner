using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// As melhorias das armas de sucata na mão (10/11/2026, a etapa 2; os números no <see cref="Melhorias"/>): os números
    /// da arma vêm com o que está posto nela (o <see cref="Dados"/>), as peças aparecem nos braços de primeira pessoa (o
    /// <see cref="PecasDasMelhorias"/>) e, mirando com a mira de ponto, os braços vão até a câmera ficar no olho do
    /// tubinho (como a luneta da sniper, a de cano usa o zoom e a tela da luneta: o <c>fovLuneta</c>).
    /// </summary>
    public partial class ArmaJogador
    {
        // o que está posto em cada modelo de sucata dos braços (-1: nada aplicado ainda)
        readonly int[] _pecasNosBracos = { -1, -1, -1 };
        float _pesoDaMira;
        int _modeloMirado = -1;

        /// <summary>As melhorias da arma da mão (0: nenhuma, ou não é de sucata).</summary>
        public ulong MelhoriasNaMao => ComArma && Inventario != null ? Inventario.MelhoriasDe(_id) : 0UL;

        /// <summary>Os números da arma 't' com as melhorias 'b' e as habilidades deste jogador (a Mola nova, o Mestre armeiro).</summary>
        public DadosDaArma DadosCom(TipoDeArma t, ulong b) =>
            Melhorias.Dados(t, b, Habilidades.Pontos(rede, Habilidade.MolaNova), Habilidades.Tem(rede, Habilidade.MestreArmeiro));

        /// <summary>
        /// O teste pelo MCP (10/11/2026), com o Editor sem o foco (o mouse e o teclado não chegam ao jogo): mirar como com o
        /// botão direito apertado, e os cliques do tiro e os R que faltam (cada um vale um quadro em que a arma pode).
        /// </summary>
        public static bool MirarNoTeste;
        public static int CliquesNoTeste, RecargasNoTeste;

        bool CliqueDoTeste()
        {
            if (CliquesNoTeste <= 0 || Time.time < _proximoTiro || Recarregando) return false;
            CliquesNoTeste--;
            return true;
        }

        bool RecargaDoTeste()
        {
            if (RecargasNoTeste <= 0 || Recarregando) return false;
            RecargasNoTeste--;
            return true;
        }

        /// <summary>Quanto a mira de ponto já chegou ao olho (0 a 1; o teste e a foto).</summary>
        public float PesoDaMiraDePonto => _pesoDaMira;

        /// <summary>O pente guardado da arma não passa de 'cabe' (o carregador estendido saiu).</summary>
        public void LimitarPente(ushort id, int cabe)
        {
            if (_pentes.TryGetValue(id, out int p) && p > cabe) _pentes[id] = cabe;
            if (id == _id && ComArma && Municao > cabe) Municao = cabe;
        }

        /// <summary>
        /// O espalhamento do tiro agora (graus): o da arma (mirando ou não; o rifle sem luneta de quadril), as miras
        /// postas, correndo e o corpo machucado. O mesmo do tiro (o teste mede por aqui).
        /// </summary>
        public float Abertura()
        {
            var d = Dados;
            bool semAMira = d.fovLuneta > 0f ? !NaLuneta : d.dispersaoSemLuneta > 0f && !rede.Mirando.Value;
            bool mirando = d.fovLuneta > 0f ? NaLuneta : rede.Mirando.Value;
            float mira = Melhorias.FatorDaMira(MelhoriasNaMao, mirando, semAMira);
            return (semAMira ? d.dispersaoSemLuneta : d.dispersao * mira) + (rede.Velocidade > 3f ? d.dispersaoAndando * mira * 2f : 0f)
                   + PartesDoCorpo.DispersaoExtra(Corpo);
        }

        void LateUpdate()
        {
            if (rede == null || !rede.IsOwner) return;
            PecasNosBracos();
            MirarPeloPonto();
        }

        // as peças do que está posto na arma da mão, no modelo dela (só quando muda)
        void PecasNosBracos()
        {
            if (!ComArma) return;
            int k = Melhorias.IndiceDa(Atual), m = ModeloDaMao(NaMao);
            if (k < 0 || m < 0 || m >= modelos.Length) return;
            ulong b = MelhoriasNaMao;
            if (_pecasNosBracos[k] == Melhorias.Pecas(b)) return;
            _pecasNosBracos[k] = Melhorias.Pecas(b);
            var smr = modelos[m].GetComponentInChildren<SkinnedMeshRenderer>(true);
            PecasDasMelhorias.NosBracos(k, smr, b, bocas[(int)Atual]);
            // sem o carregador estendido, o pente não passa do de sempre
            if (Municao > Dados.capacidade) Municao = Dados.capacidade;
        }

        /// <summary>
        /// Mirando com a mira de ponto: os braços vão (sem pressa: <see cref="Melhorias.VelocidadeDaMira"/>) até o olho do
        /// tubinho ficar na câmera dos braços, com o cano no meio da tela; o tiro sai do meio, onde o pontinho está.
        /// Recarregando, na terceira pessoa ou soltando o botão, eles voltam.
        /// </summary>
        void MirarPeloPonto()
        {
            int m = ComArma ? ModeloDaMao(NaMao) : -1;
            int k = ComArma ? Melhorias.IndiceDa(Atual) : -1;
            bool comPonto = m >= 0 && m < modelos.Length && k >= 0 && Melhorias.MiraDeCima(MelhoriasNaMao) == Melhorias.Ponto;
            bool quer = comPonto && rede.Mirando.Value && !Recarregando && !rede.EmTerceiraPessoa && rede.cameraBracos != null;
            _pesoDaMira = Mathf.MoveTowards(_pesoDaMira, quer ? 1f : 0f, Time.deltaTime * (quer ? Melhorias.VelocidadeDaMira * 0.5f : Melhorias.VelocidadeDaMira));
            if (_modeloMirado >= 0 && _modeloMirado != m && _modeloMirado < modelos.Length)
            {
                modelos[_modeloMirado].transform.localPosition = Vector3.zero;
                modelos[_modeloMirado].transform.localRotation = Quaternion.identity;
                _modeloMirado = -1;
            }
            if (!comPonto || _pesoDaMira <= 0f)
            {
                if (_modeloMirado >= 0) modelos[_modeloMirado].transform.localPosition = Vector3.zero;
                if (_modeloMirado >= 0) modelos[_modeloMirado].transform.localRotation = Quaternion.identity;
                _modeloMirado = -1;
                if (!comPonto) _pesoDaMira = 0f;
                return;
            }
            var d = PecasDasMelhorias.Dados;
            var raiz = modelos[m].transform;
            var smr = modelos[m].GetComponentInChildren<SkinnedMeshRenderer>(true);
            var gun = PecasDasMelhorias.OssoDaArma(smr);
            if (d == null || gun == null || d.fpCorpo == null || k >= d.fpCorpo.Length) return;
            _modeloMirado = m;
            // a pose de sempre (a da animação), e dela a mira: o olho e os eixos do tubinho no mundo
            raiz.localPosition = Vector3.zero;
            raiz.localRotation = Quaternion.identity;
            var mira = gun.localToWorldMatrix * d.fpCorpo[k];
            Vector3 olho = mira.MultiplyPoint3x4(Melhorias.OlhoDaMiraDePonto(Atual));
            var giroDaMira = Quaternion.LookRotation(mira.MultiplyVector(Vector3.forward), mira.MultiplyVector(Vector3.up));
            var cam = rede.cameraBracos.transform;
            var acerto = cam.rotation * Quaternion.Inverse(giroDaMira);
            Vector3 pos = cam.position + acerto * (raiz.position - olho);
            var rot = acerto * raiz.rotation;
            float w = Mathf.SmoothStep(0f, 1f, _pesoDaMira);
            raiz.SetPositionAndRotation(Vector3.Lerp(raiz.position, pos, w), Quaternion.Slerp(raiz.rotation, rot, w));
        }
    }
}
