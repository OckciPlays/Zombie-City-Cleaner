using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// A mobília (Plano_Mapa, "A mobília", primeira fatia): os móveis nos pontos dos móveis das obras
    /// (<see cref="MoveisDaObra"/>). Os modelos vêm do Cidade_Mobilia.fbx (o cidade_mobilia.py da pasta de arte), um por
    /// tipo de ponto, com as variações (as cores dos sofás e dos cobertores, o guarda-roupa grande e o pequeno, as
    /// prateleiras da garagem, da loja, da farmácia e da rouparia, o balcão com a caixa registradora nas lojas), cada um
    /// feito no tamanho do catálogo, com a origem no chão, no meio, e a frente para +z (a do ponto).
    ///  - <see cref="Registrar"/> (na thread principal, no começo: <c>MobiliaDoMundo</c>) copia a malha de cada modelo;
    ///    depois tudo aqui só lê, e as obras são montadas em várias threads.
    ///  - <see cref="Por"/> põe o modelo de cada ponto no desenho (escalado para o tamanho do ponto, girado e sorteado
    ///    pela semente da obra: o mesmo móvel em todas as máquinas) e uma caixa na colisão (o NavMesh desvia dela).
    /// Sem os modelos registrados (os testes que não os pedem), as obras ficam vazias como antes.
    /// </summary>
    public static class Mobilia
    {
        sealed class Modelo
        {
            public MalhaDeObra Malha;
            public Vector3 Tamanho;
        }

        // os modelos registrados: trocados de uma vez (quem está montando em outra thread fica com o dicionário velho)
        static volatile Dictionary<string, Modelo> _modelos = new Dictionary<string, Modelo>();

        /// <summary>
        /// O tamanho em que cada modelo foi feito (largura, altura, fundo): o do catálogo do ponto dele (o MOBILIA do
        /// cidade_mobilia.py; ao mudar lá, mude aqui). O móvel é escalado do tamanho dele para o do ponto.
        /// </summary>
        public static readonly Dictionary<string, Vector3> Feitos = new Dictionary<string, Vector3>
        {
            ["Sofa_Verde"] = new Vector3(2.0f, 0.85f, 0.9f), ["Sofa_Marrom"] = new Vector3(2.0f, 0.85f, 0.9f), ["Sofa_Azul"] = new Vector3(2.0f, 0.85f, 0.9f),
            ["Poltrona_Vinho"] = new Vector3(0.85f, 0.9f, 0.85f), ["Poltrona_Verde"] = new Vector3(0.85f, 0.9f, 0.85f),
            ["Rack"] = new Vector3(1.6f, 0.6f, 0.45f), ["Estante"] = new Vector3(1.0f, 1.9f, 0.4f),
            ["Fogao"] = new Vector3(0.75f, 0.9f, 0.65f), ["Pia"] = new Vector3(1.4f, 0.9f, 0.6f),
            ["Geladeira"] = new Vector3(0.75f, 1.8f, 0.7f), ["Frigobar"] = new Vector3(0.5f, 0.85f, 0.5f),
            ["Armario"] = new Vector3(1.0f, 1.9f, 0.45f), ["Mesa"] = new Vector3(1.2f, 0.75f, 0.8f),
            ["Cama_Azul"] = new Vector3(1.6f, 0.55f, 2.05f), ["Cama_Vinho"] = new Vector3(1.6f, 0.55f, 2.05f),
            ["CamaDeSolteiro_Verde"] = new Vector3(0.95f, 0.55f, 2.0f), ["CamaDeSolteiro_Marrom"] = new Vector3(0.95f, 0.55f, 2.0f),
            ["GuardaRoupa"] = new Vector3(1.6f, 2.1f, 0.6f), ["GuardaRoupa_Pequeno"] = new Vector3(1.0f, 2.1f, 0.6f),
            ["CriadoMudo"] = new Vector3(0.45f, 0.55f, 0.45f), ["Comoda"] = new Vector3(1.0f, 0.9f, 0.5f),
            ["Vaso"] = new Vector3(0.4f, 0.8f, 0.7f), ["PiaDeBanheiro"] = new Vector3(0.6f, 0.85f, 0.45f),
            ["Chuveiro"] = new Vector3(0.9f, 2.1f, 0.9f), ["Tanque"] = new Vector3(0.6f, 0.9f, 0.55f),
            ["MaquinaDeLavar"] = new Vector3(0.6f, 0.85f, 0.6f),
            ["Prateleira"] = new Vector3(1.2f, 1.8f, 0.45f), ["Prateleira_Loja"] = new Vector3(1.5f, 1.9f, 0.45f),
            ["Prateleira_Farmacia"] = new Vector3(1.2f, 1.9f, 0.4f), ["Prateleira_Rouparia"] = new Vector3(1.2f, 1.9f, 0.45f),
            ["Bancada"] = new Vector3(1.5f, 0.9f, 0.6f), ["Caixas_A"] = new Vector3(0.6f, 0.6f, 0.6f), ["Caixas_B"] = new Vector3(0.6f, 0.6f, 0.6f),
            ["Balcao"] = new Vector3(1.8f, 1.1f, 0.6f), ["Balcao_Caixa"] = new Vector3(1.6f, 1.0f, 0.6f),
            ["Gondola"] = new Vector3(0.9f, 1.6f, 2.7f), ["Freezer"] = new Vector3(1.6f, 0.9f, 0.8f),
            ["ExpositorRefrigerado"] = new Vector3(1.2f, 2.0f, 0.7f), ["ExpositorDeArmas"] = new Vector3(1.5f, 2.0f, 0.35f),
            ["Vitrine"] = new Vector3(1.4f, 1.0f, 0.6f), ["EstanteDeAco"] = new Vector3(1.8f, 2.2f, 0.6f),
            ["ArmarioDeArmas"] = new Vector3(1.0f, 2.0f, 0.5f), ["CaixasDeMunicao"] = new Vector3(0.6f, 0.4f, 0.4f),
            ["MesaDeCorte"] = new Vector3(1.6f, 0.9f, 0.7f), ["MesaDeEscritorio"] = new Vector3(1.4f, 0.75f, 0.7f),
            ["Arquivo"] = new Vector3(0.5f, 1.3f, 0.6f), ["Cofre"] = new Vector3(0.6f, 1.0f, 0.6f),
            ["Maca"] = new Vector3(0.8f, 0.9f, 2.0f), ["ArmarioDeRemedios"] = new Vector3(1.0f, 1.9f, 0.45f),
            ["BancoDeEspera"] = new Vector3(1.8f, 0.85f, 0.6f), ["AparelhoDeRaioX"] = new Vector3(1.2f, 1.8f, 2.2f),
            ["CamaDeHospital"] = new Vector3(1.0f, 0.8f, 2.1f),
        };

        /// <summary>
        /// Quanto a caixa de colisão de um móvel fica para dentro do ponto, de cada lado (m): com a folga de quem anda do
        /// <see cref="MoveisDaObra"/> (0,3 m), o vão entre dois móveis fica com 1,05 m para o NavMesh (o zumbi tem 0,35 m de
        /// raio, e o NavMesh, com as células de 12 cm, pode perder uma de cada lado: com 5 cm, um canto entre a ponta da
        /// folha de uma porta e um guarda-roupa fechava). O móvel passa 15 cm da caixa: quem anda (com o raio dele) não
        /// chega a entrar nele.
        /// </summary>
        public const float Recuo = 0.15f;

        /// <summary>Os modelos já estão registrados (as obras ganham os móveis).</summary>
        public static bool Pronta => _modelos.Count > 0;
        public static int Quantos => _modelos.Count;

        /// <summary>
        /// Copia as malhas dos modelos (lidas da Unity: precisam estar legíveis) pelos nomes. Na thread principal, antes de
        /// montar as obras (ou de novo, trocando todos).
        /// </summary>
        public static void Registrar(IEnumerable<Mesh> malhas)
        {
            var novos = new Dictionary<string, Modelo>();
            if (malhas != null)
                foreach (var m in malhas)
                {
                    if (m == null || !Feitos.TryGetValue(m.name, out var tam) || !m.isReadable) continue;
                    novos[m.name] = new Modelo
                    {
                        Malha = MalhaDeObra.De(m.vertices, m.normals, m.uv, m.triangles),
                        Tamanho = tam,
                    };
                }
            _modelos = novos;
        }

        /// <summary>Esquece os modelos (as obras voltam a ficar vazias).</summary>
        public static void Limpar() => _modelos = new Dictionary<string, Modelo>();

        /// <summary>
        /// O nome do modelo do ponto (null: o tipo não tem modelo), com a variação sorteada por 'sorte'. Os tipos são os
        /// do catálogo do <see cref="MoveisDaObra"/>; onde o mesmo tipo vai em lugares diferentes, o cômodo (e, na
        /// prateleira do salão, o fundo: só a da farmácia tem 0,4 m) escolhe o modelo.
        /// </summary>
        public static string ModeloDo(PontoDeMovel p, uint sorte)
        {
            string Um(params string[] nomes) => nomes[sorte % (uint)nomes.Length];
            bool deLoja = p.comodo == "salao" || p.comodo == "loja";
            return p.tipo switch
            {
                "sofa" => Um("Sofa_Verde", "Sofa_Marrom", "Sofa_Azul"),
                "poltrona" => Um("Poltrona_Vinho", "Poltrona_Verde"),
                "rack" => "Rack",
                "estante" => "Estante",
                "fogao" => "Fogao",
                "pia" => "Pia",
                "geladeira" => "Geladeira",
                "frigobar" => "Frigobar",
                "armario" => "Armario",
                "mesa" => "Mesa",
                "cama" => Um("Cama_Azul", "Cama_Vinho"),
                "cama de solteiro" => Um("CamaDeSolteiro_Verde", "CamaDeSolteiro_Marrom"),
                "guarda-roupa" => p.tamanho.x > 1.3f ? "GuardaRoupa" : "GuardaRoupa_Pequeno",
                "criado-mudo" => "CriadoMudo",
                "comoda" => "Comoda",
                "vaso" => "Vaso",
                "pia de banheiro" => "PiaDeBanheiro",
                "chuveiro" => "Chuveiro",
                "tanque" => "Tanque",
                "maquina de lavar" => "MaquinaDeLavar",
                "prateleira" => p.comodo == "rouparia" ? "Prateleira_Rouparia" : !deLoja ? "Prateleira" : p.tamanho.z < 0.42f ? "Prateleira_Farmacia" : "Prateleira_Loja",
                "bancada" => "Bancada",
                "caixas" => Um("Caixas_A", "Caixas_B"),
                "balcao" => deLoja ? "Balcao_Caixa" : "Balcao",
                "gondola" => "Gondola",
                "freezer" => "Freezer",
                "expositor refrigerado" => "ExpositorRefrigerado",
                "expositor de armas" => "ExpositorDeArmas",
                "vitrine" => "Vitrine",
                "estante de aco" => "EstanteDeAco",
                "armario de armas" => "ArmarioDeArmas",
                "caixas de municao" => "CaixasDeMunicao",
                "mesa de corte" => "MesaDeCorte",
                "mesa de escritorio" => "MesaDeEscritorio",
                "arquivo" => "Arquivo",
                "cofre" => "Cofre",
                "maca" => "Maca",
                "armario de remedios" => "ArmarioDeRemedios",
                "banco de espera" => "BancoDeEspera",
                "aparelho de raio-x" => "AparelhoDeRaioX",
                "cama de hospital" => "CamaDeHospital",
                _ => null,
            };
        }

        /// <summary>A sorte do ponto i de uma obra (a mesma em todas as máquinas).</summary>
        public static uint Sorte(int semente, int i) => (uint)(Sorteio.Misturar(((ulong)(uint)semente << 20) ^ (ulong)(i + 1) * 0x9E3779B97F4A7C15UL) >> 32);

        /// <summary>
        /// Os móveis dos pontos (nas coordenadas da obra, sem espelhar): o modelo de cada um no 'desenho' (pode ser null)
        /// e uma caixa do tamanho do ponto na 'colisao' (pode ser null). Seguro em várias threads.
        /// </summary>
        public static void Por(List<PontoDeMovel> pontos, MalhaDeObra desenho, MalhaDeObra colisao, int semente)
        {
            var modelos = _modelos;
            if (modelos.Count == 0 || pontos == null) return;
            for (int i = 0; i < pontos.Count; i++)
            {
                var p = pontos[i];
                string nome = ModeloDo(p, Sorte(semente, i));
                if (nome == null || !modelos.TryGetValue(nome, out var m)) continue;
                var giro = Quaternion.Euler(0f, p.giro, 0f);
                if (desenho != null)
                {
                    var escala = new Vector3(p.tamanho.x / m.Tamanho.x, p.tamanho.y / m.Tamanho.y, p.tamanho.z / m.Tamanho.z);
                    desenho.Juntar(m.Malha, Matrix4x4.TRS(p.centro, giro, escala));
                }
                // a caixa um pouco menor que o ponto: o vão entre dois móveis fica largo para o zumbi no NavMesh
                colisao?.Caixa(Matrix4x4.TRS(p.centro + Vector3.up * (p.tamanho.y / 2), giro, Vector3.one),
                               new Vector3(Mathf.Max(0.1f, p.tamanho.x - 2 * Recuo), p.tamanho.y, Mathf.Max(0.1f, p.tamanho.z - 2 * Recuo)), "Concreto");
            }
        }
    }
}
